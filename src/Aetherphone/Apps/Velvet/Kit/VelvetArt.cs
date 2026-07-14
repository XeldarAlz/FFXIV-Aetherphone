using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VelvetArt
{
    public static void Moon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 light, Vector4 carve,
        bool glow = true)
    {
        if (glow)
        {
            drawList.AddCircleFilled(center, radius * 2.1f, VelvetTheme.MoonGlow.Packed(), 40);
        }

        drawList.AddCircleFilled(center, radius, light.Packed(), 44);
        drawList.AddCircleFilled(new Vector2(center.X + radius * 0.42f, center.Y - radius * 0.34f), radius,
            carve.Packed(), 44);
    }

    public static void Bloom(ImDrawListPtr drawList, Rect area, float strength = 1f)
    {
        var top = new Vector2(area.Min.X, area.Min.Y + area.Height * 0.42f);
        Squircle.FillVerticalGradient(drawList, top, area.Max, 0f, VelvetTheme.Alpha(VelvetTheme.BloomTop, 0f).Packed(),
            VelvetTheme.Alpha(VelvetTheme.BloomTop, 0.22f * strength).Packed());
    }

    public static void Wordmark(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var textSize = Typography.Measure("Velvet", TextStyles.Title2);
        var min = new Vector2(center.X - textSize.X * 0.5f, center.Y - textSize.Y * 0.5f);
        Typography.Draw(min, "Velvet", VelvetTheme.TitleInk, TextStyles.Title2);
        Moon(drawList, new Vector2(min.X + textSize.X + 8f * scale, min.Y + 4f * scale), 4f * scale,
            VelvetTheme.Moonlight, VelvetTheme.GroundTop);
        var underY = min.Y + textSize.Y + 2f * scale;
        drawList.AddLine(new Vector2(min.X + 2f * scale, underY), new Vector2(min.X + textSize.X - 2f * scale, underY),
            VelvetTheme.Alpha(VelvetTheme.Rose, 0.7f).Packed(), 1f * scale);
    }

    public static void MoonriseHeader(ImDrawListPtr drawList, Rect area, float scale)
    {
        var moonCenter = new Vector2(area.Max.X - 34f * scale, area.Min.Y + 20f * scale);
        Moon(drawList, moonCenter, 7f * scale, VelvetTheme.Moonlight, VelvetTheme.GroundTop);
    }
}
