using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static partial class Typography
{
    private static readonly Vector4 WitchfireCore = new(0.84f, 0.97f, 0.85f, 1f);
    private static readonly Vector4 WitchfireDayFlare = new(0.1f, 0.45f, 0.18f, 1f);
    private static readonly Vector4 WitchfireDayGlow = new(0.36f, 0.91f, 0.45f, 1f);

    private const int WitchfireMaxGlyphs = 48;
    private const float WitchfireInkMinimum = 0.15f;
    private const int WitchfireEmberGlowCells = 2;
    private const uint WitchfireOpaque = 0xFFFFFFFFu;
    private const float WitchfireFilmRate = 18f;
    private const float WitchfirePhaseSteps = 5.6f * WitchfireFilmRate;
    private const float WitchfireFlickerRate = 7f;
    private const float WitchfireFlutterRate = 17f;
    private const float WitchfireFlutterWeight = 0.4f;
    private const float WitchfireGlyphSpread = 13.1f;
    private const float WitchfireFlutterSpread = 5.3f;
    private const float WitchfireHeatFloor = 0.3f;
    private const float WitchfireFlareFrom = 0.72f;
    private const float WitchfireFlareWeight = 0.45f;
    private const float WitchfireDayFlareWeight = 0.45f;
    private const float WitchfireDayCrest = 0.33f;
    private const float WitchfireCoolest = 0.45f;
    private const float WitchfireCrownCool = 0.2f;
    private const float WitchfireLetterCore = 0.3f;
    private const float WitchfireEdgeDistance = 0.035f;
    private const float WitchfireEdgeShade = 0.22f;
    private const float WitchfireEdgeAlpha = 0.28f;
    private const float WitchfireGustFraction = 0.22f;
    private const float WitchfireGustWidth = 0.18f;
    private const float WitchfireGustDip = 0.7f;
    private const float WitchfireGlowRadius = 0.85f;
    private const float WitchfireGlowAlpha = 0.32f;
    private const float WitchfireCoreGlowRadius = 0.45f;
    private const float WitchfireCoreGlowAlpha = 0.3f;
    private const float WitchfireDayGlowAlpha = 0.14f;
    private const int WitchfireGlowCells = 4;
    private const float WitchfireFlameHeight = 0.36f;
    private const float WitchfireFlameFloor = 0.35f;
    private const float WitchfireFlameWidth = 0.5f;
    private const float WitchfireFlameMinWidth = 0.22f;
    private const float WitchfireFlameAlpha = 0.5f;
    private const float WitchfireDayFlameAlpha = 0.28f;
    private const float WitchfireFlameSway = 0.22f;
    private const float WitchfireFlameSwayRate = 1.3f;
    private const float WitchfireFlameSwaySpread = 3.7f;
    private const float WitchfireFlameLift = 0.02f;
    private const float WitchfireGustLean = 0.5f;
    private const float WitchfireGustFlameDip = 0.45f;
    private const int WitchfireFlameSegments = 7;
    private const int WitchfireEmberSlots = 7;
    private const float WitchfireEmberLifeFloor = 1.4f;
    private const float WitchfireEmberLifeSpread = 1.2f;
    private const float WitchfireEmberRise = 1.6f;
    private const float WitchfireEmberHead = 0.042f;
    private const float WitchfireEmberGlow = 0.18f;
    private const float WitchfireEmberTrail = 0.07f;
    private const float WitchfireEmberChance = 0.7f;
    private const float WitchfireEmberSpread = 0.5f;
    private const float WitchfireEmberCurl = 0.12f;
    private const float WitchfireEmberCurlRate = 4.2f;
    private const float WitchfireEmberGustPush = 0.4f;
    private const int WitchfireSmokeSlots = 2;
    private const float WitchfireSmokeLife = 3.4f;
    private const float WitchfireSmokeChance = 0.6f;
    private const float WitchfireSmokeAlpha = 0.08f;
    private const float WitchfireDaySmokeAlpha = 0.06f;

    private readonly record struct WitchfireGlyph(float Center, float Top, float Bottom, float Width, float Heat,
        float Gust);

    private readonly record struct WitchfireStop(float At, Vector4 Color);

    private readonly struct WitchfirePalette
    {
        public readonly bool Daylight;
        public readonly Vector4 Hot;
        public readonly Vector4 Mid;
        public readonly Vector4 Shade;
        public readonly Vector4 Core;
        public readonly Vector4 Ember;
        public readonly Vector4 Glow;
        public readonly Vector4 Smoke;
        public readonly Vector4 Flare;
        public readonly float FlareWeight;

        public WitchfirePalette(in EffectFrame frame, bool daylight)
        {
            var ramp = frame.Effect.Ramp;
            var first = ramp.Count > 0 ? ramp.Stop(0) : frame.Crest;
            var second = ramp.Count > 1 ? ramp.Stop(1) : frame.Crest;
            var third = ramp.Count > 2 ? ramp.Stop(2) : frame.Color;
            Daylight = daylight;
            Shade = ramp.Count > 0 ? ramp.Stop(ramp.Count - 1) : frame.Color;
            Mid = second;
            Ember = first;
            Core = daylight ? WitchfireDayFlare : WitchfireCore;
            Hot = daylight ? WitchfireDayFlare : Vector4.Lerp(first, WitchfireCore, 0.4f);
            Glow = daylight ? WitchfireDayGlow : Vector4.Lerp(first, WitchfireDayGlow, 0.5f);
            Smoke = daylight ? third : Vector4.Lerp(third, White, 0.15f);
            Flare = daylight ? WitchfireDayFlare : WitchfireCore;
            FlareWeight = daylight ? WitchfireDayFlareWeight : WitchfireFlareWeight;
        }
    }

    private static void DrawWitchfire(in EffectFrame frame)
    {
        if (frame.Width <= 0f || frame.Height <= 0f)
        {
            frame.Fill();
            return;
        }

        var seconds = Math.Floor(frame.Seconds * WitchfireFilmRate) / WitchfireFilmRate;
        var phase = MathF.Floor(frame.Phase * WitchfirePhaseSteps) / WitchfirePhaseSteps;
        var gust = WitchfireGust(phase, out var front);
        var palette = new WitchfirePalette(frame, Palette.Luminance(frame.Crest) < WitchfireDayCrest);
        if (!palette.Daylight)
        {
            var edge = new Vector4(frame.Crest.X * WitchfireEdgeShade, frame.Crest.Y * WitchfireEdgeShade,
                frame.Crest.Z * WitchfireEdgeShade, frame.Crest.W * WitchfireEdgeAlpha);
            DrawStroke(frame, MathF.Max(1f, frame.FontSize * WitchfireEdgeDistance), edge);
        }

        Span<WitchfireGlyph> glyphs = stackalloc WitchfireGlyph[WitchfireMaxGlyphs];
        var count = DrawWitchfireLetters(frame, palette, seconds, gust, front, glyphs);
        if (count == 0)
        {
            return;
        }

        ReadOnlySpan<WitchfireGlyph> lit = glyphs[..count];
        DrawWitchfireGlow(frame, palette, lit);
        DrawWitchfireSmoke(frame, palette, lit, seconds, gust);
        DrawWitchfireFlames(frame, palette, lit, seconds);
        DrawWitchfireEmbers(frame, palette, lit, seconds, gust);
    }

    private static float WitchfireGust(float phase, out float front)
    {
        front = 0f;
        if (phase >= WitchfireGustFraction)
        {
            return 0f;
        }

        var progress = phase / WitchfireGustFraction;
        front = -WitchfireGustWidth + (1f + WitchfireGustWidth * 2f) * progress;
        return MathF.Sin(progress * MathF.PI);
    }

    private static int DrawWitchfireLetters(in EffectFrame frame, in WitchfirePalette palette, double seconds,
        float gust, float front, Span<WitchfireGlyph> glyphs)
    {
        var firstVertex = frame.DrawList.VtxBuffer.Size;
        frame.Fill();
        var quads = frame.QuadsSince(firstVertex);
        var quadCount = quads.Length / 4;
        Span<float> quadHeat = stackalloc float[WitchfireMaxGlyphs];
        var count = 0;
        for (var quad = 0; quad < quadCount && quad < WitchfireMaxGlyphs; quad++)
        {
            var first = quad * 4;
            var min = quads[first].Pos;
            var max = min;
            for (var corner = 1; corner < 4; corner++)
            {
                min = Vector2.Min(min, quads[first + corner].Pos);
                max = Vector2.Max(max, quads[first + corner].Pos);
            }

            var center = (min.X + max.X) * 0.5f;
            var heat = WitchfireHeatFloor + (1f - WitchfireHeatFloor) * FlickerNoise(seconds, quad);
            var local = 0f;
            if (gust > 0f)
            {
                var distance = ((center - frame.Position.X) / frame.Width - front) / WitchfireGustWidth;
                local = MathF.Exp(-distance * distance);
                heat *= 1f - WitchfireGustDip * local;
            }

            quadHeat[quad] = heat;
            var inked = max.Y - min.Y >= frame.FontSize * WitchfireInkMinimum
                        && max.X - min.X >= frame.FontSize * WitchfireInkMinimum;
            if (inked && count < glyphs.Length)
            {
                glyphs[count] = new WitchfireGlyph(center, min.Y, max.Y, max.X - min.X, heat, local * gust);
                count++;
            }
        }

        var ramp = frame.Effect.Ramp;
        for (var vertexIndex = 0; vertexIndex < quads.Length; vertexIndex++)
        {
            ref var vertex = ref quads[vertexIndex];
            var quad = vertexIndex >> 2;
            var heat = quad < WitchfireMaxGlyphs ? quadHeat[quad] : WitchfireHeatFloor;
            var down = Math.Clamp((vertex.Pos.Y - frame.Position.Y) / frame.Height, 0f, 1f);
            var cool = Math.Clamp((1f - heat) * WitchfireCoolest + (1f - down) * WitchfireCrownCool, 0f, 1f);
            var color = ramp.Count > 1 ? ramp.SampleAcross(cool) : Vector4.Lerp(frame.Crest, frame.Color, cool);
            if (heat > WitchfireFlareFrom)
            {
                var flare = (heat - WitchfireFlareFrom) / (1f - WitchfireFlareFrom) * palette.FlareWeight;
                color = Vector4.Lerp(color, palette.Flare, flare);
            }

            if (!palette.Daylight)
            {
                color = Vector4.Lerp(color, WitchfireCore, WitchfireLetterCore * heat * down);
            }

            var coverage = (vertex.Col >> 24) / 255f;
            vertex.Col = ImGui.GetColorU32(color with { W = color.W * coverage });
        }

        return count;
    }

    private static void DrawWitchfireGlow(in EffectFrame frame, in WitchfirePalette palette,
        ReadOnlySpan<WitchfireGlyph> glyphs)
    {
        var alpha = palette.Daylight ? WitchfireDayGlowAlpha : WitchfireGlowAlpha;
        for (var glyph = 0; glyph < glyphs.Length; glyph++)
        {
            var lit = glyphs[glyph];
            var center = new Vector2(lit.Center, (lit.Top + lit.Bottom) * 0.5f);
            var radius = frame.FontSize * WitchfireGlowRadius * (0.85f + 0.3f * lit.Heat);
            NightScene.Glow(frame.DrawList, center, radius, Faded(palette.Glow, alpha * lit.Heat), WitchfireGlowCells);
            if (!palette.Daylight)
            {
                NightScene.Glow(frame.DrawList, center, frame.FontSize * WitchfireCoreGlowRadius,
                    Faded(Vector4.Lerp(palette.Glow, WitchfireCore, 0.5f), WitchfireCoreGlowAlpha * lit.Heat),
                    WitchfireGlowCells);
            }
        }
    }

    private static void DrawWitchfireSmoke(in EffectFrame frame, in WitchfirePalette palette,
        ReadOnlySpan<WitchfireGlyph> glyphs, double seconds, float gust)
    {
        var strength = palette.Daylight ? WitchfireDaySmokeAlpha : WitchfireSmokeAlpha;
        for (var slot = 0; slot < WitchfireSmokeSlots; slot++)
        {
            var travel = seconds / WitchfireSmokeLife + slot * 0.5;
            var cycle = (int)Math.Floor(travel);
            if (Hash01(slot, cycle, 43) >= WitchfireSmokeChance)
            {
                continue;
            }

            var local = (float)(travel - cycle);
            var lit = glyphs[Math.Min((int)(Hash01(slot, cycle, 41) * glyphs.Length), glyphs.Length - 1)];
            var center = new Vector2(
                lit.Center + MathF.Sin(local * 3f + slot) * frame.FontSize * 0.15f + gust * frame.FontSize * 0.3f * local,
                lit.Top - local * frame.FontSize * 0.9f);
            var radius = frame.FontSize * (0.25f + 0.6f * local);
            NightScene.Glow(frame.DrawList, center, radius, Faded(palette.Smoke, strength * MathF.Sin(MathF.PI * local)),
                WitchfireGlowCells);
        }
    }

    private static void DrawWitchfireFlames(in EffectFrame frame, in WitchfirePalette palette,
        ReadOnlySpan<WitchfireGlyph> glyphs, double seconds)
    {
        var flameAlpha = palette.Daylight ? WitchfireDayFlameAlpha : WitchfireFlameAlpha;
        Span<WitchfireStop> stops = stackalloc WitchfireStop[5];
        for (var glyph = 0; glyph < glyphs.Length; glyph++)
        {
            var lit = glyphs[glyph];
            var flicker = FlickerNoise(seconds * 1.6, glyph + 57);
            var height = frame.FontSize * WitchfireFlameHeight
                         * (WitchfireFlameFloor + (1f - WitchfireFlameFloor) * (0.5f * lit.Heat + 0.5f * flicker))
                         * (1f - WitchfireGustFlameDip * lit.Gust);
            var sway = (ValueNoise((float)(seconds * WitchfireFlameSwayRate) + glyph * WitchfireFlameSwaySpread,
                glyph + 90) - 0.5f) * 2f * WitchfireFlameSway;
            var lean = sway + WitchfireGustLean * lit.Gust;
            var width = MathF.Max(frame.FontSize * WitchfireFlameMinWidth, lit.Width * WitchfireFlameWidth);
            var alpha = flameAlpha * (0.6f + 0.4f * lit.Heat);
            var baseline = new Vector2(lit.Center, lit.Top + frame.FontSize * WitchfireFlameLift);

            stops[0] = new WitchfireStop(0f, Faded(palette.Hot, 0f));
            stops[1] = new WitchfireStop(0.2f, Faded(palette.Hot, alpha * 0.22f));
            stops[2] = new WitchfireStop(0.65f, Faded(palette.Mid, alpha * 0.12f));
            stops[3] = new WitchfireStop(1f, Faded(palette.Shade, 0f));
            FillFlame(frame.DrawList, baseline + new Vector2(0f, width * 0.2f), width * 1.8f, height * 1.25f,
                lean * 1.1f, stops[..4]);

            stops[0] = new WitchfireStop(0f, Faded(palette.Hot, 0f));
            stops[1] = new WitchfireStop(0.18f, Faded(palette.Hot, alpha));
            stops[2] = new WitchfireStop(0.45f, Faded(palette.Mid, alpha * 0.7f));
            stops[3] = new WitchfireStop(0.8f, Faded(palette.Shade, alpha * 0.25f));
            stops[4] = new WitchfireStop(1f, Faded(palette.Shade, 0f));
            FillFlame(frame.DrawList, baseline, width, height, lean, stops);

            stops[0] = new WitchfireStop(0f, Faded(palette.Core, alpha * 0.85f));
            stops[1] = new WitchfireStop(1f, Faded(palette.Hot, 0f));
            FillFlame(frame.DrawList, baseline, width * 0.45f, height * 0.55f, lean * 0.8f, stops[..2]);
        }
    }

    private static void FillFlame(ImDrawListPtr drawList, Vector2 baseline, float width, float height, float lean,
        ReadOnlySpan<WitchfireStop> stops)
    {
        if (height <= 0.5f || width <= 0.5f)
        {
            return;
        }

        var tip = new Vector2(baseline.X + lean * height, baseline.Y - height);
        var left = new Vector2(baseline.X - width * 0.5f, baseline.Y);
        var right = new Vector2(baseline.X + width * 0.5f, baseline.Y);
        var firstVertex = drawList.VtxBuffer.Size;
        drawList.PathClear();
        TraceCubic(drawList, left, new Vector2(baseline.X - width * 0.55f, baseline.Y - height * 0.45f),
            new Vector2(tip.X - width * 0.15f, tip.Y + height * 0.35f), tip, true);
        TraceCubic(drawList, tip, new Vector2(tip.X + width * 0.15f, tip.Y + height * 0.35f),
            new Vector2(baseline.X + width * 0.55f, baseline.Y - height * 0.45f), right, false);
        var bottom = new Vector2(baseline.X, baseline.Y + width * 0.25f);
        for (var step = 1; step < WitchfireFlameSegments; step++)
        {
            var amount = step / (float)WitchfireFlameSegments;
            var inverse = 1f - amount;
            drawList.PathLineTo(inverse * inverse * right + 2f * inverse * amount * bottom + amount * amount * left);
        }

        drawList.PathFillConvex(WitchfireOpaque);
        ShadeFlame(drawList, firstVertex, new Vector2(baseline.X, baseline.Y + width * 0.25f), tip, stops);
    }

    private static void TraceCubic(ImDrawListPtr drawList, Vector2 start, Vector2 firstControl, Vector2 secondControl,
        Vector2 end, bool includeStart)
    {
        for (var step = includeStart ? 0 : 1; step <= WitchfireFlameSegments; step++)
        {
            var amount = step / (float)WitchfireFlameSegments;
            var inverse = 1f - amount;
            drawList.PathLineTo(inverse * inverse * inverse * start
                                + 3f * inverse * inverse * amount * firstControl
                                + 3f * inverse * amount * amount * secondControl
                                + amount * amount * amount * end);
        }
    }

    private static void ShadeFlame(ImDrawListPtr drawList, int firstVertex, Vector2 start, Vector2 end,
        ReadOnlySpan<WitchfireStop> stops)
    {
        var axis = end - start;
        var length = MathF.Max(axis.LengthSquared(), 0.0001f);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var index = firstVertex; index < vertices.Length; index++)
        {
            ref var vertex = ref vertices[index];
            var along = Math.Clamp(Vector2.Dot(vertex.Pos - start, axis) / length, 0f, 1f);
            var color = stops[^1].Color;
            for (var stop = 1; stop < stops.Length; stop++)
            {
                if (along > stops[stop].At)
                {
                    continue;
                }

                var from = stops[stop - 1];
                var span = MathF.Max(stops[stop].At - from.At, 0.0001f);
                color = Vector4.Lerp(from.Color, stops[stop].Color, (along - from.At) / span);
                break;
            }

            var coverage = (vertex.Col >> 24) / 255f;
            vertex.Col = ImGui.GetColorU32(color with { W = color.W * coverage });
        }
    }

    private static void DrawWitchfireEmbers(in EffectFrame frame, in WitchfirePalette palette,
        ReadOnlySpan<WitchfireGlyph> glyphs, double seconds, float gust)
    {
        var size = frame.FontSize;
        for (var slot = 0; slot < WitchfireEmberSlots; slot++)
        {
            var life = WitchfireEmberLifeFloor + WitchfireEmberLifeSpread * Hash01(slot, 0, 11);
            var travel = seconds / life + slot * 0.37;
            var cycle = (int)Math.Floor(travel);
            if (Hash01(slot, cycle, 7) >= WitchfireEmberChance)
            {
                continue;
            }

            var local = (float)(travel - cycle);
            var lit = glyphs[Math.Min((int)(Hash01(slot, cycle, 3) * glyphs.Length), glyphs.Length - 1)];
            var origin = lit.Center + (Hash01(slot, cycle, 9) - 0.5f) * lit.Width * 0.6f;
            var drift = (Hash01(slot, cycle, 5) - 0.5f) * size * WitchfireEmberSpread;
            var head = EmberAt(origin, lit.Top, local, slot, drift, gust, size);
            var tail = EmberAt(origin, lit.Top, MathF.Max(0f, local - WitchfireEmberTrail), slot, drift, gust, size);
            Vector4 color;
            if (palette.Daylight)
            {
                color = Vector4.Lerp(WitchfireDayFlare, palette.Shade, local);
            }
            else if (local < 0.4f)
            {
                color = Vector4.Lerp(WitchfireCore, palette.Ember, local / 0.4f);
            }
            else
            {
                color = Vector4.Lerp(palette.Ember, palette.Shade, (local - 0.4f) / 0.6f);
            }

            var alpha = MathF.Min(1f, local * 6f) * MathF.Pow(1f - local, 1.2f)
                        * (0.75f + 0.25f * MathF.Sin((float)(seconds * 23.0) + slot * 7f));
            NightScene.Glow(frame.DrawList, head, size * WitchfireEmberGlow * 1.6f,
                Faded(color, alpha * (palette.Daylight ? 0.2f : 0.35f)), WitchfireEmberGlowCells);
            frame.DrawList.AddLine(tail, head, ImGui.GetColorU32(Faded(color, alpha * 0.6f)),
                MathF.Max(1f, size * WitchfireEmberHead * 1.6f));
            var headColor = palette.Daylight ? color : Vector4.Lerp(color, White, 0.3f);
            frame.DrawList.AddCircleFilled(head, size * WitchfireEmberHead, ImGui.GetColorU32(Faded(headColor, alpha)));
        }
    }

    private static Vector2 EmberAt(float origin, float top, float amount, int slot, float drift, float gust,
        float size)
    {
        return new Vector2(
            origin + MathF.Sin(amount * WitchfireEmberCurlRate + slot) * size * WitchfireEmberCurl * amount
                   + drift * amount + gust * size * WitchfireEmberGustPush * amount,
            top - amount * size * WitchfireEmberRise);
    }

    private static float FlickerNoise(double seconds, int glyph)
    {
        var slow = ValueNoise((float)(seconds * WitchfireFlickerRate) + glyph * WitchfireGlyphSpread, glyph);
        var fast = ValueNoise((float)(seconds * WitchfireFlutterRate) + glyph * WitchfireFlutterSpread, glyph + 31);
        return slow * (1f - WitchfireFlutterWeight) + fast * WitchfireFlutterWeight;
    }

    private static float ValueNoise(float position, int salt)
    {
        var cell = MathF.Floor(position);
        var fraction = position - cell;
        var smooth = fraction * fraction * (3f - 2f * fraction);
        var index = (int)cell;
        var from = Hash01(index, salt, 21);
        var to = Hash01(index + 1, salt, 21);
        return from + (to - from) * smooth;
    }
}
