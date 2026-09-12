using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct ChatLineStyle
{
    public readonly GameChannel? Channel;
    public readonly bool ShowSender;
    public readonly bool Ghost;
    public readonly float Entrance;
    public readonly bool Timestamp;
    public readonly bool WorldName;
    public readonly bool GameColors;
    public readonly float TextScale;
    public readonly int Repeats;
    public readonly string LocalWorld;

    public ChatLineStyle(GameChannel? channel, bool showSender, bool ghost, float entrance, bool timestamp,
        bool worldName, bool gameColors, float textScale, int repeats, string localWorld)
    {
        Channel = channel;
        ShowSender = showSender;
        Ghost = ghost;
        Entrance = entrance;
        Timestamp = timestamp;
        WorldName = worldName;
        GameColors = gameColors;
        TextScale = textScale;
        Repeats = repeats;
        LocalWorld = localWorld;
    }
}

internal static class ChatLineView
{
    private const float SidePad = 16f;
    private const float ColumnGap = 8f;
    private const float LineGap = 4f;
    private const float RailWidth = 2f;
    private const float MentionTint = 0.10f;
    private const float GhostAlpha = 0.55f;
    private const float RepeatPadX = 6f;
    private const float RepeatHeight = 16f;
    private const float MinimumWrap = 24f;
    private const int RepeatLabelCache = 100;
    private const string TimeProbe = "00:00";

    private static readonly string[] RepeatLabels = new string[RepeatLabelCache];

