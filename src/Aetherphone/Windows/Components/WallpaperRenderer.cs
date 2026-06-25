using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperRenderer
{
    public static void Draw(ImDrawListPtr drawList, Rect rect, float rounding, WallpaperEntry light, WallpaperEntry dark, float aspect, float darkness, Vector4 fallback)
    {
        DrawSingle(drawList, rect, rounding, light, aspect, 1f, fallback);
        if (darkness > 0.001f)
        {
            DrawSingle(drawList, rect, rounding, dark, aspect, darkness, null);
        }
    }

    public static void DrawSingle(ImDrawListPtr drawList, Rect rect, float rounding, WallpaperEntry entry, float aspect, float alpha, Vector4? fallback)
    {
        var library = Plugin.Wallpapers;
        if (library.HandlePath(entry.FilePath) is not { } handle)
        {
            if (fallback is { } color)
            {
                drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(color), rounding);
            }

            return;
        }

        var (uv0, uv1) = entry.Crop.ComputeUv(library.SizeOfPath(entry.FilePath), aspect);
        var tint = alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        drawList.AddImageRounded(handle, rect.Min, rect.Max, uv0, uv1, tint, rounding, ImDrawFlags.RoundCornersAll);
    }
}
