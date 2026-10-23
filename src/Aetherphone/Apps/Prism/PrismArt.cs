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

    private const float LabelUnits = 13f;
    private const float StoryBarUnits = 2f;
    private const float StoryBarSpacingUnits = 4f;
    private const float ShadeShare = 0.78f;
    private const float ShadeSolidShare = 0.46f;
    private const float TopShadeUnits = 110f;
    private const float TopFadeShare = 0.34f;
    private const float TopFogDeep = 0.9f;
    private const float FogAlpha = 0.6f;
    private const float MedalFill = 0.12f;
    private const int MedalSegments = 48;
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

    private static readonly Inks PhotoInks = new(new Vector4(0f, 0f, 0f, 1f), White, new Vector4(1f, 1f, 1f, 0.66f),
        new Vector4(1f, 1f, 1f, 0.28f), Ink, new Vector4(0f, 0f, 0f, FogAlpha));

    private static Inks themeInks = PhotoInks;
    private static Inks inks = PhotoInks;

    private readonly record struct Inks(Vector4 Back, Vector4 Fore, Vector4 Muted, Vector4 Faint, Vector4 OnFore,
        Vector4 Fog);

    public static Vector4 PhotoFore => White;

    public static Vector4 Back => inks.Back;

    public static Vector4 Page => themeInks.Back;

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
    }

    public static void OverPhoto(bool photo) => inks = photo ? PhotoInks : themeInks;

    private static readonly Vector4[] Spectrum =
    {
        AccentRing.Red, AccentRing.Orange, AccentRing.Gold, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly uint[] SpectrumPacked = PackAll(Spectrum);

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

    public static bool Link(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 color, float scale,
        bool overlay = false)
    {
        var pixels = LabelPixels();
        var size = PrismType.Measure(label, PrismFace.Label, pixels);
        var tap = Metrics.Size.TapTarget * scale;
        var padY = MathF.Max(0f, (tap - size.Y) * 0.5f);
        var padX = MathF.Max(0f, (tap - size.X) * 0.5f);
        var min = new Vector2(topLeft.X - padX, topLeft.Y - padY);
        var max = new Vector2(topLeft.X + size.X + padX, topLeft.Y + size.Y + padY);
        var hovered = overlay
            ? !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max)
            : UiInteract.Hover(min, max);
        PrismType.Draw(drawList, topLeft, label, hovered ? Fore : color, PrismFace.Label, pixels);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static bool Icon(ImDrawListPtr drawList, Vector2 center, string glyph, float scale)
    {
        var size = HeaderIconUnits * scale;
        var half = MathF.Max(size, Metrics.Size.TapTarget * scale) * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = UiInteract.Hover(min, max);
        PhoneIcon.Draw(drawList, center, glyph, hovered ? Muted : Fore, size);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
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

    public static void PhotoPanel(ImDrawListPtr drawList, Rect panel, float radius) =>
        Squircle.Fill(drawList, panel.Min, panel.Max, radius, ImGui.GetColorU32(ScrimDeep));

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

    public static void SlotTile(ImDrawListPtr drawList, Rect tile, int slot, float scale, bool empty = false)
    {
        var size = tile.Width;
        var radius = size * Metrics.Radius.HomeTileFactor;
        var iconSize = size * TileIconShare;
        var level = Surfaces.Fill(themeInks.Fore, FillLevel.Primary);
        if (empty)
        {
            drawList.AddRectFilled(tile.Min, tile.Max, ImGui.GetColorU32(inks.Fog), radius);
            drawList.AddRect(tile.Min, tile.Max, ImGui.GetColorU32(level), radius, ImDrawFlags.None,
                Metrics.Stroke.Thin * scale);
            SlotIcon(drawList, tile.Center, iconSize, slot, themeInks.Muted);
            return;
        }

        drawList.AddRectFilled(tile.Min, tile.Max, ImGui.GetColorU32(level), radius);
        SlotIcon(drawList, tile.Center, iconSize, slot, themeInks.Fore);
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
