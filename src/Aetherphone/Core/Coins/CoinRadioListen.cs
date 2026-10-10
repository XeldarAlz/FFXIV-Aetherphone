using Aetherphone.Core.Radio;

namespace Aetherphone.Core.Coins;

internal enum RadioListenStep : byte
{
    None,
    Start,
    End,
    Switch,
}

internal static class CoinRadioListen
{
    public const long RetryMilliseconds = 30_000;
    private const int NotFoundStatus = 404;
    private const string OwnStationReason = "own_station";

    public static long RetryDelayFor(bool started, string reason, int statusCode)
    {
        if (started || statusCode == NotFoundStatus
            || string.Equals(reason, OwnStationReason, StringComparison.Ordinal))
        {
            return 0;
        }

        return RetryMilliseconds;
    }

    public static RadioListenStep Decide(string listeningStationId, RadioPlaybackState state, string stationId,
        bool signedIn)
    {
        var hasStation = signedIn && stationId.Length > 0;
        var playing = hasStation && state == RadioPlaybackState.Playing;
        if (listeningStationId.Length == 0)
        {
            return playing ? RadioListenStep.Start : RadioListenStep.None;
        }

        var sameStation = hasStation && string.Equals(listeningStationId, stationId, StringComparison.Ordinal);
        if (sameStation && (playing || IsTransient(state)))
        {
            return RadioListenStep.None;
        }

        return playing ? RadioListenStep.Switch : RadioListenStep.End;
    }

    private static bool IsTransient(RadioPlaybackState state) =>
        state is RadioPlaybackState.Buffering or RadioPlaybackState.Reconnecting;
}
