using System.Globalization;
using System.Text;

namespace Aetherphone.Harness.Profiling;

internal sealed record PerfReport(string App, int Frames, int Warmup, double DrawAverage, double DrawPercentile,
    double DrawMax, long DrawBytesAverage, long DrawBytesMax, int DrawAllocatingFrames, long UpdateBytesAverage,
    long UpdateBytesMax, int UpdateAllocatingFrames, int Gen0, int Gen1, int Gen2, int Errors, string? FirstError,
    List<KeyValuePair<string, long>> Samples)
{
    private const int ShownSamples = 8;
    private const long Kilobyte = 1024;

    public string Format()
    {
        var builder = new StringBuilder();
        builder.Append("perf  app ").Append(App).Append("  ").Append(Frames).Append(" frames after ")
            .Append(Warmup).Append(" warmup\n");
        builder.Append("  draw time     avg ").Append(Milliseconds(DrawAverage)).Append("  p95 ")
            .Append(Milliseconds(DrawPercentile)).Append("  max ").Append(Milliseconds(DrawMax)).Append('\n');
        AppendAllocations(builder, "  draw alloc  ", DrawBytesAverage, DrawBytesMax, DrawAllocatingFrames);
        AppendAllocations(builder, "  update alloc", UpdateBytesAverage, UpdateBytesMax, UpdateAllocatingFrames);
        builder.Append("  gc            gen0 ").Append(Gen0).Append("  gen1 ").Append(Gen1).Append("  gen2 ")
            .Append(Gen2).Append('\n');
        if (Errors > 0)
        {
            builder.Append("  errors        ").Append(Errors).Append(" logged while measuring, first: ").Append(FirstError)
                .Append('\n');
        }

        if (Samples.Count == 0)
        {
            return builder.Append("  no UI-thread allocations sampled\n").ToString();
        }

        builder.Append("  sampled UI-thread allocations (one sample per ~100 KB)\n");
        for (var index = 0; index < Samples.Count && index < ShownSamples; index++)
        {
            builder.Append("    ").Append(Bytes(Samples[index].Value).PadLeft(8)).Append("  ")
                .Append(Samples[index].Key).Append('\n');
        }

        return builder.ToString();
    }

    private void AppendAllocations(StringBuilder builder, string label, long average, long max, int allocating) =>
        builder.Append(label).Append("  avg ").Append(Bytes(average)).Append("/frame  max ").Append(Bytes(max))
            .Append("  in ").Append(allocating).Append(" of ").Append(Frames).Append(" frames\n");

    private static string Milliseconds(double value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " ms";

    private static string Bytes(long value) => value < Kilobyte
        ? value.ToString(CultureInfo.InvariantCulture) + " B"
        : (value / (double)Kilobyte).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
}
