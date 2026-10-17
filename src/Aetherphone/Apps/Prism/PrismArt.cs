using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismArt
{
    public const float StripUnits = 46f;
    public const float GutterUnits = 20f;
    public const float ButtonUnits = 30f;
    public const int Stars = 5;

    private const float LabelRatio = 0.82f;
    private const float ScoreDigitUnits = 24f;
    private const float ButtonPadUnits = 12f;
    private const float StitchInsetUnits = 4f;
    private const float StitchDashUnits = 5f;
    private const float StoryBarUnits = 2f;
    private const float StoryBarSpacingUnits = 4f;
    private const float TagPadUnits = 8f;
    private const float TagInsetUnits = 10f;
    private const float ShadeShare = 0.78f;
    private const float ShadeSolidShare = 0.46f;
    private const float TopShadeUnits = 96f;
    private const float PressedAlpha = 0.82f;
    private const float ArrowNudge = 0.08f;
    private const double GlowPeriodSeconds = 4.5;
    private const double GlowSweepSeconds = 1.4;
    private const float GlowBandUnits = 72f;
    private const float GlowHaloUnits = 6f;

    public static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Bone = new(0.957f, 0.945f, 0.918f, 1f);
    public static readonly Vector4 Ink = new(0.067f, 0.067f, 0.067f, 1f);
    public static readonly Vector4 Muted = new(1f, 1f, 1f, 0.66f);
    public static readonly Vector4 Faint = new(1f, 1f, 1f, 0.28f);
    public static readonly Vector4 InkMuted = new(0.067f, 0.067f, 0.067f, 0.62f);

    private static readonly Vector4 ShadeClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 ShadeMid = new(0f, 0f, 0f, 0.72f);
    private static readonly Vector4 ShadeDeep = new(0f, 0f, 0f, 0.92f);
    private static readonly Vector4 ShadeTop = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 Stitch = new(0.067f, 0.067f, 0.067f, 0.35f);
    private static readonly Vector4 GlowCore = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 GlowHalo = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 GlowClear = new(1f, 1f, 1f, 0f);

    private static readonly Vector4[] Spectrum =
    {
        AccentRing.Red, AccentRing.Orange, AccentRing.Gold, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly string[] Digits = { "1", "2", "3", "4", "5" };

    public static float LabelPixels() => Typography.LineHeight(TextStyles.Caption1) * LabelRatio;

    public static float Gutter(float scale) => GutterUnits * scale;

    public static void Shade(ImDrawListPtr drawList, Rect screen, float scale)
    {
        var top = new Vector2(screen.Max.X, screen.Min.Y + TopShadeUnits * scale);
        drawList.AddRectFilledMultiColor(screen.Min, top, ImGui.GetColorU32(ShadeTop), ImGui.GetColorU32(ShadeTop),
            ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeClear));
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
        PrismType.Draw(drawList, row.Min, left, color, EditorialFace.Label, pixels);
        if (right.Length > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(row.Max.X, row.Min.Y), right, color, EditorialFace.Label, pixels);
        }

        return PrismType.LineHeight(EditorialFace.Label, pixels);
    }

    public static bool Strip(ImDrawListPtr drawList, Rect rect, string left, string right, bool filled,
        bool enabled = true, UiSound sound = UiSound.Tap)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var alpha = enabled ? pressed ? PressedAlpha : 1f : Faint.W;
        var fill = Palette.WithAlpha(White, alpha);
        var text = filled ? Ink : Palette.WithAlpha(White, alpha);
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
        PrismType.Draw(drawList, new Vector2(rect.Min.X + inset, top), left, text, EditorialFace.Label, pixels);
        var room = rect.Width - inset * 3f - PrismType.Measure(left, EditorialFace.Label, pixels).X;
        if (right.Length > 0 && PrismType.Measure(right, EditorialFace.Label, pixels).X <= room)
        {
            var nudge = PressFx.Toward(ImGui.GetID(left), hovered ? 1f + ArrowNudge : 1f) - 1f;
            PrismType.DrawRight(drawList, new Vector2(rect.Max.X - inset + nudge * rect.Height, top), right, text,
                EditorialFace.Label, pixels);
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
        var size = PrismType.Measure(label, EditorialFace.Label, pixels);
        var pad = MathF.Max(0f, (Metrics.Size.TapTarget * scale - size.Y) * 0.5f);
        var min = new Vector2(topLeft.X, topLeft.Y - pad);
        var max = new Vector2(topLeft.X + size.X, topLeft.Y + size.Y + pad);
        var hovered = UiInteract.Hover(min, max);
        PrismType.Draw(drawList, topLeft, label, hovered ? White : color, EditorialFace.Label, pixels);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static float ButtonWidth(string label, float scale) =>
        PrismType.Measure(label, EditorialFace.Label, LabelPixels()).X + ButtonPadUnits * scale * 2f;

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
        drawList.AddRect(topLeft, max, ImGui.GetColorU32(hovered ? White : Faint), 0f, ImDrawFlags.None, 1f);
        PrismType.Draw(drawList, new Vector2(topLeft.X + pad, topLeft.Y + (height - pixels) * 0.5f), label,
            hovered ? White : Muted, EditorialFace.Label, pixels);
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
            drawList.AddRect(min, max, ImGui.GetColorU32(hovered || selected ? White : Faint), 0f, ImDrawFlags.None,
                1f);
            if (share > 0f)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(White, share)));
            }

            var size = PrismType.Measure(Digits[index], EditorialFace.Headline, digitPixels);
            PrismType.Draw(drawList, (min + max) * 0.5f - size * 0.5f, Digits[index], Vector4.Lerp(White, Ink, share),
                EditorialFace.Headline, digitPixels);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (enabled && UiInteract.Click(min, max, hovered))
            {
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
        var band = GlowBandUnits * scale;
        var center = left - band + (right - left + band * 2f) * progress;
        var height = MathF.Max(1f, 2f * scale);
        var halo = GlowHaloUnits * scale;
        drawList.PushClipRect(new Vector2(left, y - halo), new Vector2(right, y + height + halo), true);
        DrawGlowBand(drawList, center, band, y - halo, y + height + halo, GlowHalo);
        DrawGlowBand(drawList, center, band * 0.5f, y, y + height, GlowCore);
        drawList.PopClipRect();
    }

    private static void DrawGlowBand(ImDrawListPtr drawList, float center, float half, float top, float bottom,
        Vector4 color)
    {
        var clear = ImGui.GetColorU32(GlowClear);
        var lit = ImGui.GetColorU32(color);
        drawList.AddRectFilledMultiColor(new Vector2(center - half, top), new Vector2(center, bottom), clear, lit, lit,
            clear);
        drawList.AddRectFilledMultiColor(new Vector2(center, top), new Vector2(center + half, bottom), lit, clear,
            clear, lit);
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
                    ImGui.GetColorU32(White));
            }
        }
    }

    public static Vector2 Tag(ImDrawListPtr drawList, Vector2 topRight, string label, float scale)
    {
        var pixels = LabelPixels();
        var pad = TagPadUnits * scale;
        var size = PrismType.Measure(label, EditorialFace.Label, pixels) + new Vector2(pad * 2f, pad * 2f);
        var inset = TagInsetUnits * scale;
        var min = new Vector2(topRight.X - inset - size.X, topRight.Y + inset);
        var rect = new Rect(min, min + size);
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Bone));
        DrawStitch(drawList, rect, scale);
        PrismType.Draw(drawList, min + new Vector2(pad, pad), label, Ink, EditorialFace.Label, pixels);
        return rect.Center;
    }
}
