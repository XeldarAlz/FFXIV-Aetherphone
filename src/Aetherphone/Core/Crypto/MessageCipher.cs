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

internal readonly record struct OutboundMedia(
    byte[] UploadBytes,
    string Body,
    int EncVersion,
    int Generation,
    string? CommitmentTag,
    string? FrankingKey);

internal sealed class MessageCipher
{
    private readonly KeyVault vault;
    private readonly ConversationKeyStore keys;
    private readonly ConcurrentDictionary<string, DmDecryptedBody> decryptedBodies = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (long AtUnix, string Text)> previewCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> generationByMessage = new(StringComparer.Ordinal);

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
        generationByMessage.Clear();
    }

    public void RecordGeneration(string messageId, int generation)
    {
        if (generation >= 1)
        {
            generationByMessage[messageId] = generation;
        }
    }

    public bool TryGetGeneration(string messageId, out int generation)
    {
        return generationByMessage.TryGetValue(messageId, out generation);
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

    public OutboundMedia? PrepareOutboundMedia(string scope, int generation, string senderId, byte[] plaintextBytes,
        string caption, int mediaKind)
    {
        if (TryEncryptMedia(scope, generation, plaintextBytes, senderId, mediaKind, out var sealedBytes)
            && TryEncrypt(scope, generation, caption, senderId, out var capEnvelope))
        {
            return new OutboundMedia(sealedBytes, capEnvelope.Envelope, EnvelopeCodec.VersionEnvelope, generation,
                capEnvelope.CommitmentTag, capEnvelope.FrankingKeyBase64);
        }

        return null;
    }

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
            resolved = vault.State is KeyVaultState.Unlocked or KeyVaultState.Locked
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

        RecordGeneration(messageId, generation);
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
            return vault.State is KeyVaultState.Unlocked or KeyVaultState.Locked
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
