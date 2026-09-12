using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.GameChat;

internal static unsafe class GameChatMode
{
    public static string CurrentChannelKey()
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null)
        {
            return string.Empty;
        }

        var shell = uiModule->GetRaptureShellModule();
        if (shell == null)
        {
            return string.Empty;
        }

        return GameChannels.TryResolve((XivChatType)shell->ChatType, out var channel) && channel.CanSend
               && !channel.NeedsTarget
            ? channel.Key
            : string.Empty;
    }
}
