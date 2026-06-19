using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Core.DataAccess.Abstract;
using MoneyTransferService.DataAccess.Abstract;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete;

public class TransferService(
    IUnitOfWork unitOfWork,
    IValidator<TransferRequest> transferRequestValidator,
    ITransferBusinessRules transferBusinessRules,
    ITransferAuditRepository auditRepository) : ITransferService
{
    public async Task<Transfer> TransferAsync(
        Guid senderAccountId,
        Guid receiverAccountId,
        decimal amount,
        string currencyCode,
        string idempotencyKey,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TransferRequest(
            senderAccountId,
            receiverAccountId,
            amount,
            currencyCode,
            idempotencyKey,
            description
        );

        await ValidateRequestAsync(request, cancellationToken);

        var transferRepository = unitOfWork.GetRepository<Transfer>();
        Transfer? existingTransfer = await GetExistingTransferAsync(request, transferRepository, cancellationToken);
        if (existingTransfer != null)
        {
            return existingTransfer;
        }

        var accountRepository = unitOfWork.GetRepository<Account>();
        var transferAccounts = await GetTransferAccountsAsync(accountRepository, request, cancellationToken);

        await EnsureTransferCanBeCompletedAsync(request, transferAccounts);

        var transfer = CreatePendingTransfer(request, transferAccounts);

        await auditRepository.LogTransferAsync(transfer, AuditEventType.INITIATED);

        ApplyTransferBalanceChanges(request, transferAccounts, accountRepository);

        await CompleteTransferAsync(transfer, transferRepository, cancellationToken);

        await SaveTransferAsync(request, transferAccounts, transfer, cancellationToken);

        await auditRepository.LogTransferAsync(transfer, AuditEventType.COMPLETED);

        return transfer;
    }

    private async Task SaveTransferAsync(
        TransferRequest request,
        TransferAccounts transferAccounts,
        Transfer transfer,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var failedTransfer = CreateFailedTransfer(
                request,
                "Optimistic concurrency version conflict during save.",
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount,
                transfer.Id);
            await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);
            throw new ConcurrencyException(failedTransfer.FailureReason!, ex);
        }
    }

    private static async Task CompleteTransferAsync(Transfer transfer, IRepository<Transfer> transferRepository, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.COMPLETED;
        transfer.CompletedAt = DateTimeOffset.UtcNow;
        await transferRepository.AddAsync(transfer, cancellationToken);
    }

    private async Task EnsureTransferCanBeCompletedAsync(TransferRequest request, TransferAccounts transferAccounts)
    {
        transferBusinessRules.EnsureAccountIsActive(transferAccounts.SenderAccount, AccountRole.SENDER);
        transferBusinessRules.EnsureAccountIsActive(transferAccounts.ReceiverAccount, AccountRole.RECEIVER);
        transferBusinessRules.EnsureCurrencyMatches(transferAccounts.SenderAccount, AccountRole.SENDER, request.CurrencyCode);
        transferBusinessRules.EnsureCurrencyMatches(transferAccounts.ReceiverAccount, AccountRole.RECEIVER, request.CurrencyCode);

        try
        {
            transferBusinessRules.EnsureSufficientFunds(transferAccounts.SenderAccount, request.Amount);
        }
        catch (InsufficientFundsException ex)
        {
            var failedTransfer = CreateFailedTransfer(
                request,
                ex.Message,
                transferAccounts.SenderAccount,
                transferAccounts.ReceiverAccount);

            await auditRepository.LogTransferAsync(
                failedTransfer,
                AuditEventType.FAILED,
                failedTransfer.FailureReason);

            throw;
        }
    }

    private sealed record TransferAccounts(Account SenderAccount, Account ReceiverAccount);

    private async Task<TransferAccounts> GetTransferAccountsAsync(IRepository<Account> accountRepository, TransferRequest request, CancellationToken cancellationToken)
    {
        var senderAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(request.SenderAccountId, cancellationToken),
            AccountRole.SENDER,
            request.SenderAccountId);

        var receiverAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(request.ReceiverAccountId, cancellationToken),
            AccountRole.RECEIVER,
            request.ReceiverAccountId);

        return new TransferAccounts(senderAccount, receiverAccount);
    }

    private static async Task<Transfer?> GetExistingTransferAsync(
        TransferRequest request,
        IRepository<Transfer> transferRepository,
        CancellationToken cancellationToken)
    {
        var existingTransfers = await transferRepository.GetAllAsync(cancellationToken);
        return existingTransfers.FirstOrDefault(t => t.IdempotencyKey == request.IdempotencyKey);
    }

    private async Task ValidateRequestAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await transferRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Invalid transfer request.", validationResult.Errors);
        }
    }

    private static void ApplyTransferBalanceChanges(TransferRequest request, TransferAccounts transferAccounts, IRepository<Account> accountRepository)
    {
        transferAccounts.SenderAccount.Debit(request.Amount);
        transferAccounts.ReceiverAccount.Credit(request.Amount);

        accountRepository.Update(transferAccounts.SenderAccount);
        accountRepository.Update(transferAccounts.ReceiverAccount);
    }

    private static Transfer CreatePendingTransfer(TransferRequest request, TransferAccounts transferAccounts)
    {
        return new Transfer
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderAccountId = request.SenderAccountId,
            SenderAccount = transferAccounts.SenderAccount,
            ReceiverAccountId = request.ReceiverAccountId,
            ReceiverAccount = transferAccounts.ReceiverAccount,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    private static Transfer CreateFailedTransfer(
        TransferRequest request,
        string failureReason,
        Account? senderAccount = null,
        Account? receiverAccount = null,
        Guid? transferId = null)
    {
        var transfer = new Transfer
        {
            Id = transferId ?? Guid.Empty,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderAccountId = request.SenderAccountId,
            ReceiverAccountId = request.ReceiverAccountId,
            IdempotencyKey = request.IdempotencyKey,
            Status = TransferStatus.FAILED,
            FailureReason = failureReason
        };

        if (senderAccount != null)
        {
            transfer.SenderAccount = senderAccount;
        }

        if (receiverAccount != null)
        {
            transfer.ReceiverAccount = receiverAccount;
        }

        return transfer;
    }
}


