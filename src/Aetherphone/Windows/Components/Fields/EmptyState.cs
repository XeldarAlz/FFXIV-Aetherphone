using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class EmptyState
{
    private const float ActionGap = 22f;
    private const float ActionHeight = 38f;
    private const float ActionPadding = 44f;
    private const float ActionMinWidth = 132f;

    private const float FontAwesomeGlyph = 1.7f;
    private const float PhoneGlyph = 34f;
    private const float IconRadius = 34f;
    private const float BaseLift = 40f;
    private const float TitleOffset = 58f;
    private const float HintOffset = 84f;

    public static void Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint) =>
        DrawBody(body, ui, IconGlyph.Of(icon), title, hint, 0f);

    public static void Draw(Rect body, AppSkin ui, string glyph, string title, string hint) =>
        DrawBody(body, ui, glyph, title, hint, PhoneGlyph);

    public static bool Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint,
        string actionLabel) =>
        DrawWithAction(body, ui, IconGlyph.Of(icon), title, hint, actionLabel, 0f);

    public static bool Draw(Rect body, AppSkin ui, string glyph, string title, string hint, string actionLabel) =>
        DrawWithAction(body, ui, glyph, title, hint, actionLabel, PhoneGlyph);

    private static bool DrawWithAction(Rect body, AppSkin ui, string glyph, string title, string hint,
        string actionLabel, float glyphHeight)
    {
        var bottom = DrawBody(body, ui, glyph, title, hint, glyphHeight);
        if (actionLabel.Length == 0)
        {
            return false;
        }

        var scale = UiScale.Current;
        var natural = Typography.Measure(actionLabel, TextStyles.Callout).X + ActionPadding * scale;
        var width = Math.Clamp(natural, ActionMinWidth * scale, MathF.Max(ActionMinWidth * scale, body.Width - 56f * scale));
        var top = bottom + ActionGap * scale;
        var rect = new Rect(new Vector2(body.Center.X - width * 0.5f, top),
            new Vector2(body.Center.X + width * 0.5f, top + ActionHeight * scale));
        return ConfirmDialog.DrawPillButton(rect, actionLabel, true, ui.Theme, 1f, 1f, ConfirmButtonTone.Primary,
            "emptyState.action");
    }

    private static float DrawBody(Rect body, AppSkin ui, string glyph, string title, string hint, float glyphHeight)
    {
        var scale = UiScale.Current;
        var centerX = body.Center.X;
        var baseY = body.Center.Y - BaseLift * scale;
        var maxWidth = MathF.Min(body.Width - 56f * scale, 300f * scale);
        var hintHeight = hint.Length == 0 ? 0f : Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, maxWidth).Y;
        var fullTop = baseY - IconRadius * scale;
        var fullBottom = hint.Length == 0
            ? baseY + TitleOffset * scale + Typography.LineHeight(TextStyles.Title3) * 0.5f
            : baseY + HintOffset * scale + hintHeight;
        if (fullTop < body.Min.Y || fullBottom > body.Max.Y)
        {
            return DrawCompact(body, ui, title, hint, maxWidth, hintHeight);
        }

        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, baseY);
        drawList.AddCircleFilled(iconCenter, IconRadius * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        if (glyphHeight > 0f)
        {
            PhoneIcon.Draw(drawList, iconCenter, glyph, ui.MutedInk, glyphHeight * scale);
        }
        else
        {
            AppSkin.Icon(iconCenter, glyph, ui.MutedInk, FontAwesomeGlyph);
        }

        var titleY = baseY + TitleOffset * scale;
        Typography.DrawCentered(new Vector2(centerX, titleY), title, ui.TitleInk, TextStyles.Title3);
        if (hint.Length == 0)
        {
            return fullBottom;
        }

        Typography.DrawWrappedCentered(new Vector2(centerX, baseY + HintOffset * scale), hint, ui.MutedInk,
            TextStyles.Subheadline, maxWidth);
        return fullBottom;
    }

    private static float DrawCompact(Rect body, AppSkin ui, string title, string hint, float maxWidth,
        float hintHeight)
    {
        var scale = UiScale.Current;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var gap = Metrics.Space.Xs * scale;
        var showHint = hint.Length > 0 && titleHeight + gap + hintHeight <= body.Height;
        var blockHeight = showHint ? titleHeight + gap + hintHeight : titleHeight;
        var top = MathF.Max(body.Min.Y, body.Center.Y - blockHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(body.Min, body.Max, true);
        Typography.DrawCentered(new Vector2(body.Center.X, top + titleHeight * 0.5f), title, ui.TitleInk,
            TextStyles.Title3);
        if (showHint)
        {
            Typography.DrawWrappedCentered(new Vector2(body.Center.X, top + titleHeight + gap), hint, ui.MutedInk,
                TextStyles.Subheadline, maxWidth);
        }

        drawList.PopClipRect();
        return top + blockHeight;
    }
}
