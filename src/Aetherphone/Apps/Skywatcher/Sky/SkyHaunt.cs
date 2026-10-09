using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal static class SkyHaunt
{
    private const float GoldenStep = 0.618034f;
    private const double FlightPeriodSeconds = 17.0;
    private const float FlightWindow = 0.34f;
    private const int FlightSize = 3;
    private const int StormBatCount = 4;
    private const int WispCount = 5;
    private const int LeafCount = 9;
    private const int EyePairCount = 3;
    private const double GhostPeriodSeconds = 21.0;
    private const float GhostWindow = 0.6f;
    private const int GhostArcSegments = 12;
    private const int GhostHemSegments = 18;
    private const int GhostPointCount = GhostArcSegments + GhostHemSegments + 2;

    private static readonly Vector4 BatShadow = new(0.03f, 0.02f, 0.06f, 1f);
    private static readonly Vector4 WispGreen = new(0.55f, 1.00f, 0.78f, 1f);
    private static readonly Vector4 EyeAmber = new(1.00f, 0.78f, 0.30f, 1f);
    private static readonly Vector4 GhostWhite = new(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Vector4 LeafOrange = new(0.96f, 0.52f, 0.16f, 1f);
    private static readonly Vector4 LeafRed = new(0.78f, 0.22f, 0.12f, 1f);
    private static readonly Vector4 LeafBrown = new(0.58f, 0.36f, 0.16f, 1f);

    public static void Draw(in SkyCanvas canvas, WeatherKind kind, float daylight, in AmbienceInk ink, bool natural)
    {
        var night = 1f - daylight;
        var mono = ink.Star;
        switch (kind)
        {
            case WeatherKind.Clear:
                Bats(canvas, natural ? BatShadow : mono, 0.04f, 0.12f);
                break;
            case WeatherKind.Clouds:
                Bats(canvas, natural ? BatShadow : mono, 0.12f, 0.30f);
                break;
            case WeatherKind.Fog:
                Ghost(canvas, natural ? GhostWhite : mono, natural ? BatShadow : mono);
                Wisps(canvas, natural ? WispGreen : mono, 0.8f);
                Eyes(canvas, natural ? EyeAmber : mono, night);
                break;
            case WeatherKind.Rain:
                Eyes(canvas, natural ? EyeAmber : mono, night);
                break;
            case WeatherKind.Thunder:
                StormBats(canvas, natural ? BatShadow : mono);
                Eyes(canvas, natural ? EyeAmber : mono, night);
                break;
            case WeatherKind.Wind:
                Leaves(canvas, natural, mono);
                Bats(canvas, natural ? BatShadow : mono, 0.10f, 0.25f);
                break;
            case WeatherKind.Sand:
                Leaves(canvas, natural, mono);
                break;
            case WeatherKind.Heat:
                Eyes(canvas, natural ? EyeAmber : mono, night);
                break;
            case WeatherKind.Snow:
                Ghost(canvas, natural ? GhostWhite : mono, natural ? BatShadow : mono);
                break;
            default:
                Wisps(canvas, natural ? WispGreen : mono, 1f);
                Eyes(canvas, natural ? EyeAmber : mono, 1f);
                break;
        }
    }

    private static float BatSize(in SkyCanvas canvas) => MathF.Min(1.15f * canvas.Scale, canvas.Width * 0.0034f);

    private static void Bats(in SkyCanvas canvas, Vector4 color, float laneTop, float laneSpan)
    {
        var cycle = SkyLayers.Phase(FlightPeriodSeconds);
        if (cycle > FlightWindow)
        {
            return;
        }

        var travel = cycle / FlightWindow;
        var flight = (int)(SkyLayers.Seconds / FlightPeriodSeconds);
        var leftward = SkyLayers.Hash(flight, 2.3f) < 0.5f;
        var laneY = laneTop + SkyLayers.Hash(flight, 5.9f) * laneSpan;
        var size = BatSize(canvas);
        var alpha = 0.85f * canvas.Opacity;
        for (var batIndex = 0; batIndex < FlightSize; batIndex++)
        {
            var along = travel * 1.4f - 0.2f - batIndex * 0.08f;
            var fractionX = leftward ? 1f - along : along;
            var offsetY = (batIndex % 2 == 0 ? 1f : -1f) * batIndex * 0.035f;
            var fractionY = laneY + offsetY + MathF.Sin((along * 3f + batIndex) * MathF.PI) * 0.025f;
            var flap = MathF.Sin((float)(SkyLayers.Seconds * (11.0 + batIndex * 1.7)));
            NightScene.DrawBat(canvas.DrawList, canvas.At(fractionX, fractionY), size * (1f - batIndex * 0.18f), flap,
                SkyLayers.Color(color, alpha));
        }
    }

    private static void StormBats(in SkyCanvas canvas, Vector4 color)
    {
        var flash = SkyParticles.Flash();
        if (flash < 0.05f)
        {
            return;
        }

        var strike = (int)(SkyLayers.Seconds / SkyParticles.LightningPeriodSeconds);
        var size = BatSize(canvas);
        var ink = SkyLayers.Color(color, 0.9f * flash * canvas.Opacity);
        for (var batIndex = 0; batIndex < StormBatCount; batIndex++)
        {
            var position = canvas.At(0.12f + SkyLayers.Hash(strike * 13 + batIndex, 4.4f) * 0.76f,
                0.08f + SkyLayers.Hash(strike * 13 + batIndex, 9.2f) * 0.34f);
            var flap = SkyLayers.Hash(strike * 13 + batIndex, 1.6f) * 2f - 1f;
            NightScene.DrawBat(canvas.DrawList, position, size * (0.7f + 0.3f * SkyLayers.Hash(batIndex, 3.8f)), flap,
                ink);
        }
    }

    private static void Wisps(in SkyCanvas canvas, Vector4 color, float strength)
    {
        var scale = canvas.Scale;
        for (var wispIndex = 0; wispIndex < WispCount; wispIndex++)
        {
            var drift = SkyLayers.Phase(14.0 + SkyLayers.Hash(wispIndex, 1.3f) * 8.0, SkyLayers.Hash(wispIndex, 4.7f));
            var turn = drift * MathF.PI * 2f;
            var fractionX = 0.06f + SkyLayers.Frac(wispIndex * GoldenStep + 0.13f) * 0.88f + MathF.Sin(turn) * 0.04f;
            var fractionY = 0.60f + SkyLayers.Hash(wispIndex, 8.2f) * 0.28f + MathF.Sin(turn * 2f + wispIndex) * 0.025f;
            var flicker = 0.55f + 0.45f * SkyLayers.Wave(1.3 + SkyLayers.Hash(wispIndex, 6.1f) * 1.4,
                SkyLayers.Hash(wispIndex, 2.2f));
            var alpha = flicker * strength * canvas.Opacity;
            var position = canvas.At(fractionX, fractionY);
            SkyLayers.Glow(canvas, position, new Vector2(18f * scale), color, 0.60f * alpha);
            SkyLayers.Glow(canvas, position, new Vector2(4.5f * scale), color, alpha);
        }
    }

    private static void Eyes(in SkyCanvas canvas, Vector4 color, float visibility)
    {
        if (visibility <= 0.01f)
        {
            return;
        }

        var scale = canvas.Scale;
        for (var pairIndex = 0; pairIndex < EyePairCount; pairIndex++)
        {
            var period = 11.0 + pairIndex * 3.1;
            var offset = SkyLayers.Hash(pairIndex, 3.3f);
            var cycle = SkyLayers.Phase(period, offset);
            var open = MathF.Min(Ramp(cycle, 0.15f, 0.25f), 1f - Ramp(cycle, 0.60f, 0.70f));
            if (open <= 0f)
            {
                continue;
            }

            var spot = (int)(SkyLayers.Seconds / period + offset) * 7 + pairIndex;
            var center = canvas.At(0.10f + SkyLayers.Hash(spot, 1.1f) * 0.80f, 0.68f + SkyLayers.Hash(spot, 2.2f) * 0.22f);
            var blink = MathF.Abs(cycle - 0.42f) < 0.012f ? 0.15f : 1f;
            var alpha = open * visibility * canvas.Opacity;
            var spacing = new Vector2(5.5f * scale, 0f);
            var radii = new Vector2(3.6f, 2.4f * blink) * scale;
            SkyLayers.Glow(canvas, center, new Vector2(16f, 10f) * scale, color, 0.30f * alpha);
            SkyLayers.Glow(canvas, center - spacing, radii, color, 0.95f * alpha);
            SkyLayers.Glow(canvas, center + spacing, radii, color, 0.95f * alpha);
            if (blink < 1f)
            {
                continue;
            }

            var pupil = SkyLayers.Color(color, alpha);
            canvas.DrawList.AddCircleFilled(center - spacing, 1.3f * scale, pupil, 10);
            canvas.DrawList.AddCircleFilled(center + spacing, 1.3f * scale, pupil, 10);
        }
    }

    private static void Ghost(in SkyCanvas canvas, Vector4 color, Vector4 eyes)
    {
        var cycle = SkyLayers.Phase(GhostPeriodSeconds);
        if (cycle > GhostWindow)
        {
            return;
        }

        var travel = cycle / GhostWindow;
        var pass = (int)(SkyLayers.Seconds / GhostPeriodSeconds);
        var leftward = SkyLayers.Hash(pass, 6.4f) < 0.5f;
        var along = travel * 1.3f - 0.15f;
        var fractionY = 0.40f + SkyLayers.Hash(pass, 2.7f) * 0.18f + MathF.Sin(travel * MathF.PI * 3f) * 0.04f;
        var center = canvas.At(leftward ? 1f - along : along, fractionY);
        var size = MathF.Min(14f * canvas.Scale, canvas.Width * 0.045f);
        var alpha = MathF.Sin(travel * MathF.PI) * canvas.Opacity;
        var drawList = canvas.DrawList;
        SkyLayers.Glow(canvas, center + new Vector2(0f, size * 0.4f), new Vector2(size * 2.6f), color, 0.22f * alpha);

        Span<Vector2> outline = stackalloc Vector2[GhostPointCount];
        TraceGhost(outline, center, size, (float)SkyLayers.Seconds * 2.5f);
        var fill = SkyLayers.Color(color, 0.20f * alpha);
        var hub = center + new Vector2(0f, size * 0.4f);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
        {
            drawList.AddTriangleFilled(hub, outline[pointIndex], outline[(pointIndex + 1) % outline.Length], fill);
        }

        drawList.Flags = flags;
        drawList.PathClear();
        for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
        {
            drawList.PathLineTo(outline[pointIndex]);
        }

        drawList.PathStroke(SkyLayers.Color(color, 0.30f * alpha), ImDrawFlags.Closed, canvas.Scale);
        var eyeInk = SkyLayers.Color(eyes, 0.45f * alpha);
        var gaze = leftward ? -0.12f : 0.12f;
        drawList.AddCircleFilled(center + new Vector2((gaze - 0.32f) * size, -0.05f * size), size * 0.14f, eyeInk, 10);
        drawList.AddCircleFilled(center + new Vector2((gaze + 0.32f) * size, -0.05f * size), size * 0.14f, eyeInk, 10);
    }

    private static void TraceGhost(Span<Vector2> outline, Vector2 center, float size, float sway)
    {
        for (var step = 0; step <= GhostArcSegments; step++)
        {
            var angle = MathF.PI + MathF.PI * step / GhostArcSegments;
            outline[step] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
        }

        var hemY = center.Y + size * 1.15f;
        for (var step = 0; step <= GhostHemSegments; step++)
        {
            var across = step / (float)GhostHemSegments;
            var ripple = MathF.Abs(MathF.Sin(across * MathF.PI * 3f + sway)) * size * 0.28f;
            outline[GhostArcSegments + 1 + step] = new Vector2(center.X + size * (1f - 2f * across), hemY + ripple);
        }
    }

    private static void Leaves(in SkyCanvas canvas, bool natural, Vector4 mono)
    {
        var drawList = canvas.DrawList;
        var scale = canvas.Scale;
        var seconds = (float)SkyLayers.Seconds;
        for (var leafIndex = 0; leafIndex < LeafCount; leafIndex++)
        {
            var progress = SkyLayers.Phase(4.5 + SkyLayers.Hash(leafIndex, 2.2f) * 2.5, SkyLayers.Hash(leafIndex, 7.7f));
            var laneY = 0.15f + SkyLayers.Frac(leafIndex * GoldenStep + 0.31f) * 0.72f;
            var center = canvas.At(progress * 1.3f - 0.15f,
                laneY + MathF.Sin(progress * MathF.PI * 3f + leafIndex) * 0.05f + progress * 0.08f);
            var spin = seconds * (2f + SkyLayers.Hash(leafIndex, 3.3f) * 3f) + leafIndex;
            var direction = new Vector2(MathF.Cos(spin), MathF.Sin(spin));
            var normal = new Vector2(-direction.Y, direction.X);
            var length = 5.5f * scale;
            var width = (0.6f + 2.4f * MathF.Abs(MathF.Cos(spin * 1.7f))) * scale;
            var tint = natural ? LeafTint(leafIndex) : mono;
            var ink = SkyLayers.Color(tint, MathF.Sin(progress * MathF.PI) * 0.85f * canvas.Opacity);
            var tip = center + direction * length;
            var back = center - direction * length;
            drawList.AddQuadFilled(tip, center + normal * width, back, center - normal * width, ink);
            drawList.AddLine(back, back - direction * 2.4f * scale, ink, scale);
        }
    }

    private static Vector4 LeafTint(int leafIndex) => (leafIndex % 3) switch
    {
        0 => LeafOrange,
        1 => LeafRed,
        _ => LeafBrown,
    };

    private static float Ramp(float value, float start, float end) => Math.Clamp((value - start) / (end - start), 0f, 1f);
}
