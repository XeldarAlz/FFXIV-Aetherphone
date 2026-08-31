using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Translation;

internal sealed class TranslationService : IDisposable
{
    private const int BatchMax = 20;
    private const int FlushDelayMilliseconds = 150;
    private const string UndeterminedLang = "und";
    private static readonly TimeSpan FailureRetryFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StatusFreshFor = TimeSpan.FromMinutes(15);
    private static readonly string[] SurfaceNames = { "post", "comment", "dm", "bio", "ad", "muster", "venue", "story" };
    private static readonly TranslationEntry None = new(string.Empty);

    private readonly struct PendingItem
    {
        public readonly TranslationKey Key;
        public readonly string Text;

        public PendingItem(TranslationKey key, string text)
        {
            Key = key;
            Text = text;
        }
    }

    private readonly AethernetSession session;
    private readonly TranslationClient client;
    private readonly Configuration configuration;
    private readonly StoreWork work = new("Translation");
    private readonly ConcurrentDictionary<TranslationKey, TranslationEntry> entries = new();
    private readonly HashSet<string> translatedConversations = new(StringComparer.Ordinal);
    private readonly object pendingLock = new();
    private readonly List<PendingItem> pending = new();
    private bool flushScheduled;
    private string activeTarget = string.Empty;
    private string? statusToken;
    private DateTime statusFetchedUtc;
    private volatile bool statusFetching;
    private volatile bool enabled;
    private long version;

    public TranslationService(AethernetSession session, TranslationClient client, Configuration configuration)
    {
        this.session = session;
        this.client = client;
        this.configuration = configuration;
        var saved = configuration.TranslatedConversations;
        for (var index = 0; index < saved.Count; index++)
        {
            translatedConversations.Add(saved[index]);
        }

        session.Changed += OnSessionChanged;
    }

    public long Version => Interlocked.Read(ref version);

    public bool Enabled
    {
        get
        {
            EnsureFresh();
            return enabled;
        }
    }

    public string TargetLanguage =>
        configuration.TranslationTargetLanguage.Length > 0 ? configuration.TranslationTargetLanguage : Loc.Current.Code;

    public bool DisclosureSeen => configuration.TranslationDisclosureSeen;

    public void MarkDisclosureSeen()
    {
        configuration.TranslationDisclosureSeen = true;
        configuration.Save();
    }

    public bool ShouldOffer(string? lang)
    {
        if (!Enabled || lang is not { Length: > 0 } || string.Equals(lang, UndeterminedLang, StringComparison.Ordinal))
        {
            return false;
        }

        return !string.Equals(lang, TargetLanguage, StringComparison.OrdinalIgnoreCase);
    }

    public TranslationEntry Peek(in TranslationKey key)
    {
        EnsureFresh();
        return entries.TryGetValue(key, out var entry) ? entry : None;
    }

    public TranslationView View(in TranslationKey key, string original)
    {
        var entry = Peek(key);
        return entry.Showing
            ? new TranslationView(entry.Translated, entry.LayoutKey, entry)
            : new TranslationView(original, key.Id, entry);
    }

    public TranslationView View(in TranslationKey key, string original, string? lang)
    {
        var entry = Peek(key);
        if (entry.State == TranslationState.Idle && configuration.AutoTranslatePosts && configuration.TranslationDisclosureSeen
            && ShouldOffer(lang))
        {
            Request(key, original);
            entry = Peek(key);
        }

        return entry.Showing
            ? new TranslationView(entry.Translated, entry.LayoutKey, entry)
            : new TranslationView(original, key.Id, entry);
    }

    public bool IsSameAsTarget(string text)
    {
        return string.Equals(LanguageGuess.Detect(text), TargetLanguage, StringComparison.OrdinalIgnoreCase);
    }

    public void Forget(in TranslationKey key)
    {
        if (entries.TryRemove(key, out _))
        {
            Interlocked.Increment(ref version);
        }
    }

    public void Request(in TranslationKey key, string text)
    {
        if (!Enabled || text.Length == 0)
        {
            return;
        }

        var entry = entries.GetOrAdd(key, static pendingKey => new TranslationEntry(pendingKey.Id));
        var state = entry.State;
        if (state == TranslationState.Loading)
        {
            return;
        }

        if (state == TranslationState.Hidden)
        {
            entry.State = TranslationState.Shown;
            Interlocked.Increment(ref version);
            return;
        }

        if (state == TranslationState.Shown || state == TranslationState.SameLanguage)
        {
            return;
        }

        entry.State = TranslationState.Loading;
        Interlocked.Increment(ref version);
        Enqueue(new PendingItem(key, text));
    }

    public void EnsureRequested(in TranslationKey key, string text)
    {
        var entry = Peek(key);
        var state = entry.State;
        if (ReferenceEquals(entry, None) || state == TranslationState.Idle)
        {
            Request(key, text);
            return;
        }

        if (state == TranslationState.Failed
            && DateTime.UtcNow.Ticks - Interlocked.Read(ref entry.FailedAtTicks) >= FailureRetryFor.Ticks)
        {
            Request(key, text);
        }
    }

    public void ToggleOriginal(in TranslationKey key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return;
        }

        switch (entry.State)
        {
            case TranslationState.Shown:
                entry.State = TranslationState.Hidden;
                break;
            case TranslationState.Hidden:
                entry.State = TranslationState.Shown;
                break;
            default:
                return;
        }

