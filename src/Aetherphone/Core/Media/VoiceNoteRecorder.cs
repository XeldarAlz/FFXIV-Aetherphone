using System.Runtime.InteropServices;
using Aetherphone.Core.Telephony.Audio;
using NAudio.Wave;

namespace Aetherphone.Core.Media;

internal sealed class VoiceNoteRecorder : IDisposable
{
    public const int MaxSeconds = 150;
    private const int SampleRate = 16000;
    private const int BytesPerSecond = SampleRate * 2;
    private const int MaxBytes = BytesPerSecond * MaxSeconds;
    private const int BufferMilliseconds = 50;
    private const int BlockSamples = SampleRate * BufferMilliseconds / 1000;
    private const float NoGate = 0f;

    private static float carriedGain = 1f;
    private readonly object gate = new();
    private readonly AutoGain autoGain = new(NoGate);
    private bool autoGainEnabled;
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

    public bool Start(VoiceInput input)
    {
        Cancel();
        try
        {
            var capture = new WaveInEvent
            {
                DeviceNumber = input.Device,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
                BufferMilliseconds = BufferMilliseconds,
                NumberOfBuffers = 4,
            };
            capture.DataAvailable += OnData;
            lock (gate)
            {
                buffer = new MemoryStream();
                autoGainEnabled = input.AutoGain;
                autoGain.Reset(autoGainEnabled ? Volatile.Read(ref carriedGain) : 1f);
            }

            capture.StartRecording();
            waveIn = capture;
            startedUtc = DateTime.UtcNow;
            recording = true;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Voice note recording failed to start");
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
            if (autoGainEnabled)
            {
                autoGain.Level(MemoryMarshal.Cast<byte, short>(pcm.AsSpan(0, pcm.Length & ~1)), SampleRate, BlockSamples);
            }

            CarryGain();
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
            CarryGain();
        }

        recording = false;
        level = 0f;
    }

    private void CarryGain()
    {
        if (autoGainEnabled)
        {
            Volatile.Write(ref carriedGain, autoGain.Gain);
        }
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
            AepLog.Warning(exception, "Voice note recording failed to stop");
        }

        toStop.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        float rms;
        lock (gate)
        {
            if (buffer is null || buffer.Length >= MaxBytes)
            {
                return;
            }

            var pcm = MemoryMarshal.Cast<byte, short>(args.Buffer.AsSpan(0, args.BytesRecorded & ~1));
            rms = AutoGain.Rms(pcm);
            if (autoGainEnabled)
            {
                autoGain.Observe(rms, pcm.Length, SampleRate);
                rms *= autoGain.Gain;
            }

            var count = (int)Math.Min(args.BytesRecorded, MaxBytes - buffer.Length);
            buffer.Write(args.Buffer, 0, count);
        }

        level = rms;
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
