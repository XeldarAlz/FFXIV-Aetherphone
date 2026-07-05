using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundService : IDisposable
{
    private readonly Configuration configuration;
    private readonly SoundLibrary library;
    private readonly SoundEffectPlayer player;
    private readonly IFramework framework;
    private volatile bool callRingLooping;

    public SoundService(Configuration configuration, SoundLibrary library, SoundEffectPlayer player,
        IFramework framework)
    {
        this.configuration = configuration;
        this.library = library;
        this.player = player;
        this.framework = framework;
    }

    public IReadOnlyList<SoundOption> Options => library.Options;

    public string Label(string token)
    {
        if (SoundTokens.TryFile(token, out var fileName))
        {
            return SoundLibrary.PrettyFileName(fileName);
        }

        if (SoundTokens.TryGame(token, out var soundId))
        {
            var label = CatalogLabels.Ringtone(soundId);
            return string.IsNullOrEmpty(label) ? Loc.T(L.Catalogs.RingtonePing) : label;
        }

        return string.Empty;
    }

    public void Preview(string token, float volume) => Play(token, volume, false);

    public string AddUserFile(string sourcePath) => library.AddUserFile(sourcePath);

    public void PlayNotification(string appId) =>
        Play(configuration.ResolveNotificationToken(appId), configuration.NotificationVolume, false);

    public void StartCallRing()
    {
        var token = configuration.RingtoneSound;
        if (SoundTokens.TryFile(token, out var fileName) && library.TryResolvePath(fileName, out var path))
        {
            callRingLooping = true;
            player.PlayLoop(path, configuration.RingtoneVolume);
            return;
        }

        callRingLooping = false;
        PlayGameSound(SoundTokens.TryGame(token, out var soundId) ? soundId : SoundTokens.DefaultGameSoundId);
    }

    public void PulseCallRing()
    {
        if (callRingLooping)
        {
            return;
        }

        var token = configuration.RingtoneSound;
        PlayGameSound(SoundTokens.TryGame(token, out var soundId) ? soundId : SoundTokens.DefaultGameSoundId);
    }

    public void StopCallRing()
    {
        callRingLooping = false;
        player.StopLoop();
    }

    private void Play(string token, float volume, bool loop)
    {
        if (SoundTokens.TryFile(token, out var fileName) && library.TryResolvePath(fileName, out var path))
        {
            if (loop)
            {
                player.PlayLoop(path, volume);
            }
            else
            {
                player.PlayOnce(path, volume);
            }

            return;
        }

        PlayGameSound(SoundTokens.TryGame(token, out var soundId) ? soundId : SoundTokens.DefaultGameSoundId);
    }

    private void PlayGameSound(uint soundId)
    {
        if (soundId == 0)
        {
            return;
        }

        _ = framework.RunOnFrameworkThread(() => UIGlobals.PlayChatSoundEffect(soundId));
    }

    public void Dispose() => player.Dispose();
}
