namespace Aetherphone.Harness.Fakes;

internal static class HarnessLog
{
    private static readonly HashSet<string> Seen = new();

    public static void Note(string message) => Console.Error.WriteLine($"[harness] {message}");

    public static void Plugin(string level, string message) => Console.WriteLine($"[{level}] {message}");

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
    }
}
