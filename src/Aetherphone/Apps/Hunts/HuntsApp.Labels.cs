using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Lumina.Excel.Sheets;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    internal static readonly Vector4 RankSSColor = HuntsArt.RankSSColor;
    internal static readonly Vector4 RankSColor = HuntsArt.RankSColor;

    private readonly Dictionary<string, string> worldLabelCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(uint TerritoryId, string ZoneId, string Language), string> zoneLabelCache = new();
    private readonly string[] expansionNames = new string[HuntExpansions.Ids.Length];
    private string expansionNamesLanguage = string.Empty;

    private string ResolveMobLabel(HuntMobDefinition? mob, string mobId) =>
        mob?.Name.GetValueOrDefault(configuration.Language)
        ?? mob?.Name.GetValueOrDefault("en")
        ?? Prettify(mobId);

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

    private string ResolvePlace(string worldId, int zoneInstance, HuntMobDefinition? mob)
    {
        var world = ResolveWorldLabel(worldId);
        return mob is not null && zoneInstance > 0 && hunts.ZoneInstanceCountFor(mob) > 1
            ? Loc.T(L.Hunts.PlaceInstance, world, zoneInstance)
            : world;
    }

    private string ResolveMobZoneId(HuntMobDefinition? mob, string mobId, string worldId, int zoneInstance)
    {
        if (hunts.ZoneIdFor(mobId, worldId, zoneInstance) is { Length: > 0 } live)
        {
            return live;
        }

        return mob is { ZoneIds.Length: > 0 } ? mob.ZoneIds[0] : string.Empty;
    }

    private string ResolveZoneLabel(string zoneId)
    {
        if (zoneId.Length == 0)
        {
            return string.Empty;
        }

        var territoryId = zoneCatalog.ResolveTerritoryId(zoneId);
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

    private string ExpansionName(int index)
    {
        var language = HuntUiLanguage.Key();
        if (!string.Equals(language, expansionNamesLanguage, StringComparison.Ordinal))
        {
            expansionNamesLanguage = language;
            Array.Clear(expansionNames);
        }

        if (expansionNames[index] is { } cached)
        {
            return cached;
        }

        var name = Plugin.DataManager.GetExcelSheet<ExVersion>(HuntUiLanguage.SheetLanguage())
            .TryGetRow((uint)index, out var row) ? row.Name.ExtractText() : string.Empty;
        expansionNames[index] = name.Length > 0 ? name : HuntExpansions.Labels[index];
        return expansionNames[index];
    }

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
        if (status is HuntWindowStatus.Open or HuntWindowStatus.Capped && TryResolveConditionGate(mob, now, out _))
        {
            return HuntWindowStatus.Unmet;
        }

        return status;
    }

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

    private string ResolvePhaseLabel(string mobId, string worldId, int zoneInstance, HuntMobDefinition? mob)
    {
        if (mob is not { Windows.Length: > 0 } || hunts.PhaseFor(mobId, worldId, zoneInstance) is not { } phase)
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
        return Loc.T(L.Hunts.PhaseOf, currentPhase, ownPhaseCount);
    }

    private string ResolveReporterLabel(string mobId, string worldId, int zoneInstance)
    {
        var reporterNames = hunts.ReporterNamesFor(mobId, worldId, zoneInstance);
        return reporterNames is { Length: > 0 }
            ? Loc.T(L.Hunts.SpawnInfoLineFormat, Loc.T(L.Hunts.ReportedByLabel), string.Join(", ", reporterNames))
            : string.Empty;
    }

    internal static string Prettify(string slug)
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
}
