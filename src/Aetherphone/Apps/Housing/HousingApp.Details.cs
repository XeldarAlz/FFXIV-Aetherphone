using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal readonly record struct HousingFact(string Label, string Value);

internal sealed partial class HousingApp
{
    private const float SnippetHeight = 168f;
    private const float SnippetZoom = 3.2f;
    private const float FactRowHeight = 46f;
    private const float DetailPad = 16f;
    private const float DetailButtonHeight = 44f;
    private const float DetailButtonGap = 10f;

    private readonly List<HousingFact> plotFacts = new();
    private readonly List<HousingFact> dataFacts = new();
    private HousingPlotKey factsKey;
    private int factsRevision = -1;
    private int factsWatchRevision = -1;
    private object? factsCulture;
    private CachedText detailCountdown;
    private CachedText detailEnds;
    private CachedText detailTitle;
    private CachedText snippetPlace;

    private void DrawDetailsRoute(in PhoneContext context, HousingView view)
    {
        var key = view.Plot;
        var plot = FindPlot(key);
        var record = housing.Watch.Find(key);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        if (plot is null && record is null)
        {
            HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Home,
                Loc.T(L.Housing.PlotGoneTitle), Loc.T(L.Housing.NoScansHint), string.Empty, scale);
        }
        else
        {
            SyncFacts(key, plot, record);
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawSnippet(drawList, origin, width, key, plot, record, scale);
                cursorY = DrawDetailLottery(drawList, new Vector2(origin.X, cursorY), width, plot, record, scale);
                cursorY = DrawDetailActions(new Vector2(origin.X, cursorY), width, key, plot, scale);
                cursorY = DrawFactsCard(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Housing.SectionPlot), plotFacts, scale);
                cursorY = DrawFactsCard(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Housing.SettingsData), dataFacts, scale);
                cursorY += Metrics.Space.Md * scale;
                cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), Loc.T(L.Housing.EntriesCaveat),
                    ui.MutedInk, TextStyles.Footnote, width);
                ReserveTo(origin, width, cursorY + BottomPad * scale);
            }
        }

        var title = detailTitle.IsCurrent(key.Plot) ? detailTitle.Value : detailTitle.Store(key.Plot,
            HousingFormat.PlotLabel(key.Plot));
        AppHeader.EndLargeTitle(in navBar, context, "housing.nav.details", title, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void SyncFacts(HousingPlotKey key, HousingPlot? plot, HousingWatchRecord? record)
    {
        if (factsKey == key && factsRevision == housing.Revision && factsWatchRevision == housing.Watch.Revision &&
            ReferenceEquals(factsCulture, Loc.Culture))
        {
            return;
        }

        factsKey = key;
        factsRevision = housing.Revision;
        factsWatchRevision = housing.Watch.Revision;
        factsCulture = Loc.Culture;
        plotFacts.Clear();
        dataFacts.Clear();
        var now = DateTime.UtcNow;
        var size = plot?.Size ?? (HousingPlotSize)(record?.Size ?? 0);
        var entries = plot?.Entries ?? record?.Entries;
        plotFacts.Add(new HousingFact(Loc.T(L.Housing.SizeLabel), HousingFormat.SizeLabel(size)));
        plotFacts.Add(new HousingFact(Loc.T(L.Housing.EntriesLabel), HousingFormat.Entries(entries)));
        if (HousingFormat.Odds(entries) is { } odds)
        {
            plotFacts.Add(new HousingFact(Loc.T(L.Housing.OddsLabel), odds));
        }

        plotFacts.Add(new HousingFact(Loc.T(L.Housing.PriceLabel),
            HousingFormat.Price(plot?.Price ?? record?.Price ?? 0L)));
        plotFacts.Add(new HousingFact(Loc.T(L.Housing.EligibilityLabel),
            HousingFormat.EligibilityLabel(plot?.Eligibility ??
                                           (HousingPurchaseEligibility)(record?.Eligibility ?? 0))));
        plotFacts.Add(new HousingFact(Loc.T(L.Housing.PurchaseLabel),
            HousingFormat.ModeLabel(plot?.Mode ?? HousingPurchaseMode.Unknown)));
        plotFacts.Add(new HousingFact(Loc.T(L.Housing.DivisionLabel),
            HousingFormat.DivisionLabel(HousingDistricts.IsSubdivision(key.Plot))));

        var lastSeen = plot?.LastSeenUtc ?? FromUnix(record?.LastSeenUnix ?? 0L);
        var firstSeen = plot?.FirstSeenUtc ?? FromUnix(record?.FirstSeenUnix ?? 0L);
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.WorldLabel), housing.WorldNameOf(key.WorldId)));
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.DataCenterLabel), Blank(housing.DataCenterName(key.WorldId))));
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.RegionLabel), Blank(housing.RegionName(key.WorldId))));
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.ScannedLabel), HousingFormat.ScanAge(lastSeen, now)));
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.FirstReported),
            firstSeen == default ? Loc.T(L.Housing.NotReported) : HousingFormat.ExactLocalTime(firstSeen)));
        dataFacts.Add(new HousingFact(Loc.T(L.Housing.ProviderLabel), housing.ProviderName));
    }

    private static string Blank(string value) => value.Length > 0 ? value : Loc.T(L.Housing.NotReported);

    private float DrawSnippet(ImDrawListPtr drawList, Vector2 origin, float width, HousingPlotKey key,
        HousingPlot? plot, HousingWatchRecord? record, float scale)
    {
        if (housing.GameMaps.For(key.DistrictId) is not { } district)
        {
            return origin.Y;
        }

        var map = district.For(HousingDistricts.IsSubdivision(key.Plot));
        if (!map.TryGetPoint(key.Plot, out var point) || housing.GameMaps.Texture(map) is not { } texture)
        {
            return origin.Y;
        }

        var height = SnippetHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        var aspect = height / MathF.Max(1f, width);
        var spanX = 1f / SnippetZoom;
        var spanY = spanX * aspect;
        var uvCenter = new Vector2(Math.Clamp(point.X, spanX * 0.5f, 1f - spanX * 0.5f),
            Math.Clamp(point.Y, spanY * 0.5f, 1f - spanY * 0.5f));
        var uvMin = uvCenter - new Vector2(spanX, spanY) * 0.5f;
        var uvMax = uvCenter + new Vector2(spanX, spanY) * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.Palette.BackdropBottom));
        drawList.AddImageRounded(texture.Handle, min, max, uvMin, uvMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)),
            radius, ImDrawFlags.RoundCornersAll);
        var markerAt = new Vector2(min.X + (point.X - uvMin.X) / spanX * width,
            min.Y + (point.Y - uvMin.Y) / spanY * height);
        var size = plot?.Size ?? (HousingPlotSize)(record?.Size ?? 0);
        var phase = plot?.Phase ?? (HousingLotteryPhase)(record?.Phase ?? 0);
        var style = new HousingMarkerStyle(size, phase, housing.Watch.IsWatched(key), true, false, false);
        HousingMarkers.Draw(drawList, markerAt, style, ui.Accent, scale * 1.2f, Pulse.Wave(Pulse.Calm));
        var placeKey = ((long)key.DistrictId << 8) | (uint)key.Ward;
        var placeText = snippetPlace.IsCurrent(placeKey)
            ? snippetPlace.Value
            : snippetPlace.Store(placeKey, HousingFormat.Place(HousingDistricts.DisplayName(key.DistrictId), key.Ward));
        var labelSize = Typography.Measure(placeText, TextStyles.Footnote);
        var chipPad = Metrics.Space.Md * scale;
        var chipHeight = labelSize.Y + Metrics.Space.Sm * scale;
        var chipMin = new Vector2(min.X + Metrics.Space.Md * scale, max.Y - Metrics.Space.Md * scale - chipHeight);
        var chipMax = new Vector2(MathF.Min(max.X - Metrics.Space.Md * scale, chipMin.X + labelSize.X + chipPad * 2f),
            chipMin.Y + chipHeight);
        Material.ThemedGlass(drawList, chipMin, chipMax, chipHeight * 0.5f, scale, ui.Palette.BackdropTop);
        Typography.Draw(drawList, new Vector2(chipMin.X + chipPad, chipMin.Y + (chipHeight - labelSize.Y) * 0.5f),
            Typography.FitText(placeText, chipMax.X - chipMin.X - chipPad * 2f, TextStyles.Footnote), ui.TitleInk,
            TextStyles.Footnote);
        return max.Y + HousingArt.SectionGap * scale * 0.5f;
    }

    private float DrawDetailLottery(ImDrawListPtr drawList, Vector2 origin, float width, HousingPlot? plot,
        HousingWatchRecord? record, float scale)
    {
        var now = DateTime.UtcNow;
        var phase = plot?.Phase ?? (HousingLotteryPhase)(record?.Phase ?? 0);
        var ends = plot?.PhaseEndsUtc ?? OptionalUnix(record?.PhaseEndUnix ?? 0L);
        var stillReported = plot is not null;
        var pad = DetailPad * scale;
        var height = pad * 2f + Typography.LineHeight(TextStyles.SubheadlineEmphasized) +
                     Typography.LineHeight(TextStyles.Title1) + HousingArt.LineGap * 4f * scale +
                     HousingArt.BarHeight * scale + Typography.LineHeight(TextStyles.Footnote);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        HousingArt.Card(drawList, ui, min, max, scale);
        var left = min.X + pad;
        var right = max.X - pad;
        var hue = stillReported ? HousingMarkers.PhaseColor(phase, ui.Accent) : AppPalettes.HousingResults;
        var y = min.Y + pad;
        var label = stillReported ? HousingFormat.PhaseLabel(phase) : Loc.T(L.Housing.LastKnown);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(label, right - left,
            TextStyles.SubheadlineEmphasized), hue, TextStyles.SubheadlineEmphasized);
        y += Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var countdown = HousingText.Countdown(ref detailCountdown, ends, now);
        var fitted = WidgetText.FitStyle(countdown, TextStyles.Title1, right - left, true);
        WidgetText.Tabular(drawList, new Vector2(left, y), countdown, ui.TitleInk, fitted);
        y += Typography.LineHeight(TextStyles.Title1) + HousingArt.LineGap * 2f * scale;
        HousingArt.Bar(drawList, new Vector2(left, y), new Vector2(right, y + HousingArt.BarHeight * scale),
            MathF.Max(0f, HousingLottery.Progress(phase, ends, now)), hue);
        y += HousingArt.BarHeight * scale + HousingArt.LineGap * 2f * scale;
        var endsText = ends is null
            ? Loc.T(L.Housing.NotReported)
            : HousingText.Moment(ref detailEnds, ends);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(endsText, right - left, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        return max.Y;
    }

    private float DrawDetailActions(Vector2 origin, float width, HousingPlotKey key, HousingPlot? plot, float scale)
    {
        var height = DetailButtonHeight * scale;
        var gap = DetailButtonGap * scale;
        var top = origin.Y + Metrics.Space.Md * scale;
        var travel = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
        if (HousingChrome.PillButton(travel, Loc.T(L.Housing.TravelHere), true, ui))
        {
            TravelTo(key);
        }

        top = travel.Max.Y + gap;
        var half = (width - gap) * 0.5f;
        var watched = housing.Watch.IsWatched(key);
        var watchRect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + half, top + height));
        var remindRect = new Rect(new Vector2(watchRect.Max.X + gap, top), new Vector2(origin.X + width, top + height));
        if (HousingChrome.PillButton(watchRect, Loc.T(watched ? L.Housing.Unwatch : L.Housing.Watch), false, ui))
        {
            if (plot is not null)
            {
                ToggleWatch(plot);
            }
            else
            {
                housing.Watch.CancelReminder(key);
                housing.Watch.Unwatch(key);
                UiFeedback.Play(UiSound.ToggleOff);
                InvalidateCache();
                router.Pop();
            }
        }

        var reminder = housing.Watch.FindReminder(key);
        var remindLabel = reminder is { Notified: false } ? Loc.T(L.Housing.ReminderSet) : Loc.T(L.Housing.RemindMe);
        if (HousingChrome.PillButton(remindRect, remindLabel, false, ui, false, plot?.PhaseEndsUtc is not null))
        {
            OpenReminderSheet(key);
        }

        return remindRect.Max.Y;
    }

    private float DrawFactsCard(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        List<HousingFact> facts, float scale)
    {
        var top = origin.Y + HousingArt.SectionGap * scale;
        var cursorY = top + HousingArt.SectionHeader(drawList, new Vector2(origin.X, top), width, title,
            string.Empty, ui, out _, scale);
        cursorY += HousingArt.HeaderGap * scale;
        var rowHeight = FactRowHeight * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + rowHeight * facts.Count);
        HousingArt.Card(drawList, ui, min, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Body);
        for (var index = 0; index < facts.Count; index++)
        {
            var rowTop = min.Y + rowHeight * index;
            if (index > 0)
            {
                HousingArt.Hairline(drawList, ui, min.X + pad, max.X - pad, rowTop);
            }

            var centerY = rowTop + rowHeight * 0.5f;
            var fact = facts[index];
            var labelWidth = MathF.Min((width - pad * 2f) * 0.5f, Typography.Measure(fact.Label, TextStyles.Body).X);
            Typography.Draw(drawList, new Vector2(min.X + pad, centerY - lineHeight * 0.5f),
                Typography.FitText(fact.Label, labelWidth, TextStyles.Body), ui.MutedInk, TextStyles.Body);
            var valueMax = MathF.Max(1f, width - pad * 2f - labelWidth - HousingArt.TextGap * scale);
            var value = Typography.FitText(fact.Value, valueMax, TextStyles.Body);
            var valueWidth = Typography.Measure(value, TextStyles.Body).X;
            Typography.Draw(drawList, new Vector2(max.X - pad - valueWidth, centerY - lineHeight * 0.5f), value,
                ui.TitleInk, TextStyles.Body);
        }

        return max.Y;
    }
}
