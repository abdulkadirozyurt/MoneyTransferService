using MoneyTransferService.Business.Abstract;
using MoneyTransferService.WebAPI.Contracts;

namespace MoneyTransferService.WebAPI.Endpoints;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/transfers").WithTags("Transfers");

        group.MapPost("/", CreateTransferAsync).WithName("CreateTransfer");
        group.MapGet("/{id:guid}", GetTransferByIdAsync).WithName("GetTransferById");
        group.MapGet("/history", GetTransferHistoryAsync).WithName("GetTransferHistory");

        return app;
    }

    private static async Task<IResult> CreateTransferAsync(
        CreateTransferRequest request,
        ITransferService transferService,
        CancellationToken cancellationToken)
    {
        var transfer = await transferService.TransferAsync(
            request.SenderAccountId,
            request.ReceiverAccountId,
            request.Amount,
            request.CurrencyCode,
            request.IdempotencyKey,
            request.Description,
            cancellationToken);

        return Results.Created($"/transfers/{transfer.Id}", TransferResponse.FromTransfer(transfer));
    }

    private static async Task<IResult> GetTransferByIdAsync(
        Guid id,
        ITransferService transferService,
        CancellationToken cancellationToken)
    {
        var transfer = await transferService.GetTransferByIdAsync(id, cancellationToken);
        return transfer is null
            ? Results.NotFound(new { error = "Transfer not found." })
            : Results.Ok(TransferResponse.FromTransfer(transfer));
    }

    private static async Task<IResult> GetTransferHistoryAsync(
        ITransferService transferService,
        CancellationToken cancellationToken)
    {
        var transfers = await transferService.GetTransferHistoryAsync(cancellationToken);
        return Results.Ok(transfers.Select(TransferResponse.FromTransfer));
    }
}
