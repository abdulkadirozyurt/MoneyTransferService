using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace MoneyTransferService.WebAPI.Tests;

internal sealed class TraceCaptureStartupFilter : IStartupFilter
{
    public const string TraceIdHeaderName = "X-Test-Trace-ID";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (httpContext, nextMiddleware) =>
            {
                httpContext.Response.OnStarting(() =>
                {
                    if (Activity.Current is { } activity)
                    {
                        httpContext.Response.Headers[TraceIdHeaderName] =
                            activity.TraceId.ToString();
                    }

                    return Task.CompletedTask;
                });

                await nextMiddleware();
            });

            next(app);
        };
    }
}
