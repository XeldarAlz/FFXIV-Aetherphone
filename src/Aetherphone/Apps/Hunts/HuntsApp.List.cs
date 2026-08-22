using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const float RowHeight = 64f;
    private const float InstanceBadgeSize = 16f;

    private static readonly Vector4 OpenBarColor = new(0.20f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 CappedBarColor = new(0.20f, 0.55f, 0.95f, 1f);
    private static readonly Vector4 UnmetBarColor = new(0.60f, 0.40f, 0.90f, 1f);
    private static readonly Vector4 SpawnedBarColor = new(0.95f, 0.78f, 0.20f, 1f);
    private static readonly Vector4 RankSSColor = new(0.95f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 RankSColor = new(0.65f, 0.45f, 0.95f, 1f);
    private static readonly Vector4 RankAColor = new(0.30f, 0.65f, 0.95f, 1f);

    private static readonly TimeSpan FilteredWindowsResortInterval = TimeSpan.FromSeconds(5);

    private readonly List<HuntWindowDto> filteredWindows = new();
    private readonly Comparison<HuntWindowDto> compareByPercentageDescending;
    private readonly Dictionary<string, string> worldLabelCache = new();
    private readonly Dictionary<(string MobId, string WorldId, int ZoneInstance), string> rowIdCache = new();
    private readonly Dictionary<(string MobId, string WorldId, int ZoneInstance, int CurrentPhase, int OwnPhaseCount), string> phaseLabelCache = new();
    private readonly Dictionary<(string MobId, string WorldId, int ZoneInstance, HuntWindowStatus Status, string PhaseLabel), string> statusLabelCache = new();
    private DateTimeOffset sortNow;
    private HuntWindowDto[]? filteredWindowsSource;
    private int filteredWindowsSpawnVersion = -1;
    private int filteredWindowsFilterRevision = -1;
    private string filteredWindowsSearchQuery = string.Empty;
    private DateTimeOffset filteredWindowsResortAt;

    private void DrawList(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            if (hunts.Failed)
            {
                DrawFailed(body, scale);
                return;
            }

            if (!hunts.Loaded)
            {
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                    ui.MutedInk, Loc.T(L.Common.Loading));
                return;
            }

            var now = DateTimeOffset.UtcNow;
            EnsureFilteredWindows(now);
            if (filteredWindows.Count == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.MapMarkerAlt, Loc.T(L.Hunts.Empty), string.Empty);
                return;
            }

            var card = RowListCard.Begin(ui, filteredWindows.Count, RowHeight, scale);
            for (var index = 0; index < filteredWindows.Count; index++)
            {
                DrawRow(card.Row(index), filteredWindows[index], scale, now);
            }

            card.End();
        }
    }

    private void EnsureFilteredWindows(DateTimeOffset now)
    {
        var windows = hunts.Windows;
        var spawnVersion = hunts.ActiveSpawnVersion;
        var filterRevision = filter.Revision;
        var searchText = searchQuery.Trim();

        var upToDate = ReferenceEquals(windows, filteredWindowsSource)
            && spawnVersion == filteredWindowsSpawnVersion
            && filterRevision == filteredWindowsFilterRevision
            && string.Equals(searchText, filteredWindowsSearchQuery, StringComparison.Ordinal)
            && now < filteredWindowsResortAt;
        if (upToDate)
        {
            return;
        }

        filteredWindowsSource = windows;
        filteredWindowsSpawnVersion = spawnVersion;
        filteredWindowsFilterRevision = filterRevision;
        filteredWindowsSearchQuery = searchText;
        filteredWindowsResortAt = now + FilteredWindowsResortInterval;
        FilterWindows(now, windows, searchText);
    }

    private void FilterWindows(DateTimeOffset now, HuntWindowDto[] windows, string searchText)
    {
        sortNow = now;
        filteredWindows.Clear();
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            var mob = mobCatalog.Find(window.MobId);

            if (mob is { Rank: "SS" } && !hunts.IsSpawned(window.MobId, window.WorldId, window.ZoneInstance))
            {
                continue;
            }

            var status = ResolveDisplayStatus(window, mob, now);
            if (!filter.Matches(window, mob, status))
            {
                continue;
            }

            if (searchText.Length > 0
                && !ResolveMobLabel(mob, window.MobId).Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filteredWindows.Add(window);
        }

        filteredWindows.Sort(compareByPercentageDescending);
    }

    private int CompareByPercentageDescending(HuntWindowDto left, HuntWindowDto right)
    {
        var leftSpawned = hunts.IsSpawned(left.MobId, left.WorldId, left.ZoneInstance);
        var rightSpawned = hunts.IsSpawned(right.MobId, right.WorldId, right.ZoneInstance);
        if (leftSpawned != rightSpawned)
        {
            return leftSpawned ? -1 : 1;
        }

        var leftPercentage = HuntWindowMath.Percentage(left, mobCatalog.Find(left.MobId), sortNow);
        var rightPercentage = HuntWindowMath.Percentage(right, mobCatalog.Find(right.MobId), sortNow);
        if (leftPercentage is null && rightPercentage is null)
        {
            return CompareByMinimumReachedAt(left, right);
        }

        if (leftPercentage is null)
        {
            return 1;
        }

        if (rightPercentage is null)
        {
            return -1;
        }

        var percentageCompare = rightPercentage.Value.CompareTo(leftPercentage.Value);
        return percentageCompare != 0 ? percentageCompare : CompareByMinimumReachedAt(left, right);
    }

    private int CompareByMinimumReachedAt(HuntWindowDto left, HuntWindowDto right)
    {
        var leftMoment = HuntWindowMath.MinimumReachedAt(left, mobCatalog.Find(left.MobId));
        var rightMoment = HuntWindowMath.MinimumReachedAt(right, mobCatalog.Find(right.MobId));
        if (leftMoment is null)
        {
            return rightMoment is null ? 0 : 1;
        }

        if (rightMoment is null)
        {
            return -1;
        }

        return leftMoment.Value.CompareTo(rightMoment.Value);
    }

    private void DrawRow(Rect row, HuntWindowDto window, float scale, DateTimeOffset now)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(ImGui.GetWindowDrawList(), row.Min, row.Max, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.05f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var def = mobCatalog.Find(window.MobId);
        var mobLabel = ResolveMobLabel(def, window.MobId);

        var textLeft = row.Min.X;
        var nameY = row.Center.Y - 16f * scale;
        var rowId = ResolveRowId(window.MobId, window.WorldId, window.ZoneInstance);
        DrawMarkNameLine(rowId, def, mobLabel, window.ZoneInstance, textLeft, nameY, row.Width * 0.55f, scale);

        var worldLabel = ResolveWorldLabel(window.WorldId);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), worldLabel, ui.MutedInk, TextStyles.Footnote);

        var status = ResolveDisplayStatus(window, def, now);
        var statusLabel = ComposeStatusLabel(status, window.MobId, window.WorldId, window.ZoneInstance, def);
        var statusInk = StatusColor(status);

        var percentage = HuntWindowMath.Percentage(window, def, now);
        DrawProgressBar(row, status, percentage ?? 0d, scale);

        var detailLabel = ResolveDetailLabel(status, window, def, now);

        var statusSize = Typography.Measure(statusLabel, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(row.Max.X - statusSize.X, nameY), statusLabel, statusInk, TextStyles.BodyEmphasized);

        if (detailLabel.Length > 0)
        {
            var detailSize = Typography.Measure(detailLabel, TextStyles.Footnote);
            Typography.Draw(new Vector2(row.Max.X - detailSize.X, row.Center.Y + 4f * scale), detailLabel,
                ui.MutedInk, TextStyles.Footnote);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenDetail(window);
        }
    }

    private string ResolveRowId(string mobId, string worldId, int zoneInstance)
    {
        var key = (mobId, worldId, zoneInstance);
        if (rowIdCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var rowId = "hunts.window." + mobId + worldId + zoneInstance;
        rowIdCache[key] = rowId;
        return rowId;
    }

    private string ResolveWorldLabel(string worldId)
    {
        if (worldLabelCache.TryGetValue(worldId, out var cached))
        {
            return cached;
        }

        var label = Prettify(worldId);
        worldLabelCache[worldId] = label;
        return label;
    }

    private string ResolveMobLabel(HuntMobDefinition? def, string mobId) =>
        def?.Name.GetValueOrDefault(configuration.Language)
        ?? def?.Name.GetValueOrDefault("en")
        ?? Prettify(mobId);

    private HuntWindowStatus ResolveDisplayStatus(HuntWindowDto window, HuntMobDefinition? mob, DateTimeOffset now)
    {
        if (hunts.IsScheduled(window.MobId, window.WorldId, window.ZoneInstance))
        {
            return HuntWindowStatus.Scheduled;
        }

        if (hunts.IsSpawned(window.MobId, window.WorldId, window.ZoneInstance))
        {
            return HuntWindowStatus.Spawned;
        }

        var status = HuntWindowMath.Status(window, mob, now);
        if (status is HuntWindowStatus.Open or HuntWindowStatus.Capped &&
            TryResolveConditionGate(mob, now, out _))
        {
            return HuntWindowStatus.Unmet;
        }

        return status;
    }

    private string ResolveDetailLabel(HuntWindowStatus status, HuntWindowDto window, HuntMobDefinition? def,
        DateTimeOffset now) => status switch
    {
        HuntWindowStatus.Closed => HuntWindowMath.TimeUntilMinimum(window, def, now) is { } untilMinimum
            ? TimeText.Until(untilMinimum)
            : string.Empty,
        HuntWindowStatus.Unmet => TryResolveConditionGate(def, now, out var untilGate)
            ? TimeText.Until(untilGate)
            : string.Empty,
        HuntWindowStatus.Open or HuntWindowStatus.Capped =>
            HuntWindowMath.MinimumReachedAt(window, def) is { } minimumReachedAt
                ? TimeText.Ago(minimumReachedAt)
                : string.Empty,
        HuntWindowStatus.Spawned or HuntWindowStatus.Scheduled =>
            hunts.SpawnedSince(window.MobId, window.WorldId, window.ZoneInstance) is { } spawnedSince
                ? TimeText.AgoPrecise(spawnedSince)
                : string.Empty,
        _ => string.Empty,
    };

    private static bool TryResolveConditionGate(HuntMobDefinition? mob, DateTimeOffset now, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (mob?.Conditions?.Automatic is not { Length: > 0 } automatic)
        {
            return false;
        }

        if (HuntSpawnConditionResolver.ResolveGate(automatic, now) is not { } gate || gate.ActiveAt(now))
        {
            return false;
        }

        remaining = gate.Start - now;
        return true;
    }

    private void DrawMarkNameLine(string marqueeId, HuntMobDefinition? def, string mobLabel, int zoneInstance,
        float left, float y, float maxWidth, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerY = y + Typography.LineHeight(TextStyles.BodyEmphasized) * 0.5f;

        var nameLeft = left;
        var rankLabel = def?.Rank ?? string.Empty;
        if (rankLabel.Length > 0)
        {
            var rankBadgeWidth = InlineBadge.Draw(drawList, nameLeft, centerY, rankLabel, RankBadgeColor(rankLabel),
                scale);
            nameLeft = nameLeft + rankBadgeWidth + 6f * scale;
        }

        var showsInstanceBadge = def is not null && zoneInstance > 0 && hunts.ZoneInstanceCountFor(def) > 1;
        var instanceReserve = showsInstanceBadge ? (InstanceBadgeSize + 6f) * scale : 0f;
        var nameMaxWidth = maxWidth - (nameLeft - left) - instanceReserve;
        var nameWidth = Marquee.DrawLeftAuto(marqueeId, mobLabel, nameLeft, y, nameMaxWidth,
            TextStyles.BodyEmphasized, ui.TitleInk);

        if (showsInstanceBadge)
        {
            var badgeLeft = nameLeft + nameWidth + 6f * scale;
            InlineBadge.Draw(drawList, badgeLeft, centerY, zoneInstance.ToString(Loc.Culture), ui.MutedInk, scale,
                InstanceBadgeSize * scale);
        }
    }

    private Vector4 RankBadgeColor(string rank) => rank switch
    {
        "SS" => RankSSColor,
        "S" => RankSColor,
        "A" => RankAColor,
        _ => ui.MutedInk,
    };

    private void DrawProgressBar(Rect row, HuntWindowStatus status, double percentage, float scale)
    {
        var barHeight = 3f * scale;
        var top = row.Max.Y - 6f * scale - barHeight;
        ProgressBar.Draw(ImGui.GetWindowDrawList(), row.Min.X, row.Max.X, top, barHeight, percentage,
            StatusColor(status), TextStyles.Caption2, 6f * scale);
    }

    private Vector4 StatusColor(HuntWindowStatus status) => status switch
    {
        HuntWindowStatus.Open => OpenBarColor,
        HuntWindowStatus.Capped => CappedBarColor,
        HuntWindowStatus.Unmet => UnmetBarColor,
        HuntWindowStatus.Spawned or HuntWindowStatus.Scheduled => SpawnedBarColor,
        _ => ui.MutedInk,
    };

    private static string StatusLabel(HuntWindowStatus status) => status switch
    {
        HuntWindowStatus.Closed => Loc.T(L.Hunts.Closed),
        HuntWindowStatus.Open => Loc.T(L.Hunts.Open),
        HuntWindowStatus.Capped => Loc.T(L.Hunts.Capped),
        HuntWindowStatus.Unmet => Loc.T(L.Hunts.Unmet),
        HuntWindowStatus.Spawned => Loc.T(L.Hunts.Spawned),
        HuntWindowStatus.Scheduled => Loc.T(L.Hunts.Scheduled),
        _ => Loc.T(L.Hunts.Unknown),
    };

    private string ComposeStatusLabel(HuntWindowStatus status, string mobId, string worldId, int zoneInstance,
        HuntMobDefinition? mob)
    {
        var statusLabel = StatusLabel(status);
        var phaseLabel = ResolvePhaseLabel(mobId, worldId, zoneInstance, mob);
        if (phaseLabel.Length == 0)
        {
            return statusLabel;
        }

        var key = (mobId, worldId, zoneInstance, status, phaseLabel);
        if (statusLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var composed = statusLabel + " (" + phaseLabel + ")";
        statusLabelCache[key] = composed;
        return composed;
    }

    private string ResolvePhaseLabel(string mobId, string worldId, int zoneInstance, HuntMobDefinition? mob)
    {
        if (mob is not { Windows.Length: > 0 })
        {
            return string.Empty;
        }

        if (hunts.PhaseFor(mobId, worldId, zoneInstance) is not { } phase)
        {
            return string.Empty;
        }

        var windowIndex = Math.Clamp(phase.WindowNum - 1, 0, mob.Windows.Length - 1);
        var phases = mob.Windows[windowIndex].Phases;
        var ownPhaseCount = 0;
        for (var phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
        {
            var phaseMobId = phases[phaseIndex].MobId;
            if (phaseMobId is null || string.Equals(phaseMobId, mobId, StringComparison.Ordinal))
            {
                ownPhaseCount++;
            }
        }

        if (ownPhaseCount <= 1)
        {
            return string.Empty;
        }

        var currentPhase = Math.Clamp(phase.PhaseNum, 1, ownPhaseCount);
        var key = (mobId, worldId, zoneInstance, currentPhase, ownPhaseCount);
        if (phaseLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var phaseLabel = currentPhase.ToString(Loc.Culture) + "/" + ownPhaseCount.ToString(Loc.Culture);
        phaseLabelCache[key] = phaseLabel;
        return phaseLabel;
    }

    private static string Prettify(string slug)
    {
        if (slug.Length == 0)
        {
            return slug;
        }

        var parts = slug.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            parts[index] = part.Length > 0 ? char.ToUpperInvariant(part[0]) + part[1..] : part;
        }

        return string.Join(' ', parts);
    }

    private void DrawFailed(Rect body, float scale)
    {
        EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Hunts.Failed), string.Empty);
        var label = Loc.T(L.Hunts.TryAgain);
        var width = Typography.Measure(label, TextStyles.BodyEmphasized).X + 44f * scale;
        var height = 38f * scale;
        var min = new Vector2(body.Center.X - width * 0.5f, body.Center.Y + 34f * scale);
        var rect = new Rect(min, min + new Vector2(width, height));
        if (ui.PillButton(rect, label, true))
        {
            hunts.Retry();
        }
    }
}
