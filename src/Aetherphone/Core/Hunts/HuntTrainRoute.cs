namespace Aetherphone.Core.Hunts;

internal readonly record struct HuntTrainStop(string ZoneId, int ZoneInstance, string MobId, int WindowIndex);

internal static class HuntTrainRoute
{
    public const string TrainRank = "A";

    public static void Build(IReadOnlyDictionary<string, HuntMobDefinition> mobs, HuntWindowDto[] windows,
        string worldId, string expansionId, List<HuntTrainStop> into)
    {
        into.Clear();
        if (worldId.Length == 0 || expansionId.Length == 0)
        {
            return;
        }

        var zoneOrder = new List<string>();
        var mobOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mob in mobs.Values)
        {
            if (!IsTrainMark(mob, expansionId))
            {
                continue;
            }

            mobOrder[mob.Id] = mobOrder.Count;
            var zoneId = ZoneOf(mob);
            if (!zoneOrder.Contains(zoneId))
            {
                zoneOrder.Add(zoneId);
            }
        }

        if (mobOrder.Count == 0)
        {
            return;
        }

        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            if (!string.Equals(window.WorldId, worldId, StringComparison.OrdinalIgnoreCase) ||
                !mobOrder.ContainsKey(window.MobId) || !mobs.TryGetValue(window.MobId, out var mob))
            {
                continue;
            }

            into.Add(new HuntTrainStop(ZoneOf(mob), window.ZoneInstance, window.MobId, index));
        }

        into.Sort((left, right) =>
        {
            var byZone = zoneOrder.IndexOf(left.ZoneId).CompareTo(zoneOrder.IndexOf(right.ZoneId));
            if (byZone != 0)
            {
                return byZone;
            }

            var byInstance = left.ZoneInstance.CompareTo(right.ZoneInstance);
            return byInstance != 0 ? byInstance : mobOrder[left.MobId].CompareTo(mobOrder[right.MobId]);
        });
    }

    public static bool IsTrainMark(HuntMobDefinition mob, string expansionId) =>
        string.Equals(mob.Rank, TrainRank, StringComparison.Ordinal) &&
        string.Equals(mob.ExpansionId, expansionId, StringComparison.Ordinal);

    public static bool StartsLeg(List<HuntTrainStop> stops, int index) =>
        index == 0 || !string.Equals(stops[index - 1].ZoneId, stops[index].ZoneId, StringComparison.Ordinal) ||
        stops[index - 1].ZoneInstance != stops[index].ZoneInstance;

    private static string ZoneOf(HuntMobDefinition mob) => mob.ZoneIds.Length > 0 ? mob.ZoneIds[0] : string.Empty;
}
