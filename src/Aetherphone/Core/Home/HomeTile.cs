using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Home;

internal sealed class HomeTile
{
    public required string Key { get; init; }

    public IPhoneApp? App { get; init; }

    public string FolderName { get; set; } = string.Empty;

    public List<IPhoneApp> Apps { get; } = new();

    public bool IsFolder => App is null;

    public static HomeTile ForApp(IPhoneApp app) => new() { Key = app.Id, App = app };

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
