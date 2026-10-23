using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal readonly record struct OutputTarget(MMDevice? Endpoint, int WaveOutDevice)
{
    public static readonly OutputTarget SystemDefault = new(null, -1);
}

internal static class AudioDevices
{
    private const long RefreshMilliseconds = 3000;
    private const long StalledMilliseconds = 15000;
    private const string PendingStatus = "not answered yet";
    private static readonly object reportGate = new();
    private static readonly object publishGate = new();
    private static readonly HashSet<string> reported = new(StringComparer.Ordinal);
    private static readonly Action WinMmPass = RefreshWinMm;
    private static readonly Action CoreAudioPass = RefreshCoreAudio;
    private static string[] inputs = Array.Empty<string>();
    private static string[] outputs = Array.Empty<string>();
    private static SpeakerEntry[] speakers = Array.Empty<SpeakerEntry>();
    private static SpeakerEntry[] coreAudioSpeakers = Array.Empty<SpeakerEntry>();
    private static string[] waveOutNames = Array.Empty<string>();
    private static EndpointCounts coreAudioCounts;
    private static int coreAudioUnreadable;
    private static string coreAudioStatus = PendingStatus;
    private static string lastSummary = string.Empty;
    private static long winMmStartedAt = long.MinValue / 2;
    private static long coreAudioStartedAt = long.MinValue / 2;
    private static int winMmBusy;
    private static int coreAudioBusy;

    public static string[] Inputs => Volatile.Read(ref inputs);

    public static string[] Outputs => Volatile.Read(ref outputs);

    public static void Refresh()
    {
        var now = Environment.TickCount64;
        Start(ref winMmBusy, ref winMmStartedAt, now, WinMmPass, "waveIn/waveOut");
        if (Start(ref coreAudioBusy, ref coreAudioStartedAt, now, CoreAudioPass, "Core Audio speaker"))
        {
            lock (publishGate)
            {
                coreAudioStatus = $"no answer after {StalledMilliseconds / 1000} s";
            }

            Publish();
        }
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

    public static OutputTarget ResolveOutput(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return OutputTarget.SystemDefault;
        }

        var listed = Volatile.Read(ref speakers);
        var position = SpeakerList.IndexOfName(listed, name);
        if (position >= 0)
        {
            var entry = listed[position];
            if (entry.IsWaveOutOnly)
            {
                var waveOut = WaveOutIndexOf(name);
                return waveOut >= 0 ? new OutputTarget(null, waveOut) : OutputTarget.SystemDefault;
            }

            if (TryEndpointById(entry.EndpointId, out var byId))
            {
                return new OutputTarget(byId, -1);
            }
        }

        var scanned = FindEndpointByName(name);
        if (scanned is not null)
        {
            return new OutputTarget(scanned, -1);
        }

        var fallback = WaveOutIndexOf(name);
        return fallback >= 0 ? new OutputTarget(null, fallback) : OutputTarget.SystemDefault;
    }

    private static bool Start(ref int busy, ref long startedAt, long now, Action pass, string label)
    {
        var started = Interlocked.Read(ref startedAt);
        if (now - started < RefreshMilliseconds)
        {
            return false;
        }

        if (Interlocked.Exchange(ref busy, 1) == 1)
        {
            return now - started >= StalledMilliseconds
                && Report("stalled:" + label, null,
                    $"[Audio] {label} enumeration has not finished after {(now - started) / 1000} s");
        }

        Interlocked.Exchange(ref startedAt, now);
        _ = Task.Run(pass);
        return false;
    }

    private static void RefreshWinMm()
    {
        try
        {
            PublishInputs();
            PublishWaveOutNames();
            Publish();
        }
        finally
        {
            Interlocked.Exchange(ref winMmBusy, 0);
        }
    }

