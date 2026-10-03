using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Venues.Widgets;

internal sealed class VenuesWidget : IHomeWidget
{
    private const string AppKey = "venues";
    private const int RefreshMilliseconds = 15000;
    private const int FetchMilliseconds = 30000;
    private const int MaxRows = 6;
    private const float ThumbUnits = 44f;
    private const float RowUnits = 54f;
    private const int SampleCount = 4;
    private static readonly Vector4 LiveColor = AccentRing.Green;

    private readonly VenuesService venues;
    private readonly Configuration configuration;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly ArtworkCache artwork;
    private readonly List<VenueEvent> live = new();
    private readonly Dictionary<string, CachedText> subtitles = new(StringComparer.Ordinal);
    private readonly LiveComparer liveOrder = new();
    private WidgetRefresh refresh;
    private WidgetRefresh fetch;
    private int seenVersion = -1;
    private VenueEvent? nextOpening;
    private CachedText countText;
    private CachedText nextText;

    public VenuesWidget(VenuesService venues, Configuration configuration, GameData gameData,
        RemoteImageCache images, ArtworkCache artwork)
    {
        this.venues = venues;
        this.configuration = configuration;
        this.gameData = gameData;
        this.images = images;
        this.artwork = artwork;
    }

    public string Id => "venues.live";
    public string DisplayName => Loc.T(L.Apps.Venues);
    public string Description => Loc.T(L.WidgetsUtility.VenuesDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => live.Count > 0 ? 0.35f : 0f;

    public void Draw(in WidgetContext context)
    {
        if (!context.Preview && context.Opacity > 0f)
        {
            Fetch();
        }

        Refresh();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accent = AppAccents.For(AppKey);
        var sample = context.Preview && live.Count == 0;
        var total = sample ? SampleCount : live.Count;
        var trailing = total > 0 ? WidgetText.Integer(ref countText, total) : string.Empty;
        var top = WidgetChrome.Header(context, ink, AppKey, Loc.T(L.Venues.LiveNowLabel), accent, trailing,
            ink.Secondary);
        var body = new Rect(new Vector2(content.Min.X, top + WidgetMetrics.RowGap * scale), content.Max);
        if (total == 0)
        {
            if (venues.State is VenueState.Idle or VenueState.Loading && venues.Events.Count == 0)
            {
                WidgetChrome.RedactedRows(context, ink, body, Capacity(body, scale), WidgetRowLead.Thumbnail);
                return;
            }

            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.GlassCheers, accent,
                Loc.T(L.WidgetsUtility.NoLiveVenues), NextOpening());
            return;
        }

        var rows = Math.Min(Capacity(body, scale), total);
        var rowHeight = body.Height / Math.Max(1, Capacity(body, scale));
        var nowUtc = DateTime.UtcNow;
        for (var index = 0; index < rows; index++)
        {
            var rowRect = new Rect(new Vector2(body.Min.X, body.Min.Y + index * rowHeight),
                new Vector2(body.Max.X, body.Min.Y + (index + 1) * rowHeight));
            if (sample)
            {
                DrawSample(context, ink, index, rowRect);
            }
            else
            {
                DrawVenue(context, ink, index, rowRect, live[index], nowUtc);
            }

            if (index < rows - 1)
            {
                var left = rowRect.Min.X + ThumbUnits * scale + WidgetMetrics.Gutter * scale;
                WidgetChrome.Separator(context, ink, left, rowRect.Max.X, rowRect.Max.Y);
            }
        }
    }

