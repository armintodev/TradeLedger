namespace TradeLedger.Api.Shared;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string PolicyName = "web";

    public string[] AllowedOrigins { get; set; } = [];
}
