using MoneyTransferService.Business.Abstract;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.WebAPI.Contracts;

namespace MoneyTransferService.WebAPI.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/accounts")
            .WithTags("Accounts");

        group.MapPost("/", CreateAccountAsync)
            .WithName("CreateAccount");

        group.MapGet("/{id:guid}", GetAccountByIdAsync)
            .WithName("GetAccountById");

        group.MapGet("/{id:guid}/balance", GetAccountBalanceAsync)
            .WithName("GetAccountBalance");

        return app;
    }

    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        try
        {
            var account = await accountService.CreateAccountAsync(
                request.IndividualCustomerId,
                request.CorporateCustomerId,
                request.CurrencyCode,
                request.InitialBalance,
                cancellationToken);

            return Results.Created($"/accounts/{account.Id}", AccountResponse.FromAccount(account));
        }
        catch (InvalidAccountRequestException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (AccountOwnerNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (AccountCreationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAccountByIdAsync(
        Guid id,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var account = await accountService.GetAccountByIdAsync(id, cancellationToken);
        return account is null
            ? Results.NotFound(new { error = "Account not found." })
            : Results.Ok(AccountResponse.FromAccount(account));
    }

    private static async Task<IResult> GetAccountBalanceAsync(
        Guid id,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var account = await accountService.GetAccountByIdAsync(id, cancellationToken);
        return account is null
            ? Results.NotFound(new { error = "Account not found." })
            : Results.Ok(new AccountBalanceResponse(
                account.Id,
                account.AccountNumber,
                account.CurrencyCode,
                account.Balance,
                account.Status));
    }
}
