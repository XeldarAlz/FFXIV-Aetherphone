using Aetherphone.Core.Game;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.GameChat;

internal sealed class LinkpearlHotkey
{
    private const int RecentDepth = 8;
    private const double CycleWindowSeconds = 1.6;

    private readonly Configuration configuration;
    private readonly ChatInbox inbox;
    private readonly LinkpearlPopouts popouts;
    private readonly InboxRow[] recent = new InboxRow[RecentDepth];
    private double lastPressedAt = double.NegativeInfinity;
    private int cycleIndex = -1;

    public LinkpearlHotkey(Configuration configuration, ChatInbox inbox, LinkpearlPopouts popouts)
    {
        this.configuration = configuration;
        this.inbox = inbox;
        this.popouts = popouts;
    }

    public void Tick()
    {
        if (!configuration.LinkpearlHotkeyEnabled)
        {
            return;
        }

        var key = (VirtualKey)configuration.LinkpearlHotkeyKey;
        var chordKey = ImGuiKeyFor(key);
        if (chordKey == ImGuiKey.None || TextEntryHasFocus)
        {
            return;
        }

        if (!ImGui.IsKeyPressed(chordKey, false) || !ModifierHeld())
        {
            return;
        }

        Swallow(key);
        Open();
    }

    private static bool TextEntryHasFocus => ImGui.GetIO().WantTextInput || GameOwnsInput;

    private static unsafe bool GameOwnsInput
    {
        get
        {
            if (!GameMemory.Attached)
            {
                return false;
            }

            var module = RaptureAtkModule.Instance();
            return module is not null && module->IsTextInputActive();
        }
    }

    private bool ModifierHeld()
    {
        var io = ImGui.GetIO();
        return (VirtualKey)configuration.LinkpearlHotkeyModifier switch
        {
            VirtualKey.CONTROL => io.KeyCtrl,
            VirtualKey.MENU => io.KeyAlt,
            VirtualKey.SHIFT => io.KeyShift,
            _ => true,
        };
    }

    private void Open()
    {
        inbox.Sync();
        var count = RecentConversations.Collect(inbox.Pinned, inbox.Rows, recent);
        var now = ImGui.GetTime();
        cycleIndex = RecentConversations.NextIndex(cycleIndex, count, now - lastPressedAt <= CycleWindowSeconds);
        lastPressedAt = now;
        if (cycleIndex < 0)
        {
            return;
        }

        popouts.SetSuppressed(false);
        popouts.Open(recent[cycleIndex].Key);
    }

    private static void Swallow(VirtualKey key)
    {
        var keyState = Plugin.KeyState;
        if (keyState.IsVirtualKeyValid(key))
        {
            keyState[key] = false;
        }
    }

    private static ImGuiKey ImGuiKeyFor(VirtualKey key) =>
        key is >= VirtualKey.F1 and <= VirtualKey.F12
            ? ImGuiKey.F1 + (int)(key - VirtualKey.F1)
            : ImGuiKey.None;
}
