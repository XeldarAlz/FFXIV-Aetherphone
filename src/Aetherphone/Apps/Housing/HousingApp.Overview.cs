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
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const int WatchPreviewRows = 3;
    private const int DistrictCount = 5;
    private const int OverviewButtonCount = 2;
    private const float HeaderGlyphScale = 1.3f;
    private const float HeaderIconWidth = 26f;
    private const float RingMaxFraction = 0.5f;
    private const float RingMinSize = 150f;
    private const float RingThickness = 10f;
    private const float RingGlowThickness = 2f;
    private const float RingGlowAlpha = 0.14f;
    private const float RingTrackAlpha = 0.2f;
    private const float RingTextFraction = 0.84f;
    private const float RingIconSize = 18f;
    private const float RingLineGap = 3f;
    private const float StatIconSize = 20f;
    private const float StatPad = 14f;
    private const float DistrictsGap = 14f;
    private const float TileMinAspect = 0.6f;
    private const float TileMaxAspect = 0.82f;
    private const float WideTileMinAspect = 0.26f;
    private const float WideTileMaxAspect = 0.36f;
    private const float TileIconSize = 30f;
    private const float TileLineGap = 1f;
    private const float TileChevronScale = 0.85f;
    private const float TileChevronWidth = 14f;
    private const float TileStrokeAlpha = 0.16f;
    private const float TileHoverAlpha = 0.08f;
    private const float ScrimTopAlpha = 0.04f;
    private const float ScrimBottomAlpha = 0.86f;
    private const float FallbackTopDarken = 0.2f;
    private const float FallbackBottomDarken = 0.62f;
    private const float TileSubAlpha = 0.84f;
    private const float FooterDot = 3.5f;
    private const float BackdropHeightFraction = 0.55f;
    private const float BackdropAlpha = 0.55f;

    private static readonly Vector4 TileInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ScrimInk = new(0.02f, 0.03f, 0.05f, 1f);
    private static readonly Vector4 BackdropTint = new(0.72f, 0.78f, 0.88f, 1f);
    private static readonly HousingDistrictStats NoWorldStats = new(0, 0, 0, 0, 0, -1, 0);

    private static readonly string[] DistrictTileIds =
    [
        "housing.district.0", "housing.district.1", "housing.district.2", "housing.district.3",
        "housing.district.4",
    ];

    private readonly NavBarButton[] overviewButtons = new NavBarButton[OverviewButtonCount];
    private readonly CachedText[] districtTexts = new CachedText[DistrictCount];
    private readonly CachedText[] districtOpenTexts = new CachedText[DistrictCount];
    private readonly CachedText[] watchCountdowns = new CachedText[WatchPreviewRows];
    private readonly CachedText[] watchPlaces = new CachedText[WatchPreviewRows];
    private readonly CachedText[] watchTitles = new CachedText[WatchPreviewRows];
    private CachedText heroCountdown;
    private CachedText heroEnds;
    private CachedText heroEndsClock;
    private CachedText footerText;
    private CachedText cachedBanner;
    private int worldStateRevision = -1;
    private uint worldStateWorld;
    private HousingLotteryState worldLottery = HousingLotteryState.Unknown;
    private readonly HousingDistrictStats[] districtStats = new HousingDistrictStats[DistrictCount];
    private readonly bool[] districtLoaded = new bool[DistrictCount];
    private HousingDistrictStats worldStats = NoWorldStats;
    private CachedText worldOpenText;
    private DateTime worldFetchedUtc;
    private Spring heroFill;
    private Spring backdropFade;
    private IDalamudTextureWrap? backdropTexture;
    private uint backdropDistrict;
    private CachedText headerSubtitle;

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
                using (var surface = AppSurface.Begin(navBar.Body))
                {
                    var drawList = ImGui.GetWindowDrawList();
                    var origin = ImGui.GetCursorScreenPos();
                    var width = ScrollLayout.StableContentWidth();
                    var restingTop = origin.Y + ImGui.GetScrollY() - surface.Pull;
                    var visibleHeight = navBar.Body.Max.Y - TabBar.ContentInset(scale) - restingTop;
                    var layout = FitOverview(width, visibleHeight, scale);
                    var cursorY = DrawLotteryHero(drawList, origin, width, layout.RingBox, scale);
                    cursorY = DrawDistricts(drawList, new Vector2(origin.X, cursorY), width, in layout, scale);
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
        DrawOverviewHeader(context.Content, scale);
        if (pressed == 0)
        {
            OpenWorldPicker();
        }
        else if (pressed == 1)
        {
            PushRoute(HousingRoute.Settings, WorldTitle());
        }
    }

    private string HeaderSubtitle()
    {
        var phase = housing.HasWorldSelected && worldLottery.IsKnown
            ? HousingFormat.PhaseLabel(worldLottery.Phase)
            : Loc.T(L.Housing.LotteryTitle);
        var world = housing.HasWorldSelected ? housing.WorldName : string.Empty;
        if (world.Length == 0)
        {
            return phase;
        }

        var key = ((long)housing.WorldId << 8) | (byte)(worldLottery.IsKnown ? worldLottery.Phase : 0);
        return headerSubtitle.IsCurrent(key)
            ? headerSubtitle.Value
            : headerSubtitle.Store(key, Loc.T(L.Housing.PhaseOnWorld, phase, world));
    }

    private void UpdateDistrictBackdrop()
    {
        var districtId = housing.DistrictId;
        if (districtId != backdropDistrict)
        {
            backdropDistrict = districtId;
            backdropFade = default;
        }

        backdropTexture = housing.HasWorldSelected ? housing.Art.Backdrop(districtId) : null;
        backdropFade.Step(backdropTexture is null ? 0f : 1f, Motion.Sheet, deltaSeconds);
    }

    private void DrawDistrictBackdrop(ImDrawListPtr drawList, Rect screen, float rounding)
    {
        var fade = Math.Clamp(backdropFade.Value, 0f, 1f);
        if (backdropTexture is not { Width: > 0, Height: > 0 } texture || fade <= 0.001f)
        {
            return;
        }

        var height = screen.Height * BackdropHeightFraction;
        var max = new Vector2(screen.Max.X, screen.Min.Y + height);
        CoverUv(texture.Width / (float)texture.Height, screen.Width / MathF.Max(1f, height), out var uv0,
            out var uv1);
        var map = new TextureMap(screen.Min, uv0, new Vector2((uv1.X - uv0.X) / MathF.Max(1f, screen.Width), 0f),
            new Vector2(0f, (uv1.Y - uv0.Y) / MathF.Max(1f, height)));
        var top = ImGui.GetColorU32(Palette.WithAlpha(BackdropTint, BackdropAlpha * fade));
        var bottom = ImGui.GetColorU32(Palette.WithAlpha(BackdropTint, 0f));
        Squircle.FillImageMapped(drawList, screen.Min, max, rounding, texture.Handle, in map, top, bottom);
    }

    private void DrawOverviewHeader(Rect content, float scale)
    {
        var inset = (Metrics.Space.GlassInset + NavBarMetrics.ButtonPad) * scale;
        var left = content.Min.X + inset;
        var right = content.Max.X - inset - NavBarMetrics.ButtonsWidth(OverviewButtonCount, scale) -
                    Metrics.Space.Sm * scale;
        var rowHeight = (NavBarMetrics.InlineHeight + NavBarMetrics.UntitledTopGap) * scale;
        if (right - left <= HeaderIconWidth * scale)
        {
            return;
        }

        var rect = new Rect(content.Min, new Vector2(right, content.Min.Y + rowHeight));
        using (ScreenLayer.BeginPassive("housing.overview.header", rect))
        {
            var drawList = ImGui.GetWindowDrawList();
            var centerY = content.Min.Y + NavBarMetrics.InlineHeight * scale * 0.5f;
            var iconWidth = HeaderIconWidth * scale;
            AppSkin.Icon(drawList, new Vector2(left + iconWidth * 0.5f, centerY), PhoneIcons.Home, ui.TitleInk,
                HeaderGlyphScale);
            var textLeft = left + iconWidth + Metrics.Space.Glass * scale;
            var textWidth = MathF.Max(1f, right - textLeft);
            var subtitle = HeaderSubtitle();
            var titleHeight = Typography.LineHeight(TextStyles.Title3);
            var subtitleHeight = Typography.LineHeight(TextStyles.Footnote);
            var top = centerY - (titleHeight + subtitleHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(DisplayName, textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
                Typography.FitText(subtitle, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
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
        worldStats = NoWorldStats;
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
            worldStats = Combine(worldStats, districtStats[index]);
            if (snapshot.FetchedUtc > worldFetchedUtc)
            {
                worldFetchedUtc = snapshot.FetchedUtc;
            }

            worldLottery = HousingLottery.Prefer(worldLottery, HousingLottery.Resolve(snapshot.Plots, now), now);
        }
    }

    private static HousingDistrictStats Combine(in HousingDistrictStats total, in HousingDistrictStats district)
    {
        var fewest = !total.HasFewestEntries
            ? district.FewestEntries
            : district.HasFewestEntries
                ? Math.Min(total.FewestEntries, district.FewestEntries)
                : total.FewestEntries;
        return new HousingDistrictStats(total.Open + district.Open, total.Small + district.Small,
            total.Medium + district.Medium, total.Large + district.Large, total.Wards + district.Wards, fewest,
            total.Lottery + district.Lottery);
    }

    private readonly record struct OverviewLayout(float RingBox, float TileHeight, float WideHeight);

    private OverviewLayout FitOverview(float width, float visibleHeight, float scale)
    {
        var gap = Metrics.Space.Glass * scale;
        var halfWidth = (width - gap) * 0.5f;
        var tileHeight = MathF.Max(TileNaturalHeight(scale), halfWidth * TileMinAspect);
        var wideHeight = MathF.Max(TileStackHeight(scale) + Metrics.Space.Glass * scale * 2f,
            width * WideTileMinAspect);
        var belowTiles = WatchPreviewHeight(scale) + DataFooterHeight(width, DateTime.UtcNow, scale) +
                         BottomPad * scale;
        var fixedHeight = DistrictsGap * scale + tileHeight * 2f + wideHeight + gap * 2f + belowTiles;
        var ringMax = MathF.Floor(width * RingMaxFraction);
        var ringBox = Math.Clamp(visibleHeight - fixedHeight, MathF.Min(RingMinSize * scale, ringMax), ringMax);
        var spare = visibleHeight - fixedHeight - ringBox;
        if (spare > 0f)
        {
            var share = spare / 3f;
            tileHeight = MathF.Min(tileHeight + share, MathF.Max(tileHeight, halfWidth * TileMaxAspect));
            wideHeight = MathF.Min(wideHeight + share, MathF.Max(wideHeight, width * WideTileMaxAspect));
        }

        return new OverviewLayout(MathF.Floor(ringBox), MathF.Floor(tileHeight), MathF.Floor(wideHeight));
    }

    private float DrawLotteryHero(ImDrawListPtr drawList, Vector2 origin, float width, float ringBox, float scale)
    {
        var now = DateTime.UtcNow;
        var lottery = worldLottery;
        var expired = lottery.IsKnown && HousingFormat.HasExpired(lottery.EndsUtc, now);
        if (expired)
        {
            housing.RefreshAfterExpiry();
        }

        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + ringBox);
        UiAnchors.Report("housing.lottery", new Rect(min, max));
        var hue = HeroHue(lottery, expired);
        var thickness = RingThickness * scale;
        var center = new Vector2(origin.X + ringBox * 0.5f, origin.Y + ringBox * 0.5f);
        var radius = ringBox * 0.5f - thickness * RingGlowThickness * 0.5f;
        ProgressRing.Track(drawList, center, radius, thickness, Palette.WithAlpha(ui.MutedInk, RingTrackAlpha));
        var progress = lottery.IsKnown ? HousingLottery.Progress(lottery.Phase, lottery.EndsUtc, now) : -1f;
        var remaining = progress >= 0f ? 1f - progress : 0f;
        var shown = heroFill.Step(remaining, Motion.Sheet, deltaSeconds);
        if (progress >= 0f)
        {
            var fraction = Math.Clamp(shown, 0f, 1f);
            ProgressRing.Fill(drawList, center, radius, thickness * RingGlowThickness, fraction,
                Palette.WithAlpha(hue, RingGlowAlpha));
            ProgressRing.Fill(drawList, center, radius, thickness, fraction, hue);
        }

        DrawRingCenter(drawList, center, (radius - thickness * 0.5f) * 2f * RingTextFraction, lottery, expired,
            hue, now, scale);
        var statMin = new Vector2(origin.X + ringBox + Metrics.Space.Md * scale, origin.Y);
        DrawOpenStat(drawList, new Rect(statMin, max), hue, scale);
        return max.Y;
    }

    private void DrawRingCenter(ImDrawListPtr drawList, Vector2 center, float textWidth,
        HousingLotteryState lottery, bool expired, Vector4 hue, DateTime now, float scale)
    {
        var hero = !lottery.IsKnown
            ? "--"
            : expired
                ? Loc.T(L.Housing.CountdownEnded)
                : HousingText.Countdown(ref heroCountdown, lottery.EndsUtc, now);
        var heroStyle = WidgetText.FitStyle(hero, TextStyles.Title1, textWidth, true);
        var dateLine = string.Empty;
        var clockLine = string.Empty;
        if (!lottery.IsKnown)
        {
            dateLine = Loc.T(L.Housing.PhaseUnknown);
        }
        else if (expired)
        {
            dateLine = Loc.T(L.Housing.PhaseExpired);
        }
        else
        {
            var ends = lottery.EndsUtc!.Value;
            dateLine = heroEnds.IsCurrent(ends.Ticks)
                ? heroEnds.Value
                : heroEnds.Store(ends.Ticks, Loc.T(L.Housing.EndsAt, HousingFormat.LocalDate(ends)));
            clockLine = heroEndsClock.IsCurrent(ends.Ticks)
                ? heroEndsClock.Value
                : heroEndsClock.Store(ends.Ticks, HousingFormat.LocalClock(ends));
        }

        var lineStyle = TextStyles.Footnote;
        var dateStyle = WidgetText.FitStyle(dateLine, lineStyle, textWidth, false);
        var clockStyle = WidgetText.FitStyle(clockLine, lineStyle, textWidth, false);
        var iconSize = RingIconSize * scale;
        var gap = RingLineGap * scale;
        var heroHeight = Typography.LineHeight(heroStyle);
        var lineHeight = Typography.LineHeight(lineStyle);
        var linesHeight = clockLine.Length > 0 ? lineHeight * 2f : lineHeight;
        var top = center.Y - (iconSize + gap + heroHeight + gap + linesHeight) * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(center.X, top + iconSize * 0.5f), FontAwesomeIcon.Ticket, hue,
            iconSize);
        var heroTop = top + iconSize + gap;
        var heroWidth = WidgetText.TabularWidth(hero, heroStyle);
        WidgetText.Tabular(drawList, new Vector2(center.X - heroWidth * 0.5f, heroTop), hero, ui.TitleInk,
            heroStyle);
        var lineTop = heroTop + heroHeight + gap;
        DrawCenteredLine(drawList, center.X, lineTop, dateLine, dateStyle);
        if (clockLine.Length > 0)
        {
            DrawCenteredLine(drawList, center.X, lineTop + lineHeight, clockLine, clockStyle);
        }
    }

    private void DrawCenteredLine(ImDrawListPtr drawList, float centerX, float top, string text, in TextStyle style)
    {
        var width = Typography.Measure(text, style).X;
        Typography.Draw(drawList, new Vector2(centerX - width * 0.5f, top), text, ui.MutedInk, style);
    }

    private void DrawOpenStat(ImDrawListPtr drawList, Rect card, Vector4 hue, float scale)
    {
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var pad = StatPad * scale;
        var innerWidth = MathF.Max(1f, card.Width - pad * 2f);
        var iconSize = StatIconSize * scale;
        var iconGap = Metrics.Space.Sm * scale;
        var open = worldStats.Open;
        var number = worldOpenText.IsCurrent(open)
            ? worldOpenText.Value
            : worldOpenText.Store(open, HousingText.Count(open));
        var numberStyle = WidgetText.FitStyle(number, TextStyles.Title1,
            MathF.Max(1f, innerWidth - iconSize - iconGap), true);
        var caption = Loc.T(L.Housing.OpenPlotsReported);
        var captionStyle = WidgetText.FitStyle(caption, TextStyles.Footnote, innerWidth, false);
        var numberHeight = Typography.LineHeight(numberStyle);
        var captionHeight = Typography.LineHeight(captionStyle);
        var breakdownWidth = LayoutTileSwatches(drawList, Vector2.Zero, float.MaxValue, in worldStats, ui.Accent,
            ui.MutedInk, scale, false);
        var showBreakdown = open > 0 && breakdownWidth <= innerWidth;
        var breakdownHeight = showBreakdown ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var breakdownGap = showBreakdown ? Metrics.Space.Glass * scale : 0f;
        var top = card.Center.Y -
                  (numberHeight + Metrics.Space.Xxs * scale + captionHeight + breakdownGap + breakdownHeight) * 0.5f;
        var numberWidth = WidgetText.TabularWidth(number, numberStyle);
        var rowLeft = card.Center.X - (iconSize + iconGap + numberWidth) * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(rowLeft + iconSize * 0.5f, top + numberHeight * 0.5f),
            FontAwesomeIcon.Home, hue, iconSize);
        WidgetText.Tabular(drawList, new Vector2(rowLeft + iconSize + iconGap, top), number, ui.TitleInk,
            numberStyle);
        var captionTop = top + numberHeight + Metrics.Space.Xxs * scale;
        var captionWidth = Typography.Measure(caption, captionStyle).X;
        Typography.Draw(drawList, new Vector2(card.Center.X - captionWidth * 0.5f, captionTop), caption,
            ui.MutedInk, captionStyle);
        if (showBreakdown)
        {
            var rowCenterY = captionTop + captionHeight + breakdownGap + breakdownHeight * 0.5f;
            LayoutTileSwatches(drawList, new Vector2(card.Center.X - breakdownWidth * 0.5f, rowCenterY),
                float.MaxValue, in worldStats, ui.Accent, ui.MutedInk, scale, true);
        }
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

    private float DrawDistricts(ImDrawListPtr drawList, Vector2 origin, float width, in OverviewLayout layout,
        float scale)
    {
        var districts = HousingDistricts.All;
        var gap = Metrics.Space.Glass * scale;
        var halfWidth = (width - gap) * 0.5f;
        var tileHeight = layout.TileHeight;
        var wideHeight = layout.WideHeight;
        var top = origin.Y + DistrictsGap * scale;
        var cursorY = top;
        for (var index = 0; index < districts.Count; index++)
        {
            var column = index % 2;
            var wide = column == 0 && index == districts.Count - 1;
            var height = wide ? wideHeight : tileHeight;
            var left = origin.X + column * (halfWidth + gap);
            var tile = new Rect(new Vector2(left, cursorY),
                new Vector2(wide ? origin.X + width : left + halfWidth, cursorY + height));
            if (DrawDistrictTile(drawList, tile, index, districts[index].Id, wide, scale))
            {
                OpenDistrictOnMap(districts[index].Id);
            }

            if (wide || column == 1)
            {
                cursorY = tile.Max.Y + gap;
            }
        }

        var bottom = cursorY - gap;
        UiAnchors.Report("housing.districts",
            new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, bottom)));
        return bottom;
    }

    private static float TileStackHeight(float scale) =>
        Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Subheadline) +
        Typography.LineHeight(TextStyles.Caption1) + Typography.LineHeight(TextStyles.Footnote) +
        TileLineGap * scale * 3f;

    private static float FewestLineHeight(float scale) =>
        Typography.LineHeight(TextStyles.Caption1) + TileLineGap * scale;

    private bool ShowsFewestEntries(int index)
    {
        var stats = districtStats[index];
        var entriesOpen = !worldLottery.IsKnown || worldLottery.Phase == HousingLotteryPhase.Entry;
        return districtLoaded[index] && stats.Open > 0 && stats.HasFewestEntries && entriesOpen;
    }

    private static float TileNaturalHeight(float scale) =>
        (Metrics.Space.Glass * 2f + TileIconSize + Metrics.Space.Xxs) * scale + TileStackHeight(scale);

    private bool DrawDistrictTile(ImDrawListPtr drawList, Rect rest, int index, uint districtId, bool wide,
        float scale)
    {
        var hovered = HousingChrome.Hover(rest.Min, rest.Max, false);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !DragScrollHost.AnyDragging;
        var press = PressFx.Scale(ImGui.GetID(DistrictTileIds[index]), pressed, PressFx.CardPressedScale);
        var half = rest.Size * 0.5f * press;
        var tile = new Rect(rest.Center - half, rest.Center + half);
        var radius = Metrics.Radius.Grouped * scale * press;
        DrawTileBackdrop(drawList, tile, districtId, radius, wide);
        if (hovered)
        {
            Squircle.Fill(drawList, tile.Min, tile.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(TileInk, TileHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Stroke(drawList, tile.Min, tile.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(TileInk, TileStrokeAlpha)), Metrics.Stroke.Hairline);
        var pad = Metrics.Space.Glass * scale;
        var iconSize = TileIconSize * scale;
        var stackHeight = TileStackHeight(scale);
        float textLeft;
        float stackBottom;
        if (wide)
        {
            var iconCenter = new Vector2(tile.Min.X + pad + iconSize * 0.5f, tile.Center.Y);
            HousingArt.DistrictTile(drawList, iconCenter, iconSize, districtId);
            textLeft = iconCenter.X + iconSize * 0.5f + HousingArt.TextGap * scale;
            var shownHeight = ShowsFewestEntries(index) ? stackHeight : stackHeight - FewestLineHeight(scale);
            stackBottom = tile.Center.Y + shownHeight * 0.5f;
        }
        else
        {
            HousingArt.DistrictTile(drawList,
                new Vector2(tile.Min.X + pad + iconSize * 0.5f, tile.Min.Y + pad + iconSize * 0.5f), iconSize,
                districtId);
            textLeft = tile.Min.X + pad;
            stackBottom = tile.Max.Y - pad;
        }

        DrawTileText(drawList, textLeft, tile.Max.X - pad, stackBottom, index, districtId, scale);
        return hovered && UiInteract.Click(rest.Min, rest.Max, hovered);
    }

    private void DrawTileBackdrop(ImDrawListPtr drawList, Rect tile, uint districtId, float radius, bool wide)
    {
        if (housing.Art.Tile(districtId) is { Width: > 0, Height: > 0 } texture)
        {
            CoverUv(texture.Width / (float)texture.Height, tile.Width / MathF.Max(1f, tile.Height), out var uv0,
                out var uv1);
            Squircle.FillImage(drawList, tile.Min, tile.Max, radius, texture.Handle, ImGui.GetColorU32(TileInk), uv0,
                uv1);
        }
        else
        {
            var hue = HousingArt.DistrictHue(districtId);
            Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius,
                ImGui.GetColorU32(Palette.Darken(hue, FallbackTopDarken)),
                ImGui.GetColorU32(Palette.Darken(hue, FallbackBottomDarken)));
        }

        var clear = ImGui.GetColorU32(Palette.WithAlpha(ScrimInk, ScrimTopAlpha));
        var shade = ImGui.GetColorU32(Palette.WithAlpha(ScrimInk, ScrimBottomAlpha));
        if (wide)
        {
            Squircle.FillHorizontalGradient(drawList, tile.Min, tile.Max, radius, shade, clear);
        }
        else
        {
            Squircle.FillVerticalGradient(drawList, tile.Min, tile.Max, radius, clear, shade);
        }
    }

    private static void CoverUv(float textureAspect, float tileAspect, out Vector2 uv0, out Vector2 uv1)
    {
        if (textureAspect > tileAspect)
        {
            var span = tileAspect / textureAspect;
            uv0 = new Vector2((1f - span) * 0.5f, 0f);
            uv1 = new Vector2((1f + span) * 0.5f, 1f);
            return;
        }

        var height = textureAspect / tileAspect;
        uv0 = new Vector2(0f, (1f - height) * 0.5f);
        uv1 = new Vector2(1f, (1f + height) * 0.5f);
    }

    private void DrawTileText(ImDrawListPtr drawList, float left, float right, float bottom, int index,
        uint districtId, float scale)
    {
        var stats = districtStats[index];
        var loaded = districtLoaded[index];
        var lineGap = TileLineGap * scale;
        var subInk = Palette.WithAlpha(TileInk, TileSubAlpha);
        var chevronWidth = TileChevronWidth * scale;
        var width = MathF.Max(1f, right - left);
        var swatchHeight = Typography.LineHeight(TextStyles.Footnote);
        var swatchCenterY = bottom - swatchHeight * 0.5f;
        AppSkin.Icon(drawList, new Vector2(right - chevronWidth * 0.5f, swatchCenterY), PhoneIcons.ChevronRight,
            TileInk, TileChevronScale);
        var cursorY = bottom - swatchHeight;
        var hasOpenings = loaded && stats.Open > 0;
        if (hasOpenings)
        {
            LayoutTileSwatches(drawList, new Vector2(left, swatchCenterY), right - chevronWidth, in stats, ui.Accent,
                subInk, scale, true);
        }

        if (ShowsFewestEntries(index))
        {
            cursorY -= FewestLineHeight(scale);
            var fewest = districtTexts[index].IsCurrent(stats.FewestEntries)
                ? districtTexts[index].Value
                : districtTexts[index].Store(stats.FewestEntries,
                    Loc.T(L.Housing.FewestEntries, stats.FewestEntries));
            Typography.Draw(drawList, new Vector2(left, cursorY),
                Typography.FitText(fewest, width, TextStyles.Caption1), subInk, TextStyles.Caption1);
        }

        var openHeight = Typography.LineHeight(TextStyles.Subheadline);
        cursorY -= lineGap + openHeight;
        var openLine = !loaded
            ? Loc.T(L.Housing.DistrictNotLoaded)
            : stats.Open == 0
                ? Loc.T(L.Housing.DistrictNoOpenings)
                : districtOpenTexts[index].IsCurrent(stats.Open)
                    ? districtOpenTexts[index].Value
                    : districtOpenTexts[index].Store(stats.Open, Loc.Plural(L.Housing.OpenPlots, stats.Open));
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(openLine, width, TextStyles.Subheadline), subInk, TextStyles.Subheadline);
        cursorY -= lineGap + Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, cursorY),
            Typography.FitText(HousingDistricts.DisplayName(districtId), width, TextStyles.Headline), TileInk,
            TextStyles.Headline);
    }

    private static float LayoutTileSwatches(ImDrawListPtr drawList, Vector2 leftCenter, float right,
        in HousingDistrictStats stats, Vector4 shapeInk, Vector4 labelInk, float scale, bool draw)
    {
        var x = leftCenter.X;
        var end = leftCenter.X;
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
            if (x + swatchRadius * 2f + Metrics.Space.Xxs * scale + labelWidth > right)
            {
                break;
            }

            if (draw)
            {
                HousingMarkers.DrawSwatch(drawList, new Vector2(x + swatchRadius, leftCenter.Y), sizes[sizeIndex],
                    shapeInk, scale * 0.8f);
            }

            x += swatchRadius * 2f + Metrics.Space.Xxs * scale;
            if (draw)
            {
                Typography.Draw(drawList, new Vector2(x, leftCenter.Y - lineHeight * 0.5f), label, labelInk,
                    TextStyles.Footnote);
            }

            end = x + labelWidth;
            x = end + Metrics.Space.Glass * scale;
        }

        return end - leftCenter.X;
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

    private float WatchPreviewHeight(float scale)
    {
        var count = Math.Min(WatchPreviewRows, housing.Watch.Watched.Count);
        if (count == 0)
        {
            return 0f;
        }

        return (HousingArt.SectionGap + CardSectionHeader.HeightUnits + HousingArt.HeaderGap +
                HousingArt.RowHeight * count) * scale;
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
        var top = origin.Y + Metrics.Space.Md * scale;
        var now = DateTime.UtcNow;
        var freshness = SnapshotFreshness();
        var text = Refreshing
            ? Loc.T(L.Housing.Updating)
            : HousingText.Updated(ref footerText, worldFetchedUtc, now);
        var refreshLabel = Loc.T(L.Housing.Refresh);
        var refreshSize = Typography.Measure(refreshLabel, TextStyles.Subheadline);
        var lineHeight = FooterRowHeight(refreshSize, scale);
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

        return bottom + Typography.DrawWrappedLeft(new Vector2(origin.X, bottom), CachedBannerText(now),
            AppPalettes.HousingParchment, TextStyles.Footnote, width);
    }

    private float DataFooterHeight(float width, DateTime now, float scale)
    {
        var refreshSize = Typography.Measure(Loc.T(L.Housing.Refresh), TextStyles.Subheadline);
        var height = Metrics.Space.Md * scale + FooterRowHeight(refreshSize, scale);
        if (housing.ActiveSource != HousingProviderKind.Cache)
        {
            return height;
        }

        return height + Typography.MeasureWrappedBlock(CachedBannerText(now), TextStyles.Footnote, width).Y;
    }

    private static float FooterRowHeight(Vector2 refreshSize, float scale) =>
        MathF.Max(Metrics.Size.TapTarget * scale, refreshSize.Y);

    private string CachedBannerText(DateTime now)
    {
        var minutes = (long)Math.Max(0d, (now - worldFetchedUtc).TotalMinutes);
        return cachedBanner.IsCurrent(minutes)
            ? cachedBanner.Value
            : cachedBanner.Store(minutes,
                Loc.T(L.Housing.CachedBanner, HousingFormat.AgeRelative(worldFetchedUtc, now)));
    }
}
