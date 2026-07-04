using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ConfirmDialog
{
    private const float CardRounding = 24f;
    private const float CardPadding = 20f;
    private const float CardMaxWidth = 340f;
    private const float CardMinWidth = 280f;
    private const float ButtonHeight = 36f;
    private const float ButtonGap = 10f;

    public static void Draw(
        Rect area,
        PhoneTheme theme,
        string message,
        string confirmLabel,
        string cancelLabel,
        string submittingLabel,
        bool submitting,
        string? status,
        out bool canceled,
        out bool confirmed,
        float opacity = 1f)
    {
        canceled = false;
        confirmed = false;

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();

        var pad = CardPadding * scale;
        var cardWidth = Math.Clamp(area.Width - 40f * scale, CardMinWidth * scale, CardMaxWidth * scale);
        var wrapWidth = cardWidth - pad * 2f;

        Vector2 messageSize;
        using (Plugin.Fonts.Push(0.95f, FontWeight.Medium))
        {
            messageSize = ImGui.CalcTextSize(message, false, wrapWidth);
        }

        var buttonHeight = ButtonHeight * scale;
        var buttonGap = ButtonGap * scale;
        var cardHeight = pad + messageSize.Y + 14f * scale + buttonHeight + pad;
        var cardMin = new Vector2(area.Center.X - cardWidth * 0.5f, area.Center.Y - cardHeight * 0.5f);
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);

        var surface = Palette.WithAlpha(theme.Surface, opacity);
        var stroke = Palette.WithAlpha(theme.TextStrong, 0.08f * opacity);
        var textColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, opacity);

        Squircle.Fill(drawList, cardMin, cardMax, CardRounding * scale, ImGui.GetColorU32(surface));
        Squircle.Stroke(drawList, cardMin, cardMax, CardRounding * scale, ImGui.GetColorU32(stroke), 1f);

        using (Plugin.Fonts.Push(0.95f, FontWeight.Medium))
        {
            ImGui.SetCursorScreenPos(new Vector2(cardMin.X + pad, cardMin.Y + pad));
            ImGui.PushTextWrapPos(cardMin.X + pad + wrapWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextWrapped(message);
            }

            ImGui.PopTextWrapPos();
        }

        var buttonY = cardMax.Y - pad - buttonHeight;
        var buttonWidth = (cardWidth - pad * 2f - buttonGap) * 0.5f;
        var cancelRect = new Rect(new Vector2(cardMin.X + pad, buttonY), new Vector2(cardMin.X + pad + buttonWidth, buttonY + buttonHeight));
        var confirmRect = new Rect(new Vector2(cancelRect.Max.X + buttonGap, buttonY), new Vector2(cardMax.X - pad, buttonY + buttonHeight));

        if (DrawPillButton(cancelRect, cancelLabel, true, theme, scale, opacity))
        {
            canceled = true;
            return;
        }

        var confirmLabelEffective = submitting ? submittingLabel : confirmLabel;
        if (DrawPillButton(confirmRect, confirmLabelEffective, !submitting, theme, scale, opacity, danger: true))
        {
            confirmed = true;
            return;
        }

        if (status is { Length: > 0 })
        {
            var statusSize = Typography.Measure(status, 0.78f);
            var mutedColor = new Vector4(theme.TextMuted.X, theme.TextMuted.Y, theme.TextMuted.Z, opacity);
            using (ImRaii.PushColor(ImGuiCol.Text, mutedColor))
            {
                ImGui.SetCursorScreenPos(new Vector2(cardMin.X + pad, buttonY - 10f * scale - statusSize.Y));
                ImGui.TextUnformatted(status);
            }
        }
    }

    private static bool DrawPillButton(Rect rect, string label, bool enabled, PhoneTheme theme, float scale, float opacity, bool danger = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;

        Vector4 fill;
        Vector4 textColor;

        if (danger)
        {
            if (enabled)
            {
                fill = Palette.WithAlpha(hovered ? Palette.Mix(theme.Danger, theme.TextStrong, 0.12f) : theme.Danger, opacity);
                textColor = new Vector4(1f, 1f, 1f, opacity);
            }
            else
            {
                fill = Palette.WithAlpha(theme.Danger, 0.4f * opacity);
                textColor = new Vector4(1f, 1f, 1f, 0.4f * opacity);
            }
        }
        else
        {
            if (enabled)
            {
                fill = Palette.WithAlpha(hovered ? new Vector4(1f, 1f, 1f, 0.16f) : theme.SurfaceMuted, opacity);
                textColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, opacity);
            }
            else
            {
                fill = Palette.WithAlpha(theme.SurfaceMuted, opacity);
                textColor = new Vector4(theme.TextMuted.X, theme.TextMuted.Y, theme.TextMuted.Z, opacity);
            }
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.12f * opacity)), 1f);

        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, textColor, 0.9f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
