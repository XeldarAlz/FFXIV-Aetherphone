using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal static class SkyParticles
{
    private const float GoldenStep = 0.618034f;
    private const double LightningPeriodSeconds = 6.8;
    private const int BoltSegments = 11;
    private const int BranchSegments = 5;
    private const int RayCount = 5;
    private const int FarFlakeCount = 46;
    private const int NearFlakeCount = 10;
    private const int GustCount = 4;
    private const int DustCount = 14;
    private const int EmberCount = 7;
    private const int MoteCount = 9;

    public static void Stars(in SkyCanvas canvas, Vector4 star, Vector4 glow, int count, float visibility)
    {
        var drawList = canvas.DrawList;
        var scale = canvas.Scale;
        var opacity = canvas.Opacity * visibility;
        for (var starIndex = 0; starIndex < count; starIndex++)
        {
            var position = new Vector2(canvas.Bounds.Min.X + SkyLayers.Hash(starIndex, 12.9898f) * canvas.Width,
                canvas.Bounds.Min.Y + MathF.Pow(SkyLayers.Hash(starIndex, 78.233f), 1.45f) * canvas.Height * 0.70f);
            var twinkle = 0.55f + 0.45f * SkyLayers.Wave(2.1 + SkyLayers.Hash(starIndex, 3.7f) * 3.4,
                SkyLayers.Hash(starIndex, 8.3f));
            var bright = starIndex % 9 == 0;
            var radius = (bright ? 1.25f : 0.55f + SkyLayers.Hash(starIndex, 9.1f) * 0.55f) * scale;
            var alpha = (bright ? 0.95f : 0.22f + 0.46f * SkyLayers.Hash(starIndex, 51.3f)) * twinkle * opacity;
            drawList.AddCircleFilled(position, radius, SkyLayers.Color(star, alpha), 10);
            if (bright)
            {
                SkyLayers.Glow(canvas, position, new Vector2(radius * 6f), glow, alpha * 0.45f);
            }
        }
    }

    public static void ShootingStar(in SkyCanvas canvas, Vector4 star, float visibility)
    {
        var cycle = SkyLayers.Phase(14.0);
        if (cycle > 0.05f)
        {
            return;
        }

        var travel = cycle / 0.05f;
        var lane = SkyLayers.Hash((int)(SkyLayers.Seconds / 14.0), 4.1f);
        var start = canvas.At(0.10f + lane * 0.35f, 0.06f + lane * 0.12f);
        var sweep = new Vector2(canvas.Width * 0.48f, canvas.Height * 0.20f);
        var head = start + sweep * travel;
        var direction = Vector2.Normalize(sweep);
        var tail = head - direction * 22f * canvas.Scale * (1f - travel * 0.4f);
        var alpha = MathF.Sin(travel * MathF.PI) * 0.9f * canvas.Opacity * visibility;
        canvas.DrawList.AddLine(tail, head, SkyLayers.Color(star, alpha * 0.6f), 1.2f * canvas.Scale);
        SkyLayers.Glow(canvas, head, new Vector2(4f * canvas.Scale), star, alpha);
    }

    public static void Luminary(in SkyCanvas canvas, Vector2 anchor, float daylight, in AmbienceInk ink,
        float strength)
    {
        var drawList = canvas.DrawList;
        var center = canvas.At(anchor.X, anchor.Y);
        var breath = 0.92f + 0.08f * SkyLayers.Wave(9.0);
        var opacity = canvas.Opacity * strength;
        var width = canvas.Width;
        SkyLayers.Glow(canvas, center, new Vector2(width * (0.70f + 0.45f * daylight)), ink.Glow,
            (0.12f + 0.16f * daylight) * breath * opacity);
        SkyLayers.Glow(canvas, center, new Vector2(width * 0.26f), ink.Glow, (0.20f + 0.20f * daylight) * opacity);
        SkyLayers.Glow(canvas, center, new Vector2(width * 0.085f), ink.Core, (0.55f + 0.35f * daylight) * opacity);
        var disc = width * (0.026f + 0.006f * daylight);
        drawList.AddCircleFilled(center, disc, SkyLayers.Color(ink.Core, 0.92f * opacity), 40);
        if (daylight >= 0.5f)
        {
            return;
        }

        var shadowOffset = new Vector2(disc * 0.42f, -disc * 0.30f) * (1f - daylight * 2f);
        drawList.AddCircleFilled(center + shadowOffset, disc * 0.92f,
            SkyLayers.Color(ink.Sky, 0.85f * (1f - daylight * 2f) * opacity), 40);
    }

    public static void SunRays(in SkyCanvas canvas, Vector2 anchor, Vector4 glow, float visibility)
    {
        if (canvas.Height < canvas.Width * 1.2f)
        {
            return;
        }

        var origin = canvas.At(anchor.X, anchor.Y);
        var sway = (SkyLayers.Wave(31.0) - 0.5f) * 0.06f;
        for (var rayIndex = 0; rayIndex < RayCount; rayIndex++)
        {
            var angle = MathF.PI * (0.56f + rayIndex * 0.085f) + sway * (1f + rayIndex * 0.2f);
            var length = canvas.Width * (1.05f + SkyLayers.Hash(rayIndex, 2.2f) * 0.55f);
            var width = canvas.Width * (0.10f + SkyLayers.Hash(rayIndex, 6.6f) * 0.10f);
            var flicker = 0.65f + 0.35f * SkyLayers.Wave(11.0 + rayIndex * 2.3, rayIndex * 0.37f);
            SkyLayers.Beam(canvas.DrawList, origin, angle, length, width, glow,
                0.075f * flicker * visibility * canvas.Opacity);
        }
    }

    public static float Flash()
    {
        var cycle = SkyLayers.Phase(LightningPeriodSeconds);
        return MathF.Min(1f, Spike(cycle, 0.030f, 0.026f) + Spike(cycle, 0.085f, 0.050f) * 0.6f);
    }

    public static void Bolt(in SkyCanvas canvas, Vector4 color, float flash)
    {
        var strike = (int)(SkyLayers.Seconds / LightningPeriodSeconds);
        Span<Vector2> points = stackalloc Vector2[BoltSegments + 1];
        var startX = 0.18f + SkyLayers.Hash(strike, 1.3f) * 0.64f;
        var endY = 0.34f + SkyLayers.Hash(strike, 2.9f) * 0.22f;
        var x = startX;
        for (var pointIndex = 0; pointIndex <= BoltSegments; pointIndex++)
        {
            var along = pointIndex / (float)BoltSegments;
            points[pointIndex] = canvas.At(x, along * endY);
            x += (SkyLayers.Hash(strike * 31 + pointIndex, 7.7f) - 0.5f) * 0.075f;
        }

        DrawJagged(canvas, points, color, flash, 1f);
        var forkAt = 3 + (int)(SkyLayers.Hash(strike, 5.1f) * 4f);
        Span<Vector2> branch = stackalloc Vector2[BranchSegments + 1];
        branch[0] = points[forkAt];
        var direction = SkyLayers.Hash(strike, 8.8f) < 0.5f ? -1f : 1f;
        for (var pointIndex = 1; pointIndex <= BranchSegments; pointIndex++)
        {
            var jitter = (SkyLayers.Hash(strike * 17 + pointIndex, 3.3f) - 0.5f) * 0.03f;
            branch[pointIndex] = branch[pointIndex - 1] +
                                 new Vector2((0.028f * direction + jitter) * canvas.Width, 0.022f * canvas.Height);
        }

        DrawJagged(canvas, branch, color, flash, 0.55f);
    }

    public static void Splashes(in SkyCanvas canvas, Vector4 rain, int count)
    {
        var drawList = canvas.DrawList;
        for (var splashIndex = 0; splashIndex < count; splashIndex++)
        {
            var life = SkyLayers.Phase(0.9 + SkyLayers.Hash(splashIndex, 1.9f) * 0.5,
                SkyLayers.Hash(splashIndex, 4.4f));
            var lane = SkyLayers.Frac(splashIndex * GoldenStep + (float)Math.Floor(SkyLayers.Seconds / 1.3) * 0.37f);
            var center = canvas.At(0.04f + lane * 0.92f, 0.955f - SkyLayers.Hash(splashIndex, 6.2f) * 0.03f);
            var radius = (2f + life * 6f) * canvas.Scale;
            var alpha = (1f - life) * 0.35f * canvas.Opacity;
            drawList.PathArcTo(center, radius, MathF.PI * 1.08f, MathF.PI * 1.92f, 12);
            drawList.PathStroke(SkyLayers.Color(rain, alpha), ImDrawFlags.None, 1f * canvas.Scale);
        }
    }

    public static void Snowfall(in SkyCanvas canvas, Vector4 flake)
    {
        var drawList = canvas.DrawList;
        var scale = canvas.Scale;
        for (var flakeIndex = 0; flakeIndex < FarFlakeCount; flakeIndex++)
        {
            var fall = SkyLayers.Phase(9.0 + SkyLayers.Hash(flakeIndex, 2.1f) * 4.0, SkyLayers.Hash(flakeIndex, 7.3f));
            var laneX = SkyLayers.Frac(flakeIndex * GoldenStep + 0.03f);
            var sway = MathF.Sin((fall * 3f + flakeIndex * 0.7f) * MathF.PI) * 0.025f;
            var position = canvas.At(laneX + sway, fall * 1.08f - 0.04f);
            var alpha = (0.32f + SkyLayers.Hash(flakeIndex, 5.5f) * 0.30f) * canvas.Opacity;
            drawList.AddCircleFilled(position, (1.1f + SkyLayers.Hash(flakeIndex, 3.1f) * 0.9f) * scale,
                SkyLayers.Color(flake, alpha), 10);
        }

        for (var flakeIndex = 0; flakeIndex < NearFlakeCount; flakeIndex++)
        {
            var fall = SkyLayers.Phase(5.2 + SkyLayers.Hash(flakeIndex, 8.1f) * 2.5,
                SkyLayers.Hash(flakeIndex, 1.7f));
            var laneX = SkyLayers.Frac(flakeIndex * GoldenStep + 0.41f);
            var sway = MathF.Sin((fall * 2f + flakeIndex * 0.9f) * MathF.PI) * 0.045f;
            var position = canvas.At(laneX + sway, fall * 1.16f - 0.08f);
            var radius = (8f + SkyLayers.Hash(flakeIndex, 9.4f) * 7f) * scale;
            SkyLayers.Glow(canvas, position, new Vector2(radius), flake, 0.75f * canvas.Opacity);
            drawList.AddCircleFilled(position, radius * 0.16f, SkyLayers.Color(flake, 0.55f * canvas.Opacity), 12);
        }
    }

    public static void Gusts(in SkyCanvas canvas, Vector4 stroke)
    {
        const int segmentCount = 12;
        var drawList = canvas.DrawList;
        for (var gustIndex = 0; gustIndex < GustCount; gustIndex++)
        {
            var progress = SkyLayers.Phase(3.6 + gustIndex * 0.9, gustIndex * 0.31f);
            var laneY = 0.22f + gustIndex * 0.17f;
            var head = progress * 1.7f - 0.35f;
            var tail = 0.34f;
            var envelope = MathF.Sin(progress * MathF.PI) * 0.30f * canvas.Opacity;
            var previous = GustPoint(canvas, head - tail, laneY, gustIndex);
            for (var segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
            {
                var along = segmentIndex / (float)segmentCount;
                var current = GustPoint(canvas, head - tail + tail * along, laneY, gustIndex);
                drawList.AddLine(previous, current, SkyLayers.Color(stroke, envelope * along * along),
                    (0.8f + along) * canvas.Scale);
                previous = current;
            }
        }
    }

    public static void Dust(in SkyCanvas canvas, Vector4 tint)
    {
        var drawList = canvas.DrawList;
        for (var moteIndex = 0; moteIndex < DustCount; moteIndex++)
        {
            var progress = SkyLayers.Phase(1.6 + SkyLayers.Hash(moteIndex, 2.6f) * 1.4, moteIndex * 0.37f);
            var laneY = 0.10f + SkyLayers.Frac(moteIndex * GoldenStep + 0.24f) * 0.82f;
            var position = canvas.At(progress * 1.3f - 0.15f,
                laneY + MathF.Sin(progress * MathF.PI * 4f + moteIndex) * 0.02f);
            var alpha = MathF.Sin(progress * MathF.PI) * 0.55f * canvas.Opacity;
            var radius = (1.6f + moteIndex % 3 * 1.1f) * canvas.Scale;
            SkyLayers.Glow(canvas, position, new Vector2(radius * 2.2f, radius * 1.4f), tint, alpha);
        }
    }

    public static void Embers(in SkyCanvas canvas, Vector4 ember)
    {
        var drawList = canvas.DrawList;
        for (var emberIndex = 0; emberIndex < EmberCount; emberIndex++)
        {
            var rise = SkyLayers.Phase(4.2 + emberIndex * 0.55, emberIndex * 0.29f);
            var position = canvas.At(
                0.08f + SkyLayers.Frac(emberIndex * GoldenStep + 0.19f) * 0.84f +
                MathF.Sin(rise * MathF.PI * 3f + emberIndex) * 0.03f, 1.06f - 1.2f * rise);
            var alpha = MathF.Sin(rise * MathF.PI) * 0.75f * canvas.Opacity;
            SkyLayers.Glow(canvas, position, new Vector2(9f * canvas.Scale), ember, alpha);
            drawList.AddCircleFilled(position, 1.4f * canvas.Scale, SkyLayers.Color(ember, alpha), 8);
        }
    }

    public static void Motes(in SkyCanvas canvas, Vector4 tint)
    {
        var drawList = canvas.DrawList;
        for (var moteIndex = 0; moteIndex < MoteCount; moteIndex++)
        {
            var angle = SkyLayers.Phase(9.0 + moteIndex * 0.9) * MathF.PI * 2f;
            var anchor = canvas.At(SkyLayers.Hash(moteIndex, 23.7f), 0.12f + SkyLayers.Hash(moteIndex, 61.9f) * 0.76f);
            var position = anchor + new Vector2(MathF.Cos(angle) * canvas.Width * 0.04f,
                MathF.Sin(angle) * canvas.Height * 0.03f);
            var alpha = (0.30f + 0.40f * SkyLayers.Wave(2.8 + moteIndex * 0.34)) * canvas.Opacity;
            SkyLayers.Glow(canvas, position, new Vector2((9f + moteIndex % 3 * 4f) * canvas.Scale), tint, alpha);
        }
    }

    private static void DrawJagged(in SkyCanvas canvas, ReadOnlySpan<Vector2> points, Vector4 color, float flash,
        float weight)
    {
        var drawList = canvas.DrawList;
        var glow = SkyLayers.Color(color, 0.22f * flash * weight * canvas.Opacity);
        var core = SkyLayers.Color(color, 0.95f * flash * weight * canvas.Opacity);
        for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
        {
            drawList.AddLine(points[pointIndex - 1], points[pointIndex], glow, 7f * weight * canvas.Scale);
        }

        for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
        {
            drawList.AddLine(points[pointIndex - 1], points[pointIndex], core, 1.6f * weight * canvas.Scale);
        }
    }

    private static Vector2 GustPoint(in SkyCanvas canvas, float fractionX, float laneY, int gustIndex) =>
        canvas.At(fractionX, laneY + MathF.Sin(fractionX * 7f + gustIndex * 1.9f) * 0.018f);

    private static float Spike(float cycle, float center, float width)
    {
        var distance = MathF.Abs(cycle - center);
        return distance >= width ? 0f : 1f - distance / width;
    }
}
