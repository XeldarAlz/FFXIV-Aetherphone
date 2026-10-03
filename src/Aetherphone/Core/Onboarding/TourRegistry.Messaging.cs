using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Onboarding;

internal static partial class TourRegistry
{
    private static void AddMessagingTours(Dictionary<string, GuideSequence> tours)
    {
        Add(tours, "messages", 4,
            new[]
            {
                GuideStep.Intro(L.Apps.Linkpearl, L.Onboarding.LinkpearlIntroBody),
                GuideStep.TryUntil(L.Onboarding.StartChatTitle, L.Onboarding.LinkpearlNewChatBody, "messages.new",
                    GuideGesture.Tap, "messages.newchat.tiles"),
                GuideStep.TryUntil(L.Onboarding.LinkpearlChannelTitle, L.Onboarding.LinkpearlChannelBody,
                    "messages.newchat.tiles", GuideGesture.Tap, "messages.composer"),
                GuideStep.Point(L.Onboarding.LinkpearlReplyTitle, L.Onboarding.LinkpearlReplyBody,
                    "messages.composer", GuideGesture.None),
                GuideStep.TryTap(L.Onboarding.LinkpearlLayoutTitle, L.Onboarding.LinkpearlLayoutBody,
                    "messages.thread.layout"),
                GuideStep.TryUntil(L.Onboarding.LinkpearlMoreTitle, L.Onboarding.LinkpearlMoreBody,
                    "messages.thread.more", GuideGesture.Tap, "messages.sheet"),
            });
        Add(tours, "message", 3,
            new[]
            {
                GuideStep.Intro(L.Apps.Message, L.Onboarding.ChocoChatIntroBody),
                GuideStep.Point(L.Onboarding.StartChatTitle, L.Onboarding.ChocoChatNewChatBody, "message.newchat",
                    GuideGesture.Tap),
                GuideStep.TryTap(L.Onboarding.ChocoChatCallsTitle, L.Onboarding.ChocoChatCallsBody,
                    "message.tab.calls"),
                GuideStep.TryTap(L.Onboarding.ChocoChatContactsTitle, L.Onboarding.ChocoChatContactsBody,
                    "message.tab.contacts"),
                GuideStep.Point(L.Onboarding.ChocoChatNumberTitle, L.Onboarding.ChocoChatNumberBody,
                    "message.mynumber", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.ChocoChatAddContactTitle, L.Onboarding.ChocoChatAddContactBody,
                    "message.addcontact", GuideGesture.Tap),
            });
        Add(tours, "notifications", 4,
            new[]
            {
                GuideStep.Point(L.Onboarding.NotificationsAlertsTitle, L.Onboarding.NotificationsAlertsBody,
                    "notifications.list", GuideGesture.None),
                GuideStep.Point(L.Onboarding.NotificationsFocusTitle, L.Onboarding.NotificationsFocusBody,
                    "notifications.focus", GuideGesture.Tap),
                GuideStep.Point(L.Onboarding.NotificationsAnywhereTitle, L.Onboarding.NotificationsAnywhereBody,
                    "chrome.controlcenter", GuideGesture.SwipeDown),
            });
    }
}
