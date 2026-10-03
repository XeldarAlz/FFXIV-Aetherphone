using Aetherphone.Core.Songs;
using NAudio.Wave;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SoundCheckTests : IDisposable
{
    private const int SampleRate = 48_000;
    private const double ToneFrequency = 997.0;
    private const double Tolerance = 0.15;

    private readonly DirectoryInfo root =
        new(Path.Combine(Path.GetTempPath(), "aep-soundcheck-" + Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (root.Exists)
        {
            root.Delete(true);
        }
    }

    private static float[] Tone(double amplitude, double seconds, int channels, int sampleRate = SampleRate)
    {
        var frames = (int)(seconds * sampleRate);
        var samples = new float[frames * channels];
        for (var frame = 0; frame < frames; frame++)
        {
            var value = (float)(amplitude * Math.Sin(2.0 * Math.PI * ToneFrequency * frame / sampleRate));
            for (var channel = 0; channel < channels; channel++)
            {
                samples[frame * channels + channel] = value;
            }
        }

        return samples;
    }

    private static double Decibels(double amplitude) => 20.0 * Math.Log10(amplitude);

    [Fact]
    public void A_stereo_tone_measures_at_its_level_in_dbfs()
    {
        var meter = new LoudnessMeter(SampleRate, 2);

        meter.Process(Tone(0.1, 10, 2));

        Assert.InRange(meter.IntegratedLufs, Decibels(0.1) - Tolerance, Decibels(0.1) + Tolerance);
        Assert.InRange(meter.SamplePeak, 0.099f, 0.1001f);
    }

    [Fact]
    public void A_full_scale_tone_in_one_channel_reads_minus_three_lufs()
    {
        var meter = new LoudnessMeter(SampleRate, 1);

        meter.Process(Tone(1.0, 5, 1));

        Assert.InRange(meter.IntegratedLufs, -3.01 - Tolerance, -3.01 + Tolerance);
    }

    [Fact]
    public void A_tone_at_another_sample_rate_measures_the_same()
    {
        var meter = new LoudnessMeter(44_100, 2);

        meter.Process(Tone(0.25, 6, 2, 44_100));

        Assert.InRange(meter.IntegratedLufs, Decibels(0.25) - Tolerance, Decibels(0.25) + Tolerance);
    }

    [Fact]
    public void Silence_between_passages_is_gated_out()
    {
        var meter = new LoudnessMeter(SampleRate, 2);

        meter.Process(Tone(0.1, 5, 2));
        meter.Process(new float[SampleRate * 2 * 20]);
        meter.Process(Tone(0.1, 5, 2));

        Assert.InRange(meter.IntegratedLufs, Decibels(0.1) - Tolerance, Decibels(0.1) + Tolerance);
    }

    [Fact]
    public void Quiet_passages_more_than_ten_lu_down_fall_below_the_relative_gate()
    {
        var meter = new LoudnessMeter(SampleRate, 2);

        meter.Process(Tone(0.1, 10, 2));
        meter.Process(Tone(0.005, 10, 2));

        Assert.InRange(meter.IntegratedLufs, Decibels(0.1) - 0.3, Decibels(0.1) + Tolerance);
    }

    [Fact]
    public void Pure_silence_has_no_loudness_and_gets_no_gain()
    {
        var meter = new LoudnessMeter(SampleRate, 2);

        meter.Process(new float[SampleRate * 2 * 3]);

        Assert.True(double.IsNegativeInfinity(meter.IntegratedLufs));
        Assert.Equal(0f, SoundCheckGain.Decibels(meter.IntegratedLufs, meter.SamplePeak));
    }

    [Fact]
    public void Measuring_a_sample_provider_reads_it_to_the_end()
    {
        var provider = new BufferedSamples(Tone(0.1, 4, 2), 2);

        var result = LoudnessMeter.Measure(provider, CancellationToken.None);

        Assert.InRange(result.IntegratedLufs, Decibels(0.1) - Tolerance, Decibels(0.1) + Tolerance);
    }

    [Theory]
    [InlineData(-14.0, 0.5f, 0f)]
    [InlineData(-8.0, 1.0f, -6f)]
    [InlineData(-2.0, 1.0f, -12f)]
    [InlineData(-30.0, 0.01f, 6f)]
    [InlineData(-18.0, 0.79f, 1f)]
    [InlineData(-18.0, 1.0f, 0f)]
    public void The_gain_targets_minus_fourteen_and_never_boosts_into_clipping(double lufs, float peak,
        float expected)
    {
        Assert.Equal(expected, SoundCheckGain.Decibels(lufs, peak), 1);
    }

    [Fact]
    public void A_level_set_before_playback_applies_at_once_and_mid_track_changes_ramp()
    {
        var voice = new TrackVoice(new ConstantReader(), 0, 0f);
        var buffer = new float[TrackVoice.OutputChannels * 64];
        voice.SetLevel(0.5f, 1f);

        voice.Read(buffer, 0, buffer.Length);

        Assert.InRange(buffer[^1], 0.49f, 0.51f);

        voice.SetLevel(1f, 1f);
        voice.Read(buffer, 0, buffer.Length);

        Assert.InRange(buffer[^1], 0.5f, 0.52f);
        Assert.Equal(1f, voice.Level);
    }

    [Fact]
    public void Gains_are_stored_once_per_video_and_survive_a_reload()
    {
        using (var store = new LibraryStore(root))
        {
            store.RecordLoudnessGain("video1", -4.5f);
            store.RecordLoudnessGain("video2", float.NaN);
        }

        using var reloaded = new LibraryStore(root);

        Assert.True(reloaded.TryGetLoudnessGain("video1", out var gain));
        Assert.Equal(-4.5f, gain);
        Assert.False(reloaded.TryGetLoudnessGain("video2", out _));
    }

    private sealed class BufferedSamples : ISampleProvider
    {
        private readonly float[] samples;
        private int offset;

        public BufferedSamples(float[] samples, int channels)
        {
            this.samples = samples;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int bufferOffset, int count)
        {
            var take = Math.Min(count, samples.Length - offset);
            Array.Copy(samples, offset, buffer, bufferOffset, take);
            offset += take;
            return take;
        }
    }

    private sealed class ConstantReader : ISongAudioReader, ISampleProvider
    {
        public WaveFormat WaveFormat { get; } =
            WaveFormat.CreateIeeeFloatWaveFormat(TrackVoice.OutputSampleRate, TrackVoice.OutputChannels);

        public TimeSpan TotalTime => TimeSpan.FromMinutes(3);

        public TimeSpan CurrentTime { get; set; }

        public ISampleProvider ToSampleProvider() => this;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Fill(buffer, 1f, offset, count);
            return count;
        }

        public void Dispose()
        {
        }
    }
}
