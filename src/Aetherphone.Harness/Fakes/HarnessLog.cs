namespace Aetherphone.Harness.Fakes;

internal static class HarnessLog
{
    private const int RingCapacity = 2000;
    private static readonly HashSet<string> Seen = new();
    private static readonly List<LogLine> Ring = new();
    private static long sequence;

    public static void Note(string message)
    {
        Console.Error.WriteLine($"[harness] {message}");
        Record("harness", message);
    }

    public static void Plugin(string level, string message)
    {
        Console.WriteLine($"[{level}] {message}");
        if (level is not ("Debug" or "Verbose"))
        {
            Record(level, message);
        }
    }

    public static void Failure(string context, Exception exception)
    {
        var key = context + "|" + exception.GetType().Name + "|" + exception.Message;
        lock (Seen)
        {
            if (!Seen.Add(key))
            {
                return;
            }
        }

        Console.Error.WriteLine($"[harness] {context} failed: {exception.GetType().Name}: {exception.Message}");
        Console.Error.WriteLine(exception.StackTrace);
        Record("failure", $"{context}: {exception.GetType().Name}: {exception.Message}");
    }

    public static long LatestSequence
    {
        get
        {
            lock (Ring)
            {
                return sequence;
            }
        }
    }

    public static List<LogLine> Since(long after)
    {
        var lines = new List<LogLine>();
        lock (Ring)
        {
            for (var index = 0; index < Ring.Count; index++)
            {
                if (Ring[index].Sequence > after)
                {
                    lines.Add(Ring[index]);
                }
            }
        }

        return lines;
    }

    private static void Record(string level, string message)
    {
        lock (Ring)
        {
            sequence += 1;
            Ring.Add(new LogLine(sequence, level, message));
            if (Ring.Count > RingCapacity)
            {
                Ring.RemoveRange(0, Ring.Count - RingCapacity);
            }
        }
    }

    public readonly record struct LogLine(long Sequence, string Level, string Message);
}
