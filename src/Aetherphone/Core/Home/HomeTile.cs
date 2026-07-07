using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Home;

internal sealed class HomeTile : IGridTile
{
    public required string Key { get; init; }
    public IPhoneApp? App { get; init; }
    public IHomeWidget? Widget { get; init; }
    public WidgetSize Size { get; set; } = WidgetSize.Medium;
    public string FolderName { get; set; } = string.Empty;
    public List<IPhoneApp> Apps { get; } = new();
    public bool IsWidget => Widget is not null;
    public bool IsFolder => App is null && Widget is null;
    public int ColumnSpan => IsWidget ? WidgetSizes.ColumnSpan(Size) : 1;
    public int RowSpan => IsWidget ? WidgetSizes.RowSpan(Size) : 1;

    public static HomeTile ForApp(IPhoneApp app) => new() { Key = app.Id, App = app };

    public static HomeTile ForWidget(string key, IHomeWidget widget, WidgetSize size) =>
        new() { Key = key, Widget = widget, Size = size };

    public static HomeTile ForFolder(string key, string name, IReadOnlyList<IPhoneApp> apps)
    {
        var tile = new HomeTile { Key = key, App = null, FolderName = name };
        for (var index = 0; index < apps.Count; index++)
        {
            tile.Apps.Add(apps[index]);
        }

        return tile;
    }
}
