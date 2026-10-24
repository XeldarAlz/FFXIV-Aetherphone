using Aetherphone.Core;
using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal sealed class ChatRowSheet
{
    private const int MaxPinned = 3;

    private readonly ActionSheet sheet = new();
    private readonly ActionSheet.Item[] items = new ActionSheet.Item[3];
    private readonly Configuration configuration;
    private readonly List<string> pinned;
    private readonly List<string> archived;
    private readonly LocString deleteLabel;
    private readonly Action<string> askDelete;
    private readonly Action<string> showToast;
    private readonly Action changed;
    private string? threadId;
    private string title = string.Empty;

    public ChatRowSheet(Configuration configuration, List<string> pinned, List<string> archived,
        LocString deleteLabel, Action<string> askDelete, Action<string> showToast, Action changed)
    {
        this.configuration = configuration;
        this.pinned = pinned;
        this.archived = archived;
        this.deleteLabel = deleteLabel;
        this.askDelete = askDelete;
        this.showToast = showToast;
        this.changed = changed;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Gate() => sheet.Gate();

    public void Close() => sheet.Close();

    public void Open(string id, string sheetTitle)
    {
        threadId = id;
        title = sheetTitle;
        var isPinned = pinned.Contains(id);
        items[0] = new ActionSheet.Item(Loc.T(isPinned ? L.Common.Unpin : L.Common.Pin),
            isPinned ? PhoneIcons.PinFilled : PhoneIcons.Pin);
        items[1] = new ActionSheet.Item(
            Loc.T(archived.Contains(id) ? L.Social.UnarchiveAction : L.Social.ArchiveAction), PhoneIcons.Archive);
        items[2] = new ActionSheet.Item(Loc.T(deleteLabel), PhoneIcons.Trash, true);
        sheet.Open();
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        var picked = sheet.Draw(screen, ActionSheetStyle.From(ui), items, Loc.T(L.Common.Cancel), false, title);
        if (picked < 0 || threadId is not { } id)
        {
            return;
        }

        switch (picked)
        {
            case 0:
                TogglePinned(id);
                break;
            case 1:
                ToggleArchived(id);
                break;
            case 2:
                askDelete(id);
                break;
        }
    }

    public void Forget(string id)
    {
        if (pinned.Remove(id) | archived.Remove(id))
        {
            configuration.Save();
            changed();
        }
    }

    private void TogglePinned(string id)
    {
        if (!pinned.Remove(id))
        {
            if (pinned.Count >= MaxPinned)
            {
                showToast(Loc.T(L.Social.PinChatLimit, MaxPinned));
                return;
            }

            pinned.Add(id);
            archived.Remove(id);
        }

        configuration.Save();
        changed();
    }

    private void ToggleArchived(string id)
    {
        if (!archived.Remove(id))
        {
            archived.Add(id);
            pinned.Remove(id);
        }

        configuration.Save();
        changed();
    }
}
