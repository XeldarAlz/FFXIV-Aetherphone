using Aetherphone.Core.Housing;

namespace Aetherphone.Apps.Housing;

internal enum HousingRoute : byte
{
    Root,
    Details,
    Settings,
    WorldPicker,
}

internal enum HousingTab : byte
{
    Overview,
    Map,
    Plots,
    Watchlist,
}

internal sealed class HousingView
{
    public static readonly HousingView Root = new(HousingRoute.Root, default, string.Empty);

    public HousingView(HousingRoute route, HousingPlotKey plot, string backTitle)
    {
        Route = route;
        Plot = plot;
        BackTitle = backTitle;
    }

    public HousingRoute Route { get; }
    public HousingPlotKey Plot { get; }
    public string BackTitle { get; }
}
