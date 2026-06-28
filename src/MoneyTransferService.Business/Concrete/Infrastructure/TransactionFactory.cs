using MoneyTransferService.Business.Models;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.Core.Constants;
using MoneyTransferService.Entities.Concrete;

namespace MoneyTransferService.Business.Concrete.Infrastructure;

/// <summary>
/// Centralises all Transaction entity creation logic.
/// Keeps handler classes focused on orchestration rather than object construction.
/// </summary>
public sealed class TransactionFactory
{
    public Transaction CreatePendingTransfer(TransferCommand request, TransactionAccounts accounts)
    {
        return new Transaction
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            TransactionType = TransactionTypes.TRANSFER,
            SenderIban = accounts.SenderAccount.Iban,
            SenderAccountId = accounts.SenderAccount.Id,
            SenderAccount = accounts.SenderAccount,
            ReceiverIban = accounts.ReceiverAccount.Iban,
            ReceiverAccountId = accounts.ReceiverAccount.Id,
            ReceiverAccount = accounts.ReceiverAccount,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    public Transaction CreatePendingDeposit(DepositCommand request, Account account)
    {
        return new Transaction
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            TransactionType = TransactionTypes.DEPOSIT,
            ReceiverIban = account.Iban,
            ReceiverAccountId = account.Id,
            ReceiverAccount = account,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    public Transaction CreatePendingWithdraw(WithdrawCommand request, Account account)
    {
        return new Transaction
        {
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            TransactionType = TransactionTypes.WITHDRAW,
            SenderIban = account.Iban,
            SenderAccountId = account.Id,
            SenderAccount = account,
            IdempotencyKey = request.IdempotencyKey,
            Description = request.Description,
            Status = TransferStatus.PENDING
        };
    }

    public Transaction CreateFailedTransaction(CreateFailedTransactionParameters parameters)
    {
        return new Transaction
        {
            Id = Guid.Empty,
            Amount = parameters.Amount,
            CurrencyCode = parameters.CurrencyCode,
            TransactionType = parameters.TransactionType,
            IdempotencyKey = parameters.IdempotencyKey,
            SenderIban = parameters.SenderAccount?.Iban,
            SenderAccountId = parameters.SenderAccount?.Id,
            SenderAccount = parameters.SenderAccount,
            ReceiverIban = parameters.ReceiverAccount?.Iban,
            ReceiverAccountId = parameters.ReceiverAccount?.Id,
            ReceiverAccount = parameters.ReceiverAccount,
            Status = TransferStatus.FAILED,
            FailureReason = parameters.FailureReason
        };
    }
}
