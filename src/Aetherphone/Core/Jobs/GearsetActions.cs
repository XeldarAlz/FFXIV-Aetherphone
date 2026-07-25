using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class GearsetActions
{
    /// <summary>
    /// Equipping a gearset is the only way the client changes class, crafters and gatherers included: they own
    /// gearsets just like combat jobs do.
    /// </summary>
    public static bool Equip(int gearsetId)
    {
        var module = RaptureGearsetModule.Instance();
        if (module is null || !module->IsValidGearset(gearsetId))
        {
            return false;
        }

        return module->EquipGearset(gearsetId) == 0;
    }
}
