using System.Diagnostics.Tracing;

namespace Aetherphone.Harness.Profiling;

internal sealed class AllocationSampler : EventListener
{
    private const string RuntimeSource = "Microsoft-Windows-DotNETRuntime";
    private const EventKeywords GcKeyword = (EventKeywords)0x1;
    private const int AllocationTickEvent = 10;
    private const string TypeNameField = "TypeName";
    private const string AmountField = "AllocationAmount64";
    private const string TypeIdField = "TypeID";
    private readonly Dictionary<string, long> bytesByType = new();
    private readonly Dictionary<nint, string> namesByHandle = new();
    private long uiThread = -1;
    private DateTime windowStart = DateTime.MaxValue;
    private DateTime windowEnd = DateTime.MaxValue;

    public void Begin()
    {
        ProfilerMarks.Log.Mark();
        lock (bytesByType)
        {
            bytesByType.Clear();
            windowStart = DateTime.UtcNow;
            windowEnd = DateTime.MaxValue;
        }
    }

    public List<KeyValuePair<string, long>> End(TimeSpan drain)
    {
        lock (bytesByType)
        {
            windowEnd = DateTime.UtcNow;
        }

        Thread.Sleep(drain);
        lock (bytesByType)
        {
            var samples = new List<KeyValuePair<string, long>>(bytesByType);
            samples.Sort(static (left, right) => right.Value.CompareTo(left.Value));
            return samples;
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == RuntimeSource)
        {
            EnableEvents(eventSource, EventLevel.Verbose, GcKeyword);
        }
        else if (eventSource.Name == ProfilerMarks.SourceName)
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventSource.Name == ProfilerMarks.SourceName)
        {
            uiThread = eventData.OSThreadId;
            return;
        }

        if (eventData.EventId != AllocationTickEvent || eventData.OSThreadId != uiThread ||
            eventData.PayloadNames is not { } names || eventData.Payload is not { } payload)
        {
            return;
        }

        var nameIndex = names.IndexOf(TypeNameField);
        var amountIndex = names.IndexOf(AmountField);
        if (nameIndex < 0 || amountIndex < 0 || payload[nameIndex] is not string typeName ||
            payload[amountIndex] is not ulong amount)
        {
            return;
        }

        var handleIndex = names.IndexOf(TypeIdField);
        var handle = handleIndex < 0 ? 0 : payload[handleIndex] switch
        {
            nint pointer => pointer,
            ulong value => (nint)value,
            _ => 0,
        };
        var stamp = eventData.TimeStamp.ToUniversalTime();
        lock (bytesByType)
        {
            if (stamp < windowStart || stamp > windowEnd)
            {
                return;
            }

            var fullName = FullName(handle, typeName);
            bytesByType[fullName] = bytesByType.GetValueOrDefault(fullName) + (long)amount;
        }
    }

    private string FullName(nint handle, string fallback)
    {
        if (handle == 0)
        {
            return fallback;
        }

        if (namesByHandle.TryGetValue(handle, out var known))
        {
            return known;
        }

        var name = Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr(handle))?.ToString() ?? fallback;
        namesByHandle[handle] = name;
        return name;
    }
}
