using System.Runtime.InteropServices;
using Concentus;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal sealed class AudioCapture : IDisposable
{
    private const int Bitrate = 28000;
    private const float GateOpenRms = 0.018f;
    private const float GateCloseRms = 0.010f;
    private const int HangoverFrames = 12;

    private readonly object gate = new();
    private readonly IOpusEncoder encoder;
    private readonly byte[] accumulator = new byte[OpusAudio.FrameBytes * 4];
    private readonly byte[] packet = new byte[OpusAudio.MaxPacketBytes];
    private readonly List<byte[]> pending = new(4);

    private int accumulated;
    private int hangover;
    private bool gateOpen;

    private WaveInEvent? waveIn;
    private volatile bool muted;
    private volatile float level;

    public AudioCapture()
    {
        encoder = OpusAudio.CreateEncoder(Bitrate);
    }

    public Action<ReadOnlyMemory<byte>>? FrameEncoded { get; set; }

    public bool Muted
    {
        get => muted;
        set => muted = value;
    }

    public float Level => level;

    public void Start(int deviceIndex)
    {
        Stop();

        try
        {
            var capture = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = new WaveFormat(OpusAudio.SampleRate, 16, OpusAudio.Channels),
                BufferMilliseconds = 20,
                NumberOfBuffers = 4,
            };

            capture.DataAvailable += OnData;
            capture.StartRecording();
            waveIn = capture;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Voice capture failed to start: {exception.Message}");
        }
    }

    public void Stop()
    {
        WaveInEvent? toStop;
        lock (gate)
        {
            toStop = waveIn;
            waveIn = null;
            accumulated = 0;
            hangover = 0;
            gateOpen = false;
        }

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
            AepLog.Warning($"Voice capture failed to stop: {exception.Message}");
        }

        toStop.Dispose();
        level = 0f;
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        pending.Clear();

        lock (gate)
        {
            if (waveIn is null)
            {
                return;
            }

            Append(args.Buffer, args.BytesRecorded);
            while (accumulated >= OpusAudio.FrameBytes)
            {
                EncodeFrame();
                var remaining = accumulated - OpusAudio.FrameBytes;
                if (remaining > 0)
                {
                    Array.Copy(accumulator, OpusAudio.FrameBytes, accumulator, 0, remaining);
                }

                accumulated = remaining;
            }
        }

        var handler = FrameEncoded;
        if (handler is null)
        {
            return;
        }

        for (var index = 0; index < pending.Count; index++)
        {
            handler(pending[index]);
        }
    }

    private void Append(byte[] buffer, int count)
    {
        var capacity = accumulator.Length - accumulated;
        if (count > capacity)
        {
            count = capacity;
        }

        Array.Copy(buffer, 0, accumulator, accumulated, count);
        accumulated += count;
    }

    private void EncodeFrame()
    {
        var pcm = MemoryMarshal.Cast<byte, short>(accumulator.AsSpan(0, OpusAudio.FrameBytes));
        var rms = Rms(pcm);
        level = rms;

        UpdateGate(rms);
        if (muted || !gateOpen)
        {
            return;
        }

        var written = encoder.Encode(pcm, OpusAudio.FrameSamples, packet, packet.Length);
        if (written <= 0)
        {
            return;
        }

        var frame = new byte[written];
        Array.Copy(packet, frame, written);
        pending.Add(frame);
    }

    private void UpdateGate(float rms)
    {
        if (rms >= GateOpenRms)
        {
            gateOpen = true;
            hangover = HangoverFrames;
            return;
        }

        if (gateOpen && rms < GateCloseRms)
        {
            hangover--;
            if (hangover <= 0)
            {
                gateOpen = false;
            }
        }
    }

    private static float Rms(ReadOnlySpan<short> pcm)
    {
        if (pcm.Length == 0)
        {
            return 0f;
        }

        double sum = 0;
        for (var index = 0; index < pcm.Length; index++)
        {
            double sample = pcm[index];
            sum += sample * sample;
        }

        return (float)(Math.Sqrt(sum / pcm.Length) / 32768.0);
    }

    public void Dispose()
    {
        Stop();
        (encoder as IDisposable)?.Dispose();
    }
}
