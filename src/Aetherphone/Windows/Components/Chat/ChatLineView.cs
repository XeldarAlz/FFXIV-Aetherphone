using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct ChatLineStyle
{
    public readonly Vector4 Rail;
    public readonly bool ShowSender;
    public readonly bool Ghost;
    public readonly float Entrance;

    public ChatLineStyle(Vector4 rail, bool showSender, bool ghost = false, float entrance = 1f)
    {
        Rail = rail;
        ShowSender = showSender;
        Ghost = ghost;
        Entrance = entrance;
    }
}

internal static class ChatLineView
{
    private const float RailWidth = 2f;
    private const float TextInset = 14f;
    private const float LineGap = 4f;
    private const float MentionTint = 0.10f;
    private const float GhostAlpha = 0.55f;

    public static bool Draw(ChatEntry entry, PhoneTheme theme, in ChatLineStyle style, Vector4 accent,
        out int linkTarget)
    {
        linkTarget = -1;
        var overrides = ChannelStyles.Shared.For(entry.ChannelKey);
        if (entry.IsSelf && overrides is { HideOutgoing: true })
        {
            return false;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ScrollLayout.StableContentWidth();
        var textLeft = origin.X + TextInset * scale;
        var wrap = MathF.Max(24f * scale, available - TextInset * scale - 4f * scale);
        var alpha = Math.Clamp(style.Entrance, 0f, 1f) * (style.Ghost ? GhostAlpha : 1f);
        var senderHeight = style.ShowSender ? Typography.LineHeight(TextStyles.FootnoteEmphasized) : 0f;
        var runs = ChatRuns.For(entry, ImGui.GetFrameCount());
        float bodyHeight;
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            Plugin.Fonts.NoticeText(entry.Text);
            bodyHeight = RunText.Layout(entry.Id, runs.Runs, wrap).Size.Y;
        }

        var totalHeight = senderHeight + bodyHeight;
        var lineMin = origin;
        var lineMax = new Vector2(origin.X + available, origin.Y + totalHeight);
        if (entry.IsMention)
        {
            Squircle.Fill(drawList, lineMin, new Vector2(lineMax.X, lineMax.Y + LineGap * scale * 0.5f),
                Metrics.Radius.Sm * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, MentionTint * alpha)));
        }

        var railColor = entry.IsMention ? accent : style.Rail;
        if (railColor.W > 0f)
        {
            var railLeft = origin.X + 2f * scale;
            drawList.AddRectFilled(new Vector2(railLeft, origin.Y + 2f * scale),
                new Vector2(railLeft + RailWidth * scale, lineMax.Y),
                ImGui.GetColorU32(Palette.WithAlpha(railColor, railColor.W * alpha)), RailWidth * scale * 0.5f);
        }

        if (style.ShowSender)
        {
            DrawSender(drawList, entry, theme, NameInk(overrides, entry, accent), origin, textLeft, wrap, alpha,
                scale);
        }

        var bodyTop = new Vector2(textLeft, origin.Y + senderHeight);
        var body = BodyInk(overrides, entry, theme);
        var ink = Palette.WithAlpha(body, body.W * alpha);
        var interactive = !style.Ghost && style.Entrance >= 1f;
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            var layout = RunText.Layout(entry.Id, runs.Runs, wrap);
            var clicked = RunText.Draw(drawList, layout, runs.Runs, bodyTop, ink, alpha, interactive);
            if (clicked >= 0)
            {
                linkTarget = clicked;
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, lineMax.Y + LineGap * scale));
        return interactive && linkTarget < 0 && UiInteract.Hover(lineMin, lineMax) &&
               ImGui.IsMouseClicked(ImGuiMouseButton.Right);
    }

    private static Vector4 NameInk(ChannelStyle? overrides, ChatEntry entry, Vector4 accent)
    {
        var packed = overrides is null
            ? 0u
            : entry.IsSelf ? overrides.OutgoingName : overrides.IncomingName;
        if (packed != 0u)
        {
            return ChannelInk.Unpack(packed);
        }

        return entry.IsSelf ? accent : SenderTint.Of(entry.AuthorName);
    }

    private static Vector4 BodyInk(ChannelStyle? overrides, ChatEntry entry, PhoneTheme theme)
    {
        var packed = overrides is null
            ? 0u
            : entry.IsSelf ? overrides.OutgoingBody : overrides.IncomingBody;
        return packed != 0u ? ChannelInk.Unpack(packed) : theme.TextStrong;
    }

    private static void DrawSender(ImDrawListPtr drawList, ChatEntry entry, PhoneTheme theme, Vector4 tint,
        Vector2 origin, float textLeft, float wrap, float alpha, float scale)
    {
        var name = FirstName(entry.AuthorName);
        var nameWidth = MathF.Min(wrap * 0.62f, Typography.Measure(name, TextStyles.FootnoteEmphasized).X);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y),
            Typography.FitText(name, nameWidth, TextStyles.FootnoteEmphasized),
            Palette.WithAlpha(tint, tint.W * alpha), TextStyles.FootnoteEmphasized);
        var stamp = TimeText.Clock(entry.At);
        Typography.Draw(drawList, new Vector2(textLeft + nameWidth + 6f * scale, origin.Y + 1f * scale), stamp,
            Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), TextStyles.Caption2);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
