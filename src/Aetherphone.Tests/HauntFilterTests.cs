using Aetherphone.Core.Notifications;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HauntFilterTests
{
    private const int ChannelCount = 2;
    private const float OnsetFraction = 0.1f;
    private const float MaximumOnsetMilliseconds = 30f;
    private const float MaximumTailFraction = 0.01f;
    private const int TailFrames = HauntFilter.SampleRate / 500;
    private const float MaximumGrowth = 1.12f;

    [Theory]
    [InlineData(nameof(UiSound.Tap), nameof(HauntDepth.Light))]
    [InlineData(nameof(UiSound.ToggleOn), nameof(HauntDepth.Light))]
    [InlineData(nameof(UiSound.Keystroke), nameof(HauntDepth.Faint))]
    [InlineData(nameof(UiSound.AppOpen), nameof(HauntDepth.Full))]
    [InlineData(nameof(UiSound.Success), nameof(HauntDepth.Full))]
    [InlineData(nameof(UiSound.GamePop), nameof(HauntDepth.None))]
    [InlineData(nameof(UiSound.HalloweenKnock), nameof(HauntDepth.None))]
    [InlineData(nameof(UiSound.HalloweenFlare), nameof(HauntDepth.None))]
    public void DepthFollowsTheChannel(string soundName, string depthName)
    {
        var sound = Enum.Parse<UiSound>(soundName);

        Assert.Equal(Enum.Parse<HauntDepth>(depthName), UiSoundCatalog.HauntDepthFor(sound));
    }

    [Fact]
    public void NoDepthLeavesTheClipUntouched()
    {
        var clip = new float[] { 0.5f, 0.5f, -0.25f, -0.25f };

        Assert.Same(clip, HauntFilter.Apply(clip, HauntDepth.None));
    }

    [Fact]
    public void EveryHauntedClipKeepsItsShape()
    {
        for (var soundIndex = 0; soundIndex < UiSoundCatalog.Entries.Length; soundIndex++)
        {
            var depth = UiSoundCatalog.HauntDepthFor((UiSound)soundIndex);
            if (depth == HauntDepth.None)
            {
                continue;
            }

            var files = UiSoundCatalog.Entries[soundIndex].Files;
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var plain = UiSoundDecodeTests.Decode(files[fileIndex]);
                var haunted = HauntFilter.Apply(plain, depth);
                AssertShape(files[fileIndex], plain, haunted);
            }
        }
    }

    private static void AssertShape(string file, float[] plain, float[] haunted)
    {
        Assert.True(haunted.Length > plain.Length, $"{file} gained no tail");
        var plainPeak = UiSoundDecodeTests.Peak(plain);
        var peak = UiSoundDecodeTests.Peak(haunted);
        Assert.True(peak <= MathF.Min(plainPeak * MaximumGrowth, 0.99f) + 1e-4f, $"{file} grew to a peak of {peak}");

        var threshold = peak * OnsetFraction;
        var onset = 0;
        while (onset < haunted.Length && MathF.Abs(haunted[onset]) < threshold)
        {
            onset++;
        }

        var milliseconds = onset / (float)ChannelCount / HauntFilter.SampleRate * 1000f;
        Assert.True(milliseconds < MaximumOnsetMilliseconds, $"{file} starts {milliseconds:F1} ms late");

        var limit = peak * MaximumTailFraction;
        for (var sampleIndex = haunted.Length - TailFrames * ChannelCount; sampleIndex < haunted.Length; sampleIndex++)
        {
            Assert.True(MathF.Abs(haunted[sampleIndex]) <= limit, $"{file} clicks as it ends");
        }
    }
}
