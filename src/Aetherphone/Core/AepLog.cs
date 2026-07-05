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

    private static void TrackErrorTelemetry(string message)
    {
        var analytics = Plugin.Analytics;
        if (analytics is null)
        {
            return;
        }

        ParseError(message, out var component, out var operation, out var detail);
        var key = $"{component}:{operation}";

        var now = DateTime.UtcNow;
        if (LastErrorSent.TryGetValue(key, out var lastSent) && (now - lastSent) < ErrorTelemetryCooldown)
        {
            return;
        }

        LastErrorSent[key] = now;
        analytics.Track(Analytics.AnalyticsEvents.ErrorOccurred(component, operation, detail));
    }

    private static void ParseError(string message, out string component, out string operation, out string detail)
    {
        var span = message.AsSpan();
        var bracketEnd = span.IndexOf(']');
        if (bracketEnd > 0 && span[0] == '[')
        {
            component = new string(span.Slice(1, bracketEnd - 1));
            var afterBracket = span.Slice(bracketEnd + 1).TrimStart();
            var firstSpace = afterBracket.IndexOf(' ');
            if (firstSpace > 0)
            {
                operation = new string(afterBracket.Slice(0, firstSpace));
                detail = new string(afterBracket.Slice(firstSpace + 1).TrimStart());
            }
            else
            {
                operation = new string(afterBracket);
                detail = string.Empty;
            }
        }
        else
        {
            var firstSpace = span.IndexOf(' ');
            if (firstSpace > 0)
            {
                component = new string(span.Slice(0, firstSpace));
                var rest = span.Slice(firstSpace + 1).TrimStart();
                operation = new string(rest);
                detail = string.Empty;
            }
            else
            {
                component = "general";
                operation = new string(span);
                detail = string.Empty;
            }
        }
    }
}
