using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal enum VCardStyle
{
    Plain,
    Elevated,
    Inset,
}

internal static class VCard
{
    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, VCardStyle style,
        float hover = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (style == VCardStyle.Inset)
        {
            Squircle.Fill(drawList, min, max, radius, VelvetTheme.Sunken.Packed());
            Squircle.Stroke(drawList, min, max, radius, VelvetTheme.Divider.Packed(), Metrics.Stroke.Hairline * scale);
            return;
        }

        if (style == VCardStyle.Elevated)
        {
            Elevation.Card(drawList, min, max, radius, scale, 0.9f);
        }

        var top = Vector4.Lerp(VelvetTheme.CardHi, VelvetTheme.Lerp(VelvetTheme.CardHi, VelvetTheme.RoseShadow, 0.12f),
            hover);
        var bottom = Vector4.Lerp(VelvetTheme.Card, VelvetTheme.CardHi, 0.35f * hover);
        Squircle.FillVerticalGradient(drawList, min, max, radius, top.Packed(), bottom.Packed());
        Squircle.Stroke(drawList, min, max, radius, VelvetTheme.CardStroke.Packed(), Metrics.Stroke.Hairline * scale);
        Sheen(drawList, min, max, scale);
    }

    public static void Sheen(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var inset = 12f * scale;
        var y = min.Y + 1.5f * scale;
        drawList.AddLine(new Vector2(min.X + inset, y), new Vector2(max.X - inset, y), VelvetTheme.Sheen.Packed(),
            1f * scale);
    }
}
