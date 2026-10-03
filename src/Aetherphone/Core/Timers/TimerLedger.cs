namespace Aetherphone.Core.Timers;

internal static class TimerLedger
{
    public static TimerCharacterRecord? Find(List<TimerCharacterRecord> records, ulong contentId)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index].ContentId == contentId)
            {
                return records[index];
            }
        }

        return null;
    }

    public static bool OwnedByOther(List<TimerCharacterRecord> records, ulong contentId, ulong retainerId)
    {
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.ContentId == contentId)
            {
                continue;
            }

            for (var retainerIndex = 0; retainerIndex < record.Retainers.Count; retainerIndex++)
            {
                if (record.Retainers[retainerIndex].RetainerId == retainerId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool ApplyRetainers(List<TimerCharacterRecord> records, ulong contentId, string name, string world,
        ReadOnlySpan<CapturedRetainer> captured, long nowUnix)
    {
        if (contentId == 0 || captured.Length == 0 || OwnedByOther(records, contentId, captured[0].RetainerId))
        {
            return false;
        }

        var record = Ensure(records, contentId, name, world, out var changed);
        if (!SameRetainers(record.Retainers, captured))
        {
            record.Retainers.Clear();
            for (var index = 0; index < captured.Length; index++)
            {
                var source = captured[index];
                record.Retainers.Add(new TimerRetainerRecord
                {
                    RetainerId = source.RetainerId,
                    Name = source.Name,
                    CompleteUnix = source.CompleteUnix,
                    DurationSeconds = source.DurationSeconds,
                });
            }

            changed = true;
        }

        record.RetainersSeenUnix = nowUnix;
        return changed;
    }

    public static bool ApplyMapAllowance(List<TimerCharacterRecord> records, ulong contentId, string name,
        string world, long allowanceUnix, long nowUnix)
    {
        if (contentId == 0 || allowanceUnix <= 0)
        {
            return false;
        }

        var record = Ensure(records, contentId, name, world, out var changed);
        if (record.MapAllowanceUnix != allowanceUnix)
        {
            record.MapAllowanceUnix = allowanceUnix;
            changed = true;
        }

        record.MapSeenUnix = nowUnix;
        return changed;
    }

    public static bool ApplyVessels(List<TimerWorkshopRecord> records, string key, string company, string world,
        ReadOnlySpan<CapturedVessel> captured, long nowUnix)
    {
        if (key.Length == 0 || captured.Length == 0)
        {
            return false;
        }

        var record = FindWorkshop(records, key);
        var changed = false;
        if (record is null)
        {
            record = new TimerWorkshopRecord { Key = key };
            records.Add(record);
            changed = true;
        }

        if (!string.Equals(record.Company, company, StringComparison.Ordinal) ||
            !string.Equals(record.World, world, StringComparison.Ordinal))
        {
            record.Company = company;
            record.World = world;
            changed = true;
        }

        if (!SameVessels(record.Vessels, captured))
        {
            record.Vessels.Clear();
            for (var index = 0; index < captured.Length; index++)
            {
                var source = captured[index];
                record.Vessels.Add(new TimerVesselRecord
                {
                    Name = source.Name,
                    Airship = source.Airship,
                    RegisterUnix = source.RegisterUnix,
                    ReturnUnix = source.ReturnUnix,
                });
            }

            changed = true;
        }

        record.SeenUnix = nowUnix;
        return changed;
    }

    public static TimerWorkshopRecord? FindWorkshop(List<TimerWorkshopRecord> records, string key)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (string.Equals(records[index].Key, key, StringComparison.Ordinal))
            {
                return records[index];
            }
        }

        return null;
    }

    public static string WorkshopKey(string company, string world, ulong contentId) =>
        company.Length > 0 && world.Length > 0 ? string.Concat(company, "@", world) : contentId.ToString();

    public static bool Crossed(long previousUnix, long nowUnix, long momentUnix) =>
        momentUnix > 0 && momentUnix > previousUnix && momentUnix <= nowUnix;

    public static float Progress(long startUnix, long endUnix, long nowUnix)
    {
        if (endUnix <= 0)
        {
            return 0f;
        }

        if (nowUnix >= endUnix)
        {
            return 1f;
        }

        var span = endUnix - startUnix;
        if (startUnix <= 0 || span <= 0)
        {
            return 0f;
        }

        return Math.Clamp((nowUnix - startUnix) / (float)span, 0f, 1f);
    }

    private static TimerCharacterRecord Ensure(List<TimerCharacterRecord> records, ulong contentId, string name,
        string world, out bool changed)
    {
        changed = false;
        var record = Find(records, contentId);
        if (record is null)
        {
            record = new TimerCharacterRecord { ContentId = contentId };
            records.Add(record);
            changed = true;
        }

        if (name.Length > 0 && !string.Equals(record.Name, name, StringComparison.Ordinal))
        {
            record.Name = name;
            changed = true;
        }

        if (world.Length > 0 && !string.Equals(record.World, world, StringComparison.Ordinal))
        {
            record.World = world;
            changed = true;
        }

        return record;
    }

    private static bool SameRetainers(List<TimerRetainerRecord> stored, ReadOnlySpan<CapturedRetainer> captured)
    {
        if (stored.Count != captured.Length)
        {
            return false;
        }

        for (var index = 0; index < captured.Length; index++)
        {
            var left = stored[index];
            var right = captured[index];
            if (left.RetainerId != right.RetainerId || left.CompleteUnix != right.CompleteUnix ||
                left.DurationSeconds != right.DurationSeconds ||
                !string.Equals(left.Name, right.Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameVessels(List<TimerVesselRecord> stored, ReadOnlySpan<CapturedVessel> captured)
    {
        if (stored.Count != captured.Length)
        {
            return false;
        }

        for (var index = 0; index < captured.Length; index++)
        {
            var left = stored[index];
            var right = captured[index];
            if (left.Airship != right.Airship || left.RegisterUnix != right.RegisterUnix ||
                left.ReturnUnix != right.ReturnUnix || !string.Equals(left.Name, right.Name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
