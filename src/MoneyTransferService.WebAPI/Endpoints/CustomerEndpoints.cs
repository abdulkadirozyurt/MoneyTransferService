using MoneyTransferService.Business.Abstract.Services;
using MoneyTransferService.WebAPI.Contracts;

namespace MoneyTransferService.WebAPI.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        // Individual customer endpoints
        group.MapPost("/individual", CreateIndividualCustomerAsync).WithName("CreateIndividualCustomer");
        group.MapGet("/individual", GetIndividualCustomersAsync).WithName("GetIndividualCustomers");
        group.MapGet("/individual/{id:guid}", GetIndividualCustomerByIdAsync).WithName("GetIndividualCustomerById");

        // Corporate customer endpoints
        group.MapPost("/corporate", CreateCorporateCustomerAsync).WithName("CreateCorporateCustomer");
        group.MapGet("/corporate", GetCorporateCustomersAsync).WithName("GetCorporateCustomers");
        group.MapGet("/corporate/{id:guid}", GetCorporateCustomerByIdAsync).WithName("GetCorporateCustomerById");

        return app;
    }

    private static async Task<IResult> CreateIndividualCustomerAsync(
        CreateIndividualCustomerRequest request,
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.CreateIndividualCustomerAsync(
            request.Email,
            request.PhoneNumber,
            request.FirstName,
            request.LastName,
            request.NationalIdentityNumber,
            cancellationToken);

        return Results.Created($"/customers/individual/{customer.Id}",
            CustomerResponse.FromIndividualCustomer(customer));
    }

    private static async Task<IResult> GetIndividualCustomersAsync(
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customers = await customerService.GetIndividualCustomersAsync(cancellationToken);
        return Results.Ok(customers.Select(CustomerResponse.FromIndividualCustomer));
    }

    private static async Task<IResult> GetIndividualCustomerByIdAsync(
        Guid id,
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.GetIndividualCustomerByIdAsync(id, cancellationToken);
        return customer is null
            ? Results.NotFound(new { error = "Individual customer not found." })
            : Results.Ok(CustomerResponse.FromIndividualCustomer(customer));
    }

    private static async Task<IResult> CreateCorporateCustomerAsync(
        CreateCorporateCustomerRequest request,
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.CreateCorporateCustomerAsync(
            request.Email,
            request.PhoneNumber,
            request.CompanyName,
            request.TaxNumber,
            request.TaxOffice,
            cancellationToken);

        return Results.Created($"/customers/corporate/{customer.Id}",
            CustomerResponse.FromCorporateCustomer(customer));
    }

    private static async Task<IResult> GetCorporateCustomersAsync(
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customers = await customerService.GetCorporateCustomersAsync(cancellationToken);
        return Results.Ok(customers.Select(CustomerResponse.FromCorporateCustomer));
    }

    private static async Task<IResult> GetCorporateCustomerByIdAsync(
        Guid id,
        ICustomerService customerService,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCorporateCustomerByIdAsync(id, cancellationToken);
        return customer is null
            ? Results.NotFound(new { error = "Corporate customer not found." })
            : Results.Ok(CustomerResponse.FromCorporateCustomer(customer));
    }
}