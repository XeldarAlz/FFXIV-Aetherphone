using System.Collections.Generic;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal enum ConfirmButtonTone
{
    Neutral,
    Danger,
    Primary,
}

internal static class ConfirmDialog
{
    private const float CardRounding = 24f;
    private const float CardPadding = 22f;
    private const float CardMaxWidth = 360f;
    private const float CardSideMargin = 24f;
    private const float ButtonHeight = 38f;
    private const float ButtonGap = 10f;
    private const float TitleGap = 12f;
    private const float MessageGap = 18f;
    private const float StatusGap = 10f;
    private const float LineLeading = 4f;
    private const float TitleScale = 1.55f;
    private const float MessageScale = 0.92f;
    private const float ButtonScale = 0.9f;

    private static readonly List<string> LineBuffer = new();

    public static void Draw(Rect area, PhoneTheme theme, string? title, string message, string confirmLabel,
        string cancelLabel, string busyLabel, bool busy, string? status, bool danger, bool acknowledge, float opacity,
        float cardScale, out Rect cardRect, out bool canceled, out bool confirmed)
    {
        canceled = false;
        confirmed = false;
        var scale = ImGuiHelpers.GlobalScale;
        var s = scale * cardScale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = CardPadding * s;
        var available = area.Width - CardSideMargin * 2f * scale;
        var cardWidth = MathF.Min(CardMaxWidth * scale, available) * cardScale;
        var wrapWidth = cardWidth - pad * 2f;

        var hasTitle = !string.IsNullOrEmpty(title);
        var titleScale = TitleScale * cardScale;
        var messageScale = MessageScale * cardScale;

        var titleHeight = hasTitle ? Typography.Measure(title!, titleScale, FontWeight.Bold).Y : 0f;
        var lineHeight = WrapMessage(message, wrapWidth, messageScale, FontWeight.Medium);
        var lineStep = lineHeight + LineLeading * s;
        var lineCount = LineBuffer.Count;
        var messageBlockHeight = lineCount > 0 ? lineHeight + (lineCount - 1) * lineStep : 0f;

        var hasStatus = status is { Length: > 0 };
        var statusHeight = hasStatus ? Typography.Measure(status!, 0.78f * cardScale).Y : 0f;

        var buttonHeight = ButtonHeight * s;
        var titlePart = hasTitle ? titleHeight + TitleGap * s : 0f;
        var statusPart = hasStatus ? StatusGap * s + statusHeight : 0f;
        var cardHeight = pad + titlePart + messageBlockHeight + statusPart + MessageGap * s + buttonHeight + pad;

        var cardMin = new Vector2(area.Center.X - cardWidth * 0.5f, area.Center.Y - cardHeight * 0.5f);
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);
        cardRect = new Rect(cardMin, cardMax);

        var surface = Palette.WithAlpha(theme.Surface, opacity);
        var stroke = Palette.WithAlpha(theme.TextStrong, 0.08f * opacity);
        Squircle.Fill(drawList, cardMin, cardMax, CardRounding * s, ImGui.GetColorU32(surface));
        Squircle.Stroke(drawList, cardMin, cardMax, CardRounding * s, ImGui.GetColorU32(stroke), 1f);

