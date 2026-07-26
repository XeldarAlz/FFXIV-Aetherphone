using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.YellowPages;

internal sealed class AdInquiryStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan ThreadPollInterval = TimeSpan.FromSeconds(4);

    private readonly AethernetSession session;
    private readonly YellowPagesClient client;
    private readonly ConversationKeyStore keys;
    private readonly KeyVault vault;
    private readonly MessageCipher cipher;
    private readonly RealtimeSignalBus signals;
    private readonly AppGate gate;
    private readonly PollCadence cadence;
    private readonly StoreWork work = new("YellowPagesInquiries");

    private string? lastAccountId;
    private volatile AdInquiryDto[] threads = Array.Empty<AdInquiryDto>();
    private volatile AdInquiryMessageDto[] messages = Array.Empty<AdInquiryMessageDto>();
    private volatile bool loading;
    private volatile bool loadedOnce;
    private volatile bool sending;
    private string? openThreadId;
    private DateTime nextThreadPoll = DateTime.MinValue;

    public AdInquiryStore(AethernetSession session, YellowPagesClient client, KeyVault vault,
        ConversationKeyStore keys, PhoneVisibility visibility, RealtimeSignalBus signals, AppGate gate)
    {
        this.session = session;
        this.client = client;
        this.keys = keys;
        this.vault = vault;
        cipher = new MessageCipher(vault, keys);
        this.signals = signals;
        this.gate = gate;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        session.Changed += OnSessionChanged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public string MyUserId => session.CurrentUser?.Id ?? string.Empty;

    public bool CanEncrypt => cipher.IsUnlocked;

    public KeyVault Vault => vault;

    public KeyVaultState VaultState => vault.State;

    public string ScopeFor(string otherUserId) =>
        ConversationKeyStore.AdScope(ConversationKeyStore.Pair(MyUserId, otherUserId));

    /// <summary>Plaintext for display. Bodies always travel and rest as sealed envelopes.</summary>
    public string Reveal(string otherUserId, AdInquiryMessageDto message)
    {
        if (message.EncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return message.Body;
        }

        return cipher.ResolveBody(ScopeFor(otherUserId), message.Id, message.Body, message.SenderId,
            message.CommitmentTag).Text;
    }

    public string RevealPreview(AdInquiryDto thread)
    {
        if (thread.LastEncVersion != EnvelopeCodec.VersionEnvelope || thread.LastMessageId is null)
        {
            return thread.LastBody;
        }

        return cipher.ResolveBody(ScopeFor(thread.OtherUserId), thread.LastMessageId, thread.LastBody,
            thread.LastSenderId, thread.LastCommitmentTag).Text;
    }

    public AdInquiryDto[] Threads => threads;

    public AdInquiryMessageDto[] Messages => messages;

    public bool Loading => loading;

    public bool LoadedOnce => loadedOnce;

    public bool Sending => sending;

    public string? OpenThreadId => openThreadId;

    public int UnreadTotal
    {
        get
        {
            var snapshot = threads;
            var total = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                total += snapshot[index].UnreadCount;
            }

            return total;
        }
    }

    public int UnreadForAd(string adId)
    {
        var snapshot = threads;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AdId == adId)
            {
                total += snapshot[index].UnreadCount;
            }
        }

        return total;
    }

    public int CountForAd(string adId)
    {
        var snapshot = threads;
        var total = 0;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].AdId == adId)
            {
                total++;
            }
        }

        return total;
    }

    public AdInquiryDto? Thread(string inquiryId)
    {
        var snapshot = threads;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == inquiryId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        work.Run("inquiries", async token =>
        {
            await keys.HydrateAdsAsync(token).ConfigureAwait(false);
            var page = await client.InquiriesAsync(null, token).ConfigureAwait(false);
            if (page is not null)
            {
                threads = page.Items;
                loadedOnce = true;
            }
        }, () => loading = false);
    }

    public void Open(string inquiryId)
    {
        openThreadId = inquiryId;
        messages = Array.Empty<AdInquiryMessageDto>();
        nextThreadPoll = DateTime.MinValue;
        HydrateKeys();
        FetchMessages(inquiryId);
        MarkRead(inquiryId);
    }

    public void Close()
    {
        openThreadId = null;
        messages = Array.Empty<AdInquiryMessageDto>();
    }

    public void Send(string inquiryId, string otherUserId, string body, Action<bool> done)
    {
        if (!session.IsSignedIn || sending)
        {
            done(false);
            return;
        }

        sending = true;
        work.Run("inquiry send", async token =>
        {
            var sealedBody = await SealAsync(otherUserId, body, token).ConfigureAwait(false);
            if (sealedBody is null)
            {
                return false;
            }

            var sent = await client.SendInquiryAsync(inquiryId, sealedBody.Value.Envelope,
                sealedBody.Value.CommitmentTag, token).ConfigureAwait(false);
            if (sent is null)
            {
                return false;
            }

            cipher.RecordDecrypted(sent.Id, body, sealedBody.Value.FrankingKeyBase64);
            messages = Append(messages, sent);
            return true;
        }, ok =>
        {
            sending = false;
            if (ok)
            {
                Refresh();
            }

            done(ok);
        });
    }

    /// <summary>Seals a body for the pair, minting the thread key generation when this is the first
    /// message. Returns null when the vault is locked or the other side has published no key, and the
    /// caller must not fall back to plaintext: the server rejects anything that is not an envelope.</summary>
    private async Task<EncryptedOutbound?> SealAsync(string otherUserId, string body, CancellationToken token)
    {
        if (!cipher.IsUnlocked)
        {
            return null;
        }

        var status = await keys.EnsureAdKeysAsync(otherUserId, MyUserId, token).ConfigureAwait(false);
        if (!status.CanEncrypt)
        {
            return null;
        }

        var scope = ScopeFor(otherUserId);
        return cipher.TryEncrypt(scope, keys.CurrentGeneration(scope), body, MyUserId, out var encoded)
            ? encoded
            : null;
    }

    /// <summary>Opens or reuses the thread for an ad and posts the first message.</summary>
    public void OpenForAd(string adId, string ownerId, string body, Action<AdInquiryDto?> done)
    {
        if (!session.IsSignedIn)
        {
            done(null);
            return;
        }

        AdInquiryDto? opened = null;
        work.Run("inquiry open", async token =>
        {
            var sealedBody = await SealAsync(ownerId, body, token).ConfigureAwait(false);
            if (sealedBody is null)
            {
                return false;
            }

            opened = await client.OpenInquiryAsync(adId, sealedBody.Value.Envelope,
                sealedBody.Value.CommitmentTag, token).ConfigureAwait(false);
            return opened is not null;
        }, ok =>
        {
            if (ok)
            {
                Refresh();
            }

            done(opened);
        });
    }

    private void HydrateKeys()
    {
        work.Run("inquiry keys", async token =>
        {
            await keys.HydrateAdsAsync(token).ConfigureAwait(false);
        });
    }

    private void FetchMessages(string inquiryId)
    {
        work.Run("inquiry messages", async token =>
        {
            var page = await client.InquiryMessagesAsync(inquiryId, null, token).ConfigureAwait(false);
            if (page is not null && openThreadId == inquiryId)
            {
                messages = page.Items;
            }
        });
    }

    private void MarkRead(string inquiryId)
    {
        work.Run("inquiry read", async token =>
        {
            await client.MarkInquiryReadAsync(inquiryId, token).ConfigureAwait(false);
        }, () => ClearUnread(inquiryId));
    }

    private void ClearUnread(string inquiryId)
    {
        var snapshot = threads;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id != inquiryId || snapshot[index].UnreadCount == 0)
            {
                continue;
            }

            var next = (AdInquiryDto[])snapshot.Clone();
            next[index] = next[index] with { UnreadCount = 0 };
            threads = next;
            return;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || !gate.Open)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (openThreadId is { } inquiryId && now >= nextThreadPoll)
        {
            nextThreadPoll = now + ThreadPollInterval;
            FetchMessages(inquiryId);
        }

        if (cadence.Due(now))
        {
            Refresh();
        }
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (connected)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        threads = Array.Empty<AdInquiryDto>();
        messages = Array.Empty<AdInquiryMessageDto>();
        openThreadId = null;
        loadedOnce = false;
        cipher.Clear();
        cadence.Reset();
    }

    private static AdInquiryMessageDto[] Append(AdInquiryMessageDto[] source, AdInquiryMessageDto message)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id == message.Id)
            {
                return source;
            }
        }

        var next = new AdInquiryMessageDto[source.Length + 1];
        Array.Copy(source, next, source.Length);
        next[^1] = message;
        return next;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
