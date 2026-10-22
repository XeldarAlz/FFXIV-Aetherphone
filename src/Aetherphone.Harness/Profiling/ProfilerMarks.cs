using System.Diagnostics.Tracing;

namespace Aetherphone.Harness.Profiling;

[EventSource(Name = SourceName)]
internal sealed class ProfilerMarks : EventSource
{
    public const string SourceName = "Aetherphone-Harness-Profiler";
    public static readonly ProfilerMarks Log = new();

    [Event(1)]
    public void Mark() => WriteEvent(1);
}
