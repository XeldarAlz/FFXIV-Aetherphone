using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
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

    private const float LabelZoom = 1.45f;

    private void DrawMapRoute(Rect area)
    {
        var scale = UiScale.Current;
        DrawTopBar(area, scale);
        var top = area.Min.Y + TopBarHeight * scale;
        var contextHeight = HousingChrome.SelectorHeight(scale) + 10f * scale;
        var contextBar = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + contextHeight));
        DrawContextBar(contextBar, scale);
        var phaseBar = new Rect(new Vector2(area.Min.X, contextBar.Max.Y),
            new Vector2(area.Max.X, contextBar.Max.Y + PhaseBarHeight * scale));
        DrawPhaseBar(phaseBar, scale);
        var bannerBottom = DrawDataBanner(area, phaseBar.Max.Y, scale);
        var footer = new Rect(new Vector2(area.Min.X, area.Max.Y - FooterHeight * scale), area.Max);
        var viewport = new Rect(new Vector2(area.Min.X, bannerBottom), new Vector2(area.Max.X, footer.Min.Y));
        UiAnchors.Report("housing.map", viewport);
        if (!housing.HasWorldSelected)
        {
            DrawNoWorldState(viewport, scale);
            DrawFooter(footer, scale);
            return;
        }

        if (housing.GameMap is null)
        {
            DrawEmptyCard(viewport, FontAwesomeIcon.MapSigns, Loc.T(L.Housing.GameMapUnavailable),
                Loc.T(L.Housing.GameMapUnavailableHint), Loc.T(L.Housing.ViewAsList),
                () => Push(HousingRoute.List), scale);
            DrawFooter(footer, scale);
            return;
        }

        DrawMapViewport(viewport, scale);
        DrawFooter(footer, scale);
        DrawSheet(area, viewport, scale);
        DrawFilterDrawer(area, scale);
        DrawWardPicker(area, viewport, scale);
    }

    private void DrawTopBar(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + TopBarHeight * scale * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 40f * scale, area.Min.Y + TopBarHeight * scale);
        var backHovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);
        if (BackButton.Draw("housing.root.back", new Vector2(area.Min.X + 15f * scale, rowCenterY), 15f * scale,
                ui.Accent, backHovered, scale))
        {
            Back();
        }

        var emblemCenter = new Vector2(area.Min.X + 40f * scale, rowCenterY);
        DrawAppEmblem(drawList, emblemCenter, 26f * scale);
        var buttonRadius = 15f * scale;
        var gap = 34f * scale;
        var rightmost = area.Max.X - 14f * scale - buttonRadius;
        var settingsCenter = new Vector2(rightmost, rowCenterY);
        var watchCenter = new Vector2(rightmost - gap, rowCenterY);
        var listCenter = new Vector2(rightmost - gap * 2f, rowCenterY);
        var titleLeft = emblemCenter.X + 14f * scale;
        var titleRight = listCenter.X - buttonRadius - 8f * scale;
        var titleStyle = TextStyles.Title3;
        var titleWidth = MathF.Max(1f, titleRight - titleLeft);
        Typography.Draw(drawList, new Vector2(titleLeft, rowCenterY - Typography.LineHeight(titleStyle) * 0.5f),
            Typography.FitText(DisplayName, titleWidth, titleStyle), ui.TitleInk, titleStyle);
        if (HousingChrome.MapButton(listCenter, buttonRadius, FontAwesomeIcon.ListUl, ui,
                Loc.T(L.Housing.ViewAsList), false, false))
        {
            Push(HousingRoute.List);
        }

        if (HousingChrome.MapButton(watchCenter, buttonRadius, FontAwesomeIcon.Bookmark, ui,
                Loc.T(L.Housing.Watchlist), false, false))
        {
            Push(HousingRoute.Watchlist);
        }

        if (HousingChrome.MapButton(settingsCenter, buttonRadius, FontAwesomeIcon.Cog, ui,
                Loc.T(L.Housing.Settings), false, false))
        {
            Push(HousingRoute.Settings);
        }

        var watchCount = housing.Watch.Watched.Count;
        if (watchCount > 0)
        {
            var badgeCenter = new Vector2(watchCenter.X + buttonRadius * 0.72f, watchCenter.Y - buttonRadius * 0.72f);
            drawList.AddCircleFilled(badgeCenter, 5.5f * scale, ImGui.GetColorU32(AppPalettes.HousingBrass), 16);
        }
    }

    private void DrawAppEmblem(ImDrawListPtr drawList, Vector2 center, float size)
    {
        if (AppIconTextures.TryDraw(drawList, Id, center, size, ui.Accent))
        {
            return;
        }

        HousingGlyphs.Estate(drawList, center, size * 0.31f, ui.Accent, ui.Palette.BackdropTop);
    }

    private void DrawContextBar(Rect bar, float scale)
    {
        var pad = 14f * scale;
        var gap = 7f * scale;
        var height = HousingChrome.SelectorHeight(scale);
        var top = bar.Center.Y - height * 0.5f;
        var available = bar.Width - pad * 2f - gap * 2f;
        var worldWidth = available * 0.42f;
        var districtWidth = available * 0.35f;
        var wardWidth = available - worldWidth - districtWidth;
        var x = bar.Min.X + pad;
        var worldRect = new Rect(new Vector2(x, top), new Vector2(x + worldWidth, top + height));
        x = worldRect.Max.X + gap;
        var districtRect = new Rect(new Vector2(x, top), new Vector2(x + districtWidth, top + height));
        x = districtRect.Max.X + gap;
        var wardRect = new Rect(new Vector2(x, top), new Vector2(x + wardWidth, top + height));
        var worldName = housing.WorldName;
        if (HousingChrome.Selector(worldRect, Loc.T(L.Housing.WorldLabel),
                worldName.Length > 0 ? worldName : Loc.T(L.Housing.ChooseWorld), ui, false))
        {
            worldSearch = string.Empty;
            Push(HousingRoute.WorldPicker);
        }

        if (HousingChrome.Selector(districtRect, Loc.T(L.Housing.DistrictLabel),
                HousingDistricts.ShortDisplayName(housing.DistrictId), ui, false))
        {
            OpenDistrictMenu(districtRect);
        }

        if (HousingChrome.Selector(wardRect, Loc.T(L.Housing.WardLabel), housing.Ward.ToString(Loc.Culture), ui,
                false))
        {
            ShowOverlay(wardPickerOpen ? HousingOverlay.None : HousingOverlay.WardPicker);
        }
    }

    private void DrawPhaseBar(Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var plots = VisiblePlots();
        var now = DateTime.UtcNow;
        var phase = HousingLotteryPhase.Unknown;
        DateTime? soonest = null;
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (plot.PhaseEndsUtc is not { } ends || plot.Phase == HousingLotteryPhase.Unknown)
            {
                continue;
            }

            if (soonest is null || ends < soonest)
            {
                soonest = ends;
                phase = plot.Phase;
            }
        }

        if (soonest is not null && HousingFormat.HasExpired(soonest, now))
        {
            phase = HousingLotteryPhase.Expired;
            housing.RefreshAfterExpiry();
        }

        var label = Loc.Culture.TextInfo.ToUpper(HousingFormat.PhaseLabel(phase));
        var labelStyle = TextStyles.Caption1;
        Typography.Draw(drawList, new Vector2(bar.Min.X + pad, bar.Center.Y - Typography.LineHeight(labelStyle) * 0.5f),
            label, ui.HeaderInk, labelStyle);
        var timerText = phase switch
        {
            HousingLotteryPhase.Expired => Loc.T(L.Housing.PhaseExpired),
            _ when soonest is null => Loc.T(L.Housing.TimeUnknown),
            _ => Loc.T(L.Housing.Remaining, HousingFormat.Countdown(HousingFormat.Remaining(soonest, now)
                ?? TimeSpan.Zero)),
        };
        var timerStyle = TextStyles.SubheadlineEmphasized;
        var timerSize = Typography.Measure(timerText, timerStyle);
        Typography.Draw(drawList, new Vector2(bar.Max.X - pad - timerSize.X, bar.Center.Y - timerSize.Y * 0.5f),
            timerText, phase == HousingLotteryPhase.Entry ? ui.TitleInk : ui.BodyInk, timerStyle);
    }

    private float DrawDataBanner(Rect area, float top, float scale)
    {
        if (housing.ActiveSource != HousingProviderKind.Cache)
        {
            return top;
        }

        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var text = Loc.T(L.Housing.CachedBanner,
            HousingFormat.AgeRelative(housing.Snapshot?.FetchedUtc ?? default, DateTime.UtcNow));
        var style = TextStyles.Caption1;
        var textWidth = area.Width - pad * 2f - 20f * scale;
        var textHeight = Typography.MeasureWrappedBlock(text, style, textWidth).Y;
        var height = textHeight + 12f * scale;
        var min = new Vector2(area.Min.X + pad, top + 2f * scale);
        var max = new Vector2(area.Max.X - pad, min.Y + height);
        var hue = AppPalettes.HousingParchment;
        var rounding = Metrics.Radius.Sm * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.16f)));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.42f)),
            Metrics.Stroke.Hairline);
        var ink = Palette.Mix(hue, new Vector4(1f, 1f, 1f, 1f), 0.45f);
        Typography.DrawWrappedCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), text,
            ink, style, textWidth);
        return max.Y + 2f * scale;
    }

    private void DrawMapViewport(Rect viewport, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var plan = CurrentPlan();
        var plots = VisiblePlots();
        var controlsBlocked = ReserveControls(viewport, scale, out var zoomIn, out var zoomOut, out var recenter,
            out var legendButton, out var buttonRadius);
        var sheetRect = SheetRect(viewport, scale);
        var pointerOverSheet = sheetSpring.Value > 0.02f &&
                               ImGui.IsMouseHoveringRect(sheetRect.Min, sheetRect.Max);
        var gestureBlocked = controlsBlocked || pointerOverSheet || filtersOpen || wardPickerOpen ||
                             reminderPickerOpen || menu.Open;
        var mapSize = MapSize(viewport);
        HandleGesture(viewport, mapSize, gestureBlocked, plan, plots, scale);
        var origin = MapOrigin(viewport, mapSize);
        drawList.PushClipRect(viewport.Min, viewport.Max, true);
        DrawPlan(drawList, plan, origin, mapSize, scale);
        DrawMarkers(drawList, plan, plots, origin, mapSize, viewport, scale);
        drawList.PopClipRect();
        DrawViewportEdges(drawList, viewport, scale);
        DrawStateOverlay(viewport, plots, scale);
        DrawMapControls(zoomIn, zoomOut, recenter, legendButton, buttonRadius);
        DrawDivisionSwitch(viewport, plan, scale);
        if (legendOpen)
        {
            DrawLegend(viewport, scale);
        }

        DrawFirstUseHint(viewport, plots, scale);
    }

    private Rect SheetRect(Rect viewport, float scale)
    {
        var height = MathF.Min(SheetHeightFor(reminderPickerOpen, scale), viewport.Height * 0.94f);
        return new Rect(new Vector2(viewport.Min.X, viewport.Max.Y - height), viewport.Max);
    }

    private bool ReserveControls(Rect viewport, float scale, out Vector2 zoomIn, out Vector2 zoomOut,
        out Vector2 recenter, out Vector2 legendButton, out float radius)
    {
        radius = 15f * scale;
        var right = viewport.Max.X - 14f * scale - radius;
        var spacing = radius * 2.35f;
        var firstY = viewport.Min.Y + 16f * scale + radius;
        zoomIn = new Vector2(right, firstY);
        zoomOut = new Vector2(right, firstY + spacing);
        recenter = new Vector2(right, firstY + spacing * 2f);
        legendButton = new Vector2(viewport.Min.X + 14f * scale + radius, viewport.Min.Y + 16f * scale + radius);
        var mouse = ImGui.GetMousePos();
        return Near(mouse, zoomIn, radius) || Near(mouse, zoomOut, radius) || Near(mouse, recenter, radius) ||
               Near(mouse, legendButton, radius);
    }

    private static bool Near(Vector2 point, Vector2 center, float radius)
    {
        var offset = point - center;
        return offset.LengthSquared() <= radius * radius * 1.44f;
    }

    private void DrawMapControls(Vector2 zoomIn, Vector2 zoomOut, Vector2 recenter, Vector2 legendButton,
        float radius)
    {
        if (HousingChrome.MapButton(zoomIn, radius, FontAwesomeIcon.Plus, ui, Loc.T(L.Housing.ZoomIn), false,
                false))
        {
            ZoomAround(zoomIn, zoomTarget * ZoomButtonStep);
        }

        if (HousingChrome.MapButton(zoomOut, radius, FontAwesomeIcon.Minus, ui, Loc.T(L.Housing.ZoomOut), false,
                false))
        {
            ZoomAround(zoomOut, zoomTarget / ZoomButtonStep);
        }

        var canRecenter = selectedPlot.IsValid;
        if (HousingChrome.MapButton(recenter, radius,
                canRecenter ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Expand, ui,
                canRecenter ? Loc.T(L.Housing.Recenter) : Loc.T(L.Housing.ResetMap), false, false))
        {
            if (canRecenter)
            {
                CenterOnSelected();
            }
            else
            {
                ResetMapView();
            }
        }

        if (HousingChrome.MapButton(legendButton, radius, FontAwesomeIcon.Question, ui, Loc.T(L.Housing.Legend),
                legendOpen, false))
        {
            legendOpen = !legendOpen;
        }
    }

    private float MapSize(Rect viewport) =>
        MathF.Min(viewport.Width, viewport.Height) * 0.94f * zoomSpring.Value;

    private Vector2 MapOrigin(Rect viewport, float mapSize) =>
        viewport.Center - new Vector2(mapSize, mapSize) * 0.5f + new Vector2(panXSpring.Value, panYSpring.Value);

    private Vector2 ToScreen(Vector2 origin, float mapSize, Vector2 normalized) =>
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
        var offset = anchor - LastViewportCenter;
        panTarget = (panTarget - offset) * ratio + offset;
        zoomTarget = clamped;
        ClampPan();
    }

    private Vector2 LastViewportCenter { get; set; }

    private void ClampPan()
    {
        var span = LastMapSpan;
        panTarget = new Vector2(Math.Clamp(panTarget.X, -span.X, span.X), Math.Clamp(panTarget.Y, -span.Y, span.Y));
    }

    private Vector2 LastMapSpan { get; set; }

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

        var mapSize = MathF.Min(LastViewportSize.X, LastViewportSize.Y) * 0.94f * zoomTarget;
        var offsetFromCenter = (normalized - new Vector2(0.5f, 0.5f)) * mapSize;
        panTarget = -offsetFromCenter;
        ClampPan();
    }

    private Vector2 LastViewportSize { get; set; }

    private void HandleGesture(Rect viewport, float mapSize, bool blocked, in HousingPlan plan,
        List<HousingPlot> plots, float scale)
    {
        LastViewportCenter = viewport.Center;
        LastViewportSize = viewport.Size;
        LastMapSpan = new Vector2(MathF.Max(0f, mapSize * 0.5f), MathF.Max(0f, mapSize * 0.5f));
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(viewport.Min);
        ImGui.InvisibleButton("##housingMap", viewport.Size, ImGuiButtonFlags.MouseButtonLeft);
        var active = ImGui.IsItemActive();
        var hovered = ImGui.IsItemHovered();
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

        if (active && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
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
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                panTarget += delta;
                panXSpring.SnapTo(panTarget.X);
                panYSpring.SnapTo(panTarget.Y);
                ClampPan();
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

        var origin = MapOrigin(viewport, mapSize);
        HandleTap(ImGui.GetMousePos(), origin, mapSize, plan, plots, scale);
    }

    private void HandleTap(Vector2 point, Vector2 origin, float mapSize, in HousingPlan plan,
        List<HousingPlot> plots, float scale)
    {
        var hit = HousingMarkers.HitRadius * scale;
        var bestDistance = hit * hit;
        var found = false;
        var best = default(HousingPlotKey);
        for (var index = 0; index < plots.Count; index++)
        {
            var plot = plots[index];
            if (!plan.TryGetPoint(plot.Key.Plot, out var mapPoint))
            {
                continue;
            }

            var center = ToScreen(origin, mapSize, mapPoint);
            var distance = (center - point).LengthSquared();
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = plot.Key;
            found = true;
        }

        if (!found)
        {
            if (sheetOpen)
            {
                sheetOpen = false;
            }

            return;
        }

        SelectPlot(best);
    }

    private void SelectPlot(HousingPlotKey key)
    {
        if (selectedPlot == key && sheetOpen)
        {
            return;
        }

        selectedPlot = key;
        sheetOpen = true;
        reminderPickerOpen = false;
        reminderChoice = IndexOfLeadTime(configuration.HousingReminderMinutes);
    }

    private static int IndexOfLeadTime(int minutes)
    {
        var choices = HousingDefaults.ReminderChoices;
        for (var index = 0; index < choices.Length; index++)
        {
            if (choices[index] == minutes)
            {
                return index;
            }
        }

        return 2;
    }

    private HousingPlotKey? HoveredMarker(Vector2 origin, float mapSize, in HousingPlan plan,
        List<HousingPlot> plots, float scale, Rect viewport)
    {
        var mouse = ImGui.GetMousePos();
        if (!viewport.Contains(mouse))
        {
            return null;
        }

        var hit = HousingMarkers.HitRadius * scale;
        var bestDistance = hit * hit;
        HousingPlotKey? best = null;
        for (var index = 0; index < plots.Count; index++)
        {
            if (!plan.TryGetPoint(plots[index].Key.Plot, out var mapPoint))
            {
                continue;
            }

            var center = ToScreen(origin, mapSize, mapPoint);
            var distance = (center - mouse).LengthSquared();
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
        if (plan.Map is { } gameMap && DrawGameMapTexture(drawList, gameMap, origin, mapSize, scale))
        {
            return;
        }

        var min = origin;
        var max = origin + new Vector2(mapSize, mapSize);
        var rounding = 10f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0.08f, 0.10f, 0.09f, 1f)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingBrass, 0.24f)), 1.2f * scale);
    }

    private bool DrawGameMapTexture(ImDrawListPtr drawList, HousingGameMap gameMap, Vector2 origin, float mapSize,
        float scale)
    {
        var texture = housing.GameMaps.Texture(gameMap);
        if (texture is null)
        {
            return false;
        }

        var min = origin;
        var max = origin + new Vector2(mapSize, mapSize);
        var rounding = 10f * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(0.06f, 0.07f, 0.07f, 1f)));
        drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.94f)), rounding, ImDrawFlags.RoundCornersAll);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.03f, 0.05f, 0.04f, 0.22f)), rounding);
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingBrass, 0.34f)), 1.4f * scale);
        return true;
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

        var hovered = HoveredMarker(origin, mapSize, plan, plots, scale, viewport);
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

            var isSelected = selectedPlot == plot.Key;
            var style = new HousingMarkerStyle(plot.Size, plot.Phase, housing.Watch.IsWatched(plot.Key), isSelected,
                IsStale(plot), hovered == plot.Key);
            HousingMarkers.Draw(drawList, center, style, ui.Accent, scale, isSelected ? emphasis : 0f);
            var wantsLabel = showLabels || isSelected || style.Watched;
            if (!wantsLabel)
            {
                continue;
            }

            var radius = HousingMarkers.Radius * scale * HousingMarkers.SizeScale(plot.Size);
            HousingMarkers.DrawLabel(drawList, center, radius, plot.Key.Plot.ToString(Loc.Culture), scale);
        }
    }

    private void DrawViewportEdges(ImDrawListPtr drawList, Rect viewport, float scale)
    {
        var fade = 18f * scale;
        var top = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0.85f));
        var clear = ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropTop, 0f));
        drawList.AddRectFilledMultiColor(viewport.Min, new Vector2(viewport.Max.X, viewport.Min.Y + fade), top, top,
            clear, clear);
    }

    private void DrawStateOverlay(Rect viewport, List<HousingPlot> plots, float scale)
    {
        if (plots.Count > 0)
        {
            return;
        }

        if (housing.Snapshot is null)
        {
            if (housing.IsRefreshing)
            {
                DrawLoading(viewport, Loc.T(L.Housing.LoadingFirst), scale);
                return;
            }

            DrawOfflineState(viewport, scale);
            return;
        }

        if (housing.Filters.HasNarrowingFilters && WardHasReportedPlots())
        {
            DrawEmptyCard(viewport, FontAwesomeIcon.Filter, Loc.T(L.Housing.NoFilterMatches), null,
                Loc.T(L.Housing.ClearFilters), () =>
                {
                    housing.Filters.Reset();
                    housing.PersistFilterDefaults();
                    InvalidateCache();
                }, scale);
            return;
        }

        if (housing.Snapshot is { Plots.Count: 0 })
        {
            DrawEmptyCard(viewport, FontAwesomeIcon.MapSigns, Loc.T(L.Housing.NoScans),
                Loc.T(L.Housing.NoScansHint), Loc.T(L.Housing.ChooseWard), () => wardPickerOpen = true, scale);
            return;
        }

        DrawEmptyCard(viewport, FontAwesomeIcon.Home, Loc.T(L.Housing.NoOpenings, housing.Ward), null,
            Loc.T(L.Housing.ChooseWard), () => wardPickerOpen = true, scale);
    }

    private void DrawDivisionSwitch(Rect viewport, in HousingPlan plan, float scale)
    {
        if (!plan.HasDivisions)
        {
            return;
        }

        var mainLabel = Loc.T(L.Housing.MainDivision);
        var subLabel = Loc.T(L.Housing.Subdivision);
        var height = 26f * scale;
        var width = MathF.Min(viewport.Width - 100f * scale,
            Typography.Measure(mainLabel, TextStyles.SubheadlineEmphasized).X +
            Typography.Measure(subLabel, TextStyles.SubheadlineEmphasized).X + 46f * scale);
        var center = new Vector2(viewport.Center.X, viewport.Min.Y + 16f * scale + height * 0.5f);
        var rect = new Rect(new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f),
            new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f));
        Elevation.Floating(ImGui.GetWindowDrawList(), rect.Min, rect.Max, height * 0.5f, scale, 0.7f);
        var picked = HousingChrome.Segment(rect, mainLabel, subLabel, showSubdivision ? 1 : 0, ui, false);
        if (picked == 1 == showSubdivision)
        {
            return;
        }

        showSubdivision = picked == 1;
        sheetOpen = false;
        selectedPlot = default;
        ResetMapView();
        InvalidateCache();
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

    private void DrawLoading(Rect viewport, string label, float scale)
    {
        LoadingPulse.Draw(new Vector2(viewport.Center.X, viewport.Center.Y - 12f * scale), 18f * scale, ui.Accent,
            ui.MutedInk, label);
    }

    private void DrawOfflineState(Rect viewport, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = MathF.Min(viewport.Width - 48f * scale, 300f * scale);
        var height = 176f * scale;
        var center = viewport.Center;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale, true);
        HousingGlyphs.Estate(drawList, new Vector2(center.X, min.Y + 36f * scale), 16f * scale, ui.MutedInk,
            ui.Palette.BackdropTop);
        Typography.DrawCentered(drawList, new Vector2(center.X, min.Y + 76f * scale), Loc.T(L.Housing.Offline),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, min.Y + 108f * scale),
            Loc.T(L.Housing.OfflineHint), ui.MutedInk, TextStyles.Footnote, width - 32f * scale);
        var buttonHeight = 30f * scale;
        var buttonY = max.Y - 18f * scale - buttonHeight;
        var retry = new Rect(new Vector2(min.X + 24f * scale, buttonY),
            new Vector2(max.X - 24f * scale, buttonY + buttonHeight));
        if (HousingChrome.PillButton(retry, Loc.T(L.Housing.Retry), true, ui, false))
        {
            RequestRefresh();
        }
    }

    private void DrawNoWorldState(Rect viewport, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = MathF.Min(viewport.Width - 48f * scale, 300f * scale);
        var height = 190f * scale;
        var center = viewport.Center;
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale, true);
        HousingGlyphs.Estate(drawList, new Vector2(center.X, min.Y + 38f * scale), 18f * scale, ui.Accent,
            ui.Palette.BackdropTop);
        Typography.DrawCentered(drawList, new Vector2(center.X, min.Y + 84f * scale), Loc.T(L.Housing.NoWorldTitle),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, min.Y + 116f * scale),
            Loc.T(L.Housing.NoWorldHint), ui.MutedInk, TextStyles.Footnote, width - 32f * scale);
        var buttonHeight = 32f * scale;
        var chooseY = max.Y - 18f * scale - buttonHeight;
        var choose = new Rect(new Vector2(min.X + 20f * scale, chooseY),
            new Vector2(max.X - 20f * scale, chooseY + buttonHeight));
        if (HousingChrome.PillButton(choose, Loc.T(L.Housing.ChooseWorld), true, ui, false))
        {
            worldSearch = string.Empty;
            Push(HousingRoute.WorldPicker);
        }
    }

    private void DrawEmptyCard(Rect viewport, FontAwesomeIcon icon, string title, string? hint, string actionLabel,
        Action onAction, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = MathF.Min(viewport.Width - 48f * scale, 300f * scale);
        var hintHeight = hint is null
            ? 0f
            : Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - 32f * scale).Y + 10f * scale;
        var height = 148f * scale + hintHeight;
        var center = new Vector2(viewport.Center.X, viewport.Center.Y - 10f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale, true);
        var iconCenter = new Vector2(center.X, min.Y + 34f * scale);
        drawList.AddCircleFilled(iconCenter, 20f * scale, ImGui.GetColorU32(ui.FieldSurface), 28);
        AppSkin.Icon(drawList, iconCenter, icon.ToIconString(), ui.MutedInk, 1f);
        var titleY = min.Y + 70f * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(center.X, titleY + 8f * scale), title, ui.TitleInk,
            TextStyles.SubheadlineEmphasized, width - 28f * scale);
        if (hint is not null)
        {
            Typography.DrawWrappedCentered(drawList, new Vector2(center.X, titleY + 34f * scale + hintHeight * 0.2f),
                hint, ui.MutedInk, TextStyles.Footnote, width - 32f * scale);
        }

        var buttonHeight = 30f * scale;
        var buttonY = max.Y - 16f * scale - buttonHeight;
        var button = new Rect(new Vector2(min.X + 24f * scale, buttonY),
            new Vector2(max.X - 24f * scale, buttonY + buttonHeight));
        if (HousingChrome.PillButton(button, actionLabel, true, ui, false))
        {
            onAction();
        }
    }

    private void DrawFirstUseHint(Rect viewport, List<HousingPlot> plots, float scale)
    {
        if (configuration.HousingMapHintDismissed || plots.Count == 0 || sheetOpen)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var width = MathF.Min(viewport.Width - 60f * scale, 280f * scale);
        var text = Loc.T(L.Housing.MapHint);
        var textSize = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width - 26f * scale);
        var dismissHeight = 24f * scale;
        var height = textSize.Y + 24f * scale + dismissHeight;
        var min = new Vector2(viewport.Center.X - width * 0.5f, viewport.Max.Y - height - 14f * scale);
        var max = new Vector2(min.X + width, min.Y + height);
        var rounding = Metrics.Radius.Md * scale;
        Elevation.Card(drawList, min, max, rounding, scale);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(new Vector4(0.10f, 0.13f, 0.12f, 0.96f)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingBrass, 0.34f)), Metrics.Stroke.Hairline);
        Typography.DrawWrappedLeft(new Vector2(min.X + 13f * scale, min.Y + 11f * scale), text, ui.BodyInk,
            TextStyles.Footnote, width - 26f * scale);
        var dismissWidth = HousingChrome.MeasurePill(Loc.T(L.Housing.GotIt), dismissHeight);
        var dismiss = new Rect(new Vector2(max.X - 13f * scale - dismissWidth, max.Y - 11f * scale - dismissHeight),
            new Vector2(max.X - 13f * scale, max.Y - 11f * scale));
        if (HousingChrome.PillButton(dismiss, Loc.T(L.Housing.GotIt), false, ui, false))
        {
            configuration.HousingMapHintDismissed = true;
            configuration.Save();
        }
    }

    private void DrawLegend(Rect viewport, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var entries = LegendEntries;
        var rowHeight = 20f * scale;
        var width = 178f * scale;
        var height = entries.Length * rowHeight + 18f * scale;
        var min = new Vector2(viewport.Min.X + 14f * scale, viewport.Min.Y + 50f * scale);
        var max = new Vector2(min.X + width, min.Y + height);
        var rounding = Metrics.Radius.Md * scale;
        PopoverSurface.Draw(drawList, min, max, rounding, frameTheme, scale);
        for (var index = 0; index < entries.Length; index++)
        {
            var rowCenterY = min.Y + 9f * scale + rowHeight * (index + 0.5f);
            var swatchCenter = new Vector2(min.X + 20f * scale, rowCenterY);
            DrawLegendSwatch(drawList, swatchCenter, index, scale);
            Typography.Draw(drawList,
                new Vector2(min.X + 38f * scale, rowCenterY - Typography.LineHeight(TextStyles.Caption1) * 0.5f),
                Loc.T(entries[index]), ui.BodyInk, TextStyles.Caption1);
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
                HousingMarkers.DrawSwatch(drawList, center, HousingPlotSize.Small, ui.MutedInk, scale);
                HousingGlyphs.WatchNotch(drawList, center, 5f * scale, ImGui.GetColorU32(AppPalettes.HousingBrass));
                break;
            case 4:
                HousingGlyphs.DashedRing(drawList, center, 7f * scale,
                    ImGui.GetColorU32(AppPalettes.HousingParchment), 1.4f * scale);
                break;
            default:
                drawList.AddCircle(center, 7.5f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 24,
                    1.8f * scale);
                break;
        }
    }

    private static readonly LocString[] LegendEntries =
    {
        L.Housing.LegendSmall, L.Housing.LegendMedium, L.Housing.LegendLarge, L.Housing.LegendWatched,
        L.Housing.LegendStale, L.Housing.LegendSelected,
    };

    private void DrawFooter(Rect footer, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        drawList.AddLine(new Vector2(footer.Min.X + pad, footer.Min.Y), new Vector2(footer.Max.X - pad, footer.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingBrass, 0.18f)), 1f * scale);
        var refreshRadius = 13f * scale;
        var refreshCenter = new Vector2(footer.Max.X - pad - refreshRadius, footer.Center.Y);
        var filterLabel = housing.Filters.ActiveCount > 0
            ? Loc.T(L.Housing.FiltersCount, housing.Filters.ActiveCount)
            : Loc.T(L.Housing.Filters);
        var filterHeight = 25f * scale;
        var filterWidth = HousingChrome.MeasurePill(filterLabel, filterHeight);
        var filterRect = new Rect(
            new Vector2(refreshCenter.X - refreshRadius - 8f * scale - filterWidth,
                footer.Center.Y - filterHeight * 0.5f),
            new Vector2(refreshCenter.X - refreshRadius - 8f * scale, footer.Center.Y + filterHeight * 0.5f));
        var statusText = FooterStatusText();
        var freshness = FooterFreshness();
        var chipLabel = HousingFormat.FreshnessLabel(freshness);
        var chipWidth = HousingChrome.MeasureChip(chipLabel);
        var chipTop = footer.Center.Y - HousingChrome.ChipHeight * scale * 0.5f;
        HousingChrome.Chip(drawList, new Vector2(footer.Min.X + pad, chipTop), chipLabel,
            HousingChrome.FreshnessHue(freshness, ui.Accent), false);
        var textLeft = footer.Min.X + pad + chipWidth + 7f * scale;
        var textMax = MathF.Max(1f, filterRect.Min.X - 8f * scale - textLeft);
        Typography.Draw(drawList,
            new Vector2(textLeft, footer.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(statusText, textMax, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        if (HousingChrome.PillButton(filterRect, filterLabel, housing.Filters.ActiveCount > 0, ui, false))
        {
            ShowOverlay(filtersOpen ? HousingOverlay.None : HousingOverlay.Filters);
        }

        var busy = housing.IsRefreshing || refreshFeedback;
        if (HousingChrome.RefreshButton(refreshCenter, refreshRadius, ui, busy, Loc.T(L.Housing.Refresh),
                false))
        {
            RequestRefresh();
        }
    }

    private string FooterStatusText()
    {
        if (housing.IsRefreshing || refreshFeedback)
        {
            return Loc.T(L.Housing.Updating);
        }

        if (housing.Snapshot is not { } snapshot)
        {
            return Loc.T(L.Housing.Offline);
        }

        return Loc.T(L.Housing.UpdatedAgo, HousingFormat.ScanAgeShort(snapshot.FetchedUtc, DateTime.UtcNow));
    }

    private HousingDataFreshness FooterFreshness()
    {
        if (housing.Snapshot is not { } snapshot)
        {
            return HousingDataFreshness.Unknown;
        }

        return housing.Thresholds.Classify(snapshot.FetchedUtc, DateTime.UtcNow, snapshot.Source);
    }
}
