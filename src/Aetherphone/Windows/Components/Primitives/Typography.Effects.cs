using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static partial class Typography
{
    private const float SweepBandFraction = 0.34f;
    private const float GlintBandFraction = 0.16f;
    private const float GlintActiveFraction = 0.30f;
    private const float FrostBandFraction = 0.52f;
    private const float RippleCrests = 2.5f;
    private const float WaveSpan = 1.0f;
    private const float SpectrumSpan = 1.0f;
    private const float EmberSlowCrests = 1.7f;
    private const float EmberFastCrests = 4.3f;
    private const float EmberFastWeight = 0.40f;
    private const float EmberFastRate = 1.6f;
    private const float AuroraCounterCrests = 0.6f;
    private const float AuroraCounterRate = 0.7f;
    private const float PrismSpan = 2.0f;
    private const float GlitchActiveFraction = 0.18f;
    private const int GlitchSteps = 8;
    private const int GlitchBands = 3;
    private const float GlitchShift = 2.4f;
    private const float GlitchAlpha = 0.85f;
    private const int EclipseHaloPoints = 8;
    private const float EclipseHaloRadius = 1.6f;
    private const float EclipseHaloAlpha = 0.20f;
    private const float StarfallRiseFraction = 0.28f;
    private const float StarfallStagger = 0.19f;
    private const float StarfallRadiusScale = 0.065f;
    private const float RimAlpha = 0.26f;
    private const float GlowRimAlpha = 0.55f;
    private const float RimOffset = 1f;
    private const float ThumpWidth = 0.12f;
    private const float ThumpEchoAt = 0.18f;
    private const float ThumpEchoWeight = 0.6f;
    private const float ClipOvershoot = 0.5f;
    private const float SeedSpreadSeconds = 61.7f;

    private const float ChromeDarkBandTop = 0.46f;
    private const float ChromeDarkBandBottom = 0.56f;
    private const float ChromeFootShine = 0.70f;
    private const float ChromeDim = 0.55f;
    private const float ChromeSheenAlpha = 0.70f;
    private const float BlazeFlickerRate = 9f;
    private const float BlazeFlickerFloor = 0.70f;
    private const float BlazeFlickerSpread = 0.60f;
    private const int BlazeEmberSlots = 5;
    private const float BlazeEmberLife = 1.1f;
    private const float BlazeEmberRadius = 0.045f;
    private const int StripeBands = 7;

    private const float BounceAmplitude = 0.10f;
    private const float BounceStagger = 0.08f;
    private const float ShiverActiveFraction = 0.35f;
    private const float ShiverRate = 24f;
    private const float ShiverAmplitudeX = 0.04f;
    private const float ShiverAmplitudeY = 0.03f;
    private const float WobbleRadians = 0.10f;
    private const float WobbleStagger = 0.09f;
    private const float PopStagger = 0.06f;
    private const float PopWindow = 0.25f;
    private const float PopGrowth = 0.28f;
    private const float FlipStagger = 0.05f;
    private const float FlipWindow = 0.30f;
    private const float TypeFraction = 0.25f;
    private const float HoldFraction = 0.60f;
    private const float CaretBlinkRate = 2f;
    private const float CaretWidth = 0.08f;
    private const float CaretTop = 0.14f;
    private const float CaretBottom = 0.86f;

    private const float OutlineDistance = 0.065f;
    private const float OutlineAlpha = 0.90f;
    private const float ShadowOffset = 0.09f;
    private const float ShadowAlpha = 0.55f;
    private const int LongshadowSteps = 5;
    private const float LongshadowStep = 0.045f;
    private const float LongshadowNearAlpha = 0.55f;
    private const float LongshadowFarAlpha = 0.10f;
    private const float EmbossDistance = 0.05f;
    private const float EmbossLightAlpha = 0.85f;
    private const float EmbossDarkAlpha = 0.55f;
    private const float ChromaticShift = 0.065f;
    private const float ChromaticDrift = 0.6f;
    private const float ChromaticAlpha = 0.50f;
    private const int NeonLayers = 3;
    private const float NeonSpread = 0.16f;
    private const float NeonCoreWhite = 0.55f;
    private const float NeonFlickerRate = 12f;
    private const float NeonFlickerChance = 0.08f;
    private const float NeonFlickerDim = 0.45f;
    private const float UnderlineThickness = 0.09f;
    private const float UnderlineSegment = 0.35f;
    private const float ScanBand = 0.16f;
    private const float ScanAlpha = 0.90f;
    private const float CometBand = 0.10f;

    private const float MoteOvershoot = 0.35f;
    private const float MoteStagger = 0.37f;
    private const float MoteLifeFloor = 0.75f;
    private const float MoteLifeSpread = 0.50f;
    private const float MoteDrift = 0.25f;
    private const float MoteScaleFloor = 0.70f;
    private const float MoteScaleSpread = 0.60f;
    private const float MoteSpinRate = 2.2f;
    private const float MoteSwayRate = 2.2f;
    private const int SakuraSlots = 6;
    private const float SakuraLife = 2.6f;
    private const float SakuraSize = 0.28f;
    private const float SakuraSway = 0.18f;
    private const int SnowSlots = 8;
    private const float SnowLife = 3.2f;
    private const float SnowRadius = 0.07f;
    private const float SnowSway = 0.12f;
    private const int FireflySlots = 6;
    private const float FireflyLife = 3.0f;
    private const float FireflySize = 0.06f;
    private const float FireflyPulseRate = 4f;
    private const float FireflyWander = 0.12f;
    private const int HeartSlots = 5;
    private const float HeartLife = 2.0f;
    private const float HeartSize = 0.32f;
    private const float HeartSway = 0.10f;
    private const int GlitterSlots = 8;
    private const float GlitterLife = 1.2f;
    private const float GlitterSize = 0.30f;
    private const int BubbleSlots = 6;
    private const float BubbleLife = 2.4f;
    private const float BubbleRadius = 0.12f;
    private const float BubbleSway = 0.10f;
    private const int ConfettiSlots = 10;
    private const float ConfettiLife = 2.2f;
    private const float ConfettiSize = 0.16f;
    private const float ConfettiSway = 0.14f;
    private const float StormCycleSeconds = 1.6f;
    private const float StormStrikeChance = 0.50f;
    private const float StormFlashFraction = 0.14f;
    private const float StormEchoStart = 0.17f;
    private const float StormEchoEnd = 0.24f;
    private const float StormEchoAlpha = 0.5f;
    private const float StormFlashAlpha = 0.85f;
    private const int BoltSegments = 5;
    private const float BoltJitter = 0.18f;
    private const float BoltThickness = 1.5f;

    private static readonly Vector4 ChromaticRed = new(1f, 0.25f, 0.25f, 1f);
    private static readonly Vector4 ChromaticCyan = new(0.25f, 0.90f, 1f, 1f);
    private static readonly Vector4 EmbossDark = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly record struct SweepTier(float WidthScale, float AlphaScale);

    private static readonly SweepTier[] SweepTiers =
    {
        new(1.00f, 0.28f),
        new(0.58f, 0.58f),
        new(0.26f, 1.00f),
    };

    private static readonly float[] CometTail = { 1.00f, 0.45f, 0.25f, 0.12f, 0.05f };

    private static readonly float[] SparkOffsetsX = { 0.12f, 0.34f, 0.58f, 0.76f, 0.91f };

    private static readonly float[] SparkOffsetsY = { 0.18f, 0.66f, 0.10f, 0.74f, 0.36f };

    private readonly ref struct EffectFrame
    {
        public readonly ImDrawListPtr DrawList;
        public readonly ImFontPtr Font;
        public readonly float FontSize;
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly string Text;
        public readonly Vector4 Color;
        public readonly TextEffect Effect;
        public readonly double Seconds;

        public EffectFrame(ImDrawListPtr drawList, ImFontPtr font, float fontSize, Vector2 position, Vector2 size,
            string text, Vector4 color, in TextEffect effect)
        {
            DrawList = drawList;
            Font = font;
            FontSize = fontSize;
            Position = position;
            Size = size;
            Text = text;
            Color = color;
            Effect = effect;
            Seconds = Pulse.Seconds + effect.Seed * SeedSpreadSeconds;
        }

        public float Width => Size.X;

        public float Height => Size.Y;

        public float Right => Position.X + Size.X;

        public float Bottom => Position.Y + Size.Y;

        public Vector4 Crest => Effect.Crest;

        public float Phase => Effect.Phase;

        public void AddText(Vector2 at, Vector4 color)
        {
            DrawList.AddText(Font, FontSize, at, ImGui.GetColorU32(color), Text);
        }

        public void Fill()
        {
            AddText(Position, Color);
        }

        public void Fill(Vector4 color)
        {
            AddText(Position, color);
        }

        public void PushClip(Vector2 min, Vector2 max)
        {
            DrawList.PushClipRect(min, max, true);
        }

        public void PopClip()
        {
            DrawList.PopClipRect();
        }

        public Span<ImDrawVert> QuadsSince(int firstVertex)
        {
            return DrawList.VtxBuffer.AsSpan()[firstVertex..];
        }
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 position, string text, Vector4 color, in TextStyle style,
        in TextEffect effect)
    {
        if (effect.Kind == NameEffectKind.None)
        {
            Draw(drawList, position, text, color, style.Scale, style.Weight);
            return;
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(text);
            var frame = new EffectFrame(drawList, ImGui.GetFont(), ImGui.GetFontSize(), position,
                ImGui.CalcTextSize(text), text, color, effect);
            Paint(frame);
        }
    }

    private static void Paint(in EffectFrame frame)
    {
        switch (frame.Effect.Kind)
        {
            case NameEffectKind.Breath:
            case NameEffectKind.Heartbeat:
                DrawBeat(frame);
                return;
            case NameEffectKind.Pulse:
                DrawPulse(frame);
                return;
            case NameEffectKind.Glow:
                DrawRim(frame, GlowRimAlpha);
                frame.Fill();
                return;
            case NameEffectKind.Eclipse:
                DrawHalo(frame);
                frame.Fill();
                return;
            case NameEffectKind.Gradient:
            case NameEffectKind.Ripple:
            case NameEffectKind.Flow:
            case NameEffectKind.Wave:
            case NameEffectKind.Ember:
            case NameEffectKind.Aurora:
            case NameEffectKind.Prism:
            case NameEffectKind.Spectrum:
            case NameEffectKind.Candy:
            case NameEffectKind.Horizon:
                DrawTinted(frame);
                return;
            case NameEffectKind.Chrome:
                DrawTinted(frame);
                DrawCrest(frame, SweepBandFraction, frame.Phase, ChromeSheenAlpha);
                return;
            case NameEffectKind.Blaze:
                DrawTinted(frame);
                DrawEmbers(frame);
                return;
            case NameEffectKind.Frost:
                frame.Fill();
                DrawRim(frame, RimAlpha);
                DrawCrest(frame, FrostBandFraction, frame.Phase, 1f);
                return;
            case NameEffectKind.Sweep:
                frame.Fill();
                DrawCrest(frame, SweepBandFraction, frame.Phase, 1f);
                return;
            case NameEffectKind.Glint:
                frame.Fill();
                if (frame.Phase < GlintActiveFraction)
                {
                    DrawCrest(frame, GlintBandFraction, frame.Phase / GlintActiveFraction, 1f);
                }

                return;
            case NameEffectKind.Glitch:
                frame.Fill();
                DrawTear(frame);
                return;
            case NameEffectKind.Starfall:
                frame.Fill();
                DrawSparks(frame);
                return;
            case NameEffectKind.Stripes:
                DrawStripes(frame);
                return;
            case NameEffectKind.Bounce:
            case NameEffectKind.Shiver:
            case NameEffectKind.Wobble:
            case NameEffectKind.Pop:
            case NameEffectKind.Flipboard:
            case NameEffectKind.Typewriter:
                DrawMoving(frame);
                return;
            case NameEffectKind.Outline:
                DrawStroke(frame, MathF.Max(1f, frame.FontSize * OutlineDistance), Faded(frame.Crest, OutlineAlpha));
                frame.Fill();
                return;
            case NameEffectKind.Shadow:
                frame.AddText(frame.Position + new Vector2(frame.FontSize * ShadowOffset, frame.FontSize * ShadowOffset),
                    Faded(frame.Crest, ShadowAlpha));
                frame.Fill();
                return;
            case NameEffectKind.Longshadow:
                DrawLongshadow(frame);
                frame.Fill();
                return;
            case NameEffectKind.Emboss:
                DrawEmboss(frame);
                frame.Fill();
                return;
            case NameEffectKind.Chromatic:
                DrawChromatic(frame);
                frame.Fill();
                return;
            case NameEffectKind.Neon:
                DrawNeon(frame);
                return;
            case NameEffectKind.Underline:
                frame.Fill();
                DrawUnderline(frame);
                return;
            case NameEffectKind.Scan:
                frame.Fill();
                DrawScan(frame);
                return;
            case NameEffectKind.Comet:
                frame.Fill();
                DrawComet(frame);
                return;
            case NameEffectKind.Sakura:
            case NameEffectKind.Snowfall:
            case NameEffectKind.Fireflies:
            case NameEffectKind.Hearts:
            case NameEffectKind.Glitter:
            case NameEffectKind.Bubbles:
            case NameEffectKind.Confetti:
                frame.Fill();
                DrawMotes(frame);
                return;
            case NameEffectKind.Storm:
                frame.Fill();
                DrawStorm(frame);
                return;
            default:
                frame.Fill();
                return;
        }
    }

    private static void DrawBeat(in EffectFrame frame)
    {
        var beat = frame.Effect.Kind == NameEffectKind.Heartbeat ? Thump(frame.Phase) : Wave(frame.Phase);
        frame.Fill(Vector4.Lerp(frame.Color, frame.Crest, beat));
    }

    private static void DrawPulse(in EffectFrame frame)
    {
        var ramp = frame.Effect.Ramp;
        var lit = ramp.Count > 0
            ? ramp.Sample(frame.Phase)
            : Vector4.Lerp(frame.Color, frame.Crest, Wave(frame.Phase));
        frame.Fill(lit);
    }

    private static void DrawCrest(in EffectFrame frame, float fraction, float travel, float alphaScale)
    {
        var top = frame.Position.Y - frame.Height * ClipOvershoot;
        var bottom = frame.Bottom + frame.Height * ClipOvershoot;
        var band = MathF.Max(1f, frame.Width * fraction);
        var center = frame.Position.X - band * 0.5f + (frame.Width + band) * travel;
        for (var tierIndex = 0; tierIndex < SweepTiers.Length; tierIndex++)
        {
            var tier = SweepTiers[tierIndex];
            var halfWidth = band * tier.WidthScale * 0.5f;
            frame.PushClip(new Vector2(center - halfWidth, top), new Vector2(center + halfWidth, bottom));
            frame.AddText(frame.Position, Faded(frame.Crest, tier.AlphaScale * alphaScale));
            frame.PopClip();
        }
    }

    private static void DrawRim(in EffectFrame frame, float alpha)
    {
        var rim = Faded(frame.Crest, alpha);
        frame.AddText(frame.Position + new Vector2(RimOffset, 0f), rim);
        frame.AddText(frame.Position + new Vector2(-RimOffset, 0f), rim);
        frame.AddText(frame.Position + new Vector2(0f, RimOffset), rim);
        frame.AddText(frame.Position + new Vector2(0f, -RimOffset), rim);
    }

    private static void DrawStroke(in EffectFrame frame, float distance, Vector4 color)
    {
        for (var offsetIndex = 0; offsetIndex < HaloOffsets.Length; offsetIndex++)
        {
            frame.AddText(frame.Position + HaloOffsets[offsetIndex] * distance, color);
        }
    }

    private static void DrawHalo(in EffectFrame frame)
    {
        var breathe = 0.65f + 0.35f * Wave(frame.Phase);
        var radius = EclipseHaloRadius * breathe;
        var glow = Faded(frame.Crest, EclipseHaloAlpha * breathe);
        for (var pointIndex = 0; pointIndex < EclipseHaloPoints; pointIndex++)
        {
            var angle = pointIndex / (float)EclipseHaloPoints * MathF.Tau;
            frame.AddText(frame.Position + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius), glow);
        }
    }

    private static void DrawTear(in EffectFrame frame)
    {
        if (frame.Phase < 1f - GlitchActiveFraction)
        {
            return;
        }

        var ramp = frame.Effect.Ramp;
        var step = (int)(frame.Phase * GlitchSteps);
        var bandHeight = frame.Height / GlitchBands;
        var leading = Faded(ramp.Start, GlitchAlpha);
        var trailing = Faded(ramp.Quarter, GlitchAlpha);
        for (var bandIndex = 0; bandIndex < GlitchBands; bandIndex++)
        {
            var shift = Jitter(step, bandIndex) * GlitchShift;
            var bandTop = frame.Position.Y + bandHeight * bandIndex;
            frame.PushClip(new Vector2(frame.Position.X - GlitchShift, bandTop),
                new Vector2(frame.Right + GlitchShift, bandTop + bandHeight));
            frame.AddText(frame.Position + new Vector2(shift, 0f), leading);
            frame.AddText(frame.Position + new Vector2(-shift, 0f), trailing);
            frame.PopClip();
        }
    }

    private static void DrawSparks(in EffectFrame frame)
    {
        var radius = frame.FontSize * StarfallRadiusScale;
        for (var sparkIndex = 0; sparkIndex < SparkOffsetsX.Length; sparkIndex++)
        {
            var local = Fraction(frame.Phase + sparkIndex * StarfallStagger);
            var start = 1f - StarfallRiseFraction;
            if (local < start)
            {
                continue;
            }

            var glow = MathF.Sin((local - start) / StarfallRiseFraction * MathF.PI);
            if (glow <= 0.01f)
            {
                continue;
            }

            var center = new Vector2(
                frame.Position.X + frame.Width * SparkOffsetsX[sparkIndex],
                frame.Position.Y + frame.Height * SparkOffsetsY[sparkIndex]);
            frame.DrawList.AddCircleFilled(center, radius * (0.6f + 0.6f * glow),
                ImGui.GetColorU32(Faded(frame.Crest, glow)));
        }
    }

    private static void DrawTinted(in EffectFrame frame)
    {
        var firstVertex = frame.DrawList.VtxBuffer.Size;
        frame.Fill();
        if (frame.Width <= 0f || frame.Height <= 0f)
        {
            return;
        }

        var quads = frame.QuadsSince(firstVertex);
        for (var vertexIndex = 0; vertexIndex < quads.Length; vertexIndex++)
        {
            ref var vertex = ref quads[vertexIndex];
            var across = Math.Clamp((vertex.Pos.X - frame.Position.X) / frame.Width, 0f, 1f);
            var down = Math.Clamp((vertex.Pos.Y - frame.Position.Y) / frame.Height, 0f, 1f);
            vertex.Col = ImGui.GetColorU32(Tint(frame, across, down, vertexIndex >> 2));
        }
    }

    private static Vector4 Tint(in EffectFrame frame, float across, float down, int glyphIndex)
    {
        var effect = frame.Effect;
        return effect.Kind switch
        {
            NameEffectKind.Gradient when effect.Ramp.Count > 0 => effect.Ramp.SampleAcross(across),
            NameEffectKind.Wave => effect.Ramp.Sample(across * WaveSpan - effect.Phase),
            NameEffectKind.Spectrum => effect.Ramp.Sample(across * SpectrumSpan - effect.Phase),
            NameEffectKind.Prism => effect.Ramp.Sample(across * PrismSpan - effect.Phase * PrismSpan),
            NameEffectKind.Aurora => Vector4.Lerp(
                effect.Ramp.Sample(across - effect.Phase),
                effect.Ramp.Sample(across * AuroraCounterCrests + effect.Phase * AuroraCounterRate),
                0.5f),
            NameEffectKind.Candy => CandyTint(frame, glyphIndex),
            NameEffectKind.Horizon when effect.Ramp.Count > 0 => effect.Ramp.SampleAcross(down),
            NameEffectKind.Horizon => Vector4.Lerp(frame.Color, effect.Crest, down),
            NameEffectKind.Chrome => ChromeTint(frame, down),
            NameEffectKind.Blaze => BlazeTint(frame, down, glyphIndex),
            _ => Vector4.Lerp(frame.Color, effect.Crest, CrestFactor(effect.Kind, across, effect.Phase)),
        };
    }

    private static Vector4 CandyTint(in EffectFrame frame, int glyphIndex)
    {
        var ramp = frame.Effect.Ramp;
        var step = (int)(frame.Phase * ramp.Count);
        return ramp.Stop((glyphIndex + step) % ramp.Count);
    }

    private static Vector4 ChromeTint(in EffectFrame frame, float down)
    {
        float shade;
        if (down < ChromeDarkBandTop)
        {
            shade = 1f - down / ChromeDarkBandTop;
        }
        else if (down < ChromeDarkBandBottom)
        {
            shade = -(down - ChromeDarkBandTop) / (ChromeDarkBandBottom - ChromeDarkBandTop);
        }
        else
        {
            shade = -1f + (down - ChromeDarkBandBottom) / (1f - ChromeDarkBandBottom) * (1f + ChromeFootShine);
        }

        if (shade >= 0f)
        {
            return Vector4.Lerp(frame.Color, frame.Crest, shade);
        }

        var dim = new Vector4(frame.Color.X * ChromeDim, frame.Color.Y * ChromeDim, frame.Color.Z * ChromeDim,
            frame.Color.W);
        return Vector4.Lerp(frame.Color, dim, -shade);
    }

    private static Vector4 BlazeTint(in EffectFrame frame, float down, int glyphIndex)
    {
        var lick = Hash01(glyphIndex, (int)(frame.Seconds * BlazeFlickerRate), 3);
        var heat = Math.Clamp(down * (BlazeFlickerFloor + BlazeFlickerSpread * lick), 0f, 1f);
        var ramp = frame.Effect.Ramp;
        return ramp.Count > 0 ? ramp.SampleAcross(heat) : Vector4.Lerp(frame.Color, frame.Crest, heat);
    }

    private static void DrawEmbers(in EffectFrame frame)
    {
        var radius = frame.FontSize * BlazeEmberRadius;
        for (var slot = 0; slot < BlazeEmberSlots; slot++)
        {
            var mote = MoteFor(frame, slot, BlazeEmberLife, true, 0f);
            var alpha = (1f - mote.Local) * (1f - mote.Local);
            frame.DrawList.AddCircleFilled(mote.Center, radius * mote.Scale,
                ImGui.GetColorU32(Faded(frame.Crest, alpha)));
        }
    }

    private static void DrawStripes(in EffectFrame frame)
    {
        frame.Fill();
        if (frame.Width <= 0f)
        {
            return;
        }

        var ramp = frame.Effect.Ramp;
        var top = frame.Position.Y - frame.Height * ClipOvershoot;
        var bottom = frame.Bottom + frame.Height * ClipOvershoot;
        var bandWidth = frame.Width / StripeBands;
        var scroll = Fraction(frame.Phase) * bandWidth * ramp.Count;
        for (var band = -ramp.Count; band <= StripeBands; band++)
        {
            var left = frame.Position.X + band * bandWidth + scroll;
            if (left >= frame.Right)
            {
                break;
            }

            if (left + bandWidth <= frame.Position.X)
            {
                continue;
            }

            var stop = ((band % ramp.Count) + ramp.Count) % ramp.Count;
            frame.PushClip(new Vector2(MathF.Max(left, frame.Position.X), top),
                new Vector2(MathF.Min(left + bandWidth, frame.Right), bottom));
            frame.AddText(frame.Position, ramp.Stop(stop));
            frame.PopClip();
        }
    }

    private static void DrawMoving(in EffectFrame frame)
    {
        var firstVertex = frame.DrawList.VtxBuffer.Size;
        frame.Fill();
        var quads = frame.QuadsSince(firstVertex);
        var glyphCount = quads.Length / 4;
        if (glyphCount == 0)
        {
            return;
        }

        switch (frame.Effect.Kind)
        {
            case NameEffectKind.Bounce:
                for (var glyph = 0; glyph < glyphCount; glyph++)
                {
                    var hop = MathF.Max(0f, MathF.Sin((frame.Phase - glyph * BounceStagger) * MathF.Tau));
                    OffsetQuad(quads, glyph, new Vector2(0f, -hop * frame.FontSize * BounceAmplitude));
                }

                return;
            case NameEffectKind.Shiver:
                if (frame.Phase >= ShiverActiveFraction)
                {
                    return;
                }

                var step = (int)(frame.Seconds * ShiverRate);
                for (var glyph = 0; glyph < glyphCount; glyph++)
                {
                    var shiftX = (Hash01(glyph, step, 1) - 0.5f) * 2f * frame.FontSize * ShiverAmplitudeX;
                    var shiftY = (Hash01(glyph, step, 2) - 0.5f) * 2f * frame.FontSize * ShiverAmplitudeY;
                    OffsetQuad(quads, glyph, new Vector2(shiftX, shiftY));
                }

                return;
            case NameEffectKind.Wobble:
                for (var glyph = 0; glyph < glyphCount; glyph++)
                {
                    var angle = MathF.Sin((frame.Phase - glyph * WobbleStagger) * MathF.Tau) * WobbleRadians;
                    RotateQuad(quads, glyph, angle);
                }

                return;
            case NameEffectKind.Pop:
                for (var glyph = 0; glyph < glyphCount; glyph++)
                {
                    var local = Fraction(frame.Phase - glyph * PopStagger);
                    if (local >= PopWindow)
                    {
                        continue;
                    }

                    var growth = 1f + PopGrowth * MathF.Sin(local / PopWindow * MathF.PI);
                    ScaleQuad(quads, glyph, new Vector2(growth, growth));
                }

                return;
            case NameEffectKind.Flipboard:
                for (var glyph = 0; glyph < glyphCount; glyph++)
                {
                    var local = Fraction(frame.Phase - glyph * FlipStagger);
                    if (local >= FlipWindow)
                    {
                        continue;
                    }

                    var squash = MathF.Abs(MathF.Cos(local / FlipWindow * MathF.PI));
                    ScaleQuad(quads, glyph, new Vector2(1f, MathF.Max(0.02f, squash)));
                }

                return;
            case NameEffectKind.Typewriter:
                DrawTypewriter(frame, quads, glyphCount);
                return;
        }
    }

    private static void DrawTypewriter(in EffectFrame frame, Span<ImDrawVert> quads, int glyphCount)
    {
        var phase = frame.Phase;
        int shown;
        var typing = true;
        if (phase < TypeFraction)
        {
            shown = (int)(phase / TypeFraction * (glyphCount + 1));
        }
        else if (phase < TypeFraction + HoldFraction)
        {
            shown = glyphCount;
            typing = false;
        }
        else
        {
            var erase = (phase - TypeFraction - HoldFraction) / (1f - TypeFraction - HoldFraction);
            shown = glyphCount - (int)(erase * (glyphCount + 1));
        }

        shown = Math.Clamp(shown, 0, glyphCount);
        for (var glyph = shown; glyph < glyphCount; glyph++)
        {
            FadeQuad(quads, glyph, 0f);
        }

        var blinkOn = (int)(frame.Seconds * CaretBlinkRate) % 2 == 0;
        if (!typing && !blinkOn)
        {
            return;
        }

        var caretLeft = shown == 0 ? frame.Position.X : quads[(shown - 1) * 4 + 1].Pos.X + 1f;
        var caretWidth = MathF.Max(1f, frame.FontSize * CaretWidth);
        frame.DrawList.AddRectFilled(
            new Vector2(caretLeft, frame.Position.Y + frame.Height * CaretTop),
            new Vector2(caretLeft + caretWidth, frame.Position.Y + frame.Height * CaretBottom),
            ImGui.GetColorU32(frame.Crest));
    }

    private static void DrawLongshadow(in EffectFrame frame)
    {
        var step = frame.FontSize * LongshadowStep;
        for (var layer = LongshadowSteps; layer >= 1; layer--)
        {
            var depth = (layer - 1f) / (LongshadowSteps - 1f);
            var alpha = LongshadowNearAlpha + (LongshadowFarAlpha - LongshadowNearAlpha) * depth;
            frame.AddText(frame.Position + new Vector2(step * layer, step * layer), Faded(frame.Crest, alpha));
        }
    }

    private static void DrawEmboss(in EffectFrame frame)
    {
        var distance = MathF.Max(1f, frame.FontSize * EmbossDistance);
        frame.AddText(frame.Position + new Vector2(-distance, -distance), Faded(frame.Crest, EmbossLightAlpha));
        frame.AddText(frame.Position + new Vector2(distance, distance), Faded(EmbossDark, EmbossDarkAlpha));
    }

    private static void DrawChromatic(in EffectFrame frame)
    {
        var shift = MathF.Max(1f, frame.FontSize * ChromaticShift)
            * (1f + ChromaticDrift * MathF.Sin(frame.Phase * MathF.Tau));
        frame.AddText(frame.Position + new Vector2(-shift, 0f), Faded(ChromaticRed, ChromaticAlpha));
        frame.AddText(frame.Position + new Vector2(shift, 0f), Faded(ChromaticCyan, ChromaticAlpha));
    }

    private static void DrawNeon(in EffectFrame frame)
    {
        var flicker = Hash01((int)(frame.Seconds * NeonFlickerRate), 0, 9) < NeonFlickerChance ? NeonFlickerDim : 1f;
        var spread = frame.FontSize * NeonSpread;
        for (var layer = NeonLayers; layer >= 1; layer--)
        {
            var distance = spread * layer / NeonLayers;
            var alpha = frame.Crest.W * (1f - (layer - 1f) / NeonLayers) / 4f * flicker;
            DrawStroke(frame, distance, Faded(frame.Crest, alpha));
        }

        var core = Vector4.Lerp(frame.Color, White, NeonCoreWhite * flicker);
        frame.Fill(core);
    }

    private static void DrawUnderline(in EffectFrame frame)
    {
        if (frame.Width <= 0f)
        {
            return;
        }

        var thickness = MathF.Max(1f, frame.FontSize * UnderlineThickness);
        var segment = frame.Width * UnderlineSegment;
        var left = frame.Position.X + (frame.Width - segment) * Triangle(frame.Phase);
        var bottom = frame.Bottom;
        frame.DrawList.AddRectFilled(new Vector2(left, bottom - thickness), new Vector2(left + segment, bottom),
            ImGui.GetColorU32(frame.Crest), thickness * 0.5f);
    }

    private static void DrawScan(in EffectFrame frame)
    {
        var band = frame.FontSize * ScanBand;
        var center = frame.Position.Y - band + (frame.Height + band * 2f) * frame.Phase;
        frame.PushClip(new Vector2(frame.Position.X - band, center - band * 0.5f),
            new Vector2(frame.Right + band, center + band * 0.5f));
        frame.AddText(frame.Position, Faded(frame.Crest, ScanAlpha));
        frame.PopClip();
    }

    private static void DrawComet(in EffectFrame frame)
    {
        if (frame.Width <= 0f)
        {
            return;
        }

        var top = frame.Position.Y - frame.Height * ClipOvershoot;
        var bottom = frame.Bottom + frame.Height * ClipOvershoot;
        var band = MathF.Max(1f, frame.Width * CometBand);
        var tail = band * (CometTail.Length - 1);
        var head = frame.Position.X - tail - band * 0.5f + (frame.Width + tail + band) * frame.Phase;
        for (var tier = 0; tier < CometTail.Length; tier++)
        {
            var center = head - band * tier;
            frame.PushClip(new Vector2(center - band * 0.5f, top), new Vector2(center + band * 0.5f, bottom));
            frame.AddText(frame.Position, Faded(frame.Crest, CometTail[tier]));
            frame.PopClip();
        }
    }

    private readonly record struct Mote(Vector2 Center, float Local, float Scale, float Spin, int Cycle);

    private static Mote MoteFor(in EffectFrame frame, int slot, float baseLife, bool rising, float sway)
    {
        var life = baseLife * (MoteLifeFloor + MoteLifeSpread * Hash01(slot, 0, 11));
        var travel = frame.Seconds / life + slot * MoteStagger;
        var cycle = (int)Math.Floor(travel);
        var local = (float)(travel - cycle);
        var drift = (Hash01(slot, cycle, 2) - 0.5f) * MoteDrift;
        var across = Math.Clamp(Hash01(slot, cycle, 1) + drift * local, 0f, 1f);
        var swing = MathF.Sin((float)(frame.Seconds * MoteSwayRate) + slot) * frame.FontSize * sway;
        var overshoot = frame.Height * MoteOvershoot;
        var span = frame.Height + overshoot * 2f;
        var y = rising ? frame.Bottom + overshoot - span * local : frame.Position.Y - overshoot + span * local;
        var scale = MoteScaleFloor + MoteScaleSpread * Hash01(slot, cycle, 3);
        var spin = Hash01(slot, cycle, 4) * MathF.Tau + local * MoteSpinRate;
        return new Mote(new Vector2(frame.Position.X + frame.Width * across + swing, y), local, scale, spin, cycle);
    }

    private static Mote RestingMoteFor(in EffectFrame frame, int slot, float baseLife, float wander)
    {
        var life = baseLife * (MoteLifeFloor + MoteLifeSpread * Hash01(slot, 0, 11));
        var travel = frame.Seconds / life + slot * MoteStagger;
        var cycle = (int)Math.Floor(travel);
        var local = (float)(travel - cycle);
        var wanderX = MathF.Sin((float)(frame.Seconds * MoteSwayRate) + slot) * frame.FontSize * wander;
        var wanderY = MathF.Cos((float)(frame.Seconds * MoteSwayRate * 0.7f) + slot) * frame.FontSize * wander;
        var center = new Vector2(
            frame.Position.X + frame.Width * Hash01(slot, cycle, 1) + wanderX,
            frame.Position.Y + frame.Height * Hash01(slot, cycle, 2) + wanderY);
        var scale = MoteScaleFloor + MoteScaleSpread * Hash01(slot, cycle, 3);
        return new Mote(center, local, scale, 0f, cycle);
    }

    private static void DrawMotes(in EffectFrame frame)
    {
        if (frame.Width <= 0f)
        {
            return;
        }

        switch (frame.Effect.Kind)
        {
            case NameEffectKind.Sakura:
                for (var slot = 0; slot < SakuraSlots; slot++)
                {
                    var mote = MoteFor(frame, slot, SakuraLife, false, SakuraSway);
                    var tint = Faded(MoteTint(frame, slot), MathF.Sin(mote.Local * MathF.PI));
                    DrawPetal(frame.DrawList, mote.Center, frame.FontSize * SakuraSize * mote.Scale, mote.Spin,
                        ImGui.GetColorU32(tint));
                }

                return;
            case NameEffectKind.Snowfall:
                for (var slot = 0; slot < SnowSlots; slot++)
                {
                    var mote = MoteFor(frame, slot, SnowLife, false, SnowSway);
                    var tint = Faded(frame.Crest, MathF.Sin(mote.Local * MathF.PI));
                    frame.DrawList.AddCircleFilled(mote.Center, frame.FontSize * SnowRadius * mote.Scale,
                        ImGui.GetColorU32(tint));
                }

                return;
            case NameEffectKind.Fireflies:
                for (var slot = 0; slot < FireflySlots; slot++)
                {
                    var mote = RestingMoteFor(frame, slot, FireflyLife, FireflyWander);
                    var glow = 0.35f + 0.65f * MathF.Max(0f, MathF.Sin((float)(frame.Seconds * FireflyPulseRate) + slot));
                    var alpha = MathF.Min(1f, (1f - mote.Local) * 2f) * MathF.Min(1f, mote.Local * 4f) * glow;
                    DrawFirefly(frame.DrawList, mote.Center, frame.FontSize * FireflySize * mote.Scale, frame.Crest, alpha);
                }

                return;
            case NameEffectKind.Hearts:
                for (var slot = 0; slot < HeartSlots; slot++)
                {
                    var mote = MoteFor(frame, slot, HeartLife, true, HeartSway);
                    var alpha = MathF.Min(1f, (1f - mote.Local) * 2f) * MathF.Min(1f, mote.Local * 4f);
                    DrawHeart(frame.DrawList, mote.Center, frame.FontSize * HeartSize * mote.Scale,
                        ImGui.GetColorU32(Faded(MoteTint(frame, slot), alpha)));
                }

                return;
            case NameEffectKind.Glitter:
                for (var slot = 0; slot < GlitterSlots; slot++)
                {
                    var mote = RestingMoteFor(frame, slot, GlitterLife, 0f);
                    var twinkle = MathF.Sin(mote.Local * MathF.PI);
                    DrawSparkle(frame.DrawList, mote.Center, frame.FontSize * GlitterSize * mote.Scale * twinkle,
                        ImGui.GetColorU32(Faded(frame.Crest, twinkle)));
                }

                return;
            case NameEffectKind.Bubbles:
                for (var slot = 0; slot < BubbleSlots; slot++)
                {
                    var mote = MoteFor(frame, slot, BubbleLife, true, BubbleSway);
                    var alpha = MathF.Min(1f, (1f - mote.Local) * 2f) * MathF.Min(1f, mote.Local * 4f);
                    DrawBubble(frame.DrawList, mote.Center, frame.FontSize * BubbleRadius * mote.Scale,
                        ImGui.GetColorU32(Faded(frame.Crest, alpha)));
                }

                return;
            case NameEffectKind.Confetti:
                for (var slot = 0; slot < ConfettiSlots; slot++)
                {
                    var mote = MoteFor(frame, slot, ConfettiLife, false, ConfettiSway);
                    var tint = Faded(MoteTint(frame, slot), MathF.Sin(mote.Local * MathF.PI));
                    DrawConfetti(frame.DrawList, mote.Center, frame.FontSize * ConfettiSize * mote.Scale, mote.Spin,
                        ImGui.GetColorU32(tint));
                }

                return;
        }
    }

    private static Vector4 MoteTint(in EffectFrame frame, int slot)
    {
        var ramp = frame.Effect.Ramp;
        return ramp.Count > 0 ? ramp.Stop(slot % ramp.Count) : frame.Crest;
    }

    private static void DrawStorm(in EffectFrame frame)
    {
        if (frame.Width <= 0f)
        {
            return;
        }

        var travel = frame.Seconds / StormCycleSeconds;
        var cycle = (int)Math.Floor(travel);
        if (Hash01(cycle, 0, 5) >= StormStrikeChance)
        {
            return;
        }

        var local = (float)(travel - cycle);
        if (local < StormFlashFraction)
        {
            var flash = 1f - local / StormFlashFraction;
            DrawBolt(frame, cycle, flash);
            frame.Fill(Faded(frame.Crest, StormFlashAlpha * flash));
            return;
        }

        if (local >= StormEchoStart && local < StormEchoEnd)
        {
            var echo = 1f - (local - StormEchoStart) / (StormEchoEnd - StormEchoStart);
            frame.Fill(Faded(frame.Crest, StormEchoAlpha * echo));
        }
    }

    private static void DrawBolt(in EffectFrame frame, int cycle, float alpha)
    {
        var jitter = frame.FontSize * BoltJitter;
        var top = frame.Position.Y - frame.Height * ClipOvershoot;
        var bottom = frame.Bottom + frame.Height * MoteOvershoot;
        var startX = frame.Position.X + frame.Width * Hash01(cycle, 1, 5);
        var previous = new Vector2(startX, top);
        var packed = ImGui.GetColorU32(Faded(frame.Crest, alpha));
        for (var segment = 1; segment <= BoltSegments; segment++)
        {
            var next = new Vector2(
                startX + (Hash01(cycle, segment, 6) - 0.5f) * 2f * jitter,
                top + (bottom - top) * segment / BoltSegments);
            frame.DrawList.AddLine(previous, next, packed, BoltThickness);
            previous = next;
        }
    }

    private static void DrawHeart(ImDrawListPtr drawList, Vector2 at, float size, uint color)
    {
        var radius = size * 0.5f;
        drawList.AddCircleFilled(new Vector2(at.X - radius * 0.48f, at.Y), radius * 0.6f, color);
        drawList.AddCircleFilled(new Vector2(at.X + radius * 0.48f, at.Y), radius * 0.6f, color);
        drawList.AddTriangleFilled(
            new Vector2(at.X - radius, at.Y + radius * 0.12f),
            new Vector2(at.X + radius, at.Y + radius * 0.12f),
            new Vector2(at.X, at.Y + radius * 1.45f),
            color);
    }

    private static void DrawSparkle(ImDrawListPtr drawList, Vector2 at, float size, uint color)
    {
        var radius = size * 0.5f;
        var waist = radius * 0.28f;
        drawList.AddQuadFilled(
            new Vector2(at.X, at.Y - radius),
            new Vector2(at.X + waist, at.Y),
            new Vector2(at.X, at.Y + radius),
            new Vector2(at.X - waist, at.Y),
            color);
        drawList.AddQuadFilled(
            new Vector2(at.X - radius, at.Y),
            new Vector2(at.X, at.Y - waist),
            new Vector2(at.X + radius, at.Y),
            new Vector2(at.X, at.Y + waist),
            color);
    }

    private static void DrawPetal(ImDrawListPtr drawList, Vector2 at, float size, float angle, uint color)
    {
        var radius = size * 0.5f;
        drawList.AddQuadFilled(
            Rotated(at, 0f, -radius, angle),
            Rotated(at, radius * 0.55f, 0f, angle),
            Rotated(at, 0f, radius, angle),
            Rotated(at, -radius * 0.55f, 0f, angle),
            color);
    }

    private static void DrawConfetti(ImDrawListPtr drawList, Vector2 at, float size, float angle, uint color)
    {
        var halfWidth = size * 0.5f;
        var halfHeight = size * 0.28f;
        drawList.AddQuadFilled(
            Rotated(at, -halfWidth, -halfHeight, angle),
            Rotated(at, halfWidth, -halfHeight, angle),
            Rotated(at, halfWidth, halfHeight, angle),
            Rotated(at, -halfWidth, halfHeight, angle),
            color);
    }

    private static void DrawBubble(ImDrawListPtr drawList, Vector2 at, float radius, uint color)
    {
        drawList.AddCircle(at, radius, color, 0, 1f);
        drawList.AddCircleFilled(new Vector2(at.X - radius * 0.35f, at.Y - radius * 0.35f), radius * 0.22f, color);
    }

    private static void DrawFirefly(ImDrawListPtr drawList, Vector2 at, float size, Vector4 tint, float alpha)
    {
        drawList.AddCircleFilled(at, size * 1.8f, ImGui.GetColorU32(Faded(tint, alpha * 0.25f)));
        drawList.AddCircleFilled(at, size * 0.5f, ImGui.GetColorU32(Faded(tint, alpha)));
    }

    private static Vector2 Rotated(Vector2 at, float x, float y, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(at.X + x * cos - y * sin, at.Y + x * sin + y * cos);
    }

    private static void OffsetQuad(Span<ImDrawVert> quads, int glyph, Vector2 delta)
    {
        var first = glyph * 4;
        for (var corner = 0; corner < 4; corner++)
        {
            quads[first + corner].Pos += delta;
        }
    }

    private static void ScaleQuad(Span<ImDrawVert> quads, int glyph, Vector2 scale)
    {
        var first = glyph * 4;
        var center = (quads[first].Pos + quads[first + 2].Pos) * 0.5f;
        for (var corner = 0; corner < 4; corner++)
        {
            ref var vertex = ref quads[first + corner];
            vertex.Pos = new Vector2(
                center.X + (vertex.Pos.X - center.X) * scale.X,
                center.Y + (vertex.Pos.Y - center.Y) * scale.Y);
        }
    }

    private static void RotateQuad(Span<ImDrawVert> quads, int glyph, float angle)
    {
        var first = glyph * 4;
        var center = (quads[first].Pos + quads[first + 2].Pos) * 0.5f;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        for (var corner = 0; corner < 4; corner++)
        {
            ref var vertex = ref quads[first + corner];
            var x = vertex.Pos.X - center.X;
            var y = vertex.Pos.Y - center.Y;
            vertex.Pos = new Vector2(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);
        }
    }

    private static void FadeQuad(Span<ImDrawVert> quads, int glyph, float alpha)
    {
        var first = glyph * 4;
        var alphaBits = (uint)MathF.Round(alpha * 255f) << 24;
        for (var corner = 0; corner < 4; corner++)
        {
            ref var vertex = ref quads[first + corner];
            vertex.Col = (vertex.Col & 0x00FFFFFFu) | alphaBits;
        }
    }

    private static Vector4 Faded(Vector4 color, float alpha) => color with { W = color.W * alpha };

    private static float CrestFactor(NameEffectKind kind, float progress, float phase)
    {
        return kind switch
        {
            NameEffectKind.Flow => Triangle(progress + phase),
            NameEffectKind.Ripple => Wave(progress * RippleCrests + phase),
            NameEffectKind.Ember => Ember(progress, phase),
            _ => progress,
        };
    }

    private static float Ember(float progress, float phase)
    {
        var slow = Wave(progress * EmberSlowCrests + phase);
        var fast = Wave(progress * EmberFastCrests - phase * EmberFastRate);
        return slow * (1f - EmberFastWeight) + fast * EmberFastWeight;
    }

    private static float Thump(float phase)
    {
        var wrapped = Fraction(phase);
        return MathF.Min(1f, Spike(wrapped, 0f) + Spike(wrapped, ThumpEchoAt) * ThumpEchoWeight);
    }

    private static float Spike(float phase, float at)
    {
        var delta = phase - at;
        if (delta < 0f || delta > ThumpWidth)
        {
            return 0f;
        }

        return MathF.Sin(delta / ThumpWidth * MathF.PI);
    }

    private static float Jitter(int step, int bandIndex)
    {
        var hash = (uint)((step * 73856093) ^ ((bandIndex + 1) * 19349663));
        hash ^= hash >> 13;
        hash *= 2654435761u;
        hash ^= hash >> 16;
        return ((hash % 2000u) / 1000f) - 1f;
    }

    private static float Hash01(int first, int second, int salt)
    {
        var hash = (uint)(first * 73856093) ^ (uint)(second * 19349663) ^ (uint)(salt * 83492791);
        hash ^= hash >> 13;
        hash *= 2654435761u;
        hash ^= hash >> 16;
        return (hash & 0xFFFFFFu) / 16777216f;
    }

    private static float Wave(float phase) => (MathF.Sin(phase * MathF.Tau) + 1f) * 0.5f;

    private static float Fraction(float phase) => phase - MathF.Floor(phase);

    private static float Triangle(float phase) => 1f - MathF.Abs(Fraction(phase) * 2f - 1f);
}
