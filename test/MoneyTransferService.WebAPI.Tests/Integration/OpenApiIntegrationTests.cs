using System.Text.Json;
using FluentAssertions;
using MoneyTransferService.WebAPI.Diagnostics;

namespace MoneyTransferService.WebAPI.Tests.Integration;

[Collection(WebApiCollection.Name)]
public sealed class OpenApiIntegrationTests
{
    private static readonly HashSet<string> HttpOperationNames =
        ["get", "post", "put", "patch", "delete", "head", "options", "trace"];

    private readonly HttpClient _client;

    public OpenApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task OpenApiDocument_ShouldDescribeOptionalCorrelationIdRequestAndResponseHeaders()
    {
        using var document = JsonDocument.Parse(
            await _client.GetStringAsync(
                "/openapi/v1.json",
                TestContext.Current.CancellationToken));

        var operations = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value
                .EnumerateObject()
                .Where(operation => HttpOperationNames.Contains(operation.Name))
                .Select(operation => operation.Value))
            .ToArray();

        operations.Should().NotBeEmpty();

        foreach (var operation in operations)
        {
            var correlationParameter = operation
                .GetProperty("parameters")
                .EnumerateArray()
                .Single(parameter =>
                    parameter.GetProperty("name").GetString() ==
                    RequestDiagnosticsContext.CorrelationIdHeaderName);

            correlationParameter.GetProperty("in").GetString().Should().Be("header");
            if (correlationParameter.TryGetProperty("required", out var required))
            {
                required.GetBoolean().Should().BeFalse();
            }

            var schema = correlationParameter.GetProperty("schema");
            schema.GetProperty("minLength").GetInt32().Should().Be(1);
            schema.GetProperty("maxLength").GetInt32()
                .Should().Be(RequestDiagnosticsContext.CorrelationIdMaxLength);
            schema.GetProperty("pattern").GetString()
                .Should().Be(RequestDiagnosticsContext.CorrelationIdPattern);

            foreach (var response in operation.GetProperty("responses").EnumerateObject())
            {
                response.Value
                    .GetProperty("headers")
                    .TryGetProperty(
                        RequestDiagnosticsContext.CorrelationIdHeaderName,
                        out _)
                    .Should().BeTrue();
            }
        }
    }
}
