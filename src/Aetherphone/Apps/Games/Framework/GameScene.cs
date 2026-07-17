using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameScene
{
    private const float WashAlpha = 0.14f;
    private const float VignetteAlpha = 0.38f;

    private readonly struct Blob
    {
        public readonly float RadiusFactor;
        public readonly float SpeedX;
        public readonly float SpeedY;
        public readonly float PhaseX;
        public readonly float PhaseY;
        public readonly float AnchorX;
        public readonly float AnchorY;
        public readonly float Alpha;

        public Blob(float radiusFactor, float speedX, float speedY, float phaseX, float phaseY, float anchorX,
            float anchorY, float alpha)
        {
            RadiusFactor = radiusFactor;
            SpeedX = speedX;
            SpeedY = speedY;
            PhaseX = phaseX;
            PhaseY = phaseY;
            AnchorX = anchorX;
            AnchorY = anchorY;
            Alpha = alpha;
        }
    }

    private static readonly Blob[] Blobs =
    {
        new(0.52f, 0.31f, 0.23f, 0.0f, 1.7f, 0.22f, 0.20f, 0.075f),
        new(0.44f, 0.22f, 0.29f, 2.4f, 4.1f, 0.80f, 0.42f, 0.060f),
        new(0.60f, 0.17f, 0.21f, 4.9f, 0.8f, 0.46f, 0.86f, 0.055f),
    };

    public static void Ambient(ImDrawListPtr drawList, Rect body, Vector4 accent)
    {
        var time = (float)ImGui.GetTime();
        var washTop = ImGui.GetColorU32(accent with { W = WashAlpha });
        var washClear = ImGui.GetColorU32(accent with { W = 0f });
        var washBottom = new Vector2(body.Min.X, body.Min.Y + body.Height * 0.5f);
        drawList.AddRectFilledMultiColor(body.Min, new Vector2(body.Max.X, washBottom.Y), washTop, washTop, washClear,
            washClear);
        for (var index = 0; index < Blobs.Length; index++)
        {
            ref readonly var blob = ref Blobs[index];
            var anchor = new Vector2(body.Min.X + body.Width * blob.AnchorX, body.Min.Y + body.Height * blob.AnchorY);
            var drift = new Vector2(MathF.Sin(time * blob.SpeedX + blob.PhaseX) * body.Width * 0.14f,
                MathF.Cos(time * blob.SpeedY + blob.PhaseY) * body.Height * 0.10f);
            DrawGlowBlob(drawList, anchor + drift, body.Width * blob.RadiusFactor, accent, blob.Alpha);
        }

        var vignetteTop = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        var vignetteBottom = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, VignetteAlpha));
        var vignetteStart = new Vector2(body.Min.X, body.Max.Y - body.Height * 0.34f);
        drawList.AddRectFilledMultiColor(vignetteStart, body.Max, vignetteTop, vignetteTop, vignetteBottom,
            vignetteBottom);
    }

    private static void DrawGlowBlob(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float alpha)
    {
        for (var layer = 3; layer >= 1; layer--)
        {
            var layerRadius = radius * (0.45f + layer * 0.19f);
            var layerAlpha = alpha * (4 - layer) * 0.34f;
            drawList.AddCircleFilled(center, layerRadius,
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.25f) with { W = layerAlpha }));
        }
    }

    public static void Arena(ImDrawListPtr drawList, Rect rect, float rounding, float scale, Vector4 accent)
    {
        var glowInset = 5f * scale;
        Squircle.Fill(drawList, rect.Min - new Vector2(glowInset, glowInset), rect.Max + new Vector2(glowInset, glowInset),
            rounding + glowInset, ImGui.GetColorU32(accent with { W = 0.07f }));
        Elevation.Draw(drawList, rect.Min, rect.Max, rounding, scale, 13f, 5f, 0.30f);
        var top = ImGui.GetColorU32(GamePalette.Lighten(GamePalette.Board, 0.055f));
        var bottom = ImGui.GetColorU32(GamePalette.Darken(GamePalette.Board, 0.30f));
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding, top, bottom);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.075f)),
            1f * scale);
        var highlightInset = MathF.Max(rounding, 1f);
        drawList.AddLine(new Vector2(rect.Min.X + highlightInset, rect.Min.Y + 1f * scale),
            new Vector2(rect.Max.X - highlightInset, rect.Min.Y + 1f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f * scale);
    }
}
