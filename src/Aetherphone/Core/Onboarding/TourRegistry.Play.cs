using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddPlayTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "games", 5,
            new[]
            {
                GuideStep.Point(L.Onboarding.GamesDailyTitle, L.Onboarding.GamesDailyBody, "games.featured",
                    GuideGesture.Tap),
                GuideStep.TryTap(L.GamesHub.TourTogetherTitle, L.GamesHub.TourTogetherBody, "games.tab.together"),
                GuideStep.TryTap(L.GamesHub.TourLibraryTitle, L.GamesHub.TourLibraryBody, "games.tab.library"),
                GuideStep.TryTap(L.GamesHub.TourProfileTitle, L.GamesHub.TourProfileBody, "games.tab.records"),
            });
        Add(tours, "casino", 3,
            new[]
            {
                GuideStep.Intro(L.Apps.Casino, L.Onboarding.CasinoIntroBody),
                GuideStep.Point(L.Casino.TourBankrollTitle, L.Casino.TourBankrollBody, "casino.chipbar",
                    GuideGesture.None),
                GuideStep.Point(L.Casino.TourTonightTitle, L.Casino.TourTonightBody, "casino.tonight",
                    GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.CasinoDailySpinTitle, L.Onboarding.CasinoDailySpinBody, "casino.spin",
                    GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.CasinoGamesTabTitle, L.Onboarding.CasinoGamesTabBody,
                    "casino.tab.games"),
                GuideStep.TryTap(L.Onboarding.CasinoRulesTitle, L.Onboarding.CasinoRulesBody, "casino.rules"),
            });
        Add(tours, "coin", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.CoinWalletTitle, L.Onboarding.CoinWalletBody, "coin.balance",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.CoinDailyTitle, L.Onboarding.CoinDailyBody, "coin.checkin"),
                GuideStep.Point(L.Coin.TourTodayTitle, L.Coin.TourTodayBody, "coin.today", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.CoinShopTabTitle, L.Onboarding.CoinShopTabBody, "coin.tab.shop"),
                GuideStep.Point(L.Onboarding.CoinShelvesTitle, L.Onboarding.CoinShelvesBody, "coin.shop",
                    GuideGesture.Tap),
            });
        Add(tours, "clock", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.ClockGameTimeTitle, L.Onboarding.ClockGameTimeBody, "clock.world.game",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.ClockAlarmsTabTitle, L.Onboarding.ClockAlarmsTabBody,
                    "clock.tab.alarms"),
                GuideStep.TryUntil(L.Onboarding.ClockNewAlarmTitle, L.Onboarding.ClockNewAlarmBody, "clock.add",
                    GuideGesture.Tap, "clock.alarm.time"),
                GuideStep.Span(L.Onboarding.ClockAlarmTimeTitle, L.Onboarding.ClockAlarmTimeBody, "clock.alarm.time",
                    "clock.alarm.repeat"),
                GuideStep.Point(L.Onboarding.ClockAlarmSaveTitle, L.Onboarding.ClockAlarmSaveBody,
                    "clock.alarm.save", GuideGesture.None),
            });
        Add(tours, "calendar", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.CalendarPickDayTitle, L.Onboarding.CalendarPickDayBody,
                    "calendar.grid"),
                GuideStep.Point(L.Onboarding.CalendarDayEventsTitle, L.Onboarding.CalendarDayEventsBody,
                    "calendar.agenda", GuideGesture.None),
                GuideStep.Point(L.Onboarding.CalendarGroupsTitle, L.Onboarding.CalendarGroupsBody,
                    "calendar.groups", GuideGesture.None),
                GuideStep.TryUntil(L.Onboarding.CalendarNewEventTitle, L.Onboarding.CalendarNewEventBody,
                    "calendar.new", GuideGesture.Tap, "calendar.editor.alert"),
                GuideStep.Point(L.Onboarding.CalendarAlertTitle, L.Onboarding.CalendarAlertBody,
                    "calendar.editor.alert", GuideGesture.None),
            });
        Add(tours, "calculator", 4,
            new[]
            {
                GuideStep.TryUntil(L.Onboarding.CalculatorSumTitle, L.Onboarding.CalculatorSumBody,
                    "calculator.keypad", GuideGesture.Tap, "calculator.answer"),
                GuideStep.TryTap(L.Onboarding.CalculatorReuseTitle, L.Onboarding.CalculatorReuseBody,
                    "calculator.tape"),
                GuideStep.Point(L.Onboarding.CalculatorTypeTitle, L.Onboarding.CalculatorTypeBody,
                    "calculator.display", GuideGesture.None),
            });
        Add(tours, "timers", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.TimersNextUpTitle, L.Onboarding.TimersNextUpBody, "timers.hero",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.TimersCharactersTitle, L.Onboarding.TimersCharactersBody,
                    "timers.retainers", GuideGesture.None),
                GuideStep.Point(L.Onboarding.TimersBellTitle, L.Onboarding.TimersBellBody, "timers.bell",
                    GuideGesture.Tap),
            });
    }
}
