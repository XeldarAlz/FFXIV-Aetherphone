using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using RetainerTask = Lumina.Excel.Sheets.RetainerTask;

namespace Aetherphone.Core.Timers;

internal static unsafe class GameTimerCapture
{
    private const int SecondsPerMinute = 60;
    private const long EarliestPlausibleUnix = 1_500_000_000L;

    public static int ReadRetainers(CapturedRetainer[] into, ExcelSheet<RetainerTask>? tasks)
    {
        var manager = RetainerManager.Instance();
        if (manager is null || !manager->IsReady)
        {
            return 0;
        }

        var count = Math.Min((int)manager->GetRetainerCount(), into.Length);
        var written = 0;
        for (var index = 0; index < count; index++)
        {
            var retainer = manager->GetRetainerBySortedIndex((uint)index);
            if (retainer is null || retainer->RetainerId == 0)
            {
                continue;
            }

            var ventureId = retainer->VentureId;
            var complete = ventureId != 0 ? (long)retainer->VentureComplete : 0L;
            into[written] = new CapturedRetainer(retainer->RetainerId, retainer->NameString, complete,
                DurationOf(tasks, ventureId));
            written++;
        }

        return written;
    }

    public static int ReadVessels(CapturedVessel[] into)
    {
        var housing = HousingManager.Instance();
        if (housing is null || housing->WorkshopTerritory is null)
        {
            return -1;
        }

        var workshop = housing->WorkshopTerritory;
        var written = 0;
        var submersibles = workshop->Submersible.Data;
        for (var index = 0; index < submersibles.Length && written < into.Length; index++)
        {
            ref var vessel = ref submersibles[index];
            if (vessel.RankId == 0)
            {
                continue;
            }

            into[written] = new CapturedVessel(vessel.NameString, false, vessel.RegisterTime, vessel.ReturnTime);
            written++;
        }

        var airships = workshop->Airship.Data;
        for (var index = 0; index < airships.Length && written < into.Length; index++)
        {
            ref var vessel = ref airships[index];
            if (vessel.RankId == 0)
            {
                continue;
            }

            into[written] = new CapturedVessel(vessel.NameString, true, vessel.RegisterTime, vessel.ReturnTime);
            written++;
        }

        return written;
    }

    public static long ReadMapAllowance()
    {
        var state = UIState.Instance();
        if (state is null)
        {
            return 0;
        }

        var timestamp = (long)state->GetNextMapAllowanceTimestamp();
        return timestamp >= EarliestPlausibleUnix ? timestamp : 0;
    }

    private static int DurationOf(ExcelSheet<RetainerTask>? tasks, uint ventureId)
    {
        if (ventureId == 0 || tasks is null || !tasks.TryGetRow(ventureId, out var task))
        {
            return 0;
        }

        return task.MaxTimemin * SecondsPerMinute;
    }
}
