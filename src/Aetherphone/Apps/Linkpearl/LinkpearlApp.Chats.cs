using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private enum ChatFilter : byte
    {
        All,
        Tells,
        Tabs,
        Unread,
    }

    private const float CellPadX = ChatListChrome.CellPadX;
    private const float ChatSearchHeight = 52f;
    private const float ChatChipsHeight = 44f;
    private const float ChatSearchRevealSeconds = 0.16f;
    private const float HitRowHeight = 56f;
    private const float PausedBannerHeight = 36f;
    private const float EmptyTop = 60f;
    private const byte MenuPopout = 0;
    private const byte MenuMarkRead = 1;
    private const byte MenuTogglePin = 2;
    private const byte MenuToggleMute = 3;
    private const byte MenuEditTab = 4;
    private const byte MenuDeleteTab = 5;
    private const byte MenuClearHistory = 6;
    private const byte MenuSearch = 7;
    private const byte MenuWallpaper = 8;

    private readonly ChipRail filterRail = new();
    private readonly string[] filterLabels = new string[4];
    private readonly bool[] filterActive = new bool[4];
    private readonly List<ActionSheet.Item> conversationItems = new(7);
    private readonly List<byte> conversationActions = new(7);
    private Spring chatSearchReveal;
    private bool chatSearchOpen;
    private bool chatSearchFocus;
    private string conversationSheetKey = string.Empty;
    private string conversationSheetTitle = string.Empty;

    private void DrawChatsTab(Rect content)
    {
        inbox.Sync();
        var scale = UiScale.Current;
        UiAnchors.Report("messages.list", content);
        var top = DrawChatSearchRow(content, scale);
        search.Run(chatSearchQuery, inbox, chatLog);
        if (search.Active)
        {
            DrawSearchResults(new Rect(new Vector2(content.Min.X, top), content.Max));
            return;
        }

        if (inbox.Count == 0)
        {
            if (EmptyState.Draw(new Rect(new Vector2(content.Min.X, top), content.Max), ui, PhoneIcons.MessageCircle,
                    Loc.T(L.Linkpearl.EmptyTitle), Loc.T(L.Linkpearl.EmptyHint), Loc.T(L.Linkpearl.StartChat)))
            {
                OpenNewChat();
            }

            return;
        }

        if (notificationGate.Paused)
        {
            top = DrawPausedBanner(new Rect(
                new Vector2(content.Min.X + CellPadX * scale, top + Metrics.Space.Xs * scale),
                new Vector2(content.Max.X - CellPadX * scale,
                    top + Metrics.Space.Xs * scale + PausedBannerHeight * scale)));
        }

        var railTop = top + (ChatChipsHeight - ChipRail.RowHeight) * 0.5f * scale;
        DrawFilterRail(new Rect(new Vector2(content.Min.X + CellPadX * scale, railTop),
            new Vector2(content.Max.X - CellPadX * scale, railTop + ChipRail.RowHeight * scale)));
        top += ChatChipsHeight * scale;
        DrawConversationList(new Rect(new Vector2(content.Min.X, top), content.Max), scale);
    }

    private float DrawChatSearchRow(Rect area, float scale)
    {
        var target = chatSearchOpen ? 1f : 0f;
        var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var reveal = chatSearchReveal.Step(target, ChatSearchRevealSeconds, frameSeconds);
        if (chatSearchReveal.IsResting(target, 0.005f, 0.05f))
        {
            chatSearchReveal.SnapTo(target);
            reveal = target;
        }

        var height = ChatSearchHeight * scale * Math.Clamp(reveal, 0f, 1f);
        if (height < 1f)
        {
            return area.Min.Y;
        }

        var drawList = ImGui.GetWindowDrawList();
        var bottom = area.Min.Y + height;
        drawList.PushClipRect(area.Min, new Vector2(area.Max.X, bottom), true);
        var bar = new Rect(new Vector2(area.Min.X + CellPadX * scale, bottom - ChatSearchHeight * scale),
            new Vector2(area.Max.X - CellPadX * scale, bottom));
        SearchField.Draw(bar, "##linkpearlSearch", Loc.T(L.Linkpearl.SearchHint), ref chatSearchQuery, ui.Palette,
            focus: chatSearchFocus);
        chatSearchFocus = false;
        drawList.PopClipRect();
        return bottom;
    }

    private void ToggleChatSearch()
    {
        if (chatSearchOpen)
        {
            CloseChatSearch();
            return;
        }

        chatSearchOpen = true;
        chatSearchFocus = true;
    }

    private void CloseChatSearch()
    {
        chatSearchOpen = false;
        chatSearchFocus = false;
        chatSearchQuery = string.Empty;
        search.Clear();
    }

    private void ResetChatSearch()
    {
        CloseChatSearch();
        chatSearchReveal.SnapTo(0f);
    }

    private void DrawFilterRail(Rect rail)
    {
        filterLabels[0] = Loc.T(L.Linkpearl.FilterAll);
        filterLabels[1] = Loc.T(L.Linkpearl.FilterTells);
        filterLabels[2] = Loc.T(L.Linkpearl.FilterTabs);
        filterLabels[3] = Loc.T(L.Linkpearl.FilterUnread);
        for (var index = 0; index < filterActive.Length; index++)
        {
            filterActive[index] = (int)chatFilter == index;
        }

        var tapped = filterRail.Draw(rail, ui, filterLabels, filterActive, false, "messages.filters",
            ChipRail.CompactLabelPadding);
        if (tapped >= 0)
        {
            chatFilter = (ChatFilter)tapped;
        }
    }

    private float DrawPausedBanner(Rect banner)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, banner.Min, banner.Max, banner.Height * 0.5f, ImGui.GetColorU32(ink.AccentWash));
        var iconCenter = new Vector2(banner.Min.X + 18f * scale, banner.Center.Y);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.BellOff, ink.AccentLink, 18f * scale);
        var resume = Loc.T(L.Linkpearl.Resume);
        var resumeSize = Typography.Measure(resume, TextStyles.FootnoteEmphasized);
        var resumeCenter = new Vector2(banner.Max.X - Metrics.Space.Lg * scale - resumeSize.X * 0.5f, banner.Center.Y);
        var labelLeft = iconCenter.X + 16f * scale;
        var labelWidth = resumeCenter.X - resumeSize.X * 0.5f - Metrics.Space.Md * scale - labelLeft;
        var label = Typography.FitText(Loc.T(L.Linkpearl.NotificationsPaused), labelWidth, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, banner.Center.Y - labelSize.Y * 0.5f), label, ink.TitleInk,
            TextStyles.Footnote);
        if (TextButton.Draw(resumeCenter, resume, ink.AccentLink, scale))
        {
            notificationGate.SetPaused(false);
        }

        return banner.Max.Y;
    }

    private void DrawConversationList(Rect body, float scale)
    {
        using (AppSurface.BeginEdgeToEdge(body))
        {
            var pinned = inbox.Pinned;
            var drewPinned = false;
            for (var index = 0; index < pinned.Count; index++)
            {
                if (!Passes(pinned[index]))
                {
                    continue;
                }

                if (!drewPinned)
                {
                    chrome.DrawSectionLabel(Loc.T(L.Linkpearl.PinnedSection));
                    drewPinned = true;
                }

                DrawRow(pinned[index]);
            }

            var rows = inbox.Rows;
            var drewRows = false;
            for (var index = 0; index < rows.Count; index++)
            {
                if (!Passes(rows[index]))
                {
                    continue;
                }

                if (!drewRows && drewPinned)
                {
                    chrome.DrawSectionLabel(Loc.T(L.Messages.TabChats));
                }

                drewRows = true;
                DrawRow(rows[index]);
            }

            if (!drewPinned && !drewRows)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + EmptyTop * scale),
                    Loc.T(L.Linkpearl.NoFilterMatches), ink.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawRow(InboxRow row)
    {
        var action = InboxRowView.Draw(chrome, activeTheme, row, lodestone, true);
        switch (action)
        {
            case InboxRowAction.Open:
                OpenConversation(row.Key);
                break;
            case InboxRowAction.Menu:
                OpenConversationSheet(row);
                break;
            case InboxRowAction.TogglePin:
                TogglePin(row);
                break;
            case InboxRowAction.ToggleMute:
                inbox.ToggleMuted(row);
                break;
        }
    }

    private bool Passes(InboxRow row) => chatFilter switch
    {
        ChatFilter.Tells => row.IsTell,
        ChatFilter.Tabs => !row.IsTell,
        ChatFilter.Unread => row.Unread > 0,
        _ => true,
    };

    private void TogglePin(InboxRow row)
    {
        if (!row.Pinned && row.Tab is not null && tabs.PinnedCount() >= TabStore.MaxPinned)
        {
            ShellToast.Show(Loc.T(L.Linkpearl.PinLimit, TabStore.MaxPinned));
            return;
        }

        inbox.TogglePinned(row);
    }

    private void DrawSearchResults(Rect body)
    {
        var scale = UiScale.Current;
        var hits = search.Hits;
        if (hits.Count == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + EmptyTop * scale),
                Loc.T(L.Linkpearl.NoMatches), ink.MutedInk);
            return;
        }

        using (AppSurface.BeginEdgeToEdge(body))
        {
            for (var index = 0; index < hits.Count; index++)
            {
                if (DrawHitRow(hits[index], scale))
                {
                    OpenHit(hits[index]);
                }
            }
        }
    }

    private bool DrawHitRow(ChatHit hit, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, HitRowHeight * scale, ui.HoverWash);
        var row = cell.Bounds;
        var left = row.Min.X + CellPadX * scale;
        var right = row.Max.X - CellPadX * scale;
        var stamp = TimeText.Short(hit.Entry.At);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(right - stampSize.X, row.Min.Y + 9f * scale), stamp, ink.MutedInk,
            TextStyles.Caption1);
        var titleWidth = right - stampSize.X - Metrics.Space.Sm * scale - left;
        Typography.Draw(drawList, new Vector2(left, row.Min.Y + 8f * scale),
            Typography.FitText(hit.Title, titleWidth, TextStyles.SubheadlineEmphasized), ink.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var preview = hit.Entry.AuthorName.Length > 0
            ? string.Concat(hit.Entry.AuthorName, ": ", hit.Entry.Text)
            : hit.Entry.Text;
        Typography.Draw(drawList, new Vector2(left, row.Min.Y + 29f * scale),
            Typography.FitText(preview, right - left, TextStyles.Footnote), ink.MutedInk, TextStyles.Footnote);
        chrome.DrawRowHairline(drawList, cell, left);
        return cell.Tapped;
    }

    private void OpenHit(ChatHit hit)
    {
        chatThread.Reveal(hit.Entry.Id);
        OpenConversation(hit.ConversationKey);
    }

    private void OpenConversationSheet(InboxRow row, bool fromThread = false)
    {
        conversationSheetKey = row.Key;
        conversationSheetTitle = Title(row);
        conversationItems.Clear();
        conversationActions.Clear();
        if (fromThread)
        {
            AddSheetItem(L.Common.Search, MenuSearch, PhoneIcons.Search, chatThread.SearchOpen);
            AddSheetItem(L.Message.Wallpaper, MenuWallpaper, PhoneIcons.Wallpaper);
        }

        AddSheetItem(popouts.IsOpen(row.Key) ? L.Linkpearl.ClosePopout : L.Linkpearl.OpenPopout, MenuPopout,
            PhoneIcons.ExternalLink);
        if (row.Unread > 0)
        {
            AddSheetItem(L.Linkpearl.MarkRead, MenuMarkRead, PhoneIcons.Checks);
        }

        AddSheetItem(row.Pinned ? L.Common.Unpin : L.Common.Pin, MenuTogglePin,
            row.Pinned ? PhoneIcons.PinFilled : PhoneIcons.Pin);
        AddSheetItem(row.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute, MenuToggleMute,
            row.Muted ? PhoneIcons.Bell : PhoneIcons.BellOff);
        if (row.Tab is not null)
        {
            AddSheetItem(L.Linkpearl.EditTab, MenuEditTab, PhoneIcons.Pencil);
        }

        AddSheetItem(L.Linkpearl.ClearHistory, MenuClearHistory, PhoneIcons.Trash, danger: true);
        if (row.Tab is not null)
        {
            AddSheetItem(L.Linkpearl.DeleteTab, MenuDeleteTab, PhoneIcons.X, danger: true);
        }

        conversationSheet.Open();
    }

    private void DrawConversationSheet(Rect area)
    {
        if (!conversationSheet.CapturesPointer)
        {
            return;
        }

        var row = inbox.Find(conversationSheetKey);
        if (row is null)
        {
            conversationSheet.Close();
        }

        var picked = conversationSheet.Draw(area, ActionSheetStyle.From(ui),
            CollectionsMarshal.AsSpan(conversationItems), Loc.T(L.Common.Cancel), false, conversationSheetTitle);
        if (picked < 0 || row is null)
        {
            return;
        }

        RunConversationAction(row, conversationActions[picked]);
    }

    private void AddSheetItem(LocString label, byte action, string glyph, bool selected = false, bool danger = false)
    {
        conversationItems.Add(new ActionSheet.Item(Loc.T(label), glyph, danger, selected));
        conversationActions.Add(action);
    }

    private void RunConversationAction(InboxRow row, byte action)
    {
        switch (action)
        {
            case MenuSearch:
                chatThread.ToggleSearch();
                break;
            case MenuWallpaper:
                router.Push(LinkpearlRoute.Wallpaper(row.Key));
                break;
            case MenuPopout:
                if (!popouts.Toggle(row.Key))
                {
                    ShellToast.Show(Loc.T(L.Linkpearl.PopoutLimit, LinkpearlPopouts.MaxWindows));
                }

                break;
            case MenuMarkRead:
                inbox.MarkRead(row);
                inbox.FlushSeen();
                notifications.RemoveGroup(row.Key);
                break;
            case MenuTogglePin:
                TogglePin(row);
                break;
            case MenuToggleMute:
                inbox.ToggleMuted(row);
                break;
            case MenuEditTab when row.Tab is { } editTab:
                OpenTabEditor(editTab);
                break;
            case MenuDeleteTab when row.Tab is { } deleteTab:
                AskDeleteTab(deleteTab);
                break;
            case MenuClearHistory:
                AskClearHistory(row);
                break;
        }
    }

    private void AskDeleteTab(ChatTab tab) =>
        confirm.Ask(new ConfirmRequest
        {
            Title = tab.Name,
            Message = Loc.T(L.Linkpearl.DeleteTabConfirm),
            ConfirmLabel = Loc.T(L.Messages.DeleteHistoryButton),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Sheet = true,
            Confirm = () =>
            {
                popouts.Close(ChatInbox.KeyForTab(tab));
                tabs.Delete(tab);
                inbox.Invalidate();
                if (router.Current.Screen != LinkpearlScreen.Root)
                {
                    router.Reset();
                }
            },
        });

    private void AskClearHistory(InboxRow row) =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Title(row),
            Message = Loc.T(L.Linkpearl.ClearHistoryConfirm),
            ConfirmLabel = Loc.T(L.Linkpearl.ClearHistory),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Sheet = true,
            Confirm = () => ClearHistory(row),
        });

    private void ClearHistory(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            for (var index = 0; index < tab.Channels.Count; index++)
            {
                archive.Delete(tab.Channels[index]);
                chatLog.Clear(tab.Channels[index]);
            }
        }
        else
        {
            archive.Delete(row.StreamKey);
            chatLog.Clear(row.StreamKey);
        }

        inbox.Invalidate();
        threadKey = string.Empty;
    }

    private void OpenConversation(string key)
    {
        var row = inbox.Find(key);
        if (row is null)
        {
            return;
        }

        inbox.MarkRead(row);
        notifications.RemoveGroup(key);
        router.Push(LinkpearlRoute.Conversation(key));
    }

    private static string Title(InboxRow row) => row.Tab is { } tab ? tab.Name : NameMask.Display(row.Title);
}
