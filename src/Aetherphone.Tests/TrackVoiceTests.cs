using Aetherphone.Core.Songs;
using NAudio.Wave;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TrackVoiceTests
{
    [Fact]
    public void A_seek_the_source_refuses_faults_the_voice_instead_of_throwing_into_the_mixer()
    {
        var voice = new TrackVoice(new RefusingSeekReader(), 30, 0f);
        var buffer = new float[TrackVoice.OutputChannels * 256];

        voice.ServicePendingSeek();
        var written = voice.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, written);
        Assert.True(voice.Faulted);
        Assert.True(voice.Finished);
    }

    [Fact]
    public void The_audio_thread_outputs_silence_instead_of_waiting_while_a_seek_runs_elsewhere()
    {
        var reader = new SlowSeekReader();
        var voice = new TrackVoice(reader, 0, 0f);
        var buffer = new float[TrackVoice.OutputChannels * 256];
        voice.Seek(90);
        var seeker = new Thread(voice.ServicePendingSeek) { IsBackground = true };
        seeker.Start();
        Assert.True(reader.SeekStarted.Wait(TimeSpan.FromSeconds(5)));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var written = voice.Read(buffer, 0, buffer.Length);
        watch.Stop();

        Assert.Equal(buffer.Length, written);
        Assert.True(watch.ElapsedMilliseconds < 1000);
        Assert.All(buffer, sample => Assert.Equal(0f, sample));
        Assert.Equal(90, voice.PositionSeconds, 3);

        reader.ReleaseSeek.Set();
        Assert.True(seeker.Join(TimeSpan.FromSeconds(5)));
        Assert.False(voice.SeekPending);
        voice.Read(buffer, 0, buffer.Length);

        Assert.Contains(buffer, sample => sample != 0f);
        Assert.True(voice.PositionSeconds > 90);
    }

    [Fact]
    public void A_seek_requested_before_the_first_read_never_plays_the_start_of_the_track()
    {
        var voice = new TrackVoice(new SlowSeekReader { Blocking = false }, 45, 0f);
        var buffer = new float[TrackVoice.OutputChannels * 128];

        voice.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.Equal(0f, sample));
        Assert.True(voice.SeekPending);
        voice.ServicePendingSeek();
        Assert.Equal(45, voice.PositionSeconds, 3);
    }

    private sealed class SlowSeekReader : ISongAudioReader, ISampleProvider
    {
        private TimeSpan current;

        public ManualResetEventSlim SeekStarted { get; } = new();
        public ManualResetEventSlim ReleaseSeek { get; } = new();
        public bool Blocking { get; init; } = true;

        public WaveFormat WaveFormat { get; } =
            WaveFormat.CreateIeeeFloatWaveFormat(TrackVoice.OutputSampleRate, TrackVoice.OutputChannels);

        public TimeSpan TotalTime => TimeSpan.FromMinutes(20);

        public TimeSpan CurrentTime
        {
            get => current;
            set
            {
                SeekStarted.Set();
                if (Blocking)
                {
                    ReleaseSeek.Wait(TimeSpan.FromSeconds(10));
                }

                current = value;
            }
        }

        public ISampleProvider ToSampleProvider() => this;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Fill(buffer, 0.25f, offset, count);
            return count;
        }

        public void Dispose()
        {
        }
    }

    private sealed class RefusingSeekReader : ISongAudioReader, ISampleProvider
    {
        public WaveFormat WaveFormat { get; } =
            WaveFormat.CreateIeeeFloatWaveFormat(TrackVoice.OutputSampleRate, TrackVoice.OutputChannels);

        public TimeSpan TotalTime => TimeSpan.FromMinutes(3);

        public TimeSpan CurrentTime
        {
            get => TimeSpan.Zero;
            set => throw new IOException("stream reopen failed");
        }

        public ISampleProvider ToSampleProvider() => this;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        public void Dispose()
        {
        }
    }
}
