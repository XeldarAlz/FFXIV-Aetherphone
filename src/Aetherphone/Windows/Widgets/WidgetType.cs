using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetType
{
    public const float EyebrowTracking = 1.6f;
    public const float MinimumFit = 0.85f;

    public static readonly TextStyle Display = TextStyles.WidgetDisplay;
    public static readonly TextStyle DisplayCompact = TextStyles.WidgetDisplayCompact;
    public static readonly TextStyle Title = new(1.05f, FontWeight.SemiBold);
    public static readonly TextStyle Headline = new(0.86f, FontWeight.SemiBold);
    public static readonly TextStyle Body = new(0.82f, FontWeight.Regular);
    public static readonly TextStyle Caption = new(0.72f, FontWeight.Medium);
    public static readonly TextStyle Eyebrow = new(0.66f, FontWeight.SemiBold);

    public static TextStyle Hero(WidgetSize size) => size == WidgetSize.Small ? DisplayCompact : Display;
}
