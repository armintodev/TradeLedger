namespace TradeLedger.Core.Domain;

public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}

public sealed class DomainRuleException(string code, string message) : DomainException(code, message)
{
    public override int StatusCode => 422;

    public override string Title => "The request breaks a rule of the journal";
}

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string field, string message)
        : base("validation_failed", message) =>
        Errors = new Dictionary<string, string[]> { [field] = [message] };

    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_failed", "One or more fields are invalid.") =>
        Errors = errors;

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public override int StatusCode => 400;

    public override string Title => "One or more fields are invalid";
}

public sealed class ResourceNotFoundException(string resource, object key)
    : DomainException("not_found", $"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;

    public override int StatusCode => 404;

    public override string Title => "Not found";
}

public sealed class ResourceConflictException(string code, string message) : DomainException(code, message)
{
    public override int StatusCode => 409;

    public override string Title => "Conflict";
}

public sealed class NotAuthenticatedException()
    : DomainException("not_authenticated", "This request has no authenticated user.")
{
    public override int StatusCode => 401;

    public override string Title => "Not authenticated";
}

public sealed class EngineUnavailableException(string kind)
    : DomainException(
        "engine_unavailable",
        $"The {kind} engine is not implemented yet, so queueing would create a run that could never finish.")
{
    public override int StatusCode => 501;

    public override string Title => "That engine is not available yet";
}
