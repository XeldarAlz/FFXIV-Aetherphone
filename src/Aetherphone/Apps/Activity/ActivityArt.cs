using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal static class ActivityArt
{
    public const float SectionGap = 24f;
    public const float HeaderGap = 10f;
    public const float CardPad = 16f;
    public const float TileGap = 12f;
    public const float RingTrackAlpha = 0.22f;
    public const float MaxLaps = 2f;

    private const float MinSegmentAngle = MathF.PI / 96f;
    private const float ChordSag = 0.3f;
    private const float Top = -MathF.PI * 0.5f;
    private const float TailDarken = 0.30f;
    private const float LapLighten = 0.22f;
    private const float HeadShadowAlpha = 0.45f;
    private const float GlowBase = 0.22f;
    private const float GlowPulse = 0.14f;
    private const float StateTileSize = 44f;
    private const float StateGlyphSize = 20f;
    private const float StateGap = 14f;
    private const float StateLineGap = 3f;
    private const float ChevronThickness = 1.8f;
    private const float MedallionRimAlpha = 0.55f;
    private const float MedallionLockedAlpha = 0.35f;
    private const float MedallionGlyph = 0.42f;
    private const float MedallionFacets = 6f;

    private static readonly Vector4 LockedInk = new(0.42f, 0.42f, 0.46f, 1f);

    private static readonly Vector4[] RingTints =
    {
        ActivityRings.RingOneTint,
        ActivityRings.RingTwoTint,
        ActivityRings.RingThreeTint,
    };

    private static readonly FontAwesomeIcon[] RingIcons =
    {
        FontAwesomeIcon.ArrowUp,
        FontAwesomeIcon.Dungeon,
        FontAwesomeIcon.Coins,
    };

    private static readonly Vector4[] AwardTints =
    {
        ActivityRings.RingOneTint,
        AccentRing.Orange,
        AccentRing.Violet,
        AccentRing.Rose,
        ActivityRings.RingTwoTint,
        ActivityRings.RingThreeTint,
        AccentRing.Azure,
        AccentRing.Gold,
    };

    private static readonly FontAwesomeIcon[] AwardIcons =
    {
        FontAwesomeIcon.Star,
        FontAwesomeIcon.Fire,
        FontAwesomeIcon.Crown,
        FontAwesomeIcon.Bolt,
        FontAwesomeIcon.Dungeon,
        FontAwesomeIcon.Coins,
        FontAwesomeIcon.Clock,
        FontAwesomeIcon.Trophy,
    };

    public static Vector4 AwardTint(ActivityAward award) => AwardTints[(int)award];

    public static FontAwesomeIcon AwardIcon(ActivityAward award) => AwardIcons[(int)award];

    public static Vector4 Tint(int ring) => RingTints[ring];

    public static FontAwesomeIcon RingIcon(int ring) => RingIcons[ring];

    public static float FrameDelta() => MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);

    public static void Rings(ImDrawListPtr drawList, Vector2 center, float outerRadius, float thickness, float gap,
        ReadOnlySpan<float> fractions, bool glow)
    {
        for (var ring = 0; ring < ActivityGoals.RingCount; ring++)
        {
            var radius = outerRadius - thickness * 0.5f - (thickness + gap) * ring;
            Ring(drawList, center, radius, thickness, fractions[ring], RingTints[ring], glow);
        }
    }

    public static void Ring(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float fraction,
        Vector4 tint, bool glow)
    {
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, RingTrackAlpha)), 0, thickness);
        var clamped = Math.Clamp(fraction, 0f, MaxLaps);
        if (clamped <= 0.0001f)
        {
            return;
        }

        if (glow && clamped >= ActivityGoals.ClosedThreshold)
        {
            var strength = GlowBase + GlowPulse * Pulse.Wave(Pulse.Breath);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, strength * 0.5f)), 64,
                thickness * 1.9f);
        }

        var firstLap = MathF.Min(clamped, 1f);
        GradientArc(drawList, center, radius, thickness, Top, Top + firstLap * MathF.Tau,
            Palette.Darken(tint, TailDarken), tint);
        if (clamped <= 1f)
        {
            return;
        }

        var lapEnd = Top + clamped * MathF.Tau;
        var headDirection = Direction(lapEnd);
        var tangent = new Vector2(-headDirection.Y, headDirection.X);
        drawList.AddCircleFilled(center + headDirection * radius + tangent * thickness * 0.18f, thickness * 0.55f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, HeadShadowAlpha)), 16);
        GradientArc(drawList, center, radius, thickness, Top, lapEnd, tint, Palette.Lighten(tint, LapLighten));
    }

    private static void GradientArc(ImDrawListPtr drawList, Vector2 center, float radius, float thickness,
        float start, float end, Vector4 startInk, Vector4 endInk)
    {
        var span = end - start;
        var outerEdge = radius + thickness * 0.5f;
        var maxAngle = MathF.Max(MinSegmentAngle, 2f * MathF.Acos(1f - MathF.Min(1f, ChordSag / outerEdge)));
        var segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(span) / maxAngle));
        var previous = center + Direction(start) * radius;
        var capSegments = Math.Max(8, segments / 2);
        drawList.AddCircleFilled(previous, thickness * 0.5f, ImGui.GetColorU32(startInk), capSegments);
        var color = 0u;
        for (var index = 1; index <= segments; index++)
        {
            var progress = index / (float)segments;
            var current = center + Direction(start + span * progress) * radius;
            color = ImGui.GetColorU32(Vector4.Lerp(startInk, endInk, progress));
            drawList.AddLine(previous, current, color, thickness);
            previous = current;
        }

        drawList.AddCircleFilled(previous, thickness * 0.5f, color, capSegments);
    }

    private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ChevronThickness * scale;
        var tip = new Vector2(center.X + size * 0.5f, center.Y);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y - size), tip, color, thickness);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y + size), tip, color, thickness);
    }

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink)
    {
        var fitted = Typography.FitText(title, width, TextStyles.Title3);
        Typography.Draw(drawList, origin, fitted, ink, TextStyles.Title3);
        return Typography.LineHeight(TextStyles.Title3);
    }

    public static void GlyphTile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint,
        FontAwesomeIcon icon)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * 0.46f);
    }

    public static float StateHeight(string title, string body, float width, float scale)
    {
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(StateTileSize * scale, titleHeight + StateLineGap * scale + bodyHeight) +
               CardPad * 2f * scale;
    }

    public static float State(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon,
        Vector4 tint, string title, string body, float scale)
    {
        var height = StateHeight(title, body, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Widget * scale, true);
        var pad = CardPad * scale;
        var tileSize = StateTileSize * scale;
        GlyphTile(drawList, new Vector2(origin.X + pad + tileSize * 0.5f, origin.Y + pad + tileSize * 0.5f), tileSize,
            tint, icon);
        var textLeft = origin.X + pad + tileSize + StateGap * scale;
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + StateLineGap * scale), body,
            ui.MutedInk, TextStyles.Subheadline, textWidth);
        return height;
    }

    private static float StateTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (CardPad * 2f + StateTileSize + StateGap) * scale);

    public static void Medallion(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint,
        FontAwesomeIcon icon, bool earned)
    {
        var body = earned ? tint : LockedInk;
        var alpha = earned ? 1f : MedallionLockedAlpha;
        var outer = Palette.Darken(body, 0.35f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(outer, alpha)), 48);
        var facets = (int)MedallionFacets;
        var inner = radius * 0.80f;
        var top = Palette.Lighten(body, 0.18f);
        var bottom = Palette.Darken(body, 0.12f);
        for (var facet = 0; facet < facets; facet++)
        {
            var angleA = Top + MathF.Tau * facet / facets;
            var angleB = Top + MathF.Tau * (facet + 1) / facets;
            var shade = 0.5f + 0.5f * MathF.Sin((angleA + angleB) * 0.5f);
            var color = Vector4.Lerp(top, bottom, shade);
            drawList.AddTriangleFilled(center, center + Direction(angleA) * inner, center + Direction(angleB) * inner,
                ImGui.GetColorU32(Palette.WithAlpha(color, alpha)));
        }

        drawList.AddCircle(center, radius * 0.90f,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(body, 0.45f), MedallionRimAlpha * alpha)), 48,
            MathF.Max(1f, radius * 0.06f));
        var ink = earned ? AccentRing.Ink : Palette.WithAlpha(AccentRing.Ink, 0.45f);
        ProgressRing.CenterIcon(drawList, center, icon, ink, radius * 2f * MedallionGlyph);
    }

    public static void Bars(ImDrawListPtr drawList, Rect area, ReadOnlySpan<float> fractions, int highlight,
        Vector4 tint, Vector4 guideInk, float barWidth)
    {
        var count = fractions.Length;
        var columnWidth = area.Width / count;
        var height = area.Height;
        var guideY = area.Max.Y - height / MaxLaps;
        DashedLine(drawList, new Vector2(area.Min.X, guideY), area.Max.X, guideInk, barWidth * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var centerX = area.Min.X + columnWidth * (index + 0.5f);
            var left = centerX - barWidth * 0.5f;
            var right = centerX + barWidth * 0.5f;
            var fraction = Math.Clamp(fractions[index], 0f, MaxLaps) / MaxLaps;
            var ink = index == highlight ? tint : Palette.WithAlpha(tint, 0.42f);
            drawList.AddRectFilled(new Vector2(left, area.Min.Y), new Vector2(right, area.Max.Y),
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.10f)), barWidth * 0.5f);
            if (fraction <= 0.001f)
            {
                continue;
            }

            var barTop = MathF.Min(area.Max.Y - barWidth, area.Max.Y - height * fraction);
            drawList.AddRectFilled(new Vector2(left, barTop), new Vector2(right, area.Max.Y),
                ImGui.GetColorU32(ink), barWidth * 0.5f);
        }
    }

    private static void DashedLine(ImDrawListPtr drawList, Vector2 start, float endX, Vector4 ink, float dash)
    {
        var color = ImGui.GetColorU32(ink);
        var step = MathF.Max(2f, dash * 2f);
        for (var dashX = start.X; dashX < endX; dashX += step)
        {
            drawList.AddLine(new Vector2(dashX, start.Y), new Vector2(MathF.Min(endX, dashX + dash), start.Y), color,
                1f);
        }
    }
}
