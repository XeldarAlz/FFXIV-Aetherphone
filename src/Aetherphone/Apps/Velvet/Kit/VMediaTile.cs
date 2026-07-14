using System.Numerics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VMediaTile
{
    public static void Placeholder(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius)
    {
        Squircle.Fill(drawList, min, max, radius, VelvetTheme.Sunken.Packed());
    }

    public static void Conceal(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, string label,
        float reveal)
    {
        var alpha = 1f - Math.Clamp(reveal, 0f, 1f);
        if (alpha <= 0.001f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        Squircle.Fill(drawList, min, max, radius, VelvetTheme.Alpha(VelvetTheme.PlumWell, alpha).Packed());
        Squircle.Stroke(drawList, min, max, radius, VelvetTheme.Alpha(VelvetTheme.Rose, 0.5f * alpha).Packed(),
            Metrics.Stroke.Hairline * scale);
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f - 8f * scale);
        ProgressRing.Glow(center, 24f * scale, VelvetTheme.Alpha(VelvetTheme.Rose, 0.5f * alpha), 0.6f);
        VelvetArt.Moon(drawList, center, 11f * scale, VelvetTheme.Alpha(VelvetTheme.Moonlight, alpha),
            VelvetTheme.PlumWell, glow: false);
        Typography.DrawCentered(new Vector2(center.X, max.Y - 24f * scale), label,
            VelvetTheme.Alpha(VelvetTheme.GoldInk, alpha), TextStyles.Footnote);
    }

    public static void InReview(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius)
    {
        Squircle.Fill(drawList, min, max, radius, VelvetTheme.Scrim.Packed());
        Typography.DrawCentered(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), "In review",
            VelvetTheme.TitleInk, TextStyles.FootnoteEmphasized);
    }
}
