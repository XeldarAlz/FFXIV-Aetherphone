using Aetherphone.Core.Notifications;
using NAudio.Wave;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SoundEffectPlayerTests
{
    [Fact]
    public void OpensWavWithoutMediaFoundation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetherphone-{Guid.NewGuid():N}.wav");
        try
        {
            WriteSilentPcmWav(path);
            using var reader = SoundEffectPlayer.OpenReader(path);

            Assert.IsType<WaveFileReader>(reader);
            Assert.Equal(8000, reader.WaveFormat.SampleRate);
            Assert.Equal(1, reader.WaveFormat.Channels);
            Assert.True(reader.Length > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void OpensMp3WithManagedDecoder()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.mp3");
        using var reader = SoundEffectPlayer.OpenReader(path);
        var buffer = new byte[4096];

        Assert.IsType<Mp3FileReaderBase>(reader);
        Assert.True(reader.Read(buffer, 0, buffer.Length) > 0);
    }

    private static void WriteSilentPcmWav(string path)
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int sampleCount = 800;
        var dataBytes = sampleCount * channels * bitsPerSample / 8;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);
        writer.Write(new byte[dataBytes]);
    }
}
