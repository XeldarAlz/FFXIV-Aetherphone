using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Music.NowPlaying;

internal sealed class NowPlayingText
{
    private const int MaximumCrossfadeSeconds = 12;

    public static readonly int[] SleepPresets = [15, 30, 45, 60];

    private readonly string?[] crossfade = new string?[MaximumCrossfadeSeconds + 1];
    private readonly string?[] sleepMinutes = new string?[SleepPresets.Length];
    private string language = string.Empty;
    private string playingOnApp = string.Empty;
    private string playingOnText = string.Empty;

    public string Crossfade(int seconds)
    {
        EnsureLanguage();
        var clamped = Math.Clamp(seconds, 0, MaximumCrossfadeSeconds);
        if (clamped == 0)
        {
            return Loc.T(L.Common.Off);
        }

        return crossfade[clamped] ??= string.Format(Loc.Culture, Loc.T(L.Music.NowPlaying.CrossfadeSeconds), clamped);
    }

    public string SleepPreset(int presetIndex)
    {
        EnsureLanguage();
        var index = Math.Clamp(presetIndex, 0, SleepPresets.Length - 1);
        return sleepMinutes[index] ??=
            string.Format(Loc.Culture, Loc.T(L.Music.NowPlaying.SleepMinutes), SleepPresets[index]);
    }

    public string PlayingOn(string appName)
    {
        EnsureLanguage();
        if (ReferenceEquals(appName, playingOnApp) && playingOnText.Length > 0)
        {
            return playingOnText;
        }

        playingOnApp = appName;
        playingOnText = string.Format(Loc.Culture, Loc.T(L.Music.NowPlaying.PlayingOn), appName);
        return playingOnText;
    }

    private void EnsureLanguage()
    {
        var code = Loc.Current.Code;
        if (string.Equals(code, language, StringComparison.Ordinal))
        {
            return;
        }

        language = code;
        Array.Clear(crossfade);
        Array.Clear(sleepMinutes);
        playingOnApp = string.Empty;
        playingOnText = string.Empty;
    }
}
