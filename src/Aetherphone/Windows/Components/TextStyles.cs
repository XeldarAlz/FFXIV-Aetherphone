using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal readonly record struct TextStyle(float Scale, FontWeight Weight);

internal static class TextStyles
{
    public static readonly TextStyle LargeTitle = new(1.90f, FontWeight.Bold);

    public static readonly TextStyle Title1 = new(1.65f, FontWeight.Bold);

    public static readonly TextStyle Title2 = new(1.32f, FontWeight.Bold);

    public static readonly TextStyle Title3 = new(1.20f, FontWeight.SemiBold);

    public static readonly TextStyle Headline = new(1.00f, FontWeight.SemiBold);

    public static readonly TextStyle Body = new(1.00f, FontWeight.Regular);

    public static readonly TextStyle BodyEmphasized = new(1.00f, FontWeight.Medium);

    public static readonly TextStyle Callout = new(0.95f, FontWeight.Regular);

    public static readonly TextStyle Subheadline = new(0.88f, FontWeight.Regular);

    public static readonly TextStyle SubheadlineEmphasized = new(0.88f, FontWeight.SemiBold);

    public static readonly TextStyle Footnote = new(0.80f, FontWeight.Regular);

    public static readonly TextStyle FootnoteEmphasized = new(0.80f, FontWeight.SemiBold);

    public static readonly TextStyle Caption1 = new(0.72f, FontWeight.Regular);

    public static readonly TextStyle Caption2 = new(0.60f, FontWeight.Medium);
}
