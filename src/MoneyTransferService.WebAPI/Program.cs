using MoneyTransferService.Business;
using MoneyTransferService.DataAccess;
using MoneyTransferService.WebAPI.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterDataAccessServices(builder.Configuration);
builder.Services.RegisterBusinessServices(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi();
//     app.MapScalarApiReference();    
// }

app.MapOpenApi();
app.MapScalarApiReference();    

app.UseHttpsRedirection();

app.MapAccountEndpoints();
app.MapTransferEndpoints();

app.Run();