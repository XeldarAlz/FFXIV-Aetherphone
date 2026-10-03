namespace Aetherphone.Core.Timers;

internal readonly record struct RunningTimer(string Name, bool Voyage, bool Airship, long StartUnix, long EndUnix)
{
    public bool Exists => EndUnix > 0;
}

internal readonly record struct TimerTally(int ReadyVentures, int ReadyVoyages, RunningTimer Soonest)
{
    public int Ready => ReadyVentures + ReadyVoyages;
}

internal static class TimerBoard
{
    public const long IslandHorizonSeconds = 600;

    public static TimerTally Tally(List<TimerCharacterRecord> characters, List<TimerWorkshopRecord> workshops,
        long nowUnix)
    {
        var readyVentures = 0;
        var readyVoyages = 0;
        var soonest = default(RunningTimer);
        for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
        {
            var retainers = characters[characterIndex].Retainers;
            for (var index = 0; index < retainers.Count; index++)
            {
                var retainer = retainers[index];
                if (retainer.CompleteUnix <= 0)
                {
                    continue;
                }

                if (retainer.CompleteUnix <= nowUnix)
                {
                    readyVentures++;
                    continue;
                }

                if (!soonest.Exists || retainer.CompleteUnix < soonest.EndUnix)
                {
                    var start = retainer.DurationSeconds > 0 ? retainer.CompleteUnix - retainer.DurationSeconds : 0;
                    soonest = new RunningTimer(retainer.Name, false, false, start, retainer.CompleteUnix);
                }
            }
        }

        for (var workshopIndex = 0; workshopIndex < workshops.Count; workshopIndex++)
        {
            var vessels = workshops[workshopIndex].Vessels;
            for (var index = 0; index < vessels.Count; index++)
            {
                var vessel = vessels[index];
                if (vessel.ReturnUnix <= 0)
                {
                    continue;
                }

                if (vessel.ReturnUnix <= nowUnix)
                {
                    readyVoyages++;
                    continue;
                }

                if (!soonest.Exists || vessel.ReturnUnix < soonest.EndUnix)
                {
                    soonest = new RunningTimer(vessel.Name, true, vessel.Airship, vessel.RegisterUnix,
                        vessel.ReturnUnix);
                }
            }
        }

        return new TimerTally(readyVentures, readyVoyages, soonest);
    }

    public static bool InIslandWindow(in RunningTimer timer, long nowUnix) =>
        timer.Exists && timer.EndUnix > nowUnix && timer.EndUnix - nowUnix <= IslandHorizonSeconds;

    public static void OrderCharacters(List<TimerCharacterRecord> source, ulong currentContentId,
        List<TimerCharacterRecord> destination)
    {
        destination.Clear();
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index].Retainers.Count > 0)
            {
                destination.Add(source[index]);
            }
        }

        for (var index = 1; index < destination.Count; index++)
        {
            var current = destination[index];
            var target = index - 1;
            while (target >= 0 && Before(current, destination[target], currentContentId))
            {
                destination[target + 1] = destination[target];
                target--;
            }

            destination[target + 1] = current;
        }
    }

    private static bool Before(TimerCharacterRecord left, TimerCharacterRecord right, ulong currentContentId)
    {
        var leftCurrent = left.ContentId == currentContentId;
        var rightCurrent = right.ContentId == currentContentId;
        if (leftCurrent != rightCurrent)
        {
            return leftCurrent;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) < 0;
    }
}
