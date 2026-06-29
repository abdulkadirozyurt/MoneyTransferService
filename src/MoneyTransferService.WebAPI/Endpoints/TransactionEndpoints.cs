using MoneyTransferService.Business.Abstract.Services;
using MoneyTransferService.Business.Requests;
using MoneyTransferService.WebAPI.Contracts;

namespace MoneyTransferService.WebAPI.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transactions").WithTags("Transactions");

        group.MapPost("/deposit", CreateDepositAsync).WithName("CreateDeposit");
        group.MapPost("/withdraw", CreateWithdrawAsync).WithName("CreateWithdraw");
        group.MapPost("/transfer", CreateTransferAsync).WithName("CreateTransactionTransfer");
        group.MapGet("/{id:guid}", GetTransactionByIdAsync).WithName("GetTransactionById");
        group.MapGet("/history", GetTransactionHistoryAsync).WithName("GetTransactionHistory");

        return app;
    }

    private static async Task<IResult> CreateDepositAsync(
        CreateDepositRequest request,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        var command = new DepositCommand(
            request.AccountIban,
            request.Amount,
            request.CurrencyCode,
            request.IdempotencyKey,
            request.Description);

        var transaction = await transactionService.DepositAsync(command, cancellationToken);

        return Results.Created($"/api/transactions/{transaction.Id}", TransactionResponse.FromTransaction(transaction));
    }

    private static async Task<IResult> CreateWithdrawAsync(
        CreateWithdrawRequest request,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        var command = new WithdrawCommand(
            request.AccountIban,
            request.Amount,
            request.CurrencyCode,
            request.IdempotencyKey,
            request.Description);

        var transaction = await transactionService.WithdrawAsync(command, cancellationToken);

        return Results.Created($"/api/transactions/{transaction.Id}", TransactionResponse.FromTransaction(transaction));
    }

    private static async Task<IResult> CreateTransferAsync(
        CreateTransferRequest request,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        var command = new TransferCommand(
            request.SenderIban,
            request.ReceiverIban,
            request.Amount,
            request.CurrencyCode,
            request.IdempotencyKey,
            request.Description);

        var transaction = await transactionService.TransferAsync(command, cancellationToken);

        // wrong status code for idempotent requests, should be 200 OK if the transaction already exists !!!!!!!!!!!!!!!!!
        return Results.Created($"/api/transactions/{transaction.Id}", TransactionResponse.FromTransaction(transaction));
    }

    private static async Task<IResult> GetTransactionByIdAsync(
        Guid id,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        var transaction = await transactionService.GetTransactionByIdAsync(id, cancellationToken);
        return transaction is null
            ? Results.NotFound(new { error = "Transaction not found." })
            : Results.Ok(TransactionResponse.FromTransaction(transaction));
    }

    private static async Task<IResult> GetTransactionHistoryAsync(
        int pageNumber,
        int pageSize,
        ITransactionService transactionService,
        CancellationToken cancellationToken)
    {
        var transactions = await transactionService.GetTransactionHistoryAsync(pageNumber, pageSize, cancellationToken);
        return Results.Ok(transactions.Select(TransactionResponse.FromTransaction));
    }
}
