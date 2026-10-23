using Aetherphone.Core.Onboarding;
using Aetherphone.Harness.Fakes;
using Aetherphone.Harness.Host;

namespace Aetherphone.Harness.Profiling;

internal static class FrameProfiler
{
    private const float Percentile = 0.95f;
    private static readonly TimeSpan Drain = TimeSpan.FromMilliseconds(500);

    public static PerfReport Measure(PhoneHost host, int warmup, int frames)
    {
        frames = Math.Max(1, frames);
        var recording = UiAnchors.ForceRecording;
        UiAnchors.ForceRecording = false;
        try
        {
            using var sampler = new AllocationSampler();
            host.Step(Math.Max(0, warmup));
            var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            var drawTimes = new double[frames];
            long drawTotal = 0;
            long drawMax = 0;
            long updateTotal = 0;
            long updateMax = 0;
            var drawAllocating = 0;
            var updateAllocating = 0;
            var logStart = HarnessLog.LatestSequence;
            sampler.Begin();
            for (var index = 0; index < frames; index++)
            {
                host.Step();
                var cost = host.LastFrame;
                drawTimes[index] = cost.DrawMilliseconds;
                drawTotal += cost.DrawBytes;
                drawMax = Math.Max(drawMax, cost.DrawBytes);
                drawAllocating += cost.DrawBytes > 0 ? 1 : 0;
                updateTotal += cost.UpdateBytes;
                updateMax = Math.Max(updateMax, cost.UpdateBytes);
                updateAllocating += cost.UpdateBytes > 0 ? 1 : 0;
            }

            var samples = sampler.End(Drain);
            var errors = Errors(HarnessLog.Since(logStart), out var firstError);
            var drawAverage = Average(drawTimes);
            Array.Sort(drawTimes);
            return new PerfReport(host.CurrentAppId ?? "home", frames, warmup,
                drawAverage, drawTimes[(int)((frames - 1) * Percentile)], drawTimes[frames - 1],
                drawTotal / frames, drawMax, drawAllocating, updateTotal / frames, updateMax, updateAllocating,
                GC.CollectionCount(0) - collections[0], GC.CollectionCount(1) - collections[1],
                GC.CollectionCount(2) - collections[2], errors, firstError, samples);
        }
        finally
        {
            UiAnchors.ForceRecording = recording;
        }
    }

    private static int Errors(List<HarnessLog.LogLine> lines, out string? first)
    {
        first = null;
        var count = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Level is not ("Error" or "Fatal" or "failure"))
            {
                continue;
            }

            count += 1;
            first ??= FirstLine(lines[index].Message);
        }

        return count;
    }

    private static string FirstLine(string message)
    {
        var end = message.IndexOf('\n');
        return end < 0 ? message : message[..end];
    }

    private static double Average(double[] values)
    {
        var total = 0d;
        for (var index = 0; index < values.Length; index++)
        {
            total += values[index];
        }

        return total / values.Length;
    }
}
