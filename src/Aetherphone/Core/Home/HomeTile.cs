using Aetherphone.Core.Apps;
using Aetherphone.Core.Shortcuts;

namespace Aetherphone.Core.Home;

internal sealed class HomeTile : IGridTile
{
    public const int StackCapacity = 10;

    private readonly IHomeWidget? widget;
    private readonly string instanceKey = string.Empty;
    private string config = string.Empty;
    private int stackIndex;

    public required string Key { get; init; }
    public IPhoneApp? App { get; init; }
    public ShortcutEntry? Shortcut { get; init; }
    public GridCell Cell { get; set; } = HomeGridSolver.Unassigned;
    public WidgetSize Size { get; set; } = WidgetSize.Medium;
    public string FolderName { get; set; } = string.Empty;
    public string FolderTint { get; set; } = string.Empty;
    public List<HomeTile> Members { get; } = new();
    public List<HomeTile> Stack { get; } = new();
    public bool SmartRotate { get; set; } = true;
    public bool IsStack => Stack.Count > 0;
    public HomeTile Visible => IsStack ? Stack[StackIndex] : this;

    public IHomeWidget? Widget
    {
        get => IsStack ? Visible.widget : widget;
        init => widget = value;
    }

    public string InstanceKey
    {
        get => IsStack ? Visible.instanceKey : instanceKey;
        init => instanceKey = value;
    }

    public string Config
    {
        get => IsStack ? Visible.config : config;
        set
        {
            if (IsStack)
            {
                Visible.Config = value;
                return;
            }

            config = value ?? string.Empty;
        }
    }

    public int StackIndex
    {
        get => Stack.Count == 0 ? 0 : Math.Clamp(stackIndex, 0, Stack.Count - 1);
        set => stackIndex = value;
    }

    public bool IsWidget => Widget is not null;
    public bool IsShortcut => Shortcut is not null;
    public bool IsFolder => App is null && Widget is null && Shortcut is null;
    public int ColumnSpan => IsWidget ? WidgetSizes.ColumnSpan(Size) : 1;
    public int RowSpan => IsWidget ? WidgetSizes.RowSpan(Size) : 1;

    public static HomeTile ForApp(IPhoneApp app) => new() { Key = app.Id, App = app };

    public static HomeTile ForShortcut(ShortcutEntry shortcut) =>
        new() { Key = string.Concat("shortcut#", shortcut.Id.ToString("N")), Shortcut = shortcut };

    public static HomeTile ForWidget(string instanceKey, IHomeWidget widget, WidgetSize size, string config) =>
        new()
        {
            Key = string.Concat("widget#", instanceKey),
            Widget = widget,
            Size = size,
            InstanceKey = instanceKey,
            Config = config ?? string.Empty,
        };

    public static HomeTile ForStack(string key, IReadOnlyList<HomeTile> members, int visibleIndex, bool smartRotate)
    {
        var tile = new HomeTile { Key = key, SmartRotate = smartRotate };
        for (var index = 0; index < members.Count; index++)
        {
            tile.Stack.Add(members[index]);
        }

        if (members.Count > 0)
        {
            tile.Size = members[0].Size;
        }

        tile.StackIndex = visibleIndex;
        return tile;
    }

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