        var centerX = area.Center.X;
        var cursorY = cardMin.Y + pad;
        if (hasTitle)
        {
            var titleColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, opacity);
            Typography.DrawCentered(drawList, new Vector2(centerX, cursorY + titleHeight * 0.5f), title!, titleColor,
                titleScale, FontWeight.Bold);
            cursorY += titleHeight + TitleGap * s;
        }

        var messageColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, 0.88f * opacity);
        DrawMessage(drawList, centerX, cursorY, lineStep, messageColor, messageScale);
        cursorY += messageBlockHeight;

        if (hasStatus)
        {
            cursorY += StatusGap * s;
            var mutedColor = new Vector4(theme.TextMuted.X, theme.TextMuted.Y, theme.TextMuted.Z, opacity);
            Typography.DrawCentered(drawList, new Vector2(centerX, cursorY + statusHeight * 0.5f), status!, mutedColor,
                0.78f * cardScale);
        }

        var buttonY = cardMax.Y - pad - buttonHeight;
        if (acknowledge)
        {
            var acknowledgeRect = new Rect(new Vector2(cardMin.X + pad, buttonY),
                new Vector2(cardMax.X - pad, buttonY + buttonHeight));
            if (DrawPillButton(acknowledgeRect, confirmLabel, true, theme, cardScale, opacity, ConfirmButtonTone.Primary))
            {
                confirmed = true;
            }

            return;
        }

        var buttonGap = ButtonGap * s;
        var buttonWidth = (cardWidth - pad * 2f - buttonGap) * 0.5f;
        var cancelRect = new Rect(new Vector2(cardMin.X + pad, buttonY),
            new Vector2(cardMin.X + pad + buttonWidth, buttonY + buttonHeight));
        var confirmRect = new Rect(new Vector2(cancelRect.Max.X + buttonGap, buttonY),
            new Vector2(cardMax.X - pad, buttonY + buttonHeight));
        if (DrawPillButton(cancelRect, cancelLabel, !busy, theme, cardScale, opacity))
        {
            canceled = true;
        }

        var confirmLabelEffective = busy ? busyLabel : confirmLabel;
        var confirmTone = danger ? ConfirmButtonTone.Danger : ConfirmButtonTone.Neutral;
        if (DrawPillButton(confirmRect, confirmLabelEffective, !busy, theme, cardScale, opacity, confirmTone))
        {
            confirmed = true;
        }
    }

    private static void DrawMessage(ImDrawListPtr drawList, float centerX, float top, float lineStep,
        Vector4 color, float messageScale)
    {
        using (Plugin.Fonts.Push(messageScale, FontWeight.Medium))
        {
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var colorU32 = ImGui.GetColorU32(color);
            for (var lineIndex = 0; lineIndex < LineBuffer.Count; lineIndex++)
            {
                var line = LineBuffer[lineIndex];
                if (line.Length == 0)
                {
                    continue;
                }

                var width = ImGui.CalcTextSize(line).X;
                var position = new Vector2(centerX - width * 0.5f, top + lineIndex * lineStep);
                drawList.AddText(font, fontSize, position, colorU32, line);
            }
        }
    }

    private static float WrapMessage(string message, float wrapWidth, float scale, FontWeight weight)
    {
        LineBuffer.Clear();
        float lineHeight;
        using (Plugin.Fonts.Push(scale, weight))
        {
            lineHeight = ImGui.GetTextLineHeight();
            var paragraphs = message.Split('\n');
            for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                WrapParagraph(paragraphs[paragraphIndex], wrapWidth);
            }
        }

        return lineHeight;
    }

    private static void WrapParagraph(string text, float wrapWidth)
    {
        if (text.Length == 0)
        {
            LineBuffer.Add(string.Empty);
            return;
        }

        var lineStart = 0;
        var lineWidth = 0f;
        var lastSpace = -1;
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            var advance = ImGui.CalcTextSize(character.ToString()).X;
            if (character == ' ')
            {
                lastSpace = index;
            }

            if (lineWidth + advance > wrapWidth && index > lineStart)
            {
                if (lastSpace > lineStart)
                {
                    LineBuffer.Add(text.Substring(lineStart, lastSpace - lineStart));
                    index = lastSpace + 1;
                    lineStart = index;
                }
                else
                {
                    LineBuffer.Add(text.Substring(lineStart, index - lineStart));
                    lineStart = index;
                }

                lineWidth = 0f;
                lastSpace = -1;
                continue;
            }

            lineWidth += advance;
            index++;
        }

        if (lineStart < text.Length)
        {
            LineBuffer.Add(text.Substring(lineStart));
        }
    }

    public static bool DrawPillButton(Rect rect, string label, bool enabled, PhoneTheme theme, float cardScale,
        float opacity, ConfirmButtonTone tone = ConfirmButtonTone.Neutral)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        Vector4 fill;
        Vector4 textColor;
        switch (tone)
        {
            case ConfirmButtonTone.Danger:
                fill = enabled
                    ? Palette.WithAlpha(hovered ? Palette.Mix(theme.Danger, theme.TextStrong, 0.12f) : theme.Danger,
                        opacity)
                    : Palette.WithAlpha(theme.Danger, 0.4f * opacity);
                textColor = new Vector4(1f, 1f, 1f, enabled ? opacity : 0.4f * opacity);
                break;
            case ConfirmButtonTone.Primary:
                fill = enabled
                    ? Palette.WithAlpha(hovered ? Palette.Mix(theme.Accent, theme.TextStrong, 0.14f) : theme.Accent,
                        opacity)
                    : Palette.WithAlpha(theme.Accent, 0.4f * opacity);
                textColor = new Vector4(1f, 1f, 1f, enabled ? opacity : 0.4f * opacity);
                break;
            default:
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

                break;
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.12f * opacity)), 1f);
        var textSize = Typography.Measure(label, ButtonScale * cardScale, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, textColor, ButtonScale * cardScale, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
