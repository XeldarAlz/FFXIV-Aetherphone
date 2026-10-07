using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Skyfall;

internal struct GroundLight
{
    public float X;
    public float Strength;
    public float Life;
    public float MaxLife;
    public Vector4 Color;
}

internal sealed class SkyfallRenderer
{
    public static readonly Vector4 MeteorColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 MeteorHead = new(1f, 0.93f, 0.80f, 1f);
    public static readonly Vector4 BlastFill = new(1f, 0.96f, 0.85f, 1f);
    public static readonly Vector4 ShieldColor = new(0.55f, 0.90f, 1f, 1f);
    public const float LightRadius = 22f;
    private static readonly Vector4 Window = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4 Rubble = new(0.35f, 0.33f, 0.36f, 0.9f);
    private static readonly Vector4 SkylineWindow = new(1f, 0.92f, 0.70f, 0.45f);
    private const float TowerWidth = 2.4f;
    private const int TowerCount = 3;
    private const float DomeRadius = 7.5f;
    private const float HazeHeight = 34f;
    private const float SkylineStartX = -40f;
    private const float SkylineEndX = 140f;
    private const int FarBuildingCount = 30;
    private const int NearBuildingCount = 24;
    private const int WindowRows = 5;
    private const float WindowPitch = 2.2f;
    private const ulong SkylineSeed = 0x534B594C494E45UL;
    private static readonly float[] FarX = new float[FarBuildingCount];
    private static readonly float[] FarWidth = new float[FarBuildingCount];
    private static readonly float[] FarHeight = new float[FarBuildingCount];
    private static readonly uint[] FarWindows = new uint[FarBuildingCount];
    private static readonly float[] NearX = new float[NearBuildingCount];
    private static readonly float[] NearWidth = new float[NearBuildingCount];
    private static readonly float[] NearHeight = new float[NearBuildingCount];

    static SkyfallRenderer()
    {
        var random = GameRandom.FromSeed(SkylineSeed);
        var farPitch = (SkylineEndX - SkylineStartX) / FarBuildingCount;
        for (var index = 0; index < FarBuildingCount; index++)
        {
            FarX[index] = SkylineStartX + index * farPitch + random.NextFloat() * farPitch * 0.4f;
            FarWidth[index] = farPitch * (0.55f + random.NextFloat() * 0.4f);
            FarHeight[index] = 7f + random.NextFloat() * 12f;
            FarWindows[index] = random.NextUInt();
        }

        var nearPitch = (SkylineEndX - SkylineStartX) / NearBuildingCount;
        for (var index = 0; index < NearBuildingCount; index++)
        {
            NearX[index] = SkylineStartX + index * nearPitch + random.NextFloat() * nearPitch * 0.3f;
            NearWidth[index] = nearPitch * (0.6f + random.NextFloat() * 0.45f);
            NearHeight[index] = 3f + random.NextFloat() * 6f;
        }
    }

    public void Draw(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, Rect full, Vector4 accent,
        float scale, ReadOnlySpan<GroundLight> lights)
    {
        drawList.PushClipRect(full.Min, full.Max, true);
        DrawSkyline(drawList, in camera, accent);
        DrawGround(drawList, in camera, full, accent, scale);
        DrawGroundLights(drawList, in camera, full, lights);
        DrawCities(drawList, board, in camera, accent);
        DrawBattery(drawList, in camera, accent);
        DrawShieldPickup(drawList, board, in camera, scale);
        DrawMeteors(drawList, board, in camera, scale);
        DrawInterceptors(drawList, board, in camera, accent, scale);
        DrawBlasts(drawList, board, in camera, accent, scale);
        drawList.PopClipRect();
    }

