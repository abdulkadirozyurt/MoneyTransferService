using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MoneyTransferService.WebAPI.Diagnostics;
using MoneyTransferService.WebAPI.Extensions;
using Serilog;
using Serilog.AspNetCore;

namespace MoneyTransferService.WebAPI.Tests.Middlewares;

public sealed class SerilogMiddlewareTests
{
    [Fact]
    public void ConfigureRequestLogging_ShouldUseSeparateCorrelationRequestAndTraceIdentifiers()
    {
        using var activity = new Activity("request")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "request-123"
        };
        RequestDiagnosticsContext.SetCorrelationId(httpContext, "checkout-123");

        var options = new RequestLoggingOptions();
        SerilogSerilogRequestLoggingMiddlewareExtensions.ConfigureRequestLogging(options);
        var diagnosticContext = new RecordingDiagnosticContext();

        options.EnrichDiagnosticContext!(diagnosticContext, httpContext);

        diagnosticContext.Properties["CorrelationId"].Should().Be("checkout-123");
        diagnosticContext.Properties["RequestId"].Should().Be("request-123");
        diagnosticContext.Properties["TraceId"].Should().Be(activity.TraceId.ToString());
        diagnosticContext.Properties["CorrelationId"]
            .Should().NotBe(diagnosticContext.Properties["RequestId"]);
        diagnosticContext.Properties["TraceId"]
            .Should().NotBe(diagnosticContext.Properties["CorrelationId"]);
    }

    private sealed class RecordingDiagnosticContext : IDiagnosticContext
    {
        public Dictionary<string, object?> Properties { get; } = [];

        public void Set(string propertyName, object? value, bool destructureObjects = false)
        {
            Properties[propertyName] = value;
        }

        public void SetException(Exception exception)
        {
            Properties["Exception"] = exception;
        }
    }
}
