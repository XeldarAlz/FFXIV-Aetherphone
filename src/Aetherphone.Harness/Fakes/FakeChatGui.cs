using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeChatGui : IChatGui
{
    private readonly Dictionary<(string PluginName, uint CommandId), Action<uint, SeString>> linkHandlers = new();

    public event IChatGui.OnHandleableChatMessageDelegate? ChatMessage { add { } remove { } }

    public event IChatGui.OnHandleableChatMessageDelegate? CheckMessageHandled { add { } remove { } }

    public event IChatGui.OnChatMessageDelegate? ChatMessageHandled { add { } remove { } }

    public event IChatGui.OnChatMessageDelegate? ChatMessageUnhandled { add { } remove { } }

    public event IChatGui.OnLogMessageDelegate? LogMessage { add { } remove { } }

    public uint LastLinkedItemId => 0;

    public byte LastLinkedItemFlags => 0;

    public IReadOnlyDictionary<(string PluginName, uint CommandId), Action<uint, SeString>> RegisteredLinkHandlers => linkHandlers;

    public DalamudLinkPayload AddChatLinkHandler(uint commandId, Action<uint, SeString> commandAction)
    {
        linkHandlers[("Aetherphone", commandId)] = commandAction;
        return null!;
    }

    public void RemoveChatLinkHandler(uint commandId) => linkHandlers.Remove(("Aetherphone", commandId));

    public void RemoveChatLinkHandler() => linkHandlers.Clear();

    public void Print(XivChatEntry chat) => HarnessLog.Plugin("chat", chat.Message?.TextValue ?? string.Empty);

    public void Print(string message, string? messageTag = null, ushort? tagColor = null) => HarnessLog.Plugin("chat", message);

    public void Print(SeString message, string? messageTag = null, ushort? tagColor = null) => HarnessLog.Plugin("chat", message.TextValue);

    public void PrintError(string message, string? messageTag = null, ushort? tagColor = null) => HarnessLog.Plugin("chat-error", message);

    public void PrintError(SeString message, string? messageTag = null, ushort? tagColor = null) => HarnessLog.Plugin("chat-error", message.TextValue);

    public void Print(ReadOnlySpan<byte> message, string? messageTag = null, ushort? tagColor = null) =>
        HarnessLog.Plugin("chat", System.Text.Encoding.UTF8.GetString(message));

    public void PrintError(ReadOnlySpan<byte> message, string? messageTag = null, ushort? tagColor = null) =>
        HarnessLog.Plugin("chat-error", System.Text.Encoding.UTF8.GetString(message));
}
