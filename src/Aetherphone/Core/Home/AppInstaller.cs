namespace Aetherphone.Core.Home;

internal sealed class AppInstaller
{
    private HomeLayoutService? layout;

    public event Action<string>? Changed;

    public void Bind(HomeLayoutService service)
    {
        if (layout is not null)
        {
            layout.InstalledChanged -= OnInstalledChanged;
        }

        layout = service;
        layout.InstalledChanged += OnInstalledChanged;
    }

    public static bool CanUninstall(string appId) => HomeLayoutService.CanUninstall(appId);

    public bool IsInstalled(string appId) => layout?.IsInstalled(appId) ?? false;

    public bool Install(string appId) => layout?.Install(appId) ?? false;

    public bool Uninstall(string appId) => layout?.Uninstall(appId) ?? false;

    public AppGate Gate(string appId) => new(this, appId);

    private void OnInstalledChanged(string appId) => Changed?.Invoke(appId);
}
