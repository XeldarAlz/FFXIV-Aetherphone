using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.VenueSync;

internal static class SaleSummaryRow
{
    public const float Height = 40f;

    public static void Draw(Rect row, int count, decimal total, PhoneTheme theme, Vector4 accent)
    {
        var text = count == 0
            ? Loc.T(L.VenueSync.NoSalesLoggedThisSession)
            : $"{Loc.Plural(L.VenueSync.SaleCount, count)} · {Loc.T(L.VenueSync.PriceGil, total.ToString("N0", Loc.Culture))}";
        var color = count == 0 ? theme.TextStrong : accent;
        Marquee.DrawLeftAuto("sale-summary", text, row.Min.X, row.Min.Y, row.Width, TextStyles.Body, color);
    }
}
