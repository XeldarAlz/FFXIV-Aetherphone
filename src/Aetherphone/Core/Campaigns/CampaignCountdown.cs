using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Campaigns;

internal enum CampaignPhase : byte
{
    Before,
    Live,
    Between,
    Ended,
}

internal enum ExperienceState : byte
{
    Upcoming,
    Live,
    Ended,
}

internal readonly record struct CampaignMoment(CampaignPhase Phase, int CurrentIndex, int NextIndex,
    int NextStreamedIndex, long SecondsToStart)
{
    public bool WindowOpen => Phase is CampaignPhase.Live or CampaignPhase.Between;
}

internal static class CampaignCountdown
{
    private const long SecondsPerMinute = 60L;
    private const long SecondsPerHour = 3600L;
    private const long SecondsPerDay = 86400L;

    public static CampaignMoment Evaluate(CampaignDto campaign, long nowUnix)
    {
        var experiences = campaign.Experiences;
        var current = -1;
        var next = -1;
        var nextStreamed = -1;
        for (var index = 0; index < experiences.Length; index++)
        {
            var experience = experiences[index];
            var state = StateOf(experience, nowUnix);
            if (state == ExperienceState.Live && current < 0)
            {
                current = index;
            }

            if (state == ExperienceState.Upcoming
                && (next < 0 || experience.StartsAtUnix < experiences[next].StartsAtUnix))
            {
                next = index;
            }

            if (state != ExperienceState.Ended && !string.IsNullOrEmpty(experience.TwitchUrl)
                && (nextStreamed < 0 || experience.StartsAtUnix < experiences[nextStreamed].StartsAtUnix))
            {
                nextStreamed = index;
            }
        }

        if (nowUnix >= campaign.EndsAtUnix)
        {
            return new CampaignMoment(CampaignPhase.Ended, current, next, nextStreamed, 0L);
        }

        if (nowUnix < campaign.StartsAtUnix)
        {
            return new CampaignMoment(CampaignPhase.Before, current, next, nextStreamed,
                campaign.StartsAtUnix - nowUnix);
        }

        return new CampaignMoment(current >= 0 ? CampaignPhase.Live : CampaignPhase.Between, current, next,
            nextStreamed, 0L);
    }

    public static ExperienceState StateOf(CampaignExperienceDto experience, long nowUnix)
    {
        if (nowUnix >= experience.EndsAtUnix)
        {
            return ExperienceState.Ended;
        }

        return nowUnix >= experience.StartsAtUnix ? ExperienceState.Live : ExperienceState.Upcoming;
    }

    public static string Text(long remainingSeconds)
    {
        var remaining = Math.Max(0L, remainingSeconds);
        if (remaining >= SecondsPerDay)
        {
            return Loc.T(L.LoveIsland.CountdownDays, remaining / SecondsPerDay,
                remaining % SecondsPerDay / SecondsPerHour, remaining % SecondsPerHour / SecondsPerMinute);
        }

        if (remaining >= SecondsPerHour)
        {
            return Loc.T(L.LoveIsland.CountdownHours, remaining / SecondsPerHour,
                remaining % SecondsPerHour / SecondsPerMinute, remaining % SecondsPerMinute);
        }

        return Loc.T(L.LoveIsland.CountdownMinutes, remaining / SecondsPerMinute, remaining % SecondsPerMinute);
    }
}
