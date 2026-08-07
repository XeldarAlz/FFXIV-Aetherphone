using System.Runtime.InteropServices;
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

    public const int SystemDefaultOutput = -1;

    public static string[] OutputNames()
    {
        var count = OutputCount();
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        var names = new string[count];
        for (var index = 0; index < count; index++)
        {
            names[index] = OutputName(index);
        }

        return names;
    }

    public static int ResolveOutput(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return SystemDefaultOutput;
        }

        var count = OutputCount();
        for (var index = 0; index < count; index++)
        {
            if (string.Equals(OutputName(index), name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return SystemDefaultOutput;
    }

    private static int OutputCount()
    {
        try
        {
            return WaveOutGetNumDevs();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Audio output enumeration unavailable: {exception.Message}");
            return 0;
        }
    }

    private static string OutputName(int index)
    {
        try
        {
            return WaveOutGetDevCaps((IntPtr)index, out var caps, Marshal.SizeOf<WaveOutCaps>()) == 0
                ? caps.ProductName
                : string.Empty;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Audio output name unavailable for {index}: {exception.Message}");
            return string.Empty;
        }
    }

    [DllImport("winmm.dll", EntryPoint = "waveOutGetNumDevs")]
    private static extern int WaveOutGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "waveOutGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern int WaveOutGetDevCaps(IntPtr deviceId, out WaveOutCaps caps, int capsSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WaveOutCaps
    {
        public short Mid;
        public short Pid;
        public int DriverVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxProductNameLength)]
        public string ProductName;

        public int Formats;
        public short Channels;
        public short Reserved;
        public int Support;
    }

    private const int MaxProductNameLength = 32;
}
