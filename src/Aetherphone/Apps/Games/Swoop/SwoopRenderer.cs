using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal sealed class SwoopRenderer
{
    public static readonly Vector4 TrailOuter = new(0.80f, 1f, 0.38f, 1f);
    public static readonly Vector4 TrailCore = new(1f, 0.97f, 0.78f, 1f);
    private const int PaletteCount = 6;
    private const int SlotsPerPalette = 8;
    private const int StripeSamples = 4;
    private const float TopBandDepth = 1.6f;
    private const float MidBandDepth = 6.5f;
    private const float RimThickness = 0.36f;
    private const float PoleHeight = 9f;
    private const float FlagLength = 3.2f;
    private const float FlagHeight = 2f;
    private const int SpeedLineCount = 12;
    private const int SlotTopLight = 0;
    private const int SlotTopDark = 1;
    private const int SlotMidLight = 2;
    private const int SlotMidDark = 3;
    private const int SlotDeepLight = 4;
    private const int SlotDeepDark = 5;
    private const int SlotRim = 6;
    private const int SlotRimHighlight = 7;
    private static readonly Vector4[] StripeLight =
    {
        new(0.47f, 0.78f, 0.38f, 1f), new(0.95f, 0.78f, 0.46f, 1f), new(0.30f, 0.70f, 0.66f, 1f),
        new(0.92f, 0.56f, 0.64f, 1f), new(0.62f, 0.54f, 0.90f, 1f), new(0.94f, 0.60f, 0.32f, 1f),
    };

    private static readonly Vector4[] StripeDark =
    {
        new(0.40f, 0.70f, 0.33f, 1f), new(0.89f, 0.70f, 0.40f, 1f), new(0.25f, 0.62f, 0.59f, 1f),
        new(0.85f, 0.49f, 0.58f, 1f), new(0.55f, 0.48f, 0.82f, 1f), new(0.87f, 0.52f, 0.27f, 1f),
    };

    private static readonly Vector4[] Rim =
    {
        new(0.82f, 0.95f, 0.58f, 1f), new(1f, 0.94f, 0.74f, 1f), new(0.64f, 0.94f, 0.88f, 1f),
        new(1f, 0.83f, 0.86f, 1f), new(0.86f, 0.82f, 1f, 1f), new(1f, 0.85f, 0.58f, 1f),
    };

    private static readonly Vector4 CrystalLight = new(0.62f, 0.97f, 1f, 1f);
    private static readonly Vector4 CrystalDark = new(0.28f, 0.66f, 0.96f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.32f, 1f);
    private static readonly Vector4 Pole = new(0.96f, 0.93f, 0.86f, 1f);
    private static readonly Vector4 Stone = new(0.58f, 0.58f, 0.64f, 1f);
    private static readonly Vector4 Dusk = new(0.04f, 0.04f, 0.12f, 1f);
    private static readonly Vector4 DuskEdge = new(0.62f, 0.50f, 1f, 1f);
    private static readonly float[] SpeedLineY = new float[SpeedLineCount];
    private static readonly float[] SpeedLineLength = new float[SpeedLineCount];
    private static readonly float[] SpeedLineRate = new float[SpeedLineCount];
    private static readonly float[] SpeedLinePhase = new float[SpeedLineCount];

    private readonly uint[] colors = new uint[PaletteCount * SlotsPerPalette];

    static SwoopRenderer()
    {
        var random = new SwoopRandom(0x5EED5u);
        for (var line = 0; line < SpeedLineCount; line++)
        {
            SpeedLineY[line] = random.NextUnit();
            SpeedLineLength[line] = random.NextUnit();
            SpeedLineRate[line] = 0.9f + random.NextUnit() * 0.8f;
            SpeedLinePhase[line] = random.NextUnit();
        }
    }

    public static Vector2 Screen(in Camera2D camera, double x, float y) => camera.ToScreen(new Vector2((float)x, -y));

    public static double WorldX(in Camera2D camera, float screenX) => camera.ToWorld(new Vector2(screenX, 0f)).X;

    public void PrepareColors(in SwoopLighting lighting)
    {
        for (var palette = 0; palette < PaletteCount; palette++)
        {
            var light = SwoopShapes.Multiply(StripeLight[palette], lighting.Tint);
            var dark = SwoopShapes.Multiply(StripeDark[palette], lighting.Tint);
            var rim = SwoopShapes.Multiply(Rim[palette], lighting.Tint);
            var slot = palette * SlotsPerPalette;
            colors[slot + SlotTopLight] = ImGui.GetColorU32(light);
            colors[slot + SlotTopDark] = ImGui.GetColorU32(dark);
            colors[slot + SlotMidLight] = ImGui.GetColorU32(GamePalette.Darken(light, 0.12f));
            colors[slot + SlotMidDark] = ImGui.GetColorU32(GamePalette.Darken(dark, 0.12f));
            colors[slot + SlotDeepLight] = ImGui.GetColorU32(GamePalette.Darken(light, 0.3f));
            colors[slot + SlotDeepDark] = ImGui.GetColorU32(GamePalette.Darken(dark, 0.3f));
            colors[slot + SlotRim] = ImGui.GetColorU32(rim);
            colors[slot + SlotRimHighlight] = ImGui.GetColorU32(GamePalette.Lighten(rim, 0.35f));
        }
    }

    public void DrawTerrain(ImDrawListPtr drawList, SwoopBoard board, in Camera2D camera, Rect area, float scale)
    {
        var first = Math.Max(board.FirstSample, (int)Math.Floor(WorldX(in camera, area.Min.X - 4f) / SwoopBoard.SampleSpacing));
        var end = Math.Min(board.EndSample - 1, (int)Math.Ceiling(WorldX(in camera, area.Max.X + 4f) / SwoopBoard.SampleSpacing));
        if (end <= first)
        {
            return;
        }

        var terrain = board.Terrain;
        var pixels = camera.Px(1f);
        var topDepth = new Vector2(0f, TopBandDepth * pixels);
        var midDepth = new Vector2(0f, MidBandDepth * pixels);
        var bottom = area.Max.Y + 2f;
        var island = terrain.IslandIndexAt(SwoopBoard.SampleX(first));
        var boundary = island < 0 ? terrain.IslandStart(0) : terrain.IslandEnd(island);
        var previous = Screen(in camera, SwoopBoard.SampleX(first), board.SampleHeight(first));
        for (var sample = first; sample < end; sample++)
        {
            var current = Screen(in camera, SwoopBoard.SampleX(sample + 1), board.SampleHeight(sample + 1));
            while (SwoopBoard.SampleX(sample) >= boundary)
            {
                island++;
                boundary = terrain.IslandEnd(island);
            }

            var slot = PaletteSlot(island);
            var dark = (FloorDivide(sample, StripeSamples) & 1) == 1;
            drawList.AddQuadFilled(previous, current, current + topDepth, previous + topDepth,
                colors[slot + (dark ? SlotTopDark : SlotTopLight)]);
            drawList.AddQuadFilled(previous + topDepth, current + topDepth, current + midDepth, previous + midDepth,
                colors[slot + (dark ? SlotMidDark : SlotMidLight)]);
            drawList.AddQuadFilled(previous + midDepth, current + midDepth, new Vector2(current.X, MathF.Max(bottom, current.Y + midDepth.Y)),
                new Vector2(previous.X, MathF.Max(bottom, previous.Y + midDepth.Y)), colors[slot + (dark ? SlotDeepDark : SlotDeepLight)]);
            previous = current;
        }

        DrawRims(drawList, board, in camera, first, end, scale);
        var shadeTop = ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.08f, 0f));
        var shadeBottom = ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.08f, 0.38f));
        drawList.AddRectFilledMultiColor(new Vector2(area.Min.X, area.Max.Y - area.Height * 0.22f), area.Max, shadeTop, shadeTop,
            shadeBottom, shadeBottom);
    }

    private void DrawRims(ImDrawListPtr drawList, SwoopBoard board, in Camera2D camera, int first, int end, float scale)
    {
        var terrain = board.Terrain;
        var thickness = MathF.Max(2f * scale, RimThickness * camera.Px(1f));
        var runStart = first;
        var island = terrain.IslandIndexAt(SwoopBoard.SampleX(first));
        var boundary = island < 0 ? terrain.IslandStart(0) : terrain.IslandEnd(island);
        for (var sample = first; sample <= end; sample++)
        {
            if (sample < end && SwoopBoard.SampleX(sample) < boundary)
            {
                continue;
            }

            var slot = PaletteSlot(island);
            StrokeRun(drawList, board, in camera, runStart, sample, colors[slot + SlotRim], thickness, 0f);
            StrokeRun(drawList, board, in camera, runStart, sample, colors[slot + SlotRimHighlight], thickness * 0.35f, -thickness * 0.3f);
            runStart = sample;
            while (SwoopBoard.SampleX(sample) >= boundary)
            {
                island++;
                boundary = terrain.IslandEnd(island);
            }
        }
    }

    private static void StrokeRun(ImDrawListPtr drawList, SwoopBoard board, in Camera2D camera, int from, int to, uint color,
        float thickness, float lift)
    {
        if (to <= from)
        {
            return;
        }

        drawList.PathClear();
        for (var sample = from; sample <= to; sample++)
        {
            var point = Screen(in camera, SwoopBoard.SampleX(sample), board.SampleHeight(sample));
            drawList.PathLineTo(new Vector2(point.X, point.Y + lift));
        }

        drawList.PathStroke(color, ImDrawFlags.None, thickness);
    }

    public void DrawMarkers(ImDrawListPtr drawList, SwoopBoard board, in Camera2D camera, Rect area, in SwoopLighting lighting,
        float zoomOut, float time, float scale)
    {
        var terrain = board.Terrain;
        var left = WorldX(in camera, area.Min.X) - FlagLength - 2.0;
        var right = WorldX(in camera, area.Max.X) + 2.0;
        var island = Math.Max(1, terrain.IslandIndexAt(left) + 1);
        var pixels = camera.Px(1f);
        var labelScale = Math.Clamp(1f / MathF.Max(1f, zoomOut), 0.55f, 1f);
        while (island < SwoopTerrain.MaxIslands && terrain.IslandStart(island) <= right)
        {
            var x = terrain.IslandStart(island);
            var ground = (float)terrain.Height(x);
            var reached = island <= board.CurrentIsland;
            var flagTone = SwoopShapes.Multiply(reached ? Gold : StripeLight[island % PaletteCount], lighting.Tint);
            var foot = Screen(in camera, x, ground);
            var top = Screen(in camera, x, ground + PoleHeight);
            var stoneHalf = new Vector2(0.7f * pixels, 0.35f * pixels);
            drawList.AddRectFilled(foot - stoneHalf, foot + stoneHalf,
                ImGui.GetColorU32(SwoopShapes.Multiply(Stone, lighting.Tint)), stoneHalf.Y);
            drawList.AddLine(foot, top, ImGui.GetColorU32(SwoopShapes.Multiply(Pole, lighting.Tint)), MathF.Max(2f * scale, 0.26f * pixels));
            var wave = MathF.Sin(time * 4f + island) * 0.3f * pixels;
            var flagTop = top + new Vector2(0f, 0.2f * pixels);
            var tipTop = flagTop + new Vector2(FlagLength * pixels, 0.25f * pixels + wave);
            var tipBottom = flagTop + new Vector2(FlagLength * pixels, FlagHeight * pixels * 0.9f + wave * 0.6f);
            var flagBottom = flagTop + new Vector2(0f, FlagHeight * pixels);
            drawList.AddQuadFilled(flagTop, tipTop, tipBottom, flagBottom, ImGui.GetColorU32(flagTone));
            drawList.AddLine(flagTop, tipTop, ImGui.GetColorU32(GamePalette.Lighten(flagTone, 0.35f)), MathF.Max(1f, 0.12f * pixels));
            var label = GameNumber.Label(island + 1);
            var labelCenter = (flagTop + tipTop + tipBottom + flagBottom) * 0.25f;
            Typography.DrawCentered(drawList, labelCenter, label, GamePalette.InkOn(flagTone), TextStyles.Caption1.Scale * labelScale,
                FontWeight.Bold);
            var pulse = 0.7f + 0.3f * MathF.Sin(time * 3f + island);
            SwoopShapes.Glow(drawList, top, 1.6f * pixels, reached ? Gold : new Vector4(1f, 0.95f, 0.8f, 1f),
                (0.8f + lighting.Night) * pulse);
            drawList.AddCircleFilled(top, MathF.Max(2f * scale, 0.42f * pixels), ImGui.GetColorU32(reached ? Gold : new Vector4(1f, 0.97f, 0.88f, 1f)),
                16);
            island++;
        }
    }

    public void DrawCrystals(ImDrawListPtr drawList, SwoopBoard board, in Camera2D camera, Rect area, in SwoopLighting lighting,
        float time, float scale)
    {
        var left = WorldX(in camera, area.Min.X) - 2.0;
        var right = WorldX(in camera, area.Max.X) + 2.0;
        var radius = MathF.Max(3f * scale, SwoopBoard.CrystalRadius * camera.Px(1f));
        var light = ImGui.GetColorU32(SwoopShapes.Mix(CrystalLight, SwoopShapes.Multiply(CrystalLight, lighting.Tint), 0.3f));
        var dark = ImGui.GetColorU32(SwoopShapes.Mix(CrystalDark, SwoopShapes.Multiply(CrystalDark, lighting.Tint), 0.3f));
        var glint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f));
        for (var index = 0; index < board.CrystalCount; index++)
        {
            ref readonly var crystal = ref board.Crystal(index);
            if (crystal.X < left || crystal.X > right)
            {
                continue;
            }

            var bob = MathF.Sin(time * 2.4f + crystal.Phase) * 0.25f;
            var center = Screen(in camera, crystal.X, crystal.Y + bob);
            var spin = MathF.Abs(MathF.Cos(time * 1.8f + crystal.Phase)) * 0.75f + 0.25f;
            SwoopShapes.Glow(drawList, center, radius * 2.6f, CrystalLight, 0.6f + lighting.Night * 0.6f);
            var top = center + new Vector2(0f, -radius * 1.35f);
            var bottom = center + new Vector2(0f, radius * 1.35f);
            var leftPoint = center + new Vector2(-radius * 0.85f * spin, 0f);
            var rightPoint = center + new Vector2(radius * 0.85f * spin, 0f);
            drawList.AddTriangleFilled(top, leftPoint, bottom, light);
            drawList.AddTriangleFilled(top, bottom, rightPoint, dark);
            drawList.AddLine(top + new Vector2(-radius * 0.2f * spin, radius * 0.5f), top + new Vector2(0f, radius * 0.25f), glint,
                MathF.Max(1f, radius * 0.18f));
        }
    }

    public static void DrawDarkness(ImDrawListPtr drawList, Rect area, SwoopBoard board, in SwoopLighting lighting, float time,
        float scale)
    {
        var approach = board.Night ? 1f : SwoopShapes.Smooth(14f, 0f, board.Clock);
        if (approach > 0f)
        {
            var sweep = board.Night ? SwoopShapes.Smooth(0f, 1.6f, board.NightSeconds) : 0f;
            var edge = area.Min.X + area.Width * (-0.15f + 0.5f * approach + 0.9f * sweep);
            var fade = area.Width * 0.35f;
            var solidEdge = edge - fade;
            var dark = ImGui.GetColorU32(Dusk with { W = 0.62f });
            var clear = ImGui.GetColorU32(Dusk with { W = 0f });
            if (solidEdge > area.Min.X)
            {
                drawList.AddRectFilled(area.Min, new Vector2(MathF.Min(solidEdge, area.Max.X), area.Max.Y), dark);
            }

            if (solidEdge < area.Max.X)
            {
                drawList.AddRectFilledMultiColor(new Vector2(solidEdge, area.Min.Y), new Vector2(edge, area.Max.Y), dark, clear, clear, dark);
                var shimmer = 0.18f + 0.1f * MathF.Sin(time * 3f);
                drawList.AddLine(new Vector2(solidEdge, area.Min.Y), new Vector2(solidEdge, area.Max.Y),
                    ImGui.GetColorU32(DuskEdge with { W = shimmer * approach * (1f - sweep) }), 2f * scale);
            }
        }

        if (lighting.Night <= 0f)
        {
            return;
        }

        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.12f, 0.3f * lighting.Night)));
    }

    public static void DrawSpeedLines(ImDrawListPtr drawList, Rect area, float speed, float time, float scale)
    {
        var intensity = SwoopShapes.Smooth(24f, 38f, speed);
        if (intensity <= 0f)
        {
            return;
        }

        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f * intensity));
        var thickness = MathF.Max(1f, 1.4f * scale);
        for (var line = 0; line < SpeedLineCount; line++)
        {
            var y = area.Min.Y + area.Height * (0.14f + 0.72f * SpeedLineY[line]);
            var travel = SwoopShapes.Fraction(time * SpeedLineRate[line] * (1f + intensity) + SpeedLinePhase[line]);
            var x = area.Max.X + area.Width * 0.2f - travel * area.Width * 1.5f;
            var length = area.Width * (0.1f + 0.18f * SpeedLineLength[line]) * intensity;
            drawList.AddLine(new Vector2(x, y), new Vector2(x + length, y), color, thickness);
        }
    }

    public static void DrawAltitudeArrow(ImDrawListPtr drawList, Vector2 tip, string label, Vector4 accent, float time,
        float scale)
    {
        var size = 9f * scale;
        var pulse = 0.75f + 0.25f * MathF.Sin(time * 6f);
        SwoopShapes.Glow(drawList, tip + new Vector2(0f, size), size * 2.4f, accent, pulse);
        drawList.AddTriangleFilled(tip, tip + new Vector2(size, size * 1.3f), tip + new Vector2(-size, size * 1.3f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)));
        Typography.DrawCentered(drawList, tip + new Vector2(0f, size * 2.6f), label, new Vector4(1f, 1f, 1f, 0.95f),
            TextStyles.Caption1.Scale, FontWeight.Bold);
    }

    private static int PaletteSlot(int island) => (Math.Max(0, island) % PaletteCount) * SlotsPerPalette;

    private static int FloorDivide(int value, int divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
}
