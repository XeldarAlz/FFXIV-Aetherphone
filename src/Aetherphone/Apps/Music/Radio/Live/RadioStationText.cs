using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioStationText
{
    private const double RefreshSeconds = 15.0;

    private CommunityStationDto? station;
    private LanguageInfo? language;
    private double expiresAt;

    public RadioCountLabel Live { get; } = new();

    public RadioFittedText Name { get; } = new();

    public RadioFittedText Status { get; } = new();

    public RadioFittedText Subtitle { get; } = new();

    public string OffAir { get; private set; } = string.Empty;

    public string Schedule { get; private set; } = string.Empty;

    public string Header { get; private set; } = string.Empty;

    public bool NeedsRefresh(CommunityStationDto candidate, double now)
    {
        return !ReferenceEquals(station, candidate) || !ReferenceEquals(language, Loc.Current) || now >= expiresAt;
    }

    public void Refresh(CommunityStationDto candidate, double now, string offAir, string schedule, string header)
    {
        station = candidate;
        language = Loc.Current;
        expiresAt = now + RefreshSeconds;
        OffAir = offAir;
        Schedule = schedule;
        Header = header;
    }
}
