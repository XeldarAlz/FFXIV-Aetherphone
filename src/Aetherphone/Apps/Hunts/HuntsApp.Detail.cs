using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float MapMaxSize = 320f;
    private const float MapDotRadius = 3.6f;
    private const float MapDotRingRadius = 5.6f;
    private const float MapAetheryteIconSize = 20f;
    private const float MapLegendHeight = 22f;
    private const string AetherytePoiType = "aetheryte";
    private const uint AetheryteMapIconId = 60453;
    private const float DetailBarHeight = 8f;
    private const float DetailActionHeight = 40f;
    private const float WorldRowHeight = 52f;
    private const float WorldRingSize = 30f;
    private const float WorldRingThickness = 3.5f;
    private const float RewardIconSize = 40f;
    private const float RewardGap = 14f;
    private const float AlertStripHeight = 32f;
    private const float TimingColumnGap = 8f;
    private const int AlertModeCount = 4;
    private const int LoreCollapsedLines = 4;

    private readonly HuntRow detailRow = new();
    private readonly List<HuntRow> otherWorldRows = new();
    private readonly List<HuntRow> otherWorldPool = new();
    private readonly List<HuntPoiEntry> detailMapAetherytePoints = new();
    private readonly PhotoZoomView detailMapZoom = new();
    private readonly string[] alertOptions = new string[AlertModeCount];
    private readonly Dictionary<int, string> rewardCaptions = new();
    private readonly Dictionary<double, string> hoursLabels = new();
    private object? hoursLabelCulture;
    private readonly Dictionary<int, string> coordinateLabels = new();
    private float timelineLabelRight;
    private string detailKey = string.Empty;
    private string detailKeyWorld = string.Empty;
    private int detailKeyInstance;
    private object? detailCulture;
    private string detailStatusLine = string.Empty;
    private HuntMobNotificationMode alertHintMode;
    private string alertHintWorld = string.Empty;
    private bool alertHintSignedIn;
    private object? alertHintCulture;
    private string alertHint = string.Empty;
    private object? rewardCaptionCulture;
    private string detailMobId = string.Empty;
    private string detailMapZoneId = string.Empty;
    private string detailOpensLabel = string.Empty;
    private string detailCapLabel = string.Empty;
    private string detailCopyText = string.Empty;
    private int detailSpawnVersion = -1;
    private DateTimeOffset detailRefreshAt;
    private bool detailHasWindow;
    private bool detailMapHovered;
    private bool detailMapPendingFocus;
    private bool detailLoreExpanded;
    private (int WindowNum, int PhaseNum)? detailMapActivePhase;
    private string? detailMapConfirmedZoneId;

    private void OpenDetail(HuntWindowDto window, string backTitle) =>
        OpenDetailFor(window.MobId, window.WorldId, window.ZoneInstance, backTitle);

    private void OpenDetailFor(string mobId, string worldId, int zoneInstance, string backTitle)
    {
        PrepareDetail(mobId, worldId, zoneInstance);
        Push(new HuntsView(HuntsRoute.Detail, mobId, worldId, zoneInstance, backTitle));
    }

    private void SwitchDetailWorld(HuntRow row, string backTitle)
    {
        UiFeedback.Play(UiSound.Tap);
        PrepareDetail(row.Window.MobId, row.Window.WorldId, row.Window.ZoneInstance);
        router.Replace(new HuntsView(HuntsRoute.Detail, row.Window.MobId, row.Window.WorldId,
            row.Window.ZoneInstance, backTitle));
    }

    private void PrepareDetail(string mobId, string worldId, int zoneInstance)
    {
        detailKey = string.Empty;
        if (!string.Equals(detailMobId, mobId, StringComparison.Ordinal))
        {
            detailLoreExpanded = false;
        }

        detailMobId = mobId;
        detailMapActivePhase = hunts.PhaseFor(mobId, worldId, zoneInstance);
        detailMapConfirmedZoneId = hunts.ZoneIdFor(mobId, worldId, zoneInstance);
        ResolveDetailMap(mobCatalog.Find(mobId), worldId, zoneInstance);
        detailMapPendingFocus = true;
        detailMapHovered = false;
    }

    private void ResolveDetailMap(HuntMobDefinition? mob, string worldId, int zoneInstance)
    {
        detailMapZoneId = string.Empty;
        detailMapAetherytePoints.Clear();
        if (mob is null || mob.ZoneIds.Length == 0)
        {
            return;
        }

        detailMapZoneId = HuntCandidateResolver.ResolveBestZoneId(mob, worldId, zoneInstance, zoneCatalog, hunts,
            out _);
        if (zoneCatalog.FindZone(detailMapZoneId) is not { } zone)
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

    private void DrawDetail(in PhoneContext context, HuntsView view)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        var mob = mobCatalog.Find(view.MobId);
        var now = DateTimeOffset.UtcNow;
        EnsureDetailModel(view, mob, now);
        SyncDetailMap(view, mob);
        using (ImRaii.PushId("hunts.detail"))
        using (AppSurface.Begin(navBar.Body, disableMouseWheelScroll: detailMapHovered))
        {
            DrawDetailHero(mob, now, scale);
            DrawDetailActions(view, scale);
            DrawDetailMapSection(view, mob, scale);
            DrawOtherWorlds(view, scale);
            DrawAlertCard(view, scale);
            DrawDetailText(view, scale);
            DrawRewardsCard(view.MobId, scale);
            DrawTimingCard(mob, scale);
            DrawLoreCard(view.MobId, scale);
            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.detail.nav", detailRow.Name, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private void EnsureDetailModel(HuntsView view, HuntMobDefinition? mob, DateTimeOffset now)
    {
        var current = string.Equals(view.MobId, detailKey, StringComparison.Ordinal) &&
                      string.Equals(view.WorldId, detailKeyWorld, StringComparison.Ordinal) &&
                      view.ZoneInstance == detailKeyInstance && hunts.ActiveSpawnVersion == detailSpawnVersion &&
                      ReferenceEquals(Loc.Culture, detailCulture) && now < detailRefreshAt;
        if (current)
        {
            return;
        }

        detailKey = view.MobId;
        detailKeyWorld = view.WorldId;
        detailKeyInstance = view.ZoneInstance;
        detailCulture = Loc.Culture;
        detailSpawnVersion = hunts.ActiveSpawnVersion;
        detailRefreshAt = now + BoardRefreshInterval;
        var window = FindWindow(view.MobId, view.WorldId, view.ZoneInstance);
        detailHasWindow = window is not null;
        var placeholder = window ?? new HuntWindowDto
        {
            MobId = view.MobId,
            WorldId = view.WorldId,
            ZoneInstance = view.ZoneInstance,
        };
        var status = window is null ? HuntWindowStatus.Unknown : ResolveDisplayStatus(window, mob, now);
        FillRow(detailRow, placeholder, mob, status, ResolveMobLabel(mob, view.MobId), now);
        detailOpensLabel = window is not null && HuntWindowMath.MinimumReachedAt(window, mob) is { } opensAt
            ? HuntsText.Moment(opensAt)
            : string.Empty;
        detailCapLabel = window is not null && HuntWindowMath.TimingFor(window, mob) is { Cap: { } cap } &&
                         window.StartedAt != default
            ? HuntsText.Moment(window.StartedAt + TimeSpan.FromHours(cap))
            : string.Empty;
        detailCopyText = detailRow.Coordinate is { } coordinate
            ? Loc.T(L.Hunts.CopyLine, detailRow.Name, detailRow.Subtitle,
                CoordinateText(coordinate.X, coordinate.Y))
            : Loc.T(L.Hunts.CopyLineNoSpot, detailRow.Name, detailRow.Subtitle);
        detailStatusLine = detailHasWindow ? DetailStatusLine() : Loc.T(L.Hunts.NoWindowDataHint);
        BuildOtherWorlds(view, mob, now);
    }

    private void BuildOtherWorlds(HuntsView view, HuntMobDefinition? mob, DateTimeOffset now)
    {
        otherWorldRows.Clear();
        var windows = hunts.Windows;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (!string.Equals(window.MobId, view.MobId, StringComparison.Ordinal) ||
                (string.Equals(window.WorldId, view.WorldId, StringComparison.OrdinalIgnoreCase) &&
                 window.ZoneInstance == view.ZoneInstance))
            {
                continue;
            }

            while (otherWorldPool.Count <= otherWorldRows.Count)
            {
                otherWorldPool.Add(new HuntRow());
            }

            var row = otherWorldPool[otherWorldRows.Count];
            FillRow(row, window, mob, ResolveDisplayStatus(window, mob, now), detailRow.Name, now);
            row.Subtitle = ResolvePlace(window.WorldId, window.ZoneInstance, mob);
            otherWorldRows.Add(row);
        }

        otherWorldRows.Sort(static (left, right) =>
        {
            var bySection = ((int)left.Section).CompareTo((int)right.Section);
            return bySection != 0 ? bySection : left.SortKey.CompareTo(right.SortKey);
        });
    }

    private HuntWindowDto? FindWindow(string mobId, string worldId, int zoneInstance)
    {
        var windows = hunts.Windows;
        for (var index = 0; index < windows.Length; index++)
        {
            var candidate = windows[index];
            if (string.Equals(candidate.MobId, mobId, StringComparison.Ordinal) &&
                string.Equals(candidate.WorldId, worldId, StringComparison.OrdinalIgnoreCase) &&
                candidate.ZoneInstance == zoneInstance)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SyncDetailMap(HuntsView view, HuntMobDefinition? mob)
    {
        var livePhase = hunts.PhaseFor(view.MobId, view.WorldId, view.ZoneInstance);
        var liveZoneId = hunts.ZoneIdFor(view.MobId, view.WorldId, view.ZoneInstance);
        if (livePhase is null || (livePhase == detailMapActivePhase && liveZoneId == detailMapConfirmedZoneId))
        {
            return;
        }

        detailMapActivePhase = livePhase;
        detailMapConfirmedZoneId = liveZoneId;
        ResolveDetailMap(mob, view.WorldId, view.ZoneInstance);
    }

    private void DrawDetailHero(HuntMobDefinition? mob, DateTimeOffset now, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var inner = width - pad * 2f;
        var window = detailHasWindow ? detailRow.Window : null;
        var timeline = window is not null && detailRow.Section != HuntBoardSection.Live
            ? HuntBoard.TimelineFor(window, mob, now)
            : null;
        var capsuleHeight = HuntsArt.CapsuleHeight(scale);
        var bigStyle = TextStyles.Title2;
        var bigHeight = Typography.LineHeight(bigStyle);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var footHeight = Typography.LineHeight(TextStyles.Footnote);
        var gap = HuntsArt.LineGap * scale * 2f;
        var height = pad + capsuleHeight + gap + bigHeight + gap + lineHeight + pad;
        if (timeline is not null)
        {
            height += HuntsArt.RowGap * scale + DetailBarHeight * scale + gap + footHeight;
        }

        if (detailRow.Reporters.Length > 0)
        {
            height += gap + footHeight;
        }

        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("hunts.detail.hero", card);
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale, elevated: true);
        var left = card.Min.X + pad;
        var top = card.Min.Y + pad;
        var capsuleRight = left;
        if (detailRow.Rank.Length > 0)
        {
            var rankColor = HuntsArt.RankColor(detailRow.Rank, ui.Accent);
            capsuleRight += HuntsArt.Capsule(drawList, new Vector2(left, top), detailRow.Rank,
                IconTile.Surface(rankColor), AccentRing.Ink, scale) + HuntsArt.RowGap * scale * 0.5f;
        }

        var placeHeight = Typography.LineHeight(TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(capsuleRight, top + (capsuleHeight - placeHeight) * 0.5f),
            Typography.FitText(detailRow.Subtitle, MathF.Max(1f, card.Max.X - pad - capsuleRight), TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        top += capsuleHeight + gap;
        var statusInk = HuntsArt.StatusColor(detailRow.Status, ui.TitleInk);
        var big = detailHasWindow ? detailRow.Primary : Loc.T(L.Hunts.NoWindowData);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(big, inner, bigStyle),
            detailHasWindow ? statusInk : ui.TitleInk, bigStyle);
        top += bigHeight + gap;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(detailStatusLine, inner, TextStyles.Subheadline),
            ui.MutedInk, TextStyles.Subheadline);
        top += lineHeight;
        if (timeline is { } line)
        {
            top += HuntsArt.RowGap * scale;
            var bar = new Rect(new Vector2(left, top), new Vector2(card.Max.X - pad, top + DetailBarHeight * scale));
            HuntsArt.Timeline(drawList, bar, in line, statusInk == ui.TitleInk ? ui.Accent : statusInk, statusInk,
                ui.TitleInk, scale);
            top += DetailBarHeight * scale + gap;
            timelineLabelRight = float.MinValue;
            DrawTimelineLabel(drawList, bar, line.Minimum, detailOpensLabel, top);
            DrawTimelineLabel(drawList, bar, line.Cap, detailCapLabel, top);
            top += footHeight;
        }

        if (detailRow.Reporters.Length > 0)
        {
            top += gap;
            Typography.Draw(drawList, new Vector2(left, top),
                Typography.FitText(detailRow.Reporters, inner, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private string DetailStatusLine()
    {
        var status = StatusLabel(detailRow.Status);
        if (detailRow.Section == HuntBoardSection.Live || detailRow.Secondary.Length == 0)
        {
            return detailRow.Section == HuntBoardSection.Live ? Loc.T(L.Hunts.LiveHint) : status;
        }

        return Loc.T(L.Hunts.StatusDetail, status, detailRow.Secondary);
    }

    private void DrawTimelineLabel(ImDrawListPtr drawList, Rect bar, float fraction, string text, float top)
    {
        if (text.Length == 0)
        {
            return;
        }

        var width = Typography.Measure(text, TextStyles.Footnote).X;
        var x = MathF.Max(bar.Min.X, MathF.Min(bar.Min.X + bar.Width * fraction - width * 0.5f, bar.Max.X - width));
        if (x < timelineLabelRight)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(x, top), Typography.FitText(text, bar.Width, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        timelineLabelRight = x + width + HuntsArt.TileGap * UiScale.Current;
    }

    private void DrawDetailActions(HuntsView view, float scale)
    {
        if (detailRow.Section != HuntBoardSection.Live)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = DetailActionHeight * scale;
        var gap = HuntsArt.TileGap * scale;
        var worldId = HuntDataCenterWorlds.WorldRowId(view.WorldId);
        var canTravel = detailRow.TerritoryId != 0 && worldId != 0;
        var copyWidth = canTravel ? (width - gap) * 0.38f : width;
        var copyRect = new Rect(new Vector2(origin.X + width - copyWidth, origin.Y),
            new Vector2(origin.X + width, origin.Y + height));
        if (canTravel)
        {
            var goRect = new Rect(origin, new Vector2(copyRect.Min.X - gap, origin.Y + height));
            UiAnchors.Report("hunts.detail.go", goRect);
            var alreadyHere = Plugin.ClientState.TerritoryType == detailRow.TerritoryId &&
                              LocationShare.CurrentWorldId() == worldId;
            var label = alreadyHere ? Loc.T(L.Hunts.PlaceFlagOnMap) : Loc.T(L.Hunts.NavigateToLocation);
            if (ui.PillButton(goRect, label, true, "hunts.detail.navigate"))
            {
                UiFeedback.Play(UiSound.Tap);
                NavigateToCoordinate(detailRow.TerritoryId, worldId, ResolveMapId(detailRow.TerritoryId),
                    detailRow.Coordinate, detailRow.Coordinate, view.ZoneInstance);
            }
        }

        if (ui.PillButton(copyRect, Loc.T(L.Hunts.CopyForChat), false, "hunts.detail.copy"))
        {
            ImGui.SetClipboardText(detailCopyText);
            UiFeedback.Play(UiSound.Success);
            ShellToast.Show();
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawDetailMapSection(HuntsView view, HuntMobDefinition? mob, float scale)
    {
        var (states, _) = mob is not null
            ? candidateCache.ResolveFor(mob, view.WorldId, view.ZoneInstance, detailMapZoneId,
                includeLandmineOnlySpots: false)
            : (Array.Empty<HuntPoiState>(), null);
        if (states.Count == 0 && detailMapAetherytePoints.Count == 0)
        {
            detailMapHovered = false;
            return;
        }

        var zone = zoneCatalog.FindZone(detailMapZoneId);
        var territoryId = zoneCatalog.ResolveTerritoryId(detailMapZoneId);
        var texture = zoneMapTextures.ForTerritory(territoryId);
        if (zone is null || texture is null)
        {
            detailMapHovered = false;
            return;
        }

        ui.SectionLabel(ResolveZoneLabel(zone.Id), TextStyles.FootnoteEmphasized, 6f);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var size = MathF.Min(width, MapMaxSize * scale);
        var mapLeft = origin.X + (width - size) * 0.5f;
        var stage = new Rect(new Vector2(mapLeft, origin.Y), new Vector2(mapLeft + size, origin.Y + size));
        UiAnchors.Report("hunts.detail.map", stage);
        detailMapHovered = UiInteract.Hover(stage.Min, stage.Max);
        ImGui.SetCursorScreenPos(stage.Min);
        using (var mapChild = ImRaii.Child("##huntsDetailMap", stage.Size, false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                   ImGuiWindowFlags.NoBackground))
        {
            if (mapChild)
            {
                DrawDetailZoneMapContent(stage, texture, scale, states, view, territoryId);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, stage.Max.Y));
        DrawMapLegend(states, origin.X, width, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, size + MapLegendHeight * scale + HuntsArt.CardGap * scale));
    }

    private void DrawMapLegend(IReadOnlyList<HuntPoiState> states, float left, float width, float scale)
    {
        var hasCandidate = false;
        var hasSighted = false;
        var hasConfirmed = false;
        for (var index = 0; index < states.Count; index++)
        {
            switch (states[index].State)
            {
                case HuntsMapMarkerState.Confirmed:
                    hasConfirmed = true;
                    break;
                case HuntsMapMarkerState.Sighted:
                    hasSighted = true;
                    break;
                case HuntsMapMarkerState.Candidate:
                    hasCandidate = true;
                    break;
            }
        }

        var drawList = ImGui.GetWindowDrawList();
        var top = ImGui.GetCursorScreenPos().Y + 6f * scale;
        var centerY = top + Typography.LineHeight(TextStyles.Footnote) * 0.5f;
        var x = left;
        if (hasCandidate)
        {
            x = DrawLegendItem(drawList, x, centerY, ui.Accent, Loc.T(L.Hunts.NativeMapLegendCandidate), scale);
        }

        if (hasSighted)
        {
            x = DrawLegendItem(drawList, x, centerY, ui.MutedInk, Loc.T(L.Hunts.NativeMapLegendSighted), scale);
        }

        if (hasConfirmed && x < left + width)
        {
            DrawLegendItem(drawList, x, centerY, HuntsArt.OpenColor, Loc.T(L.Hunts.NativeMapLegendConfirmed), scale);
        }
    }

    private float DrawLegendItem(ImDrawListPtr drawList, float x, float centerY, Vector4 ink, string label, float scale)
    {
        HuntsArt.Dot(drawList, new Vector2(x + 4f * scale, centerY), ink, scale);
        var textLeft = x + 12f * scale;
        var size = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - size.Y * 0.5f), label, ui.MutedInk,
            TextStyles.Footnote);
        return textLeft + size.X + HuntsArt.RowGap * scale;
    }

    private void DrawDetailZoneMapContent(Rect stage, IDalamudTextureWrap texture, float scale,
        IReadOnlyList<HuntPoiState> states, HuntsView view, uint territoryId)
    {
        if (detailMapPendingFocus)
        {
            detailMapPendingFocus = false;
            FocusDetailMap(stage, texture.Size, states);
        }

        var drawList = ImGui.GetWindowDrawList();
        detailMapZoom.Draw(stage, texture, frameTheme, HuntsArt.CardRadius * scale, showButtons: false);
        var fit = PhotoZoomView.FitScale(stage, texture.Size);
        var drawnSize = texture.Size * fit * detailMapZoom.Zoom;
        var center = stage.Center + detailMapZoom.Pan;
        var min = center - drawnSize * 0.5f;
        var max = center + drawnSize * 0.5f;
        drawList.PushClipRect(stage.Min, stage.Max, true);
        for (var index = 0; index < states.Count; index++)
        {
            var (poi, state) = states[index];
            var (rawX, rawY) = poi.ParsedLocation();
            var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
            var dotPosition = new Vector2(min.X + normalizedX * (max.X - min.X), min.Y + normalizedY * (max.Y - min.Y));
            DrawSpawnDot(drawList, dotPosition, scale, poi.Id, state);
        }

        var worldId = HuntDataCenterWorlds.WorldRowId(view.WorldId);
        var mapId = ResolveMapId(territoryId);
        for (var index = 0; index < detailMapAetherytePoints.Count; index++)
        {
            var poi = detailMapAetherytePoints[index];
            var (rawX, rawY) = poi.ParsedLocation();
            var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
            var dotPosition = new Vector2(min.X + normalizedX * (max.X - min.X), min.Y + normalizedY * (max.Y - min.Y));
            DrawAetheryteDot(drawList, dotPosition, scale, poi, territoryId, worldId, mapId, view.ZoneInstance);
        }

        drawList.PopClipRect();
        Material.EdgeSquircle(drawList, stage.Min, stage.Max, HuntsArt.CardRadius * scale, scale);
    }

    private void FocusDetailMap(Rect stage, Vector2 textureSize, IReadOnlyList<HuntPoiState> states)
    {
        if (states.Count == 0)
        {
            detailMapZoom.Reset();
            return;
        }

        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var index = 0; index < states.Count; index++)
        {
            var (rawX, rawY) = states[index].Poi.ParsedLocation();
            var (normalizedX, normalizedY) = MapPixelMath.NormalizeToFullCanvas(rawX, rawY);
            min = new Vector2(MathF.Min(min.X, normalizedX), MathF.Min(min.Y, normalizedY));
            max = new Vector2(MathF.Max(max.X, normalizedX), MathF.Max(max.Y, normalizedY));
        }

        detailMapZoom.FocusOn(stage, textureSize, new Rect(min, max));
    }

    private void DrawSpawnDot(ImDrawListPtr drawList, Vector2 center, float scale, int poiId,
        HuntsMapMarkerState state)
    {
        var ink = state switch
        {
            HuntsMapMarkerState.Confirmed => HuntsArt.OpenColor,
            HuntsMapMarkerState.Sighted => ui.MutedInk,
            HuntsMapMarkerState.ActiveMinion => HuntsArt.LiveColor,
            HuntsMapMarkerState.SsSpawn => ui.Theme.Danger,
            _ => ui.Accent,
        };
        drawList.AddCircleFilled(center, MapDotRingRadius * scale, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)),
            20);
        drawList.AddCircle(center, MapDotRingRadius * scale, ImGui.GetColorU32(Vector4.One), 20, 1.4f * scale);
        drawList.AddCircleFilled(center, MapDotRadius * scale, ImGui.GetColorU32(ink), 20);
        var hitRadius = MapDotRingRadius * scale + 3f * scale;
        var hitMin = new Vector2(center.X - hitRadius, center.Y - hitRadius);
        var hitMax = new Vector2(center.X + hitRadius, center.Y + hitRadius);
        if (!UiInteract.Hover(hitMin, hitMax) || zoneCatalog.ResolveCoordinate(poiId) is not { } coordinate)
        {
            return;
        }

        if (!coordinateLabels.TryGetValue(poiId, out var label))
        {
            label = CoordinateText(coordinate.X, coordinate.Y);
            coordinateLabels[poiId] = label;
        }

        HoverTooltip.Show(new Rect(hitMin, hitMax), label, HoverLabelSide.Above);
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
            UiFeedback.Play(UiSound.Tap);
            NavigateToAetheryte(territoryId, worldId, mapId, zoneCatalog.ResolveCoordinate(poi.Id), zoneInstance);
        }
    }

    private string ResolvePoiLabel(HuntPoiEntry poi) =>
        poi.Name?.GetValueOrDefault(configuration.Language) ?? poi.Name?.GetValueOrDefault("en") ?? string.Empty;

    private void DrawOtherWorlds(HuntsView view, float scale)
    {
        if (otherWorldRows.Count == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.OtherWorldsSection), TextStyles.FootnoteEmphasized, 6f);
        var card = GroupCard.Begin(ui, otherWorldRows.Count, WorldRowHeight);
        card.SeparatorInset = WorldRingSize + HuntsArt.RowGap;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < otherWorldRows.Count; index++)
        {
            var row = card.NextRow();
            var bounds = new Rect(new Vector2(card.Bounds.Min.X, row.Min.Y), new Vector2(card.Bounds.Max.X, row.Max.Y));
            if (!ImGui.IsRectVisible(bounds.Min, bounds.Max))
            {
                continue;
            }

            var model = otherWorldRows[index];
            var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
            if (hovered)
            {
                drawList.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(ui.HoverWash));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = HuntsArt.StatusColor(model.Status, ui.MutedInk);
            var ring = WorldRingSize * scale;
            HuntsArt.StatusRing(drawList, new Vector2(row.Min.X + ring * 0.5f, row.Center.Y), ring * 0.5f,
                WorldRingThickness * scale, model.Ring, ink, string.Empty, ink);
            var primaryWidth = Typography.Measure(model.Primary, TextStyles.SubheadlineEmphasized).X;
            var primaryInk = model.Status is HuntWindowStatus.Closed or HuntWindowStatus.Unknown ? ui.TitleInk : ink;
            Typography.Draw(drawList, new Vector2(row.Max.X - primaryWidth,
                    row.Center.Y - Typography.LineHeight(TextStyles.SubheadlineEmphasized) * 0.5f), model.Primary,
                primaryInk, TextStyles.SubheadlineEmphasized);
            var left = row.Min.X + ring + HuntsArt.RowGap * scale;
            Typography.Draw(drawList, new Vector2(left, row.Center.Y - Typography.LineHeight(TextStyles.Body) * 0.5f),
                Typography.FitText(model.Subtitle, MathF.Max(1f, row.Max.X - primaryWidth - HuntsArt.RowGap * scale - left),
                    TextStyles.Body), ui.TitleInk, TextStyles.Body);
            if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
            {
                SwitchDetailWorld(model, view.BackTitle);
            }
        }

        card.End();
        Gap(HuntsArt.CardGap);
    }

    private void DrawAlertCard(HuntsView view, float scale)
    {
        var settings = hunts.NotificationSettings;
        var mode = settings.MobOverrideModeFor(view.MobId);
        var overrideWorld = settings.MobOverrideWorldFor(view.MobId);
        var hintWorld = mode == HuntMobNotificationMode.EnabledOnWorld && overrideWorld is { Length: > 0 }
            ? overrideWorld
            : view.WorldId;
        alertOptions[0] = Loc.T(L.Hunts.AlertModeDefault);
        alertOptions[1] = Loc.T(L.Hunts.AlertModeOn);
        alertOptions[2] = ResolveWorldLabel(view.WorldId);
        alertOptions[3] = Loc.T(L.Hunts.AlertModeOff);
        var signedIn = hunts.IsAuthenticated;
        var selected = mode switch
        {
            HuntMobNotificationMode.Enabled => 1,
            HuntMobNotificationMode.EnabledOnWorld => 2,
            HuntMobNotificationMode.Disabled => 3,
            _ => 0,
        };
        var hint = AlertHint(mode, ResolveWorldLabel(hintWorld));
        ui.SectionLabel(Loc.T(L.Hunts.MarkAlertsSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width - pad * 2f).Y;
        var height = pad + AlertStripHeight * scale + HuntsArt.RowGap * scale * 0.75f + hintHeight + pad;
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("hunts.detail.alerts", card);
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale);
        var strip = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Max.X - pad, card.Min.Y + pad + AlertStripHeight * scale));
        int picked;
        bool pressed;
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (signedIn ? 1f : AlertsDisabledAlpha)))
        {
            picked = SegmentStrip.Draw("hunts.detail.alertMode", strip, alertOptions, selected,
                Palette.Mix(ui.FieldSurface, ui.TitleInk, 0.06f), ui.Accent, ui.MutedInk, AccentRing.Ink, out pressed,
                AlertStripHeight);
        }

        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, strip.Max.Y + HuntsArt.RowGap * scale * 0.75f), hint,
            ui.MutedInk, TextStyles.Footnote, width - pad * 2f);
        var next = picked switch
        {
            1 => HuntMobNotificationMode.Enabled,
            2 => HuntMobNotificationMode.EnabledOnWorld,
            3 => HuntMobNotificationMode.Disabled,
            _ => HuntMobNotificationMode.Default,
        };
        var retarget = next == HuntMobNotificationMode.EnabledOnWorld &&
                       !string.Equals(overrideWorld, view.WorldId, StringComparison.OrdinalIgnoreCase);
        if (signedIn && pressed && (next != mode || retarget))
        {
            settings.SetMobOverride(view.MobId, next,
                next == HuntMobNotificationMode.EnabledOnWorld ? view.WorldId : null);
            hunts.SaveNotificationSettings();
            UiFeedback.Play(next == HuntMobNotificationMode.Disabled ? UiSound.ToggleOff : UiSound.ToggleOn);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private string AlertHint(HuntMobNotificationMode mode, string world)
    {
        var signedIn = hunts.IsAuthenticated;
        if (mode == alertHintMode && signedIn == alertHintSignedIn &&
            string.Equals(world, alertHintWorld, StringComparison.Ordinal) &&
            ReferenceEquals(Loc.Culture, alertHintCulture) && alertHint.Length > 0)
        {
            return alertHint;
        }

        alertHintMode = mode;
        alertHintSignedIn = signedIn;
        alertHintWorld = world;
        alertHintCulture = Loc.Culture;
        alertHint = !signedIn
            ? Loc.T(L.Hunts.NotificationsSignInHint)
            : mode switch
            {
                HuntMobNotificationMode.Enabled => Loc.T(L.Hunts.AlertHintOn),
                HuntMobNotificationMode.EnabledOnWorld => Loc.T(L.Hunts.AlertHintWorld, world),
                HuntMobNotificationMode.Disabled => Loc.T(L.Hunts.AlertHintOff),
                _ => Loc.T(L.Hunts.AlertHintDefault),
            };
        return alertHint;
    }

    private string RewardCaption(int amount)
    {
        if (!ReferenceEquals(Loc.Culture, rewardCaptionCulture))
        {
            rewardCaptionCulture = Loc.Culture;
            rewardCaptions.Clear();
        }

        if (rewardCaptions.TryGetValue(amount, out var cached))
        {
            return cached;
        }

        var caption = Loc.T(L.Hunts.RewardAmount, amount);
        rewardCaptions[amount] = caption;
        return caption;
    }

    private void DrawDetailText(HuntsView view, float scale)
    {
        var spawn = HuntMobLore.SpawnFor(view.MobId) is { } spawnLore
            ? Loc.T(spawnLore)
            : Loc.T(L.Hunts.NoSpecialSpawnCondition);
        DrawTextCard(Loc.T(L.Hunts.SpawnConditionSection), spawn, null, int.MaxValue, scale, out _);
        if (HuntMobLore.TipFor(view.MobId) is not { } tip)
        {
            return;
        }

        var note = HuntMobLore.TipIsFallback(view.MobId) ? Loc.T(L.Hunts.LoreNotAvailableInLanguage) : null;
        DrawTextCard(Loc.T(L.Hunts.TipsSection), tip, note, int.MaxValue, scale, out _);
    }

    private void DrawLoreCard(string mobId, float scale)
    {
        var raw = HuntMobLore.DescriptionFor(mobId);
        if (raw is null)
        {
            return;
        }

        var note = HuntMobLore.DescriptionIsFallback(mobId) ? Loc.T(L.Hunts.LoreNotAvailableInLanguage) : null;
        var lines = detailLoreExpanded ? int.MaxValue : LoreCollapsedLines;
        var card = DrawTextCard(Loc.T(L.Hunts.DescriptionSection), raw, note, lines, scale, out var truncated);
        if (!truncated && !detailLoreExpanded)
        {
            return;
        }

        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            detailLoreExpanded = !detailLoreExpanded;
            UiFeedback.Play(UiSound.Tap);
        }
    }

    private Rect DrawTextCard(string title, string text, string? note, int maxLines, float scale, out bool truncated)
    {
        ui.SectionLabel(title, TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var inner = width - pad * 2f;
        var lines = Typography.WrapText(text, TextStyles.Body, inner);
        truncated = lines.Length > maxLines;
        var shown = Math.Min(lines.Length, maxLines);
        var lineHeight = Typography.LineHeight(TextStyles.Body) + HuntsArt.LineGap * scale;
        var noteHeight = note is null ? 0f : Typography.MeasureWrappedBlock(note, TextStyles.Footnote, inner).Y +
                                             HuntsArt.TileGap * scale;
        var moreHeight = truncated ? Typography.LineHeight(TextStyles.SubheadlineEmphasized) : 0f;
        var height = pad + noteHeight + shown * lineHeight + moreHeight + pad * 0.75f;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale);
        var left = card.Min.X + pad;
        var top = card.Min.Y + pad;
        if (note is not null)
        {
            top += Typography.DrawWrappedLeft(new Vector2(left, top), note, ui.MutedInk, TextStyles.Footnote, inner) +
                   HuntsArt.TileGap * scale;
        }

        for (var index = 0; index < shown; index++)
        {
            Typography.Draw(drawList, new Vector2(left, top), lines[index], ui.BodyInk, TextStyles.Body);
            top += lineHeight;
        }

        if (truncated)
        {
            Typography.Draw(drawList, new Vector2(left, top), Loc.T(L.Hunts.ShowMore), ui.Accent,
                TextStyles.SubheadlineEmphasized);
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
        return card;
    }

    private void DrawRewardsCard(string mobId, float scale)
    {
        var rewards = rewardCatalog.RewardsFor(mobId);
        if (rewards.Count == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.RewardsSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var inner = width - pad * 2f;
        var iconSize = RewardIconSize * scale;
        var tileGap = RewardGap * scale;
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var tileHeight = iconSize + 4f * scale + captionHeight;
        var columns = Math.Max(1, (int)((inner + tileGap) / (iconSize + tileGap)));
        var rows = (rewards.Count + columns - 1) / columns;
        var height = pad * 2f + rows * tileHeight + (rows - 1) * tileGap;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale);
        var displayLocale = HuntUiLanguage.Key();
        var searchLocale = HuntClientLanguage.Key();
        for (var index = 0; index < rewards.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var tileMin = new Vector2(card.Min.X + pad + column * (iconSize + tileGap),
                card.Min.Y + pad + row * (tileHeight + tileGap));
            DrawRewardTile(drawList, tileMin, iconSize, captionHeight, rewards[index], displayLocale, searchLocale,
                scale);
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawRewardTile(ImDrawListPtr drawList, Vector2 iconMin, float iconSize, float captionHeight,
        HuntMobRewardEntry entry, string displayLocale, string searchLocale, float scale)
    {
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        var searchName = rewardCatalog.ItemNameFor(entry.ItemId, searchLocale);
        var iconId = HuntRewardIcons.ResolveIconId(entry.ItemId, searchName);
        GameIconTile.Draw(drawList, Plugin.TextureProvider, iconId, iconMin, iconMax, iconSize * Metrics.Radius.TileFactor,
            scale, ImGui.GetColorU32(ui.FieldSurface), edgeStroke: true);
        if (entry.Amount is { } amount)
        {
            var caption = RewardCaption(amount);
            var captionSize = Typography.Measure(caption, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(iconMin.X + (iconSize - captionSize.X) * 0.5f, iconMax.Y + 4f * scale),
                caption, ui.TitleInk, TextStyles.FootnoteEmphasized);
        }

        if (rewardCatalog.ItemNameFor(entry.ItemId, displayLocale) is { } displayName)
        {
            HoverTooltip.Show(new Rect(iconMin, new Vector2(iconMax.X, iconMax.Y + 4f * scale + captionHeight)),
                displayName);
        }
    }

    private void DrawTimingCard(HuntMobDefinition? mob, float scale)
    {
        if (mob is not { Windows.Length: > 0 })
        {
            return;
        }

        var windowIndex = detailHasWindow ? Math.Clamp(detailRow.Window.Num - 1, 0, mob.Windows.Length - 1) : 0;
        var timing = mob.Windows[windowIndex].Timing;
        if (timing?.Normal is not { } normal)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Hunts.SpawnInfoSection), TextStyles.FootnoteEmphasized, 6f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.Headline);
        var rowHeight = labelHeight + HuntsArt.LineGap * scale + valueHeight;
        var rows = timing.Maintenance is null ? 1 : 2;
        var height = pad * 2f + rows * rowHeight + (rows - 1) * HuntsArt.RowGap * scale;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale);
        var top = card.Min.Y + pad;
        DrawTimingRow(drawList, card, pad, top, normal, Loc.T(L.Hunts.TimingNormal), scale);
        if (timing.Maintenance is { } maintenance)
        {
            top += rowHeight + HuntsArt.RowGap * scale;
            drawList.AddLine(new Vector2(card.Min.X + pad, top - HuntsArt.RowGap * scale * 0.5f),
                new Vector2(card.Max.X - pad, top - HuntsArt.RowGap * scale * 0.5f), ImGui.GetColorU32(ui.Hairline),
                Metrics.Stroke.Hairline);
            DrawTimingRow(drawList, card, pad, top, maintenance, Loc.T(L.Hunts.SpawnInfoMaintenance), scale);
        }

        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
    }

    private void DrawTimingRow(ImDrawListPtr drawList, Rect card, float pad, float top, HuntMobTimingWindow window,
        string title, float scale)
    {
        var inner = card.Width - pad * 2f;
        var titleWidth = inner * 0.34f;
        var columnWidth = (inner - titleWidth - TimingColumnGap * scale * 2f) / 3f;
        var left = card.Min.X + pad;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueTop = top + labelHeight + HuntsArt.LineGap * scale;
        Typography.Draw(drawList, new Vector2(left, valueTop), Typography.FitText(title, titleWidth, TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        var x = left + titleWidth;
        DrawTimingCell(drawList, x, top, valueTop, columnWidth, Loc.T(L.Hunts.SpawnInfoMinimum),
            HoursLabel(window.Min));
        x += columnWidth + TimingColumnGap * scale;
        DrawTimingCell(drawList, x, top, valueTop, columnWidth, Loc.T(L.Hunts.SpawnInfoAverage),
            window.Avg is { } average ? HoursLabel(average) : Loc.T(L.Hunts.NoValue));
        x += columnWidth + TimingColumnGap * scale;
        DrawTimingCell(drawList, x, top, valueTop, columnWidth, Loc.T(L.Hunts.SpawnInfoMaximum),
            window.Cap is { } cap ? HoursLabel(cap) : Loc.T(L.Hunts.NoValue));
    }

    private string HoursLabel(double hours)
    {
        if (!ReferenceEquals(Loc.Culture, hoursLabelCulture))
        {
            hoursLabelCulture = Loc.Culture;
            hoursLabels.Clear();
        }

        if (hoursLabels.TryGetValue(hours, out var cached))
        {
            return cached;
        }

        var label = HuntsText.Hours(hours);
        hoursLabels[hours] = label;
        return label;
    }

    private void DrawTimingCell(ImDrawListPtr drawList, float left, float labelTop, float valueTop, float width,
        string label, string value)
    {
        Typography.Draw(drawList, new Vector2(left, labelTop), Typography.FitText(label, width, TextStyles.Footnote),
            ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, valueTop), Typography.FitText(value, width, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
    }

    private static string CoordinateText(float x, float y) =>
        string.Create(CultureInfo.InvariantCulture, $"X: {x:0.0}  Y: {y:0.0}");
}
