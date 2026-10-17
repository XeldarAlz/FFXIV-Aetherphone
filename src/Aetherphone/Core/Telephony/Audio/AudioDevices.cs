using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal static class AudioDevices
{
    private const long RefreshMilliseconds = 3000;
    private static string[] inputs = Array.Empty<string>();
    private static string[] outputs = Array.Empty<string>();
    private static long refreshedAt = long.MinValue / 2;
    private static int refreshing;

    public static string[] Inputs => Volatile.Read(ref inputs);

    public static string[] Outputs => Volatile.Read(ref outputs);

    public static void Refresh()
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref refreshedAt) < RefreshMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref refreshing, 1) == 1)
        {
            return;
        }

        Interlocked.Exchange(ref refreshedAt, now);
        _ = Task.Run(Enumerate);
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

    public static MMDevice? FindOutput(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            for (var index = 0; index < endpoints.Count; index++)
            {
                var endpoint = endpoints[index];
                if (string.Equals(endpoint.FriendlyName, name, StringComparison.Ordinal))
                {
                    return endpoint;
                }

                endpoint.Dispose();
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Audio output lookup unavailable");
        }

        return null;
    }

    private static void Enumerate()
    {
        try
        {
            Volatile.Write(ref inputs, InputNames());
            Volatile.Write(ref outputs, OutputNames());
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Audio device enumeration unavailable");
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private static string[] InputNames()
    {
        var count = WaveInEvent.DeviceCount;
        var names = new string[count];
        for (var index = 0; index < count; index++)
        {
            names[index] = WaveInEvent.GetCapabilities(index).ProductName;
        }

        return names;
    }

    private static string[] OutputNames()
    {
        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var names = new string[endpoints.Count];
        for (var index = 0; index < names.Length; index++)
        {
            using var endpoint = endpoints[index];
            names[index] = endpoint.FriendlyName;
        }

        return names;
    }
}
