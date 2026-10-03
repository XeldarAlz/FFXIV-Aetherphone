using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddCommunityTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "velvet", 5,
            new[]
            {
                GuideStep.Point(L.Onboarding.VelvetCardsTitle, L.Onboarding.VelvetCardsBody, "velvet.discover.card",
                    GuideGesture.Tap),
                GuideStep.TryUntil(L.Onboarding.VelvetFiltersTitle, L.Onboarding.VelvetFiltersBody,
                    "velvet.discover.filter", GuideGesture.Tap, "velvet.filters.facet"),
                GuideStep.TryUntil(L.Onboarding.VelvetFacetTitle, L.Onboarding.VelvetFacetBody,
                    "velvet.filters.facet", GuideGesture.Tap, "velvet.filters.option"),
                GuideStep.Point(L.Onboarding.VelvetOptionTitle, L.Onboarding.VelvetOptionBody,
                    "velvet.filters.option", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.VelvetResultsTitle, L.Onboarding.VelvetResultsBody,
                    "velvet.filters.done"),
                GuideStep.Point(L.Onboarding.VelvetSearchTitle, L.Onboarding.VelvetSearchBody,
                    "velvet.discover.search", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.VelvetFeedTitle, L.Onboarding.VelvetFeedBody, "velvet.tab.feed"),
                GuideStep.TryTap(L.Onboarding.VelvetMeTitle, L.Onboarding.VelvetMeBody, "velvet.tab.me"),
            });
        Add(tours, "muster", 2,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.MusterScopeTitle, L.Onboarding.MusterScopeBody, "muster.scope"),
                GuideStep.Point(L.Onboarding.MusterActivityTitle, L.Onboarding.MusterActivityBody,
                    "muster.categories", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.MusterCardTitle, L.Onboarding.MusterCardBody, "muster.card",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.MusterStartTitle, L.Onboarding.MusterStartBody, "muster.start",
                    GuideGesture.Tap),
            });
        Add(tours, "yellowpages", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.YellowPagesPostTitle, L.Onboarding.YellowPagesPostBody,
                    "yellowpages.tab.post", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.YellowPagesInboxTitle, L.Onboarding.YellowPagesInboxBody,
                    "yellowpages.tab.inquiries", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.YellowPagesScopeTitle, L.Onboarding.YellowPagesScopeBody,
                    "yellowpages.scope", GuideGesture.Tap),
                GuideStep.TryUntil(L.Onboarding.YellowPagesBrowseTitle, L.Onboarding.YellowPagesBrowseBody,
                    "yellowpages.intents", GuideGesture.Tap, "yellowpages.category.chips"),
                GuideStep.Point(L.Onboarding.YellowPagesChipsTitle, L.Onboarding.YellowPagesChipsBody,
                    "yellowpages.category.chips", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.YellowPagesAdTitle, L.Onboarding.YellowPagesAdBody, "yellowpages.card",
                    GuideGesture.Tap),
            });
    }
}
