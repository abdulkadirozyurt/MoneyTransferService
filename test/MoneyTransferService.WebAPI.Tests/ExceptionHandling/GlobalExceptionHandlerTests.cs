using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoneyTransferService.Business.Exceptions;
using MoneyTransferService.WebAPI.Diagnostics;
using MoneyTransferService.WebAPI.ExceptionHandling;

namespace MoneyTransferService.WebAPI.Tests.ExceptionHandling;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ShouldExposeCorrelationAndTraceIds_ForValidationException()
    {
        var exception = new ValidationException(
            [new ValidationFailure("Amount", "Amount must be greater than zero.")]);

        var result = await HandleAsync(exception);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Body.GetProperty("correlationId").GetString().Should().Be("checkout-123");
        result.Body.GetProperty("traceId").GetString().Should().Be(result.TraceId);
        result.Body.TryGetProperty("requestId", out _).Should().BeFalse();
        result.Log.Properties["CorrelationId"].Should().Be("checkout-123");
        result.Log.Properties["TraceId"].Should().Be(result.TraceId);
        result.Log.Properties["RequestId"].Should().Be("request-123");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldExposeCorrelationAndTraceIds_ForBusinessException()
    {
        var result = await HandleAsync(
            new InvalidTransferAmountException("Transfer amount is invalid."));

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Body.GetProperty("correlationId").GetString().Should().Be("checkout-123");
        result.Body.GetProperty("traceId").GetString().Should().Be(result.TraceId);
        result.Log.Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldExposeCorrelationAndTraceIds_ForUnhandledException()
    {
        var result = await HandleAsync(new InvalidOperationException("Unexpected."));

        result.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        result.Body.GetProperty("correlationId").GetString().Should().Be("checkout-123");
        result.Body.GetProperty("traceId").GetString().Should().Be(result.TraceId);
        result.Log.Level.Should().Be(LogLevel.Error);
    }

    private static async Task<HandlerResult> HandleAsync(Exception exception)
    {
        using var activity = new Activity("request")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var httpContext = CreateHttpContext();
        RequestDiagnosticsContext.SetCorrelationId(httpContext, "checkout-123");

        var handled = await handler.TryHandleAsync(
            httpContext,
            exception,
            TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        httpContext.Response.Body.Position = 0;

        using var document = await JsonDocument.ParseAsync(
            httpContext.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        return new HandlerResult(
            httpContext.Response.StatusCode,
            document.RootElement.Clone(),
            activity.TraceId.ToString(),
            logger.Entries.Should().ContainSingle().Subject);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-123",
            RequestServices = services
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/transactions/transfer";
        httpContext.Response.Body = new MemoryStream();

        return httpContext;
    }

    private sealed record HandlerResult(
        int StatusCode,
        JsonElement Body,
        string TraceId,
        LogEntry Log);

    private sealed record LogEntry(
        LogLevel Level,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state as IEnumerable<KeyValuePair<string, object?>>;
            Entries.Add(new LogEntry(
                logLevel,
                properties?.ToDictionary(pair => pair.Key, pair => pair.Value)
                    ?? new Dictionary<string, object?>()));
        }
    }
}
