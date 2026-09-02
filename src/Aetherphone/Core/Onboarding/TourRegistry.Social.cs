using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSocialTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "chirper", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Chirper, L.Onboarding.ChirperBody),
                GuideStep.Point(L.Onboarding.ChirperTabsTitle, L.Onboarding.ChirperTabsBody, "chirper.tabs"),
                GuideStep.Point(L.Onboarding.ChirperPostTitle, L.Onboarding.ChirperPostBody, "chirper.compose"),
                GuideStep.Point(L.Onboarding.ChirperSearchTitle, L.Onboarding.ChirperSearchBody, "chirper.search"),
                GuideStep.Point(L.Onboarding.ChirperActivityTitle, L.Onboarding.ChirperActivityBody,
                    "chirper.activity"),
                GuideStep.Note(L.Onboarding.ChirperKindTitle, L.Onboarding.ChirperKindBody),
            });
        Add(tours, "aethergram", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Aethergram, L.Onboarding.AethergramBody),
                GuideStep.Point(L.Onboarding.AethergramShareTitle, L.Onboarding.AethergramShareBody,
                    "aethergram.compose"),
                GuideStep.Tap(L.Onboarding.AethergramSearchTitle, L.Onboarding.AethergramSearchBody,
                    "aethergram.tab.search", "aethergram.tab.search"),
                GuideStep.Point(L.Onboarding.AethergramActivityTitle, L.Onboarding.AethergramActivityBody,
                    "aethergram.activity"),
                GuideStep.Tap(L.Onboarding.AethergramProfileTitle, L.Onboarding.AethergramProfileBody,
                    "aethergram.tab.profile", "aethergram.tab.profile"),
                GuideStep.Note(L.Onboarding.AethergramSafeTitle, L.Onboarding.AethergramSafeBody),
                GuideStep.Note(L.Onboarding.AethergramKindTitle, L.Onboarding.AethergramKindBody),
            });
        Add(tours, "velvet", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.VelvetDiscoverTitle, L.Onboarding.VelvetDiscoverBody,
                    "velvet.discover.card"),
                GuideStep.Point(L.Onboarding.VelvetFilterTitle, L.Onboarding.VelvetFilterBody, "velvet.discover.filter"),
                GuideStep.Tap(L.Onboarding.VelvetFeedTitle, L.Onboarding.VelvetFeedBody, "velvet.tab.feed",
                    "velvet.tab.feed"),
                GuideStep.Point(L.Onboarding.VelvetComposeTitle, L.Onboarding.VelvetComposeBody, "velvet.compose"),
                GuideStep.Tap(L.Onboarding.VelvetMessagesTitle, L.Onboarding.VelvetMessagesBody, "velvet.tab.messages",
                    "velvet.tab.messages"),
                GuideStep.Tap(L.Onboarding.VelvetProfileTitle, L.Onboarding.VelvetProfileBody, "velvet.tab.me",
                    "velvet.tab.me"),
                GuideStep.Point(L.Onboarding.VelvetActivityTitle, L.Onboarding.VelvetActivityBody, "velvet.activity"),
                GuideStep.Note(L.Onboarding.VelvetKindTitle, L.Onboarding.VelvetKindBody),
            });
        Add(tours, "muster", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Muster, L.Onboarding.MusterBody),
                GuideStep.Point(L.Onboarding.MusterScopeTitle, L.Onboarding.MusterScopeBody, "muster.scope"),
                GuideStep.Point(L.Onboarding.MusterCategoriesTitle, L.Onboarding.MusterCategoriesBody,
                    "muster.categories"),
                GuideStep.Point(L.Onboarding.MusterStartTitle, L.Onboarding.MusterStartBody, "muster.start"),
                GuideStep.Note(L.Onboarding.MusterSafetyTitle, L.Onboarding.MusterSafetyBody),
            });
        Add(tours, "yellowpages", 1,
            new[]
            {
                GuideStep.Note(L.Apps.YellowPages, L.Onboarding.YellowPagesBody),
                GuideStep.Point(L.Onboarding.YellowPagesScopeTitle, L.Onboarding.YellowPagesScopeBody,
                    "yellowpages.scope"),
                GuideStep.Point(L.Onboarding.YellowPagesSearchTitle, L.Onboarding.YellowPagesSearchBody,
                    "yellowpages.search"),
                GuideStep.Point(L.Onboarding.YellowPagesPostTitle, L.Onboarding.YellowPagesPostBody,
                    "yellowpages.tab.post"),
                GuideStep.Point(L.Onboarding.YellowPagesInquiriesTitle, L.Onboarding.YellowPagesInquiriesBody,
                    "yellowpages.tab.inquiries"),
                GuideStep.Note(L.Onboarding.YellowPagesSafetyTitle, L.Onboarding.YellowPagesSafetyBody),
            });
        Add(tours, "kindkupo", 1,
            new[]
            {
                GuideStep.Note(L.Apps.KindKupo, L.Onboarding.KindKupoBody),
                GuideStep.Point(L.Onboarding.KindKupoWriteTitle, L.Onboarding.KindKupoWriteBody, "kindkupo.write"),
                GuideStep.Point(L.Onboarding.KindKupoRespondTitle, L.Onboarding.KindKupoRespondBody, "kindkupo.respond"),
                GuideStep.Point(L.Onboarding.KindKupoInboxTitle, L.Onboarding.KindKupoInboxBody, "kindkupo.inbox"),
                GuideStep.Point(L.Onboarding.KindKupoRulesTitle, L.Onboarding.KindKupoRulesBody, "kindkupo.rules"),
            });
    }
}
