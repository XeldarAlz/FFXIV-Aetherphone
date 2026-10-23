using System.Diagnostics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum PrismFact : byte
{
    Modded,
    Vanilla,
    Race,
    Male,
    Female,
    FitsMe,
}

internal static class PrismArt
{
    public const float StripUnits = 46f;
    public const float ButtonUnits = 30f;
    public const float IconUnits = 24f;
    public const float HeaderIconUnits = Metrics.Size.TapTarget * 0.5f * RoundButton.GlyphFraction;
    public const float GlassIconUnits = 18f;
    public const float RowIconUnits = 16f;
    public const float InlineIconUnits = 14f;
    public const float EmptyIconUnits = 28f;
    public const int Stars = 5;
    public const int PodiumRanks = 3;
    public const float TryOnSeconds = 1.45f;
    public const float ChipUnits = 28f;

    private const float IconDotUnits = 4f;
    private const float IconDotRimUnits = 1.5f;
    private const float IconDotShare = 0.4f;

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
    private const float GlowSweepSeconds = 1.4f;
    private const float GlowBandUnits = 140f;
    private const float GlowHaloUnits = 9f;
    private const float GlowSliceUnits = 3f;
    private const float GlowHaloAlpha = 0.55f;
    private const float GlowHaloLift = 0.25f;
    private const float GlowCoreLift = 0.7f;
    private const float FogAlpha = 0.6f;
    private const float EditorialMarginUnits = 20f;
    private const float SwatchBase = 0.06f;
    private const float SwatchStep = 0.04f;
    private const int SwatchTones = 3;
    private const float SwatchFloor = 0.02f;
    private const float MedalFill = 0.12f;
    private const int MedalSegments = 48;
    private const float SwatchShadeShare = 0.45f;
    private const float ScrimShare = 0.35f;
    private const float FailedGlyphShare = 0.3f;
    private const float TryOnHoldSeconds = 0.9f;
    private const float TryOnDim = 0.45f;
    private const float TryOnBandShare = 0.35f;
    private const float TryOnBandPeak = 0.3f;
    private const float RefractionBandShare = 0.6f;
    private const float RefractionBandPeak = 0.45f;
    private const float IconStrokeUnits = 1.8f;
    private const int IconMaxPoints = 64;
    private const int IconCapSegments = 6;
    private const float ArcStepDegrees = 22.5f;
    private const float TileIconShare = 0.58f;
    private const float ToggleRestAlpha = 0.7f;

    public static readonly Vector4 Bone = new(0.957f, 0.945f, 0.918f, 1f);
    public static readonly Vector4 Ink = new(0.067f, 0.067f, 0.067f, 1f);
    public static readonly Vector4 InkMuted = new(0.067f, 0.067f, 0.067f, 0.62f);

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ShadeClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 ShadeMid = new(0f, 0f, 0f, 0.72f);
    private static readonly Vector4 ShadeDeep = new(0f, 0f, 0f, 0.92f);
    private static readonly Vector4 ScrimMid = new(0f, 0f, 0f, 0.4f);
    private static readonly Vector4 ScrimDeep = new(0f, 0f, 0f, 0.7f);
    private static readonly Vector4 ChipFill = new(0.07f, 0.07f, 0.08f, 0.55f);
    private static readonly Vector4 ChipHover = new(0.07f, 0.07f, 0.08f, 0.7f);
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

    public static Vector4 OnFore => inks.OnFore;

    public static Vector4 PhotoMuted => PhotoInks.Muted;

    public static bool Light => Palette.Luminance(themeInks.Back) >= 0.5f;

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

    private static readonly uint[] SpectrumPacked = PackAll(Spectrum);

    private static readonly string[] Digits = { "1", "2", "3", "4", "5" };

