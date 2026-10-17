using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Chirper.Widgets;

internal sealed class ChirperWidget : IHomeWidget
{
    private const string ChirperId = "chirper";
    private const int ForYouTab = 0;
    private const int LatestTab = 1;
    private const int ActivityTab = 2;
    private const int TabCount = 3;
    private const int TabControlBase = 100;
    private const int MediumRows = 3;
    private const int LargeRows = 7;
    private const int RefreshMilliseconds = 1000;
    private const long LoadRetryMilliseconds = 60_000;
    private const float ForcedRefreshSeconds = 30f;
    private const float ActivityRelevance = 0.6f;
    private const float TabGap = 4f;
    private const float TabHitPadding = 6f;
    private const float RowUnits = 40f;

    private static readonly LocString[] TabLabels = { L.Chirper.ForYou, L.Social.FeedLatest, L.Social.ActivityTitle };
    private static readonly LocString[] SampleTexts =
    {
        L.WidgetsPeople.SampleMessage,
        L.WidgetsPeople.SampleMessageRaid,
        L.WidgetsPeople.SampleMessageThanks,
    };

    private sealed class TabState
    {
        public int Tab;
    }

    private readonly struct ChirperRow
    {
        public readonly string Name;
        public readonly string World;
        public readonly string? AvatarUrl;
        public readonly string FrameId;
        public readonly string Subtitle;
        public readonly string Time;
        public readonly bool Unread;
        public readonly WidgetRoute Route;

        public ChirperRow(string name, string world, string? avatarUrl, string frameId, string subtitle, string time,
            bool unread, WidgetRoute route)
        {
            Name = name;
            World = world;
            AvatarUrl = avatarUrl;
            FrameId = frameId;
            Subtitle = subtitle;
            Time = time;
            Unread = unread;
            Route = route;
        }
    }

    private readonly IPhoneApp? app;
    private readonly AethernetSession session;
    private readonly SocialNotificationService activity;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly WidgetStates<TabState> states = new();
    private readonly ChirperRow[][] rows =
    {
        new ChirperRow[LargeRows], new ChirperRow[LargeRows], new ChirperRow[LargeRows],
    };

    private readonly int[] counts = new int[TabCount];
    private readonly object?[] seenSources = new object?[TabCount];
    private readonly long[] loadAttempts = new long[TabCount];
    private WidgetRefresh refresh;
    private WidgetFrame frame;
    private float sinceRebuild;

    public ChirperWidget(WidgetServices services)
    {
        var phone = services.Phone;
        session = phone.AethernetSession;
        activity = phone.SocialNotifications;
        images = phone.RemoteImages;
        lodestone = phone.Lodestone;
        for (var index = 0; index < services.Apps.Count; index++)
        {
            if (services.Apps[index].Id == ChirperId && services.Apps[index] is ISocialFeed)
            {
                app = services.Apps[index];
            }
        }
    }

