using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TradeSyncCard
{
    private const int HostRows = 1;
    private const int PlayerRows = 2;
    private const string SyncId = "casino.trade.sync";
    private const string AutoId = "casino.trade.auto";

    public static void Draw(AppSkin ui, CasinoTradeSync trade, float scale, bool overlay, bool host)
    {
        var card = GroupCard.Begin(ui, host ? HostRows : PlayerRows, VenueFields.RowUnits);
        var enabled = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), SyncId,
            Loc.T(L.Venue.TradeSync), trade.Enabled, scale, overlay);
        if (enabled != trade.Enabled)
        {
            trade.Enabled = enabled;
        }

        if (!host)
        {
            var auto = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), AutoId,
                Loc.T(L.Venue.TradeAutoConfirm), trade.AutoConfirm, scale, overlay);
            if (auto != trade.AutoConfirm)
            {
                trade.AutoConfirm = auto;
            }
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(L.Venue.TradeSyncHint), scale);
    }
}
