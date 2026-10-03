using Aetherphone.Core;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Collections;

internal static class CollectionsArt
{
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 OwnedInk = new(0.30f, 0.82f, 0.50f, 1f);

    private const float StateTileSize = 68f;
    private const float StateGlyphSize = 30f;
    private const float StateTitleGap = 18f;
    private const float StateHintGap = 6f;
    private const float StateActionGap = 20f;
    private const float StateActionHeight = 40f;
    private const float StateActionPadding = 40f;
    private const float StateActionMinWidth = 140f;
    private const float StateMaxTextWidth = 280f;
    private const float StateTextInset = 48f;
    private const float StateLift = 36f;
    private const float PanelPad = 16f;
    private const float PanelTileSize = 40f;
    private const float PanelGlyphSize = 18f;
    private const float PanelTileGap = 14f;
    private const float PanelLineGap = 3f;
    private const float RingLabelFill = 0.80f;
    private const float RingLabelNudge = 0.06f;
    private const float CheckThickness = 2f;
    private const float PillPadX = 10f;
    private const float PillHeight = 26f;
    private const float PillDot = 14f;
    private const float PillGap = 6f;
    private const float PillFillAlpha = 0.16f;
    private const float ChevronThickness = 1.8f;
    private const float EmptyRingAlpha = 0.45f;
    private const float EmptyRingThickness = 1.6f;

    private static readonly CategoryStyle[] Styles =
    {
        new(new Vector4(0.95f, 0.55f, 0.25f, 1f), FontAwesomeIcon.Horse),
        new(new Vector4(0.30f, 0.78f, 0.48f, 1f), FontAwesomeIcon.Paw),
        new(new Vector4(0.98f, 0.76f, 0.30f, 1f), FontAwesomeIcon.Smile),
        new(new Vector4(0.62f, 0.46f, 0.96f, 1f), FontAwesomeIcon.Music),
        new(new Vector4(0.93f, 0.38f, 0.62f, 1f), FontAwesomeIcon.Cut),
        new(new Vector4(0.26f, 0.74f, 0.86f, 1f), FontAwesomeIcon.Glasses),
        new(new Vector4(0.98f, 0.64f, 0.22f, 1f), FontAwesomeIcon.Trophy),
        new(new Vector4(0.36f, 0.62f, 0.96f, 1f), FontAwesomeIcon.Clone),
    };

    public static Vector4 Tint(CollectionCategory category) => Styles[(int)category].Tint;

    public static FontAwesomeIcon Icon(CollectionCategory category) => Styles[(int)category].Icon;

    public static void CategoryTile(ImDrawListPtr drawList, Vector2 center, float size, CollectionCategory category)
    {
        GlyphTile(drawList, center, size, Tint(category), Icon(category));
    }

