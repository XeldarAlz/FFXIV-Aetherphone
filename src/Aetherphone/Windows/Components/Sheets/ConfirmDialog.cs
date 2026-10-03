using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

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
    private const float SectionGap = 14f;
    private const float SectionCardPad = 12f;
    private const float SectionCardRounding = 14f;
    private const float SectionLabelGap = 5f;
    private const float ChipPadX = 8f;
    private const float ChipPadY = 4f;

    private static readonly List<string> LineBuffer = new();
    private const int AdvanceCacheLimit = 512;
    private static readonly Dictionary<(char Character, float FontSize), float> AdvanceCache = new();
    private static int advanceCacheGeneration = -1;

    public static void Draw(Rect area, PhoneTheme theme, string? title, string message, ConfirmSection[]? sections,
        string confirmLabel, string cancelLabel, string busyLabel, bool busy, string? status, bool danger,
        bool acknowledge, float opacity, float cardScale, out Rect cardRect, out bool canceled, out bool confirmed)
    {
        canceled = false;
        confirmed = false;
        var scale = UiScale.Current;
        var s = scale * cardScale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = CardPadding * s;
        var available = area.Width - CardSideMargin * 2f * scale;
        var cardWidth = MathF.Min(CardMaxWidth * scale, available) * cardScale;
        var wrapWidth = cardWidth - pad * 2f;

        var hasTitle = !string.IsNullOrEmpty(title);
        var titleScale = TitleScale * cardScale;
        var messageScale = MessageScale * cardScale;

        var titleStyle = new TextStyle(titleScale, FontWeight.Bold);
        var titleHeight = hasTitle ? Typography.MeasureWrappedBlock(title!, titleStyle, wrapWidth).Y : 0f;
        var hasSections = sections is { Length: > 0 };
        float messageBlockHeight;
        var lineStep = 0f;
        if (hasSections)
        {
            messageBlockHeight = SectionsHeight(sections!, wrapWidth, cardScale, s);
        }
        else
        {
            var lineHeight = WrapMessage(message, wrapWidth, messageScale, FontWeight.Medium);
            lineStep = lineHeight + LineLeading * s;
            var lineCount = LineBuffer.Count;
            messageBlockHeight = lineCount > 0 ? lineHeight + (lineCount - 1) * lineStep : 0f;
        }

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
            Typography.DrawWrappedCentered(new Vector2(centerX, cursorY), title!, titleColor, titleStyle, wrapWidth);
            cursorY += titleHeight + TitleGap * s;
        }

        var messageColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, 0.88f * opacity);
        if (hasSections)
        {
            DrawSections(drawList, sections!, cardMin.X + pad, centerX, cursorY, wrapWidth, cardScale, s, theme,
                opacity);
        }
        else
        {
            DrawMessage(drawList, centerX, cursorY, lineStep, messageColor, messageScale);
        }

        cursorY += messageBlockHeight;

        if (hasStatus)
        {
            cursorY += StatusGap * s;
            var mutedColor = new Vector4(theme.TextMuted.X, theme.TextMuted.Y, theme.TextMuted.Z, opacity);
            Typography.DrawCentered(drawList, new Vector2(centerX, cursorY + statusHeight * 0.5f),
                Typography.FitText(status!, wrapWidth, 0.78f * cardScale, FontWeight.Regular), mutedColor,
                0.78f * cardScale);
        }

        var buttonY = cardMax.Y - pad - buttonHeight;
        if (acknowledge || string.IsNullOrEmpty(cancelLabel))
        {
            var acknowledgeRect = new Rect(new Vector2(cardMin.X + pad, buttonY),
                new Vector2(cardMax.X - pad, buttonY + buttonHeight));
            if (DrawPillButton(acknowledgeRect, confirmLabel, true, theme, cardScale, opacity,
                    ConfirmButtonTone.Primary, "confirmdialog.acknowledge"))
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
        if (DrawPillButton(cancelRect, cancelLabel, !busy, theme, cardScale, opacity, ConfirmButtonTone.Neutral,
                "confirmdialog.cancel"))
        {
            canceled = true;
        }

        var confirmLabelEffective = busy ? busyLabel : confirmLabel;
        var confirmTone = danger ? ConfirmButtonTone.Danger : ConfirmButtonTone.Neutral;
        if (DrawPillButton(confirmRect, confirmLabelEffective, !busy, theme, cardScale, opacity, confirmTone,
                "confirmdialog.confirm"))
        {
            confirmed = true;
        }
    }

    private const float SheetMargin = 10f;
    private const float SheetRounding = 20f;
    private const float SheetActionHeight = 50f;
    private const float SheetCancelHeight = 52f;
    private const float SheetGap = 8f;
    private const float SheetBottomInset = 12f;
    private const float SheetPadX = 18f;
    private const float SheetHeaderPadY = 13f;
    private const float SheetHeaderLineGap = 4f;

    private static readonly TextStyle SheetActionStyle = new(1.07f, FontWeight.SemiBold);
    private static readonly TextStyle SheetCancelStyle = new(1.07f, FontWeight.Bold);
    private static readonly TextStyle SheetHeaderStyle = new(0.82f, FontWeight.SemiBold);
    private static readonly TextStyle SheetMessageStyle = new(0.82f, FontWeight.Regular);
    private const float SheetRowPressAlpha = 0.11f;

    private static readonly Vector4 SheetRowHover = new(1f, 1f, 1f, 0.06f);

    public static void DrawSheet(Rect area, PhoneTheme theme, string? title, string message, string confirmLabel,
        string cancelLabel, string busyLabel, bool busy, string? status, bool danger, float opacity,
        out Rect sheetRect, out bool canceled, out bool confirmed)
    {
        canceled = false;
        confirmed = false;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var style = ActionSheetStyle.From(theme);
        var margin = SheetMargin * scale;
        var padX = SheetPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var headerWidth = right - left - padX * 2f;
        var hasTitle = title is { Length: > 0 };
        var titleHeight = hasTitle ? Typography.MeasureWrappedBlock(title!, SheetHeaderStyle, headerWidth).Y : 0f;
        var hasMessage = message.Length > 0;
        var messageHeight = hasMessage
            ? Typography.MeasureWrappedBlock(message, SheetMessageStyle, headerWidth).Y
            : 0f;
        var hasStatus = status is { Length: > 0 };
        var statusHeight = hasStatus
            ? Typography.MeasureWrappedBlock(status!, SheetMessageStyle, headerWidth).Y
            : 0f;
        var lineGap = SheetHeaderLineGap * scale;
        var headerHeight = SheetHeaderPadY * 2f * scale + titleHeight
            + (hasMessage ? lineGap + messageHeight : 0f)
            + (hasStatus ? lineGap + statusHeight : 0f);
        var actionHeight = SheetActionHeight * scale;
        var cancelHeight = SheetCancelHeight * scale;
        var gap = SheetGap * scale;
        var cardHeight = headerHeight + actionHeight;
        var total = cardHeight + gap + cancelHeight;
        var bottom = area.Max.Y - SheetBottomInset * scale + total * (1f - opacity);
        var cancelMin = new Vector2(left, bottom - cancelHeight);
        var cancelMax = new Vector2(right, bottom);
        var cardMax = new Vector2(right, cancelMin.Y - gap);
        var cardMin = new Vector2(left, cardMax.Y - cardHeight);
        sheetRect = new Rect(cardMin, cancelMax);
        var rounding = SheetRounding * scale;
        DrawSheetPanel(drawList, cardMin, cardMax, rounding, style, opacity, scale);
        var centerX = (cardMin.X + cardMax.X) * 0.5f;
        var cursorY = cardMin.Y + SheetHeaderPadY * scale;
        var headerInk = Palette.WithAlpha(style.Ink, style.Ink.W * 0.65f * opacity);
        if (hasTitle)
        {
            Typography.DrawWrappedCentered(drawList, new Vector2(centerX, cursorY + titleHeight * 0.5f), title!,
                headerInk, SheetHeaderStyle, headerWidth);
            cursorY += titleHeight;
        }

        if (hasMessage)
        {
            cursorY += hasTitle ? lineGap : 0f;
            Typography.DrawWrappedCentered(drawList, new Vector2(centerX, cursorY + messageHeight * 0.5f), message,
                headerInk, SheetMessageStyle, headerWidth);
            cursorY += messageHeight;
        }

        if (hasStatus)
        {
            cursorY += lineGap;
            Typography.DrawWrappedCentered(drawList, new Vector2(centerX, cursorY + statusHeight * 0.5f), status!,
                Palette.WithAlpha(style.Danger, style.Danger.W * opacity), SheetMessageStyle, headerWidth);
        }

        var actionMin = new Vector2(cardMin.X, cardMax.Y - actionHeight);
        drawList.AddLine(new Vector2(actionMin.X + padX, actionMin.Y), new Vector2(cardMax.X - padX, actionMin.Y),
            ImGui.GetColorU32(Palette.WithAlpha(style.Hairline, style.Hairline.W * opacity)), 1f);
        var interactive = !busy && opacity > 0.5f;
        var actionHovered = interactive && UiInteract.Hover(actionMin, cardMax);
        if (actionHovered)
        {
            DrawSheetRowHighlight(drawList, actionMin, cardMax, rounding, opacity,
                ImDrawFlags.RoundCornersBottom);
        }

        var actionInk = busy ? Palette.WithAlpha(style.Ink, style.Ink.W * 0.5f)
            : danger ? style.Danger : style.Accent;
        Typography.DrawCentered(drawList, new Vector2(centerX, (actionMin.Y + cardMax.Y) * 0.5f),
            busy ? busyLabel : confirmLabel, Palette.WithAlpha(actionInk, actionInk.W * opacity), SheetActionStyle);
        if (UiInteract.Click(actionMin, cardMax, actionHovered))
        {
            confirmed = true;
        }

        DrawSheetPanel(drawList, cancelMin, cancelMax, rounding, style, opacity, scale);
        var cancelHovered = interactive && UiInteract.Hover(cancelMin, cancelMax);
        if (cancelHovered)
        {
            DrawSheetRowHighlight(drawList, cancelMin, cancelMax, rounding, opacity, ImDrawFlags.RoundCornersAll);
        }

        Typography.DrawCentered(drawList, new Vector2((cancelMin.X + cancelMax.X) * 0.5f,
                (cancelMin.Y + cancelMax.Y) * 0.5f), cancelLabel,
            Palette.WithAlpha(style.Ink, style.Ink.W * opacity), SheetCancelStyle);
        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            canceled = true;
        }
    }

    private static void DrawSheetRowHighlight(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        float opacity, ImDrawFlags corners)
    {
        var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? SheetRowPressAlpha : SheetRowHover.W;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(SheetRowHover, alpha * opacity)),
            rounding, corners);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
    }

    private static void DrawSheetPanel(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding,
        in ActionSheetStyle style, float opacity, float scale)
    {
        Elevation.Floating(drawList, min, max, rounding, scale, opacity);
        Squircle.Fill(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Panel, style.Panel.W * opacity)));
        Squircle.Stroke(drawList, min, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(style.Stroke, style.Stroke.W * opacity)), Metrics.Stroke.Hairline);
    }

    private static TextStyle SectionParagraphStyle(float cardScale) => new(0.92f * cardScale, FontWeight.Medium);

    private static TextStyle SectionLabelStyle(float cardScale) => new(0.95f * cardScale, FontWeight.SemiBold);

    private static TextStyle SectionTextStyle(float cardScale) => new(0.88f * cardScale, FontWeight.Regular);

    private static TextStyle SectionChipStyle(float cardScale) => new(0.85f * cardScale, FontWeight.Medium);

    private static float SectionsHeight(ConfirmSection[] sections, float wrapWidth, float cardScale, float s)
    {
        var height = 0f;
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            if (sectionIndex > 0)
            {
                height += SectionGap * s;
            }

            height += SectionHeight(sections[sectionIndex], wrapWidth, cardScale, s);
        }

        return height;
    }

    private static float SectionHeight(in ConfirmSection section, float wrapWidth, float cardScale, float s)
    {
        switch (section.Kind)
        {
            case ConfirmSectionKind.Divider:
                return Metrics.Stroke.Hairline;
            case ConfirmSectionKind.Card:
            {
                var innerWidth = wrapWidth - SectionCardPad * 2f * s;
                var height = Typography.MeasureWrappedBlock(section.Label, SectionLabelStyle(cardScale), innerWidth).Y;
                if (section.Text.Length > 0)
                {
                    height += SectionLabelGap * s
                        + Typography.MeasureWrappedBlock(section.Text, SectionTextStyle(cardScale), innerWidth).Y;
                }

                return height + SectionCardPad * 2f * s;
            }
            case ConfirmSectionKind.Labeled:
            {
                var height = Typography.MeasureWrappedBlock(section.Label, SectionLabelStyle(cardScale), wrapWidth).Y;
                if (section.Text.Length > 0)
                {
                    height += SectionLabelGap * s
                        + Typography.MeasureWrappedBlock(section.Text, SectionTextStyle(cardScale), wrapWidth).Y;
                }

                return height;
            }
            case ConfirmSectionKind.Chip:
            {
                var labelHeight = Typography.MeasureWrappedBlock(section.Label, SectionLabelStyle(cardScale),
                    wrapWidth).Y;
                var chipInner = wrapWidth - ChipPadX * 2f * s;
                var chipHeight = Typography.MeasureWrappedBlock(section.Text, SectionChipStyle(cardScale), chipInner).Y
                    + ChipPadY * 2f * s;
                return labelHeight + SectionLabelGap * s + chipHeight;
            }
            default:
                return Typography.MeasureWrappedBlock(section.Text, SectionParagraphStyle(cardScale), wrapWidth).Y;
        }
    }

    private static void DrawSections(ImDrawListPtr drawList, ConfirmSection[] sections, float left, float centerX,
        float top, float wrapWidth, float cardScale, float s, PhoneTheme theme, float opacity)
    {
        var y = top;
        var strongColor = Palette.WithAlpha(theme.TextStrong, opacity);
        var bodyColor = Palette.WithAlpha(theme.TextStrong, 0.88f * opacity);
        var mutedColor = Palette.WithAlpha(theme.TextMuted, opacity);
        var hairline = ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity));
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            if (sectionIndex > 0)
            {
                y += SectionGap * s;
            }

            var section = sections[sectionIndex];
            switch (section.Kind)
            {
                case ConfirmSectionKind.Divider:
                    drawList.AddRectFilled(new Vector2(left, y),
                        new Vector2(left + wrapWidth, y + Metrics.Stroke.Hairline), hairline);
                    y += Metrics.Stroke.Hairline;
                    break;
                case ConfirmSectionKind.Card:
                {
                    var height = SectionHeight(section, wrapWidth, cardScale, s);
                    Squircle.Fill(drawList, new Vector2(left, y), new Vector2(left + wrapWidth, y + height),
                        SectionCardRounding * s,
                        ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.7f * opacity)));
                    var innerLeft = left + SectionCardPad * s;
                    var innerWidth = wrapWidth - SectionCardPad * 2f * s;
                    var innerY = y + SectionCardPad * s;
                    innerY += Typography.DrawWrappedLeft(new Vector2(innerLeft, innerY), section.Label, strongColor,
                        SectionLabelStyle(cardScale), innerWidth);
                    if (section.Text.Length > 0)
                    {
                        innerY += SectionLabelGap * s;
                        Typography.DrawWrappedLeft(new Vector2(innerLeft, innerY), section.Text, mutedColor,
                            SectionTextStyle(cardScale), innerWidth);
                    }

                    y += height;
                    break;
                }
                case ConfirmSectionKind.Labeled:
                {
                    y += Typography.DrawWrappedLeft(new Vector2(left, y), section.Label, strongColor,
                        SectionLabelStyle(cardScale), wrapWidth);
                    if (section.Text.Length > 0)
                    {
                        y += SectionLabelGap * s;
                        y += Typography.DrawWrappedLeft(new Vector2(left, y), section.Text, bodyColor,
                            SectionTextStyle(cardScale), wrapWidth);
                    }

                    break;
                }
                case ConfirmSectionKind.Chip:
                {
                    y += Typography.DrawWrappedLeft(new Vector2(left, y), section.Label, strongColor,
                        SectionLabelStyle(cardScale), wrapWidth);
                    y += SectionLabelGap * s;
                    var chipInner = wrapWidth - ChipPadX * 2f * s;
                    var chipStyle = SectionChipStyle(cardScale);
                    var textBlock = Typography.MeasureWrappedBlock(section.Text, chipStyle, chipInner);
                    var chipWidth = MathF.Min(wrapWidth, textBlock.X + ChipPadX * 2f * s);
                    var chipHeight = textBlock.Y + ChipPadY * 2f * s;
                    Squircle.Fill(drawList, new Vector2(left, y), new Vector2(left + chipWidth, y + chipHeight),
                        6f * s, ImGui.GetColorU32(Palette.WithAlpha(theme.SurfaceMuted, 0.9f * opacity)));
                    Typography.DrawWrappedLeft(new Vector2(left + ChipPadX * s, y + ChipPadY * s), section.Text,
                        bodyColor, chipStyle, chipInner);
                    y += chipHeight;
                    break;
                }
                default:
                    y += Typography.DrawWrappedCentered(new Vector2(centerX, y), section.Text, bodyColor,
                        SectionParagraphStyle(cardScale), wrapWidth);
                    break;
            }
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
        var fontSize = ImGui.GetFontSize();
        while (index < text.Length)
        {
            var character = text[index];
            var advance = AdvanceOf(character, fontSize);
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

    private static float AdvanceOf(char character, float fontSize)
    {
        var generation = Plugin.Fonts.Generation;
        if (generation != advanceCacheGeneration)
        {
            advanceCacheGeneration = generation;
            AdvanceCache.Clear();
        }

        var key = (character, fontSize);
        if (AdvanceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (AdvanceCache.Count >= AdvanceCacheLimit)
        {
            AdvanceCache.Clear();
        }

        var advance = ImGui.CalcTextSize(character.ToString()).X;
        AdvanceCache[key] = advance;
        return advance;
    }

    public static bool DrawPillButton(Rect rect, string label, bool enabled, PhoneTheme theme, float cardScale,
        float opacity, ConfirmButtonTone tone = ConfirmButtonTone.Neutral, string? id = null)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
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
                    fill = Palette.WithAlpha(
                        hovered ? Palette.Mix(theme.SurfaceMuted, theme.TextStrong, 0.16f) : theme.SurfaceMuted,
                        opacity);
                    textColor = new Vector4(theme.TextStrong.X, theme.TextStrong.Y, theme.TextStrong.Z, opacity);
                }
                else
                {
                    fill = Palette.WithAlpha(theme.SurfaceMuted, opacity);
                    textColor = new Vector4(theme.TextMuted.X, theme.TextMuted.Y, theme.TextMuted.Z, opacity);
                }

                break;
        }

        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id ?? label, pressed);
        var pressHalf = new Vector2(rect.Width, rect.Height) * 0.5f * press;
        var pressMin = rect.Center - pressHalf;
        var pressMax = rect.Center + pressHalf;
        Squircle.Fill(drawList, pressMin, pressMax, radius * press, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, pressMin, pressMax, radius * press,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.12f * opacity)), 1f);
        var style = new TextStyle(ButtonScale * cardScale, FontWeight.SemiBold);
        var maxLabelWidth = MathF.Max(1f, rect.Width - rect.Height);
        if (id is not null)
        {
            var labelHeight = Typography.Measure(label, style).Y;
            Marquee.DrawCenteredAuto(id, label, rect.Center.X, rect.Center.Y - labelHeight * 0.5f, maxLabelWidth,
                style, textColor);
        }
        else
        {
            var fitted = Typography.FitText(label, maxLabelWidth, style);
            var textSize = Typography.Measure(fitted, style);
            Typography.Draw(rect.Center - textSize * 0.5f, fitted, textColor, style.Scale, style.Weight);
        }
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
