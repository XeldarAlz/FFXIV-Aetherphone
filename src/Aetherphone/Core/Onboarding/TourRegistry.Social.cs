using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSocialTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "chirper", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.ChirperFeedsTitle, L.Onboarding.ChirperFeedsBody, "chirper.tabs"),
                GuideStep.Point(L.Onboarding.ChirperJoinTitle, L.Onboarding.ChirperJoinBody, "chirper.post.actions",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.ChirperNavTitle, L.Onboarding.ChirperNavBody, "chirper.tabbar",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.ChirperWriteTitle, L.Onboarding.ChirperWriteBody, "chirper.compose",
                    GuideGesture.Tap, "chirper.compose.toolbar"),
                GuideStep.Point(L.Onboarding.ChirperExtrasTitle, L.Onboarding.ChirperExtrasBody,
                    "chirper.compose.toolbar", GuideGesture.None),
                GuideStep.Point(L.Onboarding.ChirperSendTitle, L.Onboarding.ChirperSendBody, "chirper.compose.post",
                    GuideGesture.None),
            });
        Add(tours, "aethergram", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.AethergramFeedsTitle, L.Onboarding.AethergramFeedsBody,
                    "aethergram.feeds"),
                GuideStep.Point(L.Onboarding.AethergramStoriesTitle, L.Onboarding.AethergramStoriesBody,
                    "aethergram.stories", GuideGesture.None),
                GuideStep.Point(L.Onboarding.AethergramReactTitle, L.Onboarding.AethergramReactBody,
                    "aethergram.card.actions", GuideGesture.None),
                GuideStep.Span(L.Onboarding.AethergramNavTitle, L.Onboarding.AethergramNavBody, "aethergram.tabbar",
                    "aethergram.inbox"),
                GuideStep.TryUntil(L.Onboarding.AethergramPostTitle, L.Onboarding.AethergramPostBody,
                    "aethergram.compose", GuideGesture.Tap, "aethergram.compose.grid"),
                GuideStep.Point(L.Onboarding.AethergramPickTitle, L.Onboarding.AethergramPickBody,
                    "aethergram.compose.grid", GuideGesture.None),
            });
        Add(tours, "polls", 5,
            new[]
            {
                GuideStep.Point(L.Onboarding.PollsCastTitle, L.Onboarding.PollsCastBody, "polls.options",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.PollsTallyTitle, L.Onboarding.PollsTallyBody, "polls.footer",
                    GuideGesture.None),
            });
        Add(tours, "announcements", 2,
            new[]
            {
                GuideStep.TryUntil(L.Onboarding.AnnouncementsReadTitle, L.Onboarding.AnnouncementsReadBody,
                    "announcements.card", GuideGesture.Tap, "announcements.detail"),
                GuideStep.Point(L.Onboarding.AnnouncementsFullTitle, L.Onboarding.AnnouncementsFullBody,
                    "announcements.detail", GuideGesture.None),
            });
    }
}
