using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const int WatchPreviewRows = 3;
    private const int DistrictCount = 5;
    private const float HeroAspect = 0.52f;
    private const float HeroPad = 20f;
    private const float HeroIconSize = 30f;
    private const float HeroIconGap = 10f;
    private const float HeroLineGap = 6f;
    private const float HeroBarHeight = 6f;
    private const float HeroSubAlpha = 0.82f;
    private const float HeroWashAlpha = 0.22f;
    private const float HeroTrackAlpha = 0.28f;
    private const float SwatchGap = 4f;
    private const float SwatchSpacing = 12f;
    private const float FooterDot = 3.5f;

    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 1f);

    private readonly NavBarButton[] overviewButtons = new NavBarButton[2];
    private readonly CachedText[] districtTexts = new CachedText[DistrictCount];
    private readonly CachedText[] watchCountdowns = new CachedText[WatchPreviewRows];
    private readonly CachedText[] watchPlaces = new CachedText[WatchPreviewRows];
    private readonly CachedText[] watchTitles = new CachedText[WatchPreviewRows];
    private CachedText heroCountdown;
    private CachedText heroEnds;
    private CachedText footerText;
    private CachedText heroOpen;
    private CachedText cachedBanner;
    private int worldStateRevision = -1;
    private uint worldStateWorld;
    private HousingLotteryState worldLottery = HousingLotteryState.Unknown;
    private readonly HousingDistrictStats[] districtStats = new HousingDistrictStats[DistrictCount];
    private readonly bool[] districtLoaded = new bool[DistrictCount];
    private int worldOpen;
    private DateTime worldFetchedUtc;
    private Spring heroFill;

    private void DrawOverviewTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var scale = UiScale.Current;
        if (!housing.HasWorldSelected)
        {
            if (HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, navBar.Body, FontAwesomeIcon.Globe,
                    Loc.T(L.Housing.NoWorldTitle), Loc.T(L.Housing.NoWorldHint), Loc.T(L.Housing.ChooseWorld),
                    scale))
            {
                OpenWorldPicker();
            }
        }
        else
        {
            SyncWorldState();
            if (!AnyDistrictLoaded())
            {
                DrawWorldLoadState(navBar.Body, scale);
            }
            else
            {
                using (AppSurface.Begin(navBar.Body))
                {
                    var drawList = ImGui.GetWindowDrawList();
                    var origin = ImGui.GetCursorScreenPos();
                    var width = ScrollLayout.StableContentWidth();
                    var cursorY = DrawLotteryHero(drawList, origin, width, scale);
                    cursorY = DrawDistricts(drawList, new Vector2(origin.X, cursorY), width, scale);
                    cursorY = DrawWatchPreview(drawList, new Vector2(origin.X, cursorY), width, scale);
                    cursorY = DrawDataFooter(drawList, new Vector2(origin.X, cursorY), width, scale);
                    ReserveTo(origin, width, cursorY + BottomPad * scale);
                }
            }
        }

        overviewButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Globe), Loc.T(L.Housing.ChooseWorld));
        overviewButtons[1] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Cog), Loc.T(L.Housing.Settings));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "housing.nav.overview", WorldTitle(),
            NavBarStyle.From(ui), overviewButtons);
        if (pressed == 0)
        {
            OpenWorldPicker();
        }
        else if (pressed == 1)
        {
            PushRoute(HousingRoute.Settings, WorldTitle());
        }
    }

    private void OpenWorldPicker()
    {
        worldSearch = string.Empty;
        PushRoute(HousingRoute.WorldPicker, RootTitle());
    }

    private void DrawWorldLoadState(Rect body, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (housing.IsRefreshing || housing.State is HousingLoadState.Idle or HousingLoadState.Loading)
        {
            LoadingPulse.Draw(body.Center, 18f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Housing.LoadingFirst));
            return;
        }

        if (HousingArt.StateScreen(drawList, ui, body, FontAwesomeIcon.Wifi,
                Loc.T(L.Housing.Offline), Loc.T(L.Housing.OfflineHint), Loc.T(L.Housing.Retry), scale))
        {
            RequestRefresh();
        }
    }

    private bool AnyDistrictLoaded()
    {
        for (var index = 0; index < districtLoaded.Length; index++)
        {
            if (districtLoaded[index])
            {
                return true;
            }
        }

        return false;
    }

    private void SyncWorldState()
    {
        var world = housing.WorldId;
        if (worldStateRevision == housing.Revision && worldStateWorld == world)
        {
            return;
        }

        worldStateRevision = housing.Revision;
        worldStateWorld = world;
        worldLottery = HousingLotteryState.Unknown;
        worldOpen = 0;
        worldFetchedUtc = default;
        var now = DateTime.UtcNow;
        var districts = HousingDistricts.All;
        for (var index = 0; index < districts.Count; index++)
        {
            districtTexts[index].Reset();
            if (housing.Lookup(world, districts[index].Id) is not { } snapshot)
            {
                districtLoaded[index] = false;
                districtStats[index] = default;
                continue;
            }

            districtLoaded[index] = true;
            districtStats[index] = HousingLottery.Summarize(snapshot.Plots);
            worldOpen += snapshot.Plots.Count;
            if (snapshot.FetchedUtc > worldFetchedUtc)
            {
                worldFetchedUtc = snapshot.FetchedUtc;
            }

            worldLottery = HousingLottery.Prefer(worldLottery, HousingLottery.Resolve(snapshot.Plots, now), now);
        }
    }

    private float DrawLotteryHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var now = DateTime.UtcNow;
        var lottery = worldLottery;
        var expired = lottery.IsKnown && HousingFormat.HasExpired(lottery.EndsUtc, now);
        if (expired)
        {
            housing.RefreshAfterExpiry();
        }

        var pad = HeroPad * scale;
        var iconSize = HeroIconSize * scale;
        var heroStyle = TextStyles.WidgetDisplay;
        var natural = pad * 2f + iconSize + Typography.LineHeight(heroStyle) + HeroBarHeight * scale +
                      Typography.LineHeight(TextStyles.Subheadline) + HeroLineGap * scale * 3f;
        var height = MathF.Max(width * HeroAspect, natural);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("housing.lottery", new Rect(min, max));
        Material.AccentGlass(drawList, min, max, Metrics.Radius.Grouped * scale, scale, HeroHue(lottery, expired));
        var subInk = Palette.WithAlpha(HeroInk, HeroSubAlpha);
        var left = min.X + pad;
        var right = max.X - pad;
        var iconCenter = new Vector2(left + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        drawList.AddCircleFilled(iconCenter, iconSize * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(HeroInk, HeroWashAlpha)), 32);
        ProgressRing.CenterIcon(drawList, iconCenter, FontAwesomeIcon.Ticket, HeroInk, iconSize * 0.48f);
        var labelLeft = iconCenter.X + iconSize * 0.5f + HeroIconGap * scale;
        var phaseLabel = lottery.IsKnown ? HousingFormat.PhaseLabel(lottery.Phase) : Loc.T(L.Housing.LotteryTitle);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var countText = heroOpen.IsCurrent(worldOpen)
            ? heroOpen.Value
            : heroOpen.Store(worldOpen, Loc.Plural(L.Housing.OpenPlots, worldOpen));
        var countWidth = MathF.Min(Typography.Measure(countText, TextStyles.Footnote).X, (right - labelLeft) * 0.5f);
        Typography.Draw(drawList,
            new Vector2(right - countWidth, iconCenter.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(countText, countWidth, TextStyles.Footnote), subInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, iconCenter.Y - headlineHeight * 0.5f),
            Typography.FitText(phaseLabel, MathF.Max(1f, right - countWidth - HeroIconGap * scale - labelLeft),
                TextStyles.Headline), HeroInk, TextStyles.Headline);

        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var subtitleTop = max.Y - pad - subtitleHeight;
        var barMax = new Vector2(right, subtitleTop - HeroLineGap * scale);
        var barMin = new Vector2(left, barMax.Y - HeroBarHeight * scale);
        var progress = lottery.IsKnown ? HousingLottery.Progress(lottery.Phase, lottery.EndsUtc, now) : -1f;
        var shown = heroFill.Step(MathF.Max(0f, progress), Motion.Sheet, deltaSeconds);
        if (progress >= 0f)
        {
            drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(HeroInk, HeroTrackAlpha)),
                HeroBarHeight * scale * 0.5f);
            var fillWidth = MathF.Max(HeroBarHeight * scale, (barMax.X - barMin.X) * Math.Clamp(shown, 0f, 1f));
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + fillWidth, barMax.Y), ImGui.GetColorU32(HeroInk),
                HeroBarHeight * scale * 0.5f);
        }

        var subtitle = !lottery.IsKnown
            ? Loc.T(L.Housing.LotteryUnknownHint)
            : expired
                ? Loc.T(L.Housing.PhaseExpired)
                : heroEnds.IsCurrent(lottery.EndsUtc!.Value.Ticks)
                    ? heroEnds.Value
                    : heroEnds.Store(lottery.EndsUtc!.Value.Ticks,
                        Loc.T(L.Housing.EndsAt, HousingFormat.ExactLocalTime(lottery.EndsUtc.Value)));
        Typography.Draw(drawList, new Vector2(left, subtitleTop),
            Typography.FitText(subtitle, right - left, TextStyles.Subheadline), subInk, TextStyles.Subheadline);

        var hero = lottery.IsKnown ? HousingText.Countdown(ref heroCountdown, lottery.EndsUtc, now) : "--";
        var fitted = WidgetText.FitStyle(hero, heroStyle, right - left, true);
        var heroTop = barMin.Y - HeroLineGap * scale - Typography.LineHeight(fitted);
        WidgetText.Tabular(drawList, new Vector2(left, heroTop), hero, HeroInk, fitted);
        return max.Y;
    }

    private Vector4 HeroHue(HousingLotteryState lottery, bool expired)
    {
        if (!lottery.IsKnown)
        {
            return ui.Accent;
        }

        if (expired || lottery.Phase is HousingLotteryPhase.Unavailable or HousingLotteryPhase.Expired)
        {
            return AppPalettes.HousingClosed;
        }

        return lottery.Phase == HousingLotteryPhase.Results ? AppPalettes.HousingResults : ui.Accent;
    }

    private float DrawDistricts(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + HousingArt.SectionGap * scale;
        var cursorY = top + HousingArt.SectionHeader(drawList, new Vector2(origin.X, top), width,
            Loc.T(L.Housing.DistrictsTitle), string.Empty, ui, out _, scale);
        cursorY += HousingArt.HeaderGap * scale;
        var districts = HousingDistricts.All;
        var rowHeight = HousingArt.RowHeight * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + rowHeight * districts.Count);
        UiAnchors.Report("housing.districts", new Rect(min, max));
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = HousingArt.TileSize * scale;
        for (var index = 0; index < districts.Count; index++)
        {
            var rowTop = min.Y + rowHeight * index;
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + rowHeight));
            if (index > 0)
            {
                HousingArt.Hairline(drawList, ui, row.Min.X + pad + tileSize + HousingArt.TextGap * scale,
                    row.Max.X - pad, rowTop);
            }

            if (DrawDistrictRow(drawList, row, index, districts[index].Id, scale))
            {
                OpenDistrictOnMap(districts[index].Id);
            }
        }

        return max.Y;
    }

    private bool DrawDistrictRow(ImDrawListPtr drawList, Rect row, int index, uint districtId, float scale)
    {
        var hovered = HousingArt.RowInteraction(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = HousingArt.TileSize * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
        HousingArt.DistrictTile(drawList, tileCenter, tileSize, districtId);
        var stats = districtStats[index];
        var loaded = districtLoaded[index];
        var trailingWidth = HousingArt.TrailingValue(drawList, row.Max.X - pad, row.Center.Y,
            loaded ? HousingText.Count(stats.Open) : "--", Loc.T(L.Housing.OpenCaption),
            stats.Open > 0 ? ui.TitleInk : ui.MutedInk, ui.MutedInk, scale);
        var textLeft = tileCenter.X + tileSize * 0.5f + HousingArt.TextGap * scale;
        var textRight = row.Max.X - pad - trailingWidth - HousingArt.TextGap * scale;
        var title = HousingDistricts.DisplayName(districtId);
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (titleHeight + HousingArt.LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(title, MathF.Max(1f, textRight - textLeft), TextStyles.BodyEmphasized), ui.TitleInk,
            TextStyles.BodyEmphasized);
        var lineTop = top + titleHeight + HousingArt.LineGap * scale;
        if (!loaded || stats.Open == 0)
        {
            var empty = loaded ? Loc.T(L.Housing.DistrictNoOpenings) : Loc.T(L.Housing.DistrictNotLoaded);
            Typography.Draw(drawList, new Vector2(textLeft, lineTop),
                Typography.FitText(empty, MathF.Max(1f, textRight - textLeft), TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            DrawSizeSwatches(drawList, new Vector2(textLeft, lineTop + lineHeight * 0.5f), textRight, stats, index,
                scale);
        }

        return hovered && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void DrawSizeSwatches(ImDrawListPtr drawList, Vector2 leftCenter, float right, in HousingDistrictStats stats,
        int index, float scale)
    {
        var x = leftCenter.X;
        Span<int> counts = [stats.Small, stats.Medium, stats.Large];
        Span<HousingPlotSize> sizes = [HousingPlotSize.Small, HousingPlotSize.Medium, HousingPlotSize.Large];
        var swatchRadius = HousingMarkers.Radius * scale * 0.7f;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        for (var sizeIndex = 0; sizeIndex < sizes.Length; sizeIndex++)
        {
            if (counts[sizeIndex] == 0)
            {
                continue;
            }

            var label = HousingText.Count(counts[sizeIndex]);
            var labelWidth = Typography.Measure(label, TextStyles.Footnote).X;
            if (x + swatchRadius * 2f + labelWidth > right)
            {
                break;
            }

            HousingMarkers.DrawSwatch(drawList, new Vector2(x + swatchRadius, leftCenter.Y), sizes[sizeIndex],
                ui.Accent, scale * 0.8f);
            x += swatchRadius * 2f + SwatchGap * scale;
            Typography.Draw(drawList, new Vector2(x, leftCenter.Y - lineHeight * 0.5f), label, ui.MutedInk,
                TextStyles.Footnote);
            x += labelWidth + SwatchSpacing * scale;
        }

        if (!stats.HasFewestEntries)
        {
            return;
        }

        var fewest = districtTexts[index].IsCurrent(stats.FewestEntries)
            ? districtTexts[index].Value
            : districtTexts[index].Store(stats.FewestEntries, Loc.T(L.Housing.FewestEntries, stats.FewestEntries));
        var available = right - x;
        if (available <= lineHeight * 2f)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(x, leftCenter.Y - lineHeight * 0.5f),
            Typography.FitText(fewest, available, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }

    private void OpenDistrictOnMap(uint districtId)
    {
        housing.SelectDistrict(districtId);
        showSubdivision = false;
        ClosePlotCard();
        ResetMapView();
        InvalidateCache();
        activeTab = HousingTab.Map;
        UiFeedback.Play(UiSound.Tap);
    }

    private float DrawWatchPreview(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var watched = housing.Watch.Watched;
        if (watched.Count == 0)
        {
            return origin.Y;
        }

        var top = origin.Y + HousingArt.SectionGap * scale;
        var trailing = watched.Count > WatchPreviewRows ? Loc.T(L.Housing.SeeAll) : string.Empty;
        var cursorY = top + HousingArt.SectionHeader(drawList, new Vector2(origin.X, top), width,
            Loc.T(L.Housing.Watching), trailing, ui, out var seeAll, scale);
        if (seeAll)
        {
            SwitchTab(HousingTab.Watchlist);
        }

        cursorY += HousingArt.HeaderGap * scale;
        var count = Math.Min(WatchPreviewRows, watched.Count);
        var rowHeight = HousingArt.RowHeight * scale;
        var min = new Vector2(origin.X, cursorY);
        var max = new Vector2(origin.X + width, cursorY + rowHeight * count);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = HousingArt.TileSize * scale;
        var now = DateTime.UtcNow;
        for (var index = 0; index < count; index++)
        {
            var rowTop = min.Y + rowHeight * index;
            var row = new Rect(new Vector2(min.X, rowTop), new Vector2(max.X, rowTop + rowHeight));
            if (index > 0)
            {
                HousingArt.Hairline(drawList, ui, row.Min.X + pad + tileSize + HousingArt.TextGap * scale,
                    row.Max.X - pad, rowTop);
            }

            var record = watched[index];
            var hovered = HousingArt.RowInteraction(drawList, ui, row, scale);
            var tileCenter = new Vector2(row.Min.X + pad + tileSize * 0.5f, row.Center.Y);
            HousingArt.DistrictTile(drawList, tileCenter, tileSize, record.DistrictId);
            var phaseEnd = OptionalUnix(record.PhaseEndUnix);
            var countdown = record.StillReported
                ? HousingText.Countdown(ref watchCountdowns[index], phaseEnd, now)
                : string.Empty;
            var trailingWidth = HousingArt.TrailingValue(drawList, row.Max.X - pad, row.Center.Y, countdown,
                record.StillReported ? HousingFormat.PhaseLabel((HousingLotteryPhase)record.Phase) : string.Empty,
                ui.TitleInk, ui.MutedInk, scale);
            var textLeft = tileCenter.X + tileSize * 0.5f + HousingArt.TextGap * scale;
            var key = record.Key.GetHashCode();
            var title = watchTitles[index].IsCurrent(key)
                ? watchTitles[index].Value
                : watchTitles[index].Store(key, Loc.T(L.Housing.PlotAndWard, record.Plot, record.Ward));
            var place = record.StillReported
                ? HousingDistricts.DisplayName(record.DistrictId)
                : NoLongerReportedText(ref watchPlaces[index], record, now);
            HousingArt.Labels(drawList, textLeft, row.Max.X - pad - trailingWidth - HousingArt.TextGap * scale,
                row.Center.Y, title, place, ui.TitleInk, record.StillReported ? ui.MutedInk : AppPalettes.HousingResults,
                scale);
            if (hovered && UiInteract.Click(row.Min, row.Max, hovered))
            {
                OpenWatched(record);
            }
        }

        return max.Y;
    }

    private void OpenWatched(HousingWatchRecord record)
    {
        if (record.StillReported && FindPlot(record.Key) is not null && record.WorldId == housing.WorldId)
        {
            OpenPlot(record.Key);
            return;
        }

        PushDetails(record.Key, RootTitle());
    }

    private float DrawDataFooter(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + HousingArt.SectionGap * scale;
        var now = DateTime.UtcNow;
        var freshness = SnapshotFreshness();
        var text = Refreshing
            ? Loc.T(L.Housing.Updating)
            : HousingText.Updated(ref footerText, worldFetchedUtc, now);
        var refreshLabel = Loc.T(L.Housing.Refresh);
        var refreshSize = Typography.Measure(refreshLabel, TextStyles.Subheadline);
        var lineHeight = MathF.Max(Metrics.Size.TapTarget * scale, refreshSize.Y);
        var centerY = top + lineHeight * 0.5f;
        var dot = FooterDot * scale;
        drawList.AddCircleFilled(new Vector2(origin.X + dot, centerY), dot,
            ImGui.GetColorU32(HousingChrome.FreshnessHue(freshness, ui.Accent)), 12);
        var textLeft = origin.X + dot * 2f + HousingArt.TextGap * scale * 0.5f;
        var textMax = MathF.Max(1f, width - refreshSize.X - HousingArt.TextGap * scale * 2f - (textLeft - origin.X));
        var footnoteHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - footnoteHeight * 0.5f),
            Typography.FitText(text, textMax, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var hitMin = new Vector2(origin.X + width - refreshSize.X - HousingArt.TextGap * scale, top);
        var hitMax = new Vector2(origin.X + width, top + lineHeight);
        var enabled = !Refreshing;
        var hovered = enabled && UiInteract.Hover(hitMin, hitMax);
        Typography.Draw(drawList, new Vector2(origin.X + width - refreshSize.X, centerY - refreshSize.Y * 0.5f),
            refreshLabel, enabled ? hovered ? Palette.Lighten(ui.Accent, 0.15f) : ui.Accent : ui.MutedInk,
            TextStyles.Subheadline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (enabled && UiInteract.Click(hitMin, hitMax, hovered))
        {
            RequestRefresh();
        }

        var bottom = top + lineHeight;
        if (housing.ActiveSource != HousingProviderKind.Cache)
        {
            return bottom;
        }

        var minutes = (long)Math.Max(0d, (now - worldFetchedUtc).TotalMinutes);
        var banner = cachedBanner.IsCurrent(minutes)
            ? cachedBanner.Value
            : cachedBanner.Store(minutes, Loc.T(L.Housing.CachedBanner, HousingFormat.AgeRelative(worldFetchedUtc, now)));
        return bottom + Typography.DrawWrappedLeft(new Vector2(origin.X, bottom), banner, AppPalettes.HousingParchment,
            TextStyles.Footnote, width);
    }
}
