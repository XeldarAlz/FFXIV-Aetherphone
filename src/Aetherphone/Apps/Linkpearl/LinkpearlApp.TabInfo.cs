using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float InfoTileRadius = 40f;
    private const float InfoChipsGap = 12f;
    private const int InfoSettingRows = 7;
    private const int InfoActionRows = 4;
    private const string MuteIdPrefix = "linkpearl.info.mute.";

    private readonly ChipRail tabInfoRail = new();
    private string[] tabInfoLabels = Array.Empty<string>();
    private bool[] tabInfoActive = Array.Empty<bool>();
    private string[] tabInfoMuteIds = Array.Empty<string>();
    private string tabInfoKey = string.Empty;
    private string tabInfoRowKey = string.Empty;
    private int tabInfoChannelCount = -1;

    private void DrawTabInfo(Rect area, string tabId)
    {
        var tab = tabs.Find(tabId);
        if (tab is null)
        {
            router.Reset();
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawScreenHeader(area, string.Empty, backToList, 1);
        if (chrome.DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Pencil,
                Loc.T(L.Linkpearl.EditTab)))
        {
            OpenTabEditor(tab);
        }

        SyncTabInfoCache(tab);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawTabInfoHero(tab, width, scale);
            DrawTabInfoSettings(tab, scale);
            DrawTabInfoChannels(tab, scale);
            DrawTabInfoActions(tab, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }

        DrawEditorMenu(area, tab);
    }

    private void SyncTabInfoCache(ChatTab tab)
    {
        var count = tab.Channels.Count;
        if (string.Equals(tabInfoKey, tab.Id, StringComparison.Ordinal) && tabInfoChannelCount == count)
        {
            return;
        }

        tabInfoKey = tab.Id;
        tabInfoChannelCount = count;
        tabInfoRowKey = ChatInbox.KeyForTab(tab);
        tabInfoLabels = new string[count];
        tabInfoActive = new bool[count];
        tabInfoMuteIds = new string[count];
        for (var index = 0; index < count; index++)
        {
            tabInfoLabels[index] = GameChannels.TryByKey(tab.Channels[index], out var channel)
                ? LinkshellNames.Label(channel)
                : tab.Channels[index];
            tabInfoMuteIds[index] = string.Concat(MuteIdPrefix, tab.Channels[index]);
        }
    }

    private static Vector4 TabTint(ChatTab tab)
    {
        var palette = ChannelTints.TabPalette;
        return palette[Math.Clamp(tab.Tint, 0, palette.Length - 1)];
    }

    private void DrawTabInfoHero(ChatTab tab, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var radius = InfoTileRadius * scale;
        var tileCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + radius);
        GameChatTiles.DrawTile(drawList, tileCenter, radius, TabTint(tab), GameChatTiles.GlyphFor(tab));
        var top = tileCenter.Y + radius + HeroNameGap * scale;
        top += Typography.DrawWrappedCentered(new Vector2(centerX, top), tab.Name, ink.TitleInk, TextStyles.Title2,
            width - Metrics.Space.Xl * scale) + InfoChipsGap * scale;
        if (tabInfoLabels.Length > 0)
        {
            var rail = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + ChipRail.RowHeight * scale));
            tabInfoRail.Draw(rail, ui, tabInfoLabels, tabInfoActive, false, null, ChipRail.CompactLabelPadding, true,
                false);
            top = rail.Max.Y;
        }

        top += HeroActionsGap * scale;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y));
    }

    private void DrawTabInfoSettings(ChatTab tab, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(ui, InfoSettingRows, ChatListChrome.SettingRowHeight);
        var sendRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, sendRow, PhoneIcons.Send, ChatListChrome.TintGreen,
                Loc.T(L.Linkpearl.RepliesGoTo), SendChannelLabel(tab)))
        {
            editorMenu.Toggle("linkpearl.editor.send", sendRow);
        }

        var layoutRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, layoutRow, PhoneIcons.LayoutList, ChatListChrome.TintAzure,
                Loc.T(L.Linkpearl.Layout),
                Loc.T(tab.Density == ChatDensity.Bubbles ? L.Linkpearl.LayoutBubbles : L.Linkpearl.LayoutLog)))
        {
            editorMenu.Toggle("linkpearl.editor.layout", layoutRow);
        }

        var alertsRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, alertsRow, PhoneIcons.Bell, ChatListChrome.TintRed, Loc.T(L.Linkpearl.Alerts),
                Loc.T(AlertLabel(tab.Alerts))))
        {
            editorMenu.Toggle("linkpearl.editor.alerts", alertsRow);
        }

        var timestampsRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, timestampsRow, PhoneIcons.Clock, ChatListChrome.TintSlate,
                Loc.T(L.Linkpearl.LogTimestamps), Loc.T(TimestampsLabel(tab))))
        {
            editorMenu.Toggle("linkpearl.info.timestamps", timestampsRow);
        }

        var textSizeRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, textSizeRow, PhoneIcons.TextSize, ChatListChrome.TintTeal,
                Loc.T(L.Linkpearl.TextSize),
                tab.TextScale > 0f ? PercentLabel(tab.TextScale) : Loc.T(L.Message.WallpaperDefault)))
        {
            editorMenu.Toggle("linkpearl.info.textScale", textSizeRow);
        }

        var historyRow = card.NextRow();
        if (chrome.DrawCardRow(drawList, historyRow, PhoneIcons.Archive, ChatListChrome.TintGold,
                Loc.T(L.Linkpearl.KeepHistory), HistoryLabel(tab)))
        {
            editorMenu.Toggle("linkpearl.editor.history", historyRow);
        }

        if (chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.Wallpaper, ChatListChrome.TintViolet,
                Loc.T(L.Message.Wallpaper)))
        {
            router.Push(LinkpearlRoute.Wallpaper(tabInfoRowKey));
        }

        card.End();
        ChatListChrome.DrawCardGap();
    }

    private void DrawTabInfoChannels(ChatTab tab, float scale)
    {
        if (tab.Channels.Count == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        chrome.DrawInsetSectionLabel(Loc.T(L.Linkpearl.Channels));
        var card = GroupCard.Begin(ui, tab.Channels.Count, ChatListChrome.SettingRowHeight);
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            var key = tab.Channels[index];
            var resolved = GameChannels.TryByKey(key, out var channel);
            var glyph = resolved ? GameChatTiles.GlyphFor(channel.Category) : PhoneIcons.Hash;
            var tint = resolved ? channel.Tint : ink.MutedInk;
            var muted = tab.IsMuted(key);
            var alerts = chrome.DrawCardSwitchRow(drawList, card.NextRow(), glyph, tint, tabInfoLabels[index], !muted,
                tabInfoMuteIds[index]);
            if (alerts == !muted)
            {
                continue;
            }

            if (alerts)
            {
                tab.MutedChannels.Remove(key);
            }
            else
            {
                tab.MutedChannels.Add(key);
            }

            tabs.Update(tab);
            inbox.Invalidate();
        }

        card.End();
        ChatListChrome.DrawCardGap();
    }

    private void DrawTabInfoActions(ChatTab tab, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(ui, InfoActionRows, ChatListChrome.SettingRowHeight);
        var popoutOpen = popouts.IsOpen(tabInfoRowKey);
        if (chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.ExternalLink, ChatListChrome.TintGreen,
                Loc.T(popoutOpen ? L.Linkpearl.ClosePopout : L.Linkpearl.OpenPopout), chevron: false)
            && !popouts.Toggle(tabInfoRowKey))
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PopoutLimit, LinkpearlPopouts.MaxWindows));
        }

        if (chrome.DrawCardRow(drawList, card.NextRow(), PhoneIcons.Pencil, ChatListChrome.TintAzure,
                Loc.T(L.Linkpearl.EditTab)))
        {
            OpenTabEditor(tab);
        }

        if (chrome.DrawCardDangerRow(drawList, card.NextRow(), PhoneIcons.Trash, Loc.T(L.Linkpearl.ClearHistory))
            && inbox.Find(tabInfoRowKey) is { } row)
        {
            AskClearHistory(row);
        }

        if (chrome.DrawCardDangerRow(drawList, card.NextRow(), PhoneIcons.X, Loc.T(L.Linkpearl.DeleteTab)))
        {
            AskDeleteTab(tab);
        }

        card.End();
    }

    private static LocString TimestampsLabel(ChatTab tab) => tab.Timestamps switch
    {
        null => L.Message.WallpaperDefault,
        true => L.Common.On,
        _ => L.Common.Off,
    };
}
