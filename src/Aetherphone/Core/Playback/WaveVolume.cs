using NAudio.Wave;

namespace Aetherphone.Core.Playback;

internal static class WaveVolume
{
    private const float ChangeEpsilon = 0.001f;

    public static void Apply(IWavePlayer output, float target, ref float lastApplied)
    {
        if (Math.Abs(target - lastApplied) < ChangeEpsilon)
        {
            return;
        }

        lastApplied = target;
        try
        {
            output.Volume = target;
        }
        catch (Exception exception)
        {
            AepLog.Debug($"waveOut volume set ignored: {exception.Message}");
        }
    }
}
