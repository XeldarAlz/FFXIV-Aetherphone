using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core.Audio;

internal static class AudioOutputFactory
{
    public static IWavePlayer Create(int desiredLatencyMs = 200)
    {
        return new RealtimeWavePlayer(CreateDefault(desiredLatencyMs), desiredLatencyMs);
    }

    private static IWavePlayer CreateDefault(int desiredLatencyMs)
    {
        try
        {
            return new WasapiOut(AudioClientShareMode.Shared, desiredLatencyMs);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception,
                "[Audio] WASAPI output unavailable; falling back to waveOut " +
                $"(devices visible to waveOut: {waveOutGetNumDevs()}).");
            return new WaveOutEvent { DesiredLatency = desiredLatencyMs };
        }
    }

    public static IWavePlayer Create(int desiredLatencyMs, MMDevice? device)
    {
        if (device is null)
        {
            return Create(desiredLatencyMs);
        }

        try
        {
            return new RealtimeWavePlayer(new WasapiOut(device, AudioClientShareMode.Shared, true, desiredLatencyMs),
                desiredLatencyMs);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Audio] Output device '{device.FriendlyName}' unavailable; using the default.");
            device.Dispose();
            return Create(desiredLatencyMs);
        }
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutGetNumDevs();
}
