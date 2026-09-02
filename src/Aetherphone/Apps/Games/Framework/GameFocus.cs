using Aetherphone.Core.Game;
using Aetherphone.Windows.Components;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameFocus
{
    public static bool Active => UiInteract.WindowFocused && !GameOwnsInput;

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
}
