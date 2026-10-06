using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Blade;

internal static class BladeRenderer
{
    public const float WorldWidth = 3.4f;
    public const float WorldTop = -1.7f;
    public const float WorldBottom = 3.6f;
    public const float WorldHeight = WorldBottom - WorldTop;
    public const float LaunchY = 3.0f;
    public const float BladeLength = 0.62f;
    public const float TipInset = 0.94f;
    public const float AppleOrbit = 1.1f;
    public const float AppleRadius = 0.13f;
    public const float RimPhase = -MathF.PI * 0.5f;
    private const float VibrationAmplitude = 0.03f;
    private const float VibrationRate = 90f;
    private const float PipSpacing = 11f;
    private const float PipRadius = 3.4f;
    private const float PipPad = 10f;
    private static readonly Vector4 Steel = new(0.82f, 0.86f, 0.92f, 1f);
    private static readonly Vector4 Handle = new(0.30f, 0.24f, 0.22f, 1f);
    private static readonly Vector4 Wood = new(0.42f, 0.30f, 0.24f, 1f);
    private static readonly Vector4 BossWood = new(0.34f, 0.22f, 0.26f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.32f);
    private static readonly Vector4 Trail = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 PipEmpty = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 AppleRed = new(0.92f, 0.26f, 0.30f, 1f);
    private static readonly Vector4 AppleLeaf = new(0.45f, 0.78f, 0.40f, 1f);
    private static readonly Vector4 AppleShine = new(1f, 1f, 1f, 0.6f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector2 WorldCenter => new(0f, (WorldTop + WorldBottom) * 0.5f);

    public static Vector2 ImpactPoint => new(0f, TipInset);

    public static Vector2 LaunchPoint => new(0f, LaunchY);

    public static Vector2 RimPoint(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    public static Vector2 ApplePoint(float angle) => RimPoint(angle) * AppleOrbit;

    public static float PipsWidth(int blades) =>
        blades <= 0 ? 0f : PipPad * 2f + (blades - 1) * PipSpacing + PipRadius * 2f;

    public static void Draw(ImDrawListPtr drawList, in Camera2D camera, BladeBoard board, Vector4 accent,
        float vibration, float time, float scale)
    {
        var center = camera.ToScreen(Vector2.Zero);
        var radius = camera.Px(1f);
        var bladeLength = camera.Px(BladeLength);
        DrawStuck(drawList, in camera, board, center, radius, bladeLength, vibration, time, scale);
        DrawWheel(drawList, board, center, radius, accent, scale);
        DrawApples(drawList, in camera, board);
        DrawFlight(drawList, in camera, board, bladeLength, scale);
    }

    public static void DrawPips(ImDrawListPtr drawList, Rect rect, BladeBoard board, Vector4 accent, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var total = board.LevelBlades;
        if (total <= 0)
        {
            return;
        }

        var spacing = PipSpacing * scale;
        var radius = PipRadius * scale;
        var origin = new Vector2(rect.Center.X - (total - 1) * spacing * 0.5f, rect.Center.Y);
        var full = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f));
        var empty = ImGui.GetColorU32(PipEmpty);
        for (var index = 0; index < total; index++)
        {
            var point = new Vector2(origin.X + index * spacing, origin.Y);
            if (index < board.Remaining)
            {
                drawList.AddCircleFilled(point, radius, full, 12);
                continue;
            }

            drawList.AddCircle(point, radius, empty, 12, MathF.Max(1f, scale));
        }
    }

    private static void DrawWheel(ImDrawListPtr drawList, BladeBoard board, Vector2 center, float radius,
        Vector4 accent, float scale)
    {
        var tone = board.IsBoss ? BossWood : Wood;
        ProgressRing.Glow(center, radius * 1.04f, accent, board.IsBoss ? 0.55f : 0.3f);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.06f), radius, ImGui.GetColorU32(Shadow), 64);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(tone, 0.18f)), 64);
        drawList.AddCircleFilled(center - new Vector2(0f, radius * 0.06f), radius * 0.94f, ImGui.GetColorU32(tone), 64);
        for (var ring = 1; ring <= 3; ring++)
        {
            drawList.AddCircle(center, radius * (0.32f + ring * 0.20f),
                ImGui.GetColorU32(GamePalette.Darken(tone, 0.28f) with { W = 0.5f }), 48, 1.4f * scale);
        }

        drawList.AddCircleFilled(center, radius * 0.24f, ImGui.GetColorU32(GamePalette.Lighten(tone, 0.14f)), 32);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f }),
            64, 2f * scale);
        var pulse = 0.35f + 0.25f * Pulse.Wave(Pulse.Calm);
        Typography.DrawCentered(drawList, center, GameNumber.Label(board.Level),
            GamePalette.Lighten(accent, 0.55f) with { W = pulse + 0.35f }, TextStyles.Title2);
    }

    private static void DrawStuck(ImDrawListPtr drawList, in Camera2D camera, BladeBoard board, Vector2 center,
        float radius, float bladeLength, float vibration, float time, float scale)
    {
        var amplitude = camera.Px(VibrationAmplitude) * vibration;
        for (var index = 0; index < board.StuckCount; index++)
        {
            var angle = board.StuckAngle(index);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var reach = radius * TipInset + amplitude * MathF.Sin(time * VibrationRate + index * 2.1f);
            DrawBlade(drawList, center + direction * reach, direction, bladeLength, scale, 1f);
        }
    }

    private static void DrawApples(ImDrawListPtr drawList, in Camera2D camera, BladeBoard board)
    {
        var radius = camera.Px(AppleRadius);
        for (var index = 0; index < board.AppleCount; index++)
        {
            DrawApple(drawList, camera.ToScreen(ApplePoint(board.AppleAngle(index))), radius);
        }
    }

    private static void DrawApple(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        ProgressRing.Glow(center, radius * 1.8f, AppleRed, 0.5f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(AppleRed), 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f,
            ImGui.GetColorU32(AppleShine), 16);
        var leafBase = center + new Vector2(radius * 0.15f, -radius);
        drawList.AddTriangleFilled(leafBase, leafBase + new Vector2(radius * 0.55f, -radius * 0.65f),
            leafBase + new Vector2(radius * 0.05f, -radius * 0.5f), ImGui.GetColorU32(AppleLeaf));
    }

    private static void DrawFlight(ImDrawListPtr drawList, in Camera2D camera, BladeBoard board, float bladeLength,
        float scale)
    {
        var launch = camera.ToScreen(LaunchPoint);
        var target = camera.ToScreen(ImpactPoint);
        var down = new Vector2(0f, 1f);
        if (!board.InFlight)
        {
            if (board.State == BladeState.Playing && board.Remaining > 0)
            {
                DrawBlade(drawList, launch, down, bladeLength, scale, 1f);
            }

            return;
        }

        var progress = board.FlightProgress;
        var eased = progress * (0.55f + 0.45f * progress);
        var tip = Vector2.Lerp(launch, target, eased);
        drawList.AddLine(new Vector2(tip.X, tip.Y + bladeLength * 0.4f), new Vector2(tip.X, launch.Y),
            ImGui.GetColorU32(Trail), 2.5f * scale);
        DrawBlade(drawList, tip, down, bladeLength, scale, 1f);
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
        var edge = ImGui.GetColorU32(White with { W = 0.75f * alpha });
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
