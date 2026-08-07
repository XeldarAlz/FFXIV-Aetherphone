using Aetherphone.Core.Apps;
using Aetherphone.Core.Shortcuts;

namespace Aetherphone.Core.Home;

internal sealed class HomeTile : IGridTile
{
    public required string Key { get; init; }
    public IPhoneApp? App { get; init; }
    public IHomeWidget? Widget { get; init; }
    public ShortcutEntry? Shortcut { get; init; }
    public GridCell Cell { get; set; } = HomeGridSolver.Unassigned;
    public WidgetSize Size { get; set; } = WidgetSize.Medium;
    public string FolderName { get; set; } = string.Empty;
    public string FolderTint { get; set; } = string.Empty;
    public List<HomeTile> Members { get; } = new();
    public bool IsWidget => Widget is not null;
    public bool IsShortcut => Shortcut is not null;
    public bool IsFolder => App is null && Widget is null && Shortcut is null;
    public int ColumnSpan => IsWidget ? WidgetSizes.ColumnSpan(Size) : 1;
    public int RowSpan => IsWidget ? WidgetSizes.RowSpan(Size) : 1;

    public static HomeTile ForApp(IPhoneApp app) => new() { Key = app.Id, App = app };

    public static HomeTile ForShortcut(ShortcutEntry shortcut) =>
        new() { Key = string.Concat("shortcut#", shortcut.Id.ToString("N")), Shortcut = shortcut };

    public static HomeTile ForWidget(string key, IHomeWidget widget, WidgetSize size) =>
        new() { Key = key, Widget = widget, Size = size };

    public static HomeTile ForFolder(string key, string name, IReadOnlyList<HomeTile> members, string tint = "")
    {
        var tile = new HomeTile { Key = key, App = null, FolderName = name, FolderTint = tint };
        for (var index = 0; index < members.Count; index++)
        {
            tile.Members.Add(members[index]);
        }

        return tile;
    }

    public static HomeTile AsLeaf(HomeTile tile)
    {
        if (tile.IsShortcut)
        {
            return ForShortcut(tile.Shortcut!);
        }

        return ForApp(tile.App!);
    }
}
