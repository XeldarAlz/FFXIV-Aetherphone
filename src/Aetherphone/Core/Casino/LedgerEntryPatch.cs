using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal static class LedgerEntryPatch
{
    public static int IndexOf(CasinoLedgerEntryDto[] entries, string entryId)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            if (string.Equals(entries[index].EntryId, entryId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static CasinoLedgerEntryDto[] Upsert(CasinoLedgerEntryDto[] entries, CasinoLedgerEntryDto entry,
        bool newestFirst)
    {
        var existing = IndexOf(entries, entry.EntryId);
        if (existing >= 0)
        {
            var replaced = (CasinoLedgerEntryDto[])entries.Clone();
            replaced[existing] = entry;
            return replaced;
        }

        var grown = new CasinoLedgerEntryDto[entries.Length + 1];
        if (newestFirst)
        {
            grown[0] = entry;
            Array.Copy(entries, 0, grown, 1, entries.Length);
            return grown;
        }

        Array.Copy(entries, grown, entries.Length);
        grown[entries.Length] = entry;
        return grown;
    }

    public static CasinoLedgerEntryDto[] Remove(CasinoLedgerEntryDto[] entries, string entryId)
    {
        var existing = IndexOf(entries, entryId);
        if (existing < 0)
        {
            return entries;
        }

        var shrunk = new CasinoLedgerEntryDto[entries.Length - 1];
        Array.Copy(entries, 0, shrunk, 0, existing);
        Array.Copy(entries, existing + 1, shrunk, existing, entries.Length - existing - 1);
        return shrunk;
    }
}
