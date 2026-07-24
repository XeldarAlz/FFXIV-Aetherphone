using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Muster;

/// <summary>Account-scoped in-memory muster state. Every muster.ping and reconnect triggers a full-replace
/// sync fetch; notifications are derived by diffing consecutive snapshots, so a missed push degrades to the
/// backstop poll instead of a lost alert. Never persisted, so no stale hosting markers survive a restart.</summary>
internal sealed class MusterStore : IDisposable
{
    public const string AppId = "muster";

    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);

    private readonly AethernetSession session;
    private readonly MusterClient client;
    private readonly NotificationService notifications;
    private readonly Configuration configuration;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence cadence;
    private readonly StoreWork work = new("Muster");

    private string? lastAccountId;
    private volatile MusterDto? mine;
    private volatile MusterAttendeeDto[] mineAttendees = Array.Empty<MusterAttendeeDto>();
    private volatile MusterDto[] contactMusters = Array.Empty<MusterDto>();
    private volatile HashSet<string> goingIds = new(StringComparer.Ordinal);
    private volatile Dictionary<string, MusterDto> knownMusters = new(StringComparer.Ordinal);
    private volatile bool primed;
    private volatile bool syncing;

    private readonly object chatLock = new();
    private readonly HashSet<string> chatRequested = new(StringComparer.Ordinal);
    private readonly HashSet<string> chatMisses = new(StringComparer.Ordinal);
    private volatile Dictionary<string, MusterDto> chatMusters = new(StringComparer.Ordinal);

    private volatile MusterDto[] directory = Array.Empty<MusterDto>();
    private string? directoryCursor;
    private volatile bool directoryLoading;
    private volatile bool directoryLoadingMore;
    private volatile bool directoryHasMore;
    private volatile bool directoryLoadedOnce;
    private int directoryGeneration;

    public MusterStore(AethernetSession session, MusterClient client, NotificationService notifications,
        Configuration configuration, PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.configuration = configuration;
        this.signals = signals;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        session.Changed += OnSessionChanged;
        signals.MusterPinged += OnMusterPinged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool IsSignedIn => session.IsSignedIn;

    public bool Syncing => syncing;

    public bool Primed => primed;

    public MusterDto? Mine => mine;

    public MusterAttendeeDto[] MineAttendees => mineAttendees;

    public MusterDto[] ContactMusters => contactMusters;

    public MusterDto[] Directory => directory;

    public bool DirectoryLoading => directoryLoading;

    public bool DirectoryLoadingMore => directoryLoadingMore;

    public bool DirectoryHasMore => directoryHasMore;

    public bool DirectoryLoadedOnce => directoryLoadedOnce;

    public bool IsGoing(string musterId)
    {
        return goingIds.Contains(musterId);
    }

    public MusterDto? ContactMusterFor(string hostAccountId)
    {
        var snapshot = contactMusters;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].HostId == hostAccountId && snapshot[index].EndsAtUnix > nowUnix)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    public void SyncNow()
    {
        if (!session.IsSignedIn || syncing)
        {
            return;
        }

        syncing = true;
        work.Run("muster sync", async token =>
        {
            var sync = await client.SyncAsync(token).ConfigureAwait(false);
            if (sync is not null)
            {
                ApplySync(sync);
            }
        }, () => syncing = false);
    }

    public void RefreshDirectory()
    {
        if (!session.IsSignedIn || directoryLoading)
        {
            return;
        }

        directoryLoading = true;
        var generation = Interlocked.Increment(ref directoryGeneration);
        work.Run("muster directory", async token =>
        {
            var page = await client.DirectoryAsync(configuration.MusterCategoryFilter,
                configuration.MusterRegionFilter, null, token).ConfigureAwait(false);
            if (page is not null && generation == Volatile.Read(ref directoryGeneration))
            {
                directory = page.Items;
                directoryCursor = page.NextCursor;
                directoryHasMore = page.NextCursor is not null;
                directoryLoadedOnce = true;
                MergeKnown(page.Items);
            }
        }, () => directoryLoading = false);
    }

    public void LoadMoreDirectory()
    {
        if (!session.IsSignedIn || directoryLoading || directoryLoadingMore || directoryCursor is null)
        {
            return;
        }

        directoryLoadingMore = true;
        var generation = Volatile.Read(ref directoryGeneration);
        var cursor = directoryCursor;
        work.Run("muster directory more", async token =>
        {
            var page = await client.DirectoryAsync(configuration.MusterCategoryFilter,
                configuration.MusterRegionFilter, cursor, token).ConfigureAwait(false);
            if (page is not null && generation == Volatile.Read(ref directoryGeneration))
            {
                directory = AppendNew(directory, page.Items);
                directoryCursor = page.NextCursor;
                directoryHasMore = page.NextCursor is not null;
                MergeKnown(page.Items);
            }
        }, () => directoryLoadingMore = false);
    }

    /// <summary>Resolves a chat invite token without re-fetching per frame: the first call queues one
    /// fetch, later calls read the cache. A miss is remembered so ended invites never retry.</summary>
    public MusterChatResolution ResolveForChat(string musterId)
    {
        if (knownMusters.TryGetValue(musterId, out var known))
        {
            return new MusterChatResolution(known, Missed: false);
        }

        if (chatMusters.TryGetValue(musterId, out var cached))
        {
            return new MusterChatResolution(cached, Missed: false);
        }

        if (!session.IsSignedIn)
        {
            return new MusterChatResolution(null, Missed: false);
        }

        lock (chatLock)
        {
            if (chatMisses.Contains(musterId))
            {
                return new MusterChatResolution(null, Missed: true);
            }

            if (!chatRequested.Add(musterId))
            {
                return new MusterChatResolution(null, Missed: false);
            }
        }

        work.Run("muster chat resolve", async token =>
        {
            var muster = await client.GetAsync(musterId, token).ConfigureAwait(false);
            if (muster is not null)
            {
                var next = new Dictionary<string, MusterDto>(chatMusters, StringComparer.Ordinal);
                next[muster.Id] = muster;
                chatMusters = next;
                return;
            }

            lock (chatLock)
            {
                chatMisses.Add(musterId);
            }
        });
        return new MusterChatResolution(null, Missed: false);
    }

    public void FetchDetail(string musterId, Action<MusterDto?> done)
    {
        if (!session.IsSignedIn)
        {
            done(null);
            return;
        }

        work.Run("muster detail", async token =>
        {
            var muster = await client.GetAsync(musterId, token).ConfigureAwait(false);
            if (muster is not null)
            {
                MergeKnown(new[] { muster });
            }

            done(muster);
        });
    }

    public void Create(CreateMusterRequest request, Action<MusterCreateOutcome> done)
    {
        if (!session.IsSignedIn)
        {
            done(MusterCreateOutcome.Failed);
            return;
        }

        var status = 0;
        work.Run("muster create", async token =>
        {
            var created = await client.CreateAsync(request, token, code => status = code).ConfigureAwait(false);
            if (created is null)
            {
                return false;
            }

            mine = created;
            mineAttendees = Array.Empty<MusterAttendeeDto>();
            MergeKnown(new[] { created });
            return true;
        }, ok => done(ok ? MusterCreateOutcome.Created : status switch
        {
            409 => MusterCreateOutcome.AlreadyHosting,
            400 => MusterCreateOutcome.Invalid,
            429 => MusterCreateOutcome.RateLimited,
            _ => MusterCreateOutcome.Failed,
        }));
    }

    public void EndMine(Action<bool> done)
    {
        var current = mine;
        if (current is null || !session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("muster end", async token =>
            await client.EndAsync(current.Id, token).ConfigureAwait(false),
            ok =>
            {
                if (ok)
                {
                    mine = null;
                    mineAttendees = Array.Empty<MusterAttendeeDto>();
                }

                done(ok);
            });
    }

    public void SetRsvp(string musterId, bool going, Action<bool> done)
    {
        if (!session.IsSignedIn)
        {
            done(false);
            return;
        }

        work.Run("muster rsvp", async token =>
        {
            var result = await client.RsvpAsync(musterId, going, token).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            ApplyRsvpResult(musterId, result);
            return true;
        }, done);
    }

    private void OnMusterPinged()
    {
        cadence.RequestImmediate();
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (connected)
        {
            cadence.RequestImmediate();
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn)
        {
            primed = false;
            return;
        }

        if (!cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        SyncNow();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        mine = null;
        mineAttendees = Array.Empty<MusterAttendeeDto>();
        contactMusters = Array.Empty<MusterDto>();
        goingIds = new HashSet<string>(StringComparer.Ordinal);
        knownMusters = new Dictionary<string, MusterDto>(StringComparer.Ordinal);
        lock (chatLock)
        {
            chatRequested.Clear();
            chatMisses.Clear();
        }

        chatMusters = new Dictionary<string, MusterDto>(StringComparer.Ordinal);
        directory = Array.Empty<MusterDto>();
        directoryCursor = null;
        directoryHasMore = false;
        directoryLoadedOnce = false;
        primed = false;
        cadence.Reset();
    }

    private void ApplySync(MusterSync sync)
    {
        var previousContacts = contactMusters;
        var previousAttendees = mineAttendees;
        var previousGoing = goingIds;
        var previousKnown = knownMusters;
        var previousMineId = mine?.Id;
        var wasPrimed = primed;

        var nextGoing = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sync.GoingMusterIds.Length; index++)
        {
            nextGoing.Add(sync.GoingMusterIds[index]);
        }

        mine = sync.Mine;
        mineAttendees = sync.MineAttendees;
        contactMusters = sync.ContactMusters;
        goingIds = nextGoing;
        RebuildKnown(previousKnown, sync, nextGoing);
        primed = true;

        if (!wasPrimed)
        {
            return;
        }

        NotifyNewContactMusters(previousContacts, sync.ContactMusters);
        NotifyNewAttendees(previousAttendees, sync, previousMineId);
        NotifyEndedRsvps(previousGoing, nextGoing, previousKnown);
    }

    private void NotifyNewContactMusters(MusterDto[] previous, MusterDto[] current)
    {
        for (var index = 0; index < current.Length; index++)
        {
            var muster = current[index];
            var seen = false;
            for (var previousIndex = 0; previousIndex < previous.Length; previousIndex++)
            {
                if (previous[previousIndex].Id == muster.Id)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Muster.NotifStartedTitle),
                    Loc.T(L.Muster.NotifStartedBody, muster.HostDisplayName), DateTime.Now,
                    AppAccents.For(AppId), muster.Id));
            }
        }
    }

    private void NotifyNewAttendees(MusterAttendeeDto[] previous, MusterSync sync, string? previousMineId)
    {
        if (sync.Mine is null || sync.Mine.Id != previousMineId)
        {
            return;
        }

        for (var index = 0; index < sync.MineAttendees.Length; index++)
        {
            var attendee = sync.MineAttendees[index];
            var seen = false;
            for (var previousIndex = 0; previousIndex < previous.Length; previousIndex++)
            {
                if (previous[previousIndex].UserId == attendee.UserId)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Muster.NotifRsvpTitle),
                    Loc.T(L.Muster.NotifRsvpBody, attendee.DisplayName), DateTime.Now,
                    AppAccents.For(AppId), sync.Mine.Id));
            }
        }
    }

    private void NotifyEndedRsvps(HashSet<string> previousGoing, HashSet<string> nextGoing,
        Dictionary<string, MusterDto> previousKnown)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var musterId in previousGoing)
        {
            if (nextGoing.Contains(musterId))
            {
                continue;
            }

            if (previousKnown.TryGetValue(musterId, out var muster) && muster.EndsAtUnix > nowUnix)
            {
                notifications.Notify(new PhoneNotification(AppId, Loc.T(L.Muster.NotifEndedTitle),
                    Loc.T(L.Muster.NotifEndedBody), DateTime.Now, AppAccents.For(AppId)));
            }
        }
    }

    private void RebuildKnown(Dictionary<string, MusterDto> previous, MusterSync sync, HashSet<string> going)
    {
        var next = new Dictionary<string, MusterDto>(StringComparer.Ordinal);
        if (sync.Mine is not null)
        {
            next[sync.Mine.Id] = sync.Mine;
        }

        for (var index = 0; index < sync.ContactMusters.Length; index++)
        {
            next[sync.ContactMusters[index].Id] = sync.ContactMusters[index];
        }

        var directorySnapshot = directory;
        for (var index = 0; index < directorySnapshot.Length; index++)
        {
            next[directorySnapshot[index].Id] = directorySnapshot[index];
        }

        foreach (var musterId in going)
        {
            if (!next.ContainsKey(musterId) && previous.TryGetValue(musterId, out var retained))
            {
                next[musterId] = retained;
            }
        }

        knownMusters = next;
    }

    private void MergeKnown(IReadOnlyList<MusterDto> musters)
    {
        if (musters.Count == 0)
        {
            return;
        }

        var next = new Dictionary<string, MusterDto>(knownMusters, StringComparer.Ordinal);
        for (var index = 0; index < musters.Count; index++)
        {
            next[musters[index].Id] = musters[index];
        }

        knownMusters = next;
    }

    private void ApplyRsvpResult(string musterId, MusterRsvpResult result)
    {
        var nextGoing = new HashSet<string>(goingIds, StringComparer.Ordinal);
        if (result.Going)
        {
            nextGoing.Add(musterId);
        }
        else
        {
            nextGoing.Remove(musterId);
        }

        goingIds = nextGoing;
        directory = WithRsvp(directory, musterId, result);
        contactMusters = WithRsvp(contactMusters, musterId, result);
        if (knownMusters.TryGetValue(musterId, out var cached))
        {
            MergeKnown(new[] { cached with { RsvpCount = result.RsvpCount, Going = result.Going } });
        }
    }

    private static MusterDto[] WithRsvp(MusterDto[] source, string musterId, MusterRsvpResult result)
    {
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index].Id != musterId)
            {
                continue;
            }

            var next = (MusterDto[])source.Clone();
            next[index] = next[index] with { RsvpCount = result.RsvpCount, Going = result.Going };
            return next;
        }

        return source;
    }

    private static MusterDto[] AppendNew(MusterDto[] existing, MusterDto[] incoming)
    {
        if (incoming.Length == 0)
        {
            return existing;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < existing.Length; index++)
        {
            seen.Add(existing[index].Id);
        }

        var merged = new List<MusterDto>(existing.Length + incoming.Length);
        merged.AddRange(existing);
        for (var index = 0; index < incoming.Length; index++)
        {
            if (seen.Add(incoming[index].Id))
            {
                merged.Add(incoming[index]);
            }
        }

        return merged.ToArray();
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.MusterPinged -= OnMusterPinged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
