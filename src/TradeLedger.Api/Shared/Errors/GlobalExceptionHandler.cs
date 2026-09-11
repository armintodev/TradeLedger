using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeLedger.Core.Domain;
using TradeLedger.Core.Shared.Proxy;

namespace TradeLedger.Api.Shared.Errors;

public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var problem = Translate(context, exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (environment.IsDevelopment())
            {
                problem.Detail = exception.Message;
                problem.Extensions["exception"] = exception.GetType().Name;
            }
        }
        else
        {
            logger.LogDebug(
                exception,
                "Request failed with {Status} on {Method} {Path}",
                problem.Status,
                context.Request.Method,
                context.Request.Path);
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, ct);

        return true;
    }

    private static ProblemDetails Translate(HttpContext context, Exception exception) => exception switch
    {
        DomainException domain => ApiProblem.FromDomain(context, domain),

        ProxyRequiredException proxy => ApiProblem.From(
            context,
            StatusCodes.Status503ServiceUnavailable,
            "No egress proxy is configured",
            "proxy_required",
            proxy.Message),

        BadHttpRequestException bad => ApiProblem.From(
            context,
            bad.StatusCode,
            "Malformed request",
            "malformed_request",
            bad.Message),

        ArgumentException argument => ApiProblem.From(
            context,
            StatusCodes.Status400BadRequest,
            "Invalid argument",
            "invalid_argument",
            argument.Message),

        DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => ApiProblem.From(
            context,
            StatusCodes.Status409Conflict,
            "That already exists",
            "duplicate_record",
            "A record with the same unique key already exists."),

        DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } => ApiProblem.From(
            context,
            StatusCodes.Status400BadRequest,
            "Referenced record does not exist",
            "missing_reference",
            "One of the ids in this request does not point at an existing record."),

        DbUpdateConcurrencyException => ApiProblem.From(
            context,
            StatusCodes.Status409Conflict,
            "The record changed while you were editing it",
            "concurrent_update",
            "Reload the record and apply the change again."),

        OperationCanceledException => ApiProblem.From(
            context,
            StatusCodes.Status499ClientClosedRequest,
            "The request was cancelled",
            "request_cancelled"),

        _ => ApiProblem.From(
            context,
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred",
            "internal_error"),
    };
}
