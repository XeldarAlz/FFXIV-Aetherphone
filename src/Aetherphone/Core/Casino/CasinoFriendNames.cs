using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Casino;

internal sealed unsafe class CasinoFriendNames
{
    private const long RefreshMilliseconds = 15_000;

    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
    private long readAtTick;

    public IReadOnlySet<string> Names => names;

    public int Count => names.Count;

    public void Refresh()
    {
        var now = Environment.TickCount64;
        if (readAtTick != 0 && now - readAtTick < RefreshMilliseconds)
        {
            return;
        }

        readAtTick = now;
        var proxy = InfoProxyFriendList.Instance();
        if (proxy == null)
        {
            return;
        }

        names.Clear();
        var count = proxy->EntryCount;
        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry == null)
            {
                continue;
            }

            var name = entry->NameString;
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
    }
}
