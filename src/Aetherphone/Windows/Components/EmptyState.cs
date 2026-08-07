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

    public static void Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint) =>
        DrawBody(body, ui, icon, title, hint);

    public static bool Draw(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint, string actionLabel)
    {
        var bottom = DrawBody(body, ui, icon, title, hint);
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

    private static float DrawBody(Rect body, AppSkin ui, FontAwesomeIcon icon, string title, string hint)
    {
        var scale = UiScale.Current;
        var centerX = body.Center.X;
        var baseY = body.Center.Y - 40f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, baseY);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        AppSkin.Icon(iconCenter, icon.ToIconString(), ui.MutedInk, 1.7f);
        var titleY = baseY + 58f * scale;
        Typography.DrawCentered(new Vector2(centerX, titleY), title, ui.TitleInk, TextStyles.Title3);
        if (hint.Length == 0)
        {
            return titleY + Typography.LineHeight(TextStyles.Title3) * 0.5f;
        }

        var hintY = baseY + 84f * scale;
        var maxWidth = MathF.Min(body.Width - 56f * scale, 300f * scale);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, maxWidth).Y;
        Typography.DrawWrappedCentered(new Vector2(centerX, hintY), hint, ui.MutedInk, TextStyles.Subheadline,
            maxWidth);
        return hintY + hintHeight;
    }
}
