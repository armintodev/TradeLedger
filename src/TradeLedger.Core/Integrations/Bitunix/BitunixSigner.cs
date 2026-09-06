using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TradeLedger.Core.Integrations.Bitunix;

public static class BitunixSigner
{
    public static string CanonicalQuery(IEnumerable<KeyValuePair<string, string?>>? parameters)
    {
        if (parameters is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var kv in parameters
                     .Where(p => p.Value is not null)
                     .OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sb.Append(kv.Key).Append(kv.Value);
        }

        return sb.ToString();
    }

    public static string Sign(
        string nonce,
        string timestamp,
        string apiKey,
        string canonicalQuery,
        string compactBody,
        string apiSecret)
    {
        var digest = Sha256Hex(nonce + timestamp + apiKey + canonicalQuery + compactBody);
        return Sha256Hex(digest + apiSecret);
    }

    public static string NewNonce() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static string Timestamp(DateTimeOffset now, TimestampFormat format = TimestampFormat.EpochMilliseconds) =>
        format switch
        {
            TimestampFormat.EpochMilliseconds =>
                now.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            TimestampFormat.CompactDateTime =>
                now.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    private static string Sha256Hex(string input) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}

public enum TimestampFormat
{
    EpochMilliseconds = 0,
    CompactDateTime = 1,
}
