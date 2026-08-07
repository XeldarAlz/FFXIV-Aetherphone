using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Blade;

internal sealed class BladeRenderer
{
    private const float WheelCenterFactor = 0.36f;
    private const float WheelRadiusWidthFactor = 0.30f;
    private const float WheelRadiusHeightFactor = 0.21f;
    private const float BladeLengthFactor = 0.62f;
    private const float LaunchOffset = 74f;
    private static readonly Vector4 Steel = new(0.82f, 0.86f, 0.92f, 1f);
    private static readonly Vector4 Handle = new(0.30f, 0.24f, 0.22f, 1f);
    private static readonly Vector4 Wood = new(0.42f, 0.30f, 0.24f, 1f);

    public static Vector2 WheelCenterOf(Rect area) =>
        new(area.Center.X, area.Min.Y + area.Height * WheelCenterFactor);

    public static float WheelRadiusOf(Rect area) =>
        MathF.Min(area.Width * WheelRadiusWidthFactor, area.Height * WheelRadiusHeightFactor);

    public static Vector2 LaunchPointOf(Rect area, float scale) =>
        new(area.Center.X, area.Max.Y - LaunchOffset * scale);

    public void Draw(BladeBoard board, Rect area, Vector4 accent, Vector2 shake, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var center = WheelCenterOf(area) + shake;
        var radius = WheelRadiusOf(area);
        var bladeLength = radius * BladeLengthFactor;
        DrawStuck(drawList, board, center, radius, bladeLength, scale);
        DrawWheel(drawList, board, center, radius, accent, scale);
        DrawFlight(drawList, board, area, center, radius, bladeLength, scale);
        DrawMagazine(drawList, board, area, accent, scale);
    }

    private static void DrawWheel(ImDrawListPtr drawList, BladeBoard board, Vector2 center, float radius,
        Vector4 accent, float scale)
    {
        var tone = board.IsBoss ? new Vector4(0.34f, 0.22f, 0.26f, 1f) : Wood;
        ProgressRing.Glow(center, radius * 1.04f, accent, board.IsBoss ? 0.55f : 0.3f);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.06f), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)), 64);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(tone, 0.18f)), 64);
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.06f), radius * 0.94f,
            ImGui.GetColorU32(tone), 64);
        for (var ring = 1; ring <= 3; ring++)
        {
            drawList.AddCircle(center, radius * (0.32f + ring * 0.20f),
                ImGui.GetColorU32(GamePalette.Darken(tone, 0.28f) with { W = 0.5f }), 48, 1.4f * scale);
        }

        drawList.AddCircleFilled(center, radius * 0.24f, ImGui.GetColorU32(GamePalette.Lighten(tone, 0.14f)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f }),
            64, 2f * scale);
        var pulse = 0.35f + 0.25f * Pulse.Wave(Pulse.Calm);
        Typography.DrawCentered(center, GameNumber.Label(board.Level),
            GamePalette.Lighten(accent, 0.55f) with { W = pulse + 0.35f }, TextStyles.Title2);
    }

    private static void DrawStuck(ImDrawListPtr drawList, BladeBoard board, Vector2 center, float radius,
        float bladeLength, float scale)
    {
        for (var index = 0; index < board.StuckCount; index++)
        {
            var angle = board.StuckAngle(index);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            DrawBlade(drawList, center + direction * radius * 0.94f, direction, bladeLength, scale, 1f);
        }
    }

    private static void DrawFlight(ImDrawListPtr drawList, BladeBoard board, Rect area, Vector2 center, float radius,
        float bladeLength, float scale)
    {
        var launch = LaunchPointOf(area, scale);
        var target = center + new Vector2(0f, radius * 0.94f);
        if (!board.InFlight)
        {
            if (board.State == BladeState.Playing || board.State == BladeState.Ready)
            {
                DrawBlade(drawList, launch, new Vector2(0f, 1f), bladeLength, scale, 1f);
            }

            return;
        }

        var progress = board.FlightProgress;
        var eased = progress * (0.55f + 0.45f * progress);
        var tip = Vector2.Lerp(launch, target, eased);
        var trail = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f));
        drawList.AddLine(new Vector2(tip.X, tip.Y + bladeLength * 0.4f), new Vector2(tip.X, launch.Y), trail,
            2.5f * scale);
        DrawBlade(drawList, tip, new Vector2(0f, 1f), bladeLength, scale, 1f);
    }

    private static void DrawMagazine(ImDrawListPtr drawList, BladeBoard board, Rect area, Vector4 accent, float scale)
    {
        if (board.LevelBlades <= 0)
        {
            return;
        }

        var spacing = 11f * scale;
        var radius = 3.4f * scale;
        var total = board.LevelBlades;
        var origin = new Vector2(area.Center.X - (total - 1) * spacing * 0.5f, area.Max.Y - 26f * scale);
        for (var index = 0; index < total; index++)
        {
            var point = new Vector2(origin.X + index * spacing, origin.Y);
            if (index < board.Remaining)
            {
                drawList.AddCircleFilled(point, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f)), 12);
                continue;
            }

            drawList.AddCircle(point, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f)), 12,
                MathF.Max(1f, scale));
        }
    }

    public static void DrawBlade(ImDrawListPtr drawList, Vector2 tip, Vector2 direction, float length, float scale,
        float alpha)
    {
        var right = new Vector2(-direction.Y, direction.X);
        var halfWidth = MathF.Max(1.6f * scale, length * 0.075f);
        var bladeBase = tip + direction * (length * 0.52f);
        var guard = tip + direction * (length * 0.58f);
        var handleEnd = tip + direction * length;
        var steel = ImGui.GetColorU32(Steel with { W = alpha });
        var edge = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.75f * alpha));
        drawList.AddTriangleFilled(tip, bladeBase + right * halfWidth, bladeBase - right * halfWidth, steel);
        drawList.AddQuadFilled(bladeBase - right * halfWidth, bladeBase + right * halfWidth,
            guard + right * halfWidth, guard - right * halfWidth, steel);
        drawList.AddLine(tip, guard - right * halfWidth * 0.35f, edge, MathF.Max(1f, scale * 0.9f));
        var guardWidth = halfWidth * 1.9f;
        drawList.AddQuadFilled(guard - right * guardWidth, guard + right * guardWidth,
            guard + direction * (length * 0.07f) + right * guardWidth,
            guard + direction * (length * 0.07f) - right * guardWidth,
            ImGui.GetColorU32(GamePalette.Darken(Steel, 0.35f) with { W = alpha }));
        var handleStart = guard + direction * (length * 0.07f);
        var handleWidth = halfWidth * 1.25f;
        drawList.AddQuadFilled(handleStart - right * handleWidth, handleStart + right * handleWidth,
            handleEnd + right * handleWidth, handleEnd - right * handleWidth,
            ImGui.GetColorU32(Handle with { W = alpha }));
        drawList.AddCircleFilled(handleEnd, handleWidth,
            ImGui.GetColorU32(GamePalette.Lighten(Handle, 0.16f) with { W = alpha }), 12);
    }
}
