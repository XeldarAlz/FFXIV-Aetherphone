using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Core.Platform;

internal static class GameKeys
{
    public static void Swallow(VirtualKey key)
    {
        var keyState = Plugin.KeyState;
        if (keyState.IsVirtualKeyValid(key))
        {
            keyState[key] = false;
        }
    }
}
