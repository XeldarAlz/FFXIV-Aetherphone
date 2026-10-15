using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Strip;

internal static class FloorHeader
{
    public const float TrailingGap = 12f;
    public const float StackGap = 2f;

    private static readonly TextStyle TitleStyle = TextStyles.Title2;
    private static readonly TextStyle SublineStyle = TextStyles.Footnote;

    public static bool SublineInline(float titleWidth, float sublineWidth, float width, float gap) =>
        titleWidth + gap + sublineWidth <= width;

    public static float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string subline, float scale)
    {
        var gap = TrailingGap * scale;
        var titleSize = Typography.Measure(title, TitleStyle);
        var sublineSize = Typography.Measure(subline, SublineStyle);
        if (SublineInline(titleSize.X, sublineSize.X, width, gap))
        {
            Typography.Draw(drawList, origin, title, ui.TitleInk, TitleStyle);
            Typography.Draw(drawList,
                new Vector2(origin.X + width - sublineSize.X, origin.Y + titleSize.Y - sublineSize.Y), subline,
                ui.MutedInk, SublineStyle);
            return origin.Y + Typography.LineHeight(TitleStyle);
        }

        var fitted = Typography.FitText(title, width, TitleStyle);
        Typography.Draw(drawList, origin, fitted, ui.TitleInk, TitleStyle);
        var top = origin.Y + Typography.LineHeight(TitleStyle) + StackGap * scale;
        var lines = Typography.WrapText(subline, SublineStyle, width);
        var lineHeight = Typography.LineHeight(SublineStyle);
        for (var line = 0; line < lines.Length; line++)
        {
            Typography.Draw(drawList, new Vector2(origin.X, top), lines[line], ui.MutedInk, SublineStyle);
            top += lineHeight;
        }

        return top;
    }

    public static float DrawWithLink(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string link, string linkId, float scale, out bool tapped)
    {
        var lineHeight = Typography.LineHeight(TitleStyle);
        var linkHeight = Button.SmallHeight * scale;
        var linkWidth = MathF.Min(width * 0.5f, Button.WidthFor(link, ButtonSize.Small));
        var centerY = origin.Y + lineHeight * 0.5f;
        var linkRect = new Rect(new Vector2(origin.X + width - linkWidth, centerY - linkHeight * 0.5f),
            new Vector2(origin.X + width, centerY + linkHeight * 0.5f));
        var titleWidth = MathF.Max(1f, width - linkWidth - TrailingGap * scale);
        Typography.Draw(drawList, origin, Typography.FitText(title, titleWidth, TitleStyle), ui.TitleInk, TitleStyle);
        tapped = Button.Draw(drawList, linkRect, link, ui.Ink, ButtonStyle.Plain, id: linkId);
        return origin.Y + MathF.Max(lineHeight, linkHeight);
    }
}
