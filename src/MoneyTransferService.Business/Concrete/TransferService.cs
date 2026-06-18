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

        var validationResult = await transferRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Invalid transfer request.", validationResult.Errors);
        }

        var transferRepository = unitOfWork.GetRepository<Transfer>();
        var existingTransfers = await transferRepository.GetAllAsync(cancellationToken);
        var existingTransfer = existingTransfers.FirstOrDefault(t => t.IdempotencyKey == idempotencyKey);
        if (existingTransfer != null)
        {
            return existingTransfer;
        }

        var accountRepository = unitOfWork.GetRepository<Account>();
        var senderAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(senderAccountId, cancellationToken),
            "Sender",
            senderAccountId);
        var receiverAccount = transferBusinessRules.EnsureAccountExists(
            await accountRepository.GetByIdAsync(receiverAccountId, cancellationToken),
            "Receiver",
            receiverAccountId);

        transferBusinessRules.EnsureAccountIsActive(senderAccount, "Sender");
        transferBusinessRules.EnsureAccountIsActive(receiverAccount, "Receiver");
        transferBusinessRules.EnsureCurrencyMatches(senderAccount, "Sender", currencyCode);
        transferBusinessRules.EnsureCurrencyMatches(receiverAccount, "Receiver", currencyCode);

        try
        {
            transferBusinessRules.EnsureSufficientFunds(senderAccount, amount);
        }
        catch (InsufficientFundsException ex)
        {
            var failedTransfer = CreateFailedTransfer(request, ex.Message, senderAccount, receiverAccount);
            await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);
            throw;
        }

        var transfer = new Transfer
        {
            Amount = amount,
            CurrencyCode = currencyCode,
            SenderAccountId = senderAccountId,
            SenderAccount = senderAccount,
            ReceiverAccountId = receiverAccountId,
            ReceiverAccount = receiverAccount,
            IdempotencyKey = idempotencyKey,
            Description = description,
            Status = TransferStatus.PENDING
        };

        await auditRepository.LogTransferAsync(transfer, AuditEventType.INITIATED);

        senderAccount.Balance -= amount;
        receiverAccount.Balance += amount;

        accountRepository.Update(senderAccount);
        accountRepository.Update(receiverAccount);

        transfer.Status = TransferStatus.COMPLETED;
        transfer.CompletedAt = DateTimeOffset.UtcNow;
        await transferRepository.AddAsync(transfer, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var failedTransfer = CreateFailedTransfer(
                request,
                "Optimistic concurrency version conflict during save.",
                senderAccount,
                receiverAccount,
                transfer.Id);
            await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);
            throw new ConcurrencyException(failedTransfer.FailureReason!, ex);
        }

        await auditRepository.LogTransferAsync(transfer, AuditEventType.COMPLETED);

        return transfer;
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
