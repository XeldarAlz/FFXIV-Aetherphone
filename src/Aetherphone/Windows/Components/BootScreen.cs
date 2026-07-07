using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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

    private static readonly Vector2[] GlowOffsets =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f), new(1f, 1f), new(-1f, -1f),
    };

    public static void Draw(Rect screen, PhoneTheme theme, BootSequence boot)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rounding = theme.ScreenRounding * scale;
        var dl = ImGui.GetForegroundDrawList();
        if (boot.BackdropAlpha > 0f)
        {
            DrawBackdrop(dl, screen, theme, boot.BackdropAlpha, rounding);
        }

        if (boot.EmblemAlpha > 0f || boot.EmblemRingAlpha > 0f)
        {
            DrawEmblem(dl, screen.Center, theme, boot, scale);
        }

        if (boot.EmblemAlpha > 0.01f)
        {
            DrawLoadingCaption(dl, screen.Center, theme, boot, scale);
        }

        if (boot.Greeting is not null && boot.GreetingAlpha > 0f)
        {
            DrawGreeting(dl, screen.Center, theme, boot, scale);
        }
    }

    private static void DrawBackdrop(ImDrawListPtr dl, Rect screen, PhoneTheme theme, float alpha, float rounding)
    {
        var baseColor = new Vector4(0.015f, 0.019f, 0.038f, alpha);
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(baseColor), rounding);
        var breath = 0.8f + 0.2f * Pulse.Wave(5200);
        dl.PushClipRect(screen.Min, screen.Max, true);
        for (var ring = 3; ring >= 1; ring--)
        {
            var radius = screen.Height * (0.16f + ring * 0.13f);
            var glow = 0.055f / ring * alpha * breath;
            dl.AddCircleFilled(screen.Center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, glow)), 96);
        }

        dl.PopClipRect();
    }

    private static void DrawEmblem(ImDrawListPtr dl, Vector2 center, PhoneTheme theme, BootSequence boot, float scale)
    {
        var accent = theme.Accent;
        var alpha = boot.EmblemAlpha;
        var baseRadius = EmblemBaseRadius * scale * boot.EmblemScale;
        if (boot.EmblemRingAlpha > 0f)
        {
            var ringRadius = baseRadius * (1f + BootTiming.EmblemRingExpansion * boot.EmblemRingProgress);
            dl.AddCircle(center, ringRadius, ImGui.GetColorU32(Palette.WithAlpha(accent, boot.EmblemRingAlpha * 0.5f)),
                72, 2.2f * scale);
        }

        if (alpha <= 0f)
        {
            return;
        }

        dl.AddCircleFilled(center, baseRadius * 2.4f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.05f * alpha)), 64);
        LoadingPulse.Spinner(center, baseRadius, accent, alpha, dl);
        dl.AddCircleFilled(center, baseRadius * 0.15f, ImGui.GetColorU32(Palette.WithAlpha(Vector4.One, alpha * 0.9f)),
            32);
    }

    private static void DrawLoadingCaption(ImDrawListPtr dl, Vector2 center, PhoneTheme theme, BootSequence boot,
        float scale)
    {
        var alpha = boot.EmblemAlpha;
        var baseRadius = EmblemBaseRadius * scale * boot.EmblemScale;
        var caret = center.Y + baseRadius * 2.15f + 14f * scale;
        LoadingPulse.Caption(new Vector2(center.X, caret), theme.TextStrong, theme.Accent, LoadingPulse.SafeLabel(),
            alpha, CaptionFontScale, drawList: dl);
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

    private static void DrawGreeting(ImDrawListPtr dl, Vector2 center, PhoneTheme theme, BootSequence boot, float scale)
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
                    DrawGlyph(dl, font, fontSize, glyphs[index], new Vector2(penX, baseY + rise), theme.TextStrong,
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
