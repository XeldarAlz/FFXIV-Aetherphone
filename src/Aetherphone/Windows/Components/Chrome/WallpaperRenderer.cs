using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperRenderer
{
    public static void Draw(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry light,
        WallpaperEntry dark, float aspect, float darkness, Vector4 fallback, float blur)
    {
        DrawSingle(drawList, shape, quad, radius, light, aspect, 1f, fallback);
        if (darkness > 0.001f)
        {
            DrawSingle(drawList, shape, quad, radius, dark, aspect, darkness, null);
        }

        RecordBackdrop(quad, light, dark, aspect, darkness);
        if (blur <= 0.001f)
        {
            return;
        }

        DrawBlurred(drawList, shape, quad, light, aspect, blur);
        if (darkness > 0.001f)
        {
            DrawBlurred(drawList, shape, quad, dark, aspect, blur * darkness);
        }
    }

    public static void RecordBackdrop(Rect quad, WallpaperEntry light, WallpaperEntry dark, float aspect,
        float darkness)
    {
        var library = Plugin.Wallpapers;
        if (!library.TryGetBlurred(light.FilePath, out var lightHandle, out var lightSize))
        {
            return;
        }

        var (lightUv0, lightUv1) = light.Crop.ComputeUv(lightSize, aspect);
        WallpaperBackdrop.Record(quad, lightHandle, lightUv0, lightUv1, library.LumaGrid(light.FilePath));
        if (darkness <= 0.001f || !library.TryGetBlurred(dark.FilePath, out var darkHandle, out var darkSize))
        {
            return;
        }

        var (darkUv0, darkUv1) = dark.Crop.ComputeUv(darkSize, aspect);
        WallpaperBackdrop.RecordDark(darkHandle, darkUv0, darkUv1, darkness, library.LumaGrid(dark.FilePath));
    }

    private static void DrawBlurred(ImDrawListPtr drawList, Rect shape, Rect quad, WallpaperEntry entry, float aspect,
        float alpha)
    {
        if (!Plugin.Wallpapers.TryGetBlurred(entry.FilePath, out var handle, out var size))
        {
            return;
        }

        DrawTexture(drawList, shape, quad, handle, size, entry, aspect, alpha);
    }

    public static void DrawSingle(ImDrawListPtr drawList, Rect rect, float radius, WallpaperEntry entry, float aspect,
        float alpha, Vector4? fallback) =>
        DrawSingle(drawList, rect, rect, radius, entry, aspect, alpha, fallback);

    public static void DrawSingle(ImDrawListPtr drawList, Rect shape, Rect quad, float radius, WallpaperEntry entry,
        float aspect, float alpha, Vector4? fallback)
    {
        var extent = MathF.Max(MathF.Max(shape.Width, shape.Height), MathF.Max(quad.Width, quad.Height));
        if (!Plugin.Wallpapers.TryGetTexture(entry.FilePath, extent, out var handle, out var size))
        {
            if (fallback is { } color)
            {
                Squircle.Fill(drawList, shape.Min, shape.Max, radius, ImGui.GetColorU32(color));
            }

            return;
        }

        DrawTexture(drawList, shape, quad, handle, size, entry, aspect, alpha);
    }

    public static void DrawSingleRounded(ImDrawListPtr drawList, Rect rect, float radius, WallpaperEntry entry,
        float aspect, float alpha, Vector4? fallback)
    {
        var extent = MathF.Max(rect.Width, rect.Height);
        if (!Plugin.Wallpapers.TryGetTexture(entry.FilePath, extent, out var handle, out var size))
        {
            if (fallback is { } color)
            {
                Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(color));
            }

            return;
        }

        var (uv0, uv1) = entry.Crop.ComputeUv(size, aspect);
        Squircle.FillImage(drawList, rect.Min, rect.Max, radius, handle, Tint(alpha), uv0, uv1);
    }

    private static uint Tint(float alpha) =>
        alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));

    private static void DrawTexture(ImDrawListPtr drawList, Rect shape, Rect quad, ImTextureID handle,
        Vector2 textureSize, WallpaperEntry entry, float aspect, float alpha)
    {
        var (uv0, uv1) = entry.Crop.ComputeUv(textureSize, aspect);
        var tint = Tint(alpha);
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
