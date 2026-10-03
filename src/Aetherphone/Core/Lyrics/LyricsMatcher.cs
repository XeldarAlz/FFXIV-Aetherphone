namespace Aetherphone.Core.Lyrics;

internal static class LyricsMatcher
{
    public const double PreferredToleranceSeconds = 3;
    public const double MaxToleranceSeconds = 8;
    public const double Rejected = -1;
    private const double MinTrackSimilarity = 0.6;
    private const double MinTrackSimilarityWithoutArtist = 0.85;
    private const double MinArtistSimilarity = 0.5;
    private const double UnknownArtistScore = 0.5;
    private const double UnknownDurationScore = 0.5;
    private const double ToleratedDurationFloor = 0.4;
    private const double TrackWeight = 0.45;
    private const double ArtistWeight = 0.3;
    private const double DurationWeight = 0.15;
    private const double SyncedBonus = 0.1;
    private const double PlainBonus = 0.05;

    public static int PickBest(ReadOnlySpan<LrcLibTrack> tracks, in LyricsQuery query)
    {
        if (tracks.Length == 0 || query.IsEmpty)
        {
            return -1;
        }

        var candidates = query.Candidates;
        var normalizedArtists = new string[candidates.Length];
        var normalizedTracks = new string[candidates.Length];
        for (var index = 0; index < candidates.Length; index++)
        {
            normalizedArtists[index] = LyricsText.Normalize(candidates[index].Artist);
            normalizedTracks[index] = LyricsText.Normalize(candidates[index].Track);
        }

        var bestIndex = -1;
        var bestScore = Rejected;
        for (var trackIndex = 0; trackIndex < tracks.Length; trackIndex++)
        {
            var score = Score(tracks[trackIndex], query.DurationSeconds, normalizedArtists, normalizedTracks);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestIndex = trackIndex;
        }

        return bestIndex;
    }

    public static double DurationScore(double expectedSeconds, double actualSeconds)
    {
        if (expectedSeconds <= 0 || actualSeconds <= 0)
        {
            return UnknownDurationScore;
        }

        var difference = Math.Abs(expectedSeconds - actualSeconds);
        if (difference <= PreferredToleranceSeconds)
        {
            return 1;
        }

        if (difference > MaxToleranceSeconds)
        {
            return Rejected;
        }

        var overshoot = (difference - PreferredToleranceSeconds) / (MaxToleranceSeconds - PreferredToleranceSeconds);
        return 1 - overshoot * (1 - ToleratedDurationFloor);
    }

    private static double Score(in LrcLibTrack track, int durationSeconds, string[] normalizedArtists,
        string[] normalizedTracks)
    {
        if (!track.Instrumental && !track.HasSynced && !track.HasPlain)
        {
            return Rejected;
        }

        var durationScore = DurationScore(durationSeconds, track.Duration);
        if (durationScore < 0)
        {
            return Rejected;
        }

        var durationKnown = durationSeconds > 0 && track.Duration > 0;
        var resultTrack = LyricsText.Normalize(track.TrackName);
        var resultArtist = LyricsText.Normalize(track.ArtistName);
        var contentBonus = track.HasSynced ? SyncedBonus : PlainBonus;
        var best = Rejected;
        for (var index = 0; index < normalizedTracks.Length; index++)
        {
            var trackSimilarity = LyricsText.Similarity(normalizedTracks[index], resultTrack);
            if (trackSimilarity < MinTrackSimilarity)
            {
                continue;
            }

            double artistScore;
            if (normalizedArtists[index].Length == 0)
            {
                if (!durationKnown || trackSimilarity < MinTrackSimilarityWithoutArtist)
                {
                    continue;
                }

                artistScore = UnknownArtistScore;
            }
            else
            {
                artistScore = LyricsText.Similarity(normalizedArtists[index], resultArtist);
                if (artistScore < MinArtistSimilarity)
                {
                    continue;
                }
            }

            var score = TrackWeight * trackSimilarity + ArtistWeight * artistScore + DurationWeight * durationScore
                + contentBonus;
            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }
}
