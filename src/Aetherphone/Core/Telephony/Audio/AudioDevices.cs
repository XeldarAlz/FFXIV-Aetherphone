using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal static class AudioDevices
{
    public static string[] InputNames()
    {
        var count = WaveInEvent.DeviceCount;
        var names = new string[count];
        for (var index = 0; index < count; index++)
        {
            names[index] = WaveInEvent.GetCapabilities(index).ProductName;
        }

        return names;
    }

    public static int ResolveInput(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }

        var count = WaveInEvent.DeviceCount;
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(WaveInEvent.GetCapabilities(index).ProductName, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    public static int ResolveOutput(string name)
    {
        _ = name;
        return -1;
    }
}
