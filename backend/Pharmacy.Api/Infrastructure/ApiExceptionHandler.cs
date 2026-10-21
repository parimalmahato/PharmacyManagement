using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Pharmacy.Api.Infrastructure;

/// <summary>
/// Logs an unexpected request failure with its trace identifier and returns a generic problem response without exposing internal details.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Logs the unexpected failure with a trace ID and sends a generic problem response without returning implementation details.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Request failed. Trace {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 500, Title = "The request could not be completed.",
            Detail = "Refresh the record before retrying. For payments or returns, use the existing pending request.",
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: ct);
        return true;
    }
}
