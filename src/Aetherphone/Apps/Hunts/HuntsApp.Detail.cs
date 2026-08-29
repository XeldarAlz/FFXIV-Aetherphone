using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float MapMaxSize = 260f;
    private const float MapDotRadius = 3.6f;
    private const float MapDotRingRadius = 5.6f;
    private const float WindowCardAlpha = 0.82f;
    private const float WindowCardTextInset = 12f;
    private const float LoreHeaderAlpha = WindowCardAlpha;
    private const float LoreHeaderHoverMix = 0.08f;
    private const float LoreBodyAlpha = 0.35f;
    private const float LoreBodyPadding = 6f;
    private const string AetherytePoiType = "aetheryte";
    private const uint AetheryteMapIconId = 60453;

    private const float MapAetheryteIconSize = 20f;

    private static readonly TimeSpan PendingFlagTimeout = TimeSpan.FromSeconds(90d);
    private static readonly TimeSpan WorldHopRetryDelay = TimeSpan.FromSeconds(1d);
    private static readonly TimeSpan FlagRetryDelay = TimeSpan.FromSeconds(1d);
    private const int FlagRetryAttempts = 3;

    private bool detailSpawnExpanded = true;
    private bool detailDescExpanded = true;
    private bool detailTipsExpanded = true;
    private bool detailRewardsExpanded = true;
    private bool detailTimingExpanded = true;
    private string detailMobId = string.Empty;
    private string detailMapZoneId = string.Empty;
    private readonly List<HuntPoiEntry> detailMapPoints = new();
    private readonly List<HuntPoiEntry> detailMapAetherytePoints = new();
    private readonly Dictionary<(uint TerritoryId, string ZoneId, string Language), string> zoneLabelCache = new();
    private readonly PhotoZoomView detailMapZoom = new();
    private bool detailMapHovered;

    private (int WindowNum, int PhaseNum)? detailMapActivePhase;
    private string? detailMapConfirmedZoneId;
    private bool detailMapFinalPhase;
    private bool detailMapZoneConfirmed;

    private uint pendingFlagWorldId;
    private uint pendingFlagTerritoryId;
    private uint pendingFlagMapId;
    private float pendingFlagMapX;
    private float pendingFlagMapY;

    private uint pendingWorldHopWorldId;
    private uint pendingWorldHopTerritoryId;
    private uint pendingWorldHopMapId;
    private (float X, float Y)? pendingWorldHopFlagCoordinate;

    private uint pendingInstanceWorldId;
    private uint pendingInstanceTerritoryId;
    private int pendingInstanceTarget;

    private void OpenDetail(HuntWindowDto window) =>
        OpenDetailFor(window.MobId, window.WorldId, window.ZoneInstance);

    private void OpenDetailFor(string mobId, string worldId, int zoneInstance)
    {
        if (!string.Equals(detailMobId, mobId, StringComparison.Ordinal))
        {
            detailSpawnExpanded = true;
            detailDescExpanded = true;
            detailTipsExpanded = true;
            detailRewardsExpanded = true;
            detailTimingExpanded = true;
            detailMapActivePhase = hunts.PhaseFor(mobId, worldId, zoneInstance);
            detailMapConfirmedZoneId = hunts.ZoneIdFor(mobId, worldId, zoneInstance);
            ResolveDetailMap(mobCatalog.Find(mobId), detailMapActivePhase, detailMapConfirmedZoneId);
            detailMapZoom.Reset();
            detailMapHovered = false;
        }

        detailMobId = mobId;
        router.Push(new HuntsView(HuntsRoute.Detail, mobId, worldId, zoneInstance));
    }

    private void ResolveDetailMap(HuntMobDefinition? mob, (int WindowNum, int PhaseNum)? activePhase,
        string? confirmedZoneId)
    {
        detailMapZoneId = string.Empty;
        detailMapPoints.Clear();
        detailMapAetherytePoints.Clear();
        detailMapFinalPhase = false;
        detailMapZoneConfirmed = false;

        if (mob is null || mob.ZoneIds.Length == 0)
        {
            return;
        }

        var poiIds = new HashSet<int>();
        if (activePhase is { } phase && mob.Windows.Length > 0)
        {
            var windowIndex = Math.Clamp(phase.WindowNum - 1, 0, mob.Windows.Length - 1);
            var phases = mob.Windows[windowIndex].Phases;
            if (phases.Length > 0)
            {
                var phaseIndex = Math.Clamp(phase.PhaseNum - 1, 0, phases.Length - 1);
                detailMapFinalPhase = phaseIndex > 0;
                var zonePoiIds = phases[phaseIndex].ZonePoiIds;
                for (var poiIndex = 0; poiIndex < zonePoiIds.Length; poiIndex++)
                {
                    poiIds.Add(zonePoiIds[poiIndex]);
                }
            }
        }
        else
        {
            for (var windowIndex = 0; windowIndex < mob.Windows.Length; windowIndex++)
            {
                var phases = mob.Windows[windowIndex].Phases;
                for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
                {
                    var zonePoiIds = phases[phaseIndex].ZonePoiIds;
                    for (var poiIndex = 0; poiIndex < zonePoiIds.Length; poiIndex++)
                    {
                        poiIds.Add(zonePoiIds[poiIndex]);
                    }
                }
            }
        }

        if (confirmedZoneId is { Length: > 0 } && Array.IndexOf(mob.ZoneIds, confirmedZoneId) >= 0)
        {
            detailMapZoneId = confirmedZoneId;
            detailMapZoneConfirmed = true;
            foreach (var poiId in poiIds)
            {
                var found = zoneCatalog.FindPoi(poiId);
                if (found is { } resolved && string.Equals(resolved.ZoneId, confirmedZoneId, StringComparison.Ordinal))
                {
                    detailMapPoints.Add(resolved.Poi);
                }
            }

            PopulateAetherytePoints(confirmedZoneId);
            return;
        }

        var bestZoneId = string.Empty;
        var bestPoints = new List<HuntPoiEntry>();
        for (var zoneIndex = 0; zoneIndex < mob.ZoneIds.Length; zoneIndex++)
        {
            var candidateZoneId = mob.ZoneIds[zoneIndex];
            var candidatePoints = new List<HuntPoiEntry>();
            foreach (var poiId in poiIds)
            {
                var found = zoneCatalog.FindPoi(poiId);
                if (found is { } resolved && string.Equals(resolved.ZoneId, candidateZoneId, StringComparison.Ordinal))
                {
                    candidatePoints.Add(resolved.Poi);
                }
            }

            if (candidatePoints.Count > bestPoints.Count)
            {
                bestZoneId = candidateZoneId;
                bestPoints = candidatePoints;
            }
        }

        detailMapZoneId = bestZoneId;
        detailMapPoints.AddRange(bestPoints);
        PopulateAetherytePoints(bestZoneId);
    }

    private void PopulateAetherytePoints(string zoneId)
    {
        if (zoneId.Length == 0)
        {
            return;
        }

        var zone = zoneCatalog.FindZone(zoneId);
        if (zone is null)
        {
            return;
        }

        var pois = zone.Pois;
        for (var poiIndex = 0; poiIndex < pois.Length; poiIndex++)
        {
            if (string.Equals(pois[poiIndex].Type, AetherytePoiType, StringComparison.Ordinal))
            {
                detailMapAetherytePoints.Add(pois[poiIndex]);
            }
        }
    }

    private void CloseDetail()
    {
        router.Pop();
        menu.Close();
    }

    private void DrawDetailHeader(Rect content, float scale, string mobId, string worldId)
    {
        var def = mobCatalog.Find(mobId);
        var title = ResolveMobLabel(def, mobId);
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;

        var sideReserve = 44f * scale;
        var titleStyle = new TextStyle(1.15f, FontWeight.SemiBold);
        var titleTop = rowCenterY - Typography.Measure(title, titleStyle).Y * 0.5f;
        var availableWidth = content.Width - sideReserve * 2f;

        var rankLabel = def?.Rank ?? string.Empty;
        var rankReserve = rankLabel.Length > 0 ? InlineBadge.Width(rankLabel, scale) + 6f * scale : 0f;
        var nameMaxWidth = availableWidth - rankReserve;
        var titleWidth = MathF.Min(Typography.Measure(title, titleStyle).X, nameMaxWidth);
        var groupLeft = content.Center.X - (rankReserve + titleWidth) * 0.5f;

        if (rankLabel.Length > 0)
        {
            InlineBadge.Draw(ImGui.GetWindowDrawList(), groupLeft, rowCenterY, rankLabel, RankBadgeColor(rankLabel),
                scale);
        }

        Marquee.DrawLeftAuto("hunts.detail.header", title, groupLeft + rankReserve, titleTop, nameMaxWidth,
            titleStyle, ui.TitleInk);

        if (AppHeader.DrawBack(content, scale, "hunts.detail.back", ui.Accent))
        {
            CloseDetail();
        }

        DrawDetailNotificationButton(content, scale, mobId, worldId, rowCenterY);
    }

    private void DrawDetailBody(Rect body, float scale, HuntsView view)
    {
        using (AppSurface.Begin(body, disableMouseWheelScroll: detailMapHovered))
        {
            Gap(8f);

            var def = mobCatalog.Find(view.MobId);
            if (FindWindow(view) is { } window)
            {
                DrawDetailWindowBar(window, def, DateTimeOffset.UtcNow, scale);
                Gap(20f);
            }

            var livePhase = hunts.PhaseFor(view.MobId, view.WorldId, view.ZoneInstance);
            var liveZoneId = hunts.ZoneIdFor(view.MobId, view.WorldId, view.ZoneInstance);
            if (livePhase is not null &&
                (livePhase != detailMapActivePhase || liveZoneId != detailMapConfirmedZoneId))
            {
                detailMapActivePhase = livePhase;
                detailMapConfirmedZoneId = liveZoneId;
                ResolveDetailMap(def, livePhase, liveZoneId);
            }

            var spawned = hunts.IsSpawned(view.MobId, view.WorldId, view.ZoneInstance);
            var confirmedPoiId = spawned ? ResolveConfirmedPoiId(view) : null;
            if (DrawDetailZoneMap(scale, confirmedPoiId, view))
            {
                Gap(20f);
            }

            if (spawned && DrawNavigateButton(view, scale, confirmedPoiId))
            {
                Gap(20f);
            }

            var spawnLore = HuntMobLore.SpawnFor(view.MobId);
            var spawnText = spawnLore is { } spawn ? Loc.T(spawn) : Loc.T(L.Hunts.NoSpecialSpawnCondition);

            DrawLoreSection(Loc.T(L.Hunts.SpawnConditionSection), spawnText, null, ref detailSpawnExpanded, scale);
            Gap(12f);

            var rawTipText = HuntMobLore.TipFor(view.MobId);
            if (rawTipText is not null)
            {
                var tipNote = HuntMobLore.TipIsFallback(view.MobId) ? Loc.T(L.Hunts.LoreNotAvailableInLanguage) : null;
                DrawLoreSection(Loc.T(L.Hunts.TipsSection), rawTipText, tipNote, ref detailTipsExpanded, scale);
                Gap(12f);
            }

            var rewards = rewardCatalog.RewardsFor(view.MobId);
            if (rewards.Count > 0)
            {
                DrawRewardsSection(rewards, ref detailRewardsExpanded, scale);
                Gap(12f);
            }

            if (DrawSpawnInfoSection(FindWindow(view), def, ref detailTimingExpanded, scale))
            {
                Gap(12f);
            }

            var rawDescText = HuntMobLore.DescriptionFor(view.MobId);
            var descText = rawDescText ?? Loc.T(L.Hunts.NoLoreAvailable);

            var descNote = rawDescText is not null && HuntMobLore.DescriptionIsFallback(view.MobId)
                ? Loc.T(L.Hunts.LoreNotAvailableInLanguage)
                : null;

            DrawLoreSection(Loc.T(L.Hunts.DescriptionSection), descText, descNote, ref detailDescExpanded, scale);
            Gap(24f);
        }
    }

    private HuntWindowDto? FindWindow(HuntsView view)
    {
        var windows = hunts.Windows;
        for (var index = 0; index < windows.Length; index++)
        {
            var candidate = windows[index];
            if (string.Equals(candidate.MobId, view.MobId, StringComparison.Ordinal) &&
                string.Equals(candidate.WorldId, view.WorldId, StringComparison.OrdinalIgnoreCase) &&
                candidate.ZoneInstance == view.ZoneInstance)
            {
                return candidate;
            }
        }

        return null;
    }

    private int? ResolveConfirmedPoiId(HuntsView view)
    {
        if (hunts.ConfirmedPoiIdFor(view.MobId, view.WorldId, view.ZoneInstance) is { } reported)
        {
            return reported;
        }

        return detailMapFinalPhase && detailMapZoneConfirmed && detailMapPoints.Count == 1
            ? detailMapPoints[0].Id
            : null;
    }

    private void DrawDetailWindowBar(HuntWindowDto window, HuntMobDefinition? def, DateTimeOffset now, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        var panelWidth = MathF.Min(width, MapMaxSize * scale);
        var panelMargin = (width - panelWidth) * 0.5f;
        var panelLeft = origin.X + panelMargin;
        var panelRight = origin.X + width - panelMargin;

        var inset = WindowCardTextInset * scale;
        var contentLeft = panelLeft + inset;
        var contentRight = panelRight - inset;

        var worldLabel = Prettify(window.WorldId);
        var showsInstanceBadge = def is not null && window.ZoneInstance > 0 && hunts.ZoneInstanceCountFor(def) > 1;
        var status = ResolveDisplayStatus(window, def, now);
        var statusLabel = ComposeStatusLabel(status, window.MobId, window.WorldId, window.ZoneInstance, def);
        var statusInk = StatusColor(status);

        var headerHeight = MathF.Max(Typography.LineHeight(TextStyles.Subheadline),
            Typography.LineHeight(TextStyles.Title3));
        var barTop = origin.Y + headerHeight + 10f * scale;
        var barHeight = 8f * scale;
        var percentage = HuntWindowMath.Percentage(window, def, now);

        var detailLabel = ResolveDetailLabel(status, window, def, now);
        var reporterLabel = ResolveReporterLabel(window);

        var bottom = barTop + barHeight;
        if (detailLabel.Length > 0)
        {
            bottom = bottom + 6f * scale + Typography.LineHeight(TextStyles.Footnote);
        }

        if (reporterLabel.Length > 0)
        {
            bottom = bottom + 6f * scale + Typography.LineHeight(TextStyles.Footnote);
        }

        var plateMin = new Vector2(panelLeft, origin.Y - 8f * scale);
        var plateMax = new Vector2(panelRight, bottom + 8f * scale);
        var radius = Metrics.Radius.Card * scale;
        var shadowOffset = new Vector2(0f, 2f * scale);
        drawList.AddRectFilled(plateMin + shadowOffset, plateMax + shadowOffset,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.24f)), radius);
        Squircle.Fill(drawList, plateMin, plateMax, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropBottom, WindowCardAlpha)));
        Squircle.Stroke(drawList, plateMin, plateMax, radius, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);

        Typography.Draw(drawList, new Vector2(contentLeft, origin.Y), worldLabel, ui.MutedInk, TextStyles.Subheadline);

        if (showsInstanceBadge)
        {
            var worldLabelWidth = Typography.Measure(worldLabel, TextStyles.Subheadline).X;
            var worldLabelCenterY = origin.Y + Typography.LineHeight(TextStyles.Subheadline) * 0.5f;
            var badgeLeft = contentLeft + worldLabelWidth + 6f * scale;
            InlineBadge.Draw(drawList, badgeLeft, worldLabelCenterY, window.ZoneInstance.ToString(Loc.Culture),
                ui.MutedInk, scale, InstanceBadgeSize * scale);
        }

        var statusSize = Typography.Measure(statusLabel, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(contentRight - statusSize.X, origin.Y), statusLabel, statusInk,
            TextStyles.Title3);

        DrawBigProgressBar(drawList, contentLeft, contentRight, barTop, barHeight, status, percentage ?? 0d, scale);

        var lineTop = barTop + barHeight;
        if (detailLabel.Length > 0)
        {
            lineTop = lineTop + 6f * scale;
            Typography.Draw(drawList, new Vector2(contentLeft, lineTop), detailLabel, ui.MutedInk,
                TextStyles.Footnote);
            lineTop = lineTop + Typography.LineHeight(TextStyles.Footnote);
        }

        if (reporterLabel.Length > 0)
        {
            lineTop = lineTop + 6f * scale;
            Typography.Draw(drawList, new Vector2(contentLeft, lineTop), reporterLabel, ui.MutedInk,
                TextStyles.Footnote);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, plateMax.Y - origin.Y));
    }

    private string ResolveReporterLabel(HuntWindowDto window)
    {
        var reporterNames = hunts.ReporterNamesFor(window.MobId, window.WorldId, window.ZoneInstance);
        return reporterNames is { Length: > 0 }
            ? Loc.T(L.Hunts.SpawnInfoLineFormat, Loc.T(L.Hunts.ReportedByLabel), string.Join(", ", reporterNames))
            : string.Empty;
    }

    private void DrawBigProgressBar(ImDrawListPtr drawList, float left, float right, float top, float height,
        HuntWindowStatus status, double percentage, float scale)
    {
        ProgressBar.Draw(drawList, left, right, top, height, percentage, StatusColor(status), TextStyles.Headline,
            8f * scale);
    }

    private bool DrawDetailZoneMap(float scale, int? confirmedPoiId, HuntsView view)
    {
        if (detailMapPoints.Count == 0 && detailMapAetherytePoints.Count == 0)
        {
            detailMapHovered = false;
            return false;
        }

        var zone = zoneCatalog.FindZone(detailMapZoneId);
        var territoryId = zoneCatalog.ResolveTerritoryId(detailMapZoneId);
        var texture = zoneMapTextures.Resolve(territoryId);
        if (zone is null || texture is null)
        {
            detailMapHovered = false;
            return false;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var size = MathF.Min(width, MapMaxSize * scale);
        var mapLeft = origin.X + (width - size) * 0.5f;

        var drawList = ImGui.GetWindowDrawList();
        var zoneLabel = ResolveZoneLabel(zone.Id, territoryId);
        var labelSize = Typography.Measure(zoneLabel, TextStyles.Footnote);
        var labelGap = 6f * scale;
        Typography.Draw(drawList, new Vector2(mapLeft + (size - labelSize.X) * 0.5f, origin.Y), zoneLabel,
            ui.MutedInk, TextStyles.Footnote);

        var mapTop = origin.Y + labelSize.Y + labelGap;
        var stage = new Rect(new Vector2(mapLeft, mapTop), new Vector2(mapLeft + size, mapTop + size));

        detailMapHovered = ImGui.IsMouseHoveringRect(stage.Min, stage.Max);
        ImGui.SetCursorScreenPos(stage.Min);
        using (var mapChild = ImRaii.Child("##huntsDetailMap", stage.Size, false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                   ImGuiWindowFlags.NoBackground))
        {
            if (mapChild)
            {
                DrawDetailZoneMapContent(stage, texture, scale, confirmedPoiId, view, territoryId);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, labelSize.Y + labelGap + size));
        return true;
    }

    private string ResolveZoneLabel(string zoneId, uint territoryId)
    {
        var key = (territoryId, zoneId, HuntUiLanguage.Key());
        if (zoneLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var label = ResolveLiveZoneName(territoryId) is { Length: > 0 } name ? name : Prettify(zoneId);
        zoneLabelCache[key] = label;
        return label;
    }

    private static string? ResolveLiveZoneName(uint territoryId) =>
        territoryId != 0 && Plugin.DataManager.GetExcelSheet<TerritoryType>(HuntUiLanguage.SheetLanguage())
            .TryGetRow(territoryId, out var territory) && territory.PlaceName.RowId != 0
            ? territory.PlaceName.Value.Name.ExtractText()
            : null;

    private void DrawDetailZoneMapContent(Rect stage, IDalamudTextureWrap texture, float scale, int? confirmedPoiId,
        HuntsView view, uint territoryId)
    {
        var drawList = ImGui.GetWindowDrawList();
        detailMapZoom.Draw(stage, texture, frameTheme, Metrics.Radius.Card * scale, showButtons: false);

        var fit = PhotoZoomView.FitScale(stage, texture.Size);
        var drawnSize = texture.Size * fit * detailMapZoom.Zoom;
        var center = stage.Center + detailMapZoom.Pan;
        var min = center - drawnSize * 0.5f;
        var max = center + drawnSize * 0.5f;

        var confirmedKnown = false;
        if (confirmedPoiId is { } confirmed)
        {
            for (var checkIndex = 0; checkIndex < detailMapPoints.Count; checkIndex++)
            {
                if (detailMapPoints[checkIndex].Id == confirmed)
                {
                    confirmedKnown = true;
                    break;
                }
            }
        }

        var finalLocationResolved = detailMapFinalPhase && detailMapZoneConfirmed && detailMapPoints.Count == 1;

        drawList.PushClipRect(stage.Min, stage.Max, true);
        for (var index = 0; index < detailMapPoints.Count; index++)
        {
            var poi = detailMapPoints[index];
            if (confirmedKnown && poi.Id != confirmedPoiId)
            {
                continue;
            }

            var (rawX, rawY) = poi.ParsedLocation();
            var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
            var dotPosition = new Vector2(min.X + normalizedX * (max.X - min.X),
                min.Y + normalizedY * (max.Y - min.Y));
            DrawSpawnDot(drawList, dotPosition, scale, poi.Id, confirmedKnown, finalLocationResolved);
        }

        var worldId = HuntDataCenterWorlds.WorldRowId(view.WorldId);
        var mapId = ResolveMapId(territoryId);
        for (var index = 0; index < detailMapAetherytePoints.Count; index++)
        {
            var poi = detailMapAetherytePoints[index];
            var (rawX, rawY) = poi.ParsedLocation();
            var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
            var dotPosition = new Vector2(min.X + normalizedX * (max.X - min.X),
                min.Y + normalizedY * (max.Y - min.Y));
            DrawAetheryteDot(drawList, dotPosition, scale, poi, territoryId, worldId, mapId, view.ZoneInstance);
        }

        drawList.PopClipRect();
    }

    private void DrawSpawnDot(ImDrawListPtr drawList, Vector2 center, float scale, int poiId, bool confirmed,
        bool finalLocation)
    {
        var ink = finalLocation ? frameTheme.Danger : confirmed ? OpenBarColor : ui.Accent;
        drawList.AddCircleFilled(center, MapDotRingRadius * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)),
            20);
        drawList.AddCircle(center, MapDotRingRadius * scale, ImGui.GetColorU32(Vector4.One), 20, 1.4f * scale);
        drawList.AddCircleFilled(center, MapDotRadius * scale, ImGui.GetColorU32(ink), 20);

        var hitRadius = MapDotRingRadius * scale + 3f * scale;
        var hitMin = new Vector2(center.X - hitRadius, center.Y - hitRadius);
        var hitMax = new Vector2(center.X + hitRadius, center.Y + hitRadius);
        if (!ImGui.IsMouseHoveringRect(hitMin, hitMax))
        {
            return;
        }

        if (zoneCatalog.ResolveCoordinate(poiId) is not { } coordinate)
        {
            return;
        }

        HoverTooltip.Show(new Rect(hitMin, hitMax), CoordinateText(coordinate.X, coordinate.Y),
            HoverLabelSide.Above);
    }

    private void DrawAetheryteDot(ImDrawListPtr drawList, Vector2 center, float scale, HuntPoiEntry poi,
        uint territoryId, uint worldId, uint mapId, int zoneInstance)
    {
        var iconRadius = MapAetheryteIconSize * 0.5f * scale;
        var iconMin = new Vector2(center.X - iconRadius, center.Y - iconRadius);
        var iconMax = new Vector2(center.X + iconRadius, center.Y + iconRadius);
        GameIconTile.Draw(drawList, Plugin.TextureProvider, AetheryteMapIconId, iconMin, iconMax, 6f * scale, scale);

        var hitRadius = iconRadius + 3f * scale;
        var hitMin = new Vector2(center.X - hitRadius, center.Y - hitRadius);
        var hitMax = new Vector2(center.X + hitRadius, center.Y + hitRadius);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(hitMin, hitMax), ResolvePoiLabel(poi), HoverLabelSide.Above);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered) && territoryId != 0 && worldId != 0)
        {
            NavigateToCoordinate(territoryId, worldId, mapId, zoneCatalog.ResolveCoordinate(poi.Id), zoneInstance);
        }
    }

    private string ResolvePoiLabel(HuntPoiEntry poi) =>
        poi.Name?.GetValueOrDefault(configuration.Language) ?? poi.Name?.GetValueOrDefault("en") ?? string.Empty;

    private bool DrawNavigateButton(HuntsView view, float scale, int? confirmedPoiId)
    {
        var territoryId = zoneCatalog.ResolveTerritoryId(detailMapZoneId);
        if (territoryId == 0)
        {
            return false;
        }

        var worldId = HuntDataCenterWorlds.WorldRowId(view.WorldId);
        if (worldId == 0)
        {
            return false;
        }

        var coordinate = confirmedPoiId is { } poiId ? zoneCatalog.ResolveCoordinate(poiId) : null;
        var destination = TravelPlanner.ResolveNearestAetheryteTo(territoryId, worldId, LocationShare.CurrentWorldId(),
            Plugin.ClientState.TerritoryType, coordinate);
        var alreadyThere = destination.Kind == TravelKind.AlreadyThere;
        var noSpawnLocationYet = alreadyThere && coordinate is null;

        var mapId = ResolveMapId(territoryId);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var panelWidth = MathF.Min(width, MapMaxSize * scale);
        var margin = (width - panelWidth) * 0.5f;
        var height = 38f * scale;
        var rect = new Rect(new Vector2(origin.X + margin, origin.Y),
            new Vector2(origin.X + width - margin, origin.Y + height));

        if (noSpawnLocationYet)
        {
            AppSkin.PillButton(rect, Loc.T(L.Hunts.NoSpawnLocationDetected), true, false, frameTheme);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height));
            return true;
        }

        var label = alreadyThere ? Loc.T(L.Hunts.PlaceFlagOnMap) : Loc.T(L.Hunts.NavigateToLocation);
        if (ui.PillButton(rect, label, true, "hunts.detail.navigate"))
        {
            NavigateToCoordinate(territoryId, worldId, mapId, coordinate, view.ZoneInstance);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return true;
    }

    private void NavigateToCoordinate(uint territoryId, uint worldId, uint mapId, (float X, float Y)? coordinate,
        int zoneInstance)
    {
        ArmPendingInstanceSync(worldId, territoryId, zoneInstance);

        var destination = TravelPlanner.ResolveNearestAetheryteTo(territoryId, worldId, LocationShare.CurrentWorldId(),
            Plugin.ClientState.TerritoryType, coordinate);
        if (destination.Kind == TravelKind.AlreadyThere)
        {
            if (coordinate is { } here)
            {
                DropFlag(territoryId, mapId, here.X, here.Y);
            }

            return;
        }

        TravelToHuntZone(in destination, worldId, territoryId, mapId, coordinate);
    }

    private void TravelToHuntZone(in TravelDestination destination, uint worldId, uint territoryId, uint mapId,
        (float X, float Y)? flagCoordinate)
    {
        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            if (destination.Kind == TravelKind.World)
            {
                ArmPendingWorldHop(worldId, territoryId, mapId, flagCoordinate);
            }
            else if (flagCoordinate is { } coordinate)
            {
                ArmPendingFlag(worldId, territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return;
        }

        HandleTravelFailure(outcome, in destination);
    }

    private void HandleTravelFailure(LifestreamOutcome outcome, in TravelDestination destination)
    {
        if (outcome == LifestreamOutcome.NotInstalled)
        {
            ImGui.SetClipboardText(TravelPlanner.Command(in destination));
            ShellToast.Show();
            return;
        }

        confirm.Alert(null, TravelPlanner.Notice(outcome, in destination), Loc.T(L.Common.Close));
    }

    private static void DropFlag(uint territoryId, uint mapId, float mapX, float mapY) =>
        LocationShare.OpenMap(new SharedLocation(territoryId, mapId, mapX, mapY, 0, 0, 0, 0));

    private static uint ResolveMapId(uint territoryId) =>
        territoryId != 0 && Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory)
            ? territory.Map.RowId
            : 0u;

    private void ArmPendingFlag(uint worldId, uint territoryId, uint mapId, float mapX, float mapY)
    {
        pendingFlagWorldId = worldId;
        pendingFlagTerritoryId = territoryId;
        pendingFlagMapId = mapId;
        pendingFlagMapX = mapX;
        pendingFlagMapY = mapY;
        pendingFlagAction.Arm();
    }

    private bool IsPendingFlagReady() =>
        Plugin.ClientState.TerritoryType == pendingFlagTerritoryId &&
        LocationShare.CurrentWorldId() == pendingFlagWorldId && !PlayerBusy.Now;

    private bool TryDropPendingFlag()
    {
        DropFlag(pendingFlagTerritoryId, pendingFlagMapId, pendingFlagMapX, pendingFlagMapY);
        return false;
    }

    private void ArmPendingWorldHop(uint worldId, uint territoryId, uint mapId, (float X, float Y)? flagCoordinate)
    {
        pendingWorldHopWorldId = worldId;
        pendingWorldHopTerritoryId = territoryId;
        pendingWorldHopMapId = mapId;
        pendingWorldHopFlagCoordinate = flagCoordinate;
        pendingWorldHopAction.Arm();
    }

    private bool IsPendingWorldHopReady() =>
        LocationShare.CurrentWorldId() == pendingWorldHopWorldId && !LifestreamBridge.IsBusy() && !PlayerBusy.Now;

    private bool TryContinuePendingWorldHop() =>
        TryContinueAfterWorldHop(pendingWorldHopTerritoryId, pendingWorldHopWorldId, pendingWorldHopMapId,
            pendingWorldHopFlagCoordinate);

    private bool TryContinueAfterWorldHop(uint territoryId, uint worldId, uint mapId,
        (float X, float Y)? flagCoordinate)
    {
        var destination = TravelPlanner.ResolveNearestAetheryteTo(territoryId, worldId, LocationShare.CurrentWorldId(),
            Plugin.ClientState.TerritoryType, flagCoordinate);
        if (destination.Kind == TravelKind.AlreadyThere)
        {
            if (flagCoordinate is { } coordinate)
            {
                DropFlag(territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return true;
        }

        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            if (flagCoordinate is { } coordinate)
            {
                ArmPendingFlag(worldId, territoryId, mapId, coordinate.X, coordinate.Y);
            }

            return true;
        }

        if (outcome is LifestreamOutcome.Busy or LifestreamOutcome.CannotTeleportNow)
        {
            return false;
        }

        HandleTravelFailure(outcome, in destination);
        return true;
    }

    private void ArmPendingInstanceSync(uint worldId, uint territoryId, int zoneInstance)
    {
        if (zoneInstance <= 0)
        {
            return;
        }

        pendingInstanceWorldId = worldId;
        pendingInstanceTerritoryId = territoryId;
        pendingInstanceTarget = zoneInstance;
        pendingInstanceAction.Arm();
    }

    private bool IsPendingInstanceSyncReady() =>
        Plugin.ClientState.TerritoryType == pendingInstanceTerritoryId &&
        LocationShare.CurrentWorldId() == pendingInstanceWorldId && !PlayerBusy.Now && !LifestreamBridge.IsBusy();

    private bool TryAdvancePendingInstanceSync()
    {
        var currentInstance = LifestreamBridge.GetCurrentInstance();
        if (currentInstance <= 0 || currentInstance == pendingInstanceTarget)
        {
            return true;
        }

        if (!LifestreamBridge.CanChangeInstance())
        {
            return false;
        }

        LifestreamBridge.ChangeInstance(pendingInstanceTarget);
        return true;
    }

    private static string CoordinateText(float x, float y) =>
        string.Create(CultureInfo.InvariantCulture, $"X: {x:0.0}  Y: {y:0.0}");

    private bool DrawSectionHeader(string title, ref bool expanded, float scale, out Vector2 origin, out float width,
        out float headerHeight)
    {
        origin = ImGui.GetCursorScreenPos();
        width = ImGui.GetContentRegionAvail().X;
        headerHeight = 32f * scale;
        var headerMax = new Vector2(origin.X + width, origin.Y + headerHeight);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(origin, headerMax);

        var headerBase = hovered ? Palette.Mix(ui.Palette.BackdropBottom, ui.TitleInk, LoreHeaderHoverMix) : ui.Palette.BackdropBottom;
        Squircle.Fill(drawList, origin, headerMax, Metrics.Radius.Sm * scale,
            ImGui.GetColorU32(Palette.WithAlpha(headerBase, LoreHeaderAlpha)));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var titleY = origin.Y + headerHeight * 0.5f - Typography.LineHeight(TextStyles.BodyEmphasized) * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X + 8f * scale, titleY), title, ui.TitleInk,
            TextStyles.BodyEmphasized);
        var chevron = expanded ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown;
        AppSkin.Icon(drawList, new Vector2(headerMax.X - 14f * scale, origin.Y + headerHeight * 0.5f),
            IconGlyph.Of(chevron), ui.MutedInk, 0.55f);

        if (UiInteract.Click(origin, headerMax, hovered))
        {
            expanded = !expanded;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, headerHeight));
        return expanded;
    }

    private void DrawLoreSection(string title, string text, string? note, ref bool expanded, float scale)
    {
        if (!DrawSectionHeader(title, ref expanded, scale, out var origin, out var width, out var headerHeight))
        {
            return;
        }

        DrawLoreSectionBody(origin, width, headerHeight, text, note, scale);
    }

    private void DrawLoreSectionBody(Vector2 origin, float width, float headerHeight, string text, string? note,
        float scale)
    {
        var bodyLeft = origin.X + 8f * scale;
        var bodyWidth = width - 16f * scale;
        var panelTop = origin.Y + headerHeight + 4f * scale;
        var textTop = panelTop + LoreBodyPadding * scale;

        var noteHeight = 0f;
        if (note is not null)
        {
            noteHeight = Typography.MeasureWrappedBlock(note, TextStyles.FootnoteEmphasized, bodyWidth).Y +
                6f * scale;
        }

        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Body, bodyWidth).Y;
        var panelBottom = textTop + noteHeight + textHeight + LoreBodyPadding * scale;

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, new Vector2(origin.X, panelTop), new Vector2(origin.X + width, panelBottom),
            Metrics.Radius.Sm * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropBottom, LoreBodyAlpha)));

        var lineTop = textTop;
        if (note is not null)
        {
            var drawnNoteHeight = Typography.DrawWrappedLeft(new Vector2(bodyLeft, lineTop), note, ui.MutedInk,
                TextStyles.FootnoteEmphasized, bodyWidth);
            lineTop += drawnNoteHeight + 6f * scale;
        }

        Typography.DrawWrappedLeft(new Vector2(bodyLeft, lineTop), text, ui.BodyInk, TextStyles.Body, bodyWidth);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, panelBottom - origin.Y));
    }

    private void DrawRewardsSection(IReadOnlyList<HuntMobRewardEntry> rewards, ref bool expanded, float scale)
    {
        if (!DrawSectionHeader(Loc.T(L.Hunts.RewardsSection), ref expanded, scale, out var origin, out var width,
                out var headerHeight))
        {
            return;
        }

        var bodyLeft = origin.X + 8f * scale;
        var bodyWidth = width - 16f * scale;
        var panelTop = origin.Y + headerHeight + 4f * scale;

        var iconSize = 40f * scale;
        var tileGap = 16f * scale;
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var tileHeight = iconSize + 4f * scale + captionHeight;
        var columns = Math.Max(1, (int)((bodyWidth + tileGap) / (iconSize + tileGap)));
        var rows = (rewards.Count + columns - 1) / columns;
        var tileTop = panelTop + LoreBodyPadding * scale;
        var panelBottom = tileTop + rows * tileHeight + (rows - 1) * tileGap + LoreBodyPadding * scale;

        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, new Vector2(origin.X, panelTop), new Vector2(origin.X + width, panelBottom),
            Metrics.Radius.Sm * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.BackdropBottom, LoreBodyAlpha)));

        var displayLocale = HuntUiLanguage.Key();
        var searchLocale = HuntClientLanguage.Key();
        for (var index = 0; index < rewards.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var tileMin = new Vector2(bodyLeft + column * (iconSize + tileGap), tileTop + row * (tileHeight + tileGap));
            DrawRewardTile(drawList, tileMin, iconSize, captionHeight, rewards[index], displayLocale, searchLocale,
                scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, panelBottom - origin.Y));
    }

    private bool DrawSpawnInfoSection(HuntWindowDto? window, HuntMobDefinition? def, ref bool expanded, float scale)
    {
        if (def is not { Windows.Length: > 0 })
        {
            return false;
        }

        var windowIndex = window is { } activeWindow ? Math.Clamp(activeWindow.Num - 1, 0, def.Windows.Length - 1) : 0;
        var timing = def.Windows[windowIndex].Timing;
        if (timing?.Normal is not { } normal)
        {
            return false;
        }

        if (!DrawSectionHeader(Loc.T(L.Hunts.SpawnInfoSection), ref expanded, scale, out var origin, out var width,
                out var headerHeight))
        {
            return true;
        }

        var text = BuildSpawnInfoText(normal, timing.Maintenance);
        DrawLoreSectionBody(origin, width, headerHeight, text, null, scale);
        return true;
    }

    private static string BuildSpawnInfoText(HuntMobTimingWindow normal, HuntMobTimingWindow? maintenance)
    {
        var lines = new List<string> { SpawnInfoLine(L.Hunts.SpawnInfoMinimum, normal.Min) };
        if (normal.Avg is { } avg)
        {
            lines.Add(SpawnInfoLine(L.Hunts.SpawnInfoAverage, avg));
        }

        if (normal.Cap is { } cap)
        {
            lines.Add(SpawnInfoLine(L.Hunts.SpawnInfoMaximum, cap));
        }

        if (maintenance is { } maint)
        {
            lines.Add(string.Empty);
            lines.Add(Loc.T(L.Hunts.SpawnInfoMaintenance));
            lines.Add(SpawnInfoLine(L.Hunts.SpawnInfoMinimum, maint.Min));
            if (maint.Avg is { } maintAvg)
            {
                lines.Add(SpawnInfoLine(L.Hunts.SpawnInfoAverage, maintAvg));
            }

            if (maint.Cap is { } maintCap)
            {
                lines.Add(SpawnInfoLine(L.Hunts.SpawnInfoMaximum, maintCap));
            }
        }

        return string.Join('\n', lines);
    }

    private static string SpawnInfoLine(LocString label, double hours)
    {
        var formattedHours = Loc.T(L.Time.HoursShort, hours.ToString("0.#", Loc.Culture));
        return Loc.T(L.Hunts.SpawnInfoLineFormat, Loc.T(label), formattedHours);
    }

    private void DrawRewardTile(ImDrawListPtr drawList, Vector2 iconMin, float iconSize, float captionHeight,
        HuntMobRewardEntry entry, string displayLocale, string searchLocale, float scale)
    {
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        var radius = 9f * scale;
        var searchName = rewardCatalog.ItemNameFor(entry.ItemId, searchLocale);
        var iconId = HuntRewardIcons.ResolveIconId(entry.ItemId, searchName);
        GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId, iconMin, iconMax, radius, scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), edgeStroke: true);

        if (entry.Amount is { } amount)
        {
            var captionText = "x" + amount.ToString(Loc.Culture);
            var captionSize = Typography.Measure(captionText, TextStyles.FootnoteEmphasized);
            var captionPosition = new Vector2(iconMin.X + (iconSize - captionSize.X) * 0.5f, iconMax.Y + 4f * scale);
            Typography.Draw(drawList, captionPosition, captionText, ui.TitleInk, TextStyles.FootnoteEmphasized);
        }

        var displayName = rewardCatalog.ItemNameFor(entry.ItemId, displayLocale);
        if (displayName is not null)
        {
            var tileMax = new Vector2(iconMax.X, iconMax.Y + 4f * scale + captionHeight);
            HoverTooltip.Show(new Rect(iconMin, tileMax), displayName);
        }
    }

    private void DrawDetailNotificationButton(Rect content, float scale, string mobId, string worldId,
        float rowCenterY)
    {
        var settings = hunts.NotificationSettings;
        var mode = settings.MobOverrideModeFor(mobId);
        var center = new Vector2(content.Max.X - 22f * scale, rowCenterY);
        if (ui.IconButton(center, 16f * scale, IconGlyph.Of(NotificationIcon(mode)), NotificationTint(mode),
                new Vector4(0f, 0f, 0f, 0f), 1.2f, MarkNotificationValueText(mobId, worldId, mode),
                HoverLabelSide.Below))
        {
            settings.SetMobOverride(mobId, NextNotificationMode(mode), worldId);
            hunts.SaveNotificationSettings();
        }
    }

    private static HuntMobNotificationMode NextNotificationMode(HuntMobNotificationMode mode) => mode switch
    {
        HuntMobNotificationMode.Default => HuntMobNotificationMode.Enabled,
        HuntMobNotificationMode.Enabled => HuntMobNotificationMode.EnabledOnWorld,
        HuntMobNotificationMode.EnabledOnWorld => HuntMobNotificationMode.Disabled,
        _ => HuntMobNotificationMode.Default,
    };

    private static FontAwesomeIcon NotificationIcon(HuntMobNotificationMode mode) =>
        mode == HuntMobNotificationMode.Disabled ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell;

    private Vector4 NotificationTint(HuntMobNotificationMode mode) => mode switch
    {
        HuntMobNotificationMode.Disabled => frameTheme.Danger,
        HuntMobNotificationMode.Default => ui.MutedInk,
        _ => ui.Accent,
    };

    private string MarkNotificationValueText(string mobId, string worldId, HuntMobNotificationMode mode) => mode switch
    {
        HuntMobNotificationMode.Enabled => Loc.T(L.Hunts.NotifyModeEnabled),
        HuntMobNotificationMode.Disabled => Loc.T(L.Hunts.NotifyModeDisabled),
        HuntMobNotificationMode.EnabledOnWorld => Loc.T(L.Hunts.NotifyModeEnabledOnWorldValue,
            Prettify(hunts.NotificationSettings.MobOverrideWorldFor(mobId) ?? worldId)),
        _ => Loc.T(L.Hunts.NotifyModeDefault),
    };
}
