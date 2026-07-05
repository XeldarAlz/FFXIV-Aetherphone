using System.Globalization;

namespace Aetherphone.Core.Analytics;

internal static class AnalyticsEvents
{
    public static AnalyticsEvent SessionStart(IReadOnlyDictionary<string, string> properties)
    {
        return new AnalyticsEvent(AnalyticsEventType.SessionStart, Properties: properties);
    }

    public static AnalyticsEvent SessionEnd(double durationSeconds)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["duration_sec"] = durationSeconds.ToString("F1", CultureInfo.InvariantCulture),
        };
        return new AnalyticsEvent(AnalyticsEventType.SessionEnd, Properties: properties);
    }

    public static AnalyticsEvent FirstRun()
    {
        return new AnalyticsEvent(AnalyticsEventType.FirstRun);
    }

    public static AnalyticsEvent ShellOpened(string trigger)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["trigger"] = trigger,
        };
        return new AnalyticsEvent(AnalyticsEventType.ShellOpened, Properties: properties);
    }

    public static AnalyticsEvent ShellClosed(double durationMs)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["duration_ms"] = durationMs.ToString("F0", CultureInfo.InvariantCulture),
        };
        return new AnalyticsEvent(AnalyticsEventType.ShellClosed, Properties: properties);
    }

    public static AnalyticsEvent AppOpen(string appId, string source)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["source"] = source,
        };
        return new AnalyticsEvent(AnalyticsEventType.AppOpen, appId, properties);
    }

    public static AnalyticsEvent ScreenView(string appId, string screen)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["screen"] = screen,
        };
        return new AnalyticsEvent(AnalyticsEventType.ScreenView, appId, properties);
    }

    public static AnalyticsEvent NotificationOpened(string appId, string groupKey)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["group_key"] = groupKey,
        };
        return new AnalyticsEvent(AnalyticsEventType.NotificationOpened, appId, properties);
    }

    public static AnalyticsEvent OnboardingStep(string tour, int step, int total, string action)
    {
        var properties = new Dictionary<string, string>(3)
        {
            ["tour"] = tour,
            ["step"] = step.ToString(CultureInfo.InvariantCulture),
            ["total"] = total.ToString(CultureInfo.InvariantCulture),
            ["action"] = action,
        };
        return new AnalyticsEvent(AnalyticsEventType.OnboardingStep, Properties: properties);
    }

    public static AnalyticsEvent SignupStep(string step)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["step"] = step,
        };
        return new AnalyticsEvent(AnalyticsEventType.SignupStep, Properties: properties);
    }

    public static AnalyticsEvent SignupStep(string step, string reason)
    {
        var properties = new Dictionary<string, string>(2)
        {
            ["step"] = step,
            ["reason"] = reason,
        };
        return new AnalyticsEvent(AnalyticsEventType.SignupStep, Properties: properties);
    }

    public static AnalyticsEvent PostCreated(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.PostCreated, appId);
    }

    public static AnalyticsEvent Reaction(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.Reaction, appId);
    }

    public static AnalyticsEvent Comment(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.Comment, appId);
    }

    public static AnalyticsEvent Follow(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.Follow, appId);
    }

    public static AnalyticsEvent DmSent(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.DmSent, appId);
    }

    public static AnalyticsEvent Call(string phase)
    {
        var properties = new Dictionary<string, string>(1)
        {
            ["phase"] = phase,
        };
        return new AnalyticsEvent(AnalyticsEventType.Call, "phone", properties);
    }

    public static AnalyticsEvent CallEnded(double durationMs, bool connected)
    {
        var properties = new Dictionary<string, string>(3)
        {
            ["phase"] = "ended",
            ["duration_ms"] = durationMs.ToString("F0", CultureInfo.InvariantCulture),
            ["connected"] = connected ? "1" : "0",
        };
        return new AnalyticsEvent(AnalyticsEventType.Call, "phone", properties);
    }

    public static AnalyticsEvent MusicListen(string station, double seconds)
    {
        var properties = new Dictionary<string, string>(2)
        {
            ["station"] = station,
            ["duration_sec"] = seconds.ToString("F0", CultureInfo.InvariantCulture),
        };
        return new AnalyticsEvent(AnalyticsEventType.MusicListen, "music", properties);
    }

    public static AnalyticsEvent MarketSearch()
    {
        return new AnalyticsEvent(AnalyticsEventType.MarketSearch, "market");
    }

    public static AnalyticsEvent AppClose(string appId, double durationMs)
    {
        var properties = new Dictionary<string, string>(2)
        {
            ["duration_ms"] = durationMs.ToString("F0", CultureInfo.InvariantCulture),
        };
        return new AnalyticsEvent(AnalyticsEventType.AppClose, appId, properties);
    }

    public static AnalyticsEvent GameStarted(string gameId)
    {
        return new AnalyticsEvent(AnalyticsEventType.GameStarted, gameId);
    }

    public static AnalyticsEvent GameEnded(string gameId, int score, double durationMs, bool completed)
    {
        var properties = new Dictionary<string, string>(3)
        {
            ["score"] = score.ToString(CultureInfo.InvariantCulture),
            ["duration_ms"] = durationMs.ToString("F0", CultureInfo.InvariantCulture),
            ["completed"] = completed ? "1" : "0",
        };
        return new AnalyticsEvent(AnalyticsEventType.GameEnded, gameId, properties);
    }

    public static AnalyticsEvent ErrorOccurred(string component, string operation, string message)
    {
        var properties = new Dictionary<string, string>(3)
        {
            ["component"] = component,
            ["operation"] = operation,
            ["message"] = message,
        };
        return new AnalyticsEvent(AnalyticsEventType.ErrorOccurred, Properties: properties);
    }

    public static AnalyticsEvent SettingChanged(string key, string value)
    {
        var properties = new Dictionary<string, string>(2)
        {
            ["key"] = key,
            ["value"] = value,
        };
        return new AnalyticsEvent(AnalyticsEventType.SettingChanged, Properties: properties);
    }
}
