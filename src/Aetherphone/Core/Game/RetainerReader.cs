using FFXIVClientStructs.FFXIV.Client.Game;

namespace Aetherphone.Core.Game;

internal static unsafe class RetainerReader
{
    public static bool TryRead(List<RetainerVenture> into)
    {
        into.Clear();
        var manager = RetainerManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var count = manager->GetRetainerCount();
        if (count == 0)
        {
            return false;
        }

        for (var index = 0u; index < count; index++)
        {
            var retainer = manager->GetRetainerBySortedIndex(index);
            if (retainer is null)
            {
                continue;
            }

            var hasVenture = retainer->VentureId != 0;
            var complete = hasVenture
                ? DateTimeOffset.FromUnixTimeSeconds(retainer->VentureComplete).UtcDateTime
                : DateTime.MinValue;
            into.Add(new RetainerVenture(retainer->RetainerId, retainer->NameString, hasVenture, complete));
        }

        return into.Count > 0;
    }
}
