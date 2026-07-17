using System.Collections.Concurrent;

namespace Aetherphone.Core;

internal static class AepLog
{
    private static readonly TimeSpan ErrorTelemetryCooldown = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, DateTime> LastErrorSent = new();

    public static void Debug(string message) => Plugin.Log.Debug(message);

    public static void Info(string message) => Plugin.Log.Information(message);

    public static void Warning(string message)
    {
        Plugin.Log.Warning(message);
        TrackErrorTelemetry(message);
    }

    public static void Error(string message)
    {
        Plugin.Log.Error(message);
        TrackErrorTelemetry(message);
    }

    private const int MaxCodeLength = 32;

    private static void TrackErrorTelemetry(string message)
    {
        var analytics = Plugin.Analytics;
        if (analytics is null)
        {
            return;
        }

        ParseError(message, out var component, out var operation);
        var key = $"{component}:{operation}";

        var now = DateTime.UtcNow;
        if (LastErrorSent.TryGetValue(key, out var lastSent) && (now - lastSent) < ErrorTelemetryCooldown)
        {
            return;
        }

        LastErrorSent[key] = now;
        analytics.Track(Analytics.AnalyticsEvents.ErrorOccurred(component, operation));
    }

    private static void ParseError(string message, out string component, out string operation)
    {
        var span = message.AsSpan();
        var bracketEnd = span.IndexOf(']');
        if (bracketEnd > 0 && span[0] == '[')
        {
            component = CodeToken(span.Slice(1, bracketEnd - 1));
            operation = CodeToken(span.Slice(bracketEnd + 1));
            return;
        }

        var firstSpace = span.IndexOf(' ');
        if (firstSpace > 0)
        {
            component = CodeToken(span.Slice(0, firstSpace));
            operation = CodeToken(span.Slice(firstSpace + 1));
            return;
        }

        component = "general";
        operation = CodeToken(span);
    }

    private static string CodeToken(ReadOnlySpan<char> text)
    {
        var trimmed = text.TrimStart();
        var end = 0;
        while (end < trimmed.Length && end < MaxCodeLength && IsCodeChar(trimmed[end]))
        {
            end++;
        }

        return end == 0 ? "other" : new string(trimmed.Slice(0, end));
    }

    private static bool IsCodeChar(char value) =>
        char.IsAsciiLetterOrDigit(value) || value == '_' || value == '-' || value == '.';
}
