using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class NightScene
{
    private const float SceneSpan = 900f;
    private const int CircleSegments = 32;
    private const int SmallSegments = 12;
    private const int MaxGlowCells = 16;
    private const int HaloCells = 16;
    private const float HaloReach = 3.1f;
    private const float VeilReach = 290f;
    private const float BloodVeilReach = 380f;

    private const float MoonlitMoonX = 0.6f;
    private const float MoonlitMoonRadius = 46f;
    private const float FarPineBase = 112f;
    private const float NearPineBase = 44f;
    private const float EyeBlinkStart = 0.955f;
    private const float EyeWakeEnd = 0.06f;
    private const float RouseSeconds = 4f;
    private const float RouseHaloBoost = 0.9f;

    private const float BloodMoonX = 0.16f;
    private const float BloodMoonRadius = 58f;
    private const float CastleBase = 40f;
    private const float CandleReach = 300f;

    private const float WitchingMoonRadius = 22f;
    private const float WitchingMoonPhase = 0.87f;
    private const float WitchingVeilReach = 320f;
    private const float KindleSeconds = 3f;
    private const float KindleGlowBoost = 1.6f;
    private const float FullMoonPhase = 0.5f;

    private const float IntroRise = 90f;
    private const float DayDip = 26f;
    private const float StarParallax = 0.5f;
    private const float HalloweenNightGrow = 1.15f;
    private const float HalloweenNightHalo = 1.45f;

    private readonly record struct Star(float X, float Y, float Radius, float Alpha, float Speed, float Phase);

    private readonly record struct Pine(float X, float Width, float Height, float Drop);

    private readonly record struct Eye(float X, float Lift, float Cycle, float Offset, float Size);

    private readonly record struct Tower(float X, float Width, float Height, bool Spire);

    private readonly record struct Pane(float X, float Lift, float Phase, float Speed);

    private readonly record struct Flyer(float X, float Y, float RadiusX, float RadiusY, float Speed, float Phase,
        float Size);

    private readonly record struct Puff(float Lift, float Speed, float Offset, float Radius);

    private static readonly Vector4 MoonlitHorizon = new(0.059f, 0.086f, 0.259f, 0.7f);
    private static readonly Vector4 MoonlitStarInk = new(0.894f, 0.918f, 1f, 1f);
    private static readonly Vector4 MoonlitHalo = new(0.80f, 0.85f, 1f, 0.34f);
    private static readonly Vector4 MoonRim = new(0.722f, 0.761f, 0.886f, 1f);
    private static readonly Vector4 MoonFace = new(0.867f, 0.890f, 0.965f, 1f);
    private static readonly Vector4 MoonShine = new(1f, 1f, 1f, 0.6f);
    private static readonly Vector4 MoonCrater = new(0.55f, 0.6f, 0.77f, 0.22f);
    private static readonly Vector4 CloudInk = new(0.03f, 0.043f, 0.133f, 0.32f);
    private static readonly Vector4 FarPineInk = new(0.051f, 0.075f, 0.22f, 1f);
    private static readonly Vector4 NearPineInk = new(0.012f, 0.016f, 0.047f, 1f);
    private static readonly Vector4 MoonlitMist = new(0.667f, 0.729f, 0.925f, 0.08f);
    private static readonly Vector4 MoonlitVeil = new(0.024f, 0.027f, 0.102f, 0.8f);
    private static readonly Vector4 EyeGlow = new(1f, 0.808f, 0.29f, 0.28f);
    private static readonly Vector4 EyeInk = new(1f, 0.878f, 0.471f, 1f);

    private static readonly Vector4 BloodHorizon = new(0.165f, 0.024f, 0.063f, 0.7f);
    private static readonly Vector4 BloodStarInk = new(1f, 0.84f, 0.84f, 0.6f);
    private static readonly Vector4 BloodHalo = new(0.9f, 0.157f, 0.235f, 0.4f);
    private static readonly Vector4 BloodRim = new(0.427f, 0.027f, 0.086f, 1f);
    private static readonly Vector4 EclipseRim = new(0.09f, 0.02f, 0.035f, 1f);
    private static readonly Vector4 EclipseFace = new(0.16f, 0.03f, 0.05f, 1f);
    private static readonly Vector4 BloodFace = new(0.784f, 0.094f, 0.18f, 1f);
    private static readonly Vector4 BloodShine = new(1f, 0.42f, 0.333f, 0.6f);
    private static readonly Vector4 BloodMottle = new(0.275f, 0f, 0.04f, 0.28f);
    private static readonly Vector4 BatInk = new(0.047f, 0.004f, 0.016f, 0.95f);
    private static readonly Vector4 CandleInk = new(1f, 0.471f, 0.188f, 0.12f);
    private static readonly Vector4 BloodMist = new(0.745f, 0.157f, 0.235f, 0.09f);
    private static readonly Vector4 BloodVeil = new(0.039f, 0.004f, 0.012f, 0.93f);
    private static readonly Vector4 CastleInk = new(0.027f, 0.004f, 0.012f, 1f);
    private static readonly Vector4 PaneInk = new(1f, 0.549f, 0.235f, 1f);

    private static readonly Vector4 WitchingHorizon = new(0.22f, 0.07f, 0.32f, 0.6f);
    private static readonly Vector4 WitchingStarInk = new(1f, 0.9f, 0.96f, 0.8f);
    private static readonly Vector4 WitchingHalo = new(0.78f, 0.62f, 1f, 0.3f);
    private static readonly Vector4 WitchingMoonLight = new(0.94f, 0.89f, 1f, 1f);
    private static readonly Vector4 WitchingMoonShade = new(0.94f, 0.89f, 1f, 0.12f);
    private static readonly Vector4 WitchingMist = new(0.62f, 0.4f, 0.86f, 0.09f);
    private static readonly Vector4 WitchingVeil = new(0.03f, 0.012f, 0.05f, 0.88f);

    private static readonly Vector3[] Craters =
    [
        new(-0.28f, -0.16f, 0.18f), new(0.24f, 0.2f, 0.24f), new(-0.08f, 0.4f, 0.12f), new(0.36f, -0.32f, 0.1f),
        new(-0.44f, 0.24f, 0.08f),
    ];

    private static readonly Vector3[] CloudBlobs =
    [
        new(-34f, 4f, 22f), new(-12f, -6f, 28f), new(14f, -2f, 30f), new(36f, 6f, 22f), new(4f, 10f, 26f),
    ];

    private static readonly Star[] MoonlitStars = StarsFrom(new Random(1031), 80);
    private static readonly Star[] BloodStars = StarsFrom(new Random(2027), 40);
    private static readonly Pine[] FarPines = PinesFrom(new Random(1032), 40f, 85f);
    private static readonly Pine[] NearPines = PinesFrom(new Random(1033), 55f, 115f);
    private static readonly Eye[] Eyes = EyesFrom(new Random(1034));
    private static readonly Puff[] MoonlitPuffs = PuffsFrom(new Random(1035));
    private static readonly Tower[] Towers = TowersFrom(new Random(2028));
    private static readonly Pane[] Panes = PanesFrom(Towers, new Random(2029));
    private static readonly Flyer[] Flyers = FlyersFrom(new Random(2030));
    private static readonly Puff[] BloodPuffs = PuffsFrom(new Random(2031));
    private static readonly Star[] WitchingStars = StarsFrom(new Random(3031), 60);
    private static readonly Puff[] WitchingPuffs = PuffsFrom(new Random(3033));

    private static float rousedAt = -100f;
    private static float kindledAt = -100f;

    public static Vector2 MoonlitMoonCenter(Rect frame, float moonY) =>
        new(frame.Min.X + frame.Width * MoonlitMoonX, moonY);

    public static float MoonlitMoonSize => MoonlitMoonRadius * UiScale.Current;

    public static Vector2 BloodMoonCenter(Rect frame, float moonY) => new(frame.Min.X + frame.Width * BloodMoonX, moonY);

    public static void Rouse() => rousedAt = (float)ImGui.GetTime();

    public static void Kindle() => kindledAt = (float)ImGui.GetTime();

    public static Vector2 MoonlitMoonCenter(Rect frame, float moonY, in NightView view) =>
        MoonlitMoonCenter(frame, MoonY(moonY, view));

    public static Vector2 BloodMoonCenter(Rect frame, float moonY, in NightView view) =>
        BloodMoonCenter(frame, MoonY(moonY, view));

    public static void Moonlit(ImDrawListPtr drawList, Rect frame, float moonY, in NightView view)
    {
        var scale = UiScale.Current;
        var time = (float)ImGui.GetTime();
        var rouse = Math.Clamp(1f - (time - rousedAt) / RouseSeconds, 0f, 1f);
        DrawHorizon(drawList, frame, MoonlitHorizon with { W = MoonlitHorizon.W * view.Reveal });
        DrawStars(drawList, frame, MoonlitStars, MoonlitStarInk, time, scale, view);
        var moon = MoonlitMoonCenter(frame, moonY, view);
        var radius = MoonlitMoonRadius * scale * (view.HalloweenNight ? HalloweenNightGrow : 1f);
        var halo = MoonlitHalo.W * (1f + rouse * RouseHaloBoost) * HaloStrength(view);
        Glow(drawList, moon, radius * HaloReach, MoonlitHalo with { W = halo }, HaloCells);
        DrawDisc(drawList, moon, radius, MoonRim, MoonFace, MoonShine, MoonCrater, -1f);
        DrawClouds(drawList, frame, moon, time, scale);
        var farBase = frame.Max.Y - FarPineBase * scale;
        DrawPines(drawList, frame, FarPines, farBase, Faded(FarPineInk, view.Reveal), scale);
        DrawPuffs(drawList, frame, MoonlitPuffs, farBase, MoonlitMist with { W = MoonlitMist.W * view.Reveal }, time,
            scale);
        DrawEyes(drawList, frame, farBase, time, rouse, scale);
        DrawPines(drawList, frame, NearPines, frame.Max.Y - NearPineBase * scale, Faded(NearPineInk, view.Reveal),
            scale);
        DrawVeil(drawList, frame, MoonlitVeil, VeilReach * scale);
    }

    public static void BloodMoon(ImDrawListPtr drawList, Rect frame, float moonY, in NightView view)
    {
        var scale = UiScale.Current;
        var time = (float)ImGui.GetTime();
        DrawHorizon(drawList, frame, BloodHorizon with { W = BloodHorizon.W * view.Reveal });
        DrawStars(drawList, frame, BloodStars, BloodStarInk, time, scale, view);
        var moon = BloodMoonCenter(frame, moonY, view);
        var radius = BloodMoonRadius * scale * (view.HalloweenNight ? HalloweenNightGrow : 1f);
        Glow(drawList, moon, radius * HaloReach, BloodHalo with { W = BloodHalo.W * HaloStrength(view) }, HaloCells);
        var face = Vector4.Lerp(EclipseFace, BloodFace, view.Reveal);
        var rim = Vector4.Lerp(EclipseRim, BloodRim, view.Reveal);
        DrawDisc(drawList, moon, radius, rim, face, BloodShine with { W = BloodShine.W * view.Reveal }, BloodMottle, 1f);
        DrawFlyers(drawList, moon, time, scale);
        DrawCandlelight(drawList, frame, scale);
        var castleBase = frame.Max.Y - CastleBase * scale;
        DrawPuffs(drawList, frame, BloodPuffs, castleBase, BloodMist with { W = BloodMist.W * view.Reveal }, time,
            scale);
        DrawCastle(drawList, frame, castleBase, time, scale);
        DrawVeil(drawList, frame, BloodVeil, BloodVeilReach * scale);
    }

    private static float MoonY(float moonY, in NightView view)
    {
        var scale = UiScale.Current;
        return moonY - view.Lift + (1f - view.Reveal) * IntroRise * scale + (1f - view.Nightness) * DayDip * scale;
    }

    private static float HaloStrength(in NightView view) =>
        view.Reveal * (0.6f + 0.4f * view.Nightness) * (view.HalloweenNight ? HalloweenNightHalo : 1f);

    private static uint Faded(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    public static float Kindling => Math.Clamp(1f - ((float)ImGui.GetTime() - kindledAt) / KindleSeconds, 0f, 1f);

    public static void Witching(ImDrawListPtr drawList, Rect frame, Vector2 moon, in NightView view)
    {
        var scale = UiScale.Current;
        var time = (float)ImGui.GetTime();
        DrawHorizon(drawList, frame, WitchingHorizon with { W = WitchingHorizon.W * view.Reveal });
        DrawStars(drawList, frame, WitchingStars, WitchingStarInk, time, scale, view);
        moon.Y = MoonY(moon.Y, view);
        var radius = WitchingMoonRadius * scale * (view.HalloweenNight ? HalloweenNightGrow : 1f);
        var halo = WitchingHalo.W * (1f + KindleGlowBoost * Kindling) * HaloStrength(view);
        Glow(drawList, moon, radius * HaloReach, WitchingHalo with { W = halo }, HaloCells);
        drawList.AddCircleFilled(moon, radius, Faded(WitchingMoonShade, view.Reveal), CircleSegments);
        var phase = view.HalloweenNight ? FullMoonPhase : WitchingMoonPhase;
        MoonPhase.Draw(drawList, moon, radius, phase, Faded(WitchingMoonLight, view.Reveal));
        DrawPuffs(drawList, frame, WitchingPuffs, frame.Max.Y - 60f * scale,
            WitchingMist with { W = WitchingMist.W * view.Reveal }, time, scale);
        DrawVeil(drawList, frame, WitchingVeil, WitchingVeilReach * scale);
    }

    public static void Glow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, int cells)
    {
        var count = Math.Clamp(cells, 1, MaxGlowCells);
        var columns = count + 1;
        Span<uint> colors = stackalloc uint[(MaxGlowCells + 1) * (MaxGlowCells + 1)];
        var origin = center - new Vector2(radius, radius);
        var step = radius * 2f / count;
        for (var row = 0; row < columns; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var point = origin + new Vector2(column * step, row * step);
                var falloff = Math.Clamp(1f - Vector2.Distance(point, center) / radius, 0f, 1f);
                falloff = falloff * falloff * (3f - 2f * falloff);
                colors[row * columns + column] = ImGui.GetColorU32(color with { W = color.W * falloff });
            }
        }

        for (var row = 0; row < count; row++)
        {
            for (var column = 0; column < count; column++)
            {
                var topLeft = colors[row * columns + column];
                var topRight = colors[row * columns + column + 1];
                var bottomLeft = colors[(row + 1) * columns + column];
                var bottomRight = colors[(row + 1) * columns + column + 1];
                if ((topLeft | topRight | bottomLeft | bottomRight) >> 24 == 0)
                {
                    continue;
                }

                var cellMin = origin + new Vector2(column * step, row * step);
                drawList.AddRectFilledMultiColor(cellMin, cellMin + new Vector2(step, step), topLeft, topRight,
                    bottomRight, bottomLeft);
            }
        }
    }

    public static void DrawBat(ImDrawListPtr drawList, Vector2 center, float size, float flap, uint ink)
    {
        drawList.AddCircleFilled(center, MathF.Abs(2.4f * size), ink, SmallSegments);
        drawList.AddTriangleFilled(center + new Vector2(-1.6f, -2.2f) * size, center + new Vector2(-1.9f, -4.6f) * size,
            center + new Vector2(-0.4f, -2.6f) * size, ink);
        drawList.AddTriangleFilled(center + new Vector2(1.6f, -2.2f) * size, center + new Vector2(1.9f, -4.6f) * size,
            center + new Vector2(0.4f, -2.6f) * size, ink);
        DrawWing(drawList, center, size, flap, -1f, ink);
        DrawWing(drawList, center, size, flap, 1f, ink);
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 center, float size, float flap, float side, uint ink)
    {
        var shoulder = center + new Vector2(side * 1.6f, -1f) * size;
        var root = center + new Vector2(side * 1.6f, 1.6f) * size;
        var tip = center + new Vector2(side * 14f, -3f - 9f * flap) * size;
        var outer = center + new Vector2(side * 11.5f, 1f - 4f * flap) * size;
        var middle = center + new Vector2(side * 8f, 2f - 2.5f * flap) * size;
        var inner = center + new Vector2(side * 4.8f, 2.6f - flap) * size;
        drawList.AddTriangleFilled(shoulder, tip, outer, ink);
        drawList.AddTriangleFilled(shoulder, outer, middle, ink);
        drawList.AddTriangleFilled(shoulder, middle, inner, ink);
        drawList.AddTriangleFilled(shoulder, inner, root, ink);
    }

    private static void DrawHorizon(ImDrawListPtr drawList, Rect frame, Vector4 tint)
    {
        var clear = ImGui.GetColorU32(tint with { W = 0f });
        var full = ImGui.GetColorU32(tint);
        var top = frame.Min.Y + frame.Height * 0.22f;
        var middle = frame.Min.Y + frame.Height * 0.45f;
        var bottom = frame.Min.Y + frame.Height * 0.75f;
        drawList.AddRectFilledMultiColor(new Vector2(frame.Min.X, top), new Vector2(frame.Max.X, middle), clear, clear,
            full, full);
        drawList.AddRectFilledMultiColor(new Vector2(frame.Min.X, middle), new Vector2(frame.Max.X, bottom), full, full,
            clear, clear);
    }

    private static void DrawStars(ImDrawListPtr drawList, Rect frame, Star[] stars, Vector4 ink, float time,
        float scale, in NightView view)
    {
        var drift = view.Lift * StarParallax;
        for (var index = 0; index < stars.Length; index++)
        {
            var star = stars[index];
            var twinkle = 0.55f + 0.45f * MathF.Sin(time * star.Speed + star.Phase);
            var alpha = ink.W * star.Alpha * twinkle * view.Dimming * view.StarSweep(star.X);
            if (alpha <= 0.002f)
            {
                continue;
            }

            var center = new Vector2(frame.Min.X + star.X * frame.Width, frame.Min.Y + star.Y * frame.Height - drift);
            drawList.AddCircleFilled(center, star.Radius * scale, ImGui.GetColorU32(ink with { W = alpha }), 8);
        }
    }

    private static void DrawVeil(ImDrawListPtr drawList, Rect frame, Vector4 tint, float reach)
    {
        var clear = ImGui.GetColorU32(tint with { W = 0f });
        var frost = ImGui.GetColorU32(tint);
        drawList.AddRectFilledMultiColor(new Vector2(frame.Min.X, frame.Max.Y - reach), frame.Max, clear,
            clear, frost, frost);
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 rim, Vector4 face,
        Vector4 shine, Vector4 mark, float light)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(rim), CircleSegments);
        var lean = new Vector2(light, light) * (radius * 0.1f);
        drawList.AddCircleFilled(center + lean, radius * 0.84f, ImGui.GetColorU32(face), CircleSegments);
        Glow(drawList, center + lean * 3f, radius * 0.5f, shine, 8);
        var markColor = ImGui.GetColorU32(mark);
        for (var index = 0; index < Craters.Length; index++)
        {
            var crater = Craters[index];
            drawList.AddCircleFilled(center + new Vector2(crater.X, crater.Y) * radius, crater.Z * radius, markColor,
                SmallSegments);
        }
    }

    private static void DrawClouds(ImDrawListPtr drawList, Rect frame, Vector2 moon, float time, float scale)
    {
        var span = frame.Width + 300f * scale;
        for (var cloud = 0; cloud < 2; cloud++)
        {
            var drift = Wrap((time * (5f + cloud * 3f) + cloud * 160f) * scale, span);
            var anchor = new Vector2(frame.Min.X - 150f * scale + drift, moon.Y + (18f + cloud * 30f) * scale);
            for (var index = 0; index < CloudBlobs.Length; index++)
            {
                var blob = CloudBlobs[index];
                Glow(drawList, anchor + new Vector2(blob.X, blob.Y) * scale, blob.Z * 1.4f * scale, CloudInk, 6);
            }
        }
    }

    private static void DrawPines(ImDrawListPtr drawList, Rect frame, Pine[] pines, float baseY, uint ink,
        float scale)
    {
        drawList.AddRectFilled(new Vector2(frame.Min.X, baseY), frame.Max, ink);
        for (var index = 0; index < pines.Length; index++)
        {
            var pine = pines[index];
            var centerX = frame.Min.X + pine.X * scale;
            var width = pine.Width * scale;
            if (centerX - width > frame.Max.X)
            {
                break;
            }

            var height = pine.Height * scale;
            var foot = baseY + pine.Drop * scale;
            DrawTier(drawList, centerX, foot, width, height * 0.52f, ink);
            DrawTier(drawList, centerX, foot - height * 0.3f, width * 0.72f, height * 0.48f, ink);
            DrawTier(drawList, centerX, foot - height * 0.56f, width * 0.46f, height * 0.44f, ink);
        }
    }

    private static void DrawTier(ImDrawListPtr drawList, float centerX, float bottom, float width, float height,
        uint ink) =>
        drawList.AddTriangleFilled(new Vector2(centerX - width * 0.5f, bottom), new Vector2(centerX, bottom - height),
            new Vector2(centerX + width * 0.5f, bottom), ink);

    private static void DrawPuffs(ImDrawListPtr drawList, Rect frame, Puff[] puffs, float baseY, Vector4 tint,
        float time, float scale)
    {
        for (var index = 0; index < puffs.Length; index++)
        {
            var puff = puffs[index];
            var radius = puff.Radius * scale;
            var span = frame.Width + radius * 4f;
            var center = new Vector2(frame.Min.X - radius * 2f + Wrap((time * puff.Speed + puff.Offset) * scale, span),
                baseY + puff.Lift * scale);
            Glow(drawList, center, radius, tint, 8);
        }
    }

    private static void DrawEyes(ImDrawListPtr drawList, Rect frame, float baseY, float time, float rouse, float scale)
    {
        for (var index = 0; index < Eyes.Length; index++)
        {
            var eye = Eyes[index];
            var phase = Wrap(time + eye.Offset, eye.Cycle) / eye.Cycle;
            var wake = MathF.Max(phase < EyeWakeEnd ? phase / EyeWakeEnd : 1f, rouse);
            var closed = phase > EyeBlinkStart && rouse <= 0f;
            var glow = EyeGlow with { W = MathF.Min(1f, EyeGlow.W * wake * (1f + rouse * 1.5f)) };
            var ink = ImGui.GetColorU32(EyeInk with { W = wake });
            var size = eye.Size * scale;
            var center = new Vector2(frame.Min.X + eye.X * frame.Width, baseY - eye.Lift * scale);
            var spread = new Vector2(4.5f * size, 0f);
            DrawEye(drawList, center - spread, size, closed, glow, ink);
            DrawEye(drawList, center + spread, size, closed, glow, ink);
        }
    }

    private static void DrawEye(ImDrawListPtr drawList, Vector2 center, float size, bool closed, Vector4 glow, uint ink)
    {
        Glow(drawList, center, 9f * size, glow, 6);
        if (closed)
        {
            var lid = new Vector2(2.2f * size, 0f);
            drawList.AddLine(center - lid, center + lid, ink, 1.2f * size);
            return;
        }

        drawList.AddCircleFilled(center, 1.9f * size, ink, SmallSegments);
    }

    private static void DrawFlyers(ImDrawListPtr drawList, Vector2 moon, float time, float scale)
    {
        var ink = ImGui.GetColorU32(BatInk);
        for (var index = 0; index < Flyers.Length; index++)
        {
            var flyer = Flyers[index];
            var angle = time * flyer.Speed + flyer.Phase;
            var center = new Vector2(moon.X + (flyer.X + MathF.Cos(angle) * flyer.RadiusX) * scale,
                moon.Y + (flyer.Y + MathF.Sin(angle * 1.3f) * flyer.RadiusY) * scale);
            var flap = MathF.Sin(time * 14f * (0.8f + flyer.Speed) + flyer.Phase);
            DrawBat(drawList, center, flyer.Size * scale, flap, ink);
        }
    }

    private static void DrawCandlelight(ImDrawListPtr drawList, Rect frame, float scale)
    {
        var clear = ImGui.GetColorU32(CandleInk with { W = 0f });
        var warm = ImGui.GetColorU32(CandleInk);
        drawList.AddRectFilledMultiColor(new Vector2(frame.Min.X, frame.Max.Y - CandleReach * scale), frame.Max, clear,
            clear, warm, warm);
    }

    private static void DrawCastle(ImDrawListPtr drawList, Rect frame, float baseY, float time, float scale)
    {
        var ink = ImGui.GetColorU32(CastleInk);
        drawList.AddRectFilled(new Vector2(frame.Min.X, baseY), frame.Max, ink);
        for (var index = 0; index < Towers.Length; index++)
        {
            var tower = Towers[index];
            var left = frame.Min.X + tower.X * scale;
            if (left > frame.Max.X)
            {
                break;
            }

            var width = tower.Width * scale;
            var height = tower.Height * scale;
            var top = baseY - height;
            drawList.AddRectFilled(new Vector2(left, top), new Vector2(left + width, baseY + 1f), ink);
            for (var merlon = 0f; merlon < tower.Width - 3f; merlon += 8f)
            {
                drawList.AddRectFilled(new Vector2(left + merlon * scale, top - 6f * scale),
                    new Vector2(left + (merlon + 4f) * scale, top + 1f), ink);
            }

            if (tower.Spire)
            {
                drawList.AddTriangleFilled(new Vector2(left - 2f * scale, top), new Vector2(left + width * 0.5f, top - height * 0.42f),
                    new Vector2(left + width + 2f * scale, top), ink);
            }
        }

        for (var index = 0; index < Panes.Length; index++)
        {
            var pane = Panes[index];
            var left = frame.Min.X + pane.X * scale;
            if (left > frame.Max.X)
            {
                continue;
            }

            var flicker = 0.6f + 0.25f * MathF.Sin(time * pane.Speed + pane.Phase) +
                0.15f * MathF.Sin(time * pane.Speed * 2.7f);
            var color = ImGui.GetColorU32(PaneInk with { W = flicker });
            var top = baseY - pane.Lift * scale;
            drawList.AddRectFilled(new Vector2(left, top + 3f * scale), new Vector2(left + 6f * scale, top + 11f * scale),
                color);
            drawList.AddCircleFilled(new Vector2(left + 3f * scale, top + 3f * scale), 3f * scale, color, 10);
        }
    }

    private static float Wrap(float value, float span) => value - MathF.Floor(value / span) * span;

    private static Star[] StarsFrom(Random random, int count)
    {
        var stars = new Star[count];
        for (var index = 0; index < count; index++)
        {
            stars[index] = new Star(random.NextSingle(), random.NextSingle() * 0.6f, 0.4f + random.NextSingle() * 1.1f,
                0.25f + random.NextSingle() * 0.6f, 0.6f + random.NextSingle() * 2.2f, random.NextSingle() * 7f);
        }

        return stars;
    }

    private static Pine[] PinesFrom(Random random, float minimumHeight, float maximumHeight)
    {
        var pines = new List<Pine>();
        var cursor = -12f;
        while (cursor < SceneSpan)
        {
            var width = 16f + random.NextSingle() * 16f;
            pines.Add(new Pine(cursor + width * 0.5f, width,
                minimumHeight + random.NextSingle() * (maximumHeight - minimumHeight), random.NextSingle() * 6f));
            cursor += width * (0.55f + random.NextSingle() * 0.35f);
        }

        return pines.ToArray();
    }

    private static Eye[] EyesFrom(Random random)
    {
        var eyes = new Eye[5];
        for (var index = 0; index < eyes.Length; index++)
        {
            eyes[index] = new Eye(0.08f + random.NextSingle() * 0.84f, 4f + random.NextSingle() * 30f,
                3f + random.NextSingle() * 5f, random.NextSingle() * 10f, 0.7f + random.NextSingle() * 0.6f);
        }

        return eyes;
    }

    private static Puff[] PuffsFrom(Random random)
    {
        var puffs = new Puff[6];
        for (var index = 0; index < puffs.Length; index++)
        {
            puffs[index] = new Puff(-20f + random.NextSingle() * 60f, 4f + random.NextSingle() * 6f,
                random.NextSingle() * SceneSpan, 60f + random.NextSingle() * 40f);
        }

        return puffs;
    }

    private static Tower[] TowersFrom(Random random)
    {
        var towers = new List<Tower>();
        var cursor = -6f;
        while (cursor < SceneSpan)
        {
            var width = 22f + random.NextSingle() * 26f;
            var spire = random.NextSingle() < 0.3f;
            var height = spire ? 78f + random.NextSingle() * 40f : 30f + random.NextSingle() * 48f;
            towers.Add(new Tower(cursor, width, height, spire));
            cursor += width + random.NextSingle() * 5f;
        }

        return towers.ToArray();
    }

    private static Pane[] PanesFrom(Tower[] towers, Random random)
    {
        var panes = new List<Pane>();
        for (var index = 0; index < towers.Length; index++)
        {
            var tower = towers[index];
            if (tower.Width <= 28f)
            {
                continue;
            }

            var rows = tower.Spire ? 2 : 1;
            for (var row = 0; row < rows; row++)
            {
                if (random.NextSingle() < 0.7f)
                {
                    panes.Add(new Pane(tower.X + tower.Width * 0.5f - 3f, tower.Height - 16f - row * 34f,
                        random.NextSingle() * 10f, 1.5f + random.NextSingle() * 3f));
                }
            }
        }

        return panes.ToArray();
    }

    private static Flyer[] FlyersFrom(Random random)
    {
        var flyers = new Flyer[6];
        for (var index = 0; index < flyers.Length; index++)
        {
            flyers[index] = new Flyer(20f + random.NextSingle() * 160f, 16f + random.NextSingle() * 120f,
                40f + random.NextSingle() * 90f, 16f + random.NextSingle() * 40f, 0.25f + random.NextSingle() * 0.35f,
                random.NextSingle() * 7f, 0.7f + random.NextSingle() * 0.5f);
        }

        return flyers;
    }
}
