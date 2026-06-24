using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyTransferService.WebAPI.Diagnostics;
using MoneyTransferService.WebAPI.Middlewares;

namespace MoneyTransferService.WebAPI.Tests.Middlewares;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldGenerateVersion7CorrelationId_WhenHeaderIsMissing()
    {
        var context = CreateHttpContext();
        var originalRequestId = context.TraceIdentifier;
        var nextInvoked = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextInvoked.Should().BeTrue();
        context.TraceIdentifier.Should().Be(originalRequestId);

        var correlationId = RequestDiagnosticsContext.GetCorrelationId(context);
        correlationId.Should().NotBeNull();
        Guid.TryParseExact(correlationId, "N", out var parsedCorrelationId).Should().BeTrue();
        parsedCorrelationId.Version.Should().Be(7);
        RequestDiagnosticsContext.GetCorrelationId(context).Should().Be(correlationId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveValidCorrelationId_WithoutChangingRequestId()
    {
        const string expectedCorrelationId = "checkout-20260624-001";
        var context = CreateHttpContext();
        var originalRequestId = context.TraceIdentifier;
        context.Request.Headers[RequestDiagnosticsContext.CorrelationIdHeaderName] =
            expectedCorrelationId;

        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        RequestDiagnosticsContext.GetCorrelationId(context).Should().Be(expectedCorrelationId);
        context.TraceIdentifier.Should().Be(originalRequestId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldGenerateCorrelationId_WhenHeaderIsEmpty()
    {
        var context = CreateHttpContext();
        context.Request.Headers[RequestDiagnosticsContext.CorrelationIdHeaderName] = string.Empty;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = RequestDiagnosticsContext.GetCorrelationId(context);
        correlationId.Should().NotBeNull();
        correlationId.Should().HaveLength(32);
        correlationId.Should().NotBeEmpty();
    }

    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new CorrelationIdMiddleware(
            next,
            NullLogger<CorrelationIdMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = "request-123"
        };
    }
}
