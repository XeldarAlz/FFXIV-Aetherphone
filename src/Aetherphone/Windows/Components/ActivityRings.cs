using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ActivityRings
{
    public const float Height = 188f;
    private static readonly Vector4 ProgressTint = new(0.98f, 0.22f, 0.36f, 1f);
    private static readonly Vector4 AdventureTint = new(0.62f, 0.94f, 0.18f, 1f);
    private static readonly Vector4 FortuneTint = new(0.36f, 0.84f, 0.96f, 1f);
    public static Vector4 RingOneTint => ProgressTint;
    public static Vector4 RingTwoTint => AdventureTint;
    public static Vector4 RingThreeTint => FortuneTint;

    public static void Draw(Vector4 trackInk, float progressFraction, float adventureFraction, float fortuneFraction)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + Height * scale * 0.5f);
        var thickness = 13f * scale;
        var gap = 5f * scale;
        var outerRadius = 64f * scale;
        var middleRadius = outerRadius - thickness - gap;
        var innerRadius = middleRadius - thickness - gap;
        var trackColor = Palette.WithAlpha(trackInk, 0.12f);
        DrawRing(center, outerRadius, thickness, progressFraction, ProgressTint, trackColor);
        DrawRing(center, middleRadius, thickness, adventureFraction, AdventureTint, trackColor);
        DrawRing(center, innerRadius, thickness, fortuneFraction, FortuneTint, trackColor);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));
    }

    private static void DrawRing(Vector2 center, float radius, float thickness, float fraction, Vector4 tint,
        Vector4 trackColor)
    {
        ProgressRing.Track(center, radius, thickness, trackColor);
        if (fraction >= 0.999f)
        {
            ProgressRing.Glow(center, radius, tint, 0.30f + 0.20f * Pulse.Wave(Pulse.Breath));
        }

        ProgressRing.Fill(center, radius, thickness, Math.Clamp(fraction, 0f, 1f), tint);
    }
}
