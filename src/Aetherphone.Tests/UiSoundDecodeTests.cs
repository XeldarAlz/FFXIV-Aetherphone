using Aetherphone.Core.Notifications;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UiSoundDecodeTests
{
    private const int SampleRate = 48000;
    private const int ChannelCount = 2;
    private const float OnsetFraction = 0.1f;
    private const float MaximumOnsetMilliseconds = 30f;
    private const float MaximumTailFraction = 0.01f;
    private const int TailFrames = SampleRate / 500;

    [Fact]
    public void EveryBundledClipDecodesToAudibleAudio()
    {
        var files = UiSoundCatalog.Files();
        for (var index = 0; index < files.Count; index++)
        {
            var samples = Decode(files[index]);
            var peak = Peak(samples);
            Assert.True(peak > 0.01f, $"{files[index]} decoded to a peak of {peak}");
        }
    }

    [Fact]
    public void EveryBundledClipStartsPromptly()
    {
        var files = UiSoundCatalog.Files();
        for (var index = 0; index < files.Count; index++)
        {
            var samples = Decode(files[index]);
            var threshold = Peak(samples) * OnsetFraction;
            var onset = 0;
            while (onset < samples.Length && Math.Abs(samples[onset]) < threshold)
            {
                onset++;
            }

            var milliseconds = onset / (float)ChannelCount / SampleRate * 1000f;
            Assert.True(milliseconds < MaximumOnsetMilliseconds,
                $"{files[index]} takes {milliseconds:F1} ms to reach its onset");
        }
    }

    [Fact]
    public void EveryBundledClipEndsWithoutAClick()
    {
        var files = UiSoundCatalog.Files();
        for (var index = 0; index < files.Count; index++)
        {
            var samples = Decode(files[index]);
            var limit = Peak(samples) * MaximumTailFraction;
            var tailStart = Math.Max(0, samples.Length - TailFrames * ChannelCount);
            for (var sampleIndex = tailStart; sampleIndex < samples.Length; sampleIndex++)
            {
                Assert.True(Math.Abs(samples[sampleIndex]) <= limit,
                    $"{files[index]} is still sounding at {Math.Abs(samples[sampleIndex]):F4} when it ends");
            }
        }
    }

    internal static float Peak(float[] samples)
    {
        var peak = 0f;
        for (var index = 0; index < samples.Length; index++)
        {
            peak = Math.Max(peak, Math.Abs(samples[index]));
        }

        return peak;
    }

    internal static float[] Decode(string file)
    {
        var path = Path.Combine(FindProjectRoot(), "src", "Aetherphone", "Sounds", file);
        using var reader = SoundEffectPlayer.OpenReader(path);
        var samples = reader.ToSampleProvider();
        if (samples.WaveFormat.SampleRate != SampleRate)
        {
            samples = new WdlResamplingSampleProvider(samples, SampleRate);
        }

        if (samples.WaveFormat.Channels == 1)
        {
            samples = new MonoToStereoSampleProvider(samples);
        }

        var decoded = new List<float>();
        var buffer = new float[4096];
        while (true)
        {
            var read = samples.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            for (var sampleIndex = 0; sampleIndex < read; sampleIndex++)
            {
                decoded.Add(buffer[sampleIndex]);
            }
        }

        return decoded.ToArray();
    }

    private static string FindProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Aetherphone.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
