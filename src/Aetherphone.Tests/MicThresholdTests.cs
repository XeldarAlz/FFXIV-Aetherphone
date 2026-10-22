using Aetherphone.Core.Telephony.Audio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MicThresholdTests
{
    [Fact]
    public void ZeroIsWideOpen()
    {
        Assert.Equal(0f, MicThreshold.ToRms(0f));
        Assert.True(MicThreshold.Opens(0f, 0f));
        Assert.True(MicThreshold.Opens(0.00001f, 0f));
    }

    [Fact]
    public void HigherNumbersNeedLouderSpeech()
    {
        Assert.True(MicThreshold.ToRms(0.6f) > MicThreshold.ToRms(0.3f));
        Assert.Equal(1f, MicThreshold.ToRms(1f), 4);
        Assert.Equal(-30f, 20f * MathF.Log10(MicThreshold.ToRms(0.5f)), 3);
        Assert.False(MicThreshold.Opens(0.01f, 0.8f));
        Assert.True(MicThreshold.Opens(0.3f, 0.8f));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.42f)]
    [InlineData(0.9f)]
    public void TheMeterUsesTheSameScaleAsTheSlider(float position)
    {
        Assert.Equal(position, MicThreshold.MeterFill(MicThreshold.ToRms(position)), 4);
    }

    [Fact]
    public void TheMeterStaysInsideTheTrack()
    {
        Assert.Equal(0f, MicThreshold.MeterFill(0f));
        Assert.Equal(0f, MicThreshold.MeterFill(0.00001f));
        Assert.Equal(1f, MicThreshold.MeterFill(2f));
    }

    [Fact]
    public void TheDefaultMatchesTheOldGate()
    {
        Assert.InRange(MicThreshold.FromLegacyGain(1f), MicThreshold.DefaultPosition - 0.01f,
            MicThreshold.DefaultPosition + 0.01f);
    }

    [Fact]
    public void AnOldSensitivityCarriesOver()
    {
        Assert.True(MicThreshold.FromLegacyGain(2f) < MicThreshold.DefaultPosition);
        Assert.True(MicThreshold.FromLegacyGain(0.5f) > MicThreshold.DefaultPosition);
        Assert.Equal(MicThreshold.FromLegacyGain(0.25f), MicThreshold.FromLegacyGain(0f));
        Assert.InRange(MicThreshold.FromLegacyGain(0f), 0f, 0.7f);
    }

    [Fact]
    public void ReadoutsRunFromZeroToOneHundred()
    {
        Assert.Equal(101, MicThreshold.Readouts.Count);
        Assert.Equal("0", MicThreshold.Readouts[0]);
        Assert.Equal("42", MicThreshold.Readouts[42]);
        Assert.Equal("100", MicThreshold.Readouts[100]);
    }

    [Fact]
    public void TheGateOpensAtTheThresholdAndClosesAfterTheHangover()
    {
        var openRms = MicThreshold.ToRms(MicThreshold.DefaultPosition);
        var hangover = 0;
        var open = AudioCaptureGate(false, ref hangover, openRms * 1.1f, openRms);
        Assert.True(open);
        open = AudioCaptureGate(open, ref hangover, openRms * 0.8f, openRms);
        Assert.True(open);
        var frames = 0;
        while (open && frames < 100)
        {
            open = AudioCaptureGate(open, ref hangover, openRms * 0.2f, openRms);
            frames++;
        }

        Assert.False(open);
        Assert.InRange(frames, 10, 14);
    }

    [Fact]
    public void AWideOpenThresholdNeverClosesTheGate()
    {
        var hangover = 0;
        Assert.True(AudioCaptureGate(false, ref hangover, 0f, 0f));
        Assert.True(AudioCaptureGate(true, ref hangover, 0f, 0f));
    }

    private static bool AudioCaptureGate(bool open, ref int hangover, float rms, float openRms) =>
        AudioCapture.NextGateState(open, ref hangover, rms, openRms);
}
