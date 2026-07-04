using System.Numerics;
using Aetherphone.Core.Game;
using Aetherphone.Core.Notifications;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Messaging;

internal sealed class ChatBridge : IDisposable
{
    private static readonly Vector4 MessagesAccent = new(0.30f, 0.78f, 0.42f, 1f);

    private readonly MessageStore store;
    private readonly NotificationService notifications;
    private readonly IChatGui chatGui;
    private readonly GameData gameData;

    public ChatBridge(MessageStore store, NotificationService notifications, IChatGui chatGui, GameData gameData)
    {
        this.store = store;
        this.notifications = notifications;
        this.chatGui = chatGui;
        this.gameData = gameData;
        chatGui.ChatMessage += OnChatMessage;
    }

    public void Send(Conversation conversation, string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        ChatSender.TrySend($"/tell {conversation.SendTarget} {trimmed}");
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var kind = message.LogKind;
        if (kind != XivChatType.TellIncoming && kind != XivChatType.TellOutgoing)
        {
            return;
        }

        if (!TryResolve(message.Sender, out var display, out var sendTarget))
        {
            return;
        }

        if (display.StartsWith("Gm ", StringComparison.Ordinal))
        {
            return;
        }

        var incoming = kind == XivChatType.TellIncoming;
        var text = message.Message.TextValue;
        store.Append(display, sendTarget, new ChatLine(incoming ? MessageDirection.Incoming : MessageDirection.Outgoing, text, DateTime.Now));

        if (incoming)
        {
            notifications.Notify(new PhoneNotification("messages", display, text, DateTime.Now, MessagesAccent, sendTarget));
        }
    }

    private bool TryResolve(SeString sender, out string display, out string sendTarget)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                display = player.PlayerName;
                var world = gameData.WorldName(player.World.RowId);
                sendTarget = world.Length > 0 ? $"{display}@{world}" : display;
                return true;
            }
        }

        var name = sender.TextValue;
        if (name.Length == 0)
        {
            display = string.Empty;
            sendTarget = string.Empty;
            return false;
        }

        display = name;
        var homeWorld = gameData.WorldName(gameData.LocalHomeWorldId);
        sendTarget = homeWorld.Length > 0 ? $"{name}@{homeWorld}" : name;
        return true;
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;
}
