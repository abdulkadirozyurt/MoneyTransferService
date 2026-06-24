using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using MoneyTransferService.WebAPI.Diagnostics;

namespace MoneyTransferService.WebAPI.OpenApi;

internal sealed class CorrelationIdOperationTransformer : IOpenApiOperationTransformer
{
    private const string Description =
        "Optional request correlation identifier. When omitted, the API generates one. " +
        "The identifier used for the request is returned in the X-Correlation-ID response header.";

    /// <summary>
    /// Adds the optional <c>X-Correlation-ID</c> request header and corresponding
    /// response header documentation to an OpenAPI operation.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being generated.</param>
    /// <param name="context">Metadata for the operation transformation.</param>
    /// <param name="cancellationToken">Signals cancellation of OpenAPI generation.</param>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Parameters ??= [];

        if (!operation.Parameters.Any(parameter =>
                parameter.In == ParameterLocation.Header &&
                string.Equals(
                    parameter.Name,
                    RequestDiagnosticsContext.CorrelationIdHeaderName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = RequestDiagnosticsContext.CorrelationIdHeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Description = Description,
                Schema = CreateCorrelationIdSchema()
            });
        }

        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var response in operation.Responses.Values.OfType<OpenApiResponse>())
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers.TryAdd(
                RequestDiagnosticsContext.CorrelationIdHeaderName,
                new OpenApiHeader
                {
                    Description = "Correlation identifier used while processing the request.",
                    Required = true,
                    Schema = CreateCorrelationIdSchema()
                });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates the shared OpenAPI schema describing the accepted correlation ID
    /// length and character format.
    /// </summary>
    /// <returns>An OpenAPI string schema matching runtime validation rules.</returns>
    private static OpenApiSchema CreateCorrelationIdSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 1,
            MaxLength = RequestDiagnosticsContext.CorrelationIdMaxLength,
            Pattern = RequestDiagnosticsContext.CorrelationIdPattern
        };
    }
}
