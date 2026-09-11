using Microsoft.AspNetCore.Mvc;
using TradeLedger.Core.Domain;

namespace TradeLedger.Api.Shared.Errors;

public static class ApiProblem
{
    public const string BaseUri = "https://tradeledger.local/problems/";

    public static ProblemDetails From(
        HttpContext context,
        int status,
        string title,
        string code,
        string? detail = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = BaseUri + code.Replace('_', '-'),
            Title = title,
            Status = status,
            Detail = detail,
            Instance = $"{context.Request.Method} {context.Request.Path}",
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    public static ProblemDetails FromDomain(HttpContext context, DomainException exception) =>
        From(
            context,
            exception.StatusCode,
            exception.Title,
            exception.Code,
            exception.Message,
            (exception as DomainValidationException)?.Errors);
}
