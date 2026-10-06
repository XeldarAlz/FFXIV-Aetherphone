using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class BoardPlate
{
    public const float Padding = 10f;
    public const float Radius = Metrics.Radius.Lg;
    private const float FillAlpha = 0.72f;
    private const float PaperFillAlpha = 0.55f;
    private const float FillDarken = 0.30f;
    private const float RimAlpha = 0.10f;
    private const float PaperRimAlpha = 0.06f;
    private const float SheenAlpha = 0.06f;
    private const float SheenHeight = 12f;
    private const float GlowAlpha = 0.12f;
    private const float GlowSpread = 24f;

    public static void Draw(ImDrawListPtr drawList, Rect rect, float radius, float scale, Vector4 accent, StageInk tone)
    {
        var spread = GlowSpread * scale;
        var glowMin = rect.Min - new Vector2(spread, spread * 0.5f);
        var glowMax = rect.Max + new Vector2(spread, spread * 1.5f);
        Squircle.Fill(drawList, glowMin, glowMax, radius + spread, ImGui.GetColorU32(accent with { W = GlowAlpha * 0.5f }));
        Squircle.Fill(drawList, rect.Min - new Vector2(spread * 0.4f, 0f), rect.Max + new Vector2(spread * 0.4f, spread * 0.8f),
            radius + spread * 0.4f, ImGui.GetColorU32(accent with { W = GlowAlpha }));
        Elevation.Floating(drawList, rect.Min, rect.Max, radius, scale);
        var fill = tone == StageInk.Dark
            ? new Vector4(1f, 1f, 1f, PaperFillAlpha)
            : Palette.Darken(StageBackdrop.LastGround, FillDarken) with { W = FillAlpha };
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var rim = tone == StageInk.Dark
            ? new Vector4(0f, 0f, 0f, PaperRimAlpha)
            : new Vector4(1f, 1f, 1f, RimAlpha);
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(rim), 1f * scale);
        var sheen = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, SheenAlpha));
        Material.SheenBlock(drawList, rect.Min, rect.Max, radius, sheen, 1f * scale,
            Math.Clamp(SheenHeight * scale / MathF.Max(1f, rect.Height), 0f, 1f));
    }

    public static Rect Around(Rect grid, float scale) => grid.Inset(-Padding * scale);
}
