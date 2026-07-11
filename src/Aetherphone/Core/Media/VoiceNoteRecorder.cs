using NAudio.Wave;

namespace Aetherphone.Core.Media;

internal sealed class VoiceNoteRecorder : IDisposable
{
    public const int MaxSeconds = 150;
    private const int SampleRate = 16000;
    private const int BytesPerSecond = SampleRate * 2;
    private const int MaxBytes = BytesPerSecond * MaxSeconds;

    private readonly object gate = new();
    private MemoryStream? buffer;
    private WaveInEvent? waveIn;
    private DateTime startedUtc;
    private volatile bool recording;
    private volatile float level;

    public bool Recording => recording;

    public float Level => level;

    public float ElapsedSeconds => recording ? (float)(DateTime.UtcNow - startedUtc).TotalSeconds : 0f;

    public bool AtCapacity
    {
        get
        {
            lock (gate)
            {
                return buffer is not null && buffer.Length >= MaxBytes;
            }
        }
    }

    public bool Start(int deviceIndex)
    {
        Cancel();
        try
        {
            var capture = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 4,
            };
            capture.DataAvailable += OnData;
            lock (gate)
            {
                buffer = new MemoryStream();
            }

            capture.StartRecording();
            waveIn = capture;
            startedUtc = DateTime.UtcNow;
            recording = true;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Voice note recording failed to start: {exception.Message}");
            recording = false;
            return false;
        }
    }

    public bool Stop(out byte[] wavBytes, out int durationSeconds)
    {
        StopCapture();
        byte[] pcm;
        lock (gate)
        {
            pcm = buffer?.ToArray() ?? Array.Empty<byte>();
            buffer = null;
        }

        recording = false;
        level = 0f;
        durationSeconds = (int)MathF.Round(pcm.Length / (float)BytesPerSecond);
        if (pcm.Length < BytesPerSecond / 2)
        {
            wavBytes = Array.Empty<byte>();
            return false;
        }

        durationSeconds = Math.Max(1, durationSeconds);
        wavBytes = WrapWav(pcm);
        return true;
    }

    public void Cancel()
    {
        StopCapture();
        lock (gate)
        {
            buffer = null;
        }

        recording = false;
        level = 0f;
    }

    private void StopCapture()
    {
        var toStop = waveIn;
        waveIn = null;
        if (toStop is null)
        {
            return;
        }

        toStop.DataAvailable -= OnData;
        try
        {
            toStop.StopRecording();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Voice note recording failed to stop: {exception.Message}");
        }

        toStop.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        lock (gate)
        {
            if (buffer is null || buffer.Length >= MaxBytes)
            {
                return;
            }

            var count = (int)Math.Min(args.BytesRecorded, MaxBytes - buffer.Length);
            buffer.Write(args.Buffer, 0, count);
        }

        level = Rms(args.Buffer, args.BytesRecorded);
    }

    private static float Rms(byte[] bytes, int count)
    {
        var samples = count / 2;
        if (samples == 0)
        {
            return 0f;
        }

        double sum = 0;
        for (var index = 0; index < samples; index++)
        {
            double sample = BitConverter.ToInt16(bytes, index * 2);
            sum += sample * sample;
        }

        return (float)(Math.Sqrt(sum / samples) / 32768.0);
    }

    private static byte[] WrapWav(byte[] pcm)
    {
        var output = new byte[44 + pcm.Length];
        var span = output.AsSpan();
        "RIFF"u8.CopyTo(span);
        BitConverter.TryWriteBytes(span[4..], 36 + pcm.Length);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BitConverter.TryWriteBytes(span[16..], 16);
        BitConverter.TryWriteBytes(span[20..], (short)1);
        BitConverter.TryWriteBytes(span[22..], (short)1);
        BitConverter.TryWriteBytes(span[24..], SampleRate);
        BitConverter.TryWriteBytes(span[28..], BytesPerSecond);
        BitConverter.TryWriteBytes(span[32..], (short)2);
        BitConverter.TryWriteBytes(span[34..], (short)16);
        "data"u8.CopyTo(span[36..]);
        BitConverter.TryWriteBytes(span[40..], pcm.Length);
        pcm.CopyTo(span[44..]);
        return output;
    }

    public void Dispose()
    {
        Cancel();
    }
}
