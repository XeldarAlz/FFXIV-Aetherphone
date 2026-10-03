using Aetherphone.Core;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct BackdropSnapshot
{
    public readonly int RecordedFrame;
    public readonly Rect Quad;
    public readonly ImTextureID LightHandle;
    public readonly Vector2 LightUv0;
    public readonly Vector2 LightUv1;
    public readonly float[]? LightGrid;
    public readonly bool HasDark;
    public readonly ImTextureID DarkHandle;
    public readonly Vector2 DarkUv0;
    public readonly Vector2 DarkUv1;
    public readonly float[]? DarkGrid;
    public readonly float Darkness;
    public readonly int FlatFrame;
    public readonly Vector4 FlatColor;

    public BackdropSnapshot(int recordedFrame, Rect quad, ImTextureID lightHandle, Vector2 lightUv0,
        Vector2 lightUv1, float[]? lightGrid, bool hasDark, ImTextureID darkHandle, Vector2 darkUv0, Vector2 darkUv1,
        float[]? darkGrid, float darkness, int flatFrame, Vector4 flatColor)
    {
        RecordedFrame = recordedFrame;
        Quad = quad;
        LightHandle = lightHandle;
        LightUv0 = lightUv0;
        LightUv1 = lightUv1;
        LightGrid = lightGrid;
        HasDark = hasDark;
        DarkHandle = darkHandle;
        DarkUv0 = darkUv0;
        DarkUv1 = darkUv1;
        DarkGrid = darkGrid;
        Darkness = darkness;
        FlatFrame = flatFrame;
        FlatColor = flatColor;
    }
}

internal static class WallpaperBackdrop
{
    private const int FrameTolerance = 1;
    private const int BrightnessTaps = 3;
    private const float BrightPixelThreshold = 0.65f;

    private static int recordedFrame = -1;
    private static Rect quad;
    private static ImTextureID lightHandle;
    private static Vector2 lightUv0;
    private static Vector2 lightUv1;
    private static float[]? lightGrid;
    private static bool hasDark;
    private static ImTextureID darkHandle;
    private static Vector2 darkUv0;
    private static Vector2 darkUv1;
    private static float[]? darkGrid;
    private static float darkness;
    private static int flatFrame = -1;
    private static Vector4 flatColor;
    private static int requestedFrame = -1;

    public static bool Available => ImGui.GetFrameCount() - recordedFrame <= FrameTolerance && quad.Width > 0f &&
                                    quad.Height > 0f;

    public static bool FlatAvailable => ImGui.GetFrameCount() - flatFrame <= FrameTolerance;

    public static bool Requested => ImGui.GetFrameCount() - requestedFrame <= FrameTolerance;

    public static void Clear() => recordedFrame = -1;

    public static void RecordFlat(Vector4 color)
    {
        flatFrame = ImGui.GetFrameCount();
        flatColor = color with { W = 1f };
    }

    public static void Record(Rect drawnQuad, ImTextureID light, Vector2 lightMinUv, Vector2 lightMaxUv,
        float[]? grid)
    {
        recordedFrame = ImGui.GetFrameCount();
        quad = drawnQuad;
        lightHandle = light;
        lightUv0 = lightMinUv;
        lightUv1 = lightMaxUv;
        lightGrid = grid;
        hasDark = false;
        darkness = 0f;
    }

    public static void RecordDark(ImTextureID dark, Vector2 darkMinUv, Vector2 darkMaxUv, float amount, float[]? grid)
    {
        hasDark = true;
        darkHandle = dark;
        darkUv0 = darkMinUv;
        darkUv1 = darkMaxUv;
        darkGrid = grid;
        darkness = amount;
    }

    public static BackdropSnapshot Snapshot() =>
        new(recordedFrame, quad, lightHandle, lightUv0, lightUv1, lightGrid, hasDark, darkHandle, darkUv0, darkUv1,
            darkGrid, darkness, flatFrame, flatColor);

    public static void Restore(in BackdropSnapshot snapshot)
    {
        recordedFrame = snapshot.RecordedFrame;
        quad = snapshot.Quad;
        lightHandle = snapshot.LightHandle;
        lightUv0 = snapshot.LightUv0;
        lightUv1 = snapshot.LightUv1;
        lightGrid = snapshot.LightGrid;
        hasDark = snapshot.HasDark;
        darkHandle = snapshot.DarkHandle;
        darkUv0 = snapshot.DarkUv0;
        darkUv1 = snapshot.DarkUv1;
        darkGrid = snapshot.DarkGrid;
        darkness = snapshot.Darkness;
        flatFrame = snapshot.FlatFrame;
        flatColor = snapshot.FlatColor;
    }

