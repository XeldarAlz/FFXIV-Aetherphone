using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddGameContentTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "skywatcher", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.SkywatcherCurrentTitle, L.Onboarding.SkywatcherCurrentBody,
                    "skywatcher.current", GuideGesture.None),
                GuideStep.Point(L.Onboarding.SkywatcherForecastTitle, L.Onboarding.SkywatcherHoursBody,
                    "skywatcher.forecast", GuideGesture.None),
                GuideStep.Point(L.Onboarding.SkywatcherZonesTitle, L.Onboarding.SkywatcherZonesBody,
                    "skywatcher.tab.zones", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.SkywatcherControlTitle, L.Onboarding.SkywatcherControlBody,
                    "skywatcher.tab.control"),
                GuideStep.Point(L.Onboarding.SkywatcherTimeTitle, L.Onboarding.SkywatcherTimeBody,
                    "skywatcher.control.time", GuideGesture.None),
                GuideStep.Point(L.Onboarding.SkywatcherWeatherTitle, L.Onboarding.SkywatcherWeatherBody,
                    "skywatcher.control.weather", GuideGesture.None),
            });
        Add(tours, "market", 4,
            new[]
            {
                GuideStep.Intro(L.Apps.Market, L.Onboarding.MarketIntroBody),
                GuideStep.TryUntil(L.Onboarding.MarketSearchTitle, L.Onboarding.MarketFindBody, "market.search",
                    GuideGesture.Tap, "market.result.first"),
                GuideStep.TryTap(L.Onboarding.MarketOpenTitle, L.Onboarding.MarketOpenBody, "market.result.first"),
                GuideStep.Point(L.Onboarding.MarketCheapestTitle, L.Onboarding.MarketCheapestBody,
                    "market.detail.hero", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.MarketScopeTitle, L.Onboarding.MarketCompareBody, "market.scope"),
                GuideStep.Point(L.Onboarding.MarketAlertTitle, L.Onboarding.MarketAlertBody, "market.alert",
                    GuideGesture.Tap),
                GuideStep.Point(L.Market.TourWatchTitle, L.Market.TourWatchBody, "market.favorite",
                    GuideGesture.Tap),
            });
        Add(tours, "maps", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.MapsMapTitle, L.Onboarding.MapsMapBody, "maps.map", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.MapsExpandTitle, L.Onboarding.MapsExpandBody, "maps.expansion.first"),
                GuideStep.TryTap(L.Onboarding.MapsOpenTitle, L.Onboarding.MapsOpenBody, "maps.destination.first"),
                GuideStep.Point(L.Onboarding.MapsTravelTitle, L.Onboarding.MapsTeleportBody, "maps.place.teleport",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.MapsStarTitle, L.Onboarding.MapsFavoriteBody, "maps.place.favorite"),
            });
        Add(tours, "hunts", 7,
            new[]
            {
                GuideStep.Point(L.Onboarding.HuntsWindowsTitle, L.Hunts.TourBoardBody, "hunts.row.first",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.HuntsFilterTitle, L.Onboarding.HuntsFilterBody, "hunts.filters",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.HuntsSignInTitle, L.Hunts.TourStatusBody, "hunts.auth",
                    GuideGesture.Tap),
                GuideStep.Point(L.Hunts.TourTrainsTitle, L.Hunts.TourTrainsBody, "hunts.tab.trains",
                    GuideGesture.Tap),
                GuideStep.Point(L.Hunts.GuideTab, L.Onboarding.HuntsGuidesBody, "hunts.guide", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.HuntsOpenTitle, L.Onboarding.HuntsOpenBody, "hunts.row.first"),
                GuideStep.Point(L.Onboarding.HuntsMapTitle, L.Onboarding.HuntsMapBody, "hunts.detail.map",
                    GuideGesture.None),
            });
        Add(tours, "fishing", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.FishingHeroTitle, L.Onboarding.FishingHeroBody, "fishing.hero",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.FishingBlueTitle, L.Onboarding.FishingBlueBody, "fishing.bluefish",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.FishingRouteTitle, L.Onboarding.FishingRouteBody, "fishing.route"),
                GuideStep.Point(L.Onboarding.FishingUpcomingTitle, L.Onboarding.FishingLaterBody, "fishing.upcoming",
                    GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.FishingTimedTitle, L.Onboarding.FishingTimedBody, "fishing.tab.fish",
                    GuideGesture.Tap, "fishing.fish.first"),
                GuideStep.Point(L.Onboarding.FishingFishTitle, L.Onboarding.FishingFishBody, "fishing.fish.first",
                    GuideGesture.Tap),
            });
        Add(tours, "venues", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.VenuesScopeTitle, L.Onboarding.VenuesScopeBody, "venues.scope",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.VenuesLiveTitle, L.Onboarding.VenuesOpenNowBody, "venues.live",
                    GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.VenuesCategoriesTitle, L.Onboarding.VenuesCategoryBody,
                    "venues.category.first"),
                GuideStep.TryUntil(L.Onboarding.VenuesOpenTitle, L.Onboarding.VenuesOpenBody, "venues.card.first",
                    GuideGesture.Tap, "venues.detail.favorite"),
                GuideStep.Point(L.Travel.GoThere, L.Onboarding.VenuesGoBody, "venues.detail.go", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.VenuesSaveTitle, L.Onboarding.VenuesSaveBody, "venues.detail.favorite",
                    GuideGesture.Tap),
            });
        Add(tours, "strats", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.StratsFightsTitle, L.Onboarding.StratsOpenBody, "strats.fight.first"),
                GuideStep.Point(L.Onboarding.StratsStrategyTitle, L.Onboarding.StratsStrategyBody, "strats.strategy",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.StratsRoleTitle, L.Onboarding.StratsSpotBody, "strats.role"),
                GuideStep.Point(L.Strats.TourSetupTitle, L.Strats.TourSetupBody, "strats.setup", GuideGesture.None),
                GuideStep.Point(L.Strats.TourContentsTitle, L.Strats.TourContentsBody, "strats.contents",
                    GuideGesture.Tap),
                GuideStep.TryUntil(L.Onboarding.StratsScrollTitle, L.Onboarding.StratsScrollBody, "strats.scroll",
                    GuideGesture.SwipeUp, "strats.mechanic.first"),
                GuideStep.Point(L.Onboarding.StratsMechanicTitle, L.Onboarding.StratsMechanicBody,
                    "strats.mechanic.first", GuideGesture.None),
            });
    }
}
