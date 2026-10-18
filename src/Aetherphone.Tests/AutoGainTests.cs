using Aetherphone.Core.Telephony.Audio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AutoGainTests
{
    private const int SampleRate = 48000;
    private const int BlockSamples = 960;
    private const float Frequency = 440f;

    [Fact]
    public void QuietSpeechConvergesTowardTheTarget()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        var lastToneRms = 0f;
        for (var burst = 0; burst < 12; burst++)
        {
            for (var frame = 0; frame < 15; frame++)
            {
                phase = FillSine(block, 0.0283f, phase);
                Run(gain, block, 1f, true);
                lastToneRms = AutoGain.Rms(block);
            }

            for (var frame = 0; frame < 5; frame++)
            {
                phase = FillSine(block, 0.0007f, phase);
                Run(gain, block, 1f, true);
            }
        }

        var errorDecibels = 20f * MathF.Log10(lastToneRms / AutoGain.TargetRms);
        Assert.InRange(errorDecibels, -2f, 2f);
    }

    [Fact]
    public void LoudSpeechIsBroughtDownWithoutClipping()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        var inputRms = 0f;
        var outputRms = 0f;
        for (var frame = 0; frame < 100; frame++)
        {
            phase = FillSine(block, 0.95f, phase);
            inputRms = AutoGain.Rms(block);
            Run(gain, block, 1f, true);
            outputRms = AutoGain.Rms(block);
            for (var index = 0; index < block.Length; index++)
            {
                Assert.InRange((int)block[index], -32000, 32000);
            }
        }

        Assert.InRange(gain.Gain, AutoGain.MinimumGain, AutoGain.MinimumGain * 1.02f);
        Assert.True(outputRms < inputRms * 0.6f);
    }

    [Fact]
    public void SilenceIsNeverAmplified()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        for (var frame = 0; frame < 250; frame++)
        {
            phase = FillSine(block, 0.0014f, phase);
            var before = (short[])block.Clone();
            Run(gain, block, 1f, true);
            Assert.Equal(before, block);
        }

        Array.Clear(block);
        Run(gain, block, 1f, true);
        Assert.All(block, sample => Assert.Equal(0, sample));
        Assert.Equal(1f, gain.Gain);
    }

    [Fact]
    public void GainMovesInBoundedStepsPerBlock()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        for (var frame = 0; frame < 400; frame++)
        {
            var amplitude = (frame / 25) % 2 == 0 ? 0.9f : 0.02f;
            phase = FillSine(block, amplitude, phase);
            var before = gain.Gain;
            Run(gain, block, 1f, true);
            var stepDecibels = 20f * MathF.Log10(gain.Gain / before);
            Assert.InRange(stepDecibels, -6f, 0.7f);
            Assert.InRange(gain.Gain, AutoGain.MinimumGain, AutoGain.MaximumGain);
        }
    }

    [Fact]
    public void TurnedOffLeavesTheSignalAloneAndReturnsToUnity()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        for (var frame = 0; frame < 100; frame++)
        {
            phase = FillSine(block, 0.9f, phase);
            Run(gain, block, 1f, true);
        }

        Assert.True(gain.Gain < 1f);
        phase = FillSine(block, 0.3f, phase);
        Run(gain, block, 1f, false);
        Assert.Equal(1f, gain.Gain);
        phase = FillSine(block, 0.3f, phase);
        var before = (short[])block.Clone();
        Run(gain, block, 1f, false);
        Assert.Equal(before, block);
    }

    [Fact]
    public void ManualTrimStillAppliesAndNeverClips()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        FillSine(block, 0.7f, 0);
        Run(gain, block, 2f, false);
        for (var index = 0; index < block.Length; index++)
        {
            Assert.InRange((int)block[index], -32767, 32767);
        }

        Assert.True(AutoGain.Rms(block) > 0.55f);
    }

    [Fact]
    public void LimiterKeepsEverySampleInsideFullScale()
    {
        Assert.Equal(0, AutoGain.Limit(0f));
        Assert.True(AutoGain.Limit(4f) <= AutoGain.LimiterCeiling * short.MaxValue + 1f);
        Assert.True(AutoGain.Limit(-4f) >= -AutoGain.LimiterCeiling * short.MaxValue - 1f);
        Assert.Equal((short)MathF.Round(0.5f * short.MaxValue), AutoGain.Limit(0.5f));
    }

    [Fact]
    public void ClicksAfterDigitalSilenceDoNotRaiseTheGain()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        for (var frame = 0; frame < 100; frame++)
        {
            Array.Clear(block);
            Run(gain, block, 1f, true);
        }

        for (var click = 0; click < 10; click++)
        {
            Array.Clear(block);
            for (var index = 0; index < 20; index++)
            {
                block[index] = (short)(0.5f * short.MaxValue);
            }

            Run(gain, block, 1f, true);
            for (var frame = 0; frame < 10; frame++)
            {
                Array.Clear(block);
                Run(gain, block, 1f, true);
            }
        }

        Assert.Equal(1f, gain.Gain);
    }

    [Fact]
    public void NoiseBetweenWordsKeepsTheCallGateClosed()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        var noiseGateLevel = 0f;
        for (var burst = 0; burst < 12; burst++)
        {
            for (var frame = 0; frame < 15; frame++)
            {
                phase = FillSine(block, 0.0424f, phase);
                AudioCapture.Condition(block, gain, 1f, true);
            }

            for (var frame = 0; frame < 5; frame++)
            {
                phase = FillSine(block, 0.0035f, phase);
                noiseGateLevel = AudioCapture.Condition(block, gain, 1f, true);
            }
        }

        Assert.True(gain.Gain > 2f);
        Assert.True(noiseGateLevel < AudioCapture.GateCloseRms);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2f)]
    public void TrimLeavesTheSettledLoudnessToAutomaticGain(float trim)
    {
        var errorDecibels = 20f * MathF.Log10(SettledToneRms(trim) / AutoGain.TargetRms);
        Assert.InRange(errorDecibels, -2f, 2f);
    }

    [Fact]
    public void TrimDoesNotChangeTheSettledLoudness()
    {
        var spreadDecibels = 20f * MathF.Log10(SettledToneRms(2f) / SettledToneRms(0.5f));
        Assert.InRange(spreadDecibels, -1f, 1f);
    }

    [Fact]
    public void TrimStillScalesTheCallGateLevel()
    {
        var block = new short[BlockSamples];
        FillSine(block, 0.05f, 0);
        var rawRms = AutoGain.Rms(block);
        var low = AudioCapture.Condition((short[])block.Clone(), new AutoGain(), 0.5f, true);
        var high = AudioCapture.Condition((short[])block.Clone(), new AutoGain(), 2f, true);
        Assert.Equal(rawRms * 0.5f, low, 5);
        Assert.Equal(rawRms * 2f, high, 5);
    }

    [Fact]
    public void TurnedOffTheTrimScalesTheOutput()
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        for (var frame = 0; frame < 3; frame++)
        {
            phase = FillSine(block, 0.2f, phase);
            var inputRms = AutoGain.Rms(block);
            Run(gain, block, 0.5f, false);
            Assert.InRange(AutoGain.Rms(block) / inputRms, 0.49f, 0.51f);
        }
    }

    [Fact]
    public void TogglingAutomaticGainNeverJumpsBetweenFrames()
    {
        const float level = 0.03f;
        const float trim = 2f;
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var previousLast = 0f;
        var adaptiveLevel = 0f;
        var manualLevel = 0f;
        for (var frame = 0; frame < 120; frame++)
        {
            var adaptive = frame < 50 || frame >= 70;
            Array.Fill(block, (short)MathF.Round(level * short.MaxValue));
            Run(gain, block, trim, adaptive);
            if (frame > 0)
            {
                Assert.InRange(block[0] / previousLast, 0.98f, 1.02f);
            }

            previousLast = block[^1];
            if (frame == 49)
            {
                adaptiveLevel = previousLast;
            }
            else if (frame == 69)
            {
                manualLevel = previousLast;
            }
        }

        Assert.True(MathF.Abs(adaptiveLevel / manualLevel - 1f) > 0.1f);
    }

    [Fact]
    public void ResetStartsFromACarriedGainInsideTheBounds()
    {
        var gain = new AutoGain();
        gain.Reset(3f);
        Assert.Equal(3f, gain.Gain);
        gain.Reset(40f);
        Assert.Equal(AutoGain.MaximumGain, gain.Gain);
        gain.Reset(0f);
        Assert.Equal(AutoGain.MinimumGain, gain.Gain);
    }

    private static float SettledToneRms(float trim)
    {
        var gain = new AutoGain();
        var block = new short[BlockSamples];
        var phase = 0;
        var lastToneRms = 0f;
        for (var burst = 0; burst < 12; burst++)
        {
            for (var frame = 0; frame < 15; frame++)
            {
                phase = FillSine(block, 0.0707f, phase);
                Run(gain, block, trim, true);
                lastToneRms = AutoGain.Rms(block);
            }

            for (var frame = 0; frame < 5; frame++)
            {
                phase = FillSine(block, 0.0007f, phase);
                Run(gain, block, trim, true);
            }
        }

        return lastToneRms;
    }

    private static void Run(AutoGain gain, short[] block, float trim, bool adaptive)
    {
        gain.Process(block, AutoGain.Rms(block), SampleRate, trim, adaptive);
    }

    private static int FillSine(short[] block, float amplitude, int phase)
    {
        for (var index = 0; index < block.Length; index++)
        {
            var angle = 2f * MathF.PI * Frequency * (phase + index) / SampleRate;
            block[index] = (short)MathF.Round(amplitude * MathF.Sin(angle) * short.MaxValue);
        }

        return phase + block.Length;
    }
}
