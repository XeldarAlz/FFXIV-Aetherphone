using Aetherphone.Core.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal enum GearsetEquipResult : byte
{
    Sent,
    Busy,
    Missing,
    Failed,
}

internal static unsafe class GearsetActions
{
    public static bool CanChangeNow
    {
        get
        {
            var condition = Plugin.Condition;
            return !condition[ConditionFlag.InCombat] && !condition[ConditionFlag.BetweenAreas] &&
                   !condition[ConditionFlag.BetweenAreas51] && !condition[ConditionFlag.WatchingCutscene] &&
                   !condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.Casting];
        }
    }

    public static uint CurrentClassJobId
    {
        get
        {
            if (!GameMemory.Attached)
            {
                return GameMemory.Simulated?.CurrentClassJobId ?? 0u;
            }

            var playerState = PlayerState.Instance();
            return playerState is null || !playerState->IsLoaded ? 0u : playerState->CurrentClassJobId;
        }
    }

    public static int FirstGearsetFor(uint classJobId)
    {
        if (!GameMemory.Attached)
        {
            return FirstSimulatedGearsetFor(classJobId);
        }

        var module = RaptureGearsetModule.Instance();
        if (module is null)
        {
            return -1;
        }

        var entries = module->Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            ref var entry = ref entries[index];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) != 0 && entry.ClassJob == classJobId)
            {
                return entry.Id;
            }
        }

        return -1;
    }

    public static GearsetEquipResult Equip(int gearsetId)
    {
        if (gearsetId < 0)
        {
            return GearsetEquipResult.Missing;
        }

        if (!CanChangeNow)
        {
            return GearsetEquipResult.Busy;
        }

        if (Plugin.Framework.IsInFrameworkUpdateThread)
        {
            return EquipNow(gearsetId);
        }

        _ = Plugin.Framework.RunOnFrameworkThread(() => EquipNow(gearsetId));
        return GearsetEquipResult.Sent;
    }

    private static GearsetEquipResult EquipNow(int gearsetId)
    {
        if (!GameMemory.Attached)
        {
            return EquipSimulated(gearsetId);
        }

        try
        {
            var module = RaptureGearsetModule.Instance();
            if (module is null || !module->IsValidGearset(gearsetId))
            {
                return GearsetEquipResult.Missing;
            }

            return module->EquipGearset(gearsetId) == 0 ? GearsetEquipResult.Sent : GearsetEquipResult.Failed;
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning(exception, "Jobs: equipping gearset {0} failed", gearsetId);
            return GearsetEquipResult.Failed;
        }
    }

    private static int FirstSimulatedGearsetFor(uint classJobId)
    {
        if (GameMemory.Simulated is not { } simulated)
        {
            return -1;
        }

        var gearsets = simulated.Gearsets;
        for (var index = 0; index < gearsets.Length; index++)
        {
            if (gearsets[index].ClassJobId == classJobId)
            {
                return gearsets[index].Id;
            }
        }

        return -1;
    }

    private static GearsetEquipResult EquipSimulated(int gearsetId)
    {
        if (GameMemory.Simulated is not { } simulated)
        {
            return GearsetEquipResult.Failed;
        }

        if (!simulated.TryGetGearset(gearsetId, out var gearset))
        {
            return GearsetEquipResult.Missing;
        }

        simulated.ActiveGearsetId = gearset.Id;
        simulated.CurrentClassJobId = gearset.ClassJobId;
        GameMemory.SimulateAction($"equip gearset {gearset.Id} ({gearset.Name})");
        return GearsetEquipResult.Sent;
    }
}
