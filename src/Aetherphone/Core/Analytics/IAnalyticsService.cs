namespace Aetherphone.Core.Analytics;

internal interface IAnalyticsService : IDisposable
{
    bool IsFirstRun { get; }

    void Track(AnalyticsEvent analyticsEvent);
}
