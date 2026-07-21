using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core;

/// Creates audio output devices for all plugin playback (voice notes, calls, songs, radio, sound files).
/// Uses WASAPI shared mode instead of legacy winmm waveOut, because virtual audio drivers
/// (e.g. SteelSeries Sonar) may expose endpoints to WASAPI only, leaving waveOut with zero
/// devices and causing BadDeviceId on waveOutOpen. Falls back to waveOut if WASAPI fails.
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
