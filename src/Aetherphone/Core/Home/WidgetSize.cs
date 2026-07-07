namespace Aetherphone.Core.Home;

internal enum WidgetSize
{
    Small,
    Medium,
    Large,
}

[Flags]
internal enum WidgetSizeSet
{
    None = 0,
    Small = 1,
    Medium = 2,
    Large = 4,
}

internal static class WidgetSizes
{
    public static int ColumnSpan(WidgetSize size) => size == WidgetSize.Small ? 2 : 4;
    public static int RowSpan(WidgetSize size) => size == WidgetSize.Large ? 4 : 2;

    public static bool Contains(WidgetSizeSet set, WidgetSize size) => (set & FlagFor(size)) != 0;

    public static WidgetSize Smallest(WidgetSizeSet set)
    {
        if ((set & WidgetSizeSet.Small) != 0)
        {
            return WidgetSize.Small;
        }

        return (set & WidgetSizeSet.Medium) != 0 ? WidgetSize.Medium : WidgetSize.Large;
    }

    public static string Serialize(WidgetSize size) => size switch
    {
        WidgetSize.Small => "small",
        WidgetSize.Large => "large",
        _ => "medium",
    };

    public static WidgetSize Parse(string value) => value switch
    {
        "small" => WidgetSize.Small,
        "large" => WidgetSize.Large,
        _ => WidgetSize.Medium,
    };

    private static WidgetSizeSet FlagFor(WidgetSize size) => size switch
    {
        WidgetSize.Small => WidgetSizeSet.Small,
        WidgetSize.Large => WidgetSizeSet.Large,
        _ => WidgetSizeSet.Medium,
    };
}
