using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Campaigns;

internal sealed class CampaignStore : IDisposable
{
    private static readonly TimeSpan WindowOpenInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WindowClosedInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReconnectSpread = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly AethernetSession session;
    private readonly CampaignClient client;
    private readonly NotificationService notifications;
    private readonly RealtimeSignalBus signals;
    private readonly CampaignLedger ledger;
    private readonly CampaignIconSync icons;
    private readonly StoreWork work;
    private readonly List<PhoneNotification> raised = new();

    private volatile bool loading;
    private volatile bool loadingMore;
    private volatile AepFailureBox? failureBox;
    private volatile bool immediate;
    private DateTime lastPollUtc = DateTime.MinValue;
    private DateTime scheduledUtc = DateTime.MaxValue;
    private DateTime lastTickUtc = DateTime.MinValue;
    private bool holdingNight;
    private bool wasSignedIn;
    private bool iconsRestored;

    public CampaignStore(CampaignDefinition definition, AethernetSession session, CampaignClient client,
        NotificationService notifications, Configuration configuration, RealtimeSignalBus signals,
        CampaignIconSync icons)
    {
        this.session = session;
        this.client = client;
        this.notifications = notifications;
        this.signals = signals;
        this.icons = icons;
        ledger = new CampaignLedger(definition, configuration, configuration.Save);
        work = new StoreWork(definition.AppId);
        signals.CampaignPinged += OnPinged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public CampaignLedger Ledger => ledger;

    public CampaignTexts Texts => ledger.Texts;

    public bool IsSignedIn => session.IsSignedIn;

    public CampaignDto? Campaign => ledger.Campaign;

    public CampaignPostDto[] Posts => ledger.Posts;

    public CampaignMeDto? Me => ledger.Me;

    public bool HasMorePosts => ledger.PostsCursor is not null;

    public bool LoadingMore => loadingMore;

    public bool LoadedOnce => ledger.LoadedOnce;

    public bool Failed => failureBox is not null;

    public bool AppVisible { get; set; }

    public int UnreadCount => session.IsSignedIn ? ledger.UnreadCount : 0;

    public string DisplayName => ledger.DisplayName;

    public bool IsAvailable(long nowUnix) =>
        ledger.IsAvailable(AppAvailability.IsEnabled(ledger.Definition.AppId), nowUnix);

    public int TileEffectLevel(long nowUnix) => ledger.TileEffectLevel(nowUnix);

    public void Refresh()
    {
        if (!session.IsSignedIn || loading)
        {
            return;
        }

        loading = true;
        work.Run("campaign refresh", async token =>
        {
            var reported = AepFailure.None;
            var slug = ledger.Definition.Slug;
            var campaign = await client.GetAsync(slug, token, null, failure => reported = failure)
                .ConfigureAwait(false);
            if (campaign is null)
            {
                failureBox = new AepFailureBox(reported.Failed
                    ? reported
                    : AepFailure.Transport(AepFailureKind.Offline));
                AepLog.Warning($"[{ledger.Definition.AppId}] campaign failed to load: {failureBox.Failure.Describe()}");
                return;
            }

            var posts = await client.PostsAsync(slug, null, token).ConfigureAwait(false);
            var me = await client.MeAsync(slug, token).ConfigureAwait(false);
            failureBox = null;
            await Plugin.Framework.RunOnFrameworkThread(() => ApplyFetched(campaign, posts, me)).ConfigureAwait(false);
        }, () => loading = false);
    }

    public void RefreshMe()
    {
        if (!session.IsSignedIn || !ledger.LoadedOnce)
        {
            return;
        }

        work.Run("campaign me", async token =>
        {
            var me = await client.MeAsync(ledger.Definition.Slug, token).ConfigureAwait(false);
            if (me is null)
            {
                return;
            }

            await Plugin.Framework.RunOnFrameworkThread(() => ApplyMe(me)).ConfigureAwait(false);
        });
    }

    public void LoadMorePosts()
    {
        var cursor = ledger.PostsCursor;
        if (!session.IsSignedIn || cursor is null || loadingMore || loading)
        {
            return;
        }

        loadingMore = true;
        work.Run("campaign posts", async token =>
        {
            var page = await client.PostsAsync(ledger.Definition.Slug, cursor, token).ConfigureAwait(false);
            if (page is null)
            {
                return;
            }

            await Plugin.Framework.RunOnFrameworkThread(() => ledger.ApplyPosts(page, false, raised))
                .ConfigureAwait(false);
        }, () => loadingMore = false);
    }

    private void ApplyFetched(CampaignDto campaign, CampaignPostPage? posts, CampaignMeDto? me)
    {
        ledger.ApplyCampaign(campaign, raised);
        if (posts is not null)
        {
            ledger.ApplyPosts(posts, true, raised);
        }

        if (me is not null)
        {
            ledger.ApplyMe(me, raised);
        }

        icons.Sync(campaign);
        FlushRaised();
    }

    private void ApplyMe(CampaignMeDto me)
    {
        ledger.ApplyMe(me, raised);
        FlushRaised();
    }

    private void FlushRaised()
    {
        for (var index = 0; index < raised.Count; index++)
        {
            notifications.Notify(raised[index]);
        }

        raised.Clear();
    }

    private void OnPinged()
    {
        immediate = true;
    }

    private void OnRealtimeConnected(bool active)
    {
        if (!active)
        {
            return;
        }

        var due = DateTime.UtcNow + ReconnectSpread * Random.Shared.NextDouble();
        if (due < scheduledUtc)
        {
            scheduledUtc = due;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastTickUtc < TickInterval)
        {
            return;
        }

        lastTickUtc = nowUtc;
        if (!iconsRestored)
        {
            iconsRestored = true;
            icons.Restore();
        }

        var signedIn = session.IsSignedIn;
        if (wasSignedIn && !signedIn)
        {
            ReleaseNight();
        }

        wasSignedIn = signedIn;
        if (!signedIn)
        {
            return;
        }

        var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
        EvaluateNight(nowUnix);
        if (ledger.IsRetired(nowUnix))
        {
            ledger.ClearReminders();
            return;
        }

        if (!AppAvailability.IsEnabled(ledger.Definition.AppId))
        {
            return;
        }

        ledger.FireDueReminders(nowUnix, raised);
        FlushRaised();
        if (Due(nowUtc))
        {
            Refresh();
        }
    }

    private bool Due(DateTime nowUtc)
    {
        if (immediate || nowUtc >= scheduledUtc)
        {
            immediate = false;
            scheduledUtc = DateTime.MaxValue;
            lastPollUtc = nowUtc;
            return true;
        }

        if (nowUtc - lastPollUtc < CurrentInterval(nowUtc))
        {
            return false;
        }

        lastPollUtc = nowUtc;
        return true;
    }

    private TimeSpan CurrentInterval(DateTime nowUtc)
    {
        var campaign = ledger.Campaign;
        if (campaign is null)
        {
            return WindowOpenInterval;
        }

        var nowUnix = new DateTimeOffset(nowUtc).ToUnixTimeSeconds();
        return CampaignCountdown.Evaluate(campaign, nowUnix).WindowOpen ? WindowOpenInterval : WindowClosedInterval;
    }

    private void EvaluateNight(long nowUnix)
    {
        var campaign = ledger.Campaign;
        var wanted = AppVisible && campaign is not null && ledger.KeepNight && !ledger.IsRetired(nowUnix)
                     && CampaignCountdown.Evaluate(campaign, nowUnix).WindowOpen;
        if (wanted == holdingNight)
        {
            return;
        }

        if (wanted)
        {
            holdingNight = true;
            CampaignShellBridge.HoldNight?.Invoke(ledger.Definition.AppId, CampaignShellBridge.NightMinute);
            return;
        }

        ReleaseNight();
    }

    private void ReleaseNight()
    {
        if (!holdingNight)
        {
            return;
        }

        holdingNight = false;
        CampaignShellBridge.ReleaseNight?.Invoke(ledger.Definition.AppId);
    }

    public void Dispose()
    {
        signals.CampaignPinged -= OnPinged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        Plugin.Framework.Update -= OnFrameworkUpdate;
        ReleaseNight();
        work.Dispose();
    }
}
