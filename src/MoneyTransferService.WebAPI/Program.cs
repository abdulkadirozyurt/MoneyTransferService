using MoneyTransferService.Business;
using MoneyTransferService.DataAccess;
using MoneyTransferService.WebAPI.Endpoints;
using MoneyTransferService.WebAPI.ExceptionHandling;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>().AddProblemDetails();



builder.Services.RegisterDataAccessServices(builder.Configuration);
builder.Services.RegisterBusinessServices(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi();
//     app.MapScalarApiReference();    
// }

app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

var api = app.MapGroup("/api");

api.MapAccountEndpoints();
api.MapCustomerEndpoints();
api.MapTransactionEndpoints();

app.Run();