using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly DropdownMenu postMenu = new();
    private readonly DropdownMenu threadMenu = new();
    private VelvetMessagesTab messagesTab = VelvetMessagesTab.Chats;

}
