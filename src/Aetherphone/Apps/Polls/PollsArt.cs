using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Polls;

internal static class PollsArt
{
    public const float ChipHeight = 24f;
    public const float RadioRadius = 10f;

    public static readonly Vector4 EndingSoonInk = new(1f, 0.62f, 0.04f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private const float ChipPadX = 9f;
    private const float ChipIconSize = 11f;
    private const float ChipIconGap = 5f;
    private const float ChipFillAlpha = 0.16f;
    private const float MarkDotRadius = 3.5f;
    private const float MarkDotGap = 6f;
    private const float RingThickness = 1.6f;
    private const float CheckThickness = 2f;
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
    private const float SkeletonPulseRate = 2.4f;
    private const float SkeletonBaseAlpha = 0.07f;
    private const float SkeletonPulseAlpha = 0.04f;

    public static float Chip(ImDrawListPtr drawList, Vector2 leftCenter, string label, Vector4 ink,
        FontAwesomeIcon icon, float scale)
    {
        var height = ChipHeight * scale;
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var iconSize = ChipIconSize * scale;
        var width = ChipPadX * scale * 2f + iconSize + ChipIconGap * scale + textSize.X;
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ink, ChipFillAlpha)), height * 0.5f);
        var iconCenter = new Vector2(min.X + ChipPadX * scale + iconSize * 0.5f, leftCenter.Y);
        ProgressRing.CenterIcon(drawList, iconCenter, icon, ink, iconSize);
        Typography.Draw(drawList, new Vector2(iconCenter.X + iconSize * 0.5f + ChipIconGap * scale,
            leftCenter.Y - textSize.Y * 0.5f), label, ink, TextStyles.FootnoteEmphasized.Scale,
            TextStyles.FootnoteEmphasized.Weight);
        return width;
    }

    public static void NeedsVoteMark(ImDrawListPtr drawList, Vector2 rightCenter, string label, Vector4 accent,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var textLeft = rightCenter.X - textSize.X;
        Typography.Draw(drawList, new Vector2(textLeft, rightCenter.Y - textSize.Y * 0.5f), label, accent,
            TextStyles.FootnoteEmphasized.Scale, TextStyles.FootnoteEmphasized.Weight);
        var dotRadius = MarkDotRadius * scale;
        drawList.AddCircleFilled(new Vector2(textLeft - MarkDotGap * scale - dotRadius, rightCenter.Y), dotRadius,
            ImGui.GetColorU32(accent), 16);
    }

    public static float MarkWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.FootnoteEmphasized).X + (MarkDotGap + MarkDotRadius * 2f) * scale;

    public static void RadioRing(ImDrawListPtr drawList, Vector2 center, Vector4 ink, float scale)
    {
        drawList.AddCircle(center, RadioRadius * scale, ImGui.GetColorU32(ink), 32, RingThickness * scale);
    }

    public static void CheckBadge(ImDrawListPtr drawList, Vector2 center, float grow, Vector4 fill, Vector4 mark,
        float scale)
    {
        if (grow <= 0.01f)
        {
            return;
        }

        var radius = RadioRadius * scale * grow;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 32);
        var color = ImGui.GetColorU32(mark);
        var thickness = CheckThickness * scale;
        var elbow = center + new Vector2(-0.12f * radius, 0.36f * radius);
        drawList.AddLine(center + new Vector2(-0.46f * radius, 0.02f * radius), elbow, color, thickness);
        drawList.AddLine(elbow, center + new Vector2(0.48f * radius, -0.34f * radius), color, thickness);
    }

    public static void Glyph(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 ink, float size)
    {
        ProgressRing.CenterIcon(drawList, center, icon, ink, size);
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

    public static float SkeletonHeight(int optionCount, float scale)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Title3) * 0.62f;
        return Metrics.Space.Lg * scale * 3f + ChipHeight * scale + Metrics.Space.Md * scale + lineHeight * 2f
               + Metrics.Space.Sm * scale + optionCount * (Metrics.Size.TapTarget + Metrics.Space.Sm) * scale
               - Metrics.Space.Sm * scale;
    }

    public static void SkeletonCard(ImDrawListPtr drawList, AppSkin ui, Vector2 min, float width, float height,
        int optionCount, float scale, float phase)
    {
        var max = new Vector2(min.X + width, min.Y + height);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, radius, true);
        var pulse = SkeletonBaseAlpha + SkeletonPulseAlpha * (0.5f + 0.5f * MathF.Sin(phase * SkeletonPulseRate));
        var ink = ImGui.GetColorU32(Palette.WithAlpha(White, pulse));
        var pad = Metrics.Space.Lg * scale;
        var left = min.X + pad;
        var right = max.X - pad;
        var top = min.Y + pad;
        var chipHeight = ChipHeight * scale;
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(left + width * 0.28f, top + chipHeight), ink,
            chipHeight * 0.5f);
        top += chipHeight + Metrics.Space.Md * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Title3) * 0.62f;
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(right - width * 0.12f, top + lineHeight), ink,
            lineHeight * 0.5f);
        top += lineHeight + Metrics.Space.Sm * scale;
        drawList.AddRectFilled(new Vector2(left, top), new Vector2(left + (right - left) * 0.55f, top + lineHeight),
            ink, lineHeight * 0.5f);
        top += lineHeight + Metrics.Space.Lg * scale;
        var optionHeight = Metrics.Size.TapTarget * scale;
        for (var optionIndex = 0; optionIndex < optionCount; optionIndex++)
        {
            Squircle.Fill(drawList, new Vector2(left, top), new Vector2(right, top + optionHeight),
                optionHeight * 0.5f, ink);
            top += optionHeight + Metrics.Space.Sm * scale;
        }
    }
}
