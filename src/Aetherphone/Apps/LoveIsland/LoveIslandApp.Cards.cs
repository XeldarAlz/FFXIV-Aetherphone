using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.LoveIsland;

internal enum CardPill : byte
{
    None,
    Live,
    Off,
    Accent,
}

internal readonly record struct CardButton(string Label, bool Prominent, string Id);

internal readonly record struct CardContent(string Id, string Eyebrow, CardPill Pill, string PillLabel, string Title,
    string Body, int TitleLines, int BodyLines, bool Tappable, bool LargeTitle = false);

internal sealed partial class LoveIslandApp
{
    public const int CardTapped = -2;
    public const int CardIdle = -1;
    private const float LoadingOffset = 120f;
    private const float LoadingRadius = 13f;
    private const float ButtonPadX = 18f;
    private const int DefaultTitleLines = 3;

    private readonly Dictionary<string, ClampedLines> titleClamps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClampedLines> bodyClamps = new(StringComparer.Ordinal);
    private readonly CardButton[] cardButtons = new CardButton[2];
    private readonly Rect[] cardButtonRects = new Rect[2];

    private ClampedLines ClampFor(Dictionary<string, ClampedLines> cache, string id)
    {
        if (!cache.TryGetValue(id, out var clamp))
        {
            clamp = new ClampedLines();
            cache[id] = clamp;
        }

        return clamp;
    }

