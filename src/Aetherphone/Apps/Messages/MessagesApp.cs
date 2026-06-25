using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Messages;

internal sealed class MessagesApp : IPhoneApp
{
    public string Id => "messages";

    public string DisplayName => "Messages";

    public string Glyph => "M";

    public Vector4 Accent => new(0.30f, 0.78f, 0.42f, 1f);

    public int BadgeCount => store.TotalUnread();

    private readonly MessageStore store;
    private readonly ChatBridge bridge;
    private readonly MessageLauncher launcher;
    private readonly LodestoneService lodestone;

    private readonly ViewRouter<Conversation?> router;
    private readonly RouterDraw<Conversation?> drawView;
    private readonly Action backToList;
    private readonly ChatEntranceAnimator entrance = new();

    private string draft = string.Empty;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private Conversation? trackedThread;
    private bool followBottom;
    private bool snapToBottom;

    public MessagesApp(MessageStore store, ChatBridge bridge, MessageLauncher launcher, LodestoneService lodestone)
    {
        this.store = store;
        this.bridge = bridge;
        this.launcher = launcher;
        this.lodestone = lodestone;

        router = new ViewRouter<Conversation?>(null);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        trackedThread = null;
        if (launcher.TryConsume(out var display, out var sendTarget))
        {
            var conversation = store.GetOrCreate(display, sendTarget);
            conversation.MarkRead();
            router.Push(conversation, false);
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

    private void DrawView(Conversation? view, Rect area, int depth)
    {
        if (view is null)
        {
            DrawConversationList(area);
        }
        else
        {
            DrawThread(area, view);
        }
    }

    private void DrawConversationList(Rect area)
    {
        trackedThread = null;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        var conversations = store.Conversations;
        if (conversations.Count == 0)
        {
            Typography.DrawCentered(body.Center, "No messages yet", frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < conversations.Count; index++)
            {
                if (ConversationRow.Draw(conversations[index], frameTheme, lodestone))
                {
                    conversations[index].MarkRead();
                    router.Push(conversations[index]);
                }
            }
        }
    }

    private void DrawThread(Rect area, Conversation conversation)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, conversation.Contact, backToList);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var bubbles = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, area.Max.Y - composerHeight));

        entrance.Sync(conversation, ImGui.GetIO().DeltaTime);

        using (AppSurface.Begin(bubbles))
        {
            if (ReferenceEquals(trackedThread, conversation))
            {
                followBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
            }
            else
            {
                trackedThread = conversation;
                followBottom = true;
            }

            if (snapToBottom)
            {
                followBottom = true;
                snapToBottom = false;
            }

            var lines = conversation.Lines;
            for (var index = 0; index < lines.Count; index++)
            {
                ChatBubble.Draw(lines[index], frameTheme, entrance.Progress(index));
            }

            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        DrawComposer(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), frameTheme, conversation);
    }

    private void DrawComposer(Rect bar, PhoneTheme theme, Conversation conversation)
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

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##composer", "Message", ref draft, 480, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        var hasText = !string.IsNullOrWhiteSpace(draft);
        var sendCenter = new Vector2(pillMax.X - sendDiameter * 0.5f - 6f * scale, (pillMin.Y + pillMax.Y) * 0.5f);
        dl.AddCircleFilled(sendCenter, sendDiameter * 0.5f, ImGui.GetColorU32(hasText ? theme.Accent : theme.SurfaceMuted), 24);
        Typography.DrawCentered(sendCenter, ">", new Vector4(1f, 1f, 1f, 1f), 1.1f);

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
            bridge.Send(conversation, draft);
            draft = string.Empty;
            snapToBottom = true;
        }
    }

    public void Dispose()
    {
    }
}
