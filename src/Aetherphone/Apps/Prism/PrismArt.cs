using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismArt
{
    public const float StripUnits = 48f;
    public const float GutterUnits = 14f;
    public const int Stars = 5;

    private const float MonoRatio = 0.82f;
    private const float LabelDigitUnits = 26f;
    private const float LabelPadUnits = 12f;
    private const float StitchInsetUnits = 4f;
    private const float StitchDashUnits = 5f;
    private const float CircleStroke = 1.6f;
    private const float CircleWobble = 0.08f;
    private const float CircleStart = -2.2f;
    private const int CircleSegments = 40;
    private const float ShadeShare = 0.62f;
    private const float TopShadeUnits = 96f;
    private const float PressedAlpha = 0.82f;
    private const float PreviewAlpha = 0.32f;

    public static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Bone = new(0.957f, 0.945f, 0.918f, 1f);
    public static readonly Vector4 Ink = new(0.067f, 0.067f, 0.067f, 1f);
    public static readonly Vector4 Muted = new(1f, 1f, 1f, 0.66f);
    public static readonly Vector4 Faint = new(1f, 1f, 1f, 0.28f);
    public static readonly Vector4 InkMuted = new(0.067f, 0.067f, 0.067f, 0.62f);

    private static readonly Vector4 ShadeClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 ShadeDeep = new(0f, 0f, 0f, 0.82f);
    private static readonly Vector4 ShadeTop = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 Stitch = new(0.067f, 0.067f, 0.067f, 0.35f);

    private static readonly Vector4[] Spectrum =
    {
        AccentRing.Red, AccentRing.Orange, AccentRing.Gold, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly string[] Digits = { "1", "2", "3", "4", "5" };

    public static float MonoPixels() => Typography.LineHeight(TextStyles.Caption1) * MonoRatio;

    public static float Gutter(float scale) => GutterUnits * scale;

    public static void Shade(ImDrawListPtr drawList, Rect screen, float scale)
    {
        var top = new Vector2(screen.Max.X, screen.Min.Y + TopShadeUnits * scale);
        drawList.AddRectFilledMultiColor(screen.Min, top, ImGui.GetColorU32(ShadeTop), ImGui.GetColorU32(ShadeTop),
            ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeClear));
        var bottom = new Vector2(screen.Min.X, screen.Max.Y - screen.Height * ShadeShare);
        drawList.AddRectFilledMultiColor(bottom, screen.Max, ImGui.GetColorU32(ShadeClear),
            ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeDeep), ImGui.GetColorU32(ShadeDeep));
    }

    public static void Hairline(ImDrawListPtr drawList, float left, float right, float y, Vector4 color, float scale) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(color),
            MathF.Max(1f, Metrics.Stroke.Hairline * scale));

    public static float Programme(ImDrawListPtr drawList, Rect row, string left, string right, Vector4 color)
    {
        var pixels = MonoPixels();
        PrismType.Draw(drawList, row.Min, left, color, EditorialFace.Mono, pixels);
        if (right.Length > 0)
        {
            PrismType.DrawRight(drawList, new Vector2(row.Max.X, row.Min.Y), right, color, EditorialFace.Mono, pixels);
        }

        return PrismType.LineHeight(EditorialFace.Mono, pixels);
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

        var pixels = MonoPixels();
        var inset = rect.Height * 0.32f;
        var top = rect.Center.Y - pixels * 0.5f;
        PrismType.Draw(drawList, new Vector2(rect.Min.X + inset, top), left, text, EditorialFace.Mono, pixels);
        var room = rect.Width - inset * 3f - PrismType.Measure(left, EditorialFace.Mono, pixels).X;
        if (right.Length > 0 && PrismType.Measure(right, EditorialFace.Mono, pixels).X <= room)
        {
            PrismType.DrawRight(drawList, new Vector2(rect.Max.X - inset, top), right, text, EditorialFace.Mono,
                pixels);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered, sound);
    }

    public static bool Link(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 color, float scale)
    {
        var pixels = MonoPixels();
        var size = PrismType.Measure(label, EditorialFace.Mono, pixels);
        var pad = MathF.Max(0f, (Metrics.Size.TapTarget * scale - size.Y) * 0.5f);
        var min = new Vector2(topLeft.X, topLeft.Y - pad);
        var max = new Vector2(topLeft.X + size.X, topLeft.Y + size.Y + pad);
        var hovered = UiInteract.Hover(min, max);
        PrismType.Draw(drawList, topLeft, label, hovered ? White : color, EditorialFace.Mono, pixels);
        Hairline(drawList, topLeft.X, topLeft.X + size.X, topLeft.Y + size.Y + 2f * scale, hovered ? White : color,
            scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static float LabelHeight(float scale) =>
        LabelPadUnits * 2f * scale + MonoPixels() * 1.6f + Metrics.Size.TapTarget * scale;

    public static int Label(ImDrawListPtr drawList, Rect rect, string prompt, int chosen, float circle, bool enabled,
        float scale)
    {
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Bone));
        DrawStitch(drawList, rect, scale);
        var pad = LabelPadUnits * scale;
        var pixels = MonoPixels();
        PrismType.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + pad), prompt, InkMuted,
            EditorialFace.Mono, pixels);
        var cellTop = rect.Max.Y - pad - Metrics.Size.TapTarget * scale;
        var cellWidth = (rect.Width - pad * 2f) / Stars;
        var digitPixels = LabelDigitUnits * scale;
        var picked = 0;
        for (var index = 0; index < Stars; index++)
        {
            var min = new Vector2(rect.Min.X + pad + cellWidth * index, cellTop);
            var max = new Vector2(min.X + cellWidth, cellTop + Metrics.Size.TapTarget * scale);
            var center = (min + max) * 0.5f;
            var hovered = enabled && UiInteract.Hover(min, max);
            var size = PrismType.Measure(Digits[index], EditorialFace.Headline, digitPixels);
            PrismType.Draw(drawList, center - size * 0.5f, Digits[index], Ink, EditorialFace.Headline, digitPixels);
            var radius = MathF.Min(cellWidth, max.Y - min.Y) * 0.42f;
            if (chosen == index + 1)
            {
                DrawCircle(drawList, center, radius, circle, Ink, scale);
            }
            else if (hovered && chosen == 0)
            {
                DrawCircle(drawList, center, radius, 1f, Palette.WithAlpha(Ink, PreviewAlpha), scale);
            }

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

    private static void DrawCircle(ImDrawListPtr drawList, Vector2 center, float radius, float progress,
        Vector4 color, float scale)
    {
        if (progress <= 0f)
        {
            return;
        }

        var sweep = MathF.Tau * 1.08f * MathF.Min(1f, progress);
        for (var index = 0; index <= CircleSegments; index++)
        {
            var angle = CircleStart + sweep * index / CircleSegments;
            var wobble = 1f + CircleWobble * MathF.Sin(angle * 2f);
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radius * wobble * 1.12f,
                MathF.Sin(angle) * radius * wobble));
        }

        drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.None, CircleStroke * scale);
    }
}
