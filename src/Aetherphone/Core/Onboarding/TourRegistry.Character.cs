using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddCharacterTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "character", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.ActivityRingsTitle, L.Onboarding.ActivityRingsBody, "character.rings",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.ActivityWeekTitle, L.Onboarding.ActivityWeekBody, "character.week",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.ActivityGoalsTitle, L.Onboarding.ActivityGoalsBody, "character.goals"),
            });
        Add(tours, "collections", 3,
            new[]
            {
                GuideStep.TryUntil(L.Onboarding.CollectionsOpenTitle, L.Onboarding.CollectionsOpenBody,
                    "collections.tile.mounts", GuideGesture.Tap, "collections.search"),
                GuideStep.TryTap(L.Onboarding.CollectionsShowMissingTitle, L.Onboarding.CollectionsShowMissingBody,
                    "collections.filter.missing"),
                GuideStep.TryUntil(L.Onboarding.CollectionsItemTitle, L.Onboarding.CollectionsItemBody,
                    "collections.row", GuideGesture.Tap, "collections.detail"),
            });
        Add(tours, "inventory", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.InventoryFindTitle, L.Onboarding.InventoryFindBody, "inventory.search",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.InventoryWealthTitle, L.Onboarding.InventoryWealthBody,
                    "inventory.wealth", GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.InventoryOpenTitle, L.Onboarding.InventoryOpenBody,
                    "inventory.storage", GuideGesture.Tap, "inventory.source"),
            });
        Add(tours, "jobs", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.JobsHeroTitle, L.Onboarding.JobsHeroBody, "jobs.hero",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.JobsGroupsTitle, L.Onboarding.JobsGroupsBody, "jobs.categories",
                    GuideGesture.Tap),
                GuideStep.TryUntil(L.Onboarding.JobsOpenTitle, L.Onboarding.JobsOpenBody, "jobs.tile",
                    GuideGesture.Tap, "jobs.detail"),
                GuideStep.Point(L.Onboarding.JobsEquipTitle, L.Onboarding.JobsEquipBody, "jobs.gearset",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.JobsSortTitle, L.Onboarding.JobsSortBody, "jobs.gearset.menu",
                    GuideGesture.Tap),
            });
        Add(tours, "dailies", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.DailiesProgressTitle, L.Onboarding.DailiesProgressBody, "dailies.hero",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.DailiesAutoTitle, L.Onboarding.DailiesAutoBody, "dailies.auto",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.DailiesTickTitle, L.Onboarding.DailiesTickBody, "dailies.manual"),
                GuideStep.TryTap(L.Onboarding.DailiesWeeklyTitle, L.Onboarding.DailiesWeeklyBody,
                    "dailies.tab.weekly"),
                GuideStep.Point(L.Onboarding.DailiesAddTitle, L.Onboarding.DailiesAddBody, "dailies.add",
                    GuideGesture.Tap),
            });
        Add(tours, "housing", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.HousingLotteryTitle, L.Onboarding.HousingLotteryBody, "housing.lottery",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.HousingDistrictsTitle, L.Onboarding.HousingDistrictsBody,
                    "housing.districts", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.HousingMapTitle, L.Onboarding.HousingMapBody, "housing.tab.map"),
                GuideStep.Point(L.Onboarding.HousingNarrowTitle, L.Onboarding.HousingNarrowBody, "housing.filters",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.HousingPlotTitle, L.Onboarding.HousingPlotBody, "housing.map",
                    GuideGesture.Tap, "housing.sheet"),
                GuideStep.Span(L.Onboarding.HousingTrackTitle, L.Onboarding.HousingTrackBody, "housing.sheet.watch",
                    "housing.sheet.remind"),
            });
        Add(tours, "wallet", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.WalletBalanceTitle, L.Onboarding.WalletCardBody, "wallet.gil",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.WalletCapTitle, L.Onboarding.WalletCapRingBody, "wallet.capped",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.WalletActivityTitle, L.Onboarding.WalletActivityBody, "wallet.recent",
                    GuideGesture.None),
            });
    }
}
