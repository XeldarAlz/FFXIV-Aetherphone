using Aetherphone.Core.Telephony.Audio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class VoiceNoteLevelingTests
{
    private const int SampleRate = 16000;
    private const int BlockSamples = 800;
    private const int SpeechBlocks = 15;
    private const int PauseBlocks = 5;
    private const int Bursts = 4;
    private const float Frequency = 220f;
    private const float QuietSpeechAmplitude = 0.0233f;
    private const float PauseAmplitude = 0.0007f;

    [Fact]
    public void TheCallGateKeepsAQuietMicFarBelowTheTarget()
    {
        var note = Note(QuietSpeechAmplitude);
        var gain = new AutoGain();
        for (var start = 0; start < note.Length; start += BlockSamples)
        {
            var block = note.AsSpan(start, BlockSamples);
            gain.Process(block, AutoGain.Rms(block), SampleRate, 1f, true);
        }

        Assert.True(DecibelsFromTarget(BurstRms(note, 0)) < -12f);
        Assert.True(DecibelsFromTarget(BurstRms(note, Bursts - 1)) < -12f);
    }

    [Fact]
    public void ALevelledQuietNoteReachesTheTargetFromTheFirstWord()
    {
        var note = Note(QuietSpeechAmplitude);
        Record(note, new AutoGain(0f));

        for (var burst = 0; burst < Bursts; burst++)
        {
            Assert.InRange(DecibelsFromTarget(BurstRms(note, burst)), -2f, 2f);
        }
    }

    [Fact]
    public void ALevelledNoteStartsAsLoudAsItEnds()
    {
        var note = Note(0.0424f);
        Record(note, new AutoGain(0f));

        var spreadDecibels = 20f * MathF.Log10(BurstRms(note, 0) / BurstRms(note, Bursts - 1));
        Assert.InRange(spreadDecibels, -1.5f, 1.5f);
    }

    [Fact]
    public void ALevelledLoudNoteNeverClips()
    {
        var note = Note(0.95f);
        var inputRms = BurstRms(note, Bursts - 1);
        Record(note, new AutoGain(0f));

        for (var index = 0; index < note.Length; index++)
        {
            Assert.InRange((int)note[index], -32000, 32000);
        }

        Assert.True(BurstRms(note, Bursts - 1) < inputRms * 0.6f);
    }

    [Fact]
    public void LevellingLeavesSilenceAndRoomNoiseAlone()
    {
        var note = new short[BlockSamples * 40];
        FillSine(note.AsSpan(0, BlockSamples * 20), 0.0014f, 0);
        var before = (short[])note.Clone();
        var gain = new AutoGain(0f);
        Record(note, gain);

        Assert.Equal(before, note);
        Assert.Equal(1f, gain.Gain);
    }

    private static void Record(short[] note, AutoGain gain)
    {
        for (var start = 0; start < note.Length; start += BlockSamples)
        {
            var block = note.AsSpan(start, BlockSamples);
            gain.Observe(AutoGain.Rms(block), block.Length, SampleRate);
        }

        gain.Level(note, SampleRate, BlockSamples);
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