        Interlocked.Increment(ref version);
    }

    public bool IsConversationTranslated(string conversationScope)
    {
        return translatedConversations.Contains(conversationScope);
    }

    public void SetConversationTranslated(string conversationScope, bool translated)
    {
        var changed = translated
            ? translatedConversations.Add(conversationScope)
            : translatedConversations.Remove(conversationScope);
        if (!changed)
        {
            return;
        }

        var saved = configuration.TranslatedConversations;
        if (translated)
        {
            saved.Add(conversationScope);
        }
        else
        {
            saved.Remove(conversationScope);
        }

        configuration.Save();
        Interlocked.Increment(ref version);
    }

    public void Clear()
    {
        entries.Clear();
        lock (pendingLock)
        {
            pending.Clear();
        }

        Interlocked.Increment(ref version);
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }

    private void OnSessionChanged()
    {
        if (!session.IsSignedIn)
        {
            enabled = false;
            statusToken = null;
            Clear();
        }
    }

    private void EnsureFresh()
    {
        var target = TargetLanguage;
        if (!string.Equals(target, activeTarget, StringComparison.Ordinal))
        {
            if (activeTarget.Length > 0)
            {
                Clear();
            }

            activeTarget = target;
        }

        if (!session.IsSignedIn)
        {
            return;
        }

        var token = session.Token;
        var now = DateTime.UtcNow;
        var stale = !ReferenceEquals(token, statusToken) || now - statusFetchedUtc >= StatusFreshFor;
        if (!stale || statusFetching)
        {
            return;
        }

        statusFetching = true;
        statusToken = token;
        statusFetchedUtc = now;
        work.Run("status", async cancellation =>
        {
            var status = await client.StatusAsync(cancellation).ConfigureAwait(false);
            if (status is not null)
            {
                enabled = status.Enabled;
                Interlocked.Increment(ref version);
            }
        }, () => statusFetching = false);
    }

    private void Enqueue(in PendingItem item)
    {
        lock (pendingLock)
        {
            pending.Add(item);
            if (flushScheduled)
            {
                return;
            }

            flushScheduled = true;
        }

        work.Run("translate", FlushAsync);
    }

    private async Task FlushAsync(CancellationToken cancellation)
    {
        await Task.Delay(FlushDelayMilliseconds, cancellation).ConfigureAwait(false);
        while (true)
        {
            PendingItem[] batch;
            lock (pendingLock)
            {
                if (pending.Count == 0)
                {
                    flushScheduled = false;
                    return;
                }

                var take = Math.Min(BatchMax, pending.Count);
                batch = new PendingItem[take];
                pending.CopyTo(0, batch, 0, take);
                pending.RemoveRange(0, take);
            }

            await TranslateBatchAsync(batch, cancellation).ConfigureAwait(false);
        }
    }

    private async Task TranslateBatchAsync(PendingItem[] batch, CancellationToken cancellation)
    {
        var target = activeTarget;
        var items = new TranslateBatchItem[batch.Length];
        for (var index = 0; index < batch.Length; index++)
        {
            var surface = batch[index].Key.Surface;
            items[index] = new TranslateBatchItem(index.ToString(), batch[index].Text, SurfaceNames[(int)surface],
                surface != TranslationSurface.Dm);
        }

        var response = await client.TranslateAsync(new TranslateBatchRequest(target, items), cancellation)
            .ConfigureAwait(false);
        if (response is null)
        {
            for (var index = 0; index < batch.Length; index++)
            {
                MarkFailed(batch[index].Key);
            }

            Interlocked.Increment(ref version);
            return;
        }

        var results = response.Results;
        for (var index = 0; index < results.Length; index++)
        {
            var result = results[index];
            if (!int.TryParse(result.Id, out var batchIndex) || batchIndex < 0 || batchIndex >= batch.Length)
            {
                continue;
            }

            Apply(batch[batchIndex].Key, result);
        }

        for (var index = 0; index < batch.Length; index++)
        {
            if (entries.TryGetValue(batch[index].Key, out var entry) && entry.State == TranslationState.Loading)
            {
                MarkFailed(batch[index].Key);
            }
        }

        Interlocked.Increment(ref version);
    }

    private void Apply(in TranslationKey key, TranslateBatchResult result)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return;
        }

        entry.SourceLang = result.SourceLang ?? string.Empty;
        switch (result.Status)
        {
            case TranslateStatuses.Ok when result.Text is { } text:
                entry.Translated = text;
                Plugin.Fonts.NoticeText(text);
                entry.State = TranslationState.Shown;
                break;
            case TranslateStatuses.SameLanguage:
                entry.Translated = result.Text ?? string.Empty;
                entry.State = TranslationState.SameLanguage;
                break;
            case TranslateStatuses.Quota:
            case TranslateStatuses.GlobalQuota:
                Interlocked.Exchange(ref entry.FailedAtTicks, DateTime.UtcNow.Ticks);
                entry.State = TranslationState.Quota;
                break;
            default:
                MarkFailed(key);
                break;
        }
    }

    private void MarkFailed(in TranslationKey key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return;
        }

        Interlocked.Exchange(ref entry.FailedAtTicks, DateTime.UtcNow.Ticks);
        entry.State = TranslationState.Failed;
    }
}
