namespace Aetherphone.Core.Telephony.Audio;

internal readonly record struct SpeakerEntry(string Name, string EndpointId, int WaveOutIndex)
{
    public bool IsWaveOutOnly => EndpointId.Length == 0 && WaveOutIndex >= 0;
}

internal readonly record struct EndpointCounts(int Active, int Disabled, int Unplugged, int NotPresent);

internal readonly record struct SpeakerDrops(int Unnamed, int Duplicate, int WaveOutOnly);

internal static class SpeakerList
{
    public const int WaveOutNameLength = 31;

    public static bool SameDevice(string waveOutName, string endpointName)
    {
        if (string.Equals(waveOutName, endpointName, StringComparison.Ordinal))
        {
            return true;
        }

        return waveOutName.Length >= WaveOutNameLength
            && endpointName.StartsWith(waveOutName, StringComparison.Ordinal);
    }

    public static bool NeedsWaveOut(bool coreAudioAnswered, int listed, int unreadable) =>
        !coreAudioAnswered || listed == 0 || unreadable > 0;

    public static SpeakerEntry[] Merge(ReadOnlySpan<SpeakerEntry> endpoints, ReadOnlySpan<string> waveOutNames,
        bool includeWaveOutOnly, out SpeakerDrops drops)
    {
        if (!includeWaveOutOnly)
        {
            waveOutNames = ReadOnlySpan<string>.Empty;
        }

        var merged = new SpeakerEntry[endpoints.Length + waveOutNames.Length];
        var count = 0;
        var unnamed = 0;
        var duplicate = 0;
        for (var index = 0; index < endpoints.Length; index++)
        {
            var endpoint = endpoints[index];
            if (string.IsNullOrWhiteSpace(endpoint.Name))
            {
                unnamed++;
                continue;
            }

            if (IndexOfName(merged, count, endpoint.Name) >= 0)
            {
                duplicate++;
                continue;
            }

            merged[count++] = endpoint with { WaveOutIndex = -1 };
        }

        var endpointCount = count;
        var waveOutOnly = 0;
        for (var index = 0; index < waveOutNames.Length; index++)
        {
            var name = waveOutNames[index];
            if (string.IsNullOrWhiteSpace(name) || MatchesEndpoint(merged, endpointCount, name))
            {
                continue;
            }

            if (IndexOfName(merged, count, name) >= 0)
            {
                duplicate++;
                continue;
            }

            merged[count++] = new SpeakerEntry(name, string.Empty, index);
            waveOutOnly++;
        }

        drops = new SpeakerDrops(unnamed, duplicate, waveOutOnly);
        return count == merged.Length ? merged : merged[..count];
    }

    public static int IndexOfName(ReadOnlySpan<SpeakerEntry> entries, string name)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            if (string.Equals(entries[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static string Summary(int listed, SpeakerDrops drops, string coreAudioStatus, EndpointCounts counts,
        int unreadable, int waveOutCount)
    {
        var coreAudio = coreAudioStatus.Length > 0
            ? coreAudioStatus
            : $"{counts.Active} active render endpoints ({counts.Disabled} disabled, {counts.Unplugged} unplugged, " +
              $"{counts.NotPresent} not present), {unreadable} unreadable, {drops.Unnamed} unnamed";
        return $"[Audio] Speakers: listed {listed} ({listed - drops.WaveOutOnly} from Core Audio, " +
               $"{drops.WaveOutOnly} from waveOut only, {drops.Duplicate} duplicate names dropped). " +
               $"Core Audio: {coreAudio}. waveOut sees {waveOutCount} devices.";
    }

    private static int IndexOfName(SpeakerEntry[] entries, int count, string name) =>
        IndexOfName(entries.AsSpan(0, count), name);

    private static bool MatchesEndpoint(SpeakerEntry[] entries, int endpointCount, string waveOutName)
    {
        for (var index = 0; index < endpointCount; index++)
        {
            if (SameDevice(waveOutName, entries[index].Name))
            {
                return true;
            }
        }

        return false;
    }
}
