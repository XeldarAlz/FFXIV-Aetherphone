using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal sealed class MicLevelMonitor : IDisposable
{
    private const int BufferMilliseconds = 20;
    private const long IdleStopMilliseconds = 1500;
    private const long ClipHoldMilliseconds = 600;
    private readonly object worker = new();
    private WaveInEvent? waveIn;
    private string deviceName = string.Empty;
    private long lastTouchTicks;
    private long clipUntilTicks;
    private volatile bool active;
    private volatile float level;

    public bool Active => active;

    public float Level => active ? level : 0f;

    public bool Clipping => active && Environment.TickCount64 < Volatile.Read(ref clipUntilTicks);

    public void Touch() => Volatile.Write(ref lastTouchTicks, Environment.TickCount64);

    public void Start(string device)
    {
        Touch();
        if (active)
        {
            return;
        }

        deviceName = device;
        active = true;
        level = 0f;
        _ = Task.Run(Reconcile);
    }

    public void Restart(string device)
    {
        if (!active)
        {
            return;
        }

        deviceName = device;
        _ = Task.Run(Reconcile);
    }

    public void Stop()
    {
        if (!active)
        {
            return;
        }

        active = false;
        level = 0f;
        _ = Task.Run(Reconcile);
    }

    private void Reconcile()
    {
        lock (worker)
        {
            CloseCurrent();
            if (active)
            {
                OpenCurrent();
            }
        }
    }

    private void OpenCurrent()
    {
        try
        {
            var capture = new WaveInEvent
            {
                DeviceNumber = AudioDevices.ResolveInput(deviceName),
                WaveFormat = new WaveFormat(OpusAudio.SampleRate, 16, OpusAudio.Channels),
                BufferMilliseconds = BufferMilliseconds,
                NumberOfBuffers = 4,
            };
            capture.DataAvailable += OnData;
            capture.StartRecording();
            waveIn = capture;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Audio] Microphone test failed to start");
            active = false;
        }
    }

    private void CloseCurrent()
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
            AepLog.Warning(exception, "[Audio] Microphone test failed to stop");
        }

        toStop.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        var pcm = MemoryMarshal.Cast<byte, short>(args.Buffer.AsSpan(0, args.BytesRecorded & ~1));
        level = AutoGain.Rms(pcm);
        var now = Environment.TickCount64;
        if (AutoGain.Peak(pcm) >= MicThreshold.ClipLevel)
        {
            Volatile.Write(ref clipUntilTicks, now + ClipHoldMilliseconds);
        }

        if (now - Volatile.Read(ref lastTouchTicks) > IdleStopMilliseconds)
        {
            Stop();
        }
    }

    public void Dispose()
    {
        active = false;
        lock (worker)
        {
            CloseCurrent();
        }
    }
}
