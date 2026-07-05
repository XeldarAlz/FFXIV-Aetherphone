using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ActivityRings
{
    public const float Height = 188f;
    private static readonly Vector4 MoveTint = new(0.98f, 0.22f, 0.36f, 1f);
    private static readonly Vector4 ExerciseTint = new(0.62f, 0.94f, 0.18f, 1f);
    private static readonly Vector4 StandTint = new(0.36f, 0.84f, 0.96f, 1f);
    public static Vector4 RingOneTint => MoveTint;
    public static Vector4 RingTwoTint => ExerciseTint;
    public static Vector4 RingThreeTint => StandTint;

    public static void Draw(PhoneTheme theme, float jobFraction, float tomestoneFraction, float collectionFraction)
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
        var trackColor = Styling.WithAlpha(theme.TextStrong, 0.12f);
        DrawRing(center, outerRadius, thickness, jobFraction, MoveTint, trackColor);
        DrawRing(center, middleRadius, thickness, tomestoneFraction, ExerciseTint, trackColor);
        DrawRing(center, innerRadius, thickness, collectionFraction, StandTint, trackColor);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));
    }

    private static void DrawRing(Vector2 center, float radius, float thickness, float fraction, Vector4 tint,
        Vector4 trackColor)
    {
        ProgressRing.Track(center, radius, thickness, trackColor);
        if (fraction >= 0.999f)
        {
            ProgressRing.Glow(center, radius, tint, 0.30f + 0.20f * Styling.Pulse(Styling.PulseBreath));
        }

        ProgressRing.Fill(center, radius, thickness, fraction, tint);
    }
}
