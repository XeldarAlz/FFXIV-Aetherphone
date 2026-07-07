namespace Aetherphone.Core.ControlCenter;

internal enum ControlSpan : byte
{
    Small,
    Wide,
    Tall,
    Large,
    Bar,
}

internal static class ControlSpans
{
    public static int ColumnSpan(ControlSpan span) => span switch
    {
        ControlSpan.Small => 1,
        ControlSpan.Wide => 2,
        ControlSpan.Tall => 1,
        ControlSpan.Large => 2,
        ControlSpan.Bar => 4,
        _ => 1,
    };

    public static int RowSpan(ControlSpan span) => span switch
    {
        ControlSpan.Small => 1,
        ControlSpan.Wide => 1,
        ControlSpan.Tall => 2,
        ControlSpan.Large => 2,
        ControlSpan.Bar => 1,
        _ => 1,
    };

    public static bool Contains(IReadOnlyList<ControlSpan> spans, ControlSpan span)
    {
        for (var index = 0; index < spans.Count; index++)
        {
            if (spans[index] == span)
            {
                return true;
            }
        }

        return false;
    }

    public static ControlSpan Next(IReadOnlyList<ControlSpan> spans, ControlSpan current)
    {
        if (spans.Count <= 1)
        {
            return current;
        }

        for (var index = 0; index < spans.Count; index++)
        {
            if (spans[index] == current)
            {
                return spans[(index + 1) % spans.Count];
            }
        }

        return spans[0];
    }

    public static ControlSpan First(IReadOnlyList<ControlSpan> spans) => spans.Count > 0 ? spans[0] : ControlSpan.Small;

    public static string Serialize(ControlSpan span) => span switch
    {
        ControlSpan.Wide => "wide",
        ControlSpan.Tall => "tall",
        ControlSpan.Large => "large",
        ControlSpan.Bar => "bar",
        _ => "small",
    };

    public static ControlSpan Parse(string value) => value switch
    {
        "wide" => ControlSpan.Wide,
        "tall" => ControlSpan.Tall,
        "large" => ControlSpan.Large,
        "bar" => ControlSpan.Bar,
        _ => ControlSpan.Small,
    };
}
