using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Messages;

internal sealed class MessagesApp : IPhoneApp
{
    private readonly struct MessagesView
    {
        public readonly Conversation? Direct;

        public readonly LinkshellThread? Linkshell;

        public MessagesView(Conversation direct)
        {
            Direct = direct;
            Linkshell = null;
        }

        public MessagesView(LinkshellThread linkshell)
        {
            Direct = null;
            Linkshell = linkshell;
        }

        public bool IsList => Direct is null && Linkshell is null;
    }

    public string Id => "messages";

    public string DisplayName => Loc.T(L.Apps.Messages);

    public string Glyph => "M";

    public Vector4 Accent => new(0.30f, 0.78f, 0.42f, 1f);

    public int BadgeCount => store.TotalUnread() + linkshells.TotalUnread();

    private readonly MessageStore store;
    private readonly LinkshellStore linkshells;
    private readonly ChatBridge bridge;
    private readonly LinkshellBridge linkshellBridge;
    private readonly MessageLauncher launcher;
    private readonly LodestoneService lodestone;

    private readonly ViewRouter<MessagesView> router;
    private readonly RouterDraw<MessagesView> drawView;
    private readonly Action backToList;
    private readonly ChatEntranceAnimator entrance = new();
    private readonly string[] tabLabels = new string[2];
    private readonly List<LinkshellEntry> roster = new();

    private string draft = string.Empty;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private object? trackedThread;
    private int activeTab;
    private bool followBottom;
    private bool snapToBottom;
    private bool composerFocus;

    public MessagesApp(MessageStore store, LinkshellStore linkshells, ChatBridge bridge, LinkshellBridge linkshellBridge, MessageLauncher launcher, LodestoneService lodestone)
    {
        this.store = store;
        this.linkshells = linkshells;
        this.bridge = bridge;
        this.linkshellBridge = linkshellBridge;
        this.launcher = launcher;
        this.lodestone = lodestone;

        router = new ViewRouter<MessagesView>(default);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        trackedThread = null;
        if (launcher.TryConsume(out var display, out var sendTarget))
        {
            activeTab = 0;
            var conversation = store.GetOrCreate(display, sendTarget);
            conversation.MarkRead();
            router.Push(new MessagesView(conversation), false);
        }
    }

    public void OnClosed()
    {
        router.Reset();
        draft = string.Empty;
        trackedThread = null;
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(MessagesView view, Rect area, int depth)
    {
        if (view.IsList)
        {
            DrawList(area);
        }
        else if (view.Direct is { } conversation)
        {
            DrawDirectThread(area, conversation);
        }
        else if (view.Linkshell is { } thread)
        {
            DrawLinkshellThread(area, thread);
        }
    }

    private void DrawList(Rect area)
    {
        trackedThread = null;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var headerBottom = area.Min.Y + AppHeader.Height * scale;
        var segRowHeight = 40f * scale;
        var segRow = new Rect(new Vector2(area.Min.X + 14f * scale, headerBottom), new Vector2(area.Max.X - 14f * scale, headerBottom + segRowHeight));

        tabLabels[0] = Loc.T(L.Messages.TabDirect);
        tabLabels[1] = Loc.T(L.Messages.TabLinkshells);
        activeTab = SegmentStrip.Draw("messages.tabs", segRow, tabLabels, activeTab, frameTheme);

        var body = new Rect(new Vector2(area.Min.X, headerBottom + segRowHeight), area.Max);
        if (activeTab == 0)
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
                    router.Push(new MessagesView(conversations[index]));
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
                var label = LinkshellLabel.Of(entry.Channel, thread?.Name is { Length: > 0 } stored ? stored : entry.Name);
                if (LinkshellRow.Draw(entry.Channel, label, thread, frameTheme))
                {
                    OpenLinkshell(entry.Channel, entry.Name);
                }
            }

            for (var index = 0; index < threads.Count; index++)
            {
                var thread = threads[index];
                if (InRoster(thread.Channel))
                {
                    continue;
                }

                var label = LinkshellLabel.Of(thread.Channel, thread.Name);
                if (LinkshellRow.Draw(thread.Channel, label, thread, frameTheme))
                {
                    OpenLinkshell(thread.Channel, thread.Name);
                }
            }
        }
    }

    private void OpenLinkshell(LinkshellChannel channel, string name)
    {
        var thread = linkshells.GetOrCreate(channel, name);
        thread.MarkRead();
        router.Push(new MessagesView(thread));
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

    private void DrawDirectThread(Rect area, Conversation conversation)
    {
        conversation.MarkRead();
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, conversation.Contact, backToList);

        var bubbles = BubbleArea(area, out var composerBar);
        entrance.Sync(conversation, conversation.Lines.Count, ImGui.GetIO().DeltaTime);

        using (AppSurface.Begin(bubbles))
        {
            SyncFollow(conversation);
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            var lines = conversation.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                ChatBubble.Draw(lines[index], frameTheme, entrance.Progress(index));
            }

            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));

            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        DrawComposer(composerBar, frameTheme, text => bridge.Send(conversation, text));
    }

    private void DrawLinkshellThread(Rect area, LinkshellThread thread)
    {
        thread.MarkRead();
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, LinkshellLabel.Of(thread.Channel, thread.Name), backToList);

        var bubbles = BubbleArea(area, out var composerBar);
        entrance.Sync(thread, thread.Lines.Count, ImGui.GetIO().DeltaTime);

        using (AppSurface.Begin(bubbles))
        {
            SyncFollow(thread);
            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
            var lines = thread.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                ChatBubble.Draw(lines[index], frameTheme, entrance.Progress(index), GroupContext(lines, index));
            }

            ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));

            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        DrawComposer(composerBar, frameTheme, text => linkshellBridge.Send(thread, text));
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
            if (previous.Direction == MessageDirection.Incoming && previous.Author is { } previousAuthor
                && string.Equals(previousAuthor.Name, author.Name, StringComparison.Ordinal)
                && string.Equals(previousAuthor.World, author.World, StringComparison.Ordinal))
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

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
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
            if (ImGui.InputTextWithHint("##composer", Loc.T(L.Messages.Placeholder), ref draft, 480, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasText = !string.IsNullOrWhiteSpace(draft);
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f - 6f * scale, (pillMin.Y + pillMax.Y) * 0.5f);
        dl.AddCircleFilled(sendCenter, sendDiameter * 0.5f, ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
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

    public void Dispose()
    {
    }
}