    public static void GlyphTile(ImDrawListPtr drawList, Vector2 center, float size, Vector4 tint,
        FontAwesomeIcon icon)
    {
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, AccentRing.Ink, size * 0.46f);
    }

    public static bool StateScreen(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint,
        string actionLabel)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var centerX = body.Center.X;
        var tileSize = StateTileSize * scale;
        var tileTop = body.Center.Y - StateLift * scale - tileSize;
        var tileMin = new Vector2(centerX - tileSize * 0.5f, tileTop);
        var tileMax = new Vector2(centerX + tileSize * 0.5f, tileTop + tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);

        var maxWidth = MathF.Min(body.Width - StateTextInset * scale, StateMaxTextWidth * scale);
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(centerX, tileMax.Y + StateTitleGap * scale), maxWidth);
        var bottom = titleBottom;
        if (hint.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(drawList, hint, TextStyles.Subheadline, ui.MutedInk,
                new Vector2(centerX, titleBottom + StateHintGap * scale), maxWidth);
        }

        if (actionLabel.Length == 0)
        {
            return false;
        }

        var natural = Typography.Measure(actionLabel, TextStyles.Headline).X + StateActionPadding * scale;
        var width = Math.Clamp(natural, StateActionMinWidth * scale,
            MathF.Max(StateActionMinWidth * scale, body.Width - StateTextInset * scale));
        var top = bottom + StateActionGap * scale;
        var rect = new Rect(new Vector2(centerX - width * 0.5f, top),
            new Vector2(centerX + width * 0.5f, top + StateActionHeight * scale));
        return ui.AccentPill(rect, actionLabel, true, TextStyles.Headline);
    }

    public static float PanelHeight(string title, string hint, float width, float scale)
    {
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, textWidth).Y;
        var hintHeight = hint.Length > 0
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y + PanelLineGap * scale
            : 0f;
        return MathF.Max(PanelTileSize * scale, titleHeight + hintHeight) + PanelPad * 2f * scale;
    }

    public static float Panel(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, FontAwesomeIcon icon,
        Vector4 tint, string title, string hint, float scale)
    {
        var height = PanelHeight(title, hint, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale, true);
        var pad = PanelPad * scale;
        var tileSize = PanelTileSize * scale;
        var tileCenter = new Vector2(origin.X + pad + tileSize * 0.5f, origin.Y + pad + tileSize * 0.5f);
        var half = new Vector2(tileSize * 0.5f, tileSize * 0.5f);
        IconTile.FillShaded(drawList, tileCenter - half, tileCenter + half, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, tileCenter, icon, AccentRing.Ink, PanelGlyphSize * scale);
        var textLeft = tileCenter.X + tileSize * 0.5f + PanelTileGap * scale;
        var textWidth = PanelTextWidth(width, scale);
        var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), title, ui.TitleInk,
            TextStyles.Headline, textWidth);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad + titleHeight + PanelLineGap * scale),
                hint, ui.MutedInk, TextStyles.Footnote, textWidth);
        }

        return height;
    }

    private static float PanelTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (PanelPad * 2f + PanelTileSize + PanelTileGap) * scale);

    public static float SectionHeader(ImDrawListPtr drawList, Vector2 origin, float width, string title, Vector4 ink)
    {
        var fitted = Typography.FitText(title, width, TextStyles.Title3);
        Typography.Draw(drawList, origin, fitted, ink, TextStyles.Title3);
        return Typography.Measure(fitted, TextStyles.Title3).Y;
    }

    public static void Ring(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float fraction,
        Vector4 track, Vector4 fill, string label, Vector4 ink, in TextStyle style, string widest)
    {
        ProgressRing.Track(drawList, center, radius, thickness, track);
        if (fraction > 0f)
        {
            ProgressRing.Fill(drawList, center, radius, thickness, Math.Clamp(fraction, 0f, 1f), fill);
        }

        if (label.Length == 0)
        {
            return;
        }

        var maxWidth = (radius - thickness) * 2f * RingLabelFill;
        var fontScale = style.Scale;
        var reference = Typography.Measure(widest, fontScale, style.Weight).X;
        if (reference > maxWidth)
        {
            fontScale *= maxWidth / reference;
        }

        var size = Typography.Measure(label, fontScale, style.Weight);
        var position = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f + size.Y * RingLabelNudge);
        Typography.Draw(drawList, position, label, ink, fontScale, style.Weight);
    }

    public static void CheckBadge(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill, Vector4 mark,
        float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 24);
        var color = ImGui.GetColorU32(mark);
        var thickness = CheckThickness * scale;
        var elbow = center + new Vector2(-0.12f * radius, 0.36f * radius);
        drawList.AddLine(center + new Vector2(-0.46f * radius, 0.02f * radius), elbow, color, thickness);
        drawList.AddLine(elbow, center + new Vector2(0.48f * radius, -0.34f * radius), color, thickness);
    }

    public static void EmptyBadge(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 ink, float scale)
    {
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ink, EmptyRingAlpha)), 24,
            EmptyRingThickness * scale);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float size, Vector4 ink, float scale)
    {
        var color = ImGui.GetColorU32(ink);
        var thickness = ChevronThickness * scale;
        var tip = new Vector2(center.X + size * 0.5f, center.Y);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y - size), tip, color, thickness);
        drawList.AddLine(new Vector2(center.X - size * 0.5f, center.Y + size), tip, color, thickness);
    }

    public static float StatusPill(ImDrawListPtr drawList, Vector2 leftTop, string label, Vector4 color, bool owned,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padX = PillPadX * scale;
        var dot = PillDot * scale;
        var height = PillHeight * scale;
        var width = padX * 2f + dot + PillGap * scale + textSize.X;
        var max = new Vector2(leftTop.X + width, leftTop.Y + height);
        drawList.AddRectFilled(leftTop, max, ImGui.GetColorU32(Palette.WithAlpha(color, PillFillAlpha)), height * 0.5f);
        var centerY = leftTop.Y + height * 0.5f;
        var dotCenter = new Vector2(leftTop.X + padX + dot * 0.5f, centerY);
        if (owned)
        {
            CheckBadge(drawList, dotCenter, dot * 0.5f, color, White, scale * 0.8f);
        }
        else
        {
            EmptyBadge(drawList, dotCenter, dot * 0.5f, color, scale);
        }

        Typography.Draw(drawList, new Vector2(dotCenter.X + dot * 0.5f + PillGap * scale, centerY - textSize.Y * 0.5f),
            label, color, TextStyles.FootnoteEmphasized.Scale, TextStyles.FootnoteEmphasized.Weight);
        return width;
    }

    public static void Bar(ImDrawListPtr drawList, Vector2 min, Vector2 max, float fraction, Vector4 track,
        Vector4 fill)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(track), radius);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var right = MathF.Max(min.X + radius * 2f, min.X + (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(right, max.Y), ImGui.GetColorU32(fill), radius);
    }

    private readonly record struct CategoryStyle(Vector4 Tint, FontAwesomeIcon Icon);
}
