using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal readonly record struct GilLedgerTotals(long In, long Out, int Settled)
{
    public static readonly GilLedgerTotals Empty = new(0, 0, 0);

    public static GilLedgerTotals Of(CasinoLedgerEntryDto[]? entries, string myUserId)
    {
        if (entries is null || myUserId.Length == 0)
        {
            return Empty;
        }

        var received = 0L;
        var paid = 0L;
        var settled = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (!entry.Settled)
            {
                continue;
            }

            switch (CasinoGilLedger.SideOf(entry, myUserId))
            {
                case LedgerSide.Payee:
                    received += entry.Amount;
                    settled++;
                    break;
                case LedgerSide.Payer:
                    paid += entry.Amount;
                    settled++;
                    break;
            }
        }

        return new GilLedgerTotals(received, paid, settled);
    }
}
