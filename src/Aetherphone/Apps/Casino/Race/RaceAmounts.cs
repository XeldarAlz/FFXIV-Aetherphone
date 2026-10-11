using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal static class RaceAmounts
{
    public static string Text(long amount) => NumberText.Compact(amount);

    public static Vector2 Measure(string text, in TextStyle style) => CurrencyGlyph.MeasureAmount(text, style);

    public static Vector2 Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 ink, in TextStyle style,
        float alpha = 1f) =>
        CurrencyGlyph.DrawAmount(drawList, position, text, CurrencyKind.Chips, ink, style, alpha);

    public static void DrawRight(ImDrawListPtr drawList, float right, float centerY, string text, Vector4 ink,
        in TextStyle style, float alpha = 1f)
    {
        var size = Measure(text, style);
        Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), text, ink, style, alpha);
    }
}
