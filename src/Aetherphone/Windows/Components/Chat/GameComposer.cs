using System.Runtime.InteropServices;
using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

    public GameComposerResult(bool submitted, bool isCommand, string text, string channelKey)
    {
        Submitted = submitted;
        IsCommand = isCommand;
        Text = text;
        ChannelKey = channelKey;
    }
}

internal sealed class GameComposer
{
    private const float ChipMaxWidth = 58f;
    private const float RingThreshold = 0.55f;
    private const float RowHeight = 38f;
    private const float PillInset = 7f;
    private const float EmojiRadius = 12f;
    private const int MinimumLines = 1;
    private const int MaximumLines = 10;
    private const long DoubleEnterWindowMilliseconds = 700;

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
    }

    public void Unbind()
    {
        ChatDrafts.Store(conversationKey, editor.Text);
        ChatDrafts.Flush();
        conversationKey = string.Empty;
        editor.Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
    }

    public void Reset()
    {
        editor.Adopt(string.Empty);
        focus = false;
        channelMenu.Close();
        emoji.Close();
    }

    public void Refill(string text)
    {
        editor.Adopt(text);
        focus = true;
    }

    public void Clear()
    {
        editor.Adopt(string.Empty);
        ChatDrafts.Store(conversationKey, string.Empty);
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
            return new GameComposerResult(false, false, string.Empty, channelKey);
        }

        var indicator = Indicator;
        var budget = ChatSend.Budget(channel, model.SendTarget);
        capacityBytes = Splitting ? MessageSplitter.Capacity(budget, indicator) : budget;
        var pillMin = new Vector2(bar.Min.X + Metrics.Space.Md * scale, bar.Min.Y + PillInset * scale);
        var pillMax = new Vector2(bar.Max.X - Metrics.Space.Md * scale, bar.Max.Y - PillInset * scale);
        var rowHeight = RowHeight * scale;
        var chipWidth = MathF.Min(ChipMaxWidth * scale, ChipWidthOf(channel, scale));
        var chipMin = new Vector2(pillMin.X, pillMax.Y - rowHeight + 3f * scale);
        var chipMax = new Vector2(chipMin.X + chipWidth, pillMax.Y - 3f * scale);
        var sendDiameter = rowHeight - 6f * scale;
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f, pillMax.Y - rowHeight * 0.5f);
        var emojiRadius = GameEmojiComposer.PickerEnabled ? EmojiRadius * scale : 0f;
        var emojiCenter = new Vector2(chipMax.X + Metrics.Space.Xs * scale + emojiRadius,
            (pillMin.Y + pillMax.Y) * 0.5f);
        var fieldMin = new Vector2(emojiCenter.X + emojiRadius + Metrics.Space.Xs * scale, pillMin.Y);
        var fieldMax = new Vector2(sendCenter.X - sendDiameter * 0.5f - Metrics.Space.Xs * scale, pillMax.Y);
        Squircle.Fill(drawList, fieldMin, fieldMax, MathF.Min(fieldMax.Y - fieldMin.Y, rowHeight) * 0.5f,
            ImGui.GetColorU32(theme.GroupedCard));
        DrawChip(drawList, chipMin, chipMax, channel, scale);
        if (UiInteract.HoverClick(chipMin, chipMax))
        {
            channelMenu.Toggle("linkpearl.composer.channel", new Rect(chipMin, chipMax));
        }

        emoji.DrawToggle(emojiCenter, emojiRadius, theme);

        var innerWidth = MathF.Max(1f, fieldMax.X - fieldMin.X - Metrics.Space.Md * scale);
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
        }

        ChatDrafts.Store(conversationKey, editor.Text);
        var used = Encoding.UTF8.GetByteCount(editor.Text);
        var hasText = editor.HasContent;
        var parts = hasText && Splitting ? PartCount(budget, indicator) : 1;
        DrawSend(drawList, sendCenter, sendDiameter, hasText, used, capacityBytes, parts, theme, scale);
        var submitted = ConsumeEnter();
        if (hasText && UiInteract.HoverClickCircle(sendCenter, sendDiameter * 0.5f))
        {
            submitted = true;
        }

        var picked = DrawChannelMenu(model, channelKey);
        if (picked.Length > 0)
        {
            channelKey = picked;
        }

        if (!submitted || !hasText)
        {
            return new GameComposerResult(false, false, string.Empty, channelKey);
        }

        var text = editor.Text.Trim();
        focus = true;
        return new GameComposerResult(true, text[0] == '/', text, channelKey);
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
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Sm * scale,
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
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + Metrics.Space.Sm * scale,
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

        if (editor.Text.Length > 0)
        {
            return;
        }

        var padding = ImGui.GetStyle().FramePadding;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(fieldMin.X + Metrics.Space.Sm * scale + padding.X,
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
        var sendDiameter = (RowHeight - 6f) * scale;
        var emojiWidth = GameEmojiComposer.PickerEnabled
            ? EmojiRadius * 2f * scale + Metrics.Space.Xs * scale
            : 0f;
        var fieldWidth = barWidth - Metrics.Space.Md * 2f * scale - chipWidth - sendDiameter - emojiWidth -
                         Metrics.Space.Xs * 2f * scale;
        return MathF.Max(1f, fieldWidth - Metrics.Space.Md * scale);
    }

    private static float WrapWidthOf(float innerWidth, float scale) =>
        MathF.Max(1f, innerWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale);

    private static float ChipWidthOf(GameChannel channel, float scale) =>
        Typography.Measure(ShortName(channel), TextStyles.Caption1).X + 18f * scale;

    private static void DrawChip(ImDrawListPtr drawList, Vector2 min, Vector2 max, GameChannel channel,
        float scale)
    {
        var tint = channel.Tint;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.18f)));
        var label = Typography.FitText(ShortName(channel), max.X - min.X - 14f * scale, TextStyles.Caption1);
        var center = new Vector2((min.X + max.X) * 0.5f - 3f * scale, (min.Y + max.Y) * 0.5f);
        Typography.DrawCentered(drawList, center, label, tint, TextStyles.Caption1);
        AppSkin.Icon(drawList, new Vector2(max.X - 6f * scale, center.Y + 1f * scale),
            IconGlyph.Of(FontAwesomeIcon.CaretDown), Palette.WithAlpha(tint, 0.8f), 0.6f);
    }

    private static void DrawSend(ImDrawListPtr drawList, Vector2 center, float diameter, bool hasText, int used,
        int budget, int parts, PhoneTheme theme, float scale)
    {
        var radius = diameter * 0.5f;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.ArrowUp), new Vector4(1f, 1f, 1f, 1f), 0.88f);
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

        var ringColor = fraction >= 0.95f ? theme.Danger : new Vector4(0.88f, 0.65f, 0.38f, 1f);
        ProgressRing.Fill(center, radius + 2.5f * scale, 2f * scale, fraction, ringColor);
    }

    private static void DrawPartBadge(ImDrawListPtr drawList, Vector2 center, float radius, int parts,
        PhoneTheme theme, float scale)
    {
        var badgeCenter = new Vector2(center.X + radius - 1f * scale, center.Y - radius + 1f * scale);
        var badgeRadius = 7f * scale;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(theme.AppBackground), 16);
        drawList.AddCircleFilled(badgeCenter, badgeRadius - 1f * scale, ImGui.GetColorU32(theme.Accent), 16);
        Typography.DrawCentered(drawList, badgeCenter, parts.ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Caption2);
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
