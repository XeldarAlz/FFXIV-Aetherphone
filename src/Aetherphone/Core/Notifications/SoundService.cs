using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundService : IDisposable
{
    private readonly Configuration configuration;
    private readonly SoundLibrary ringtones;
    private readonly SoundLibrary notifications;
    private readonly SoundEffectPlayer player;

    public SoundService(Configuration configuration, SoundLibrary ringtones, SoundLibrary notifications,
        SoundEffectPlayer player)
    {
        this.configuration = configuration;
        this.ringtones = ringtones;
        this.notifications = notifications;
        this.player = player;
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

    public void PlayNotification(string appId) =>
        Play(SoundKind.Notification, configuration.ResolveNotificationToken(appId), configuration.NotificationVolume);

    public void StartCallRing()
    {
        if (TryResolvePath(SoundKind.Ringtone, configuration.RingtoneSound, out var path))
        {
            player.PlayLoop(path, configuration.RingtoneVolume);
        }
    }

    public void StopCallRing() => player.StopLoop();

    private SoundLibrary For(SoundKind kind) => kind == SoundKind.Ringtone ? ringtones : notifications;

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

    public void Dispose() => player.Dispose();
}
