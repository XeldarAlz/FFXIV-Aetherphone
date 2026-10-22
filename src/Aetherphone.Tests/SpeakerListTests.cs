using Aetherphone.Core.Telephony.Audio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SpeakerListTests
{
    private const string LongEndpoint = "Wave Link Stream (Elgato Wave Link)";
    private const string LongWaveOut = "Wave Link Stream (Elgato Wave L";

    [Fact]
    public void AHealthyCoreAudioListIgnoresWaveOut()
    {
        var endpoints = new[] { Endpoint("Speakers (Realtek(R) Audio)", "a"), Endpoint("Headset (USB Audio)", "b") };
        var waveOut = new[] { "Speakers (Realtek(R) Audio)", "Headset (USB Audio)", "Something only waveOut sees" };

        var include = SpeakerList.NeedsWaveOut(true, endpoints.Length, 0);
        var merged = SpeakerList.Merge(endpoints, waveOut, include, out var drops);

        Assert.False(include);
        Assert.Equal(2, merged.Length);
        Assert.Equal("a", merged[0].EndpointId);
        Assert.Equal(-1, merged[1].WaveOutIndex);
        Assert.Equal(0, drops.WaveOutOnly);
    }

    [Fact]
    public void AFailedCoreAudioPassFallsBackToWaveOutDeviceNumbers()
    {
        var waveOut = new[] { string.Empty, "Speakers (Realtek(R) Audio)", "Steam Streaming Speakers" };

        var include = SpeakerList.NeedsWaveOut(false, 0, 0);
        var merged = SpeakerList.Merge(ReadOnlySpan<SpeakerEntry>.Empty, waveOut, include, out var drops);

        Assert.True(include);
        Assert.Equal(2, merged.Length);
        Assert.True(merged[0].IsWaveOutOnly);
        Assert.Equal(1, merged[0].WaveOutIndex);
        Assert.Equal("Steam Streaming Speakers", merged[1].Name);
        Assert.Equal(2, merged[1].WaveOutIndex);
        Assert.Equal(2, drops.WaveOutOnly);
    }

    [Fact]
    public void AnUnreadableEndpointIsFilledInFromWaveOutWithoutDoublingTruncatedNames()
    {
        var endpoints = new[] { Endpoint(LongEndpoint, "a") };
        var waveOut = new[] { LongWaveOut, "Cam Link 4K" };

        var include = SpeakerList.NeedsWaveOut(true, endpoints.Length, 1);
        var merged = SpeakerList.Merge(endpoints, waveOut, include, out var drops);

        Assert.True(include);
        Assert.Equal(2, merged.Length);
        Assert.Equal(LongEndpoint, merged[0].Name);
        Assert.Equal("Cam Link 4K", merged[1].Name);
        Assert.Equal(1, merged[1].WaveOutIndex);
        Assert.Equal(1, drops.WaveOutOnly);
    }

    [Fact]
    public void AShortWaveOutNameIsNotMistakenForALongerEndpoint()
    {
        Assert.True(SpeakerList.SameDevice(LongWaveOut, LongEndpoint));
        Assert.True(SpeakerList.SameDevice("Speakers", "Speakers"));
        Assert.False(SpeakerList.SameDevice("Speakers", "Speakers (Realtek(R) Audio)"));
    }

    [Fact]
    public void DuplicateAndBlankEndpointNamesAreDropped()
    {
        var endpoints = new[]
        {
            Endpoint("Speakers (USB Audio)", "a"), Endpoint("Speakers (USB Audio)", "b"), Endpoint(" ", "c"),
            Endpoint("Headphones", "d"),
        };

        var merged = SpeakerList.Merge(endpoints, ReadOnlySpan<string>.Empty, false, out var drops);

        Assert.Equal(2, merged.Length);
        Assert.Equal("a", merged[0].EndpointId);
        Assert.Equal("Headphones", merged[1].Name);
        Assert.Equal(1, drops.Duplicate);
        Assert.Equal(1, drops.Unnamed);
    }

    [Theory]
    [InlineData(true, 2, 0, false)]
    [InlineData(false, 2, 0, true)]
    [InlineData(true, 0, 0, true)]
    [InlineData(true, 3, 1, true)]
    public void WaveOutFillsInOnlyWhenCoreAudioIsIncomplete(bool answered, int listed, int unreadable, bool expected)
    {
        Assert.Equal(expected, SpeakerList.NeedsWaveOut(answered, listed, unreadable));
    }

    [Fact]
    public void TheSummaryNamesEveryCountAndTheCoreAudioFailure()
    {
        var healthy = SpeakerList.Summary(3, new SpeakerDrops(0, 1, 1), string.Empty, new EndpointCounts(2, 1, 0, 4), 1,
            3);
        var failed = SpeakerList.Summary(2, new SpeakerDrops(0, 0, 2), "failed (0x80070490 COMException)", default, 0, 2);

        Assert.Contains("listed 3 (2 from Core Audio, 1 from waveOut only, 1 duplicate", healthy);
        Assert.Contains("2 active render endpoints (1 disabled, 0 unplugged, 4 not present), 1 unreadable", healthy);
        Assert.Contains("Core Audio: failed (0x80070490 COMException)", failed);
        Assert.Contains("waveOut sees 2 devices", failed);
    }

    [Fact]
    public void LookupByNameIsExact()
    {
        var entries = new[] { Endpoint("Speakers", "a"), new SpeakerEntry("Speakers (USB)", string.Empty, 3) };

        Assert.Equal(1, SpeakerList.IndexOfName(entries, "Speakers (USB)"));
        Assert.Equal(-1, SpeakerList.IndexOfName(entries, "speakers"));
    }

    private static SpeakerEntry Endpoint(string name, string id) => new(name, id, -1);
}