    private static readonly IconStroke[][] SlotIcons = BuildSlotIcons();
    private static readonly IconStroke[][] FactIcons = BuildFactIcons();
    private static readonly FactFill[] FactFills =
    {
        new(2, false, true), new(1, true, false), new(2, false, false), new(1, false, false), new(1, false, false),
        new(2, false, false),
    };
    private static readonly float[] BandShape = BuildBandShape();
    private static string momentLabel = string.Empty;
    private static string momentFitted = string.Empty;
    private static float momentRoom;
    private static float momentWidth;
    private static int momentFonts;

    public static float LabelPixels() => LabelUnits * UiScale.Current;

    public static float Gutter(float scale) => (EditorialMarginUnits - sidePadding) * scale;

    public static float BottomMargin(float scale) => Metrics.Space.Lg * scale;

    public static void Shade(ImDrawListPtr drawList, Rect screen, float scale, float headBottom = 0f)
    {
        var deep = ImGui.GetColorU32(Palette.WithAlpha(PhotoInks.Fog, TopFogDeep));
        var fog = ImGui.GetColorU32(PhotoInks.Fog);
        var clear = ImGui.GetColorU32(Palette.WithAlpha(PhotoInks.Fog, 0f));
        var band = MathF.Max(screen.Min.Y + TopShadeUnits * scale, headBottom + Metrics.Space.Lg * scale);
        var lift = MathF.Max(screen.Min.Y + screen.Height * TopFadeShare, band + (band - screen.Min.Y) * 0.5f);
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

    public static float HairlineWidth(float scale) => MathF.Max(1f, Metrics.Stroke.Hairline * scale);

    public static void Hairline(ImDrawListPtr drawList, float left, float right, float y, Vector4 color, float scale) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(color), HairlineWidth(scale));

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
            drawList.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(fill), 0f, ImDrawFlags.None,
                HairlineWidth(UiScale.Current));
        }

        var pixels = LabelPixels();
        var inset = rect.Height * 0.32f;
        var top = rect.Center.Y - pixels * 0.5f;
        var label = PrismType.FitLabel(left, pixels, rect.Width - inset * 2f);
        PrismType.Draw(drawList, new Vector2(rect.Min.X + inset, top), label, text, PrismFace.Label, pixels);
        var room = rect.Width - inset * 3f - PrismType.Measure(label, PrismFace.Label, pixels).X;
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

    public static bool Link(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 color, float scale,
        float rowHeight = 0f, string icon = "", bool overlay = false)
    {
        var pixels = LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, pixels);
        var lead = icon.Length > 0 ? size.Y + Metrics.Space.Xs * scale : 0f;
        var tap = Metrics.Size.TapTarget * scale;
        var padY = MathF.Max(0f, ((rowHeight > 0f ? rowHeight : tap) - size.Y) * 0.5f);
        var padX = MathF.Max(0f, (tap - size.X - lead) * 0.5f);
        var min = new Vector2(topLeft.X - padX, topLeft.Y - padY);
        var max = new Vector2(topLeft.X + lead + size.X + padX, topLeft.Y + size.Y + padY);
        var hovered = overlay
            ? !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max)
            : UiInteract.Hover(min, max);
        if (lead > 0f)
        {
            PhoneIcon.Draw(drawList, topLeft + new Vector2(size.Y * 0.5f, size.Y * 0.5f), icon, hovered ? Fore : color,
                size.Y);
        }

        PrismType.Draw(drawList, topLeft + new Vector2(lead, 0f), label, hovered ? Fore : color, PrismFace.Label, pixels);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static bool Icon(ImDrawListPtr drawList, Vector2 center, string glyph, bool dot, float scale)
    {
        var size = HeaderIconUnits * scale;
        var half = MathF.Max(size, Metrics.Size.TapTarget * scale) * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = UiInteract.Hover(min, max);
        PhoneIcon.Draw(drawList, center, glyph, hovered ? Muted : Fore, size);
        if (dot)
        {
            var dotCenter = center + new Vector2(size, -size) * IconDotShare;
            drawList.AddCircleFilled(dotCenter, (IconDotUnits + IconDotRimUnits) * scale, ImGui.GetColorU32(Back));
            drawList.AddCircleFilled(dotCenter, IconDotUnits * scale, ImGui.GetColorU32(Fore));
        }

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
        drawList.AddRect(topLeft, max, ImGui.GetColorU32(hovered ? Fore : Faint), 0f, ImDrawFlags.None,
            HairlineWidth(scale));
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
                HairlineWidth(scale));
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

    public static void Swatch(ImDrawListPtr drawList, Rect rect, int tint)
    {
        var from = ImGui.GetColorU32(Vector4.Lerp(PhotoInks.Back, PhotoInks.Fore,
            SwatchBase + SwatchStep * (tint % SwatchTones)));
        var to = ImGui.GetColorU32(Vector4.Lerp(PhotoInks.Back, PhotoInks.Fore, SwatchFloor));
        drawList.AddRectFilledMultiColor(rect.Min, rect.Max, from, from, to, to);
        var fade = new Vector2(rect.Min.X, rect.Max.Y - rect.Height * SwatchShadeShare);
        drawList.AddRectFilledMultiColor(fade, rect.Max, ImGui.GetColorU32(ShadeClear), ImGui.GetColorU32(ShadeClear),
            ImGui.GetColorU32(ShadeMid), ImGui.GetColorU32(ShadeMid));
    }

    public static void Medal(ImDrawListPtr drawList, Vector2 center, float radius, int rank, float turn, float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Vector4.Lerp(Back, Fore, MedalFill)), MedalSegments);
        if (rank is >= 1 and <= PodiumRanks)
        {
            SpectrumRing(drawList, center, radius, turn, MathF.Max(1f, 2f * scale));
            return;
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(Faint), MedalSegments, HairlineWidth(scale));
    }

    public static void SpectrumRing(ImDrawListPtr drawList, Vector2 center, float radius, float turn, float thickness)
    {
        var step = MathF.PI * 2f / Spectrum.Length;
        for (var index = 0; index < Spectrum.Length; index++)
        {
            var start = turn - MathF.PI * 0.5f + step * index;
            drawList.PathArcTo(center, radius, start, start + step, MedalSegments / Spectrum.Length);
            drawList.PathStroke(ImGui.GetColorU32(Spectrum[index]), ImDrawFlags.None, thickness);
        }
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

    public static void RuleGlow(ImDrawListPtr drawList, float left, float right, float y, float scale, float age)
    {
        if (age < 0f || age >= GlowSweepSeconds)
        {
            return;
        }

        var progress = age / GlowSweepSeconds;
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

    public static void Refraction(ImDrawListPtr drawList, Rect screen, float progress) =>
        Bands(drawList, screen, progress, RefractionBandShare, RefractionBandPeak);

    private static void Bands(ImDrawListPtr drawList, Rect screen, float progress, float share, float peak)
    {
        if (progress <= 0f || progress >= PrismMotion.Settled)
        {
            return;
        }

        var alpha = (1f - progress) * peak;
        var band = screen.Width * share;
        var left = screen.Min.X - band + (screen.Width + band) * progress;
        var bar = band / Spectrum.Length;
        drawList.PushClipRect(screen.Min, screen.Max, true);
        for (var index = 0; index < Spectrum.Length; index++)
        {
            var min = new Vector2(left + bar * index, screen.Min.Y);
            var max = new Vector2(min.X + bar, screen.Max.Y);
            if (max.X <= screen.Min.X || min.X >= screen.Max.X)
            {
                continue;
            }

            var from = BandColor(index, alpha);
            var to = BandColor(index + 1, alpha);
            drawList.AddRectFilledMultiColor(min, max, from, to, to, from);
        }

        drawList.PopClipRect();
    }

    private static uint BandColor(int stop, float alpha) =>
        (SpectrumPacked[Math.Min(stop, Spectrum.Length - 1)] & 0x00FFFFFFu) |
        ((uint)(alpha * BandShape[stop] * 255f + 0.5f) << 24);

    private static float[] BuildBandShape()
    {
        var shape = new float[Spectrum.Length + 1];
        for (var index = 0; index < shape.Length; index++)
        {
            shape[index] = MathF.Sin(MathF.PI * index / Spectrum.Length);
        }

        return shape;
    }

    public static void Placeholder(ImDrawListPtr drawList, Rect rect, bool failed, float scale, float opacity = 1f)
    {
        var fill = Surfaces.Fill(themeInks.Fore, FillLevel.Tertiary);
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(fill with { W = fill.W * opacity }));
        if (failed)
        {
            PhoneIcon.Draw(drawList, rect.Center, PhoneIcons.Photo,
                themeInks.Muted with { W = themeInks.Muted.W * opacity },
                MathF.Min(IconUnits * scale, rect.Width * FailedGlyphShare));
        }
    }

    private static void DrawStitch(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var inset = StitchInsetUnits * scale;
        var dash = StitchDashUnits * scale;
        var color = ImGui.GetColorU32(Stitch);
        var thickness = HairlineWidth(scale);
        var top = rect.Min.Y + inset;
        var bottom = rect.Max.Y - inset;
        for (var x = rect.Min.X + inset; x < rect.Max.X - inset; x += dash * 2f)
        {
            var end = MathF.Min(x + dash, rect.Max.X - inset);
            drawList.AddLine(new Vector2(x, top), new Vector2(end, top), color, thickness);
            drawList.AddLine(new Vector2(x, bottom), new Vector2(end, bottom), color, thickness);
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

    public static void Scrim(ImDrawListPtr drawList, Rect photo, float textHeight, float scale)
    {
        var carry = MathF.Min(photo.Height, textHeight + Metrics.Space.Xl * scale);
        var textTop = photo.Max.Y - carry;
        var fade = MathF.Max(photo.Height * ScrimShare - carry, Metrics.Space.Xl * scale);
        var top = MathF.Max(photo.Min.Y, textTop - fade);
        var clear = ImGui.GetColorU32(ShadeClear);
        var middle = ImGui.GetColorU32(ScrimMid);
        var deep = ImGui.GetColorU32(ScrimDeep);
        drawList.AddRectFilledMultiColor(new Vector2(photo.Min.X, top), new Vector2(photo.Max.X, textTop), clear, clear,
            middle, middle);
        drawList.AddRectFilledMultiColor(new Vector2(photo.Min.X, textTop), photo.Max, middle, middle, deep, deep);
    }

    public static void TopFog(ImDrawListPtr drawList, Rect photo, float scale)
    {
        var fog = ImGui.GetColorU32(PhotoInks.Fog);
        var clear = ImGui.GetColorU32(ShadeClear);
        drawList.AddRectFilledMultiColor(photo.Min,
            new Vector2(photo.Max.X, MathF.Min(photo.Max.Y, photo.Min.Y + TopShadeUnits * scale)), fog, fog, clear,
            clear);
    }

    public static bool PhotoChip(ImDrawListPtr drawList, Vector2 bottomLeft, float maxWidth, string glyph,
        string label, bool selected, float scale)
    {
        var half = ChipUnits * scale * 0.5f;
        var chip = PillRect(new Vector2(bottomLeft.X, bottomLeft.Y - half), maxWidth, label, scale, out var fitted);
        var reach = MathF.Max(half, Metrics.Size.TapTarget * scale * 0.5f) - half;
        var hit = new Rect(new Vector2(chip.Min.X, chip.Min.Y - reach), new Vector2(chip.Max.X, chip.Max.Y + reach));
        var hovered = UiInteract.HoverOverlay(hit);
        PaintPill(drawList, chip, glyph, fitted, selected ? White : hovered ? ChipHover : ChipFill,
            selected ? Ink : White, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hit.Min, hit.Max, hovered);
    }

    public static void TryOnMoment(ImDrawListPtr drawList, Rect photo, string glyph, string label, float age,
        float scale)
    {
        if (age <= 0f || age >= TryOnSeconds)
        {
            return;
        }

        var sweep = PrismMotion.At(age, 0f, Motion.Sheet);
        var show = sweep * (1f - PrismMotion.At(age, TryOnHoldSeconds, Motion.Appear));
        drawList.AddRectFilled(photo.Min, photo.Max, Palette.Pack(ShadeClear with { W = TryOnDim * show }));
        Bands(drawList, photo, sweep, TryOnBandShare, TryOnBandPeak);
        var line = Typography.LineHeight(PrismType.Headline);
        var gap = Metrics.Space.Xs * scale;
        var room = photo.Width - Metrics.Space.Lg * scale * 2f - line - gap;
        if (!ReferenceEquals(label, momentLabel) || room != momentRoom || Plugin.Fonts.Generation != momentFonts)
        {
            momentLabel = label;
            momentRoom = room;
            momentFonts = Plugin.Fonts.Generation;
            momentFitted = Typography.FitText(label, MathF.Max(1f, room), PrismType.Headline);
            momentWidth = Typography.Measure(momentFitted, PrismType.Headline).X;
        }

        var ink = White with { W = show };
        var left = photo.Center.X - (line + gap + momentWidth) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(left + line * 0.5f, photo.Center.Y), glyph, ink, line);
        Typography.Draw(drawList, new Vector2(left + line + gap, photo.Center.Y - line * 0.5f), momentFitted, ink,
            PrismType.Headline);
    }

    private static Rect PillRect(Vector2 anchor, float maxWidth, string label, float scale, out string fitted)
    {
        var pixels = LabelPixels();
        var chrome = (Metrics.Space.Md * 2f + InlineIconUnits + Metrics.Space.Xxs) * scale;
        fitted = PrismType.FitLabel(label, pixels, MathF.Max(1f, maxWidth - chrome));
        var width = chrome + PrismType.Measure(fitted, PrismFace.Label, pixels).X;
        var half = ChipUnits * scale * 0.5f;
        return new Rect(new Vector2(anchor.X, anchor.Y - half), new Vector2(anchor.X + width, anchor.Y + half));
    }

    public static void PhotoPill(ImDrawListPtr drawList, Vector2 bottomCorner, float maxWidth, string glyph,
        string label, bool fromRight, float scale)
    {
        var half = ChipUnits * scale * 0.5f;
        var pill = PillRect(new Vector2(bottomCorner.X, bottomCorner.Y - half), maxWidth, label, scale, out var fitted);
        var shift = new Vector2(fromRight ? pill.Width : 0f, 0f);
        PaintPill(drawList, new Rect(pill.Min - shift, pill.Max - shift), glyph, fitted, ChipFill, White, scale);
    }

    private static void PaintPill(ImDrawListPtr drawList, Rect pill, string glyph, string label, Vector4 fill,
        Vector4 ink, float scale)
    {
        var pixels = LabelPixels();
        var glyphSize = InlineIconUnits * scale;
        var left = pill.Min.X + Metrics.Space.Md * scale;
        drawList.AddRectFilled(pill.Min, pill.Max, Palette.Pack(fill), pill.Height * 0.5f);
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, pill.Center.Y), glyph, ink, glyphSize);
        PrismType.Draw(drawList,
            new Vector2(left + glyphSize + Metrics.Space.Xxs * scale,
                pill.Center.Y - PrismType.Snap(PrismFace.Label, pixels) * 0.5f), label, ink, PrismFace.Label, pixels);
    }

    private static uint[] PackAll(Vector4[] colors)
    {
        var packed = new uint[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            packed[index] = Palette.Pack(colors[index]);
        }

        return packed;
    }

    public static void SlotIcon(ImDrawListPtr drawList, Vector2 center, float size, int slot, Vector4 ink) =>
        DrawStrokes(drawList, center, size, SlotIcons[(uint)(slot + 1) < (uint)SlotIcons.Length ? slot + 1 : 0], ink);

    public static void FactIcon(ImDrawListPtr drawList, Vector2 center, float size, PrismFact fact, Vector4 ink) =>
        DrawStrokes(drawList, center, size, FactIcons[(int)fact], ink);

    public static void FactToggle(ImDrawListPtr drawList, Vector2 center, float size, PrismFact fact, float fill,
        float hover)
    {
        var rest = Muted with { W = Muted.W * ToggleRestAlpha };
        var ink = Vector4.Lerp(rest, Fore, MathF.Max(hover, fill));
        var strokes = FactIcons[(int)fact];
        var shape = FactFills[(int)fact];
        if (fill > 0.001f)
        {
            FillShapes(drawList, center, size, strokes, shape, ink with { W = ink.W * fill });
        }

        if (shape.Carve)
        {
            DrawStrokes(drawList, center, size, strokes, Vector4.Lerp(ink, Back, fill), shape.Shapes, strokes.Length);
            DrawStrokes(drawList, center, size, strokes, ink, 0, shape.Shapes);
            return;
        }

        DrawStrokes(drawList, center, size, strokes, ink);
    }

    private static void FillShapes(ImDrawListPtr drawList, Vector2 center, float size, IconStroke[] strokes,
        FactFill shape, Vector4 ink)
    {
        var unit = size / IconUnits;
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var color = ImGui.GetColorU32(ink);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        Span<Vector2> points = stackalloc Vector2[IconMaxPoints];
        for (var strokeIndex = 0; strokeIndex < shape.Shapes; strokeIndex++)
        {
            var outline = strokes[strokeIndex].Points;
            var middle = Vector2.Zero;
            for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
            {
                points[pointIndex] = origin + outline[pointIndex] * unit;
                middle += points[pointIndex];
            }

            if (!shape.Star)
            {
                drawList.AddConvexPolyFilled(ref points[0], outline.Length, color);
                continue;
            }

            middle /= outline.Length;
            for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
            {
                drawList.AddTriangleFilled(middle, points[pointIndex], points[(pointIndex + 1) % outline.Length],
                    color);
            }
        }

        drawList.Flags = flags;
    }

    private static void DrawStrokes(ImDrawListPtr drawList, Vector2 center, float size, IconStroke[] strokes,
        Vector4 ink, int first = 0, int end = int.MaxValue)
    {
        var unit = size / IconUnits;
        var thickness = IconStrokeUnits * unit;
        var origin = center - new Vector2(size * 0.5f, size * 0.5f);
        var color = ImGui.GetColorU32(ink);
        Span<Vector2> points = stackalloc Vector2[IconMaxPoints];
        for (var strokeIndex = first; strokeIndex < Math.Min(end, strokes.Length); strokeIndex++)
        {
            var stroke = strokes[strokeIndex];
            var count = stroke.Points.Length;
            for (var pointIndex = 0; pointIndex < count; pointIndex++)
            {
                points[pointIndex] = origin + stroke.Points[pointIndex] * unit;
            }

            drawList.AddPolyline(ref points[0], count, color, stroke.Closed ? ImDrawFlags.Closed : ImDrawFlags.None,
                thickness);
            if (!stroke.Closed)
            {
                drawList.AddCircleFilled(points[0], thickness * 0.5f, color, IconCapSegments);
                drawList.AddCircleFilled(points[count - 1], thickness * 0.5f, color, IconCapSegments);
            }
        }
    }

    public static void SlotTile(ImDrawListPtr drawList, Rect tile, int slot, float scale)
    {
        var size = tile.Width;
        drawList.AddRectFilled(tile.Min, tile.Max, ImGui.GetColorU32(Surfaces.Fill(themeInks.Fore, FillLevel.Primary)),
            size * Metrics.Radius.HomeTileFactor);
        SlotIcon(drawList, tile.Center, size * TileIconShare, slot, themeInks.Fore);
    }

    private readonly record struct IconStroke(Vector2[] Points, bool Closed);

    private readonly record struct FactFill(int Shapes, bool Carve, bool Star);

    private sealed class IconPen
    {
        private readonly List<Vector2> points = new();

        public IconPen Through(params float[] coordinates)
        {
            for (var index = 0; index + 1 < coordinates.Length; index += 2)
            {
                points.Add(new Vector2(coordinates[index], coordinates[index + 1]));
            }

            return this;
        }

        public IconPen Arc(float centerX, float centerY, float radiusX, float radiusY, float fromDegrees,
            float toDegrees)
        {
            var segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(toDegrees - fromDegrees) / ArcStepDegrees));
            for (var index = 0; index <= segments; index++)
            {
                var angle = (fromDegrees + (toDegrees - fromDegrees) * index / segments) * MathF.PI / 180f;
                points.Add(new Vector2(centerX + radiusX * MathF.Cos(angle), centerY + radiusY * MathF.Sin(angle)));
            }

            return this;
        }

        public IconStroke Open() => Finish(false);

        public IconStroke Closed() => Finish(true);

        private IconStroke Finish(bool closed)
        {
            Debug.Assert(points.Count <= IconMaxPoints, "A slot icon stroke outgrew the draw buffer.");
            return new IconStroke(points.ToArray(), closed);
        }
    }

    private static IconStroke Oval(float centerX, float centerY, float radiusX, float radiusY) =>
        new IconPen().Arc(centerX, centerY, radiusX, radiusY, 0f, 360f - ArcStepDegrees).Closed();

    private static IconStroke[][] BuildFactIcons()
    {
        IconStroke[] person =
        [
            Oval(12f, 8f, 4f, 4f),
            new IconPen().Arc(12f, 21f, 7f, 6.5f, 180f, 360f).Open(),
        ];
        IconStroke[] leaf =
        [
            new IconPen().Through(5f, 19f, 4.8f, 12.5f, 7.5f, 8f, 12.5f, 5.5f, 19f, 5f, 18.8f, 11.5f, 16.5f, 16f, 12f,
                18.6f).Closed(),
            new IconPen().Through(5f, 19f, 13f, 11f).Open(),
        ];
        IconStroke[] male =
        [
            Oval(10f, 14f, 5.5f, 5.5f),
            new IconPen().Through(14f, 10f, 19.5f, 4.5f).Open(),
            new IconPen().Through(14.5f, 4.5f, 19.5f, 4.5f, 19.5f, 9.5f).Open(),
        ];
        IconStroke[] female =
        [
            Oval(12f, 9f, 5.5f, 5.5f),
            new IconPen().Through(12f, 14.5f, 12f, 21f).Open(),
            new IconPen().Through(9f, 18f, 15f, 18f).Open(),
        ];
        IconStroke[] fitsMe =
        [
            Oval(9.5f, 8f, 3.8f, 3.8f),
            new IconPen().Arc(9.5f, 21f, 6.2f, 6.5f, 180f, 360f).Open(),
            new IconPen().Through(16f, 15.5f, 18.2f, 17.7f, 21.8f, 13.5f).Open(),
        ];
        return [SlotIcons[0], leaf, person, male, female, fitsMe];
    }

    private static IconStroke[][] BuildSlotIcons()
    {
        IconStroke[] sparkle =
        [
            new IconPen().Through(11f, 4f, 12.8f, 8.6f, 17.5f, 10.5f, 12.8f, 12.4f, 11f, 17f, 9.2f, 12.4f, 4.5f, 10.5f,
                9.2f, 8.6f).Closed(),
            new IconPen().Through(18f, 14.5f, 18.8f, 16.3f, 20.6f, 17.1f, 18.8f, 17.9f, 18f, 19.7f, 17.2f, 17.9f, 15.4f,
                17.1f, 17.2f, 16.3f).Closed(),
        ];
        IconStroke[] sword =
        [
            new IconPen().Through(20f, 4f, 20f, 8.5f, 9.5f, 19f, 5f, 14.5f, 15.5f, 4f).Closed(),
            new IconPen().Through(6.5f, 11.5f, 12.5f, 17.5f).Open(),
            new IconPen().Through(4f, 20f, 6.5f, 17.5f).Open(),
        ];
        IconStroke[] shield =
        [
            new IconPen().Through(12f, 3f, 20f, 6f, 20f, 12f, 19.4f, 15.3f, 17.7f, 17.9f, 15.2f, 19.85f, 12f, 21f, 8.8f,
                19.85f, 6.3f, 17.9f, 4.6f, 15.3f, 4f, 12f, 4f, 6f).Closed(),
        ];
        IconStroke[] hat =
        [
            Oval(12f, 16.5f, 9f, 2.8f),
            new IconPen().Through(7f, 14.3f, 8.6f, 7f, 9.1f, 6.25f, 9.8f, 6f, 14.2f, 6f, 14.9f, 6.25f, 15.4f, 7f, 17f,
                14.3f).Open(),
        ];
        IconStroke[] shirt =
        [
            new IconPen().Through(21f, 6f, 21f, 11f, 18f, 11f).Arc(17f, 19f, 1f, 1f, 0f, 90f)
                .Arc(7f, 19f, 1f, 1f, 90f, 180f).Through(6f, 11f, 3f, 11f, 3f, 6f).Arc(12f, 4f, 3f, 3f, 180f, 0f)
                .Closed(),
        ];
        IconStroke[] glove =
        [
            new IconPen().Through(8f, 20f).Arc(14.5f, 16.5f, 3.5f, 3.5f, 90f, 0f).Arc(16.5f, 10f, 1.5f, 1.5f, 0f, -180f)
                .Arc(13.5f, 5.5f, 1.5f, 1.5f, 0f, -180f).Through(12f, 10f).Arc(10.5f, 4.5f, 1.5f, 1.5f, 0f, -180f)
                .Through(9f, 10f).Arc(7.5f, 6.5f, 1.5f, 1.5f, 0f, -180f).Through(6f, 14.5f)
                .Arc(3.1f, 13.75f, 1.52f, 1.52f, -43.7f, -223.7f).Closed(),
        ];
        IconStroke[] trousers =
        [
            new IconPen().Through(7f, 3f, 17f, 3f, 18.5f, 21f, 14.3f, 21f, 12f, 10.5f, 9.7f, 21f, 5.5f, 21f).Closed(),
            new IconPen().Through(7.2f, 6.5f, 16.8f, 6.5f).Open(),
        ];
        IconStroke[] boot =
        [
            new IconPen().Through(8f, 3f, 13f, 3f, 13f, 12f, 17.6f, 14.2f, 18.6f, 15f, 19f, 16.4f, 19f, 19f, 5f,
                19f, 5f, 12.5f).Closed(),
            new IconPen().Through(5f, 16f, 19f, 16f).Open(),
        ];
        IconStroke[] earring =
        [
            Oval(12f, 5f, 1.8f, 1.8f),
            new IconPen().Through(12f, 6.8f, 12f, 10f, 15f, 13.5f, 12f, 19.5f, 9f, 13.5f, 12f, 10f).Open(),
        ];
        IconStroke[] necklace =
        [
            new IconPen().Through(5f, 4f, 5.53f, 7.44f, 7f, 10.13f, 9.22f, 11.87f, 12f, 12.5f, 14.78f, 11.87f, 17f,
                10.13f, 18.47f, 7.44f, 19f, 4f).Open(),
            new IconPen().Through(9.5f, 16f, 12f, 12.5f, 14.5f, 16f, 12f, 20f).Closed(),
        ];
        IconStroke[] bracelet =
        [
            Oval(12f, 11f, 8f, 4f),
            new IconPen().Through(4f, 11f).Arc(12f, 13.5f, 8f, 4f, 180f, 0f).Through(20f, 11f).Open(),
        ];
        IconStroke[] ring =
        [
            Oval(12f, 14.5f, 5f, 5f),
            new IconPen().Through(9.5f, 6.5f, 12f, 3.5f, 14.5f, 6.5f, 12f, 9.5f).Closed(),
        ];
        return [sparkle, sword, shield, hat, shirt, glove, sparkle, trousers, boot, earring, necklace, bracelet, ring,
            ring];
    }
}
