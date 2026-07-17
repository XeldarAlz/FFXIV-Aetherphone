using System.Collections.Concurrent;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Crypto;

internal enum DmBodyState : byte
{
    Plain = 0,
    Decrypted = 1,
    Pending = 2,
    NoKey = 3,
    Malformed = 4,
}

internal readonly record struct DmDecryptedBody(DmBodyState State, string Text, string? FrankingKey, bool Verified)
{
    public bool IsPlaceholder => State is DmBodyState.Pending or DmBodyState.NoKey or DmBodyState.Malformed;
}

internal readonly record struct EncryptedOutbound(string Envelope, string CommitmentTag, string FrankingKeyBase64);

internal sealed class MessageCipher
{
    private readonly KeyVault vault;
    private readonly ConversationKeyStore keys;
    private readonly ConcurrentDictionary<string, DmDecryptedBody> decryptedBodies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (long AtUnix, string Text)> previewCache = new(StringComparer.Ordinal);

    public MessageCipher(KeyVault vault, ConversationKeyStore keys)
    {
        this.vault = vault;
        this.keys = keys;
    }

    public bool IsUnlocked => vault.State == KeyVaultState.Unlocked;

    public DmDecryptedBody DecryptionState(string messageId)
    {
        return decryptedBodies.TryGetValue(messageId, out var state)
            ? state
            : new DmDecryptedBody(DmBodyState.Plain, string.Empty, null, false);
    }

    public void Clear()
    {
        decryptedBodies.Clear();
        previewCache.Clear();
    }

    public void Forget(string messageId) => decryptedBodies.TryRemove(messageId, out _);

    public bool TryEncrypt(string scope, int generation, string plaintext, string senderId, out EncryptedOutbound outbound)
    {
        if (generation > 0 && keys.TryGetCek(scope, generation, out var cek))
        {
            var encoded = EnvelopeCodec.Encode(plaintext, cek, generation, scope, senderId);
            outbound = new EncryptedOutbound(encoded.Envelope, encoded.CommitmentTag, encoded.FrankingKeyBase64);
            return true;
        }

        outbound = default;
        return false;
    }

    public void RecordDecrypted(string messageId, string plaintext, string frankingKeyBase64)
    {
        decryptedBodies[messageId] = new DmDecryptedBody(DmBodyState.Decrypted, plaintext, frankingKeyBase64, true);
    }

    // Attachments and voice notes are sealed with the same conversation CEK as the text body. The AAD
    // adds a "media" domain and the message kind so a text envelope, an image, and a voice note can
    // never be swapped for one another, and neither can media from another conversation or generation.
    public bool TryEncryptMedia(string scope, int generation, byte[] plaintext, string senderId, int mediaKind,
        out byte[] sealedBytes)
    {
        if (generation > 0 && keys.TryGetCek(scope, generation, out var cek))
        {
            sealedBytes = MediaEnvelope.Seal(plaintext, cek, scope, generation, senderId, mediaKind);
            return true;
        }

        sealedBytes = Array.Empty<byte>();
        return false;
    }

    public byte[]? TryDecryptMedia(string scope, int generation, byte[] sealedBytes, string senderId, int mediaKind)
    {
        if (generation > 0 && keys.TryGetCek(scope, generation, out var cek))
        {
            return MediaEnvelope.Open(sealedBytes, cek, scope, generation, senderId, mediaKind);
        }

        return null;
    }

    public DmDecryptedBody ResolveBody(string scope, string messageId, string body, string senderId, string? commitmentTag)
    {
        if (decryptedBodies.TryGetValue(messageId, out var cached)
            && cached.State is DmBodyState.Decrypted or DmBodyState.Malformed)
        {
            return cached;
        }

        DmDecryptedBody resolved;
        if (!EnvelopeCodec.TryParseGeneration(body, out var generation))
        {
            resolved = new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false);
        }
        else if (!keys.TryGetCek(scope, generation, out var cek))
        {
            resolved = vault.State == KeyVaultState.Unlocked
                ? new DmDecryptedBody(DmBodyState.NoKey, Loc.T(L.Encryption.NoKeyPlaceholder), null, false)
                : new DmDecryptedBody(DmBodyState.Pending, Loc.T(L.Encryption.EncryptedPlaceholder), null, false);
        }
        else
        {
            var decoded = EnvelopeCodec.Decode(body, cek, scope, senderId, commitmentTag);
            resolved = decoded.Status switch
            {
                EnvelopeDecodeStatus.Success => new DmDecryptedBody(DmBodyState.Decrypted, decoded.Body,
                    decoded.FrankingKeyBase64, decoded.CommitmentVerified),
                EnvelopeDecodeStatus.WrongKey => new DmDecryptedBody(DmBodyState.NoKey,
                    Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
                _ => new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.NoKeyPlaceholder), null, false),
            };
        }

        decryptedBodies[messageId] = resolved;
        return resolved;
    }

    public string ResolveQuotedBody(string scope, string? replyToId, string? replyBody, string? replySenderId)
    {
        if (replyToId is null || string.IsNullOrEmpty(replyBody))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (decryptedBodies.TryGetValue(replyToId, out var cached) && cached.State == DmBodyState.Decrypted)
        {
            return cached.Text;
        }

        if (!EnvelopeCodec.TryParseGeneration(replyBody, out var generation))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (!keys.TryGetCek(scope, generation, out var cek))
        {
            return vault.State == KeyVaultState.Unlocked
                ? Loc.T(L.Encryption.NoKeyPlaceholder)
                : Loc.T(L.Encryption.EncryptedPlaceholder);
        }

        var decoded = EnvelopeCodec.Decode(replyBody, cek, scope, replySenderId ?? string.Empty, null);
        return decoded.Status == EnvelopeDecodeStatus.Success ? decoded.Body : Loc.T(L.Encryption.NoKeyPlaceholder);
    }

    public string ResolvePreview(string cacheKey, string scope, long atUnix, string preview, string senderId)
    {
        if (previewCache.TryGetValue(cacheKey, out var cached) && cached.AtUnix == atUnix)
        {
            return cached.Text;
        }

        var text = Loc.T(L.Encryption.EncryptedPlaceholder);
        if (EnvelopeCodec.TryParseGeneration(preview, out var generation)
            && keys.TryGetCek(scope, generation, out var cek))
        {
            var decoded = EnvelopeCodec.Decode(preview, cek, scope, senderId, null);
            if (decoded.Status == EnvelopeDecodeStatus.Success)
            {
                text = decoded.Body;
            }

            previewCache[cacheKey] = (atUnix, text);
        }

        return text;
    }
}
