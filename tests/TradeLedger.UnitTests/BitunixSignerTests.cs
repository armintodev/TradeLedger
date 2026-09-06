using System.Security.Cryptography;
using System.Text;
using TradeLedger.Core.Integrations.Bitunix;

namespace TradeLedger.UnitTests;

public class BitunixSignerTests
{
    private const string Nonce = "123456";
    private const string Timestamp = "20241120123045";
    private const string ApiKey = "yourApiKey";
    private const string SecretKey = "yourSecretKey";
    private const string QueryParams = "id1uid200";

    private const string Body =
        """{"uid":"2899","arr":[{"id":1,"name":"maple"},{"id":2,"name":"lily"}]}""";

    [Fact]
    public void Sign_MatchesVendorReferenceVector()
    {
        var digest = Sha256Hex(Nonce + Timestamp + ApiKey + QueryParams + Body);
        var expected = Sha256Hex(digest + SecretKey);

        var actual = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, SecretKey);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Sign_IsDeterministic()
    {
        var first = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, SecretKey);
        var second = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, SecretKey);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Sign_ChangesWhenSecretChanges()
    {
        var withSecret = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, SecretKey);
        var withOther = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, "different");

        Assert.NotEqual(withSecret, withOther);
    }

    [Fact]
    public void Sign_ProducesLowercaseHex()
    {
        var sign = BitunixSigner.Sign(Nonce, Timestamp, ApiKey, QueryParams, Body, SecretKey);

        Assert.Equal(64, sign.Length);
        Assert.All(sign, c => Assert.True(char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void CanonicalQuery_SortsAsciiAscendingAndConcatenatesWithoutSeparators()
    {
        var query = new Dictionary<string, string?> { ["uid"] = "200", ["id"] = "1" };

        Assert.Equal("id1uid200", BitunixSigner.CanonicalQuery(query));
    }

    [Fact]
    public void CanonicalQuery_SortsByOrdinalNotCulture()
    {
        var query = new Dictionary<string, string?> { ["b"] = "2", ["A"] = "1" };

        Assert.Equal("A1b2", BitunixSigner.CanonicalQuery(query));
    }

    [Fact]
    public void CanonicalQuery_SkipsNullValues()
    {
        var query = new Dictionary<string, string?> { ["id"] = "1", ["symbol"] = null };

        Assert.Equal("id1", BitunixSigner.CanonicalQuery(query));
    }

    [Fact]
    public void CanonicalQuery_IsEmptyForNoParameters()
    {
        Assert.Equal(string.Empty, BitunixSigner.CanonicalQuery(null));
        Assert.Equal(string.Empty, BitunixSigner.CanonicalQuery(new Dictionary<string, string?>()));
    }

    [Fact]
    public void NewNonce_IsUniqueAcrossCalls()
    {
        var nonces = Enumerable.Range(0, 1000).Select(_ => BitunixSigner.NewNonce()).ToHashSet();

        Assert.Equal(1000, nonces.Count);
    }

    [Fact]
    public void Timestamp_EpochMilliseconds_IsNumeric()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1732105845000);

        var value = BitunixSigner.Timestamp(now, TimestampFormat.EpochMilliseconds);

        Assert.Equal("1732105845000", value);
    }

    [Fact]
    public void Timestamp_CompactDateTime_MatchesVendorSampleShape()
    {
        var now = new DateTimeOffset(2024, 11, 20, 12, 30, 45, TimeSpan.Zero);

        var value = BitunixSigner.Timestamp(now, TimestampFormat.CompactDateTime);

        Assert.Equal("20241120123045", value);
    }

    private static string Sha256Hex(string input) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
