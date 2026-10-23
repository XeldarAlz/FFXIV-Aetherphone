using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal static class EclipseSky
{
    private const int StarCount = 74;
    private const int SparkleShare = 7;
    private const int MoteCount = 16;
    private const int HeartEvery = 4;
    private const int Seed = 20261031;
    private const float StarTopBias = 1.45f;
    private const float StarMinRadius = 0.45f;
    private const float StarRadiusRange = 0.85f;
    private const float StarMinAlpha = 0.16f;
    private const float StarAlphaRange = 0.42f;
    private const float StarMinPeriod = 2.8f;
    private const float StarPeriodRange = 4.2f;
    private const float StarTwinkleDepth = 0.55f;
    private const float StarMinDepth = 0.04f;
    private const float StarDepthRange = 0.16f;
    private const float SparkleMinExtent = 2.6f;
    private const float SparkleExtentRange = 2.4f;
    private const float MoteMinSeconds = 13f;
    private const float MoteSecondsRange = 10f;
    private const float MoteMinRadius = 1.1f;
    private const float MoteRadiusRange = 1.5f;
    private const float MoteMinSway = 5f;
    private const float MoteSwayRange = 11f;
    private const float MoteTravel = 1.15f;
    private const float MotePeakAlpha = 0.60f;
    private const float MoteHeartSize = 6.5f;
    private const float MoteLift = 0.38f;
    private const float MoonX = 0.5f;
    private const float MoonY = 0.028f;
    private const float MoonRadius = 0.22f;
    private const float MoonDepth = 0.22f;
    private const double MoonBreath = 7000.0;
    private const float AmbientMoonStrength = 0.75f;
    private const float ShootingPeriod = 12.5f;
    private const float ShootingWindow = 0.95f;
    private const int ShootingTail = 12;
    private const float ShootingLength = 0.34f;

    private static readonly Vector4 Violet = new(0.43f, 0.13f, 0.58f, 1f);
    private static readonly Vector4 Crimson = new(0.62f, 0.03f, 0.17f, 1f);
    private static readonly Vector4 StarInk = new(1f, 0.93f, 0.96f, 1f);
    private static readonly Vector4 Silver = new(0.93f, 0.92f, 0.96f, 1f);
    private static readonly Vector4 MoonFace = new(0.035f, 0.008f, 0.020f, 1f);
    private static readonly Star[] Stars = BuildStars();
    private static readonly Mote[] Motes = BuildMotes();
    private static readonly Nebula[] Clouds =
    {
        new(new Vector2(0.86f, 0.06f), 0.95f, 0, 0.30f, 41f, 0.05f),
        new(new Vector2(0.08f, 0.42f), 0.75f, 1, 0.16f, 53f, 0.06f),
        new(new Vector2(0.72f, 0.88f), 0.85f, 2, 0.20f, 47f, 0.05f),
        new(new Vector2(0.20f, 0.02f), 0.55f, 2, 0.14f, 37f, 0.04f),
    };
    private static readonly Vector2[] ShootingStarts =
    {
        new(0.10f, 0.05f), new(0.55f, 0.02f), new(0.25f, 0.20f),
    };

    private readonly record struct Star(Vector2 Position, float Radius, float Alpha, float Period, float Phase,
        float Depth, bool Sparkle);

    private readonly record struct Mote(float X, float Seconds, float Offset, float Radius, float Sway,
        float SwayRate, bool Heart);

    private readonly record struct Nebula(Vector2 Anchor, float Reach, int Tone, float Alpha, float Period,
        float Wander);

    public static void Paint(ImDrawListPtr drawList, Rect target, Rect frame, float rounding, Vector4 accent,
        float drift, bool moon)
    {
        if (target.Width <= 1f || target.Height <= 1f)
        {
            return;
        }

        var scale = UiScale.Current;
        var time = (float)ImGui.GetTime();
        drawList.PushClipRect(target.Min, target.Max, true);
        PaintClouds(drawList, frame, accent, time, drift);
        PaintStars(drawList, target, frame, rounding, time, drift, scale);
        if (moon)
        {
            PaintMoon(drawList, frame, accent, time, drift, scale);
        }

        PaintShootingStar(drawList, frame, accent, time, scale);
        PaintMotes(drawList, target, frame, accent, time, scale);
        drawList.PopClipRect();
    }

    private static void PaintClouds(ImDrawListPtr drawList, Rect frame, Vector4 accent, float time, float drift)
    {
        var width = frame.Width;
        for (var index = 0; index < Clouds.Length; index++)
        {
            var cloud = Clouds[index];
            var angle = time * MathF.Tau / cloud.Period;
            var wander = new Vector2(MathF.Sin(angle) * cloud.Wander, MathF.Cos(angle * 0.7f) * cloud.Wander);
            var center = new Vector2(frame.Min.X + (cloud.Anchor.X + wander.X) * width,
                frame.Min.Y + (cloud.Anchor.Y + wander.Y) * frame.Height - drift * 0.10f);
            var breath = 0.82f + 0.18f * MathF.Sin(angle * 1.3f + index);
            var tone = cloud.Tone == 0 ? accent : cloud.Tone == 1 ? Violet : Crimson;
            SoftGlow.Draw(drawList, center, cloud.Reach * width, tone with { W = cloud.Alpha * breath });
        }
    }

    private static void PaintStars(ImDrawListPtr drawList, Rect target, Rect frame, float rounding, float time,
        float drift, float scale)
    {
        var height = frame.Height;
        for (var index = 0; index < Stars.Length; index++)
        {
            var star = Stars[index];
            var y = star.Position.Y * height - drift * star.Depth;
            y -= MathF.Floor(y / height) * height;
            var center = new Vector2(frame.Min.X + star.Position.X * frame.Width, frame.Min.Y + y);
            if (center.Y < target.Min.Y - 8f || center.Y > target.Max.Y + 8f)
            {
                continue;
            }

            if (!InsideCorners(center, frame, rounding, 6f * scale))
            {
                continue;
            }

            var wave = MathF.Sin(time * MathF.Tau / star.Period + star.Phase);
            var twinkle = 1f - StarTwinkleDepth * 0.5f * (1f + wave);
            var alpha = star.Alpha * twinkle;
            if (star.Sparkle)
            {
                EclipseFx.Sparkle(drawList, center, star.Radius * scale * (0.75f + 0.25f * twinkle),
                    StarInk with { W = MathF.Min(1f, alpha * 1.6f) });
                continue;
            }

            drawList.AddCircleFilled(center, star.Radius * scale, ImGui.GetColorU32(StarInk with { W = alpha }), 8);
        }
    }

    private static void PaintMoon(ImDrawListPtr drawList, Rect frame, Vector4 accent, float time, float drift,
        float scale)
    {
        var radius = frame.Width * MoonRadius;
        var center = new Vector2(frame.Min.X + frame.Width * MoonX,
            frame.Min.Y + frame.Height * MoonY - drift * MoonDepth);
        Moon(drawList, center, radius, accent, time, scale, false);
    }

    public static void Moon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float scale) =>
        Moon(drawList, center, radius, accent, (float)ImGui.GetTime(), scale, true);

    private static void Moon(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float time,
        float scale, bool solid)
    {
        var breath = EclipseFx.Breath(MoonBreath);
        var strength = solid ? 1f : AmbientMoonStrength;
        SoftGlow.Draw(drawList, center, radius * 3.4f, accent with { W = (0.20f + 0.08f * breath) * strength });
        SoftGlow.Draw(drawList, center, radius * 1.75f,
            EclipseFx.Rose(accent, 0.25f) with { W = (0.42f + 0.12f * breath) * strength });
        if (solid)
        {
            drawList.AddCircleFilled(center, radius * 1.03f,
                ImGui.GetColorU32(EclipseFx.Rose(accent, 0.45f) with { W = 0.55f }), 64);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoonFace), 64);
        }
        else
        {
            SoftGlow.Draw(drawList, center, radius * 1.15f, MoonFace with { W = 0.75f });
            drawList.AddCircle(center, radius, ImGui.GetColorU32(EclipseFx.Rose(accent, 0.5f) with { W = 0.22f }), 64,
                1f * scale);
        }

        Arc(drawList, center, radius * 0.985f, 0.06f, 0.94f, Silver with { W = 0.30f * strength }, 1.3f * scale);
        Arc(drawList, center, radius * 0.985f, 0.18f, 0.82f, Silver with { W = 0.55f * strength }, 1.9f * scale);
        Arc(drawList, center, radius * 0.985f, 0.32f, 0.68f, Silver with { W = 0.85f * strength }, 2.6f * scale);
        var twinkle = 0.55f + 0.45f * MathF.Sin(time * 1.7f);
        EclipseFx.Sparkle(drawList, new Vector2(center.X, center.Y - radius * 1.55f),
            radius * (0.20f + 0.06f * twinkle), Silver with { W = (0.70f + 0.30f * twinkle) * strength });
        SoftGlow.Draw(drawList, new Vector2(center.X, center.Y + radius * 0.985f), radius * 0.55f,
            Silver with { W = (0.22f + 0.14f * twinkle) * strength });
    }

    private static void Arc(ImDrawListPtr drawList, Vector2 center, float radius, float fromHalfTurns,
        float toHalfTurns, Vector4 color, float thickness)
    {
        drawList.PathArcTo(center, radius, fromHalfTurns * MathF.PI, toHalfTurns * MathF.PI, 36);
        drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.None, thickness);
    }

    private static void PaintShootingStar(ImDrawListPtr drawList, Rect frame, Vector4 accent, float time, float scale)
    {
        var cycle = MathF.Floor(time / ShootingPeriod);
        var local = (time - cycle * ShootingPeriod) / ShootingWindow;
        if (local >= 1f)
        {
            return;
        }

        var start = ShootingStarts[(int)cycle % ShootingStarts.Length];
        var origin = new Vector2(frame.Min.X + start.X * frame.Width, frame.Min.Y + start.Y * frame.Height);
        var travel = new Vector2(frame.Width * ShootingLength, frame.Width * ShootingLength * 0.42f);
        var eased = 1f - (1f - local) * (1f - local);
        var head = origin + travel * eased;
        var fade = MathF.Sin(local * MathF.PI);
        var tail = travel * 0.38f;
        for (var segment = 0; segment < ShootingTail; segment++)
        {
            var near = segment / (float)ShootingTail;
            var far = (segment + 1) / (float)ShootingTail;
            var alpha = fade * (1f - near) * 0.85f;
            drawList.AddLine(head - tail * near, head - tail * far,
                ImGui.GetColorU32(StarInk with { W = alpha }), (1.6f - near) * scale);
        }

        SoftGlow.Draw(drawList, head, 7f * scale, EclipseFx.Rose(accent, 0.55f) with { W = 0.75f * fade });
        drawList.AddCircleFilled(head, 1.4f * scale, ImGui.GetColorU32(StarInk with { W = fade }), 8);
    }

    private static void PaintMotes(ImDrawListPtr drawList, Rect target, Rect frame, Vector4 accent, float time,
        float scale)
    {
        var ink = EclipseFx.Rose(accent, MoteLift);
        var travel = frame.Height * MoteTravel;
        for (var index = 0; index < Motes.Length; index++)
        {
            var mote = Motes[index];
            var progress = (time / mote.Seconds + mote.Offset) % 1f;
            var y = frame.Max.Y + frame.Height * 0.05f - progress * travel;
            if (y < target.Min.Y - 10f || y > target.Max.Y + 10f)
            {
                continue;
            }

            var x = frame.Min.X + mote.X * frame.Width + MathF.Sin(time * mote.SwayRate + index) * mote.Sway * scale;
            var rise = MathF.Sin(progress * MathF.PI);
            var alpha = MotePeakAlpha * rise * rise;
            var center = new Vector2(x, y);
            if (mote.Heart)
            {
                SoftGlow.Draw(drawList, center, MoteHeartSize * 1.8f * scale, accent with { W = alpha * 0.45f });
                EclipseFx.Heart(drawList, center, MoteHeartSize * scale, ink with { W = alpha });
                continue;
            }

            drawList.AddCircleFilled(center, mote.Radius * 3f * scale,
                ImGui.GetColorU32(accent with { W = alpha * 0.20f }), 12);
            drawList.AddCircleFilled(center, mote.Radius * scale, ImGui.GetColorU32(ink with { W = alpha }), 10);
        }
    }

    private static bool InsideCorners(Vector2 point, Rect frame, float rounding, float margin)
    {
        var cornerX = MathF.Max(MathF.Max(frame.Min.X + rounding - point.X, point.X - (frame.Max.X - rounding)), 0f);
        var cornerY = MathF.Max(MathF.Max(frame.Min.Y + rounding - point.Y, point.Y - (frame.Max.Y - rounding)), 0f);
        var limit = MathF.Max(0f, rounding - margin);
        return cornerX * cornerX + cornerY * cornerY <= limit * limit;
    }

    private static Star[] BuildStars()
    {
        var random = new Random(Seed);
        var stars = new Star[StarCount];
        for (var index = 0; index < stars.Length; index++)
        {
            var sparkle = index % SparkleShare == 0;
            var position = new Vector2(random.NextSingle(), MathF.Pow(random.NextSingle(), StarTopBias));
            var radius = sparkle
                ? SparkleMinExtent + random.NextSingle() * SparkleExtentRange
                : StarMinRadius + random.NextSingle() * StarRadiusRange;
            stars[index] = new Star(position, radius, StarMinAlpha + random.NextSingle() * StarAlphaRange,
                StarMinPeriod + random.NextSingle() * StarPeriodRange, random.NextSingle() * MathF.Tau,
                StarMinDepth + random.NextSingle() * StarDepthRange, sparkle);
        }

        return stars;
    }

    private static Mote[] BuildMotes()
    {
        var random = new Random(Seed + 1);
        var motes = new Mote[MoteCount];
        for (var index = 0; index < motes.Length; index++)
        {
            motes[index] = new Mote(0.04f + random.NextSingle() * 0.92f,
                MoteMinSeconds + random.NextSingle() * MoteSecondsRange, random.NextSingle(),
                MoteMinRadius + random.NextSingle() * MoteRadiusRange,
                MoteMinSway + random.NextSingle() * MoteSwayRange,
                0.35f + random.NextSingle() * 0.45f, index % HeartEvery == 1);
        }

        return motes;
    }
}