    private static void DrawSkyline(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent)
    {
        var far = ImGui.GetColorU32(GamePalette.Darken(accent, 0.72f) with { W = 0.55f });
        var window = ImGui.GetColorU32(SkylineWindow);
        var windowSize = camera.Px(0.7f);
        for (var index = 0; index < FarBuildingCount; index++)
        {
            var min = camera.ToScreen(new Vector2(FarX[index], SkyfallBoard.GroundY - FarHeight[index]));
            var max = camera.ToScreen(new Vector2(FarX[index] + FarWidth[index], SkyfallBoard.GroundY));
            drawList.AddRectFilled(min, max, far);
            var bits = FarWindows[index];
            var columnLeft = camera.ToScreen(new Vector2(FarX[index] + FarWidth[index] * 0.3f, 0f)).X;
            var columnRight = camera.ToScreen(new Vector2(FarX[index] + FarWidth[index] * 0.65f, 0f)).X;
            for (var row = 0; row < WindowRows; row++)
            {
                var y = SkyfallBoard.GroundY - FarHeight[index] + 1.2f + row * WindowPitch;
                if (y > SkyfallBoard.GroundY - 1.5f)
                {
                    break;
                }

                var top = camera.ToScreen(new Vector2(0f, y)).Y;
                if ((bits & (1u << (row * 2))) != 0)
                {
                    drawList.AddRectFilled(new Vector2(columnLeft, top), new Vector2(columnLeft + windowSize, top + windowSize),
                        window);
                }

                if ((bits & (1u << (row * 2 + 1))) != 0)
                {
                    drawList.AddRectFilled(new Vector2(columnRight, top),
                        new Vector2(columnRight + windowSize, top + windowSize), window);
                }
            }
        }

        var near = ImGui.GetColorU32(GamePalette.Darken(accent, 0.86f) with { W = 0.92f });
        for (var index = 0; index < NearBuildingCount; index++)
        {
            var min = camera.ToScreen(new Vector2(NearX[index], SkyfallBoard.GroundY - NearHeight[index]));
            var max = camera.ToScreen(new Vector2(NearX[index] + NearWidth[index], SkyfallBoard.GroundY));
            drawList.AddRectFilled(min, max, near);
        }
    }

