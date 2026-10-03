using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Timers;

internal static class TimersArt
{
    public const float RowHeight = 62f;
    public const float GroupHeaderHeight = 54f;
    public const float SectionHeaderHeight = 40f;
    public const float SectionGap = 22f;
    public const float CardGap = 12f;
    public const float BellHitRadius = 20f;
    public const float BarHeight = 6f;

    public static readonly Vector4 ReadyInk = new(0.22f, 0.80f, 0.42f, 1f);

    private const float DialSize = 36f;
    private const float DialThickness = 2.6f;
    private const float DialWashAlpha = 0.20f;
    private const float DialTrackAlpha = 0.18f;
    private const float DialGlyphFraction = 0.42f;
    private const float BellGlyphSize = 15f;
    private const float BellWashAlpha = 0.16f;
    private const float StateTileSize = 48f;
    private const float StateGlyphSize = 22f;
    private const float StateGap = 14f;
    private const float StateLineGap = 4f;
    private const float TextGap = 12f;
    private const float ValueGap = 10f;
    private const float SubGap = 2f;

    public static float DialSpan(float scale) => (DialSize + TextGap) * scale;

    public static float DialCenterX(float left, float scale) => left + DialSize * 0.5f * scale;

    public static void Card(ImDrawListPtr drawList, AppSkin ui, Vector2 min, Vector2 max, float scale) =>
        ui.Card(drawList, min, max, Metrics.Radius.Widget * scale, true);

    public static void Hairline(ImDrawListPtr drawList, AppSkin ui, float left, float right, float y) =>
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ui.Hairline),
            Metrics.Stroke.Hairline);

    public static void Dial(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 tint,
        float fraction, bool showRing, float scale)
    {
        var radius = DialSize * 0.5f * scale;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(tint, DialWashAlpha)), 32);
        if (showRing)
        {
            var thickness = DialThickness * scale;
            var ringRadius = radius - thickness * 0.5f;
            ProgressRing.Track(drawList, center, ringRadius, thickness, Palette.WithAlpha(tint, DialTrackAlpha));
            ProgressRing.Fill(drawList, center, ringRadius, thickness, Math.Clamp(fraction, 0f, 1f), tint);
        }

        ProgressRing.CenterIcon(drawList, center, icon, tint, DialSize * DialGlyphFraction * scale);
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

        var width = MathF.Max(max.Y - min.Y, (max.X - min.X) * clamped);
        drawList.AddRectFilled(min, new Vector2(min.X + width, max.Y), ImGui.GetColorU32(fill), radius);
    }

    public static float Value(ImDrawListPtr drawList, float right, float centerY, string value, Vector4 ink)
    {
        if (value.Length == 0)
        {
            return 0f;
        }

        var size = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), value, ink,
            TextStyles.SubheadlineEmphasized);
        return size.X;
    }

    public static void Labels(ImDrawListPtr drawList, float left, float right, float centerY, string title,
        string subtitle, Vector4 titleInk, Vector4 subtitleInk, float scale)
    {
        var width = MathF.Max(1f, right - left);
        var fittedTitle = Typography.FitText(title, width, TextStyles.Headline);
        var titleHeight = Typography.Measure(fittedTitle, TextStyles.Headline).Y;
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), fittedTitle, titleInk,
                TextStyles.Headline);
            return;
        }

        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        var subtitleHeight = Typography.Measure(fittedSubtitle, TextStyles.Footnote).Y;
        var top = centerY - (titleHeight + SubGap * scale + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), fittedTitle, titleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + SubGap * scale), fittedSubtitle,
            subtitleInk, TextStyles.Footnote);
    }

    public static float LabelRight(float valueLeft, float scale) => valueLeft - ValueGap * scale;

    public static bool Bell(ImDrawListPtr drawList, string id, Vector2 center, bool on, AppSkin ui, string tooltip,
        float scale)
    {
        var hit = new Vector2(BellHitRadius, BellHitRadius) * scale;
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var grow = PressFx.Scale(id, pressed, PressFx.IconPressedScale);
        var ink = on ? ui.Accent : ui.MutedInk;
        if (hovered || on)
        {
            var washAlpha = on ? BellWashAlpha : BellWashAlpha * 0.6f;
            drawList.AddCircleFilled(center, BellHitRadius * 0.78f * scale * grow,
                ImGui.GetColorU32(Palette.WithAlpha(on ? ui.Accent : ui.TitleInk, washAlpha)), 32);
        }

        ProgressRing.CenterIcon(drawList, center, on ? FontAwesomeIcon.Bell : FontAwesomeIcon.BellSlash, ink,
            BellGlyphSize * scale * grow);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        if (!UiInteract.Click(center - hit, center + hit, hovered, false))
        {
            return false;
        }

        UiFeedback.Play(on ? UiSound.ToggleOff : UiSound.ToggleOn);
        return true;
    }

    public static float StateHeight(string title, string body, float width, float scale)
    {
        var textWidth = StateTextWidth(width, scale);
        var titleHeight = Typography.WrapText(title, TextStyles.Headline, textWidth).Length *
                          Typography.LineHeight(TextStyles.Headline);
        var bodyHeight = Typography.WrapText(body, TextStyles.Subheadline, textWidth).Length *
                         Typography.LineHeight(TextStyles.Subheadline);
        var textHeight = titleHeight + StateLineGap * scale + bodyHeight;
        return MathF.Max(StateTileSize * scale, textHeight) + Metrics.Space.Lg * 2f * scale;
    }

    public static void State(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float height,
        FontAwesomeIcon icon, string title, string body, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + height);
        Card(drawList, ui, origin, max, scale);
        var pad = Metrics.Space.Lg * scale;
        var tileSize = StateTileSize * scale;
        var tileMin = new Vector2(origin.X + pad, origin.Y + pad);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        IconTile.FillShaded(drawList, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, (tileMin + tileMax) * 0.5f, icon, AccentRing.Ink, StateGlyphSize * scale);
        var textLeft = tileMax.X + StateGap * scale;
        var textWidth = StateTextWidth(width, scale);
        var top = origin.Y + pad;
        top += DrawWrapped(drawList, new Vector2(textLeft, top), title, ui.TitleInk, TextStyles.Headline, textWidth);
        DrawWrapped(drawList, new Vector2(textLeft, top + StateLineGap * scale), body, ui.MutedInk,
            TextStyles.Subheadline, textWidth);
    }

    private static float StateTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Space.Lg * 2f * scale - (StateTileSize + StateGap) * scale);

    private static float DrawWrapped(ImDrawListPtr drawList, Vector2 topLeft, string text, Vector4 ink,
        in TextStyle style, float width)
    {
        var lines = Typography.WrapText(text, style, width);
        var lineHeight = Typography.LineHeight(style);
        for (var index = 0; index < lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), lines[index], ink,
                style);
        }

        return lines.Length * lineHeight;
    }
}