    public static bool Draw(ChatEntry entry, PhoneTheme theme, in ChatLineStyle style, Vector4 accent,
        out ChatChunk link, out bool linkClicked)
    {
        link = default;
        linkClicked = false;
        var overrides = ChannelStyles.Shared.For(entry.ChannelKey);
        if (entry.IsSelf && overrides is { HideOutgoing: true })
        {
            return false;
        }

        var scale = UiScale.Current;
        var textScale = style.TextScale > 0f ? style.TextScale : 1f;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ScrollLayout.StableContentWidth();
        var alpha = Math.Clamp(style.Entrance, 0f, 1f) * (style.Ghost ? GhostAlpha : 1f);
        var bodyStyle = new TextStyle(TextStyles.Callout.Scale * textScale, FontWeight.Regular);
        var timeStyle = new TextStyle(TextStyles.Caption1.Scale * textScale, FontWeight.Regular);
        var lineHeight = Typography.LineHeight(bodyStyle);
        var left = origin.X + SidePad * scale;
        var right = origin.X + available - SidePad * scale;
        var cursorX = left;
        var timeWidth = style.Timestamp ? Typography.Measure(TimeProbe, timeStyle).X : 0f;
        if (style.Timestamp)
        {
            cursorX += timeWidth + ColumnGap * scale;
        }

        var runs = ChatRuns.For(entry, ImGui.GetFrameCount());
        var withName = style.ShowSender && runs.NamePrefix.Length > 0 && !IsEmote(entry);
        var channelTint = style.Channel?.Tint ?? theme.TextMuted;
        var nameInk = NameInk(overrides, entry, accent, channelTint, style.GameColors);
        var bodyInk = BodyInk(overrides, entry, theme, channelTint, style.GameColors);
        ReadOnlySpan<TextRun> spans;
        ChatChunk[] targets;
        string layoutKey;
        if (withName)
        {
            var showWorld = style.WorldName && entry.AuthorWorld.Length > 0 &&
                            !string.Equals(entry.AuthorWorld, style.LocalWorld, StringComparison.OrdinalIgnoreCase);
            runs.LogRuns[0] = TextRun.Name(showWorld ? runs.NameWorldPrefix : runs.NamePrefix, nameInk, 0);
            spans = runs.LogRuns;
            targets = runs.LogTargets;
            layoutKey = showWorld ? runs.LogWorldKey : runs.LogKey;
        }
        else
        {
            spans = runs.Runs;
            targets = runs.Targets;
            layoutKey = entry.Id;
        }

        var repeatLabel = style.Repeats > 1 ? RepeatLabel(style.Repeats) : string.Empty;
        var repeatSize = repeatLabel.Length > 0 ? Typography.Measure(repeatLabel, timeStyle) : default;
        var repeatReserve = repeatLabel.Length > 0 ? repeatSize.X + RepeatPadX * 2f * scale + ColumnGap * scale : 0f;
        var wrap = MathF.Max(MinimumWrap * scale, right - cursorX - repeatReserve);
        RunTextLayout layout;
        using (Plugin.Fonts.Push(bodyStyle.Scale, bodyStyle.Weight))
        {
            Plugin.Fonts.NoticeText(entry.Text);
            layout = RunText.Layout(layoutKey, spans, wrap);
        }

        var totalHeight = MathF.Max(lineHeight, layout.Size.Y);
        var lineMin = origin;
        var lineMax = new Vector2(origin.X + available, origin.Y + totalHeight);
        if (entry.IsMention)
        {
            Squircle.Fill(drawList, lineMin, new Vector2(lineMax.X, lineMax.Y + LineGap * scale * 0.5f),
                Metrics.Radius.Sm * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, MentionTint * alpha)));
            var railLeft = origin.X + 2f * scale;
            drawList.AddRectFilled(new Vector2(railLeft, origin.Y + 2f * scale),
                new Vector2(railLeft + RailWidth * scale, lineMax.Y),
                ImGui.GetColorU32(Palette.WithAlpha(accent, accent.W * alpha)), RailWidth * scale * 0.5f);
        }

        if (style.Timestamp)
        {
            var stamp = TimeText.Clock(entry.At);
            var stampSize = Typography.Measure(stamp, timeStyle);
            Typography.Draw(drawList, new Vector2(left, origin.Y + (lineHeight - stampSize.Y) * 0.5f), stamp,
                Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), timeStyle);
        }

        var interactive = !style.Ghost && style.Entrance >= 1f;
        int clicked;
        using (Plugin.Fonts.Push(bodyStyle.Scale, bodyStyle.Weight))
        {
            clicked = RunText.Draw(drawList, layout, spans, new Vector2(cursorX, origin.Y),
                Palette.WithAlpha(bodyInk, bodyInk.W * alpha), alpha, interactive);
        }

        if (clicked >= 0 && clicked < targets.Length)
        {
            link = targets[clicked];
            linkClicked = true;
        }

        if (repeatLabel.Length > 0)
        {
            var pillHeight = RepeatHeight * scale * textScale;
            var pillMax = new Vector2(right, origin.Y + (lineHeight + pillHeight) * 0.5f);
            var pillMin = new Vector2(right - repeatSize.X - RepeatPadX * 2f * scale, pillMax.Y - pillHeight);
            Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextMuted, 0.16f * alpha)));
            Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, repeatLabel,
                Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), timeStyle);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, lineMax.Y + LineGap * scale));
        return interactive && !linkClicked && UiInteract.Hover(lineMin, lineMax) &&
               ImGui.IsMouseClicked(ImGuiMouseButton.Right);
    }

    private static bool IsEmote(ChatEntry entry) =>
        string.Equals(entry.ChannelKey, GameChannels.EmoteKey, StringComparison.Ordinal);

    private static string RepeatLabel(int repeats)
    {
        if (repeats >= RepeatLabelCache)
        {
            return RepeatLabels[RepeatLabelCache - 1] ??= string.Concat("×", (RepeatLabelCache - 1).ToString(Loc.Culture), "+");
        }

        return RepeatLabels[repeats] ??= string.Concat("×", repeats.ToString(Loc.Culture));
    }

    private static Vector4 NameInk(ChannelStyle? overrides, ChatEntry entry, Vector4 accent, Vector4 channelTint,
        bool gameColors)
    {
        var packed = overrides is null
            ? 0u
            : entry.IsSelf ? overrides.OutgoingName : overrides.IncomingName;
        if (packed != 0u)
        {
            return ChannelInk.Unpack(packed);
        }

        if (gameColors)
        {
            return channelTint;
        }

        return entry.IsSelf ? accent : SenderTint.Of(entry.AuthorName);
    }

    private static Vector4 BodyInk(ChannelStyle? overrides, ChatEntry entry, PhoneTheme theme, Vector4 channelTint,
        bool gameColors)
    {
        var packed = overrides is null
            ? 0u
            : entry.IsSelf ? overrides.OutgoingBody : overrides.IncomingBody;
        if (packed != 0u)
        {
            return ChannelInk.Unpack(packed);
        }

        return gameColors ? channelTint : theme.TextStrong;
    }
}
