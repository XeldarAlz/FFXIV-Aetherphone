using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Home;

internal enum WidgetMode : byte
{
    FullColor,
    Dark,
    Tinted,
    Clear,
}

internal static class WidgetModes
{
    public static WidgetMode From(IconAppearance appearance) => appearance switch
    {
        IconAppearance.Dark => WidgetMode.Dark,
        IconAppearance.Tinted => WidgetMode.Tinted,
        IconAppearance.Clear => WidgetMode.Clear,
        _ => WidgetMode.FullColor,
    };
}
