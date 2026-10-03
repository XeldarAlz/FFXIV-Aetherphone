using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddSystemTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "settings", 3,
            new[]
            {
                GuideStep.Point(L.Onboarding.SettingsProfileTitle, L.Onboarding.SettingsProfileBody,
                    "settings.account", GuideGesture.Tap),
                GuideStep.Span(L.Onboarding.SettingsLookTitle, L.Onboarding.SettingsLookBody,
                    "settings.row.appearance", "settings.row.display"),
                GuideStep.TryUntil(L.Onboarding.SettingsSearchTitle, L.Onboarding.SettingsSearchBody,
                    "settings.search", GuideGesture.Tap, "settings.row.tutorials"),
                GuideStep.TryTap(L.Onboarding.SettingsFindToursTitle, L.Onboarding.SettingsFindToursBody,
                    "settings.row.tutorials"),
                GuideStep.Point(L.Onboarding.SettingsReplayTitle, L.Onboarding.SettingsReplayBody,
                    "settings.tutorials.actions", GuideGesture.None),
            });
        Add(tours, "appstore", 2,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.AppStoreAppsTabTitle, L.Onboarding.AppStoreAppsTabBody,
                    "appstore.tab.apps"),
                GuideStep.TryTap(L.Onboarding.AppStoreCategoryTitle, L.Onboarding.AppStoreCategoryBody,
                    "appstore.category"),
                GuideStep.TryTap(L.Onboarding.AppStoreDetailTitle, L.Onboarding.AppStoreDetailBody, "appstore.row"),
                GuideStep.Point(L.Onboarding.AppStoreInstallTitle, L.Onboarding.AppStoreInstallBody,
                    "appstore.detail.get", GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.AppStoreFindTitle, L.Onboarding.AppStoreFindBody,
                    "appstore.tab.search"),
            });
        Add(tours, "shortcuts", 2,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.ShortcutsStartTitle, L.Onboarding.ShortcutsStartBody, "shortcuts.new"),
                GuideStep.Point(L.Onboarding.ShortcutsStepsTitle, L.Onboarding.ShortcutsStepsBody,
                    "shortcuts.editor.add", GuideGesture.None),
                GuideStep.Span(L.Onboarding.ShortcutsSaveTitle, L.Onboarding.ShortcutsSaveBody,
                    "shortcuts.editor.name", "shortcuts.editor.save"),
                GuideStep.TryTap(L.Onboarding.ShortcutsLeaveTitle, L.Onboarding.ShortcutsLeaveBody,
                    "shortcuts.editor.back"),
                GuideStep.TryTap(L.Onboarding.ShortcutsPluginsTabTitle, L.Onboarding.ShortcutsPluginsTabBody,
                    "shortcuts.tab.plugins"),
                GuideStep.Point(L.Onboarding.ShortcutsPluginRowTitle, L.Onboarding.ShortcutsPluginRowBody,
                    "shortcuts.plugin.row", GuideGesture.Tap),
            });
        Add(tours, "news", 3,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.NewsMaintenanceTitle, L.Onboarding.NewsMaintenanceBody,
                    "news.tab.maintenance"),
                GuideStep.Point(L.Onboarding.NewsWindowTitle, L.Onboarding.NewsWindowBody, "news.row",
                    GuideGesture.None),
                GuideStep.Point(L.Onboarding.NewsLatestTitle, L.Onboarding.NewsLatestBody, "news.refresh",
                    GuideGesture.Tap),
            });
        Add(tours, "feedback", 4,
            new[]
            {
                GuideStep.TryTap(L.Onboarding.FeedbackKindTitle, L.Onboarding.FeedbackKindBody, "feedback.kind"),
                GuideStep.TryTap(L.Onboarding.FeedbackMessageTitle, L.Onboarding.FeedbackMessageBody,
                    "feedback.input"),
                GuideStep.Point(L.Onboarding.FeedbackScreenshotsTitle, L.Onboarding.FeedbackScreenshotsBody,
                    "feedback.attach", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.FeedbackSubmitTitle, L.Onboarding.FeedbackSubmitBody, "feedback.send",
                    GuideGesture.Tap),
            });
        Add(tours, "health", 2,
            new[]
            {
                GuideStep.Point(L.Onboarding.HealthStepsTitle, L.Onboarding.HealthStepsBody, "health.today",
                    GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.HealthWaterTabTitle, L.Onboarding.HealthWaterTabBody,
                    "health.tab.water"),
                GuideStep.Point(L.Onboarding.HealthDrinksTitle, L.Onboarding.HealthDrinksBody, "health.water.drinks",
                    GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.HealthGoalsTabTitle, L.Onboarding.HealthGoalsTabBody,
                    "health.tab.goals"),
                GuideStep.Point(L.Onboarding.HealthGoalTitle, L.Onboarding.HealthGoalBody, "health.goal",
                    GuideGesture.None),
            });
    }
}
