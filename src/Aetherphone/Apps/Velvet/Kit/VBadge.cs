using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VBadge
{
    public static void Count(ImDrawListPtr drawList, Vector2 rightCenter, int count, bool danger = false)
    {
        if (count <= 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var text = count > 99 ? "99+" : count.ToString();
        var textSize = Typography.Measure(text, TextStyles.Caption1);
        var height = 18f * scale;
        var width = MathF.Max(height, textSize.X + 10f * scale);
        var min = new Vector2(rightCenter.X - width, rightCenter.Y - height * 0.5f);
        var max = new Vector2(rightCenter.X, rightCenter.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, (danger ? VelvetTheme.Danger : VelvetTheme.Rose).Packed());
        Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), text, VelvetTheme.OnAccent,
            TextStyles.Caption1);
    }

    public static void Dot(ImDrawListPtr drawList, Vector2 center, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        drawList.AddCircleFilled(center, 3.5f * scale + 1.5f * scale, VelvetTheme.GroundBottom.Packed(), 16);
        drawList.AddCircleFilled(center, 3.5f * scale, color.Packed(), 16);
    }
}
