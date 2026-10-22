using System.Runtime.InteropServices;
using Concentus;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal sealed class AudioCapture : IDisposable
{
    private const int Bitrate = 28000;
    public const float MaximumGain = 2f;
    private const int HangoverFrames = 12;
    private const long ClipHoldMilliseconds = 600;
    private readonly object gate = new();
    private readonly object deviceGate = new();
    private readonly IOpusEncoder encoder;
    private readonly byte[] accumulator = new byte[OpusAudio.FrameBytes * 4];
    private readonly byte[] packet = new byte[OpusAudio.MaxPacketBytes];
    private readonly List<byte[]> pending = new(4);
    private readonly AutoGain autoGain = new();
    private int accumulated;
    private int hangover;
    private bool gateOpen;
    private WaveInEvent? waveIn;
    private volatile bool muted;
    private volatile float gain = 1f;
    private volatile bool autoGainEnabled = true;
    private volatile float threshold = MicThreshold.DefaultPosition;
    private bool closed;
    private volatile float level;
    private long clipUntilTicks;

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

    public bool Clipping => Environment.TickCount64 < Volatile.Read(ref clipUntilTicks);

    public float Threshold
    {
        get => threshold;
        set => threshold = Math.Clamp(value, 0f, 1f);
    }

    public float Gain
    {
        get => gain;
        set => gain = Math.Clamp(value, 0f, MaximumGain);
    }

    public bool AutoGainEnabled
    {
        get => autoGainEnabled;
        set => autoGainEnabled = value;
    }

    public void Start(int deviceIndex)
    {
        lock (deviceGate)
        {
            if (closed)
            {
                return;
            }

            Open(deviceIndex);
        }
    }

    private void Open(int deviceIndex)
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
            AepLog.Warning(exception, "Voice capture failed to start");
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
            autoGain.Reset();
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
            AepLog.Warning(exception, "Voice capture failed to stop");
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
        var rawRms = AutoGain.Rms(pcm);
        if (AutoGain.Peak(pcm) >= MicThreshold.ClipLevel)
        {
            Volatile.Write(ref clipUntilTicks, Environment.TickCount64 + ClipHoldMilliseconds);
        }

        var openRms = MicThreshold.ToRms(threshold);
        level = rawRms;
        autoGain.Process(pcm, rawRms, OpusAudio.SampleRate, openRms, gain, autoGainEnabled);
        gateOpen = NextGateState(gateOpen, ref hangover, rawRms, openRms);
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

    public static bool NextGateState(bool open, ref int hangover, float rms, float openRms)
    {
        if (openRms <= 0f || rms >= openRms)
        {
            hangover = HangoverFrames;
            return true;
        }

        if (!open || rms >= openRms * MicThreshold.CloseRatio)
        {
            return open;
        }

        hangover--;
        return hangover > 0;
    }

    public void Dispose()
    {
        lock (deviceGate)
        {
            closed = true;
        }

        Stop();
        (encoder as IDisposable)?.Dispose();
    }
}
