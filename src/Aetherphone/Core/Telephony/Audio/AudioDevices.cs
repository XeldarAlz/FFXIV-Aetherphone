using System.Diagnostics.CodeAnalysis;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal static class AudioDevices
{
    private const long RefreshMilliseconds = 3000;
    private const long StalledMilliseconds = 15000;
    private static readonly object reportGate = new();
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private static string[] inputs = Array.Empty<string>();
    private static string[] outputs = Array.Empty<string>();
    private static long refreshedAt = long.MinValue / 2;
    private static int refreshing;
    private static int lastOutputCount = -1;

    public static string[] Inputs => Volatile.Read(ref inputs);

    public static string[] Outputs => Volatile.Read(ref outputs);

    public static void Refresh()
    {
        var now = Environment.TickCount64;
        var startedAt = Interlocked.Read(ref refreshedAt);
        if (now - startedAt < RefreshMilliseconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref refreshing, 1) == 1)
        {
            if (now - startedAt >= StalledMilliseconds)
            {
                Report("stalled", null, $"[Audio] Device enumeration has not finished after {(now - startedAt) / 1000} s");
            }

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
            if (TryInputName(index, out var productName) && string.Equals(productName, name, StringComparison.Ordinal))
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
            var count = endpoints.Count;
            for (var index = 0; index < count; index++)
            {
                if (!TryOutput(endpoints, index, out var endpoint, out var friendlyName))
                {
                    continue;
                }

                if (string.Equals(friendlyName, name, StringComparison.Ordinal))
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
            PublishInputs();
            PublishOutputs();
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private static void PublishInputs()
    {
        try
        {
            Volatile.Write(ref inputs, InputNames());
        }
        catch (Exception exception)
        {
            Report("inputs:" + exception.HResult, exception, "[Audio] Microphone list unavailable");
        }
    }

    private static void PublishOutputs()
    {
        try
        {
            var names = OutputNames();
            Volatile.Write(ref outputs, names);
            if (Interlocked.Exchange(ref lastOutputCount, names.Length) != names.Length)
            {
                AepLog.Info($"[Audio] Found {names.Length} active speaker devices");
            }
        }
        catch (Exception exception)
        {
            Report("outputs:" + exception.HResult, exception, "[Audio] Speaker list unavailable");
        }
    }

    private static string[] InputNames()
    {
        var count = WaveInEvent.DeviceCount;
        var names = new string[count];
        var named = 0;
        for (var index = 0; index < count; index++)
        {
            if (TryInputName(index, out var name))
            {
                names[named++] = name;
            }
        }

        return named == count ? names : names[..named];
    }

    private static string[] OutputNames()
    {
        using var enumerator = new MMDeviceEnumerator();
        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var count = endpoints.Count;
        var names = new string[count];
        var named = 0;
        for (var index = 0; index < count; index++)
        {
            if (!TryOutput(endpoints, index, out var endpoint, out var name))
            {
                continue;
            }

            endpoint.Dispose();
            names[named++] = name;
        }

        return named == count ? names : names[..named];
    }

    private static bool TryInputName(int index, out string name)
    {
        try
        {
            name = WaveInEvent.GetCapabilities(index).ProductName;
            return true;
        }
        catch (Exception exception)
        {
            Report($"input:{index}:{exception.HResult}", exception,
                $"[Audio] Skipped microphone {index}: its name could not be read");
            name = string.Empty;
            return false;
        }
    }

    private static bool TryOutput(MMDeviceCollection endpoints, int index, [NotNullWhen(true)] out MMDevice? endpoint,
        out string name)
    {
        MMDevice? opened = null;
        try
        {
            opened = endpoints[index];
            name = opened.FriendlyName;
            endpoint = opened;
            return true;
        }
        catch (Exception exception)
        {
            Report($"output:{index}:{exception.HResult}", exception,
                $"[Audio] Skipped speaker {index} ({IdOf(opened)}): its name could not be read");
            opened?.Dispose();
            name = string.Empty;
            endpoint = null;
            return false;
        }
    }

    private static string IdOf(MMDevice? endpoint)
    {
        if (endpoint is null)
        {
            return "not opened";
        }

        try
        {
            return endpoint.ID;
        }
        catch (Exception exception)
        {
            return $"no id, 0x{exception.HResult:X8}";
        }
    }

    private static void Report(string key, Exception? exception, string message)
    {
        lock (reportGate)
        {
            if (!reported.Add(key))
            {
                return;
            }
        }

        if (exception is null)
        {
            AepLog.Warning(message);
            return;
        }

        AepLog.Warning(exception, message);
    }
}
