using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperRenderer
{
    public static void Draw(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry light,
        WallpaperEntry dark, float aspect, float darkness, Vector4 fallback)
    {
        DrawSingle(drawList, shape, quad, radius, light, aspect, 1f, fallback);
        if (darkness > 0.001f)
        {
            DrawSingle(drawList, shape, quad, radius, dark, aspect, darkness, null);
        }
    }

    public static void DrawSingle(ImDrawListPtr drawList, Rect rect, float radius, WallpaperEntry entry, float aspect,
        float alpha, Vector4? fallback) =>
        DrawSingle(drawList, rect, rect, radius, entry, aspect, alpha, fallback);

    public static void DrawSingle(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry entry,
        float aspect, float alpha, Vector4? fallback)
    {
        var library = Plugin.Wallpapers;
        if (library.HandlePath(entry.FilePath) is not { } handle)
        {
            if (fallback is { } color)
            {
                Squircle.Fill(drawList, shape.Min, shape.Max, radius, ImGui.GetColorU32(color));
            }

            return;
        }

        var (uv0, uv1) = entry.Crop.ComputeUv(library.SizeOfPath(entry.FilePath), aspect);
        var tint = alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));

        var clipped = quad.Min != shape.Min || quad.Max != shape.Max;
        if (clipped)
        {
            drawList.PushClipRect(shape.Min, shape.Max, true);
        }

        drawList.AddImage(handle, quad.Min, quad.Max, uv0, uv1, tint);
        if (clipped)
        {
            drawList.PopClipRect();
        }
    }
}
