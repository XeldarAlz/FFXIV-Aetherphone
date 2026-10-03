using System.Globalization;
using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Settings;

internal sealed class InstalledAppList : IDisposable
{
    private static readonly Comparison<AppSettingsEntry> ByName = CompareByName;
    private readonly AppInstaller installer;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly List<AppSettingsEntry> scratch = new();
    private readonly Action<string> onInstalledChanged;
    private AppSettingsEntry[] entries = Array.Empty<AppSettingsEntry>();
    private LanguageInfo? language;
    private bool dirty = true;

    public InstalledAppList(AppInstaller installer, IReadOnlyList<IPhoneApp> apps)
    {
        this.installer = installer;
        this.apps = apps;
        onInstalledChanged = OnInstalledChanged;
        installer.Changed += onInstalledChanged;
    }

    public int Revision { get; private set; }

    public ReadOnlySpan<AppSettingsEntry> Entries
    {
        get
        {
            EnsureFresh();
            return entries;
        }
    }

    public bool IsInstalled(string appId) => installer.IsInstalled(appId);

    public void Dispose() => installer.Changed -= onInstalledChanged;

    private void EnsureFresh()
    {
        if (!dirty && ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        dirty = false;
        language = Loc.Current;
        scratch.Clear();
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (!installer.IsInstalled(app.Id))
            {
                continue;
            }

            scratch.Add(new AppSettingsEntry(app.Id, app.DisplayName, app.Accent, NotificationChannels.Contains(app.Id),
                app.HasBadge, app));
        }

        scratch.Sort(ByName);
        entries = scratch.ToArray();
        Revision++;
    }

    private static int CompareByName(AppSettingsEntry left, AppSettingsEntry right)
    {
        var primary = Loc.Culture.CompareInfo.Compare(left.Name, right.Name, CompareOptions.IgnoreCase);
        return primary != 0 ? primary : string.CompareOrdinal(left.AppId, right.AppId);
    }

    private void OnInstalledChanged(string appId) => dirty = true;
}
