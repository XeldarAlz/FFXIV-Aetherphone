using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class EmojiText
{
    private const float LineWrapWidth = 100000f;
    private const float ClipSlack = 4f;

    private static readonly RichTextCache BlockLayouts = new();
    private static readonly RichTextCache LineLayouts = new();

    public static float BlockHeight(string text, in TextStyle style, float wrapWidth)
    {
        var layout = Layout(BlockLayouts, text, style, wrapWidth);
        return layout is null ? Typography.MeasureWrappedBlock(text, style, wrapWidth).Y : layout.Size.Y;
    }

    public static float DrawBlock(Vector2 topLeft, string text, Vector4 ink, in TextStyle style, float wrapWidth)
    {
        var layout = Layout(BlockLayouts, text, style, wrapWidth);
        if (layout is null)
        {
            return Typography.DrawWrappedLeft(topLeft, text, ink, style, wrapWidth);
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            RichText.Draw(ImGui.GetWindowDrawList(), layout, topLeft, Ink(ink, 1f), out _);
        }

        return layout.Size.Y;
    }

    public static float DrawBlock(ImDrawListPtr drawList, Vector2 topLeft, string text, Vector4 ink, float alpha,
        in TextStyle style, float wrapWidth, float maxHeight)
    {
        var layout = Layout(BlockLayouts, text, style, wrapWidth);
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            var lineHeight = ImGui.GetTextLineHeight();
            if (layout is not null && layout.EmojiFiles.Length > 0)
            {
                lineHeight = MathF.Max(lineHeight, EmojiRender.LineHeight(layout.FontSize));
            }

            var lines = (int)MathF.Floor((maxHeight + 0.5f) / MathF.Max(lineHeight, 1f));
            if (lines <= 0)
            {
                return 0f;
            }

            if (layout is not null)
            {
                var height = MathF.Min(layout.Size.Y, lines * lineHeight);
                drawList.PushClipRect(topLeft, new Vector2(topLeft.X + wrapWidth, topLeft.Y + height), true);
                RichText.Draw(drawList, layout, topLeft, Ink(ink, alpha), out _);
                drawList.PopClipRect();
                return height;
            }

            Plugin.Fonts.NoticeText(text);
            var wrapped = Typography.WrapCurrent(text, wrapWidth);
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(ink with { W = ink.W * alpha });
            var drawn = Math.Min(lines, wrapped.Length);
            for (var index = 0; index < drawn; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(topLeft.X, topLeft.Y + index * lineHeight), packed,
                    wrapped[index]);
            }

            return drawn * lineHeight;
        }
    }

    public static float DrawLine(ImDrawListPtr drawList, MarqueeId id, string text, Vector2 topLeft, float maxWidth,
        Vector4 ink, float alpha, in TextStyle style)
    {
        var layout = Layout(LineLayouts, text, style, LineWrapWidth);
        if (layout is null)
        {
            return Marquee.DrawLeftAuto(drawList, id, text, topLeft.X, topLeft.Y, maxWidth, style,
                Palette.WithAlpha(ink, ink.W * alpha));
        }

        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            if (layout.Size.X <= maxWidth)
            {
                RichText.Draw(drawList, layout, topLeft, Ink(ink, alpha), out _);
                return layout.Size.X;
            }

            var hovering = UiInteract.Hover(topLeft, new Vector2(topLeft.X + maxWidth, topLeft.Y + layout.Size.Y));
            var offset = hovering ? Marquee.Offset(id, layout.Size.X - maxWidth) : 0f;
            var slack = ClipSlack * UiScale.Current;
            drawList.PushClipRect(new Vector2(topLeft.X, topLeft.Y - slack),
                new Vector2(topLeft.X + maxWidth, topLeft.Y + layout.Size.Y + slack), true);
            RichText.Draw(drawList, layout, topLeft with { X = topLeft.X - offset }, Ink(ink, alpha), out _);
            drawList.PopClipRect();
            return maxWidth;
        }
    }

    public static void DrawLineCentered(ImDrawListPtr drawList, MarqueeId id, string text, Vector2 topCenter,
        float maxWidth, Vector4 ink, float alpha, in TextStyle style)
    {
        var layout = Layout(LineLayouts, text, style, LineWrapWidth);
        if (layout is null)
        {
            Marquee.DrawCenteredAuto(drawList, id, text, topCenter.X, topCenter.Y, maxWidth, style,
                Palette.WithAlpha(ink, ink.W * alpha));
            return;
        }

        var width = MathF.Min(layout.Size.X, maxWidth);
        DrawLine(drawList, id, text, topCenter with { X = topCenter.X - width * 0.5f }, maxWidth, ink, alpha, style);
    }

    private static RichTextInk Ink(Vector4 ink, float alpha) => new(ink, ink, ink, alpha, 1f, false);

    private static RichTextLayout? Layout(RichTextCache cache, string text, in TextStyle style, float wrapWidth)
    {
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            return cache.LayoutFor(text, text, null, wrapWidth);
        }
    }
}
