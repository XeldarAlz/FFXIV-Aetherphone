using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismArt
{
    public const float StripUnits = 46f;
    public const float ButtonUnits = 30f;
    public const int Stars = 5;

    private const float LabelUnits = 13f;
    private const float ScoreDigitUnits = 24f;
    private const float ScorePitchLow = 0.84f;
    private const float ScorePitchStep = 0.08f;
    private const float ButtonPadUnits = 12f;
    private const float StitchInsetUnits = 4f;
    private const float StitchDashUnits = 5f;
    private const float StoryBarUnits = 2f;
    private const float StoryBarSpacingUnits = 4f;
    private const float TagPadUnits = 8f;
    private const float TagInsetUnits = 10f;
    private const float ShadeShare = 0.78f;
    private const float ShadeSolidShare = 0.46f;
    private const float TopShadeUnits = 110f;
    private const float TopFadeShare = 0.34f;
    private const float TopFogDeep = 0.9f;
    private const float PressedAlpha = 0.82f;
    private const float ArrowNudge = 0.08f;
    private const double GlowPeriodSeconds = 4.5;
    private const double GlowSweepSeconds = 1.4;
    private const float GlowBandUnits = 140f;
    private const float GlowHaloUnits = 9f;
    private const float GlowSliceUnits = 3f;
    private const float GlowHaloAlpha = 0.55f;
    private const float GlowHaloLift = 0.25f;
    private const float GlowCoreLift = 0.7f;
    private const float FogAlpha = 0.6f;
    private const float EditorialMarginUnits = 20f;

    public static readonly Vector4 Bone = new(0.957f, 0.945f, 0.918f, 1f);
    public static readonly Vector4 Ink = new(0.067f, 0.067f, 0.067f, 1f);
    public static readonly Vector4 InkMuted = new(0.067f, 0.067f, 0.067f, 0.62f);

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ShadeClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 ShadeMid = new(0f, 0f, 0f, 0.72f);
    private static readonly Vector4 ShadeDeep = new(0f, 0f, 0f, 0.92f);
    private static readonly Vector4 Stitch = new(0.067f, 0.067f, 0.067f, 0.35f);

    private static readonly Inks PhotoInks = new(new Vector4(0f, 0f, 0f, 1f), White, new Vector4(1f, 1f, 1f, 0.66f),
        new Vector4(1f, 1f, 1f, 0.28f), Ink, new Vector4(0f, 0f, 0f, FogAlpha));

    private static Inks themeInks = PhotoInks;
    private static Inks inks = PhotoInks;
    private static float sidePadding = Metrics.Space.Lg;

    private readonly record struct Inks(Vector4 Back, Vector4 Fore, Vector4 Muted, Vector4 Faint, Vector4 OnFore,
        Vector4 Fog);

    public static Vector4 PhotoFore => White;

    public static Vector4 Back => inks.Back;

    public static Vector4 Fore => inks.Fore;

    public static Vector4 Muted => inks.Muted;

    public static Vector4 Faint => inks.Faint;

    public static void Follow(PhoneTheme theme)
    {
        themeInks = new Inks(theme.AppBackground, theme.TextStrong, theme.TextMuted, theme.Separator,
            theme.AppBackground, Palette.WithAlpha(theme.AppBackground, FogAlpha));
        inks = themeInks;
        sidePadding = theme.SidePadding;
    }

    public static void OverPhoto(bool photo) => inks = photo ? PhotoInks : themeInks;

    private static readonly Vector4[] Spectrum =
    {
        AccentRing.Red, AccentRing.Orange, AccentRing.Gold, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly string[] Digits = { "1", "2", "3", "4", "5" };

    public static float LabelPixels() => LabelUnits * UiScale.Current;

    public static float Gutter(float scale) => (EditorialMarginUnits - sidePadding) * scale;

    public static float BottomMargin(float scale) => Metrics.Space.Lg * scale;

    public static void Shade(ImDrawListPtr drawList, Rect screen, float scale)
    {
        var deep = ImGui.GetColorU32(Palette.WithAlpha(themeInks.Fog, TopFogDeep));
        var fog = ImGui.GetColorU32(themeInks.Fog);
        var clear = ImGui.GetColorU32(Palette.WithAlpha(themeInks.Fog, 0f));
        var band = screen.Min.Y + TopShadeUnits * scale;
        var lift = screen.Min.Y + screen.Height * TopFadeShare;
        drawList.AddRectFilledMultiColor(screen.Min, new Vector2(screen.Max.X, band), deep, deep, fog, fog);
        drawList.AddRectFilledMultiColor(new Vector2(screen.Min.X, band), new Vector2(screen.Max.X, lift), fog, fog,
            clear, clear);
        var fade = screen.Max.Y - screen.Height * ShadeShare;
        var settle = screen.Max.Y - screen.Height * ShadeSolidShare;
        drawList.AddRectFilledMultiColor(new Vector2(screen.Min.X, fade), new Vector2(screen.Max.X, settle),
            ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeMid),
            ImGui.GetColorU32(ShadeMid));
        drawList.AddRectFilledMultiColor(new Vector2(screen.Min.X, settle), screen.Max, ImGui.GetColorU32(ShadeMid),
            ImGui.GetColorU32(ShadeMid), ImGui.GetColorU32(ShadeDeep), ImGui.GetColorU32(ShadeDeep));
    }

    public static void Hairline(ImDrawListPtr drawList, float left, float right, float y, Vector4 color, float scale) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(color),
            MathF.Max(1f, Metrics.Stroke.Hairline * scale));

    public static float Programme(ImDrawListPtr drawList, Rect row, string left, string right, Vector4 color)
    {
        var pixels = LabelPixels();
        PrismType.Draw(drawList, row.Min, left, color, PrismFace.Label, pixels);
        if (right.Length > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(row.Max.X, row.Min.Y), right, color, PrismFace.Label, pixels);
        }

        return PrismType.LineHeight(PrismFace.Label, pixels);
    }

    public static bool Strip(ImDrawListPtr drawList, Rect rect, string left, string right, bool filled,
        bool enabled = true, UiSound sound = UiSound.Tap)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var alpha = enabled ? pressed ? PressedAlpha : 1f : Faint.W;
        var fill = Palette.WithAlpha(Fore, alpha);
        var text = filled ? inks.OnFore : Palette.WithAlpha(Fore, alpha);
        if (filled)
        {
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(fill));
        }
        else
        {
            drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(fill), 0f, ImDrawFlags.None, 1f);
        }

        var pixels = LabelPixels();
        var inset = rect.Height * 0.32f;
        var top = rect.Center.Y - pixels * 0.5f;
        PrismType.Draw(drawList, new Vector2(rect.Min.X + inset, top), left, text, PrismFace.Label, pixels);
        var room = rect.Width - inset * 3f - PrismType.Measure(left, PrismFace.Label, pixels).X;
        if (right.Length > 0 && PrismType.Measure(right, PrismFace.Label, pixels).X <= room)
        {
            var nudge = PressFx.Toward(ImGui.GetID(left), hovered ? 1f + ArrowNudge : 1f) - 1f;
            PrismType.DrawRight(drawList, new Vector2(rect.Max.X - inset + nudge * rect.Height, top), right, text,
                PrismFace.Label, pixels);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered, sound);
    }

    public static bool Link(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 color, float scale)
    {
        var pixels = LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, pixels);
        var pad = MathF.Max(0f, (Metrics.Size.TapTarget * scale - size.Y) * 0.5f);
        var min = new Vector2(topLeft.X, topLeft.Y - pad);
        var max = new Vector2(topLeft.X + size.X, topLeft.Y + size.Y + pad);
        var hovered = UiInteract.Hover(min, max);
        PrismType.Draw(drawList, topLeft, label, hovered ? Fore : color, PrismFace.Label, pixels);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static float ButtonWidth(string label, float scale) =>
        PrismType.Measure(label, PrismFace.Label, LabelPixels()).X + ButtonPadUnits * scale * 2f;

    public static bool Button(ImDrawListPtr drawList, Vector2 topLeft, string label, float scale, out float width)
    {
        var pixels = LabelPixels();
        var pad = ButtonPadUnits * scale;
        var height = ButtonUnits * scale;
        width = ButtonWidth(label, scale);
        var max = new Vector2(topLeft.X + width, topLeft.Y + height);
        var reach = MathF.Max(0f, (Metrics.Size.TapTarget * scale - height) * 0.5f);
        var hitMin = new Vector2(topLeft.X, topLeft.Y - reach);
        var hitMax = new Vector2(max.X, max.Y + reach);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        drawList.AddRect(topLeft, max, ImGui.GetColorU32(hovered ? Fore : Faint), 0f, ImDrawFlags.None, 1f);
        PrismType.Draw(drawList, new Vector2(topLeft.X + pad, topLeft.Y + (height - pixels) * 0.5f), label,
            hovered ? Fore : Muted, PrismFace.Label, pixels);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hitMin, hitMax, hovered);
    }

    public static int Score(ImDrawListPtr drawList, Rect row, int chosen, float fill, bool enabled, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var cellWidth = (row.Width - gap * (Stars - 1)) / Stars;
        var digitPixels = ScoreDigitUnits * scale;
        var picked = 0;
        for (var index = 0; index < Stars; index++)
        {
            var min = new Vector2(row.Min.X + index * (cellWidth + gap), row.Min.Y);
            var max = new Vector2(min.X + cellWidth, row.Max.Y);
            var hovered = enabled && UiInteract.Hover(min, max);
            var selected = chosen == index + 1;
            var share = selected ? fill : 0f;
            drawList.AddRect(min, max, ImGui.GetColorU32(hovered || selected ? Fore : Faint), 0f, ImDrawFlags.None,
                1f);
            if (share > 0f)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(Fore, share)));
            }

            var size = PrismType.Measure(Digits[index], PrismFace.Headline, digitPixels);
            PrismType.Draw(drawList, (min + max) * 0.5f - size * 0.5f, Digits[index], Vector4.Lerp(Fore, inks.OnFore, share),
                PrismFace.Headline, digitPixels);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (enabled && UiInteract.Click(min, max, hovered, (UiSound?)null))
            {
                UiFeedback.PlayPitched(UiSound.Tap, ScorePitchLow + ScorePitchStep * index);
                picked = index + 1;
            }
        }

        return picked;
    }

    public static void SpectrumRule(ImDrawListPtr drawList, float left, float right, float y, float scale)
    {
        var segment = (right - left) / Spectrum.Length;
        var height = MathF.Max(1f, 2f * scale);
        for (var index = 0; index < Spectrum.Length; index++)
        {
            var min = new Vector2(left + segment * index, y);
            drawList.AddRectFilled(min, new Vector2(min.X + segment, y + height), ImGui.GetColorU32(Spectrum[index]));
        }
    }

    public static void RuleGlow(ImDrawListPtr drawList, float left, float right, float y, float scale)
    {
        var phase = ImGui.GetTime() % GlowPeriodSeconds;
        if (phase >= GlowSweepSeconds)
        {
            return;
        }

        var progress = (float)(phase / GlowSweepSeconds);
        progress = progress * progress * (3f - 2f * progress);
        var half = GlowBandUnits * scale * 0.5f;
        var center = left - half + (right - left + half * 2f) * progress;
        var bottom = y + MathF.Max(1f, 2f * scale);
        var halo = GlowHaloUnits * scale;
        var slice = MathF.Max(1f, GlowSliceUnits * scale);
        var x = MathF.Max(left, center - half);
        var end = MathF.Min(right, center + half);
        var previous = GlowAt(x, left, right, center, half);
        while (x < end)
        {
            var next = MathF.Min(x + slice, end);
            var current = GlowAt(next, left, right, center, half);
            drawList.AddRectFilledMultiColor(new Vector2(x, y - halo), new Vector2(next, y), previous.Clear,
                current.Clear, current.Halo, previous.Halo);
            drawList.AddRectFilledMultiColor(new Vector2(x, bottom), new Vector2(next, bottom + halo), previous.Halo,
                current.Halo, current.Clear, previous.Clear);
            drawList.AddRectFilledMultiColor(new Vector2(x, y), new Vector2(next, bottom), previous.Core, current.Core,
                current.Core, previous.Core);
            previous = current;
            x = next;
        }
    }

    private static (uint Clear, uint Halo, uint Core) GlowAt(float x, float left, float right, float center,
        float half)
    {
        var distance = (x - center) / half;
        var falloff = MathF.Max(0f, 1f - distance * distance);
        falloff *= falloff;
        var tint = SpectrumAt((x - left) / MathF.Max(1f, right - left));
        var halo = Vector4.Lerp(tint, White, GlowHaloLift);
        return (ImGui.GetColorU32(Palette.WithAlpha(halo, 0f)),
            ImGui.GetColorU32(Palette.WithAlpha(halo, GlowHaloAlpha * falloff)),
            ImGui.GetColorU32(Palette.WithAlpha(Vector4.Lerp(tint, White, GlowCoreLift), falloff)));
    }

    private static Vector4 SpectrumAt(float share)
    {
        var position = Math.Clamp(share, 0f, 1f) * Spectrum.Length - 0.5f;
        var index = Math.Clamp((int)MathF.Floor(position), 0, Spectrum.Length - 1);
        var next = Math.Min(index + 1, Spectrum.Length - 1);
        return Vector4.Lerp(Spectrum[index], Spectrum[next], Math.Clamp(position - index, 0f, 1f));
    }

    public static void Refraction(ImDrawListPtr drawList, Rect screen, float progress)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var alpha = (1f - progress) * 0.45f;
        var band = screen.Width * 0.6f;
        var left = screen.Min.X - band + (screen.Width + band) * progress;
        var bar = band / Spectrum.Length;
        for (var index = 0; index < Spectrum.Length; index++)
        {
            var min = new Vector2(left + bar * index, screen.Min.Y);
            drawList.AddRectFilled(min, new Vector2(min.X + bar, screen.Max.Y),
                ImGui.GetColorU32(Palette.WithAlpha(Spectrum[index], alpha)));
        }
    }

    private static void DrawStitch(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var inset = StitchInsetUnits * scale;
        var dash = StitchDashUnits * scale;
        var color = ImGui.GetColorU32(Stitch);
        var top = rect.Min.Y + inset;
        var bottom = rect.Max.Y - inset;
        for (var x = rect.Min.X + inset; x < rect.Max.X - inset; x += dash * 2f)
        {
            var end = MathF.Min(x + dash, rect.Max.X - inset);
            drawList.AddLine(new Vector2(x, top), new Vector2(end, top), color);
            drawList.AddLine(new Vector2(x, bottom), new Vector2(end, bottom), color);
        }
    }

    public static void StoryBars(ImDrawListPtr drawList, float left, float right, float top, int count, int current,
        float fill, float scale)
    {
        var gap = StoryBarSpacingUnits * scale;
        var height = MathF.Max(1f, StoryBarUnits * scale);
        var width = (right - left - gap * (count - 1)) / count;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(left + index * (width + gap), top);
            drawList.AddRectFilled(min, new Vector2(min.X + width, top + height), ImGui.GetColorU32(Faint));
            var share = index < current ? 1f : index == current ? Math.Clamp(fill, 0f, 1f) : 0f;
            if (share > 0f)
            {
                drawList.AddRectFilled(min, new Vector2(min.X + width * share, top + height),
                    ImGui.GetColorU32(Fore));
            }
        }
    }

    public static Vector2 Tag(ImDrawListPtr drawList, Vector2 topRight, string label, float scale)
    {
        var pixels = LabelPixels();
        var pad = TagPadUnits * scale;
        var size = PrismType.Measure(label, PrismFace.Label, pixels) + new Vector2(pad * 2f, pad * 2f);
        var inset = TagInsetUnits * scale;
        var min = new Vector2(topRight.X - inset - size.X, topRight.Y + inset);
        var rect = new Rect(min, min + size);
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Bone));
        DrawStitch(drawList, rect, scale);
        PrismType.Draw(drawList, min + new Vector2(pad, pad), label, Ink, PrismFace.Label, pixels);
        return rect.Center;
    }
}
