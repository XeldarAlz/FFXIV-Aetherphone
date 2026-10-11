using Aetherphone.Core;
using Aetherphone.Core.Animation;
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
    private const float MinZoom = 0.85f;
    private const float MaxZoom = 4.2f;
    private const float WheelStep = 0.14f;
    private const float ZoomButtonStep = 1.35f;
    private const float DragSlop = 5f;
    private const float MapSmoothTime = 0.11f;
    private const float MapFill = 0.94f;
    private const float LabelZoom = 1.45f;
    private const float ControlInset = 10f;
    private const float PlaceHeight = 48f;
    private const float PlaceTile = 30f;
    private const float PlaceChevron = 14f;
    private const float PlaceMaxWidth = 260f;
    private const float DivisionHeight = 32f;
    private const float DivisionMaxWidth = 220f;
    private const float StatusHeight = 34f;
    private const float StatusDot = 3.5f;
    private const float LegendWidth = 184f;
    private const float LegendRow = 22f;
    private const float PointOfInterestSize = 16f;
    private const float LegendIconSize = 14f;
    private const float StateScrimAlpha = 0.72f;
    private const int ControlCount = 5;

    private readonly string[] controlGlyphs = new string[ControlCount];
    private readonly string[] controlTips = new string[ControlCount];
    private Spring zoomSpring = new(1f);
    private Spring panXSpring;
    private Spring panYSpring;
    private Spring divisionThumb;
    private float zoomTarget = 1f;
    private Vector2 panTarget;
    private Vector2 lastViewportCenter;
    private Vector2 lastViewportSize;
    private bool centerPending;
    private Vector2 lastMapSpan;
    private bool dragging;
    private float dragTravel;
    private bool showSubdivision;
    private bool legendOpen;
    private CachedText placeLine;
    private CachedText statusText;
    private CachedText noOpeningsText;
    private Rect statusRect;

    private void StepMap(float delta)
    {
        zoomSpring.Step(zoomTarget, MapSmoothTime, delta);
        panXSpring.Step(panTarget.X, MapSmoothTime, delta);
        panYSpring.Step(panTarget.Y, MapSmoothTime, delta);
        divisionThumb.Step(showSubdivision ? 1f : 0f, Motion.PageSettle, delta);
        StepPlotCard(delta);
    }

    private void DrawMapTab(Rect area)
    {
        var scale = UiScale.Current;
        UiAnchors.Report("housing.map", area);
        var tabZone = TabBar.Zone(area, scale);
        var bottom = MathF.Min(area.Max.Y, tabZone.Min.Y) - ControlInset * scale;
        if (!housing.HasWorldSelected)
        {
            if (HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, area, FontAwesomeIcon.Globe,
                    Loc.T(L.Housing.NoWorldTitle), Loc.T(L.Housing.NoWorldHint), Loc.T(L.Housing.ChooseWorld),
                    scale))
            {
                OpenWorldPicker();
            }

            return;
        }

        var plan = CurrentPlan();
        var plots = VisiblePlots();
        if (housing.GameMap is null)
        {
            if (HousingArt.StateScreen(ImGui.GetWindowDrawList(), ui, area, FontAwesomeIcon.MapSigns,
                    Loc.T(L.Housing.GameMapUnavailable), Loc.T(L.Housing.GameMapUnavailableHint),
                    Loc.T(L.Housing.ViewAsList), scale))
            {
                SwitchTab(HousingTab.Plots);
            }

            DrawPlaceCapsule(area, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var stackRadius = HousingArt.GlassButtonRadius * scale;
        var stackTop = new Vector2(area.Max.X - ControlInset * scale - stackRadius, area.Min.Y + ControlInset * scale);
        var stackRect = new Rect(new Vector2(stackTop.X - stackRadius, stackTop.Y),
            new Vector2(stackTop.X + stackRadius, stackTop.Y + stackRadius * 2f * ControlCount));
        var cardRect = PlotCardRect(area, bottom, scale);
        var place = PlaceRect(area, scale);
        var legend = LegendRect(stackRect, scale);
        if (legendOpen)
        {
            UiInteract.HoverOverlay(legend);
        }

        var overControls = UiInteract.Hover(stackRect.Min, stackRect.Max) || UiInteract.Hover(place.Min, place.Max) ||
                           DivisionRect(area, scale) is { } division && UiInteract.Hover(division.Min, division.Max) ||
                           plotCardShown.Value > 0.02f && UiInteract.Hover(cardRect.Min, cardRect.Max) ||
                           legendOpen && UiInteract.HoverWindowOnly(legend.Min, legend.Max) ||
                           statusRect.Width > 0f && UiInteract.Hover(statusRect.Min, statusRect.Max);
        var blocked = overControls || ModalOpen || menu.Open;
        var mapSize = MapSize(area);
        HandleGesture(area, mapSize, blocked, plan, plots, scale);
        var origin = MapOrigin(area, mapSize);
        drawList.PushClipRect(area.Min, area.Max, true);
        DrawPlan(drawList, plan, origin, mapSize, scale);
        DrawMarkers(drawList, plan, plots, origin, mapSize, area, scale);
        drawList.PopClipRect();
        DrawStateOverlay(drawList, area, plots, origin, mapSize, scale);
        DrawPlaceCapsule(area, scale);
        DrawDivisionSwitch(area, plan, scale);
        DrawMapControls(drawList, stackTop, stackRadius);
        if (legendOpen)
        {
            DrawLegend(drawList, legend, stackRect, scale);
        }

        statusRect = default;
        if (plotCardShown.Value < 0.5f)
        {
            DrawStatusPill(area, bottom, scale);
        }

        DrawPlotCard(area, bottom, scale);
    }

    private Rect PlaceRect(Rect area, float scale)
    {
        var height = PlaceHeight * scale;
        var reserve = (HousingArt.GlassButtonRadius * 2f + ControlInset * 2f) * scale;
        var width = MathF.Min(PlaceMaxWidth * scale, area.Width - reserve - ControlInset * scale);
        var min = new Vector2(area.Min.X + ControlInset * scale, area.Min.Y + ControlInset * scale);
        return new Rect(min, min + new Vector2(width, height));
    }

    private void DrawPlaceCapsule(Rect area, float scale)
    {
        var rect = PlaceRect(area, scale);
        UiAnchors.Report("housing.context", rect);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("housing.place", down, Motion.PressScaleCard);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = (max.Y - min.Y) * 0.5f;
        Elevation.Icon(drawList, min, max, radius, radius, 0.6f);
        Material.ThemedGlass(drawList, min, max, radius, scale, ui.Palette.BackdropTop);
        var tileSize = PlaceTile * scale;
        var tileCenter = new Vector2(min.X + (max.Y - min.Y) * 0.5f, rect.Center.Y);
        HousingArt.DistrictTile(drawList, tileCenter, tileSize, housing.DistrictId);
        var textLeft = tileCenter.X + tileSize * 0.5f + HousingArt.TextGap * scale * 0.75f;
        var chevronX = max.X - radius * 0.9f;
        var textRight = chevronX - PlaceChevron * scale * 0.5f - HousingArt.TextGap * scale * 0.5f;
        var opened = VisiblePlots().Count;
        var key = ((long)housing.DistrictId << 32) | ((long)housing.Ward << 16) | (uint)opened;
        var line = placeLine.IsCurrent(key)
            ? placeLine.Value
            : placeLine.Store(key, Loc.T(L.Housing.WardOpenLine, housing.Ward, opened));
        HousingArt.Labels(drawList, textLeft, textRight, rect.Center.Y, HousingDistricts.DisplayName(housing.DistrictId),
            line, ui.TitleInk, ui.MutedInk, scale);
        PhoneIcon.Draw(drawList, new Vector2(chevronX, rect.Center.Y), PhoneIcons.ChevronDown, ui.MutedInk,
            PlaceChevron * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenLocationSheet();
        }
    }

    private Rect? DivisionRect(Rect area, float scale)
    {
        if (housing.GameMap is not { HasSubdivision: true })
        {
            return null;
        }

        var place = PlaceRect(area, scale);
        var width = MathF.Min(DivisionMaxWidth * scale, place.Width);
        var top = place.Max.Y + Metrics.Space.Sm * scale;
        return new Rect(new Vector2(place.Min.X, top), new Vector2(place.Min.X + width, top + DivisionHeight * scale));
    }

    private void DrawDivisionSwitch(Rect area, in HousingPlan plan, float scale)
    {
        if (!plan.HasDivisions || DivisionRect(area, scale) is not { } rect)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Elevation.Icon(drawList, rect.Min, rect.Max, radius, radius, 0.6f);
        Material.ThemedGlass(drawList, rect.Min, rect.Max, radius, scale, ui.Palette.BackdropTop);
        var picked = HousingChrome.Segment(rect, Loc.T(L.Housing.MainDivision), Loc.T(L.Housing.Subdivision),
            showSubdivision ? 1 : 0, ui, false, divisionThumb.Value, false);
        if (picked == 1 == showSubdivision)
        {
            return;
        }

        SwitchDivision(picked == 1);
    }

    private void SwitchDivision(bool subdivision)
    {
        showSubdivision = subdivision;
        ClosePlotCard();
        ResetMapView();
        InvalidateCache();
    }

    private void DrawMapControls(ImDrawListPtr drawList, Vector2 stackTop, float radius)
    {
        var filters = housing.Filters.ActiveCount;
        controlGlyphs[0] = PhoneIcons.AdjustmentsHorizontal;
        controlTips[0] = FiltersLabel();
        controlGlyphs[1] = PhoneIcons.Plus;
        controlTips[1] = Loc.T(L.Housing.ZoomIn);
        controlGlyphs[2] = IconGlyph.Of(FontAwesomeIcon.Minus);
        controlTips[2] = Loc.T(L.Housing.ZoomOut);
        var canRecenter = selectedPlot.IsValid;
        controlGlyphs[3] = IconGlyph.Of(canRecenter ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Expand);
        controlTips[3] = canRecenter ? Loc.T(L.Housing.Recenter) : Loc.T(L.Housing.ResetMap);
        controlGlyphs[4] = PhoneIcons.InfoCircle;
        controlTips[4] = Loc.T(L.Housing.Legend);
        UiAnchors.Report("housing.filters", new Rect(new Vector2(stackTop.X - radius, stackTop.Y),
            new Vector2(stackTop.X + radius, stackTop.Y + radius * 2f)));
        var pressed = HousingArt.GlassStack(drawList, "housing.controls", stackTop, radius, controlGlyphs,
            controlTips, ui.Palette.BackdropTop, ui.TitleInk, ui.Hairline, filters > 0 ? 0 : legendOpen ? 4 : -1,
            ui.Accent);
        switch (pressed)
        {
            case 0:
                OpenFilterSheet();
                break;
            case 1:
                UiFeedback.Play(UiSound.Tap);
                ZoomAround(lastViewportCenter, zoomTarget * ZoomButtonStep);
                break;
            case 2:
                UiFeedback.Play(UiSound.Tap);
                ZoomAround(lastViewportCenter, zoomTarget / ZoomButtonStep);
                break;
            case 3:
                UiFeedback.Play(UiSound.Tap);
                if (canRecenter)
                {
                    CenterOnSelected();
                }
                else
                {
                    ResetMapView();
                }

                break;
            case 4:
                legendOpen = !legendOpen;
                UiFeedback.Play(UiSound.Tap);
                break;
        }
    }

    private Rect LegendRect(Rect stack, float scale)
    {
        var height = LegendEntries.Length * LegendRow * scale + Metrics.Space.Lg * scale;
        var max = new Vector2(stack.Min.X - Metrics.Space.Sm * scale, stack.Min.Y + height);
        return new Rect(new Vector2(max.X - LegendWidth * scale, stack.Min.Y), max);
    }

    private void DrawLegend(ImDrawListPtr drawList, Rect rect, Rect stack, float scale)
    {
        var radius = Metrics.Radius.Card * scale;
        Elevation.Floating(drawList, rect.Min, rect.Max, radius, scale, 1f);
        Material.ThemedGlass(drawList, rect.Min, rect.Max, radius, scale, ui.Palette.BackdropTop);
        var entries = LegendEntries;
        var rowHeight = LegendRow * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        for (var index = 0; index < entries.Length; index++)
        {
            var rowCenterY = rect.Min.Y + Metrics.Space.Sm * scale + rowHeight * (index + 0.5f);
            DrawLegendSwatch(drawList, new Vector2(rect.Min.X + 20f * scale, rowCenterY), index, scale);
            Typography.Draw(drawList, new Vector2(rect.Min.X + 38f * scale, rowCenterY - lineHeight * 0.5f),
                Typography.FitText(Loc.T(entries[index]), rect.Width - 46f * scale, TextStyles.Footnote), ui.BodyInk,
                TextStyles.Footnote);
        }

        var overChild = UiInteract.HoverWindowOnly(rect.Min, rect.Max) ||
                        UiInteract.HoverWindowOnly(stack.Min, stack.Max);
        if (UiInteract.ClickedOutside(overChild))
        {
            legendOpen = false;
        }
    }

    private void DrawLegendSwatch(ImDrawListPtr drawList, Vector2 center, int index, float scale)
    {
        switch (index)
        {
            case 0:
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Small, ui.Accent, scale);
                break;
            case 1:
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Medium, ui.Accent, scale);
                break;
            case 2:
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Large, ui.Accent, scale);
                break;
            case 3:
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Small, AppPalettes.HousingResults, scale);
                break;
            case 4:
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Small, ui.MutedInk, scale);
                HousingGlyphs.WatchNotch(drawList, center, 5f * scale, ImGui.GetColorU32(AppPalettes.HousingBrass));
                break;
            case 6:
                DrawLegendIcon(drawList, center, HousingGameMaps.AethernetShardIcon, scale);
                break;
            case 7:
                DrawLegendIcon(drawList, center, HousingGameMaps.MarketBoardIcon, scale);
                break;
            default:
                HousingGlyphs.DashedRing(drawList, center, 7f * scale,
                    ImGui.GetColorU32(AppPalettes.HousingParchment), 1.4f * scale);
                break;
        }
    }

    private static void DrawLegendIcon(ImDrawListPtr drawList, Vector2 center, uint iconId, float scale)
    {
        var half = LegendIconSize * scale * 0.5f;
        GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId, new Vector2(center.X - half, center.Y - half),
            new Vector2(center.X + half, center.Y + half), 0f, scale, requireIcon: true);
    }

    private static readonly LocString[] LegendEntries =
    {
        L.Housing.LegendSmall, L.Housing.LegendMedium, L.Housing.LegendLarge, L.Housing.LegendResults,
        L.Housing.LegendWatched, L.Housing.LegendStale, L.Housing.LegendAethernetShard, L.Housing.LegendMarketBoard,
    };

    private void DrawStatusPill(Rect area, float bottom, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var now = DateTime.UtcNow;
        var text = Refreshing
            ? Loc.T(L.Housing.Updating)
            : housing.Snapshot is { } snapshot
                ? HousingText.Updated(ref statusText, snapshot.FetchedUtc, now)
                : Loc.T(L.Housing.Offline);
        var height = StatusHeight * scale;
        var textSize = Typography.Measure(text, TextStyles.Footnote);
        var dot = StatusDot * scale;
        var pad = Metrics.Space.Md * scale;
        var glyphSize = Metrics.Space.Lg * scale;
        var width = MathF.Min(area.Width - ControlInset * 2f * scale,
            pad * 2f + dot * 2f + Metrics.Space.Sm * scale * 2f + textSize.X + glyphSize);
        var min = new Vector2(area.Min.X + ControlInset * scale, bottom - height);
        var max = new Vector2(min.X + width, bottom);
        statusRect = new Rect(min, max);
        var hovered = !Refreshing && UiInteract.Hover(min, max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale("housing.status", down, Motion.PressScaleControl);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * press;
        Elevation.Icon(drawList, center - half, center + half, half.Y, half.Y, 0.6f);
        Material.ThemedGlass(drawList, center - half, center + half, half.Y, scale, ui.Palette.BackdropTop);
        var x = min.X + pad;
        drawList.AddCircleFilled(new Vector2(x + dot, center.Y), dot,
            ImGui.GetColorU32(HousingChrome.FreshnessHue(SnapshotFreshness(), ui.Accent)), 12);
        x += dot * 2f + Metrics.Space.Sm * scale;
        var textMax = MathF.Max(1f, max.X - pad - glyphSize - Metrics.Space.Sm * scale - x);
        Typography.Draw(drawList, new Vector2(x, center.Y - textSize.Y * 0.5f),
            Typography.FitText(text, textMax, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
        var rotation = Refreshing ? Pulse.Phase(900.0) * MathF.Tau : 0f;
        HousingGlyphs.RefreshArrow(drawList, new Vector2(max.X - pad - glyphSize * 0.5f, center.Y), glyphSize * 0.36f,
            Refreshing ? ui.Accent : ui.MutedInk, 1.6f * scale, rotation);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Housing.Refresh), HoverLabelSide.Above);
        if (UiInteract.Click(min, max, hovered))
        {
            RequestRefresh();
        }
    }

    private float MapSize(Rect viewport) =>
        MathF.Min(viewport.Width, viewport.Height) * MapFill * zoomSpring.Value;

    private Vector2 MapOrigin(Rect viewport, float mapSize) =>
        viewport.Center - new Vector2(mapSize, mapSize) * 0.5f + new Vector2(panXSpring.Value, panYSpring.Value);

    private static Vector2 ToScreen(Vector2 origin, float mapSize, Vector2 normalized) =>
        origin + normalized * mapSize;

    private void ResetMapView()
    {
        zoomTarget = 1f;
        panTarget = Vector2.Zero;
        zoomSpring.SnapTo(1f);
        panXSpring.SnapTo(0f);
        panYSpring.SnapTo(0f);
    }

    private void ZoomAround(Vector2 anchor, float requestedZoom)
    {
        var clamped = Math.Clamp(requestedZoom, MinZoom, MaxZoom);
        if (MathF.Abs(clamped - zoomTarget) < 0.0001f)
        {
            return;
        }

        var ratio = clamped / zoomTarget;
        var offset = anchor - lastViewportCenter;
        panTarget = (panTarget - offset) * ratio + offset;
        zoomTarget = clamped;
        lastMapSpan = new Vector2(MathF.Min(lastViewportSize.X, lastViewportSize.Y) * MapFill * zoomTarget * 0.5f);
        ClampPan();
    }

    private void ClampPan()
    {
        var span = lastMapSpan;
        panTarget = new Vector2(Math.Clamp(panTarget.X, -span.X, span.X), Math.Clamp(panTarget.Y, -span.Y, span.Y));
    }

    private string NoOpeningsText() =>
        noOpeningsText.IsCurrent(housing.Ward)
            ? noOpeningsText.Value
            : noOpeningsText.Store(housing.Ward, Loc.T(L.Housing.NoOpenings, housing.Ward));

    private void CenterOnSelected()
    {
        if (!selectedPlot.IsValid)
        {
            return;
        }

        var normalized = CurrentPlan().PositionOf(selectedPlot.Plot);
        if (zoomTarget < LabelZoom)
        {
            zoomTarget = LabelZoom;
        }

        if (lastViewportSize.X <= 0f || lastViewportSize.Y <= 0f)
        {
            centerPending = true;
            return;
        }

        var mapSize = MathF.Min(lastViewportSize.X, lastViewportSize.Y) * MapFill * zoomTarget;
        lastMapSpan = new Vector2(mapSize * 0.5f);
        var offsetFromCenter = (normalized - new Vector2(0.5f, 0.5f)) * mapSize;
        panTarget = -offsetFromCenter;
        ClampPan();
    }

    private void HandleGesture(Rect viewport, float mapSize, bool blocked, in HousingPlan plan,
        List<HousingPlot> plots, float scale)
    {
        lastViewportCenter = viewport.Center;
        lastViewportSize = viewport.Size;
        lastMapSpan = new Vector2(MathF.Max(0f, mapSize * 0.5f), MathF.Max(0f, mapSize * 0.5f));
        if (centerPending)
        {
            centerPending = false;
            CenterOnSelected();
        }

        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(viewport.Min);
        ImGui.InvisibleButton("##housingMap", viewport.Size, ImGuiButtonFlags.MouseButtonLeft);
        var active = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(viewport.Min, viewport.Max);
        ImGui.SetCursorScreenPos(cursor);
        if (blocked)
        {
            dragging = false;
            return;
        }

        if (hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                ZoomAround(ImGui.GetMousePos(), zoomTarget * (1f + wheel * WheelStep));
            }
        }

        if (active && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragTravel = 0f;
        }

        if (dragging && active)
        {
            var delta = ImGui.GetIO().MouseDelta;
            dragTravel += delta.Length();
            if (dragTravel > DragSlop * scale)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                panTarget += delta;
                ClampPan();
                panXSpring.SnapTo(panTarget.X);
                panYSpring.SnapTo(panTarget.Y);
            }

            return;
        }

        if (!dragging || !ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return;
        }

        dragging = false;
        if (dragTravel > DragSlop * scale)
        {
            return;
        }

        HandleTap(ImGui.GetMousePos(), MapOrigin(viewport, mapSize), mapSize, plan, plots, scale);
    }

    private void HandleTap(Vector2 point, Vector2 origin, float mapSize, in HousingPlan plan,
        List<HousingPlot> plots, float scale)
    {
        if (NearestMarker(point, origin, mapSize, plan, plots, scale) is { } key)
        {
            SelectPlot(key);
            return;
        }

        legendOpen = false;
        ClosePlotCard();
    }

    private HousingPlotKey? NearestMarker(Vector2 point, Vector2 origin, float mapSize, in HousingPlan plan,
        List<HousingPlot> plots, float scale)
    {
        var hit = HousingMarkers.HitRadius * scale;
        var bestDistance = hit * hit;
        HousingPlotKey? best = null;
        for (var index = 0; index < plots.Count; index++)
        {
            if (!plan.TryGetPoint(plots[index].Key.Plot, out var mapPoint))
            {
                continue;
            }

            var distance = (ToScreen(origin, mapSize, mapPoint) - point).LengthSquared();
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = plots[index].Key;
        }

        return best;
    }

    private HousingPlan CurrentPlan() => new(housing.GameMap, showSubdivision);

    private void DrawPlan(ImDrawListPtr drawList, in HousingPlan plan, Vector2 origin, float mapSize, float scale)
    {
        var min = origin;
        var max = origin + new Vector2(mapSize, mapSize);
        var rounding = Metrics.Radius.Widget * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.BackdropBottom));
        if (plan.Map is not { } gameMap || housing.GameMaps.Texture(gameMap) is not { } texture)
        {
            return;
        }

        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), rounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropBottom, 0.22f)),
            rounding);
    }

    private void DrawMarkers(ImDrawListPtr drawList, in HousingPlan plan, List<HousingPlot> plots,
        Vector2 origin, float mapSize, Rect viewport, float scale)
    {
        if (housing.Filters.ShowAllPlots)
        {
            var tint = AppPalettes.HousingParchment;
            for (var index = 0; index < plan.PlotCount; index++)
            {
                HousingMarkers.DrawBackground(drawList, ToScreen(origin, mapSize, plan.PlotAt(index)), scale, tint);
            }
        }

        DrawPointsOfInterest(drawList, plan, origin, mapSize, viewport, scale);
        var mouse = ImGui.GetMousePos();
        var hovered = viewport.Contains(mouse) && UiInteract.Hover(viewport.Min, viewport.Max)
            ? NearestMarker(mouse, origin, mapSize, plan, plots, scale)
            : default(HousingPlotKey?);
        if (hovered is not null)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var emphasis = Pulse.Wave(Pulse.Calm);
        var showLabels = zoomSpring.Value >= LabelZoom;
        var cull = HousingMarkers.HitRadius * 2f * scale;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (!plan.TryGetPoint(plot.Key.Plot, out var mapPoint))
            {
                continue;
            }

            var center = ToScreen(origin, mapSize, mapPoint);
            if (center.X < viewport.Min.X - cull || center.X > viewport.Max.X + cull ||
                center.Y < viewport.Min.Y - cull || center.Y > viewport.Max.Y + cull)
            {
                continue;
            }

            var isSelected = selectedPlot == plot.Key && plotCardOpen;
            var style = new HousingMarkerStyle(plot.Size, plot.Phase, housing.Watch.IsWatched(plot.Key), isSelected,
                FreshnessOf(plot) == HousingDataFreshness.Stale, hovered == plot.Key);
            HousingMarkers.Draw(drawList, center, style, ui.Accent, scale, isSelected ? emphasis : 0f);
            if (!showLabels && !isSelected && !style.Watched)
            {
                continue;
            }

            var radius = HousingMarkers.Radius * scale * HousingMarkers.SizeScale(plot.Size);
            HousingMarkers.DrawLabel(drawList, center, radius, HousingText.Count(plot.Key.Plot), scale);
        }
    }

    private static void DrawPointsOfInterest(ImDrawListPtr drawList, in HousingPlan plan, Vector2 origin,
        float mapSize, Rect viewport, float scale)
    {
        if (plan.Map is not { } gameMap)
        {
            return;
        }

        var points = gameMap.PointsOfInterest;
        var half = PointOfInterestSize * scale * 0.5f;
        for (var index = 0; index < points.Count; index++)
        {
            var center = ToScreen(origin, mapSize, points[index].NormalizedPosition);
            if (center.X < viewport.Min.X - half || center.X > viewport.Max.X + half ||
                center.Y < viewport.Min.Y - half || center.Y > viewport.Max.Y + half)
            {
                continue;
            }

            GameIconTile.Draw(drawList, Plugin.TextureProvider, points[index].IconId,
                new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half), 0f,
                scale, requireIcon: true);
        }
    }

    private void DrawStateOverlay(ImDrawListPtr drawList, Rect viewport, List<HousingPlot> plots, Vector2 origin,
        float mapSize, float scale)
    {
        if (plots.Count > 0)
        {
            return;
        }

        DrawStateScrim(drawList, viewport, origin, mapSize, scale);
        if (housing.Snapshot is null)
        {
            if (housing.IsRefreshing || housing.State is HousingLoadState.Idle or HousingLoadState.Loading)
            {
                LoadingPulse.Draw(viewport.Center, 18f * scale, ui.Accent, ui.MutedInk, Loc.T(L.Housing.LoadingFirst));
                return;
            }

            if (HousingArt.StateScreen(drawList, ui, viewport, FontAwesomeIcon.Wifi, Loc.T(L.Housing.Offline),
                    Loc.T(L.Housing.OfflineHint), Loc.T(L.Housing.Retry), scale))
            {
                RequestRefresh();
            }

            return;
        }

        if (housing.Filters.HasNarrowingFilters && WardHasReportedPlots())
        {
            if (HousingArt.StateScreen(drawList, ui, viewport, FontAwesomeIcon.Filter,
                    Loc.T(L.Housing.NoFilterMatches), string.Empty, Loc.T(L.Housing.ClearFilters), scale))
            {
                ClearFilters();
            }

            return;
        }

        if (OtherDivisionHasPlots())
        {
            if (HousingArt.StateScreen(drawList, ui, viewport, FontAwesomeIcon.Home, NoOpeningsText(), string.Empty,
                    Loc.T(showSubdivision ? L.Housing.MainDivision : L.Housing.Subdivision), scale))
            {
                UiFeedback.Play(UiSound.Tap);
                SwitchDivision(!showSubdivision);
            }

            return;
        }

        var hint = housing.Snapshot.Plots.Count == 0 ? Loc.T(L.Housing.NoScansHint) : string.Empty;
        if (HousingArt.StateScreen(drawList, ui, viewport, FontAwesomeIcon.Home,
                NoOpeningsText(), hint, Loc.T(L.Housing.ChooseWard), scale))
        {
            OpenLocationSheet();
        }
    }

    private void DrawStateScrim(ImDrawListPtr drawList, Rect viewport, Vector2 origin, float mapSize, float scale)
    {
        drawList.PushClipRect(viewport.Min, viewport.Max, true);
        drawList.AddRectFilled(origin, origin + new Vector2(mapSize, mapSize),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropBottom, StateScrimAlpha)),
            Metrics.Radius.Widget * scale, ImDrawFlags.RoundCornersAll);
        drawList.PopClipRect();
    }

    private bool WardHasReportedPlots()
    {
        if (housing.Snapshot is not { } snapshot)
        {
            return false;
        }

        var ward = housing.Ward;
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            if (plots[index].Key.Ward == ward)
            {
                return true;
            }
        }

        return false;
    }

    private bool OtherDivisionHasPlots()
    {
        if (housing.GameMap is not { HasSubdivision: true } || housing.Snapshot is not { } snapshot)
        {
            return false;
        }

        var ward = housing.Ward;
        var wanted = !showSubdivision;
        var plots = snapshot.Plots;
        for (var index = 0; index < plots.Count; index++)
        {
            if (plots[index].Key.Ward == ward && plots[index].IsSubdivision == wanted)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearFilters()
    {
        housing.Filters.Reset();
        housing.PersistFilterDefaults();
        InvalidateCache();
        UiFeedback.Play(UiSound.Tap);
    }
}
