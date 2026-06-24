using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MoneyTransferService.WebAPI.Diagnostics;

namespace MoneyTransferService.WebAPI.Tests.Integration;

[Collection(WebApiCollection.Name)]
public sealed class CorrelationIdIntegrationTests
{
    private readonly HttpClient _client;

    public CorrelationIdIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Request_ShouldReturnGeneratedCorrelationId_WhenHeaderIsMissing()
    {
        var response = await _client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var correlationId = GetCorrelationId(response);
        Guid.TryParseExact(correlationId, "N", out var parsedCorrelationId).Should().BeTrue();
        parsedCorrelationId.Version.Should().Be(7);
    }

    [Fact]
    public async Task Request_ShouldReturnIncomingCorrelationId_WhenHeaderIsValid()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(
            RequestDiagnosticsContext.CorrelationIdHeaderName,
            "checkout-20260624-001");

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        GetCorrelationId(response).Should().Be("checkout-20260624-001");
    }

    [Fact]
    public async Task Request_ShouldReturnProblemDetails_WhenHeaderContainsInvalidCharacters()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            RequestDiagnosticsContext.CorrelationIdHeaderName,
            "invalid correlation id");

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
        problem.GetProperty("title").GetString().Should().Be("Invalid correlation ID.");
        problem.GetProperty("correlationId").GetString().Should().Be(GetCorrelationId(response));
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        GetCorrelationId(response).Should().NotBe("invalid correlation id");
        problem.ToString().Should().NotContain("invalid correlation id");
    }

    [Fact]
    public async Task Request_ShouldReturnProblemDetails_WhenHeaderContainsOnlyWhitespace()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            RequestDiagnosticsContext.CorrelationIdHeaderName,
            "   ");

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Request_ShouldReturnProblemDetails_WhenHeaderExceedsMaximumLength()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            RequestDiagnosticsContext.CorrelationIdHeaderName,
            new string('a', RequestDiagnosticsContext.CorrelationIdMaxLength + 1));

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Request_ShouldReturnProblemDetails_WhenHeaderHasMultipleValues()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            RequestDiagnosticsContext.CorrelationIdHeaderName,
            ["correlation-one", "correlation-two"]);

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Request_ShouldContinueIncomingW3CTrace()
    {
        const string expectedTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            $"00-{expectedTraceId}-00f067aa0ba902b7-01");

        var response = await _client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(
                TraceCaptureStartupFilter.TraceIdHeaderName,
                out var values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Subject.Should().Be(expectedTraceId);
    }

    private static string GetCorrelationId(HttpResponseMessage response)
    {
        response.Headers.TryGetValues(
                RequestDiagnosticsContext.CorrelationIdHeaderName,
                out var values)
            .Should().BeTrue();

        return values!.Should().ContainSingle().Subject;
    }
}