    private float DrawCard(ImDrawListPtr drawList, float left, float top, float width, in CardContent content,
        int buttonCount, out int action)
    {
        action = CardIdle;
        var scale = UiScale.Current;
        var pad = LoveIslandKit.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        var titleStyle = content.LargeTitle ? TextStyles.Title1 : LoveIslandKit.CardTitleStyle;
        var titleLines = content.Title.Length == 0
            ? Array.Empty<string>()
            : ClampFor(titleClamps, content.Id).Get(content.Title, titleStyle, inner,
                content.TitleLines > 0 ? content.TitleLines : DefaultTitleLines, fontKey);
        var bodyLines = content.Body.Length == 0
            ? Array.Empty<string>()
            : ClampFor(bodyClamps, content.Id).Get(content.Body, LoveIslandKit.CardBodyStyle, inner,
                content.BodyLines > 0 ? content.BodyLines : int.MaxValue, fontKey);
        var eyebrowHeight = content.Eyebrow.Length > 0 || content.Pill != CardPill.None
            ? MathF.Max(Typography.LineHeight(LoveIslandKit.EyebrowStyle),
                content.Pill != CardPill.None ? LoveIslandKit.PillHeight(scale) : 0f)
            : 0f;
        var titleLineHeight = Typography.LineHeight(titleStyle);
        var bodyLineHeight = Typography.LineHeight(LoveIslandKit.CardBodyStyle);
        var height = pad * 2f + eyebrowHeight;
        if (titleLines.Length > 0)
        {
            height += (eyebrowHeight > 0f ? LoveIslandKit.EyebrowGap * scale : 0f) + titleLines.Length * titleLineHeight;
        }

        if (bodyLines.Length > 0)
        {
            height += LoveIslandKit.TitleGap * scale + bodyLines.Length * bodyLineHeight;
        }

        if (buttonCount > 0)
        {
            height += (LoveIslandKit.ButtonRowGap + LoveIslandKit.ButtonHeight) * scale;
        }

        var rest = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        LayoutButtons(rest, buttonCount, scale);
        var overButton = false;
        for (var index = 0; index < buttonCount; index++)
        {
            overButton |= UiInteract.Hover(cardButtonRects[index].Min, cardButtonRects[index].Max);
        }

        var visible = ImGui.IsRectVisible(rest.Min, rest.Max);
        if (!visible)
        {
            return rest.Max.Y;
        }

        if (LoveIslandKit.PressableCard(drawList, ui, rest, content.Id, scale, content.Tappable && !overButton,
                out _))
        {
            action = CardTapped;
        }

        var cursorY = rest.Min.Y + pad;
        var textLeft = rest.Min.X + pad;
        if (eyebrowHeight > 0f)
        {
            DrawEyebrowRow(drawList, textLeft, cursorY, inner, eyebrowHeight, in content, scale);
            cursorY += eyebrowHeight;
        }

        if (titleLines.Length > 0)
        {
            cursorY += eyebrowHeight > 0f ? LoveIslandKit.EyebrowGap * scale : 0f;
            for (var lineIndex = 0; lineIndex < titleLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), titleLines[lineIndex], ui.TitleInk,
                    titleStyle);
                cursorY += titleLineHeight;
            }
        }

        if (bodyLines.Length > 0)
        {
            cursorY += LoveIslandKit.TitleGap * scale;
            for (var lineIndex = 0; lineIndex < bodyLines.Length; lineIndex++)
            {
                Typography.Draw(drawList, new Vector2(textLeft, cursorY), bodyLines[lineIndex], ui.MutedInk,
                    LoveIslandKit.CardBodyStyle);
                cursorY += bodyLineHeight;
            }
        }

        for (var index = 0; index < buttonCount; index++)
        {
            var button = cardButtons[index];
            if (Button.Draw(drawList, cardButtonRects[index], button.Label, ui.Ink,
                    button.Prominent ? ButtonStyle.Prominent : ButtonStyle.Gray, id: button.Id))
            {
                action = index;
            }
        }

        return rest.Max.Y;
    }

    private void DrawEyebrowRow(ImDrawListPtr drawList, float left, float top, float width, float rowHeight,
        in CardContent content, float scale)
    {
        var cursorX = left;
        var centerY = top + rowHeight * 0.5f;
        if (content.Eyebrow.Length > 0)
        {
            var eyebrow = Typography.FitText(content.Eyebrow, width * 0.7f, LoveIslandKit.EyebrowStyle);
            var size = Typography.Measure(eyebrow, LoveIslandKit.EyebrowStyle);
            Typography.Draw(drawList, new Vector2(cursorX, centerY - size.Y * 0.5f), eyebrow, ui.Accent,
                LoveIslandKit.EyebrowStyle);
            cursorX += size.X + Metrics.Space.Sm * scale;
        }

        var pillTop = centerY - LoveIslandKit.PillHeight(scale) * 0.5f;
        switch (content.Pill)
        {
            case CardPill.Live:
                LoveIslandKit.LivePill(drawList, new Vector2(cursorX, pillTop), content.PillLabel, scale);
                break;
            case CardPill.Off:
                LoveIslandKit.OffPill(drawList, new Vector2(cursorX, pillTop), content.PillLabel, scale);
                break;
            case CardPill.Accent:
                LoveIslandKit.StagePill(drawList, new Vector2(cursorX, pillTop), content.PillLabel, ui.Accent, scale);
                break;
        }
    }

    private void LayoutButtons(Rect card, int buttonCount, float scale)
    {
        if (buttonCount == 0)
        {
            return;
        }

        var pad = LoveIslandKit.CardPad * scale;
        var height = LoveIslandKit.ButtonHeight * scale;
        var top = card.Max.Y - pad - height;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var gap = LoveIslandKit.ButtonGap * scale;
        var available = right - left;
        var total = 0f;
        for (var index = 0; index < buttonCount; index++)
        {
            total += NaturalButtonWidth(cardButtons[index].Label, scale);
        }

        total += gap * (buttonCount - 1);
        var shrink = total > available ? available / total : 1f;
        var cursorX = left;
        for (var index = 0; index < buttonCount; index++)
        {
            var buttonWidth = NaturalButtonWidth(cardButtons[index].Label, scale) * shrink;
            cardButtonRects[index] = new Rect(new Vector2(cursorX, top), new Vector2(cursorX + buttonWidth, top + height));
            cursorX += buttonWidth + gap * shrink;
        }
    }

    private static float NaturalButtonWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + ButtonPadX * 2f * scale;

    private float DrawPageTitle(ImDrawListPtr drawList, float left, float top, float width, string title, string sub)
    {
        var scale = UiScale.Current;
        var cursorY = top + Typography.DrawWrappedLeft(new Vector2(left, top), title, ui.TitleInk,
            LoveIslandKit.PageTitleStyle, width);
        if (sub.Length == 0)
        {
            return cursorY + Metrics.Space.Md * scale;
        }

        cursorY += Metrics.Space.Xxs * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), sub, ui.MutedInk, LoveIslandKit.PageSubStyle,
            width);
        return cursorY + Metrics.Space.Lg * scale;
    }

    private float DrawMutedNote(float left, float top, float width, string text)
    {
        return top + Typography.DrawWrappedLeft(new Vector2(left, top), text, ui.MutedInk, TextStyles.Footnote, width);
    }

    private bool DrawWideButton(ImDrawListPtr drawList, float left, float top, float width, string label, string id,
        bool prominent, out float bottom)
    {
        var scale = UiScale.Current;
        var rect = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + Button.LargeHeight * scale));
        bottom = rect.Max.Y;
        return Button.Draw(drawList, rect, label, ui.Ink, prominent ? ButtonStyle.Prominent : ButtonStyle.Gray, id: id);
    }

    private static void Reserve(Vector2 origin, float width, float bottom)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, MathF.Max(0f, bottom - origin.Y)));
    }

    private float DrawToggleRow(ImDrawListPtr drawList, float left, float top, float width, string id, string glyph,
        string label, string hint, bool value, out bool result)
    {
        var scale = UiScale.Current;
        var pad = LoveIslandKit.CardPad * scale;
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var glyphSize = glyph.Length > 0 ? 18f * scale : 0f;
        var textLeft = left + pad + (glyphSize > 0f ? glyphSize + Metrics.Space.Sm * scale : 0f);
        var textWidth = MathF.Max(1f, left + width - pad - toggleWidth - Metrics.Space.Md * scale - textLeft);
        var labelHeight = Typography.MeasureWrappedBlock(label, TextStyles.BodyEmphasized, textWidth).Y;
        var hintHeight = hint.Length > 0
            ? Metrics.Space.Xxs * scale + Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y
            : 0f;
        var height = pad * 2f + MathF.Max(labelHeight + hintHeight, toggleHeight);
        var card = new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
        ui.Card(drawList, card.Min, card.Max, LoveIslandKit.CardRadius * scale);
        var cursorY = top + pad;
        if (glyphSize > 0f)
        {
            PhoneIcon.Draw(drawList, new Vector2(left + pad + glyphSize * 0.5f,
                cursorY + Typography.LineHeight(TextStyles.BodyEmphasized) * 0.5f), glyph, ui.Accent, glyphSize);
        }

        cursorY += Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), label, ui.TitleInk,
            TextStyles.BodyEmphasized, textWidth);
        if (hint.Length > 0)
        {
            cursorY += Metrics.Space.Xxs * scale;
            Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), hint, ui.MutedInk, TextStyles.Footnote,
                textWidth);
        }

        var toggle = new Rect(new Vector2(card.Max.X - pad - toggleWidth, card.Center.Y - toggleHeight * 0.5f),
            new Vector2(card.Max.X - pad, card.Center.Y + toggleHeight * 0.5f));
        result = Toggle.Draw(id, toggle, value, theme);
        return card.Max.Y;
    }
}
