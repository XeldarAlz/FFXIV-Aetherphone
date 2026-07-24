namespace Aetherphone.Core.Home;

internal sealed class AppInstaller
{
    private HomeLayoutService? layout;

    public void Bind(HomeLayoutService service) => layout = service;

    public static bool CanUninstall(string appId) => HomeLayoutService.CanUninstall(appId);

    public bool IsInstalled(string appId) => layout?.IsInstalled(appId) ?? false;

    public bool Install(string appId) => layout?.Install(appId) ?? false;

    public bool Uninstall(string appId) => layout?.Uninstall(appId) ?? false;
}
