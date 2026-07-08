using System.Numerics;
using Aetherphone.Core.Game;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Messaging;

internal sealed class LinkshellBridge : IDisposable
{
    private static readonly Vector4 MessagesAccent = new(0.30f, 0.78f, 0.42f, 1f);
    private readonly LinkshellStore store;
    private readonly LinkshellMuteStore mutes;
    private readonly NotificationService notifications;
    private readonly IChatGui chatGui;
    private readonly GameData gameData;

    public LinkshellBridge(LinkshellStore store, LinkshellMuteStore mutes, NotificationService notifications,
        IChatGui chatGui, GameData gameData)
    {
        this.store = store;
        this.mutes = mutes;
        this.notifications = notifications;
        this.chatGui = chatGui;
        this.gameData = gameData;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Send(LinkshellThread thread, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        ChatSender.TrySend($"{thread.Channel.Command} {trimmed}");
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!LinkshellChannels.TryResolve(message.LogKind, out var channel))
        {
            return;
        }

        var text = message.Message.TextValue;
        if (text.Length == 0)
        {
            return;
        }

        var display = LinkshellDirectory.Name(channel);
        if (!TryResolveMember(message.Sender, out var name, out var world) || gameData.IsLocalPlayer(name, world))
        {
            store.Append(channel, display, new ChatLine(MessageDirection.Outgoing, text, DateTime.Now, null));
            return;
        }

        store.Append(channel, display,
            new ChatLine(MessageDirection.Incoming, text, DateTime.Now, new MessageAuthor(name, world)));
        if (mutes.IsMuted(channel))
        {
            return;
        }

        var title = LinkshellLabel.Of(channel, display);
        notifications.Notify(new PhoneNotification("messages", title, $"{name}: {text}", DateTime.Now, MessagesAccent,
            channel.Key));
    }

    private bool TryResolveMember(SeString sender, out string name, out string world)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                name = player.PlayerName;
                world = gameData.WorldName(player.World.RowId);
                return true;
            }
        }

        name = string.Empty;
        world = string.Empty;
        return false;
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;
}
