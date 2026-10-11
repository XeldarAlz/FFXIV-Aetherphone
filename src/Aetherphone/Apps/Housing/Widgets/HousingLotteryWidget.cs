using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing.Widgets;

internal sealed class HousingLotteryWidget : IHomeWidget
{
    private const int RefreshMilliseconds = 2000;
    private const int WatchedRows = 3;
    private const float LeftColumnFraction = 0.42f;
    private const float DotUnits = 7f;
    private const float ResultsRelevance = 0.8f;
    private const float ClosingRelevance = 0.6f;
    private const float ClosingHours = 24f;
    private const string Placeholder = "--";

    private static readonly Vector4 HousingAccent = AppAccents.For("housing");

    private readonly HousingService housing;
    private readonly CachedText[] plotTexts = new CachedText[WatchedRows];
    private readonly CachedText[] placeTexts = new CachedText[WatchedRows];
    private readonly CachedText[] entryTexts = new CachedText[WatchedRows];
    private WidgetRefresh refresh;
    private bool started;
    private bool known;
    private HousingLotteryPhase phase;
    private DateTime? phaseEndsUtc;
    private int openPlots = -1;
    private string worldName = string.Empty;
    private uint districtId;
    private CachedText countdownText;
    private CachedText openText;
    private CachedText locationText;
    private string locationWorld = string.Empty;

    public HousingLotteryWidget(HousingService housing)
    {
        this.housing = housing;
    }

