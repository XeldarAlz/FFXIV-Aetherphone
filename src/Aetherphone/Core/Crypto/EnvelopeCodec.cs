using System.Security.Cryptography;
using System.Text;

namespace Aetherphone.Core.Crypto;

internal enum EnvelopeDecodeStatus
{
    Success = 0,
    Malformed = 1,
    WrongKey = 2,
}

internal readonly record struct EnvelopeDecodeResult(
    EnvelopeDecodeStatus Status,
    string Body,
    string? FrankingKeyBase64,
    bool CommitmentVerified);

internal readonly record struct EncodedEnvelope(string Envelope, string CommitmentTag, string FrankingKeyBase64);

internal static class EnvelopeCodec
{
    public const string Prefix = "AE1.";
    public const int VersionPlaintext = 0;
    public const int VersionEnvelope = 1;
    private const byte ContentTypeText = 0x01;
    private const int FrankingKeyBytes = 32;

    public static bool IsEnvelope(string? body)
    {
        return body is not null && body.StartsWith(Prefix, StringComparison.Ordinal);
    }

    public static EncodedEnvelope Encode(string body, byte[] cek, int generation, string scopeId, string senderId)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var inner = new byte[1 + FrankingKeyBytes + bodyBytes.Length];
        inner[0] = ContentTypeText;
        RandomNumberGenerator.Fill(inner.AsSpan(1, FrankingKeyBytes));
        bodyBytes.CopyTo(inner.AsSpan(1 + FrankingKeyBytes));

        var commitmentTag = Convert.ToBase64String(CryptoBox.Hmac(inner.AsSpan(1, FrankingKeyBytes), bodyBytes));
        var frankingKey = Convert.ToBase64String(inner.AsSpan(1, FrankingKeyBytes));
        var sealedBytes = CryptoBox.Seal(inner, cek, Aad(scopeId, generation, senderId));
        return new EncodedEnvelope($"{Prefix}{generation}.{Convert.ToBase64String(sealedBytes)}", commitmentTag, frankingKey);
    }

    public static bool TryParseGeneration(string? body, out int generation)
    {
        generation = 0;
        if (body is null || !body.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = body.IndexOf('.', Prefix.Length);
        if (separatorIndex <= Prefix.Length)
        {
            return false;
        }

        return int.TryParse(body.AsSpan(Prefix.Length, separatorIndex - Prefix.Length), out generation) && generation >= 1;
    }

    public static EnvelopeDecodeResult Decode(string envelope, byte[] cek, string scopeId, string senderId, string? commitmentTag)
    {
        if (!TryParseGeneration(envelope, out var generation))
        {
            return new EnvelopeDecodeResult(EnvelopeDecodeStatus.Malformed, string.Empty, null, false);
        }

        var payloadStart = envelope.IndexOf('.', Prefix.Length) + 1;
        byte[] sealedBytes;
        try
        {
            sealedBytes = Convert.FromBase64String(envelope[payloadStart..]);
        }
        catch (FormatException)
        {
            return new EnvelopeDecodeResult(EnvelopeDecodeStatus.Malformed, string.Empty, null, false);
        }

        var inner = CryptoBox.Open(sealedBytes, cek, Aad(scopeId, generation, senderId));
        if (inner is null)
        {
            return new EnvelopeDecodeResult(EnvelopeDecodeStatus.WrongKey, string.Empty, null, false);
        }

        if (inner.Length < 1 + FrankingKeyBytes || inner[0] != ContentTypeText)
        {
            return new EnvelopeDecodeResult(EnvelopeDecodeStatus.Malformed, string.Empty, null, false);
        }

        var bodyBytes = inner.AsSpan(1 + FrankingKeyBytes);
        var body = Encoding.UTF8.GetString(bodyBytes);
        var frankingKey = inner.AsSpan(1, FrankingKeyBytes);
        var verified = false;
        if (commitmentTag is not null)
        {
            var expected = CryptoBox.Hmac(frankingKey, bodyBytes);
            verified = TryDecodeBase64Tag(commitmentTag, out var actual)
                && CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        return new EnvelopeDecodeResult(
            EnvelopeDecodeStatus.Success,
            body,
            Convert.ToBase64String(frankingKey),
            verified);
    }

    private static byte[] Aad(string scopeId, int generation, string senderId)
    {
        return Encoding.UTF8.GetBytes($"{scopeId}|{generation}|{senderId}");
    }

    private static bool TryDecodeBase64Tag(string tag, out byte[] decoded)
    {
        try
        {
            decoded = Convert.FromBase64String(tag);
            return decoded.Length == 32;
        }
        catch (FormatException)
        {
            decoded = Array.Empty<byte>();
            return false;
        }
    }
}
