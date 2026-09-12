using System.Runtime.InteropServices;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal readonly ref struct GameComposerModel
{
    public required PhoneTheme Theme { get; init; }
    public required Rect Screen { get; init; }
    public required ReadOnlySpan<string> Channels { get; init; }
    public required string ActiveChannel { get; init; }
    public required string SendTarget { get; init; }
}

internal readonly struct GameComposerResult
{
    public readonly bool Submitted;
    public readonly bool IsCommand;
    public readonly string Text;
    public readonly string ChannelKey;
    public readonly bool ChannelFromCommand;

    public GameComposerResult(bool submitted, bool isCommand, string text, string channelKey,
        bool channelFromCommand)
    {
        Submitted = submitted;
        IsCommand = isCommand;
        Text = text;
        ChannelKey = channelKey;
        ChannelFromCommand = channelFromCommand;
    }
}

internal sealed class GameComposer
{
    private const float ChipMaxWidth = 64f;
    private const float ChipHeight = 34f;
    private const float ChipPadLeft = 12f;
    private const float ChipPadRight = 8f;
    private const float ChipCaret = 14f;
    private const float ChipCaretGap = 3f;
    private const float ChipFillAlpha = 0.18f;
    private const float RingThreshold = 0.55f;
    private const float RowHeight = 40f;
    private const float PillInset = 10f;
    private const float EdgePad = 12f;
    private const float PillGap = 8f;
    private const float TextPad = 14f;
    private const float EmojiRadius = 15f;
    private const float EmojiInset = 6f;
    private const float SendRadius = 20f;
    private const float SendGlyph = 20f;
    private const float SendGlyphNudge = 1f;
    private const float PartBadgeRadius = 8f;
    private const int MinimumLines = 1;
    private const int MaximumLines = 10;
    private const long DoubleEnterWindowMilliseconds = 700;

    private static readonly Vector4 FieldFill = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 BudgetWarning = new(0.88f, 0.65f, 0.38f, 1f);
    private static readonly TextStyle ChipStyle = new(0.72f, FontWeight.SemiBold);

    private readonly DropdownMenu channelMenu = new();
    private readonly GameEmojiComposer emoji = new();
    private readonly List<DropdownMenu.Item> menuItems = new(24);
    private readonly List<string> menuKeys = new(24);
    private readonly List<string> splitScratch = new(MessageSplitter.MaxParts);
    private readonly SoftWrapEditor editor = new();
    private string conversationKey = string.Empty;
    private string countedSource = string.Empty;
    private string countedIndicator = string.Empty;
    private int countedBudget = -1;
    private int countedParts = 1;
    private int capacityBytes;
    private long lastEnterMilliseconds;
    private bool enterPressed;
    private bool focus;
    private bool fieldActive;
    private bool channelFromCommand;
    private int recallIndex = -1;
    private string recallStash = string.Empty;

    public string Draft => editor.Text;

    public void Gate() => channelMenu.Gate();

    public void CloseMenus()
    {
        channelMenu.Close();
        emoji.Close();
    }

    public void Bind(string nextConversationKey)
    {
        if (string.Equals(conversationKey, nextConversationKey, StringComparison.Ordinal))
        {
            return;
        }

        ChatDrafts.Store(conversationKey, editor.Text);
        conversationKey = nextConversationKey;
        editor.Adopt(ChatDrafts.Load(nextConversationKey));
        focus = false;
        channelMenu.Close();
        ResetRecall();
    }

    public void Unbind()
    {
        ChatDrafts.Store(conversationKey, editor.Text);
        ChatDrafts.Flush();
        conversationKey = string.Empty;
        editor.Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
        ResetRecall();
    }

    public void Reset()
    {
        editor.Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
        emoji.Close();
        ResetRecall();
    }

    public void Refill(string text)
    {
        editor.Adopt(text);
        focus = true;
        ResetRecall();
    }