    public string Id => "chirper.feed";
    public string DisplayName => Loc.T(L.Apps.Chirper);
    public string Description => Loc.T(L.WidgetsPeople.ChirperDescription);
    public string AppId => ChirperId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) =>
        session.IsSignedIn && activity.UnseenCount(ChirperId) > 0 ? ActivityRelevance : 0f;

    public void Draw(in WidgetContext context)
    {
        var state = states.For(context);
        if (frame.First())
        {
            Advance(context.Delta, state.Tab);
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var accent = AppAccents.For(ChirperId);
        var headerBottom = WidgetChrome.Header(context, ink, ChirperId, string.Empty, accent);
        var sample = context.Preview && !session.IsSignedIn;
        DrawTabs(context, ink, state, accent, content.Max.X, content.Min.Y, headerBottom);
        var area = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * 0.5f * context.Scale),
            content.Max);
        var slots = Math.Clamp((int)(area.Height / (RowUnits * context.Scale)), 1,
            context.Size == WidgetSize.Large ? LargeRows : MediumRows);
        if (sample)
        {
            DrawSamples(context, ink, area, slots);
            return;
        }

        if (!session.IsSignedIn)
        {
            WidgetChrome.Message(context, ink, area, ChirperId, Loc.T(L.WidgetsPeople.SignInHint), string.Empty);
            return;
        }

        var tab = state.Tab;
        var count = Math.Min(slots, counts[tab]);
        if (count == 0)
        {
            if (tab != ActivityTab && app is ISocialFeed feed && feed.IsLoading(ScopeOf(feed, tab)))
            {
                WidgetChrome.RedactedRows(context, ink, area, slots, WidgetRowLead.Avatar);
                return;
            }

            WidgetChrome.Message(context, ink, area, ChirperId, Loc.T(EmptyLabel(tab)), string.Empty);
            return;
        }

        var rowHeight = area.Height / slots;
        var block = new Rect(new Vector2(area.Min.X, area.Center.Y - count * rowHeight * 0.5f),
            new Vector2(area.Max.X, area.Center.Y + count * rowHeight * 0.5f));
        for (var index = 0; index < count; index++)
        {
            DrawRow(context, ink, block, index, rowHeight, rows[tab][index], index < count - 1);
        }
    }

    private void DrawTabs(in WidgetContext context, in WidgetInk ink, TabState state, Vector4 accent, float right,
        float top, float bottom)
    {
        var scale = context.Scale;
        var centerY = (top + bottom) * 0.5f;
        var padding = TabHitPadding * scale;
        for (var tab = TabCount - 1; tab >= 0; tab--)
        {
            var label = Loc.T(TabLabels[tab]);
            var width = WidgetChrome.PillWidth(label, WidgetType.Caption, scale);
            var hit = new Rect(new Vector2(right - width - padding * 0.5f, top - padding),
                new Vector2(right + padding * 0.5f, bottom + padding));
            if (WidgetControls.Pressable(context, TabControlBase + tab, hit, out var hovered, out _))
            {
                state.Tab = tab;
            }

            var selected = state.Tab == tab;
            var fill = selected ? ink.Accent(accent) : hovered ? ink.Fill : ink.Fill with { W = 0f };
            right = WidgetChrome.Pill(context.DrawList, right, centerY, label, fill,
                selected ? ink.OnAccent : ink.Secondary, WidgetType.Caption, scale) - TabGap * scale;
        }
    }

    private void Advance(float delta, int shownTab)
    {
        sinceRebuild += delta;
        if (!refresh.Due(RefreshMilliseconds) || app is not ISocialFeed feed)
        {
            return;
        }

        var forced = sinceRebuild >= ForcedRefreshSeconds;
        if (forced)
        {
            sinceRebuild = 0f;
        }

        for (var tab = 0; tab < TabCount; tab++)
        {
            object source = tab == ActivityTab ? activity.Latest : feed.Feed(ScopeOf(feed, tab));
            if (forced || !ReferenceEquals(source, seenSources[tab]))
            {
                seenSources[tab] = source;
                counts[tab] = tab == ActivityTab ? BuildActivity(rows[tab]) : BuildPosts(rows[tab], (PostDto[])source);
            }
        }

        if (shownTab != ActivityTab && counts[shownTab] == 0 && session.IsSignedIn)
        {
            LoadIfIdle(feed, shownTab);
        }
    }

    private void LoadIfIdle(ISocialFeed feed, int tab)
    {
        var scope = ScopeOf(feed, tab);
        var now = Environment.TickCount64;
        if (feed.IsLoading(scope) || now - loadAttempts[tab] < LoadRetryMilliseconds)
        {
            return;
        }

        loadAttempts[tab] = now;
        feed.Load(scope);
    }

    private static SocialFeedScope ScopeOf(ISocialFeed feed, int tab) =>
        tab == ForYouTab ? SocialFeedScope.ForYou : feed.LatestScope;

    private LocString EmptyLabel(int tab) => tab switch
    {
        ActivityTab => L.WidgetsPeople.CaughtUp,
        LatestTab when app is ISocialFeed { LatestScope: SocialFeedScope.Following } => L.Chirper.FollowingEmpty,
        _ => L.Chirper.ExploreEmpty,
    };

    private static int BuildPosts(ChirperRow[] target, PostDto[] posts)
    {
        var count = 0;
        for (var index = 0; index < posts.Length && count < target.Length; index++)
        {
            var post = posts[index].RepostOfId is not null && posts[index].ReferencedPost is { } original
                ? original
                : posts[index];
            if (post.Sensitive || post.SensitiveLocked || string.IsNullOrWhiteSpace(post.Text))
            {
                continue;
            }

            target[count++] = new ChirperRow(SocialIdentity.Name(post.AuthorDisplayName, post.AuthorHandle),
                post.AuthorWorld, post.AuthorAvatarUrl, post.AuthorFrameId, post.Text.ReplaceLineEndings(" "),
                TimeText.Short(post.CreatedAtUnix), false,
                WidgetRoute.To(ChirperId, WidgetRouteKind.SocialPost, post.Id));
        }

        return count;
    }

    private int BuildActivity(ChirperRow[] target)
    {
        var items = activity.Latest;
        var count = 0;
        for (var index = 0; index < items.Length && count < target.Length; index++)
        {
            var item = items[index];
            if (item.App != ChirperId || SocialActivity.IsModerationNotice(item.Type))
            {
                continue;
            }

            var route = SocialActivity.OpensPost(item) && item.PostId is { Length: > 0 } postId
                ? WidgetRoute.To(ChirperId, WidgetRouteKind.SocialPost, postId)
                : item.ActorId.Length > 0
                    ? WidgetRoute.To(ChirperId, WidgetRouteKind.SocialProfile, item.ActorId)
                    : WidgetRoute.App(ChirperId);
            target[count++] = new ChirperRow(SocialActivity.ActorLabel(item), string.Empty, item.ActorAvatarUrl,
                item.ActorFrameId, SocialActivity.Body(item), TimeText.Short(item.CreatedAtUnix), !item.Read, route);
        }

        return count;
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect area, int index, float rowHeight,
        in ChirperRow row, bool separator)
    {
        var scale = context.Scale;
        var rowRect = new Rect(new Vector2(area.Min.X, area.Min.Y + index * rowHeight),
            new Vector2(area.Max.X, area.Min.Y + (index + 1) * rowHeight));
        if (!row.Route.IsEmpty)
        {
            WidgetControls.Link(context, ink, index + 1, rowRect, row.Route);
        }

        var radius = PeopleWidgetChrome.RowAvatarRadius * scale;
        var center = new Vector2(area.Min.X + radius, rowRect.Center.Y);
        AvatarView.DrawRemote(context.DrawList, center, radius, context.Theme, row.Name, row.World, row.AvatarUrl,
            images, lodestone, 0.95f, 32, ink.Opacity, Frames.Of(row.FrameId));
        if (row.Unread)
        {
            PeopleWidgetChrome.Dot(context, ink, center + new Vector2(radius * 0.75f, -radius * 0.75f), false);
        }

        var textLeft = center.X + radius + PeopleWidgetChrome.RowTextGap * scale;
        PeopleWidgetChrome.Row(context, ink, rowRect, textLeft, row.Name, row.Subtitle, row.Time, string.Empty, false,
            row.Unread);
        if (separator)
        {
            WidgetChrome.Separator(context, ink, textLeft, area.Max.X, rowRect.Max.Y);
        }
    }

    private void DrawSamples(in WidgetContext context, in WidgetInk ink, Rect area, int slots)
    {
        var count = Math.Min(slots, SampleTexts.Length);
        var rowHeight = area.Height / Math.Max(1, count);
        for (var index = 0; index < count; index++)
        {
            var row = new ChirperRow(WidgetSamples.Names[index + 1], string.Empty, null, string.Empty,
                Loc.T(SampleTexts[index]), string.Empty, false, default);
            DrawRow(context, ink, area, index, rowHeight, row, index < count - 1);
        }
    }

    public void Dispose()
    {
    }
}
