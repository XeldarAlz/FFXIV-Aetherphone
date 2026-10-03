namespace Aetherphone.Core.SystemMedia;

internal struct ArtworkRefresh
{
    public const long ConfirmDelayMilliseconds = 2000;
    public const long MissingRetryMilliseconds = 1500;
    public const int MaximumMissingRetries = 3;

    private int trackKey;
    private bool hasTrack;
    private bool confirmPending;
    private int missingRetries;
    private long nextFetchAt;

    public bool ShouldFetch(int currentTrackKey, long nowMilliseconds)
    {
        if (!hasTrack || currentTrackKey != trackKey)
        {
            hasTrack = true;
            trackKey = currentTrackKey;
            confirmPending = true;
            missingRetries = 0;
            nextFetchAt = 0;
            return true;
        }

        return nextFetchAt != 0 && nowMilliseconds >= nextFetchAt;
    }

    public void Fetched(bool hasArtwork, long nowMilliseconds)
    {
        if (confirmPending)
        {
            confirmPending = false;
            nextFetchAt = nowMilliseconds + ConfirmDelayMilliseconds;
            return;
        }

        if (!hasArtwork && missingRetries < MaximumMissingRetries)
        {
            missingRetries++;
            nextFetchAt = nowMilliseconds + MissingRetryMilliseconds;
            return;
        }

        nextFetchAt = 0;
    }

    public void Reset() => this = default;
}