    public static bool FillEdge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float band,
        float opacity)
    {
        if (opacity <= 0f)
        {
            return false;
        }

        requestedFrame = ImGui.GetFrameCount();
        if (!Available)
        {
            return false;
        }

        var (lightMin, lightMax) = Map(min, max, lightUv0, lightUv1);
        Squircle.FillImageEdge(drawList, min, max, radius, band, lightHandle, Tint(opacity), lightMin, lightMax, 0f);
        if (!hasDark || darkness <= 0.001f)
        {
            return true;
        }

        var (darkMin, darkMax) = Map(min, max, darkUv0, darkUv1);
        Squircle.FillImageEdge(drawList, min, max, radius, band, darkHandle, Tint(opacity * darkness), darkMin,
            darkMax, 0f);
        return true;
    }

    public static bool Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, float opacity, float lens,
        float band, float refraction)
    {
        if (opacity <= 0f)
        {
            return false;
        }

        requestedFrame = ImGui.GetFrameCount();
        if (!Available)
        {
            if (!FlatAvailable)
            {
                return false;
            }

            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(flatColor with { W = opacity }));
            return true;
        }

        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f / MathF.Max(lens, 1f);
        var sampleMin = center - half;
        var sampleMax = center + half;
        var (lightMin, lightMax) = Map(sampleMin, sampleMax, lightUv0, lightUv1);
        var lightTint = Tint(opacity);
        Squircle.FillImage(drawList, min, max, radius, lightHandle, lightTint, lightMin, lightMax);
        Squircle.FillImageEdge(drawList, min, max, radius, band, lightHandle, lightTint, lightMin, lightMax,
            refraction);
        if (!hasDark || darkness <= 0.001f)
        {
            return true;
        }

        var (darkMin, darkMax) = Map(sampleMin, sampleMax, darkUv0, darkUv1);
        var darkTint = Tint(opacity * darkness);
        Squircle.FillImage(drawList, min, max, radius, darkHandle, darkTint, darkMin, darkMax);
        Squircle.FillImageEdge(drawList, min, max, radius, band, darkHandle, darkTint, darkMin, darkMax, refraction);
        return true;
    }

    public static float Brightness(Vector2 min, Vector2 max)
    {
        if (!Available)
        {
            return FlatAvailable ? Core.Theme.Palette.Luminance(flatColor) : -1f;
        }

        if (lightGrid is null)
        {
            return FlatAvailable ? Core.Theme.Palette.Luminance(flatColor) : -1f;
        }

        var (lightMin, lightMax) = Map(min, max, lightUv0, lightUv1);
        var light = Sample(lightGrid, lightMin, lightMax);
        if (!hasDark || darkness <= 0.001f || darkGrid is null)
        {
            return light;
        }

        var (darkMin, darkMax) = Map(min, max, darkUv0, darkUv1);
        return light + (Sample(darkGrid, darkMin, darkMax) - light) * darkness;
    }

    private static float Sample(float[] grid, Vector2 uvMin, Vector2 uvMax)
    {
        const int size = WallpaperLibrary.BrightnessSampleSize;
        var lumaSum = 0f;
        var brightCount = 0;
        for (var row = 0; row < BrightnessTaps; row++)
        {
            var v = uvMin.Y + (uvMax.Y - uvMin.Y) * ((row + 0.5f) / BrightnessTaps);
            var y = Math.Clamp((int)(Math.Clamp(v, 0f, 1f) * (size - 1) + 0.5f), 0, size - 1);
            for (var column = 0; column < BrightnessTaps; column++)
            {
                var u = uvMin.X + (uvMax.X - uvMin.X) * ((column + 0.5f) / BrightnessTaps);
                var x = Math.Clamp((int)(Math.Clamp(u, 0f, 1f) * (size - 1) + 0.5f), 0, size - 1);
                var luma = grid[y * size + x];
                lumaSum += luma;
                if (luma >= BrightPixelThreshold)
                {
                    brightCount++;
                }
            }
        }

        const float taps = BrightnessTaps * BrightnessTaps;
        return Math.Clamp(0.5f * (lumaSum / taps) + 0.5f * (brightCount / taps), 0f, 1f);
    }

    private static (Vector2 Min, Vector2 Max) Map(Vector2 sampleMin, Vector2 sampleMax, Vector2 uv0, Vector2 uv1)
    {
        var span = uv1 - uv0;
        var size = quad.Size;
        var mappedMin = uv0 + (sampleMin - quad.Min) / size * span;
        var mappedMax = uv0 + (sampleMax - quad.Min) / size * span;
        return (mappedMin, mappedMax);
    }

    private static uint Tint(float alpha) =>
        alpha >= 1f ? 0xFFFFFFFFu : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
}
