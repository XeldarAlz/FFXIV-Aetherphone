using Aetherphone.Core.Lyrics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LyricsRecordTests
{
    private const long Now = 1_790_000_000;
    private const long Day = 86_400;

    [Fact]
    public void RoundTripsSyncedLyrics()
    {
        var record = new LyricsRecord(LyricsRecordKind.Synced, "[00:01.00]Hello\n[00:02.00]World", Now);

        Assert.True(LyricsRecord.TryDecode(record.Encode(), out var decoded));
        Assert.Equal(LyricsRecordKind.Synced, decoded.Kind);
        Assert.Equal(record.Text, decoded.Text);
        Assert.Equal(Now, decoded.WrittenUnixSeconds);
        var state = decoded.ToState();
        Assert.Equal(LyricsStatus.Ready, state.Status);
        Assert.True(state.Document.IsSynced);
        Assert.Equal(2, state.Document.Count);
    }

    [Fact]
    public void RoundTripsAMissingMarker()
    {
        var record = new LyricsRecord(LyricsRecordKind.Missing, string.Empty, Now);

        Assert.True(LyricsRecord.TryDecode(record.Encode(), out var decoded));
        Assert.Equal(LyricsStatus.NotFound, decoded.ToState().Status);
    }

    [Fact]
    public void MissingMarkersExpireAfterAWeek()
    {
        var record = new LyricsRecord(LyricsRecordKind.Missing, string.Empty, Now);

        Assert.True(record.IsFresh(Now + 6 * Day));
        Assert.False(record.IsFresh(Now + 7 * Day));
    }

    [Fact]
    public void FoundLyricsOutliveTheMissingWindow()
    {
        var record = new LyricsRecord(LyricsRecordKind.Plain, "Hello", Now);

        Assert.True(record.IsFresh(Now + 30 * Day));
        Assert.False(record.IsFresh(Now + 91 * Day));
    }

    [Fact]
    public void RejectsForeignBytes()
    {
        Assert.False(LyricsRecord.TryDecode(Array.Empty<byte>(), out _));
        Assert.False(LyricsRecord.TryDecode("hello"u8.ToArray(), out _));
        Assert.False(LyricsRecord.TryDecode("aep-lyrics/1\nweird\n1\n"u8.ToArray(), out _));
    }

    [Fact]
    public void FromTrackPrefersSyncedThenPlainThenInstrumental()
    {
        var synced = new LrcLibTrack(1, "T", "A", "B", 100, false, "plain", "[00:01.00]synced");
        var plain = new LrcLibTrack(2, "T", "A", "B", 100, false, "plain", string.Empty);
        var instrumental = new LrcLibTrack(3, "T", "A", "B", 100, true, string.Empty, string.Empty);

        Assert.Equal(LyricsRecordKind.Synced, LyricsRecord.FromTrack(synced, Now).Kind);
        Assert.Equal(LyricsRecordKind.Plain, LyricsRecord.FromTrack(plain, Now).Kind);
        Assert.Equal(LyricsRecordKind.Instrumental, LyricsRecord.FromTrack(instrumental, Now).Kind);
        Assert.Equal(LyricsStatus.Instrumental, LyricsRecord.FromTrack(instrumental, Now).ToState().Status);
    }
}
