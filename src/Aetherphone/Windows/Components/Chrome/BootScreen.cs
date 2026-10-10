using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class BootScreen
{
    private const float GreetingFontScale = 2.3f;
    private const float CaptionFontScale = 1.10f;
    private const float EmblemBaseRadius = 44f;
    private const float LetterStaggerWindow = 0.6f;
    private const float LetterRevealSpan = 0.4f;
    private const float LetterRisePixels = 16f;
    private const float GreetingDriftPixels = 18f;
    private const float EmblemMarkSpan = 2.4f;
    private const float CaptionGapUnits = 34f;
    private const float HaloReach = 1.5f;
    private const float HaloAlpha = 0.32f;
    private const int BatCount = 3;
    private const float BatOrbitSpeed = 0.9f;
    private static readonly Vector4 StageInk = new(1f, 1f, 1f, 0.96f);

    private static readonly Vector2[] GlowOffsets =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f), new(1f, 1f), new(-1f, -1f),
    };

    public static void Draw(Rect screen, PhoneTheme theme, BootSequence boot)
    {
        var scale = UiScale.Current;
        var rounding = theme.ScreenRounding * scale;
        var dl = ImGui.GetForegroundDrawList();
        if (boot.BackdropAlpha > 0f)
        {
            DrawBackdrop(dl, screen, boot.BackdropAlpha, rounding);
        }

        if (boot.EmblemAlpha > 0f || boot.EmblemRingAlpha > 0f)
        {
            DrawEmblem(dl, screen.Center, theme, boot, scale);
        }

        if (boot.EmblemAlpha > 0.01f)
        {
            DrawLoadingCaption(dl, screen.Center, boot, scale);
        }

        if (boot.Greeting is not null && boot.GreetingAlpha > 0f)
        {
            DrawGreeting(dl, screen.Center, boot, scale);
        }
    }

    public static void DrawBackdrop(ImDrawListPtr dl, Rect screen, float alpha, float rounding) =>
        BrandMark.DrawStage(dl, screen, rounding, alpha, false, 1f);

    private static void DrawEmblem(ImDrawListPtr dl, Vector2 center, PhoneTheme theme, BootSequence boot, float scale)
    {
        var alpha = boot.EmblemAlpha;
        var baseRadius = EmblemBaseRadius * scale * boot.EmblemScale;
        var markSize = baseRadius * EmblemMarkSpan;
        if (boot.EmblemRingAlpha > 0f)
        {
            var ringSize = markSize * (1f + BootTiming.EmblemRingExpansion * 0.5f * boot.EmblemRingProgress);
            var ringHalf = new Vector2(ringSize * 0.5f, ringSize * 0.5f);
            Squircle.Stroke(dl, center - ringHalf, center + ringHalf, ringSize * BrandMark.CornerFraction,
                ImGui.GetColorU32(Accent with { W = boot.EmblemRingAlpha * 0.4f }), 1.6f * scale);
        }

        if (alpha <= 0f)
        {
            return;
        }

        if (SeasonalTheme.Halloween)
        {
            DrawHaunting(dl, center, markSize, alpha, scale);
        }

        if (BrandMark.TryDraw(dl, center, markSize, alpha))
        {
            return;
        }

        LoadingPulse.Spinner(center, baseRadius, theme.Accent, alpha, dl);
    }

    private static void DrawLoadingCaption(ImDrawListPtr dl, Vector2 center, BootSequence boot, float scale)
    {
        var alpha = boot.EmblemAlpha;
        var baseRadius = EmblemBaseRadius * scale * boot.EmblemScale;
        var caret = center.Y + baseRadius * EmblemMarkSpan * 0.5f + CaptionGapUnits * scale;
        LoadingPulse.Caption(new Vector2(center.X, caret), StageInk, Accent, LoadingPulse.SafeLabel(),
            alpha, CaptionFontScale, drawList: dl);
    }

    private static Vector4 Accent => SeasonalTheme.Halloween ? Spooks.Pumpkin : BrandMark.Lilac;

    private static void DrawHaunting(ImDrawListPtr drawList, Vector2 center, float markSize, float alpha, float scale)
    {
        NightScene.Glow(drawList, center, markSize * HaloReach, Spooks.Pumpkin with { W = HaloAlpha * alpha }, 12);
        var time = (float)ImGui.GetTime();
        var ink = ImGui.GetColorU32(Spooks.BatShadow with { W = alpha });
        for (var batIndex = 0; batIndex < BatCount; batIndex++)
        {
            var angle = time * BatOrbitSpeed + batIndex * MathF.Tau / BatCount;
            var orbit = markSize * (0.78f + batIndex * 0.1f);
            var position = center + new Vector2(MathF.Cos(angle) * orbit, MathF.Sin(angle) * orbit * 0.45f);
            var flap = MathF.Sin(time * 14f + batIndex * 1.3f);
            NightScene.DrawBat(drawList, position, (0.9f + batIndex * 0.12f) * scale, flap, ink);
        }
    }

    private static string greetingSource = string.Empty;
    private static string[] greetingGlyphs = Array.Empty<string>();

    private static string[] GreetingGlyphs(string text)
    {
        if (!string.Equals(greetingSource, text, StringComparison.Ordinal))
        {
            greetingSource = text;
            greetingGlyphs = new string[text.Length];
            for (var index = 0; index < text.Length; index++)
            {
                greetingGlyphs[index] = text[index].ToString();
            }
        }

        return greetingGlyphs;
    }

    private static void DrawGreeting(ImDrawListPtr dl, Vector2 center, BootSequence boot, float scale)
    {
        var text = boot.Greeting!;
        var length = text.Length;
        if (length == 0)
        {
            return;
        }

        var glyphs = GreetingGlyphs(text);
        using (Plugin.Fonts.Push(GreetingFontScale, FontWeight.Bold))
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            Span<float> widths = stackalloc float[length];
            var totalWidth = 0f;
            var height = 0f;
            for (var index = 0; index < length; index++)
            {
                var glyphSize = ImGui.CalcTextSize(glyphs[index]);
                widths[index] = glyphSize.X;
                totalWidth += glyphSize.X;
                if (glyphSize.Y > height)
                {
                    height = glyphSize.Y;
                }
            }

            var driftPixels = boot.GreetingDrift * GreetingDriftPixels * scale;
            var penX = center.X - totalWidth * 0.5f;
            var baseY = center.Y - height * 0.5f - driftPixels;
            for (var index = 0; index < length; index++)
            {
                var letterStart = length <= 1 ? 0f : index / (length - 1f) * LetterStaggerWindow;
                var letterProgress =
                    Easing.EaseOutCubic(Easing.Clamp01((boot.GreetingReveal - letterStart) / LetterRevealSpan));
                var letterAlpha = letterProgress * boot.GreetingAlpha;
                if (letterAlpha > 0.01f)
                {
                    var rise = (1f - letterProgress) * LetterRisePixels * scale;
                    DrawGlyph(dl, font, fontSize, glyphs[index], new Vector2(penX, baseY + rise), StageInk,
                        letterAlpha, scale);
                }

                penX += widths[index];
            }
        }
    }

    private static void DrawGlyph(ImDrawListPtr dl, ImFontPtr font, float fontSize, string glyph, Vector2 position,
        Vector4 color, float alpha, float scale)
    {
        var glowAlpha = alpha * 0.22f;
        if (glowAlpha > 0.01f)
        {
            var glow = Palette.WithAlpha(color, glowAlpha);
            for (var index = 0; index < GlowOffsets.Length; index++)
            {
                dl.AddText(font, fontSize, position + GlowOffsets[index] * (2f * scale), ImGui.GetColorU32(glow), glyph);
            }
        }

        dl.AddText(font, fontSize, position, ImGui.GetColorU32(Palette.WithAlpha(color, alpha)), glyph);
    }
}