    private static void RefreshCoreAudio()
    {
        try
        {
            ReadCoreAudioSpeakers();
            Publish();
        }
        finally
        {
            Interlocked.Exchange(ref coreAudioBusy, 0);
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

    private static void PublishWaveOutNames()
    {
        try
        {
            var names = WaveOutNames();
            lock (publishGate)
            {
                waveOutNames = names;
            }
        }
        catch (Exception exception)
        {
            Report("waveout:" + exception.HResult, exception, "[Audio] waveOut speaker list unavailable");
        }
    }

    private static void ReadCoreAudioSpeakers()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var count = endpoints.Count;
            var found = new SpeakerEntry[count];
            var named = 0;
            for (var index = 0; index < count; index++)
            {
                if (!TryOutput(endpoints, index, out var endpoint, out var name))
                {
                    continue;
                }

                var id = TryId(endpoint);
                endpoint.Dispose();
                found[named++] = new SpeakerEntry(name, id, -1);
            }

            var counts = new EndpointCounts(count, CountOf(enumerator, DeviceState.Disabled),
                CountOf(enumerator, DeviceState.Unplugged), CountOf(enumerator, DeviceState.NotPresent));
            lock (publishGate)
            {
                coreAudioSpeakers = named == count ? found : found[..named];
                coreAudioCounts = counts;
                coreAudioUnreadable = count - named;
                coreAudioStatus = string.Empty;
            }
        }
        catch (Exception exception)
        {
            Report("outputs:" + exception.HResult, exception, "[Audio] Core Audio speaker list unavailable");
            lock (publishGate)
            {
                coreAudioSpeakers = Array.Empty<SpeakerEntry>();
                coreAudioStatus = $"failed (0x{exception.HResult:X8} {exception.GetType().Name})";
            }
        }
    }

    private static void Publish()
    {
        string summary;
        lock (publishGate)
        {
            var includeWaveOut = SpeakerList.NeedsWaveOut(coreAudioStatus.Length == 0, coreAudioSpeakers.Length,
                coreAudioUnreadable);
            var merged = SpeakerList.Merge(coreAudioSpeakers, waveOutNames, includeWaveOut, out var drops);
            var names = new string[merged.Length];
            for (var index = 0; index < merged.Length; index++)
            {
                names[index] = merged[index].Name;
            }

            Volatile.Write(ref speakers, merged);
            Volatile.Write(ref outputs, names);
            summary = SpeakerList.Summary(merged.Length, drops, coreAudioStatus, coreAudioCounts, coreAudioUnreadable,
                waveOutNames.Length);
            if (string.Equals(summary, lastSummary, StringComparison.Ordinal))
            {
                return;
            }

            lastSummary = summary;
        }

        AepLog.Info(summary);
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

    private static string[] WaveOutNames()
    {
        var count = WaveInterop.waveOutGetNumDevs();
        var names = new string[Math.Max(0, count)];
        for (var index = 0; index < names.Length; index++)
        {
            names[index] = TryWaveOutName(index, out var name) ? name : string.Empty;
        }

        return names;
    }

    private static int WaveOutIndexOf(string name)
    {
        try
        {
            var count = WaveInterop.waveOutGetNumDevs();
            for (var index = 0; index < count; index++)
            {
                if (TryWaveOutName(index, out var productName)
                    && string.Equals(productName, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Audio] waveOut output lookup unavailable");
        }

        return -1;
    }

    private static bool TryEndpointById(string id, [NotNullWhen(true)] out MMDevice? endpoint)
    {
        endpoint = null;
        if (id.Length == 0)
        {
            return false;
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(id);
            if (device.State != DeviceState.Active)
            {
                device.Dispose();
                return false;
            }

            endpoint = device;
            return true;
        }
        catch (Exception exception)
        {
            Report($"output-id:{exception.HResult}", exception, $"[Audio] Speaker {id} could not be opened by id");
            return false;
        }
    }

    private static MMDevice? FindEndpointByName(string name)
    {
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

    private static int CountOf(MMDeviceEnumerator enumerator, DeviceState state)
    {
        try
        {
            return enumerator.EnumerateAudioEndPoints(DataFlow.Render, state).Count;
        }
        catch (Exception exception)
        {
            Report($"count:{state}:{exception.HResult}", exception, $"[Audio] Could not count {state} speakers");
            return -1;
        }
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

    private static bool TryWaveOutName(int index, out string name)
    {
        var result = WaveInterop.waveOutGetDevCaps((IntPtr)index, out var capabilities,
            Marshal.SizeOf<WaveOutCapabilities>());
        if (result != MmResult.NoError)
        {
            Report($"waveout:{index}:{result}", null, $"[Audio] Skipped waveOut speaker {index}: {result}");
            name = string.Empty;
            return false;
        }

        name = capabilities.ProductName;
        return true;
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

    private static string TryId(MMDevice endpoint)
    {
        try
        {
            return endpoint.ID;
        }
        catch (Exception exception)
        {
            Report($"output-noid:{exception.HResult}", exception, "[Audio] A speaker's id could not be read");
            return string.Empty;
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

    private static bool Report(string key, Exception? exception, string message)
    {
        lock (reportGate)
        {
            if (!reported.Add(key))
            {
                return false;
            }
        }

        if (exception is null)
        {
            AepLog.Warning(message);
            return true;
        }

        AepLog.Warning(exception, message);
        return true;
    }
}
