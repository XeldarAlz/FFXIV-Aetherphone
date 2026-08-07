using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core;

internal static class AudioOutputFactory
{
    public static IWavePlayer Create(int desiredLatencyMs = 200)
    {
        try
        {
            return new WasapiOut(AudioClientShareMode.Shared, desiredLatencyMs);
        }
        catch (Exception exception)
        {
            AepLog.Warning(
                $"[Audio] WASAPI output unavailable ({exception.Message}); falling back to waveOut " +
                $"(devices visible to waveOut: {waveOutGetNumDevs()}).");
            return new WaveOutEvent { DesiredLatency = desiredLatencyMs };
        }
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutGetNumDevs();
}