    private static void DrawGround(ImDrawListPtr drawList, in Camera2D camera, Rect full, Vector4 accent, float scale)
    {
        var groundY = camera.ToScreen(new Vector2(0f, SkyfallBoard.GroundY)).Y;
        var horizonTop = groundY - camera.Px(HazeHeight);
        var clear = ImGui.GetColorU32(accent with { W = 0f });
        var haze = ImGui.GetColorU32(accent with { W = 0.16f });
        drawList.AddRectFilledMultiColor(new Vector2(full.Min.X, horizonTop), new Vector2(full.Max.X, groundY), clear,
            clear, haze, haze);
        drawList.AddRectFilled(new Vector2(full.Min.X, groundY), full.Max,
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.55f) with { W = 0.9f }));
        drawList.AddLine(new Vector2(full.Min.X, groundY), new Vector2(full.Max.X, groundY),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f)), MathF.Max(1f, camera.Px(0.8f) * MathF.Min(1f, scale)));
    }

    private static void DrawGroundLights(ImDrawListPtr drawList, in Camera2D camera, Rect full,
        ReadOnlySpan<GroundLight> lights)
    {
        if (lights.Length == 0)
        {
            return;
        }

        for (var index = 0; index < lights.Length; index++)
        {
            ref readonly var light = ref lights[index];
            var fade = Easing.EaseOutCubic(light.Life / light.MaxLife);
            var center = camera.ToScreen(new Vector2(light.X, SkyfallBoard.GroundY));
            var radius = camera.Px(LightRadius * light.Strength) * (0.6f + 0.4f * fade);
            for (var layer = 3; layer >= 1; layer--)
            {
                var alpha = 0.32f * light.Strength * fade * (4 - layer) / 3f;
                drawList.AddCircleFilled(center, radius * (0.35f + layer * 0.22f),
                    ImGui.GetColorU32(light.Color with { W = alpha }), 48);
            }
        }
    }

    private static void DrawCities(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, Vector4 accent)
    {
        var block = camera.Px(TowerWidth);
        var shielded = board.ShieldCharges > 0;
        var domePulse = 0.5f + 0.5f * Pulse.Wave(Pulse.Breath);
        for (var cityIndex = 0; cityIndex < SkyfallBoard.CityCount; cityIndex++)
        {
            var center = camera.ToScreen(SkyfallBoard.CityCenter(cityIndex));
            if (!board.CityAlive(cityIndex))
            {
                drawList.AddRectFilled(new Vector2(center.X - block * 1.6f, center.Y - block * 0.4f),
                    new Vector2(center.X + block * 1.6f, center.Y), ImGui.GetColorU32(Rubble), block * 0.2f);
                continue;
            }

            var fill = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.28f));
            for (var tower = 0; tower < TowerCount; tower++)
            {
                var height = block * (tower == 1 ? 2.4f : 1.6f);
                var left = center.X + (tower - 1) * block * 1.15f - block * 0.5f;
                var min = new Vector2(left, center.Y - height);
                var max = new Vector2(left + block, center.Y);
                drawList.AddRectFilled(min, max, fill, block * 0.12f);
                var windowY = min.Y + block * 0.4f;
                while (windowY < max.Y - block * 0.4f)
                {
                    drawList.AddRectFilled(new Vector2(left + block * 0.3f, windowY),
                        new Vector2(left + block * 0.7f, windowY + block * 0.22f), ImGui.GetColorU32(Window));
                    windowY += block * 0.5f;
                }
            }

            ProgressRing.Glow(new Vector2(center.X, center.Y - block), block * 1.4f, accent, 0.18f);
            if (!shielded)
            {
                continue;
            }

            var domeRadius = camera.Px(DomeRadius);
            drawList.PathArcTo(center, domeRadius, MathF.PI, MathF.PI * 2f, 20);
            drawList.PathFillConvex(ImGui.GetColorU32(ShieldColor with { W = 0.08f + 0.06f * domePulse }));
            drawList.PathArcTo(center, domeRadius, MathF.PI, MathF.PI * 2f, 20);
            drawList.PathStroke(ImGui.GetColorU32(ShieldColor with { W = 0.35f + 0.3f * domePulse }), ImDrawFlags.None,
                MathF.Max(1f, camera.Px(0.4f)));
        }
    }

    private static void DrawBattery(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent)
    {
        var ground = camera.ToScreen(new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.GroundY));
        var size = camera.Px(3.2f);
        var apex = new Vector2(ground.X, ground.Y - size * 1.6f);
        ProgressRing.Glow(new Vector2(ground.X, ground.Y - size * 0.6f), size * 1.6f, accent, 0.35f);
        drawList.AddTriangleFilled(apex, new Vector2(ground.X + size, ground.Y), new Vector2(ground.X - size, ground.Y),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.15f)));
        drawList.AddCircleFilled(apex, size * 0.22f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)));
    }

    private static void DrawShieldPickup(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, float scale)
    {
        if (!board.ShieldFalling)
        {
            return;
        }

        var center = camera.ToScreen(board.ShieldPosition);
        var radius = camera.Px(SkyfallBoard.ShieldRadius);
        var spin = Pulse.Phase(Pulse.Orbit) * MathF.PI * 2f;
        ProgressRing.Glow(center, radius * 1.6f, ShieldColor, 0.7f);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ShieldColor), 0, MathF.Max(1f, 1.5f * scale));
        drawList.AddCircleFilled(center, radius * 0.4f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)));
        var orbit = center + new Vector2(MathF.Cos(spin), MathF.Sin(spin)) * radius;
        drawList.AddCircleFilled(orbit, MathF.Max(1f, radius * 0.22f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)));
    }

    private static void DrawMeteors(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, float scale)
    {
        var trailWide = ImGui.GetColorU32(MeteorColor with { W = 0.16f });
        var trailThin = ImGui.GetColorU32(MeteorColor with { W = 0.55f });
        var head = ImGui.GetColorU32(MeteorHead);
        var headRadius = MathF.Max(1.5f * scale, camera.Px(1.1f));
        for (var index = 0; index < board.MeteorCount; index++)
        {
            var meteor = board.GetMeteor(index);
            var start = camera.ToScreen(meteor.Start);
            var position = camera.ToScreen(meteor.Position);
            drawList.AddLine(start, position, trailWide, 3f * scale);
            drawList.AddLine(start, position, trailThin, 1.2f * scale);
            ProgressRing.Glow(position, headRadius * 2.2f, MeteorColor, 0.6f);
            drawList.AddCircleFilled(position, headRadius, head);
        }
    }

    private static void DrawInterceptors(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        var barrel = camera.ToScreen(new Vector2(SkyfallBoard.BatteryX, SkyfallBoard.BarrelY));
        var trail = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f) with { W = 0.55f });
        var marker = ImGui.GetColorU32(accent with { W = 0.75f });
        var headHalf = 1.6f * scale;
        var cross = 2.5f * scale;
        for (var index = 0; index < board.InterceptorCount; index++)
        {
            var shot = board.GetInterceptor(index);
            var position = camera.ToScreen(shot.Position);
            var target = camera.ToScreen(shot.Target);
            drawList.AddLine(barrel, position, trail, 1.5f * scale);
            drawList.AddRectFilled(position - new Vector2(headHalf, headHalf), position + new Vector2(headHalf, headHalf),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)));
            drawList.AddLine(target - new Vector2(cross, cross), target + new Vector2(cross, cross), marker, 1f * scale);
            drawList.AddLine(target - new Vector2(cross, -cross), target + new Vector2(cross, -cross), marker, 1f * scale);
        }
    }

    private static void DrawBlasts(ImDrawListPtr drawList, SkyfallBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        var fill = ImGui.GetColorU32(BlastFill with { W = 0.85f });
        var ring = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f));
        for (var index = 0; index < board.BlastCount; index++)
        {
            var blast = board.GetBlast(index);
            var center = camera.ToScreen(blast.Center);
            var radius = camera.Px(blast.Radius);
            ProgressRing.Glow(center, radius * 1.5f, MeteorColor, 0.8f);
            drawList.AddCircleFilled(center, radius, fill);
            drawList.AddCircle(center, radius, ring, 0, MathF.Max(1f, 1.5f * scale));
        }
    }
}
