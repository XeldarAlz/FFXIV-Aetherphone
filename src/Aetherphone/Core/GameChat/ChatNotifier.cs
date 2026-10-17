using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.GameChat;

internal sealed class ChatNotifier : IDisposable
{
    public const string AppId = "messages";

    private readonly ChatLog log;
    private readonly TabStore tabs;
    private readonly ChatInbox inbox;
    private readonly TellPreferences tellPreferences;
    private readonly LinkpearlNotificationGate gate;
    private readonly NotificationService notifications;
    private readonly AppGate installed;

    public ChatNotifier(ChatLog log, TabStore tabs, ChatInbox inbox, TellPreferences tellPreferences,
        LinkpearlNotificationGate gate, NotificationService notifications, AppGate installed)
    {
        this.log = log;
        this.tabs = tabs;
        this.inbox = inbox;
        this.tellPreferences = tellPreferences;
        this.gate = gate;
        this.notifications = notifications;
        this.installed = installed;
        log.Appended += OnAppended;
    }

    public void Dispose() => log.Appended -= OnAppended;

    private void OnAppended(ChatEntry entry)
    {
        if (!installed.Open || gate.Paused || entry.IsSelf)
        {
            return;
        }

        if (ChannelStyles.Shared.NeverUnread(entry.ChannelKey))
        {
            return;
        }

        if (ChatStreams.IsTell(entry.StreamKey))
        {
            if (!inbox.IsViewing(entry.StreamKey) && !tellPreferences.IsMuted(entry.StreamKey))
            {
                Raise(entry.AuthorName, entry.Text, entry.StreamKey, entry.At);
            }

            return;
        }

        var all = tabs.Tabs;
        for (var index = 0; index < all.Count; index++)
        {
            var tab = all[index];
            if (!tab.Includes(entry.ChannelKey) || !Alerts(tab, entry))
            {
                continue;
            }

            var key = ChatInbox.KeyForTab(tab);
            if (inbox.IsViewing(key))
            {
                return;
            }

            Raise(tab.Name, string.Concat(entry.AuthorName, ": ", entry.Text), key, entry.At);
            return;
        }
    }

    private static bool Alerts(ChatTab tab, ChatEntry entry)
    {
        if (tab.Alerts == AlertPolicy.Off || tab.IsMuted(entry.ChannelKey))
        {
            return false;
        }

        return tab.Alerts == AlertPolicy.All || entry.IsMention;
    }

    private void Raise(string title, string body, string groupKey, DateTime at) =>
        notifications.Notify(new PhoneNotification(AppId, title, body, at, AppAccents.For(AppId), groupKey));
}
