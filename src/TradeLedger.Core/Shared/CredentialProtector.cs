using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TradeLedger.Core.Shared;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string? KeyBase64 { get; set; }
}

public interface ICredentialProtector
{
    byte[] Protect(string plaintext);

    string Unprotect(byte[] payload);
}

public sealed class AesGcmCredentialProtector : ICredentialProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmCredentialProtector(IOptions<EncryptionOptions> options)
    {
        var configured = options.Value.KeyBase64;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Encryption:KeyBase64 is not configured. Generate one with: " +
                "dotnet user-secrets set \"Encryption:KeyBase64\" \"$(openssl rand -base64 32)\"");
        }

        _key = Convert.FromBase64String(configured);
        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Encryption:KeyBase64 must decode to 32 bytes for AES-256; got {_key.Length}.");
        }
    }

    public byte[] Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + TagSize + plainBytes.Length];

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return payload;
    }

    public string Unprotect(byte[] payload)
    {
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Encrypted credential payload is truncated.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
