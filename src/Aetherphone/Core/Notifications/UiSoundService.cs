using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Notifications;

internal sealed class UiSoundService : IDisposable
{
    private readonly Configuration configuration;
    private readonly UiSoundPlayer player;
    private readonly long[] lastPlayed;
    private readonly int[] variantCursor;
    private bool hauntingWasOn;

    public UiSoundService(Configuration configuration, UiSoundPlayer player)
    {
        this.configuration = configuration;
        this.player = player;
        lastPlayed = new long[UiSoundCatalog.Entries.Length];
        variantCursor = new int[UiSoundCatalog.Entries.Length];
    }

    public void Play(UiSound sound) => Play(sound, 0f, false);

    public void PlayPitched(UiSound sound, float rate) => Play(sound, rate, true);

    public void PlayTap(UiSound sound)
    {
        if (!configuration.UiSoundTaps)
        {
            return;
        }

        Play(SeasonalMuted(sound) ? UiSound.Tap : sound, 0f, false);
    }

    private bool SeasonalMuted(UiSound sound) => !configuration.SeasonalSounds && UiSoundCatalog.IsSeasonal(sound);

    private void Play(UiSound sound, float rate, bool pitched)
    {
        if (configuration.SilentMode || !configuration.UiSounds)
        {
            return;
        }

        if (SeasonalMuted(sound))
        {
            if (UiSoundCatalog.PlainFor(sound) is not { } plain)
            {
                return;
            }

            sound = plain;
        }

        var index = (int)sound;
        ref readonly var entry = ref UiSoundCatalog.Entries[index];
        if (!ChannelEnabled(entry.Channel))
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastPlayed[index] < entry.MinimumIntervalMilliseconds)
        {
            return;
        }

        var baseVolume = entry.Channel == UiSoundChannel.Game
            ? configuration.GameSoundVolume
            : configuration.UiSoundVolume;
        var volume = entry.Gain * baseVolume;
        if (volume <= 0f)
        {
            return;
        }

        lastPlayed[index] = now;
        var files = entry.Files;
        var cursor = variantCursor[index];
        variantCursor[index] = (cursor + 1) % files.Length;
        var haunt = HauntingOn ? UiSoundCatalog.HauntDepthFor(sound) : HauntDepth.None;
        player.Play(files[cursor], volume, pitched ? rate : PlaybackRate(entry.PitchVariance), haunt);
    }

    private bool HauntingOn => SeasonalTheme.Halloween && configuration.SeasonalSounds;

    private static float PlaybackRate(float variance)
    {
        if (variance <= 0f)
        {
            return 1f;
        }

        return 1f + (Random.Shared.NextSingle() * 2f - 1f) * variance;
    }

    public void Maintain()
    {
        var haunting = HauntingOn;
        if (hauntingWasOn && !haunting)
        {
            player.ClearHaunted();
        }

        hauntingWasOn = haunting;
        player.CloseIfIdle();
    }

    private bool ChannelEnabled(UiSoundChannel channel) => channel switch
    {
        UiSoundChannel.Transition => configuration.UiSoundTransitions,
        UiSoundChannel.Tap => configuration.UiSoundTaps,
        UiSoundChannel.Toggle => configuration.UiSoundToggles,
        UiSoundChannel.Keyboard => configuration.UiSoundKeyboard,
        UiSoundChannel.Game => configuration.GameSounds,
        _ => true,
    };

    public void Dispose() => player.Dispose();
}

internal static class UiFeedback
{
    private static UiSoundService? service;

    public static void Bind(UiSoundService bound) => service = bound;

    public static void PlayTap(UiSound sound) => service?.PlayTap(sound);

    public static void Unbind() => service = null;

    public static void Play(UiSound sound) => service?.Play(sound);

    public static void PlayPitched(UiSound sound, float rate) => service?.PlayPitched(sound, rate);

}
