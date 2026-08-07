using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class GearsetActions
{
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