    public string Id => "housing.lottery";
    public string DisplayName => Loc.T(L.Apps.Housing);
    public string Description => Loc.T(L.WidgetsAdventure.HousingDescription);
    public string AppId => "housing";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public float Relevance(string config)
    {
        Sync();
        if (!known)
        {
            return 0f;
        }

        if (phase == HousingLotteryPhase.Results)
        {
            return ResultsRelevance;
        }

        if (phase != HousingLotteryPhase.Entry || phaseEndsUtc is not { } ends)
        {
            return 0f;
        }

        return (ends - DateTime.UtcNow).TotalHours <= ClosingHours ? ClosingRelevance : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        if (!context.Preview)
        {
            Sync();
        }
        else if (refresh.Due(RefreshMilliseconds))
        {
            Refresh();
        }

        var sample = !known && context.Preview;
        if (!known && !sample)
        {
            var top = WidgetChrome.Header(context, ink, AppId, L.Apps.Housing, HousingAccent);
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, top), FontAwesomeIcon.Home, default, Loc.T(L.WidgetsAdventure.NoLottery), string.Empty);
            return;
        }

        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawSummary(context, ink, sample, content);
            return;
        }

        var gutter = WidgetMetrics.Gutter * context.Scale;
        var left = new Rect(content.Min, new Vector2(content.Min.X + content.Width * LeftColumnFraction, content.Max.Y));
        DrawSummary(context, ink, sample, left);
        var right = new Rect(new Vector2(left.Max.X + gutter * 2f, content.Min.Y), content.Max);
        DrawWatched(context, ink, sample, right);
    }

    private void DrawSummary(in WidgetContext context, in WidgetInk ink, bool sample, Rect column)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var shownPhase = sample ? HousingLotteryPhase.Entry : phase;
        var tint = HousingMarkers.PhaseColor(shownPhase, HousingAccent);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, HousingFormat.PhaseLabel(shownPhase), tint);
        var remaining = sample
            ? TimeSpan.FromHours(WidgetSamples.HousingPhaseHours)
            : HousingFormat.Remaining(phaseEndsUtc, DateTime.UtcNow);
        var hero = remaining is { } span ? WidgetText.Countdown(ref countdownText, span) : Placeholder;
        var heroStyle = WidgetType.DisplayCompact;
        var heroWidth = WidgetText.TabularWidth(hero, heroStyle);
        var fitted = heroWidth > column.Width && heroWidth > 0f
            ? new TextStyle(heroStyle.Scale * column.Width / heroWidth, heroStyle.Weight)
            : heroStyle;
        WidgetText.Tabular(drawList, new Vector2(column.Min.X, headerBottom + WidgetMetrics.Gutter * scale * 0.5f), hero,
            ink.Primary, fitted);

        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var bodyHeight = WidgetText.LineHeight(WidgetType.Body);
        var locationTop = column.Max.Y - captionHeight;
        WidgetText.Draw(drawList, new Vector2(column.Min.X, locationTop), Location(sample), ink.Secondary,
            WidgetType.Caption, column.Width);
        var plots = sample ? WidgetSamples.HousingOpenPlots : openPlots;
        if (plots < 0)
        {
            return;
        }

        var openLabel = openText.IsCurrent(plots)
            ? openText.Value
            : openText.Store(plots, Loc.T(L.WidgetsAdventure.PlotsOpen, plots));
        WidgetText.Draw(drawList, new Vector2(column.Min.X, locationTop - WidgetMetrics.RowGap * scale - bodyHeight),
            openLabel, ink.Primary, WidgetType.Body, column.Width);
    }

    private void DrawWatched(in WidgetContext context, in WidgetInk ink, bool sample, Rect column)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var glyph = WidgetMetrics.GlyphSmall * scale;
        var headerHeight = MathF.Max(glyph, eyebrowHeight);
        WidgetText.EyebrowFit(drawList, new Vector2(column.Min.X, column.Min.Y + (headerHeight - eyebrowHeight) * 0.5f),
            Loc.T(L.WidgetsAdventure.Watching), column.Width, ink.Secondary, scale);
        var body = new Rect(new Vector2(column.Min.X, column.Min.Y + headerHeight + WidgetMetrics.Gutter * scale * 0.5f),
            column.Max);
        var watched = housing.Watch.Watched;
        var count = sample ? WidgetSamples.HousingPlots.Length : Math.Min(watched.Count, WatchedRows);
        if (count == 0)
        {
            WidgetText.Wrapped(drawList, body.Min, Loc.T(L.WidgetsAdventure.NoWatched), ink.Tertiary,
                WidgetType.Caption, body.Width, 4);
            return;
        }

        var rowHeight = body.Height / WatchedRows;
        for (var index = 0; index < count; index++)
        {
            var top = body.Min.Y + index * rowHeight;
            var row = new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + rowHeight));
            if (sample)
            {
                DrawWatchedRow(context, ink, row, index, HousingDistricts.MistId, WidgetSamples.HousingWards[index],
                    WidgetSamples.HousingPlots[index], HousingLotteryPhase.Entry, index * 3 + 2);
                continue;
            }

            var record = watched[index];
            DrawWatchedRow(context, ink, row, index, record.DistrictId, record.Ward, record.Plot,
                record.StillReported ? (HousingLotteryPhase)record.Phase : HousingLotteryPhase.Unavailable,
                record.Entries ?? -1);
        }
    }

    private void DrawWatchedRow(in WidgetContext context, in WidgetInk ink, Rect row, int index, uint district,
        int ward, int plot, HousingLotteryPhase rowPhase, int entries)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var gap = WidgetMetrics.RowGap * scale;
        var blockTop = row.Center.Y - (headlineHeight + gap + captionHeight) * 0.5f;
        var dot = DotUnits * scale;
        drawList.AddCircleFilled(new Vector2(row.Min.X + dot * 0.5f, blockTop + headlineHeight * 0.5f), dot * 0.5f,
            ImGui.GetColorU32(ink.Accent(HousingMarkers.PhaseColor(rowPhase, HousingAccent))), 16);
        var textLeft = row.Min.X + dot + gutter;
        var entriesText = string.Empty;
        if (entries >= 0)
        {
            entriesText = entryTexts[index].IsCurrent(entries)
                ? entryTexts[index].Value
                : entryTexts[index].Store(entries, HousingFormat.Entries(entries));
        }

        var entriesWidth = entriesText.Length > 0
            ? WidgetText.TabularRight(drawList, row.Max.X, blockTop, entriesText, ink.Secondary,
                WidgetType.Body)
            : 0f;
        var key = district * 100000L + ward * 100L + plot;
        var plotLabel = plotTexts[index].IsCurrent(key)
            ? plotTexts[index].Value
            : plotTexts[index].Store(key, HousingFormat.PlotLabel(plot));
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop), plotLabel, ink.Primary, WidgetType.Headline,
            MathF.Max(1f, row.Max.X - entriesWidth - gutter - textLeft));
        var place = placeTexts[index].IsCurrent(key)
            ? placeTexts[index].Value
            : placeTexts[index].Store(key, HousingFormat.Place(HousingDistricts.ShortDisplayName(district), ward));
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop + headlineHeight + gap), place, ink.Secondary,
            WidgetType.Caption, MathF.Max(1f, row.Max.X - textLeft));
    }

    private void Sync()
    {
        if (!started && (AdventureWidgetArt.IsLoggedIn || housing.HasWorldSelected))
        {
            started = true;
            housing.EnsureStarted();
        }

        if (refresh.Due(RefreshMilliseconds))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var utcNow = DateTime.UtcNow;
        known = false;
        phase = HousingLotteryPhase.Unknown;
        phaseEndsUtc = null;
        openPlots = -1;
        var world = housing.WorldId;
        var lottery = HousingLotteryState.Unknown;
        var districts = HousingDistricts.All;
        for (var index = 0; index < districts.Count; index++)
        {
            if (housing.Lookup(world, districts[index].Id) is not { } snapshot)
            {
                continue;
            }

            openPlots = Math.Max(openPlots, 0) + snapshot.OpenPlotCount;
            lottery = HousingLottery.Prefer(lottery, HousingLottery.Resolve(snapshot.Plots, utcNow), utcNow);
            known = true;
        }

        if (known)
        {
            worldName = housing.WorldName;
            districtId = 0;
            phase = lottery.Phase;
            phaseEndsUtc = lottery.EndsUtc;
        }

        if (phaseEndsUtc is null)
        {
            ReadWatched();
        }

        if (phaseEndsUtc is not null && HousingFormat.HasExpired(phaseEndsUtc, utcNow))
        {
            phase = HousingLotteryPhase.Expired;
            housing.RefreshAfterExpiry();
        }
    }

    private void ReadWatched()
    {
        var watched = housing.Watch.Watched;
        for (var index = 0; index < watched.Count; index++)
        {
            var record = watched[index];
            if (record.PhaseEndUnix <= 0 || record.Phase == (byte)HousingLotteryPhase.Unknown)
            {
                continue;
            }

            var ends = DateTimeOffset.FromUnixTimeSeconds(record.PhaseEndUnix).UtcDateTime;
            if (phaseEndsUtc is null || ends < phaseEndsUtc)
            {
                phaseEndsUtc = ends;
                phase = (HousingLotteryPhase)record.Phase;
                worldName = record.WorldName;
                districtId = record.DistrictId;
            }

            known = true;
        }
    }

    private string Location(bool sample)
    {
        var world = sample ? WidgetSamples.Worlds[0] : worldName;
        var district = sample ? HousingDistricts.MistId : districtId;
        if (locationText.IsCurrent(district) && ReferenceEquals(locationWorld, world))
        {
            return locationText.Value;
        }

        locationWorld = world;
        var districtName = district == 0 ? string.Empty : HousingDistricts.ShortDisplayName(district);
        return locationText.Store(district, world.Length == 0
            ? districtName
            : districtName.Length == 0
                ? world
                : string.Concat(world, " · ", districtName));
    }

    public void Dispose()
    {
    }
}