    private void DrawVenue(in WidgetContext context, in WidgetInk ink, int index, Rect rowRect, VenueEvent venue,
        DateTime nowUtc)
    {
        WidgetControls.Link(context, ink, index, rowRect, WidgetRoute.To(AppKey, WidgetRouteKind.Venue, venue.Id));
        var drawList = context.DrawList;
        var scale = context.Scale;
        var thumb = Thumb(rowRect, scale);
        var radius = WidgetMetrics.InnerRadius(context);
        var texture = images.Sized(venue.BannerUrl, thumb.Width) ?? images.Sized(venue.LogoUrl, thumb.Width);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, thumb.Width, thumb.Height);
            Squircle.FillImage(drawList, thumb.Min, thumb.Max, radius, texture.Handle,
                ImGui.GetColorU32(ink.ImageTint), uv0, uv1);
        }
        else
        {
            Squircle.FillImage(drawList, thumb.Min, thumb.Max, radius, artwork.HandleForName(venue.Title),
                ImGui.GetColorU32(ink.ImageTint));
        }

        var confirmed = venue.IsConfirmedLive(nowUtc);
        var pill = WidgetText.Upper(confirmed ? L.Venues.LiveNow : L.Venues.OpenNow);
        ref var cache = ref WidgetCaches.Slot(subtitles, venue.Id);
        DrawText(context, ink, rowRect, thumb, venue.Title, Subtitle(ref cache, venue), pill, confirmed);
    }

    private void DrawSample(in WidgetContext context, in WidgetInk ink, int index, Rect rowRect)
    {
        var drawList = context.DrawList;
        var thumb = Thumb(rowRect, context.Scale);
        var radius = WidgetMetrics.InnerRadius(context);
        var name = WidgetSamples.Venues[index % WidgetSamples.Venues.Length];
        Squircle.FillImage(drawList, thumb.Min, thumb.Max, radius, artwork.HandleForName(name),
            ImGui.GetColorU32(ink.ImageTint));
        var world = WidgetSamples.Worlds[index % WidgetSamples.Worlds.Length];
        var confirmed = index < 2;
        var pill = WidgetText.Upper(confirmed ? L.Venues.LiveNow : L.Venues.OpenNow);
        DrawText(context, ink, rowRect, thumb, name, world, pill, confirmed);
    }

    private static void DrawText(in WidgetContext context, in WidgetInk ink, Rect rowRect, Rect thumb, string title,
        string subtitle, string pill, bool confirmed)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var pillWidth = WidgetChrome.PillWidth(pill, WidgetType.Eyebrow, scale);
        var pillFill = confirmed ? ink.Accent(LiveColor) : ink.Fill;
        var pillText = confirmed ? ink.OnAccent : ink.Secondary;
        WidgetChrome.Pill(drawList, rowRect.Max.X, rowRect.Center.Y, pill, pillFill, pillText, WidgetType.Eyebrow,
            scale);
        var left = thumb.Max.X + WidgetMetrics.Gutter * scale;
        var width = rowRect.Max.X - pillWidth - WidgetMetrics.Gutter * scale - left;
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        var subtitleHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var top = rowRect.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(left, top), title, ink.Primary, WidgetType.Headline, width);
        WidgetText.Draw(drawList, new Vector2(left, top + titleHeight), subtitle, ink.Secondary,
            WidgetType.Caption, width);
    }

    private static Rect Thumb(Rect rowRect, float scale)
    {
        var side = MathF.Min(ThumbUnits * scale, rowRect.Height - 6f * scale);
        var min = new Vector2(rowRect.Min.X, rowRect.Center.Y - side * 0.5f);
        return new Rect(min, min + new Vector2(side, side));
    }

    private static int Capacity(Rect body, float scale) =>
        Math.Clamp((int)(body.Height / (RowUnits * scale)), 1, MaxRows);

    private void Fetch()
    {
        if (!fetch.Due(FetchMilliseconds))
        {
            return;
        }

        venues.EnsureFresh(false);
    }

    private void Refresh()
    {
        if (!refresh.Due(RefreshMilliseconds) && seenVersion == venues.Version)
        {
            return;
        }

        seenVersion = venues.Version;
        live.Clear();
        nextOpening = null;
        var nowUtc = DateTime.UtcNow;
        var today = nowUtc.ToLocalTime().Date;
        var dataCenter = gameData.DataCenterName(gameData.LocalCurrentWorldId);
        var hideAdult = configuration.VenueHideAdult;
        var events = venues.Events;
        for (var index = 0; index < events.Count; index++)
        {
            var venue = events[index];
            if (hideAdult && VenueFilter.IsAdult(venue))
            {
                continue;
            }

            if (dataCenter.Length > 0 && !string.Equals(venue.DataCenter, dataCenter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (venue.IsLive(nowUtc))
            {
                live.Add(venue);
                continue;
            }

            if (venue.StartUtc is { } start && start > nowUtc && start.ToLocalTime().Date == today &&
                (nextOpening is null || start < nextOpening.StartUtc))
            {
                nextOpening = venue;
            }
        }

        liveOrder.Now = nowUtc;
        liveOrder.Favorites = configuration.VenueFavorites;
        live.Sort(liveOrder);
        liveOrder.Favorites = null;
        if (live.Count > MaxRows * 4)
        {
            live.RemoveRange(MaxRows * 4, live.Count - MaxRows * 4);
        }
    }

    private string NextOpening()
    {
        if (nextOpening?.StartUtc is not { } start)
        {
            return string.Empty;
        }

        var key = start.Ticks ^ nextOpening.Title.GetHashCode();
        if (nextText.IsCurrent(key))
        {
            return nextText.Value;
        }

        return nextText.Store(key,
            Loc.T(L.WidgetsUtility.NextOpening, nextOpening.Title, TimeText.Clock(start.ToLocalTime())));
    }

    private string Subtitle(ref CachedText cache, VenueEvent venue)
    {
        var key = (long)seenVersion;
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        var text = venue.PlaceLine.Length == 0 ? venue.World : string.Concat(venue.World, " · ", venue.PlaceLine);
        return cache.Store(key, text);
    }

    public void Dispose()
    {
    }

    private sealed class LiveComparer : IComparer<VenueEvent>
    {
        public DateTime Now;
        public IReadOnlyList<string>? Favorites;

        public int Compare(VenueEvent? left, VenueEvent? right)
        {
            if (left is null || right is null)
            {
                return left is null ? right is null ? 0 : 1 : -1;
            }

            if (Favorites is not null)
            {
                var leftFavorite = VenueFilter.Contains(Favorites, left.Id);
                var rightFavorite = VenueFilter.Contains(Favorites, right.Id);
                if (leftFavorite != rightFavorite)
                {
                    return leftFavorite ? -1 : 1;
                }
            }

            var byState = VenueFilter.CompareLiveState(left, right, Now);
            if (byState != 0)
            {
                return byState;
            }

            var byViewers = right.LiveViewers.CompareTo(left.LiveViewers);
            return byViewers != 0
                ? byViewers
                : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
        }
    }
}
