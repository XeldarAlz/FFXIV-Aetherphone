using Aetherphone.Core.Home;
using Aetherphone.Core.Jam;

namespace Aetherphone.Core.Playback;

internal sealed class MusicUninstallStop : IDisposable
{
    public const string MusicAppId = "music";

    private readonly AppInstaller installer;
    private readonly PlaybackHub playback;
    private readonly JamSession jam;

    public MusicUninstallStop(AppInstaller installer, PlaybackHub playback, JamSession jam)
    {
        this.installer = installer;
        this.playback = playback;
        this.jam = jam;
        installer.Changed += OnInstalledChanged;
    }

    public void Dispose() => installer.Changed -= OnInstalledChanged;

    private void OnInstalledChanged(string appId)
    {
        if (!string.Equals(appId, MusicAppId, StringComparison.Ordinal) || installer.IsInstalled(appId))
        {
            return;
        }

        jam.End();
        playback.Stop();
    }
}
