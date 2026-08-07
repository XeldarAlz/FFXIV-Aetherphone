using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly DropdownMenu postMenu = new();
    private readonly DropdownMenu threadMenu = new();
    private readonly DropdownMenu.Item[] postItems = new DropdownMenu.Item[3];
    private VelvetMessagesTab messagesTab = VelvetMessagesTab.Chats;
    private VelvetPostDto? menuPost;
}
