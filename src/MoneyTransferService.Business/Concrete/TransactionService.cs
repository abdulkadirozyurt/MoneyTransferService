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

public class TransactionService(
    IUnitOfWork unitOfWork,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    IValidator<TransferCommand> transferRequestValidator,
    ITransferBusinessRules transferBusinessRules,
    ITransactionAuditRepository auditRepository) : ITransactionService
{
    private const int MaxConcurrencyRetryAttempts = 3;

    public async Task<Transaction> TransferAsync(TransferCommand request, CancellationToken cancellationToken = default)
    {
        await ValidateRequestAsync(request, cancellationToken);

        for (int attempt = 1; attempt <= MaxConcurrencyRetryAttempts; attempt++)
        {
            try
            {
                return await ExecuteTransferAttemptAsync(request, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetryAttempts)
            {
                continue; // Retry the operation
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var failedTransfer = CreateFailedTransfer(request, "Optimistic concurrency version conflict during transfer.");

                await auditRepository.LogTransferAsync(failedTransfer, AuditEventType.FAILED, failedTransfer.FailureReason);

                throw new ConcurrencyException(failedTransfer.FailureReason!, exception);
            }
            catch (DbUpdateException exception) // Another request may have committed the same idempotency key first.
            {
                var existingTransfer = await GetExistingTransferAsync(
                 request,
                 transactionRepository,
                 cancellationToken);

                if (existingTransfer != null)
                {
                    return existingTransfer;
                }

                var failedTransfer = CreateFailedTransfer(
                    request,
                    "Transfer could not be completed.");

                await auditRepository.LogTransferAsync(
                    failedTransfer,
                    AuditEventType.FAILED,
                    failedTransfer.FailureReason);

                throw new TransferPersistenceException(failedTransfer.FailureReason!, exception);
            }
        }

        throw new InvalidOperationException("Max concurrency retry attempts exceeded.");
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await transactionRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetTransactionHistoryAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        return transactions.OrderByDescending(transaction => transaction.CreatedAt);
    }





    private async Task<Transaction> ExecuteTransferAttemptAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            Transaction? existingTransfer = await GetExistingTransferAsync(request, transactionRepository, cancellationToken);
            if (existingTransfer != null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return existingTransfer;
            }

            var transferAccounts = await GetTransferAccountsAsync(accountRepository, request, cancellationToken);

            await EnsureTransferCanBeCompletedAsync(request, transferAccounts);

            var transfer = CreatePendingTransfer(request, transferAccounts);

            await auditRepository.LogTransferAsync(transfer, AuditEventType.INITIATED);

            ApplyTransferBalanceChanges(request, transferAccounts, accountRepository);

            await CompleteTransferAsync(transfer, transactionRepository, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await auditRepository.LogTransferAsync(transfer, AuditEventType.COMPLETED);

            return transfer;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static async Task CompleteTransferAsync(Transaction transfer, IRepository<Transaction> transactionRepository, CancellationToken cancellationToken)
    {
        transfer.Status = TransferStatus.COMPLETED;
        transfer.CompletedAt = DateTimeOffset.UtcNow;
        await transactionRepository.AddAsync(transfer, cancellationToken);
    }

    private async Task EnsureTransferCanBeCompletedAsync(TransferCommand request, TransactionAccounts transferAccounts)
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

    private async Task<TransactionAccounts> GetTransferAccountsAsync(IAccountRepository accountRepository, TransferCommand request, CancellationToken cancellationToken)
    {
        // locking the accounts for update to prevent deadlocks
        // Locking by IBAN Order: Always lock the account with the smaller IBAN first to prevent deadlocks.
        var senderComesFirst = string.CompareOrdinal(request.SenderIban, request.ReceiverIban) < 0;

        var firstAccountIban = senderComesFirst ? request.SenderIban : request.ReceiverIban;
        var secondAccountIban = senderComesFirst ? request.ReceiverIban : request.SenderIban;

        var firstAccount = await accountRepository.GetByIbanForUpdateAsync(firstAccountIban, cancellationToken);
        var secondAccount = await accountRepository.GetByIbanForUpdateAsync(secondAccountIban, cancellationToken);


        // split sender and receiver

        var senderAccount = firstAccountIban == request.SenderIban ? firstAccount : secondAccount;
        var receiverAccount = firstAccountIban == request.ReceiverIban ? firstAccount : secondAccount;

        var sender = transferBusinessRules.EnsureAccountExists(senderAccount, AccountRole.SENDER, request.SenderIban);
        var receiver = transferBusinessRules.EnsureAccountExists(receiverAccount, AccountRole.RECEIVER, request.ReceiverIban);

        return new TransactionAccounts(sender, receiver);
    }

    // Idempotency check: If a transfer with the same IdempotencyKey already exists, return it instead of creating a new one.
    private static async Task<Transaction?> GetExistingTransferAsync(TransferCommand request, IRepository<Transaction> transferRepository, CancellationToken cancellationToken)
    {
        return await transferRepository.GetAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken);
    }

    private async Task ValidateRequestAsync(TransferCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await transferRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Invalid transfer request.", validationResult.Errors);
        }
    }

    private static void ApplyTransferBalanceChanges(TransferCommand request, TransactionAccounts transferAccounts, IRepository<Account> accountRepository)
    {
        transferAccounts.SenderAccount.Debit(request.Amount);
        transferAccounts.ReceiverAccount.Deposit(request.Amount);

        accountRepository.Update(transferAccounts.SenderAccount);
        accountRepository.Update(transferAccounts.ReceiverAccount);
    }

    private static Transaction CreatePendingTransfer(TransferCommand request, TransactionAccounts transferAccounts)
    {
        return new Transaction
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderIban = transferAccounts.SenderAccount.Iban,
            SenderAccountId = transferAccounts.SenderAccount.Id,
            SenderAccount = transferAccounts.SenderAccount,
            ReceiverIban = transferAccounts.ReceiverAccount.Iban,
            ReceiverAccountId = transferAccounts.ReceiverAccount.Id,
            ReceiverAccount = transferAccounts.ReceiverAccount,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    private static Transaction CreateFailedTransfer(
        TransferCommand request,
        string failureReason,
        Account? senderAccount = null,
        Account? receiverAccount = null,
        Guid? transferId = null)
    {
        var transfer = new Transaction
        {
            Id = transferId ?? Guid.Empty,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            SenderIban = request.SenderIban,
            ReceiverIban = request.ReceiverIban,
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
sealed record TransactionAccounts(Account SenderAccount, Account ReceiverAccount);


