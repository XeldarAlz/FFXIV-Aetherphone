namespace Aetherphone.Core.SystemMedia;

internal readonly record struct MediaSessionCandidate(bool IsOwnProcess, bool IsCurrent, bool IsPlaying,
    bool WasSelected);

internal static class MediaSessionPicker
{
    public const int None = -1;

    public static int Pick(ReadOnlySpan<MediaSessionCandidate> candidates)
    {
        var firstPlaying = None;
        var previous = None;
        var current = None;
        var first = None;
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate.IsOwnProcess)
            {
                continue;
            }

            if (candidate.IsCurrent && candidate.IsPlaying)
            {
                return index;
            }

            if (candidate.IsPlaying && firstPlaying == None)
            {
                firstPlaying = index;
            }

            if (candidate.WasSelected && previous == None)
            {
                previous = index;
            }

            if (candidate.IsCurrent && current == None)
            {
                current = index;
            }

            if (first == None)
            {
                first = index;
            }
        }

        if (firstPlaying != None)
        {
            return firstPlaying;
        }

        if (previous != None)
        {
            return previous;
        }

        return current != None ? current : first;
    }
}
