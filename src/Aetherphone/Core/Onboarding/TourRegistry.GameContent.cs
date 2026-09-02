using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddGameContentTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "skywatcher", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.SkywatcherTitle, L.Onboarding.SkywatcherBody),
                GuideStep.Point(L.Onboarding.SkywatcherCurrentTitle, L.Onboarding.SkywatcherCurrentBody,
                    "skywatcher.current"),
                GuideStep.Point(L.Onboarding.SkywatcherForecastTitle, L.Onboarding.SkywatcherForecastBody,
                    "skywatcher.forecast"),
            });
        Add(tours, "market", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.MarketTitle, L.Onboarding.MarketBody),
                GuideStep.Point(L.Onboarding.MarketSearchTitle, L.Onboarding.MarketSearchBody, "market.search"),
                GuideStep.Point(L.Onboarding.MarketScopeTitle, L.Onboarding.MarketScopeBody, "market.scope"),
                GuideStep.Note(L.Onboarding.MarketStatsTitle, L.Onboarding.MarketStatsBody),
            });
        Add(tours, "strats", 1,
            new[]
            {
                GuideStep.Note(L.Onboarding.StratsTitle, L.Onboarding.StratsBody),
                GuideStep.Point(L.Onboarding.StratsFightsTitle, L.Onboarding.StratsFightsBody, "strats.fights"),
                GuideStep.Point(L.Onboarding.StratsRoleTitle, L.Onboarding.StratsRoleBody, "strats.role"),
                GuideStep.Point(L.Onboarding.StratsChipsTitle, L.Onboarding.StratsChipsBody, "strats.chips"),
            });
        Add(tours, "venues", 2,
            new[]
            {
                GuideStep.Note(L.Onboarding.VenuesTitle, L.Onboarding.VenuesBody),
                GuideStep.Point(L.Onboarding.VenuesTimeTitle, L.Onboarding.VenuesTimeBody, "venues.time"),
                GuideStep.Point(L.Onboarding.VenuesFilterTitle, L.Onboarding.VenuesFilterBody, "venues.chips"),
                GuideStep.Point(L.Onboarding.VenuesSearchTitle, L.Onboarding.VenuesSearchBody, "venues.search"),
            });
        Add(tours, "maps", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Maps, L.Onboarding.MapsBody),
                GuideStep.Point(L.Onboarding.MapsLocationTitle, L.Onboarding.MapsLocationBody, "maps.location"),
                GuideStep.Point(L.Onboarding.MapsSearchTitle, L.Onboarding.MapsSearchBody, "maps.search"),
                GuideStep.Note(L.Onboarding.MapsStarTitle, L.Onboarding.MapsStarBody),
            });
        Add(tours, "hunts", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Hunts, L.Onboarding.HuntsBody),
                GuideStep.Point(L.Onboarding.HuntsSignInTitle, L.Onboarding.HuntsSignInBody, "hunts.auth"),
                GuideStep.Point(L.Onboarding.HuntsGuideTitle, L.Onboarding.HuntsGuideBody, "hunts.guide"),
            });
        Add(tours, "fishing", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Fishing, L.Onboarding.FishingBody),
                GuideStep.Point(L.Onboarding.FishingHeroTitle, L.Onboarding.FishingHeroBody, "fishing.hero"),
                GuideStep.Point(L.Onboarding.FishingBlueTitle, L.Onboarding.FishingBlueBody, "fishing.bluefish"),
                GuideStep.Point(L.Onboarding.FishingUpcomingTitle, L.Onboarding.FishingUpcomingBody,
                    "fishing.upcoming"),
            });
        Add(tours, "dailies", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Dailies, L.Onboarding.DailiesBody),
                GuideStep.Tap(L.Onboarding.DailiesCadenceTitle, L.Onboarding.DailiesCadenceBody, "dailies.cadence",
                    "dailies.tab.weekly"),
            });
        Add(tours, "jobs", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Jobs, L.Onboarding.JobsBody),
                GuideStep.Point(L.Onboarding.JobsSwitchTitle, L.Onboarding.JobsSwitchBody, "jobs.row"),
                GuideStep.Point(L.Onboarding.JobsCategoriesTitle, L.Onboarding.JobsCategoriesBody, "jobs.categories"),
                GuideStep.Point(L.Onboarding.JobsColorTitle, L.Onboarding.JobsColorBody, "jobs.color"),
            });
        Add(tours, "housing", 1,
            new[]
            {
                GuideStep.Note(L.Apps.Housing, L.Onboarding.HousingBody),
                GuideStep.Point(L.Onboarding.HousingContextTitle, L.Onboarding.HousingContextBody, "housing.context"),
                GuideStep.Point(L.Onboarding.HousingMapTitle, L.Onboarding.HousingMapBody, "housing.map"),
                GuideStep.Point(L.Onboarding.HousingPhaseTitle, L.Onboarding.HousingPhaseBody, "housing.phase"),
                GuideStep.Point(L.Onboarding.HousingFiltersTitle, L.Onboarding.HousingFiltersBody, "housing.filters"),
                GuideStep.Point(L.Onboarding.HousingWatchTitle, L.Onboarding.HousingWatchBody, "housing.watchlist"),
                GuideStep.Note(L.Onboarding.HousingDataTitle, L.Onboarding.HousingDataBody),
            });
        Add(tours, "inventory", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Inventory, L.Onboarding.InventoryBody),
                GuideStep.Point(L.Onboarding.InventorySummaryTitle, L.Onboarding.InventorySummaryBody,
                    "inventory.summary"),
                GuideStep.Point(L.Onboarding.InventorySourcesTitle, L.Onboarding.InventorySourcesBody,
                    "inventory.sources"),
                GuideStep.Point(L.Onboarding.InventorySearchTitle, L.Onboarding.InventorySearchBody,
                    "inventory.search"),
            });
        Add(tours, "collections", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Collections, L.Onboarding.CollectionsBody),
                GuideStep.Tap(L.Onboarding.CollectionsCategoryTitle, L.Onboarding.CollectionsCategoryBody,
                    "collections.tile.mounts", "collections.category.mounts"),
                GuideStep.Point(L.Onboarding.CollectionsSearchTitle, L.Onboarding.CollectionsSearchBody,
                    "collections.search"),
                GuideStep.Point(L.Onboarding.CollectionsMissingTitle, L.Onboarding.CollectionsMissingBody,
                    "collections.filters"),
            });
        Add(tours, "character", 2,
            new[]
            {
                GuideStep.Note(L.Apps.Character, L.Onboarding.CharacterBody),
                GuideStep.Point(L.Onboarding.CharacterRingsTitle, L.Onboarding.CharacterRingsBody, "character.rings"),
                GuideStep.Point(L.Onboarding.CharacterSummaryTitle, L.Onboarding.CharacterSummaryBody,
                    "character.summary"),
            });
        Add(tours, "health", 1,
            new[]
            {
                GuideStep.Note(L.Health.Title, L.Onboarding.HealthBody),
                GuideStep.Point(L.Onboarding.HealthTodayTitle, L.Onboarding.HealthTodayBody, "health.today"),
                GuideStep.Tap(L.Onboarding.HealthTabsTitle, L.Onboarding.HealthTabsBody, "health.tabs",
                    "health.tab.goals"),
                GuideStep.Note(L.Onboarding.HealthGoalsTitle, L.Onboarding.HealthGoalsBody),
                GuideStep.Note(L.Onboarding.HealthPrivacyTitle, L.Onboarding.HealthPrivacyBody),
            });
    }
}
