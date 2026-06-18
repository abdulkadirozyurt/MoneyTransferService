using MoneyTransferService.DataAccess;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterDataAccessServices(builder.Configuration);

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

app.Run();