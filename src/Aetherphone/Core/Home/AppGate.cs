namespace Aetherphone.Core.Home;

internal readonly struct AppGate
{
    private readonly AppInstaller? installer;
    private readonly string appId;

    public AppGate(AppInstaller installer, string appId)
    {
        this.installer = installer;
        this.appId = appId;
    }

    public bool Open => installer is null || installer.IsInstalled(appId);

    public string AppId => appId;
}
