using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TradeSyncCard
{
    private const int Rows = 1;
    private const string SyncId = "casino.trade.sync";

    public static void Draw(AppSkin ui, CasinoTradeSync trade, float scale, bool overlay)
    {
        var card = GroupCard.Begin(ui, Rows, VenueFields.RowUnits);
        var enabled = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), SyncId,
            Loc.T(L.Venue.TradeSync), trade.Enabled, scale, overlay);
        if (enabled != trade.Enabled)
        {
            trade.Enabled = enabled;
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(L.Venue.TradeSyncHint), scale);
    }
}
