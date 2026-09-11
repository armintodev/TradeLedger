namespace TradeLedger.Core.Domain;

public static class Guard
{
    public static string NotBlank(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(field, $"{field} is required.");
        }

        return value.Trim();
    }

    public static decimal Positive(decimal value, string field)
    {
        if (value <= 0m)
        {
            throw new DomainValidationException(field, $"{field} must be greater than zero.");
        }

        return value;
    }

    public static decimal NotNegative(decimal value, string field)
    {
        if (value < 0m)
        {
            throw new DomainValidationException(field, $"{field} cannot be negative.");
        }

        return value;
    }

    public static decimal? PositiveOrNull(decimal? value, string field) =>
        value is null ? null : Positive(value.Value, field);

    public static int InRange(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new DomainValidationException(field, $"{field} must be between {min} and {max}.");
        }

        return value;
    }

    public static decimal InRange(decimal value, decimal min, decimal max, string field)
    {
        if (value < min || value > max)
        {
            throw new DomainValidationException(field, $"{field} must be between {min} and {max}.");
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, string field)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException(field, $"{field} is required.");
        }

        return value;
    }

    public static DateTimeOffset NotDefault(DateTimeOffset value, string field)
    {
        if (value == default)
        {
            throw new DomainValidationException(field, $"{field} is required.");
        }

        return value;
    }

    public static void Rule(bool condition, string code, string message)
    {
        if (!condition)
        {
            throw new DomainRuleException(code, message);
        }
    }
}
