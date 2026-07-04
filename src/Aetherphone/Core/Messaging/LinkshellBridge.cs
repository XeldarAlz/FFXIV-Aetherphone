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
    private readonly NotificationService notifications;
    private readonly IChatGui chatGui;
    private readonly GameData gameData;

    public LinkshellBridge(LinkshellStore store, NotificationService notifications, IChatGui chatGui, GameData gameData)
    {
        this.store = store;
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

        ResolveSender(message.Sender, out var name, out var world);
        if (name.Length == 0)
        {
            return;
        }

        var text = message.Message.TextValue;
        if (text.Length == 0)
        {
            return;
        }

        var display = LinkshellDirectory.Name(channel);
        var isSelf = gameData.IsLocalPlayer(name, world);
        var direction = isSelf ? MessageDirection.Outgoing : MessageDirection.Incoming;
        var author = isSelf ? null : new MessageAuthor(name, world);
        store.Append(channel, display, new ChatLine(direction, text, DateTime.Now, author));

        if (isSelf)
        {
            return;
        }

        var title = LinkshellLabel.Of(channel, display);
        notifications.Notify(new PhoneNotification("messages", title, $"{name}: {text}", DateTime.Now, MessagesAccent, channel.Key));
    }

    private void ResolveSender(SeString sender, out string name, out string world)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                name = player.PlayerName;
                world = gameData.WorldName(player.World.RowId);
                return;
            }
        }

        name = TrimGlyphs(sender.TextValue);
        world = name.Length > 0 ? gameData.WorldName(gameData.LocalHomeWorldId) : string.Empty;
    }

    private static string TrimGlyphs(string raw)
    {
        var start = 0;
        while (start < raw.Length && !char.IsLetter(raw[start]))
        {
            start++;
        }

        return start == 0 ? raw.Trim() : raw.Substring(start).Trim();
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;
}
