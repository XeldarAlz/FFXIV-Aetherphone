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
    Remembered = 5,
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
    private readonly DecryptedHistoryStore? history;
    private readonly SealedTextCache<DmDecryptedBody> decryptedBodies = new();
    private readonly SealedTextCache<string> previewCache = new();
    private readonly ConcurrentDictionary<string, int> generationByMessage = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> olderKeyMessagesByScope = new(StringComparer.Ordinal);

    public MessageCipher(KeyVault vault, ConversationKeyStore keys, DecryptedHistoryStore? history = null)
    {
        this.vault = vault;
        this.keys = keys;
        this.history = history;
    }

    public bool IsUnlocked => vault.State == KeyVaultState.Unlocked;

    public DmDecryptedBody DecryptionState(string messageId)
    {
        return decryptedBodies.TryGetLatest(messageId, out var state)
            ? state
            : new DmDecryptedBody(DmBodyState.Plain, string.Empty, null, false);
    }

    public void Clear()
    {
        decryptedBodies.Clear();
        previewCache.Clear();
        generationByMessage.Clear();
        olderKeyMessagesByScope.Clear();
    }

    public bool HasOlderKeyMessages(string scope)
    {
        return olderKeyMessagesByScope.TryGetValue(scope, out var messages) && !messages.IsEmpty;
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

    public void Forget(string messageId)
    {
        decryptedBodies.Forget(messageId);
        foreach (var messages in olderKeyMessagesByScope.Values)
        {
            messages.TryRemove(messageId, out _);
        }
    }

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

    public void RecordDecrypted(string messageId, string envelope, string plaintext, string frankingKeyBase64)
    {
        decryptedBodies.Set(messageId, envelope,
            new DmDecryptedBody(DmBodyState.Decrypted, plaintext, frankingKeyBase64, true));
    }

    public OutboundMedia PrepareOutboundMedia(string scope, int generation, string senderId, byte[] plaintextBytes,
        string caption, int mediaKind, bool encrypt)
    {
        if (encrypt
            && TryEncryptMedia(scope, generation, plaintextBytes, senderId, mediaKind, out var sealedBytes)
            && TryEncrypt(scope, generation, caption, senderId, out var capEnvelope))
        {
            return new OutboundMedia(sealedBytes, capEnvelope.Envelope, EnvelopeCodec.VersionEnvelope, generation,
                capEnvelope.CommitmentTag, capEnvelope.FrankingKeyBase64);
        }

        return new OutboundMedia(plaintextBytes, caption, EnvelopeCodec.VersionPlaintext, 0, null, null);
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

    public byte[]? TryDecryptMedia(string messageId, string scope, byte[] sealedBytes, string senderId, int mediaKind)
    {
        if (generationByMessage.TryGetValue(messageId, out var generation))
        {
            return TryDecryptMedia(scope, generation, sealedBytes, senderId, mediaKind);
        }

        AepLog.Warning(
            $"[Crypto] media kind {mediaKind} for message {messageId} in {scope} has no recorded key generation on this device.");
        return null;
    }

    public byte[]? TryDecryptMedia(string scope, int generation, byte[] sealedBytes, string senderId, int mediaKind)
    {
        if (generation <= 0 || !keys.TryGetCek(scope, generation, out var cek))
        {
            AepLog.Warning(
                $"[Crypto] media kind {mediaKind} in {scope} generation {generation} has no key on this device (vault {vault.State}, scope hydrated {keys.IsScopeHydrated(scope)}, current generation {keys.CurrentGeneration(scope)}).");
            return null;
        }

        var plain = MediaEnvelope.Open(sealedBytes, cek, scope, generation, senderId, mediaKind);
        if (plain is null)
        {
            AepLog.Warning(
                $"[Crypto] media kind {mediaKind} in {scope} generation {generation} did not open with the key on this device.");
        }

        return plain;
    }

    private DmBodyState MissingKeyState(string scope)
    {
        return vault.State switch
        {
            KeyVaultState.Locked => DmBodyState.NoKey,
            KeyVaultState.Unlocked => keys.IsScopeHydrated(scope) ? DmBodyState.NoKey : DmBodyState.Pending,
            _ => DmBodyState.Pending,
        };
    }

    private LocString MissingKeyText(string scope)
    {
        return vault.State switch
        {
            KeyVaultState.Locked => L.Encryption.LockedPlaceholder,
            KeyVaultState.Unlocked => keys.IsScopeHydrated(scope)
                ? L.Encryption.OlderKeyPlaceholder
                : L.Encryption.DecryptingPlaceholder,
            KeyVaultState.Provisioning => L.Encryption.SettingUp,
            KeyVaultState.Unsupported => L.Encryption.UnsupportedSummary,
            _ => L.Encryption.EncryptedPlaceholder,
        };
    }

    public DmDecryptedBody ResolveBody(string scope, string messageId, string body, string senderId, string? commitmentTag)
    {
        if (decryptedBodies.TryGet(messageId, body, out var cached)
            && cached.State is DmBodyState.Decrypted or DmBodyState.Malformed or DmBodyState.Remembered)
        {
            return cached;
        }

        DmDecryptedBody resolved;
        if (!EnvelopeCodec.TryParseGeneration(body, out var generation))
        {
            resolved = new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.DamagedPlaceholder), null, false);
        }
        else if (!keys.TryGetCek(scope, generation, out var cek))
        {
            resolved = new DmDecryptedBody(MissingKeyState(scope), Loc.T(MissingKeyText(scope)), null, false);
        }
        else
        {
            var decoded = EnvelopeCodec.Decode(body, cek, scope, senderId, commitmentTag);
            resolved = decoded.Status switch
            {
                EnvelopeDecodeStatus.Success => new DmDecryptedBody(DmBodyState.Decrypted, decoded.Body,
                    decoded.FrankingKeyBase64, decoded.CommitmentVerified),
                EnvelopeDecodeStatus.WrongKey => new DmDecryptedBody(DmBodyState.NoKey,
                    Loc.T(L.Encryption.OlderKeyPlaceholder), null, false),
                _ => new DmDecryptedBody(DmBodyState.Malformed, Loc.T(L.Encryption.DamagedPlaceholder), null, false),
            };
        }

        if (resolved.State == DmBodyState.Decrypted)
        {
            history?.Remember(messageId, resolved.Text);
        }
        else if (resolved.State is DmBodyState.NoKey or DmBodyState.Pending
                 && history is not null && history.TryGet(messageId, out var remembered))
        {
            resolved = new DmDecryptedBody(DmBodyState.Remembered, remembered, null, false);
        }

        RecordGeneration(messageId, generation);
        TrackOlderKeyMessage(scope, messageId,
            resolved.State == DmBodyState.NoKey && vault.State == KeyVaultState.Unlocked);

        if (resolved.State is DmBodyState.NoKey or DmBodyState.Malformed
            && (!decryptedBodies.TryGetLatest(messageId, out var previous) || previous.State != resolved.State))
        {
            AepLog.Warning(
                $"[Crypto] message {messageId} in {scope} generation {generation} resolved as {resolved.State} (vault {vault.State}, scope hydrated {keys.IsScopeHydrated(scope)}, current generation {keys.CurrentGeneration(scope)}).");
        }

        decryptedBodies.Set(messageId, body, resolved);
        return resolved;
    }

    private void TrackOlderKeyMessage(string scope, string messageId, bool sealedToAnOlderKey)
    {
        if (sealedToAnOlderKey)
        {
            var messages = olderKeyMessagesByScope.GetOrAdd(scope,
                _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
            messages[messageId] = 1;
            return;
        }

        if (olderKeyMessagesByScope.TryGetValue(scope, out var tracked))
        {
            tracked.TryRemove(messageId, out _);
        }
    }

    public string ResolveQuotedBody(string scope, string? replyToId, string? replyBody, string? replySenderId)
    {
        if (replyToId is null || string.IsNullOrEmpty(replyBody))
        {
            return Loc.T(L.Encryption.EncryptedPlaceholder);
        }

        if (decryptedBodies.TryGetLatest(replyToId, out var cached) && cached.State == DmBodyState.Decrypted)
        {
            return cached.Text;
        }

        if (!EnvelopeCodec.TryParseGeneration(replyBody, out var generation))
        {
            return Loc.T(L.Encryption.NoKeyPlaceholder);
        }

        if (!keys.TryGetCek(scope, generation, out var cek))
        {
            return Loc.T(MissingKeyText(scope));
        }

        var decoded = EnvelopeCodec.Decode(replyBody, cek, scope, replySenderId ?? string.Empty, null);
        return decoded.Status == EnvelopeDecodeStatus.Success ? decoded.Body : Loc.T(L.Encryption.OlderKeyPlaceholder);
    }

    public string ResolvePreview(string cacheKey, string scope, string preview, string senderId)
    {
        if (previewCache.TryGet(cacheKey, preview, out var cached))
        {
            return cached;
        }

        var text = Loc.T(L.Encryption.EncryptedPlaceholder);
        if (!EnvelopeCodec.TryParseGeneration(preview, out var generation))
        {
            return Loc.T(L.Encryption.DamagedPlaceholder);
        }

        if (!keys.TryGetCek(scope, generation, out var cek))
        {
            keys.RequestPreviewHydrate(scope);
            return Loc.T(MissingKeyText(scope));
        }

        var decoded = EnvelopeCodec.Decode(preview, cek, scope, senderId, null);
        if (decoded.Status == EnvelopeDecodeStatus.Success)
        {
            text = decoded.Body;
        }
        else
        {
            AepLog.Warning($"[Crypto] preview for {scope} failed to decode as {decoded.Status}.");
        }

        previewCache.Set(cacheKey, preview, text);
        return text;
    }

    public bool IsPreviewResolved(string cacheKey, string preview)
    {
        return previewCache.TryGet(cacheKey, preview, out _);
    }
}
