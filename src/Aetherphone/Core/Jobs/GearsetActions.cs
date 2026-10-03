using Dalamud.Game.ClientState.Conditions;
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
}
