using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Video;

internal static class ScreenChatChannels
{
    internal const int Say = 1;
    internal const int Party = 2;
    internal const int FreeCompany = 4;
    internal const int Shout = 8;
    internal const int Default = Say | Party;

    internal static int Of(XivChatType kind) => kind switch
    {
        XivChatType.Say => Say,
        XivChatType.Party or XivChatType.CrossParty or XivChatType.Alliance => Party,
        XivChatType.FreeCompany => FreeCompany,
        XivChatType.Shout or XivChatType.Yell => Shout,
        _ => 0,
    };
}

internal readonly record struct ScreenChatLine(string Sender, string Text, long AddedAtTicks);

internal sealed class ScreenChatFeed : IDisposable
{
    internal const int Capacity = 5;
    internal const long LifetimeMilliseconds = 9000;

    private const int MaxTextLength = 140;
    private const char PrivateUseFirst = (char)0xE000;
    private const char PrivateUseLast = (char)0xF8FF;

    private readonly IChatGui chatGui;
    private readonly Configuration configuration;
    private readonly VideoEngine engine;
    private readonly ScreenChatLine[] lines = new ScreenChatLine[Capacity];
    private int next;

    internal ScreenChatFeed(IChatGui chatGui, Configuration configuration, VideoEngine engine)
    {
        this.chatGui = chatGui;
        this.configuration = configuration;
        this.engine = engine;
        chatGui.ChatMessage += OnChatMessage;
    }

    internal ReadOnlySpan<ScreenChatLine> Lines => lines;

    internal int Newest => (next + Capacity - 1) % Capacity;

    internal bool Enabled => configuration.VideoChatBubbles && engine.IsActive;

    internal static bool TryAge(in ScreenChatLine line, long nowTicks, out float age)
    {
        age = 0f;
        if (line.AddedAtTicks == 0)
        {
            return false;
        }

        var elapsed = nowTicks - line.AddedAtTicks;
        if (elapsed < 0 || elapsed >= LifetimeMilliseconds)
        {
            return false;
        }

        age = elapsed / (float)LifetimeMilliseconds;
        return true;
    }

    internal static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length];
        var length = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is >= PrivateUseFirst and <= PrivateUseLast || char.IsControl(character))
            {
                continue;
            }

            buffer[length] = character;
            length++;
        }

        var cleaned = new string(buffer, 0, length).Trim();
        return cleaned.Length > MaxTextLength ? cleaned[..MaxTextLength] : cleaned;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!Enabled || (ScreenChatChannels.Of(message.LogKind) & configuration.VideoChatBubbleChannels) == 0)
        {
            return;
        }

        var text = Clean(message.Message.TextValue);
        if (text.Length == 0)
        {
            return;
        }

        lines[next] = new ScreenChatLine(SenderName(message.Sender), text, Environment.TickCount64);
        next = (next + 1) % Capacity;
    }

    private static string SenderName(SeString sender)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is PlayerPayload player)
            {
                return player.PlayerName;
            }
        }

        var name = Clean(sender.TextValue);
        return name.Length > 0 ? name : Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
    }

    public void Dispose() => chatGui.ChatMessage -= OnChatMessage;
}
