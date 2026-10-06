using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum CellDepth : byte
{
    Flat,
    Raised,
    Sunken,
    Pressed,
}

internal static class StageCell
{
    public const float MaxLift = 3f;
    private const float ShadowAlpha = 0.28f;
    private const float ShadowOffset = 2f;
    private const float HighlightAlpha = 0.14f;
    private const float InnerShadowAlpha = 0.22f;
    private const float InnerShadowCoverage = 0.42f;
    private const float PressedShrink = 0.04f;

    public static void Draw(ImDrawListPtr drawList, Rect rect, Vector4 fill, CellDepth depth, float radius, float scale)
    {
        if (depth == CellDepth.Pressed)
        {
            var shrink = rect.Size * PressedShrink * 0.5f;
            rect = new Rect(rect.Min + shrink, rect.Max - shrink);
            depth = CellDepth.Raised;
        }

        if (depth == CellDepth.Raised)
        {
            var offset = new Vector2(0f, ShadowOffset * scale);
            Squircle.Fill(drawList, rect.Min + offset, rect.Max + offset, radius,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, ShadowAlpha)));
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        switch (depth)
        {
            case CellDepth.Raised:
                Material.Sheen(drawList, rect.Min, rect.Max, radius,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, HighlightAlpha)), 1f * scale, 1f * scale);
                break;
            case CellDepth.Sunken:
                Material.SheenBlock(drawList, rect.Min, rect.Max, radius,
                    ImGui.GetColorU32(new Vector4(0f, 0f, 0f, InnerShadowAlpha)), 0f, InnerShadowCoverage);
                break;
            default:
                break;
        }
    }

    public static float Lift(float progress) => (1f - Easing.Clamp01(progress)) * MaxLift;
}
