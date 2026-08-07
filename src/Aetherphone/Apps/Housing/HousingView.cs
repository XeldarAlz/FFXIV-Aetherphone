using Aetherphone.Core.Housing;

namespace Aetherphone.Apps.Housing;

internal enum HousingRoute : byte
{
    Map,
    List,
    Watchlist,
    Details,
    Settings,
    WorldPicker,
}

internal sealed class HousingView
{
    public static readonly HousingView Root = new(HousingRoute.Map);

    public HousingView(HousingRoute route, HousingPlotKey plot = default)
    {
        Route = route;
        Plot = plot;
    }

    public HousingRoute Route { get; }
    public HousingPlotKey Plot { get; }
}
