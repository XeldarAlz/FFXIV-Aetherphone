using Aetherphone.Core.Emoji;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class EmojiRender
{
    private const float SideScale = 1.2f;
    private const float GapScale = 0.12f;
    private const float MissingInsetScale = 0.14f;
    private const float MissingInkAlpha = 0.45f;

    public static float Advance(float fontSize) => fontSize * (SideScale + GapScale);

    public static float LineHeight(float fontSize) => fontSize * SideScale;

    public static void Draw(ImDrawListPtr drawList, string file, Vector2 topLeft, float fontSize, float alpha)
    {
        Frame(topLeft, fontSize, out var min, out var max);
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        EmojiImages.TryDraw(drawList, file, min, max, tint);
    }

    public static void Draw(ImDrawListPtr drawList, string file, Vector2 topLeft, float fontSize, Vector4 ink,
        float alpha)
    {
        if (!EmojiScanner.IsMissing(file))
        {
            Draw(drawList, file, topLeft, fontSize, alpha);
            return;
        }

        Frame(topLeft, fontSize, out var min, out var max);
        var inset = (max.X - min.X) * MissingInsetScale;
        var boxMin = min + new Vector2(inset, inset);
        var boxMax = max - new Vector2(inset, inset);
        var color = ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha * MissingInkAlpha));
        Squircle.Stroke(drawList, boxMin, boxMax, (boxMax.X - boxMin.X) * Metrics.Radius.TileFactor, color,
            MathF.Max(1f, Metrics.Stroke.Thin * UiScale.Current));
    }

    private static void Frame(Vector2 topLeft, float fontSize, out Vector2 min, out Vector2 max)
    {
        var side = fontSize * SideScale;
        var top = topLeft.Y + (fontSize - side) * 0.5f;
        min = new Vector2(topLeft.X, top);
        max = new Vector2(topLeft.X + side, top + side);
    }
}
