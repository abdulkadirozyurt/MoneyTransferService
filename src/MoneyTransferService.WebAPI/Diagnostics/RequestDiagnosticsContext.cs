using System.Diagnostics;

namespace MoneyTransferService.WebAPI.Diagnostics;

internal static class RequestDiagnosticsContext
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const int CorrelationIdMaxLength = 64;
    public const string CorrelationIdPattern = "^[A-Za-z0-9._-]+$";

    private static readonly object CorrelationIdItemKey = new();

    /// <summary>
    /// Stores the correlation ID selected for the current request in
    /// <see cref="HttpContext.Items"/> so downstream components can access it.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context.</param>
    /// <param name="correlationId">The validated or API-generated correlation ID.</param>
    public static void SetCorrelationId(HttpContext httpContext, string correlationId)
    {
        httpContext.Items[CorrelationIdItemKey] = correlationId;
    }

    /// <summary>
    /// Retrieves the correlation ID previously stored for the current request.
    /// </summary>
    /// <param name="httpContext">The current HTTP request context.</param>
    /// <returns>The request correlation ID, or <see langword="null"/> if it has not been set.</returns>
    public static string? GetCorrelationId(HttpContext httpContext)
    {
        return httpContext.Items.TryGetValue(CorrelationIdItemKey, out var correlationId)
            ? correlationId as string
            : null;
    }

    /// <summary>
    /// Reads the distributed trace ID from the currently active
    /// <see cref="Activity"/>.
    /// </summary>
    /// <returns>The OpenTelemetry/W3C trace ID, or <see langword="null"/> when no activity is active.</returns>
    public static string? GetTraceId()
    {
        return Activity.Current is { } activity
            ? activity.TraceId.ToString()
            : null;
    }

    /// <summary>
    /// Determines whether a correlation ID satisfies the configured length and
    /// safe-character rules.
    /// </summary>
    /// <param name="correlationId">The incoming correlation ID to validate.</param>
    /// <returns>
    /// <see langword="true"/> when the value contains 1–64 ASCII letters, digits,
    /// dots, underscores, or hyphens; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsValidCorrelationId(string correlationId)
    {
        if (correlationId.Length is < 1 or > CorrelationIdMaxLength)
        {
            return false;
        }

        foreach (var character in correlationId)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }
}
