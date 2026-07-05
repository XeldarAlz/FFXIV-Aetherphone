namespace Aetherphone.Core.Analytics;

internal sealed record AnalyticsEvent(
    string Type,
    string? AppId = null,
    IReadOnlyDictionary<string, string>? Properties = null);
