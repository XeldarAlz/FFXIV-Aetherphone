using Aetherphone.Core.Telephony.Audio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VoiceNoteLevelingTests
{
    private const int SampleRate = 16000;
    private const int BlockSamples = 800;
    private const int CheckSamples = 160;
    private const int SpeechBlocks = 15;
    private const int PauseBlocks = 5;
    private const int Bursts = 4;
    private const float Frequency = 220f;
    private const float QuietSpeechAmplitude = 0.0233f;
    private const float NormalSpeechAmplitude = 0.03f;
    private const float ShoutAmplitude = 0.6f;
    private const float PauseAmplitude = 0.0007f;

    [Fact]
    public void ALevelledQuietNoteReachesTheTargetFromTheFirstWord()
    {
        var note = Note(QuietSpeechAmplitude);
        SpeechLeveler.Level(note, SampleRate, 1f);

        for (var burst = 0; burst < Bursts; burst++)
        {
            Assert.InRange(DecibelsFromTarget(BurstRms(note, burst)), -2f, 2f);
        }
    }

    [Fact]
    public void ALevelledNoteStartsAsLoudAsItEnds()
    {
        var note = Note(0.0424f);
        SpeechLeveler.Level(note, SampleRate, 1f);

        var spreadDecibels = 20f * MathF.Log10(BurstRms(note, 0) / BurstRms(note, Bursts - 1));
        Assert.InRange(spreadDecibels, -1.5f, 1.5f);
    }

    [Fact]
    public void ALevelledLoudNoteStaysUnderTheCeiling()
    {
        var note = Note(0.95f);
        var inputRms = BurstRms(note, Bursts - 1);
        SpeechLeveler.Level(note, SampleRate, 1f);

        Assert.True(PeakOf(note) <= AutoGain.AutomaticCeiling * short.MaxValue + 1f);
        Assert.True(BurstRms(note, Bursts - 1) < inputRms * 0.6f);
    }

    [Fact]
    public void LevellingLeavesSilenceAndRoomNoiseAlone()
    {
        var note = new short[BlockSamples * 40];
        FillSine(note.AsSpan(0, BlockSamples * 20), 0.0014f, 0);
        var before = (short[])note.Clone();
        var carried = SpeechLeveler.Level(note, SampleRate, 1f);

        Assert.Equal(before, note);
        Assert.Equal(1f, carried);
    }

    [Fact]
    public void AShoutAfterNormalSpeechKeepsItsShape()
    {
        var input = ShoutNote();
        var note = (short[])input.Clone();
        SpeechLeveler.Level(note, SampleRate, 1f);

        for (var start = 0; start + CheckSamples <= note.Length; start += CheckSamples)
        {
            Assert.True(Residual(input.AsSpan(start, CheckSamples), note.AsSpan(start, CheckSamples)) < -40f,
                $"distorted block at {start / (float)SampleRate:F2}s");
        }

        Assert.True(PeakOf(note) <= AutoGain.AutomaticCeiling * short.MaxValue + 1f);
    }

    [Fact]
    public void NormalSpeechAfterAShoutComesStraightBack()
    {
        var note = ShoutNote();
        SpeechLeveler.Level(note, SampleRate, 1f);

        var before = AutoGain.Rms(note.AsSpan(SampleRate, SampleRate / 2));
        var after = AutoGain.Rms(note.AsSpan(SampleRate * 3 + SampleRate * 3 / 10, SampleRate / 2));
        Assert.InRange(DecibelsFromTarget(before), -2f, 2f);
        Assert.InRange(DecibelsFromTarget(after), -2f, 2f);
    }

    [Fact]
    public void WordsRightBeforeAShoutAreNotDucked()
    {
        var note = ShoutNote();
        SpeechLeveler.Level(note, SampleRate, 1f);

        var justBefore = AutoGain.Rms(note.AsSpan(SampleRate * 175 / 100, SampleRate * 18 / 100));
        Assert.InRange(DecibelsFromTarget(justBefore), -2f, 2f);
    }

    [Fact]
    public void TheShoutIsBroughtDown()
    {
        var input = ShoutNote();
        var note = (short[])input.Clone();
        SpeechLeveler.Level(note, SampleRate, 1f);

        var shoutIn = AutoGain.Rms(input.AsSpan(SampleRate * 2 + SampleRate / 5, SampleRate / 2));
        var shoutOut = AutoGain.Rms(note.AsSpan(SampleRate * 2 + SampleRate / 5, SampleRate / 2));
        Assert.InRange(shoutOut / shoutIn, AutoGain.MinimumGain * 0.98f, AutoGain.MinimumGain * 1.02f);
    }

    [Fact]
    public void TheCarriedGainIsTheGainTheNoteEndedOn()
    {
        var note = Note(QuietSpeechAmplitude);
        var carried = SpeechLeveler.Level(note, SampleRate, 1f);

        var expected = AutoGain.TargetRms / (QuietSpeechAmplitude / MathF.Sqrt(2f));
        Assert.InRange(20f * MathF.Log10(carried / expected), -1.5f, 1.5f);
    }

    private static short[] ShoutNote()
    {
        var note = new short[SampleRate * 4];
        FillSine(note.AsSpan(0, SampleRate * 2), NormalSpeechAmplitude, 0);
        FillSine(note.AsSpan(SampleRate * 2, SampleRate), ShoutAmplitude, SampleRate * 2);
        FillSine(note.AsSpan(SampleRate * 3, SampleRate), NormalSpeechAmplitude, SampleRate * 3);
        return note;
    }

    private static short[] Note(float speechAmplitude)
    {
        var note = new short[BlockSamples * (SpeechBlocks + PauseBlocks) * Bursts];
        var phase = 0;
        for (var burst = 0; burst < Bursts; burst++)
        {
            var speechStart = burst * (SpeechBlocks + PauseBlocks) * BlockSamples;
            phase = FillSine(note.AsSpan(speechStart, SpeechBlocks * BlockSamples), speechAmplitude, phase);
            phase = FillSine(note.AsSpan(speechStart + SpeechBlocks * BlockSamples, PauseBlocks * BlockSamples),
                PauseAmplitude, phase);
        }

        return note;
    }

    private static float BurstRms(short[] note, int burst)
    {
        var speechStart = burst * (SpeechBlocks + PauseBlocks) * BlockSamples;
        return AutoGain.Rms(note.AsSpan(speechStart, SpeechBlocks * BlockSamples));
    }

    private static float DecibelsFromTarget(float rms) => 20f * MathF.Log10(rms / AutoGain.TargetRms);

    private static int PeakOf(short[] samples)
    {
        var peak = 0;
        for (var index = 0; index < samples.Length; index++)
        {
            peak = Math.Max(peak, Math.Abs((int)samples[index]));
        }

        return peak;
    }

    private static float Residual(ReadOnlySpan<short> input, ReadOnlySpan<short> output)
    {
        var inputEnergy = 0d;
        var rampedEnergy = 0d;
        var doubleRampedEnergy = 0d;
        var cross = 0d;
        var rampedCross = 0d;
        for (var index = 0; index < input.Length; index++)
        {
            var inputSample = (double)input[index];
            var outputSample = (double)output[index];
            var position = index / (double)input.Length;
            inputEnergy += inputSample * inputSample;
            rampedEnergy += position * inputSample * inputSample;
            doubleRampedEnergy += position * position * inputSample * inputSample;
            cross += inputSample * outputSample;
            rampedCross += position * inputSample * outputSample;
        }

        var determinant = inputEnergy * doubleRampedEnergy - rampedEnergy * rampedEnergy;
        if (inputEnergy <= 0 || Math.Abs(determinant) < 1e-12)
        {
            return -200f;
        }

        var gain = (cross * doubleRampedEnergy - rampedCross * rampedEnergy) / determinant;
        var ramp = (inputEnergy * rampedCross - rampedEnergy * cross) / determinant;
        var error = 0d;
        var signal = 0d;
        for (var index = 0; index < input.Length; index++)
        {
            var position = index / (double)input.Length;
            var difference = output[index] - input[index] * (gain + ramp * position);
            error += difference * difference;
            signal += output[index] * (double)output[index];
        }

        return signal <= 0 ? -200f : (float)(10 * Math.Log10(Math.Max(error, 1e-9) / signal));
    }

    private static int FillSine(Span<short> samples, float amplitude, int phase)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var angle = 2f * MathF.PI * Frequency * (phase + index) / SampleRate;
            samples[index] = (short)MathF.Round(amplitude * MathF.Sin(angle) * short.MaxValue);
        }

        return phase + samples.Length;
    }
}
