namespace Aetherphone.Core.Apps;

internal interface ITabRouteTarget
{
    void OpenTab(string tab);
}

internal struct PendingTab
{
    private string requested;

    public void Request(string tab) => requested = tab;

    public bool Take(string tab)
    {
        if (!string.Equals(requested, tab, StringComparison.Ordinal))
        {
            return false;
        }

        requested = string.Empty;
        return true;
    }
}
