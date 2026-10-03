using System.Text.Json.Serialization;

namespace Aetherphone.Core.Lyrics;

internal enum LrcLibOutcome : byte
{
    Found,
    NotFound,
    Throttled,
    Failed,
}

internal readonly struct LrcLibTrack
{
    public readonly long Id;
    public readonly string TrackName;
    public readonly string ArtistName;
    public readonly string AlbumName;
    public readonly double Duration;
    public readonly bool Instrumental;
    public readonly string PlainLyrics;
    public readonly string SyncedLyrics;

    public LrcLibTrack(long id, string trackName, string artistName, string albumName, double duration,
        bool instrumental, string plainLyrics, string syncedLyrics)
    {
        Id = id;
        TrackName = trackName ?? string.Empty;
        ArtistName = artistName ?? string.Empty;
        AlbumName = albumName ?? string.Empty;
        Duration = duration;
        Instrumental = instrumental;
        PlainLyrics = plainLyrics ?? string.Empty;
        SyncedLyrics = syncedLyrics ?? string.Empty;
    }

    public bool HasSynced => !string.IsNullOrWhiteSpace(SyncedLyrics);

    public bool HasPlain => !string.IsNullOrWhiteSpace(PlainLyrics);
}

internal readonly struct LrcLibResponse
{
    public static readonly LrcLibResponse NotFound = new(LrcLibOutcome.NotFound, Array.Empty<LrcLibTrack>());
    public static readonly LrcLibResponse Throttled = new(LrcLibOutcome.Throttled, Array.Empty<LrcLibTrack>());
    public static readonly LrcLibResponse Failed = new(LrcLibOutcome.Failed, Array.Empty<LrcLibTrack>());

    public readonly LrcLibOutcome Outcome;
    public readonly LrcLibTrack[] Tracks;

    public LrcLibResponse(LrcLibOutcome outcome, LrcLibTrack[] tracks)
    {
        Outcome = outcome;
        Tracks = tracks;
    }

    public bool ShouldBackOff => Outcome is LrcLibOutcome.Throttled or LrcLibOutcome.Failed;
}

internal sealed class LrcLibTrackDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("albumName")]
    public string? AlbumName { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("instrumental")]
    public bool Instrumental { get; set; }

    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }

    public LrcLibTrack ToTrack()
    {
        return new LrcLibTrack(Id, TrackName ?? string.Empty, ArtistName ?? string.Empty, AlbumName ?? string.Empty,
            Duration, Instrumental, PlainLyrics ?? string.Empty, SyncedLyrics ?? string.Empty);
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(LrcLibTrackDto))]
[JsonSerializable(typeof(LrcLibTrackDto[]))]
internal sealed partial class LrcLibJsonContext : JsonSerializerContext
{
}
