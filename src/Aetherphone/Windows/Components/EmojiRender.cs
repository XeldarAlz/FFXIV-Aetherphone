using Aetherphone.Core.Emoji;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class EmojiRender
{
    private const float SideScale = 1.2f;
    private const float GapScale = 0.12f;

    public static float Advance(float fontSize) => fontSize * (SideScale + GapScale);

    public static float LineHeight(float fontSize) => fontSize * SideScale;

    public static void Draw(ImDrawListPtr drawList, string file, Vector2 topLeft, float fontSize, float alpha)
    {
        var side = fontSize * SideScale;
        var top = topLeft.Y + (fontSize - side) * 0.5f;
        var min = new Vector2(topLeft.X, top);
        var max = new Vector2(topLeft.X + side, top + side);
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        EmojiImages.TryDraw(drawList, file, min, max, tint);
    }
}
