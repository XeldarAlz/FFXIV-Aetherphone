using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private enum PostSheetAction
    {
        View,
        Edit,
        Audience,
        Delete,
        Report,
        Block,
    }

    private enum ProfileMenuAction
    {
        Settings,
        Rules,
        Report,
        NotInterested,
        Disconnect,
        Block,
    }

    private const int PostSheetMaxItems = 4;
    private const int ProfileMenuMaxItems = 5;

    private readonly ActionSheet.Item[] postSheetItems = new ActionSheet.Item[PostSheetMaxItems];
    private readonly PostSheetAction[] postSheetActions = new PostSheetAction[PostSheetMaxItems];
    private readonly ActionSheet.Item[] threadSheetItems = new ActionSheet.Item[1];
    private readonly ActionSheet profileMenu = new();
    private readonly ActionSheet.Item[] profileMenuItems = new ActionSheet.Item[ProfileMenuMaxItems];
    private readonly ProfileMenuAction[] profileMenuActions = new ProfileMenuAction[ProfileMenuMaxItems];
    private int profileMenuCount;
    private string profileMenuUserId = string.Empty;
    private string profileMenuName = string.Empty;
    private int postSheetCount;
    private bool sheetPostInFeed;
    private string postSheetTitle = string.Empty;
    private VelvetPostDto? sheetPost;
    private string? sheetThreadId;
}
