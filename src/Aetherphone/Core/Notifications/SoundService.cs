using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundService : IDisposable
{
    private const string RingbackFile = "ringback.wav";
    private const string AlarmFile = "alarm.wav";
    private const string TimerFile = "timer.wav";
    private const float RingbackVolume = 0.5f;
    private const int DeleteAttempts = 10;
    private const int DeleteRetryMilliseconds = 100;
    private static readonly TimeSpan NotificationLeadIn = TimeSpan.FromMilliseconds(250);

    private readonly Configuration configuration;
    private readonly SoundLibrary ringtones;
    private readonly SoundLibrary notifications;
    private readonly SoundEffectPlayer player;
    private readonly SoundEffectPlayer alarmPlayer;
    private readonly string uiDirectory;

    public SoundService(Configuration configuration, SoundLibrary ringtones, SoundLibrary notifications,
        SoundEffectPlayer player, SoundEffectPlayer alarmPlayer, string uiDirectory)
    {
        this.configuration = configuration;
        this.ringtones = ringtones;
        this.notifications = notifications;
        this.player = player;
        this.alarmPlayer = alarmPlayer;
        this.uiDirectory = uiDirectory;
    }

    public IReadOnlyList<string> Options(SoundKind kind) => For(kind).Options;

    public string Resolve(SoundKind kind, string? token) => For(kind).Resolve(token);

    public string Label(SoundKind kind, string? token)
    {
        var resolved = For(kind).Resolve(token);
        return SoundTokens.TryFile(resolved, out var fileName)
            ? SoundLibrary.PrettyFileName(fileName)
            : Loc.T(L.Catalogs.RingtoneSilent);
    }

    public void Preview(SoundKind kind, string? token, float volume)
    {
        player.StopOneShots();
        Play(kind, token, volume);
    }

    public void StopPreview() => player.StopOneShots();

    public string AddUserFile(SoundKind kind, string sourcePath) => For(kind).AddUserFile(sourcePath);

    public bool CanRemove(SoundKind kind, string token) => For(kind).IsUserFile(token);

    public void RemoveUserFile(SoundKind kind, string token)
    {
        var library = For(kind);
        if (!library.IsUserFile(token) || !library.TryUserPath(token, out var path))
        {
            return;
        }

        var wasRinging = player.Release(path);
        if (configuration.ForgetSound(kind, token))
        {
            configuration.Save();
        }

        if (wasRinging && kind == SoundKind.Ringtone)
        {
            StartCallRing();
        }

        _ = DeleteWhenReleasedAsync(library, path);
    }

    public bool TryPlayNotification(string appId, out string detail)
    {
        if (configuration.SilentMode)
        {
            detail = "silent mode";
            return false;
        }

        if (!configuration.NotificationSoundsEnabled)
        {
            detail = "notification sounds off";
            return false;
        }

        var token = configuration.ResolveNotificationToken(appId);
        if (SoundTokens.IsSilent(notifications.Resolve(token)))
        {
            detail = "silent sound chosen";
            return false;
        }

        if (!TryResolvePath(SoundKind.Notification, token, out var path))
        {
            detail = $"no playable file for {token}";
            return false;
        }

        player.PlayOnce(path, configuration.NotificationVolume, NotificationLeadIn);
        detail = Path.GetFileName(path);
        return true;
    }

    public void StartCallRing()
    {
        if (configuration.SilentMode || !configuration.RingtoneEnabled)
        {
            return;
        }

        if (TryResolvePath(SoundKind.Ringtone, configuration.RingtoneSound, out var path))
        {
            player.PlayLoop(path, configuration.RingtoneVolume);
        }
    }

    public void StartRingback()
    {
        var path = Path.Combine(uiDirectory, RingbackFile);
        if (configuration.SilentMode || !File.Exists(path))
        {
            return;
        }

        player.PlayLoop(path, RingbackVolume);
    }

    public void StartAlarmTone(AlarmRingKind kind)
    {
        var path = Path.Combine(uiDirectory, kind == AlarmRingKind.Timer ? TimerFile : AlarmFile);
        if (!File.Exists(path))
        {
            return;
        }

        alarmPlayer.PlayLoop(path, configuration.RingtoneVolume);
    }

    public void StopAlarmTone() => alarmPlayer.StopLoop();

    public void StopCallRing() => player.StopLoop();

    private SoundLibrary For(SoundKind kind) => kind == SoundKind.Ringtone ? ringtones : notifications;

    private static async Task DeleteWhenReleasedAsync(SoundLibrary library, string path)
    {
        try
        {
            for (var attempt = 0; attempt < DeleteAttempts; attempt++)
            {
                if (SoundLibrary.TryDelete(path))
                {
                    library.Refresh();
                    return;
                }

                await Task.Delay(DeleteRetryMilliseconds).ConfigureAwait(false);
            }

            AepLog.Warning($"[Sound] could not remove {Path.GetFileName(path)}: the file is still in use");
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Sound] removing {Path.GetFileName(path)} failed");
        }
    }

    private void Play(SoundKind kind, string? token, float volume)
    {
        if (TryResolvePath(kind, token, out var path))
        {
            player.PlayOnce(path, volume);
        }
    }

    private bool TryResolvePath(SoundKind kind, string? token, out string path)
    {
        var library = For(kind);
        var resolved = library.Resolve(token);
        if (SoundTokens.TryFile(resolved, out var fileName))
        {
            return library.TryResolvePath(fileName, out path);
        }

        path = string.Empty;
        return false;
    }

    public void Dispose()
    {
        player.Dispose();
        alarmPlayer.Dispose();
    }
}
