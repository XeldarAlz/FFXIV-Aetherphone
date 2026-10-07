using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameInput
{
    private static readonly VirtualKey[] ConsumedKeys =
    {
        VirtualKey.A, VirtualKey.B, VirtualKey.C, VirtualKey.D, VirtualKey.E, VirtualKey.F, VirtualKey.G, VirtualKey.H,
        VirtualKey.I, VirtualKey.J, VirtualKey.K, VirtualKey.L, VirtualKey.M, VirtualKey.N, VirtualKey.O, VirtualKey.P,
        VirtualKey.Q, VirtualKey.R, VirtualKey.S, VirtualKey.T, VirtualKey.U, VirtualKey.V, VirtualKey.W, VirtualKey.X,
        VirtualKey.Y, VirtualKey.Z,
        VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT, VirtualKey.SPACE,
        VirtualKey.BACK, VirtualKey.DELETE, VirtualKey.RETURN, VirtualKey.TAB, VirtualKey.ESCAPE,
        VirtualKey.SHIFT, VirtualKey.CONTROL,
        VirtualKey.KEY_1, VirtualKey.KEY_2, VirtualKey.KEY_3, VirtualKey.KEY_4, VirtualKey.KEY_5,
        VirtualKey.KEY_6, VirtualKey.KEY_7, VirtualKey.KEY_8, VirtualKey.KEY_9,
        VirtualKey.NUMPAD1, VirtualKey.NUMPAD2, VirtualKey.NUMPAD3, VirtualKey.NUMPAD4, VirtualKey.NUMPAD5,
        VirtualKey.NUMPAD6, VirtualKey.NUMPAD7, VirtualKey.NUMPAD8, VirtualKey.NUMPAD9,
    };

    private const long ReleaseDelayMilliseconds = 2000;

    private static int claimedFrame = -1;
    private static long lastClaimTick;
    private static bool releasePending;

    public static bool Claim()
    {
        if (!Claim(ConsumedKeys))
        {
            return false;
        }

        lastClaimTick = Environment.TickCount64;
        if (!releasePending)
        {
            releasePending = true;
            Plugin.PluginInterface.UiBuilder.Draw += HoldUntilReleased;
        }

        return true;
    }

    public static bool Claim(ReadOnlySpan<VirtualKey> consumedKeys)
    {
        if (!GameFocus.Active)
        {
            return false;
        }

        var frame = ImGui.GetFrameCount();
        if (claimedFrame == frame)
        {
            return true;
        }

        claimedFrame = frame;
        ImGui.GetIO().WantTextInput = true;
        Consume(consumedKeys);
        return true;
    }

    private static void HoldUntilReleased()
    {
        if (Environment.TickCount64 - lastClaimTick > ReleaseDelayMilliseconds)
        {
            releasePending = false;
            Plugin.PluginInterface.UiBuilder.Draw -= HoldUntilReleased;
            return;
        }

        if (claimedFrame == ImGui.GetFrameCount() || GameFocus.GameOwnsInput)
        {
            return;
        }

        ImGui.GetIO().WantTextInput = true;
        Consume(ConsumedKeys);
    }

    private static void Consume(ReadOnlySpan<VirtualKey> consumedKeys)
    {
        var keyState = Plugin.KeyState;
        for (var keyIndex = 0; keyIndex < consumedKeys.Length; keyIndex++)
        {
            var key = consumedKeys[keyIndex];
            if (keyState.IsVirtualKeyValid(key))
            {
                keyState[key] = false;
            }
        }
    }

    public static bool Held(ImGuiKey key) => Claim() && ImGui.IsKeyDown(key);

    public static bool Held(ImGuiKey key, ImGuiKey alternate) =>
        Claim() && (ImGui.IsKeyDown(key) || ImGui.IsKeyDown(alternate));

    public static bool Pressed(ImGuiKey key, bool repeat = false) => Claim() && ImGui.IsKeyPressed(key, repeat);

    public static bool Pressed(ImGuiKey key, ImGuiKey alternate, bool repeat = false) =>
        Claim() && (ImGui.IsKeyPressed(key, repeat) || ImGui.IsKeyPressed(alternate, repeat));
}
