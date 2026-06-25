namespace Aetherphone.Core.Home;

[Serializable]
internal sealed class HomeLayout
{
    public List<HomePage> Pages { get; set; } = new();
}

[Serializable]
internal sealed class HomePage
{
    public List<HomeItem> Items { get; set; } = new();
}

[Serializable]
internal sealed class HomeItem
{
    public string Kind { get; set; } = "app";

    public string AppId { get; set; } = string.Empty;

    public string FolderName { get; set; } = string.Empty;

    public List<string> AppIds { get; set; } = new();
}