    public void Clear()
    {
        editor.Adopt(string.Empty);
        ChatDrafts.Store(conversationKey, string.Empty);
        channelFromCommand = false;
        ResetRecall();
    }

    private void ResetRecall()
    {
        recallIndex = -1;
        recallStash = string.Empty;
    }

    private void RecallHistory()
    {
        var recent = Plugin.Cfg?.LinkpearlRecentSent;
        if (!fieldActive || recent is null || recent.Count == 0)
        {
            return;
        }

        var text = editor.Text;
        var atRecalled = recallIndex >= 0 && recallIndex < recent.Count &&
                         string.Equals(text, recent[recallIndex].Text, StringComparison.Ordinal);
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow) && (text.Length == 0 || atRecalled))
        {
            if (recallIndex < 0)
            {
                recallStash = text;
            }

            if (recallIndex + 1 < recent.Count)
            {
                recallIndex++;
                editor.Adopt(recent[recallIndex].Text);
            }

            return;
        }

        if (!ImGui.IsKeyPressed(ImGuiKey.DownArrow) || recallIndex < 0 || !atRecalled)
        {
            return;
        }

        recallIndex--;
        editor.Adopt(recallIndex < 0 ? recallStash : recent[recallIndex].Text);
    }

    public float Measure(float barWidth, in GameComposerModel model)
    {
        var scale = UiScale.Current;
        var baseHeight = (RowHeight + PillInset * 2f) * scale;
        if (!Multiline || !Sendable(model, out var channel))
        {
            return baseHeight;
        }

        editor.Rewrap(WrapWidthOf(InnerWidth(barWidth, channel, scale), scale));
        var visible = Math.Clamp(editor.LineCount, MinimumLines, MaxLines);
        return baseHeight + (visible - 1) * ImGui.GetTextLineHeight();
    }

    public GameComposerResult Draw(Rect bar, in GameComposerModel model)
    {
        var scale = UiScale.Current;
        var theme = model.Theme;
        var drawList = ImGui.GetWindowDrawList();
        var channelKey = model.ActiveChannel;
        if (!Sendable(model, out var channel))
        {
            DrawReadOnly(bar, theme);
            return new GameComposerResult(false, false, string.Empty, channelKey, channelFromCommand);
        }

        var indicator = Indicator;
        var budget = ChatSend.Budget(channel, model.SendTarget);
        capacityBytes = Splitting ? MessageSplitter.Capacity(budget, indicator) : budget;
        var pillMin = new Vector2(bar.Min.X + EdgePad * scale, bar.Min.Y + PillInset * scale);
        var pillMax = new Vector2(bar.Max.X - EdgePad * scale, bar.Max.Y - PillInset * scale);
        var rowHeight = RowHeight * scale;
        var rowCenterY = pillMax.Y - rowHeight * 0.5f;
        var chipWidth = MathF.Min(ChipMaxWidth * scale, ChipWidthOf(channel, scale));
        var chipMin = new Vector2(pillMin.X, rowCenterY - ChipHeight * 0.5f * scale);
        var chipMax = new Vector2(chipMin.X + chipWidth, rowCenterY + ChipHeight * 0.5f * scale);
        var sendRadius = SendRadius * scale;
        var sendCenter = new Vector2(pillMax.X - sendRadius, rowCenterY);
        var emojiRadius = GameEmojiComposer.PickerEnabled ? EmojiRadius * scale : 0f;
        var fieldMin = new Vector2(chipMax.X + PillGap * scale, pillMin.Y);
        var fieldMax = new Vector2(sendCenter.X - sendRadius - PillGap * scale, pillMax.Y);
        Squircle.Fill(drawList, fieldMin, fieldMax, MathF.Min(fieldMax.Y - fieldMin.Y, rowHeight) * 0.5f,
            ImGui.GetColorU32(FieldFill));
        DrawChip(drawList, chipMin, chipMax, channel, scale);
        if (UiInteract.HoverClick(chipMin, chipMax))
        {
            channelMenu.Toggle("linkpearl.composer.channel", new Rect(chipMin, chipMax));
        }

        var emojiCenter = new Vector2(fieldMax.X - EmojiInset * scale - emojiRadius, rowCenterY);
        emoji.DrawToggle(emojiCenter, emojiRadius, theme);

        var innerWidth = MathF.Max(1f, InnerWidth(bar.Width, channel, scale));
        enterPressed = false;
        if (Multiline)
        {
            DrawMultiline(fieldMin, fieldMax, innerWidth, theme, scale);
        }
        else
        {
            DrawSingleLine(fieldMin, fieldMax, innerWidth, theme, scale);
        }

        var suggested = editor.Text;
        emoji.DrawSuggestions(bar, model.Screen, theme, ref suggested);
        if (!string.Equals(suggested, editor.Text, StringComparison.Ordinal))
        {
            editor.Adopt(suggested);
        }

        var pickedEmoji = emoji.DrawPanel(bar, model.Screen, theme);
        if (pickedEmoji is not null &&
            Encoding.UTF8.GetByteCount(editor.Text) + pickedEmoji.Length <= capacityBytes)
        {
            editor.Append(pickedEmoji);
        }

        if (ChatCommands.TryAbsorb(editor.Text, out var absorbed, out var remainder) &&
            Offered(model.Channels, absorbed.Key))
        {
            editor.Adopt(remainder);
            channelKey = absorbed.Key;
            channelFromCommand = true;
        }

        RecallHistory();
        ChatDrafts.Store(conversationKey, editor.Text);
        var used = Encoding.UTF8.GetByteCount(editor.Text);
        var hasText = editor.HasContent;
        var parts = hasText && Splitting ? PartCount(budget, indicator) : 1;
        DrawSend(drawList, sendCenter, sendRadius, hasText, used, capacityBytes, parts, theme, scale);
        var submitted = ConsumeEnter();
        if (hasText && UiInteract.HoverClickCircle(sendCenter, sendRadius))
        {
            submitted = true;
        }

        var picked = DrawChannelMenu(model, channelKey);
        if (picked.Length > 0)
        {
            channelKey = picked;
            channelFromCommand = false;
        }

        if (!submitted || !hasText)
        {
            return new GameComposerResult(false, false, string.Empty, channelKey, channelFromCommand);
        }

        var text = editor.Text.Trim();
        focus = true;
        return new GameComposerResult(true, text[0] == '/', text, channelKey, channelFromCommand);
    }

    private static bool Multiline => Plugin.Cfg?.LinkpearlComposerMultiline ?? true;

    private static bool Splitting => Plugin.Cfg?.LinkpearlSplitLongMessages ?? true;

    private static bool DoubleEnter => Plugin.Cfg?.LinkpearlDoubleEnterSend ?? false;

    private static string Indicator => Plugin.Cfg?.LinkpearlSplitIndicator ?? string.Empty;

    private static int MaxLines =>
        Math.Clamp(Plugin.Cfg?.LinkpearlComposerMaxLines ?? 4, MinimumLines, MaximumLines);

    private static bool Sendable(in GameComposerModel model, out GameChannel channel) =>
        GameChannels.TryByKey(model.ActiveChannel, out channel) && channel.CanSend &&
        (!channel.NeedsTarget || model.SendTarget.Length > 0);

    private void DrawSingleLine(Vector2 fieldMin, Vector2 fieldMax, float innerWidth, PhoneTheme theme, float scale)
    {
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + TextPad * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(innerWidth);
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        var line = editor.Text;
        Plugin.Fonts.NoticeText(line);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##linkpearl.composer", Loc.T(L.Messages.Placeholder), ref line,
                    Math.Max(1, capacityBytes), ImGuiInputTextFlags.EnterReturnsTrue))
            {
                enterPressed = true;
            }
        }

        fieldActive = ImGui.IsItemActive();
        if (!string.Equals(line, editor.Text, StringComparison.Ordinal))
        {
            editor.Adopt(line);
        }
    }

    private void DrawMultiline(Vector2 fieldMin, Vector2 fieldMax, float innerWidth, PhoneTheme theme, float scale)
    {
        editor.Rewrap(WrapWidthOf(innerWidth, scale));
        var visible = Math.Clamp(editor.LineCount, MinimumLines, MaxLines);
        var boxHeight = visible * ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2f;
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + TextPad * scale,
            (fieldMin.Y + fieldMax.Y) * 0.5f - boxHeight * 0.5f));
        if (focus)
        {
            ImGui.SetKeyboardFocusHere();
            focus = false;
        }

        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (editor.Draw("##linkpearl.composer", new Vector2(innerWidth, boxHeight), 0, capacityBytes))
            {
                enterPressed = true;
            }
        }

        fieldActive = ImGui.IsItemActive();
        if (editor.Text.Length > 0)
        {
            return;
        }

        var padding = ImGui.GetStyle().FramePadding;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(fieldMin.X + TextPad * scale + padding.X,
                (fieldMin.Y + fieldMax.Y) * 0.5f - boxHeight * 0.5f + padding.Y),
            Loc.T(L.Messages.Placeholder), theme.TextMuted, TextStyles.Body);
    }

    private bool ConsumeEnter()
    {
        if (!enterPressed)
        {
            return false;
        }

        enterPressed = false;
        if (!DoubleEnter)
        {
            return true;
        }

        var now = Environment.TickCount64;
        var quick = now - lastEnterMilliseconds <= DoubleEnterWindowMilliseconds;
        lastEnterMilliseconds = quick ? 0 : now;
        return quick;
    }

    private int PartCount(int budget, string indicator)
    {
        if (countedBudget == budget && string.Equals(countedSource, editor.Text, StringComparison.Ordinal) &&
            string.Equals(countedIndicator, indicator, StringComparison.Ordinal))
        {
            return countedParts;
        }

        countedBudget = budget;
        countedSource = editor.Text;
        countedIndicator = indicator;
        if (countedSource.IndexOf('\n') < 0 && Encoding.UTF8.GetByteCount(countedSource) <= budget)
        {
            countedParts = 1;
            return countedParts;
        }

        MessageSplitter.Split(countedSource, budget, indicator, splitScratch);
        countedParts = Math.Max(1, splitScratch.Count);
        return countedParts;
    }

    private string DrawChannelMenu(in GameComposerModel model, string activeKey)
    {
        if (!channelMenu.IsOpenFor("linkpearl.composer.channel"))
        {
            return string.Empty;
        }

        menuItems.Clear();
        menuKeys.Clear();
        var channels = model.Channels;
        for (var index = 0; index < channels.Length; index++)
        {
            if (!GameChannels.TryByKey(channels[index], out var channel) || !channel.CanSend || channel.NeedsTarget)
            {
                continue;
            }

            menuItems.Add(new DropdownMenu.Item(GameChannels.DisplayName(channel), string.Empty, false,
                string.Equals(channel.Key, activeKey, StringComparison.Ordinal)));
            menuKeys.Add(channel.Key);
        }

        if (menuItems.Count == 0)
        {
            channelMenu.Close();
            return string.Empty;
        }

        var clicked = channelMenu.Draw(model.Screen, model.Theme, CollectionsMarshal.AsSpan(menuItems));
        return clicked >= 0 ? menuKeys[clicked] : string.Empty;
    }

    private static bool Offered(ReadOnlySpan<string> channels, string key)
    {
        for (var index = 0; index < channels.Length; index++)
        {
            if (string.Equals(channels[index], key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static float InnerWidth(float barWidth, GameChannel channel, float scale)
    {
        var chipWidth = MathF.Min(ChipMaxWidth * scale, ChipWidthOf(channel, scale));
        var emojiWidth = GameEmojiComposer.PickerEnabled
            ? (EmojiRadius * 2f + EmojiInset + PillGap) * scale
            : 0f;
        var fieldWidth = barWidth - (EdgePad * 2f + PillGap * 2f + SendRadius * 2f) * scale - chipWidth - emojiWidth;
        return MathF.Max(1f, fieldWidth - TextPad * scale);
    }

    private static float WrapWidthOf(float innerWidth, float scale) =>
        MathF.Max(1f, innerWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale);

    private static float ChipWidthOf(GameChannel channel, float scale) =>
        Typography.Measure(ShortName(channel), ChipStyle).X +
        (ChipPadLeft + ChipCaretGap + ChipCaret + ChipPadRight) * scale;

    private static void DrawChip(ImDrawListPtr drawList, Vector2 min, Vector2 max, GameChannel channel,
        float scale)
    {
        var tint = channel.Tint;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, ChipFillAlpha)));
        var labelWidth = max.X - min.X - (ChipPadLeft + ChipCaretGap + ChipCaret + ChipPadRight) * scale;
        var label = Typography.FitText(ShortName(channel), labelWidth, ChipStyle);
        var labelSize = Typography.Measure(label, ChipStyle);
        var centerY = (min.Y + max.Y) * 0.5f;
        Typography.Draw(drawList, new Vector2(min.X + ChipPadLeft * scale, centerY - labelSize.Y * 0.5f), label, tint,
            ChipStyle);
        PhoneIcon.Draw(drawList, new Vector2(max.X - ChipPadRight * scale - ChipCaret * 0.5f * scale, centerY),
            PhoneIcons.ChevronDown, Palette.WithAlpha(tint, 0.8f), ChipCaret * scale);
    }

    private static void DrawSend(ImDrawListPtr drawList, Vector2 center, float radius, bool hasText, int used,
        int budget, int parts, PhoneTheme theme, float scale)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hasText ? theme.Accent : FieldFill), 32);
        PhoneIcon.Draw(drawList, new Vector2(center.X - SendGlyphNudge * scale, center.Y), PhoneIcons.Send,
            hasText ? White : theme.TextMuted, SendGlyph * scale);
        if (parts > 1)
        {
            DrawPartBadge(drawList, center, radius, parts, theme, scale);
            return;
        }

        if (budget <= 0)
        {
            return;
        }

        var fraction = Math.Clamp((float)used / budget, 0f, 1f);
        if (fraction < RingThreshold)
        {
            return;
        }

        var ringColor = fraction >= 0.95f ? theme.Danger : BudgetWarning;
        ProgressRing.Fill(center, radius + 2.5f * scale, 2f * scale, fraction, ringColor);
    }

    private static void DrawPartBadge(ImDrawListPtr drawList, Vector2 center, float radius, int parts,
        PhoneTheme theme, float scale)
    {
        var badgeCenter = new Vector2(center.X + radius - 1f * scale, center.Y - radius + 1f * scale);
        var badgeRadius = PartBadgeRadius * scale;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(theme.AppBackground), 16);
        drawList.AddCircleFilled(badgeCenter, badgeRadius - 1f * scale, ImGui.GetColorU32(theme.Accent), 16);
        Typography.DrawCentered(drawList, badgeCenter, parts.ToString(Loc.Culture), White, TextStyles.Caption2);
    }

    private static void DrawReadOnly(Rect bar, PhoneTheme theme) =>
        Typography.DrawCentered(ImGui.GetWindowDrawList(), bar.Center, Loc.T(L.Linkpearl.ChannelReadOnly),
            theme.TextMuted, TextStyles.Caption1);

    private static string ShortName(GameChannel channel)
    {
        if (channel.IsSlotted)
        {
            return channel.Key.ToUpperInvariant();
        }

        var name = GameChannels.DisplayName(channel);
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
