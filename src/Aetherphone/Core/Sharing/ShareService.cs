using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;

namespace Aetherphone.Core.Sharing;

internal sealed class ShareService
{
    private readonly AppInstaller installer;
    private readonly List<IPhoneApp> targets = new();
    private IReadOnlyList<IPhoneApp> apps = Array.Empty<IPhoneApp>();
    private INavigator? navigator;

    public ShareService(AppInstaller installer)
    {
        this.installer = installer;
    }

    public ShareItem? Pending { get; private set; }

    public IReadOnlyList<IPhoneApp> Targets => targets;

    public void Bind(IReadOnlyList<IPhoneApp> registeredApps, INavigator shellNavigator)
    {
        apps = registeredApps;
        navigator = shellNavigator;
    }

    public bool CanShare(ShareKind kind, string sourceAppId)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (IsTarget(apps[index], kind, sourceAppId))
            {
                return true;
            }
        }

        return false;
    }

    public void Offer(in ShareItem item)
    {
        targets.Clear();
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (IsTarget(app, item.Kind, item.SourceAppId))
            {
                targets.Add(app);
            }
        }

        Pending = targets.Count > 0 ? item : null;
    }

    public void Pick(IPhoneApp app)
    {
        if (Pending is not { } item)
        {
            return;
        }

        Pending = null;
        app.OnShare(item);
        navigator?.Open(app.Id);
    }

    public void Dismiss()
    {
        Pending = null;
    }

    private bool IsTarget(IPhoneApp app, ShareKind kind, string sourceAppId)
    {
        return app.Id != sourceAppId && app.IsAvailable && installer.IsInstalled(app.Id) &&
               ShareKinds.Contains(app.AcceptedShares, kind);
    }
}
