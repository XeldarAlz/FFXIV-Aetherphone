using Aetherphone.Core;
using Aetherphone.Core.Home;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetMetrics
{
    public const float Radius = 22f;
    public const float Margin = 15f;
    public const float MarginWide = 16f;
    public const float Gutter = 8f;
    public const float RowGap = 3f;
    public const float GlyphSmall = 16f;
    public const float InnerRadiusMinimum = 6f;
    public const float ControlSmall = 28f;
    public const float ControlLarge = 44f;

    public static float MarginFor(WidgetSize size) => size == WidgetSize.Small ? Margin : MarginWide;

    public static float InnerRadius(float insetUnits) => MathF.Max(InnerRadiusMinimum, Radius - insetUnits);

    public static float InnerRadius(in WidgetContext context) =>
        InnerRadius(MarginFor(context.Size)) * context.Scale;

    public static float ContainerRadius(float scale) => Radius * scale;

    public static Rect Content(in WidgetContext context)
    {
        var inset = MarginFor(context.Size) * context.Scale;
        var bounds = context.Bounds;
        return new Rect(new Vector2(bounds.Min.X + inset, bounds.Min.Y + inset),
            new Vector2(bounds.Max.X - inset, bounds.Max.Y - inset));
    }

    public static Rect Below(in WidgetContext context, float top)
    {
        var content = Content(context);
        return new Rect(new Vector2(content.Min.X, MathF.Min(MathF.Max(top, content.Min.Y), content.Max.Y)),
            content.Max);
    }
}
