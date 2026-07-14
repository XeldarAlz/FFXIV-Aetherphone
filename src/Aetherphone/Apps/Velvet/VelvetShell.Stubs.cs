using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private readonly DropdownMenu postMenu = new();
    private readonly DropdownMenu threadMenu = new();
    private VelvetMessagesTab messagesTab = VelvetMessagesTab.Chats;

}
