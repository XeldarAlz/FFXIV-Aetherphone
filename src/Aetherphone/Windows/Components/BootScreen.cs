using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class BootScreen
{
    private const float GreetingFontScale = 1.9f;
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
        var dl = ImGui.GetWindowDrawList();
        if (boot.BackdropAlpha > 0f)
        {
            var backdrop = new Vector4(0f, 0f, 0f, boot.BackdropAlpha);
            dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(backdrop), rounding);
        }

        if (boot.EmblemAlpha > 0f || boot.EmblemRingAlpha > 0f)
        {
            DrawEmblem(screen.Center, theme, boot, scale);
        }

        if (boot.Greeting is not null && boot.GreetingAlpha > 0f)
        {
            DrawGreeting(screen.Center, theme, boot, scale);
        }
    }

    private static void DrawEmblem(Vector2 center, PhoneTheme theme, BootSequence boot, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var accent = theme.Accent;
        var alpha = boot.EmblemAlpha;
        var baseRadius = 30f * scale * boot.EmblemScale;
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

        dl.AddCircleFilled(center, baseRadius * 2.4f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.06f * alpha)), 64);
        dl.AddCircleFilled(center, baseRadius * 1.7f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.10f * alpha)), 64);
        dl.AddCircleFilled(center, baseRadius * 1.15f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f * alpha)), 64);
        dl.AddCircle(center, baseRadius, ImGui.GetColorU32(Palette.WithAlpha(accent, alpha)), 72, 3.0f * scale);
        var core = Palette.Mix(accent, Vector4.One, 0.7f);
        dl.AddCircleFilled(center, baseRadius * 0.32f, ImGui.GetColorU32(Palette.WithAlpha(core, alpha)), 48);
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

    private static void DrawGreeting(Vector2 center, PhoneTheme theme, BootSequence boot, float scale)
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
                    Easing.EaseOutCubic(Clamp01((boot.GreetingReveal - letterStart) / LetterRevealSpan));
                var letterAlpha = letterProgress * boot.GreetingAlpha;
                if (letterAlpha > 0.01f)
                {
                    var rise = (1f - letterProgress) * LetterRisePixels * scale;
                    DrawGlyph(glyphs[index], new Vector2(penX, baseY + rise), theme.TextStrong, letterAlpha, scale);
                }

                penX += widths[index];
            }
        }
    }

    private static void DrawGlyph(string glyph, Vector2 position, Vector4 color, float alpha, float scale)
    {
        var glowAlpha = alpha * 0.22f;
        if (glowAlpha > 0.01f)
        {
            var glow = Palette.WithAlpha(color, glowAlpha);
            for (var index = 0; index < GlowOffsets.Length; index++)
            {
                DrawGlyphPass(glyph, position + GlowOffsets[index] * (2f * scale), glow);
            }
        }

        DrawGlyphPass(glyph, position, Palette.WithAlpha(color, alpha));
    }

    private static void DrawGlyphPass(string glyph, Vector2 position, Vector4 color)
    {
        ImGui.SetCursorScreenPos(position);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextUnformatted(glyph);
        }
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
