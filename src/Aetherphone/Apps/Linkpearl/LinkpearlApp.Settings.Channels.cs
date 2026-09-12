using Aetherphone.Apps.Settings;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const string ChannelHexFieldId = "##linkpearlChannelHex";
    private const int ChannelHexDigitCount = 6;
    private const float ChannelHexLabelShare = 0.38f;

    private static readonly NamedColor[] ChannelInks =
    {
        new("Theme", new Vector4(0.58f, 0.60f, 0.64f, 1f)),
        new("Red", new Vector4(1f, 0.271f, 0.227f, 1f)),
        new("Orange", new Vector4(1f, 0.624f, 0.039f, 1f)),
        new("Yellow", new Vector4(1f, 0.839f, 0.039f, 1f)),
        new("Green", new Vector4(0.196f, 0.843f, 0.294f, 1f)),
        new("Blue", new Vector4(0.039f, 0.518f, 1f, 1f)),
        new("Purple", new Vector4(0.749f, 0.353f, 0.949f, 1f)),
    };

    private static readonly uint[] ChannelInkValues = PackChannelInks();

    private static readonly LocString[] ChannelInkLabels =
    {
        L.Linkpearl.InkIncomingName,
        L.Linkpearl.InkIncomingBody,
        L.Linkpearl.InkOutgoingName,
        L.Linkpearl.InkOutgoingBody,
    };

    private readonly ChannelStyle channelDraft = new();
    private string channelEditorKey = string.Empty;
    private string channelHexDigits = string.Empty;
    private int channelHexSlot = -1;

    private void DrawChannelSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.ChannelStyleSection), frameTheme);
        var masterCard = GroupCard.Begin(frameTheme, 1);
        var hideHandled = SettingsRow.Bool(masterCard.NextRow(), Loc.T(L.Linkpearl.HideHandled),
            configuration.LinkpearlHideHandledFromGameChat, frameTheme, "linkpearl.settings.hideHandled",
            Loc.T(L.Linkpearl.HideHandledHint));
        masterCard.End();
        if (hideHandled != configuration.LinkpearlHideHandledFromGameChat)
        {
            configuration.LinkpearlHideHandledFromGameChat = hideHandled;
            configuration.Save();
        }

        SettingsSection.Hint(Loc.T(L.Linkpearl.ChannelStyleHint), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var importCard = GroupCard.Begin(frameTheme, 1);
        if (SettingsRow.Action(importCard.NextRow(), Loc.T(L.Linkpearl.ImportGameColors), frameTheme.Accent,
                frameTheme))
        {
            GameLogColors.ImportAll(ChannelStyles.Shared);
            ShellToast.Show(Loc.T(L.Linkpearl.ImportedGameColors));
        }

        importCard.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var channels = GameChannels.All;
        var expanded = ExpandedChannelIndex(channels);
        if (expanded < 0)
        {
            DrawChannelRows(channels, 0, channels.Length, scale);
            return;
        }

        DrawChannelRows(channels, 0, expanded + 1, scale);
        DrawChannelEditor(channels[expanded], scale);
        DrawChannelRows(channels, expanded + 1, channels.Length, scale);
    }

    private int ExpandedChannelIndex(ReadOnlySpan<GameChannel> channels)
    {
        if (channelEditorKey.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < channels.Length; index++)
        {
            if (string.Equals(channels[index].Key, channelEditorKey, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawChannelRows(ReadOnlySpan<GameChannel> channels, int from, int to, float scale)
    {
        if (from >= to)
        {
            return;
        }

        var card = GroupCard.Begin(frameTheme, to - from);
        for (var index = from; index < to; index++)
        {
            DrawChannelRow(card.NextRow(), channels[index]);
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawChannelRow(Rect row, GameChannel channel)
    {
        var customized = ChannelStyles.Shared.IsCustomized(channel.Key);
        var value = customized ? Loc.T(L.Linkpearl.ChannelCustom) : string.Empty;
        if (!SettingsRow.Disclosure(row, GameChannels.DisplayName(channel), value, frameTheme))
        {
            return;
        }

        SelectChannel(string.Equals(channelEditorKey, channel.Key, StringComparison.Ordinal)
            ? string.Empty
            : channel.Key);
    }

    private void SelectChannel(string channelKey)
    {
        channelEditorKey = channelKey;
        channelHexSlot = -1;
        channelHexDigits = string.Empty;
        if (channelKey.Length == 0)
        {
            channelDraft.Clear();
            return;
        }

        ChannelStyles.Shared.Load(channelKey, channelDraft);
    }

    private void DrawChannelEditor(GameChannel channel, float scale)
    {
        var canHideFromGameChat = ChannelStyleStore.CanHideFromGameChat(channel);
        var cardWidth = ImGui.GetContentRegionAvail().X - 2f * Metrics.Space.Lg * scale;
        var stacked = StackChannelInks(cardWidth);
        var inkRows = ChannelStyle.InkSlotCount * (stacked ? 2 : 1);
        var hexRows = channelHexSlot >= 0 ? 1 : 0;
        var ruleRows = canHideFromGameChat ? 3 : 2;
        var resetRows = ChannelStyles.Shared.IsCustomized(channel.Key) ? 1 : 0;
        var card = GroupCard.Begin(frameTheme, inkRows + hexRows + ruleRows + resetRows);
        for (var slot = 0; slot < ChannelStyle.InkSlotCount; slot++)
        {
            DrawChannelInkRow(card.NextRow(stacked ? 2 : 1), channel, slot, stacked);
        }

        if (hexRows > 0)
        {
            DrawChannelHexRow(card.NextRow(), channel, scale);
        }

        DrawChannelRules(ref card, channel, canHideFromGameChat);
        if (resetRows > 0 &&
            SettingsRow.Action(card.NextRow(), Loc.T(L.Linkpearl.ResetChannel), frameTheme.Danger, frameTheme))
        {
            ChannelStyles.Shared.Reset(channel.Key);
            configuration.Save();
            channelDraft.Clear();
            channelHexSlot = -1;
            inbox.Invalidate();
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.InkHint), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawChannelInkRow(Rect row, GameChannel channel, int slot, bool stacked)
    {
        var packed = channelDraft.Ink(slot);
        var preset = ChannelInkIndex(packed);
        var custom = preset < 0;
        var picked = SwatchStrip.Draw(row, Loc.T(ChannelInkLabels[slot]), ChannelInks, preset, frameTheme, stacked,
            custom ? ChannelInk.Unpack(packed) : ChannelInks[0].Color, custom);
        if (picked < 0 || picked == preset)
        {
            return;
        }

        if (picked >= ChannelInks.Length)
        {
            channelHexSlot = slot;
            channelHexDigits = packed == 0u ? string.Empty : HexColor.ToDigits(ChannelInk.Unpack(packed));
            return;
        }

        channelHexSlot = -1;
        channelDraft.SetInk(slot, ChannelInkValues[picked]);
        CommitChannel(channel);
    }

    private void DrawChannelHexRow(Rect row, GameChannel channel, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = Metrics.Size.FieldHeight * scale;
        var field = new Rect(new Vector2(row.Min.X, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + height * 0.5f));
        Squircle.Fill(drawList, field.Min, field.Max, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(Palette.WithAlpha(frameTheme.TextStrong, 0.07f)));
        var valid = HexColor.TryParse(channelHexDigits, out var preview);
        var chipRadius = height * 0.5f - Metrics.Space.Xs * scale;
        var chipCenter = new Vector2(field.Min.X + Metrics.Space.Md * scale + chipRadius, field.Center.Y);
        drawList.AddCircleFilled(chipCenter, chipRadius,
            ImGui.GetColorU32(valid ? preview : Palette.WithAlpha(frameTheme.TextMuted, 0.3f)), 24);
        var labelLeft = chipCenter.X + chipRadius + Metrics.Space.Md * scale;
        var label = Typography.FitText(Loc.T(L.Linkpearl.CustomColor), field.Width * ChannelHexLabelShare,
            TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, field.Center.Y - labelSize.Y * 0.5f), label,
            frameTheme.TextMuted, TextStyles.Footnote);
        var hashLeft = labelLeft + labelSize.X + Metrics.Space.Md * scale;
        var hashSize = Typography.Measure("#", TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(hashLeft, field.Center.Y - hashSize.Y * 0.5f), "#",
            frameTheme.TextMuted, TextStyles.BodyEmphasized);
        var inputLeft = hashLeft + hashSize.X + Metrics.Space.Xxs * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(MathF.Max(1f, field.Max.X - Metrics.Space.Md * scale - inputLeft));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.Text, frameTheme.TextStrong))
        {
            if (!ImGui.InputText(ChannelHexFieldId, ref channelHexDigits, ChannelHexDigitCount,
                    ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase))
            {
                return;
            }
        }

        if (!HexColor.TryParse(channelHexDigits, out var typed))
        {
            return;
        }

        channelDraft.SetInk(channelHexSlot, ChannelInk.Pack(typed));
        CommitChannel(channel);
    }

    private void DrawChannelRules(ref GroupCard card, GameChannel channel, bool canHideFromGameChat)
    {
        var neverUnread = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.NeverUnread), channelDraft.NeverUnread,
            frameTheme, "linkpearl.channel.neverUnread", Loc.T(L.Linkpearl.NeverUnreadHint));
        if (neverUnread != channelDraft.NeverUnread)
        {
            channelDraft.NeverUnread = neverUnread;
            CommitChannel(channel);
            inbox.Invalidate();
        }

        var hideOwn = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.HideOwnLines), channelDraft.HideOutgoing,
            frameTheme, "linkpearl.channel.hideOwn", Loc.T(L.Linkpearl.HideOwnLinesHint));
        if (hideOwn != channelDraft.HideOutgoing)
        {
            channelDraft.HideOutgoing = hideOwn;
            CommitChannel(channel);
        }

        if (!canHideFromGameChat)
        {
            return;
        }

        var hideFromGame = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.HideFromGameChat),
            channelDraft.HideFromGameChat, frameTheme, "linkpearl.channel.hideFromGame",
            Loc.T(L.Linkpearl.HideFromGameChatHint), !configuration.LinkpearlHideHandledFromGameChat);
        if (hideFromGame != channelDraft.HideFromGameChat)
        {
            channelDraft.HideFromGameChat = hideFromGame;
            CommitChannel(channel);
        }
    }

    private void CommitChannel(GameChannel channel)
    {
        ChannelStyles.Shared.Apply(channel.Key, channelDraft);
        configuration.Save();
    }

    private static bool StackChannelInks(float cardWidth)
    {
        var slots = ChannelInks.Length + 1;
        for (var index = 0; index < ChannelInkLabels.Length; index++)
        {
            if (SwatchStrip.NeedsTwoRows(Loc.T(ChannelInkLabels[index]), slots, cardWidth))
            {
                return true;
            }
        }

        return false;
    }

    private static int ChannelInkIndex(uint packed)
    {
        for (var index = 0; index < ChannelInkValues.Length; index++)
        {
            if (ChannelInkValues[index] == packed)
            {
                return index;
            }
        }

        return -1;
    }

    private static uint[] PackChannelInks()
    {
        var packed = new uint[ChannelInks.Length];
        for (var index = 1; index < ChannelInks.Length; index++)
        {
            packed[index] = ChannelInk.Pack(ChannelInks[index].Color);
        }

        return packed;
    }
}
