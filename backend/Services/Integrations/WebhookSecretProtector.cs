using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Api.Services.Integrations;

public sealed class IntegrationsOptions
{
    public const string SectionName = "Integrations";

    /// <summary>Base64-encoded 32-byte key for AES-GCM webhook secret encryption.</summary>
    public string WebhookProtectionKey { get; set; } = "";
}

/// <summary>
/// Authenticated encryption for webhook signing secrets at rest.
/// Format: v1.{base64url(nonce|ciphertext|tag)}. Legacy plaintext rows are accepted on read.
/// </summary>
public sealed class WebhookSecretProtector(IOptions<IntegrationsOptions> options, IHostEnvironment env, ILogger<WebhookSecretProtector> logger)
{
    public const string Prefix = "v1.";

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) throw new InvalidOperationException("secret خالی است.");
        var key = ResolveKey(required: true);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        var packed = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, packed, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, packed, nonce.Length + cipher.Length, tag.Length);
        return Prefix + Base64UrlEncode(packed);
    }

    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return stored;
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Legacy plaintext compatibility.
            return stored;
        }

        var key = ResolveKey(required: true);
        var packed = Base64UrlDecode(stored[Prefix.Length..]);
        if (packed.Length < 12 + 16) throw new CryptographicException("webhook secret blob نامعتبر است.");
        var nonce = packed.AsSpan(0, 12);
        var tag = packed.AsSpan(packed.Length - 16, 16);
        var cipher = packed.AsSpan(12, packed.Length - 28);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] ResolveKey(bool required)
    {
        var raw = options.Value.WebhookProtectionKey?.Trim() ?? "";
        if (string.IsNullOrEmpty(raw))
        {
            raw = Environment.GetEnvironmentVariable("Integrations__WebhookProtectionKey")
                ?? Environment.GetEnvironmentVariable("ROTBEH_WEBHOOK_PROTECTION_KEY")
                ?? "";
        }

        if (string.IsNullOrEmpty(raw))
        {
            if (env.IsEnvironment("Testing") || env.IsDevelopment())
            {
                // Deterministic non-production fallback only. Never used when ASPNETCORE_ENVIRONMENT=Production.
                logger.LogWarning("Using development/testing webhook protection key fallback; set Integrations__WebhookProtectionKey for real deployments.");
                return SHA256.HashData("rotbeh-dev-webhook-protection-key-not-for-production"u8.ToArray());
            }

            if (!required) return [];
            throw new InvalidOperationException(
                "کلید Integrations:WebhookProtectionKey تنظیم نشده است. آن را از متغیر محیطی تأمین کن.");
        }

        try
        {
            var key = Convert.FromBase64String(raw);
            if (key.Length != 32)
                throw new InvalidOperationException("WebhookProtectionKey باید ۳۲ بایت (Base64) باشد.");
            return key;
        }
        catch (FormatException)
        {
            logger.LogError("WebhookProtectionKey is not valid Base64");
            throw new InvalidOperationException("WebhookProtectionKey نامعتبر است.");
        }
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
