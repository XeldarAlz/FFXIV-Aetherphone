using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private readonly ChatEntranceTracker entrance = new();
    private readonly string[] chatSegmentLabels = new string[2];
    private readonly List<LinkshellEntry> roster = new();
    private string draft = string.Empty;
    private object? trackedThread;
    private int chatSegment;
    private bool followBottom;
    private bool snapToBottom;
    private bool composerFocus;

    private void DrawChatsTab(Rect content)
    {
        if (GuideIntents.Consume("messages.tab.direct"))
        {
            chatSegment = 0;
        }

        if (GuideIntents.Consume("messages.tab.linkshells"))
        {
            chatSegment = 1;
        }

        trackedThread = null;
        var scale = ImGuiHelpers.GlobalScale;
        var segRowHeight = 40f * scale;
        var segRow = new Rect(new Vector2(content.Min.X + 14f * scale, content.Min.Y),
            new Vector2(content.Max.X - 14f * scale, content.Min.Y + segRowHeight));
        UiAnchors.Report("messages.tabs", segRow);
        chatSegmentLabels[0] = Loc.T(L.Messages.TabDirect);
        chatSegmentLabels[1] = Loc.T(L.Messages.TabLinkshells);
        chatSegment = SegmentStrip.Draw("messages.tabs", segRow, chatSegmentLabels, chatSegment, frameTheme);
        var body = new Rect(new Vector2(content.Min.X, segRow.Max.Y), content.Max);
        UiAnchors.Report("messages.list", body);
        if (chatSegment == 0)
        {
            DrawDirectList(body);
        }
        else
        {
            DrawLinkshellList(body);
        }
    }

    private void DrawDirectList(Rect body)
    {
        var conversations = store.Conversations;
        if (conversations.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Messages.Empty), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < conversations.Count; index++)
            {
                if (ConversationRow.Draw(conversations[index], frameTheme, lodestone))
                {
                    conversations[index].MarkRead();
                    router.Push(LinkpearlRoute.Direct(conversations[index]));
                }
            }
        }
    }

    private void DrawLinkshellList(Rect body)
    {
        LinkshellDirectory.Collect(roster);
        var threads = linkshells.Threads;
        if (roster.Count == 0 && threads.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Messages.LinkshellsEmpty), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < roster.Count; index++)
            {
                var entry = roster[index];
                var thread = linkshells.Find(entry.Channel);
                var label = LinkshellLabel.Of(entry.Channel,
                    thread?.Name is { Length: > 0 } stored ? stored : entry.Name);
                var action = LinkshellRow.Draw(entry.Channel, label, thread, mutes.IsMuted(entry.Channel), frameTheme);
                HandleLinkshellRow(action, entry.Channel, entry.Name);
            }

            for (var index = 0; index < threads.Count; index++)
            {
                var thread = threads[index];
                if (InRoster(thread.Channel))
                {
                    continue;
                }

                var label = LinkshellLabel.Of(thread.Channel, thread.Name);
                var action = LinkshellRow.Draw(thread.Channel, label, thread, mutes.IsMuted(thread.Channel), frameTheme);
                HandleLinkshellRow(action, thread.Channel, thread.Name);
            }
        }
    }

    private void HandleLinkshellRow(LinkshellRowAction action, LinkshellChannel channel, string name)
    {
        if (action == LinkshellRowAction.ToggleMute)
        {
            mutes.Toggle(channel);
            return;
        }

        if (action == LinkshellRowAction.Open)
        {
            OpenLinkshell(channel, name);
        }
    }

    private void OpenLinkshell(LinkshellChannel channel, string name)
    {
        var thread = linkshells.GetOrCreate(channel, name);
        thread.MarkRead();
        router.Push(LinkpearlRoute.Shell(thread));
    }

    private bool InRoster(LinkshellChannel channel)
    {
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].Channel.Equals(channel))
            {
                return true;
            }
        }

        return false;
    }

    private bool DrawNotificationPauseButton(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var center = new Vector2(content.Max.X - 22f * scale, content.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var paused = notificationGate.Paused;
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var color = paused ? context.Theme.Accent : hovered ? context.Theme.TextStrong : context.Theme.TextMuted;
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center,
            paused ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, color, 15f * scale);
        var toggleRect = new Rect(min, max);
        UiAnchors.Report("messages.notifications.toggle", toggleRect);
        HoverTooltip.Show(toggleRect,
            Loc.T(paused ? L.Messages.ResumeNotifications : L.Messages.PauseNotifications));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawDirectThread(Rect area, Conversation conversation)
    {
        conversation.MarkRead();
        notifications.RemoveGroup(conversation.SendTarget);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, conversation.Contact, backToList);
        if (DrawDeleteHistoryButton(area))
        {
            AskDeleteHistory(conversation);
        }

        var bubbles = BubbleArea(area, out var composerBar);
        entrance.Sync(conversation, conversation.Lines.Count, ImGui.GetIO().DeltaTime);
        using (AppSurface.Begin(bubbles))
        {
            SyncFollow(conversation);
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            var lines = conversation.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                if (ChatBubble.Draw(lines[index], frameTheme, entrance.Progress(index)))
                {
                    OpenChatMenu(lines[index], conversation.Contact);
                }
            }

            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        DrawComposer(composerBar, frameTheme, text => bridge.Send(conversation, text));
        DrawChatMenu(area);
    }

    private bool DrawDeleteHistoryButton(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center, FontAwesomeIcon.TrashAlt,
            hovered ? frameTheme.Danger : frameTheme.TextMuted, 15f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void AskDeleteHistory(Conversation conversation)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = conversation.Contact,
            Message = Loc.T(L.Messages.DeleteHistoryConfirm),
            ConfirmLabel = Loc.T(L.Messages.DeleteHistoryButton),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Confirm = () => DeleteHistory(conversation),
        });
    }

    private void DeleteHistory(Conversation conversation)
    {
        store.Remove(conversation);
        trackedThread = null;
        router.Pop();
    }

    private void DrawLinkshellThread(Rect area, LinkshellThread thread)
    {
        thread.MarkRead();
        notifications.RemoveGroup(thread.Channel.Key);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, LinkshellLabel.Of(thread.Channel, thread.Name), backToList);
        if (DrawMuteButton(area, thread.Channel))
        {
            mutes.Toggle(thread.Channel);
        }

        var bubbles = BubbleArea(area, out var composerBar);
        entrance.Sync(thread, thread.Lines.Count, ImGui.GetIO().DeltaTime);
        using (AppSurface.Begin(bubbles))
        {
            SyncFollow(thread);
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            var lines = thread.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                if (ChatBubble.Draw(lines[index], frameTheme, entrance.Progress(index), GroupContext(lines, index)))
                {
                    OpenChatMenu(lines[index], SenderName(lines[index]));
                }
            }

            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        DrawComposer(composerBar, frameTheme, text => linkshellBridge.Send(thread, text));
        DrawChatMenu(area);
    }

    private bool DrawMuteButton(Rect area, LinkshellChannel channel)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var muted = mutes.IsMuted(channel);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var color = muted ? frameTheme.Accent : hovered ? frameTheme.TextStrong : frameTheme.TextMuted;
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center,
            muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, color, 15f * scale);
        HoverTooltip.Show(new Rect(min, max), Loc.T(muted ? L.Messages.Unmute : L.Messages.Mute));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private GroupBubble GroupContext(IReadOnlyList<ChatLine> lines, int index)
    {
        var line = lines[index];
        if (line.Direction != MessageDirection.Incoming || line.Author is not { } author)
        {
            return default;
        }

        var showHeader = true;
        if (index > 0)
        {
            var previous = lines[index - 1];
            if (previous.Direction == MessageDirection.Incoming && previous.Author is { } previousAuthor &&
                string.Equals(previousAuthor.Name, author.Name, StringComparison.Ordinal) &&
                string.Equals(previousAuthor.World, author.World, StringComparison.Ordinal))
            {
                showHeader = false;
            }
        }

        var tint = SenderTint.Of(author.Name);
        return new GroupBubble(lodestone.Avatar(author.Name, author.World), FirstName(author.Name), tint, showHeader);
    }

    private Rect BubbleArea(Rect area, out Rect composerBar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        composerBar = new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max);
        return new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));
    }

    private void SyncFollow(object thread)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (ReferenceEquals(trackedThread, thread))
        {
            followBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        }
        else
        {
            trackedThread = thread;
            followBottom = true;
        }

        if (snapToBottom)
        {
            followBottom = true;
            snapToBottom = false;
        }
    }

    private void DrawComposer(Rect bar, PhoneTheme theme, Action<string> send)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X, bar.Min.Y + 7f * scale);
        var pillMax = new Vector2(bar.Max.X, bar.Max.Y - 7f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(theme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);
        var sendDiameter = pillMax.Y - pillMin.Y - 6f * scale;
        var inputWidth = pillMax.X - pillMin.X - sendDiameter - 30f * scale;
        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 16f * scale,
            (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);
        if (composerFocus)
        {
            ImGui.SetKeyboardFocusHere();
            composerFocus = false;
        }

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##composer", Loc.T(L.Messages.Placeholder), ref draft, 480,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasText = !string.IsNullOrWhiteSpace(draft);
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f - 6f * scale, (pillMin.Y + pillMax.Y) * 0.5f);
        dl.AddCircleFilled(sendCenter, sendDiameter * 0.5f,
            ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        ProgressRing.CenterIcon(sendCenter, FontAwesomeIcon.ArrowUp, new Vector4(1f, 1f, 1f, 1f), sendDiameter * 0.46f);
        var sendMin = sendCenter - new Vector2(sendDiameter * 0.5f, sendDiameter * 0.5f);
        var sendMax = sendCenter + new Vector2(sendDiameter * 0.5f, sendDiameter * 0.5f);
        if (hasText && ImGui.IsMouseHoveringRect(sendMin, sendMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                submitted = true;
            }
        }

        if (submitted && hasText)
        {
            send(draft);
            draft = string.Empty;
            snapToBottom = true;
            composerFocus = true;
        }
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }
}
