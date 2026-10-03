using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Notifications.Widgets;

internal sealed class RecentNotificationsWidget : IHomeWidget
{
    private const string NotificationsAppId = "notifications";
    private const float RefreshSeconds = 1f;
    private const float ForcedRefreshSeconds = 30f;
    private const int MediumRows = 3;
    private const int LargeRows = 7;
    private const float IconUnits = 28f;
    private const float UnreadDotInset = 3f;

    private static readonly string[] SampleApps = { "message", "muster", "aethergram" };
    private static readonly int[] SampleMinutesAgo = { 3, 15, 48 };

    private readonly struct NotificationRow
    {
        public readonly PhoneNotification Notification;
        public readonly string Time;
        public readonly WidgetRoute Route;

        public NotificationRow(PhoneNotification notification, string time, WidgetRoute route)
        {
            Notification = notification;
            Time = time;
            Route = route;
        }
    }

    private readonly NotificationService notifications;
    private readonly NotificationRow[] rows = new NotificationRow[LargeRows];
    private readonly NotificationRow[] samples = new NotificationRow[SampleApps.Length];
    private int rowCount;
    private int seenVersion = -1;
    private float sinceCheck = RefreshSeconds;
    private float sinceRebuild;
    private CultureInfo? samplesCulture;
    private long samplesMinute = -1;
    private WidgetFrame frame;

    public RecentNotificationsWidget(NotificationService notifications)
    {
        this.notifications = notifications;
    }

    public string Id => "notifications.recent";
    public string DisplayName => Loc.T(L.Apps.Notifications);
    public string Description => Loc.T(L.WidgetsPeople.NotificationsDescription);
    public string AppId => NotificationsAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => notifications.UnreadCount > 0 ? 0.5f : 0f;

    public void Draw(in WidgetContext context)
    {
        if (frame.First())
        {
            Advance(context.Delta);
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var sample = context.Preview && rowCount == 0;
        var headerBottom = WidgetChrome.Header(context, ink, NotificationsAppId, L.Apps.Notifications,
            AppAccents.For(NotificationsAppId));
        var unread = notifications.UnreadCount;
        if (!sample && unread > 0)
        {
            PeopleWidgetChrome.Badge(context, ink, content.Max.X, content.Min.Y + (headerBottom - content.Min.Y) * 0.5f,
                PeopleWidgetChrome.CountLabel(unread), false);
        }

        var area = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * 0.5f * context.Scale),
            content.Max);
        if (!sample && rowCount == 0)
        {
            WidgetChrome.Message(context, ink, area, NotificationsAppId, Loc.T(L.WidgetsPeople.NoNotifications),
                Loc.T(L.WidgetsPeople.CaughtUp));
            return;
        }

        var maximum = context.Size == WidgetSize.Large ? LargeRows : MediumRows;
        var slots = PeopleWidgetChrome.RowCount(area.Height, context.Scale, maximum);
        var rowHeight = area.Height / slots;
        var source = sample ? Samples() : rows;
        var count = Math.Min(slots, sample ? samples.Length : rowCount);
        for (var index = 0; index < count; index++)
        {
            DrawRow(context, ink, area, index, rowHeight, source[index], index < count - 1);
        }
    }

    private void Advance(float delta)
    {
        sinceCheck += delta;
        sinceRebuild += delta;
        if (sinceCheck < RefreshSeconds)
        {
            return;
        }

        sinceCheck = 0f;
        if (notifications.Version == seenVersion && sinceRebuild < ForcedRefreshSeconds)
        {
            return;
        }

        sinceRebuild = 0f;
        seenVersion = notifications.Version;
        var recent = notifications.Recent;
        rowCount = 0;
        for (var index = recent.Count - 1; index >= 0 && rowCount < LargeRows; index--)
        {
            var notification = recent[index];
            rows[rowCount++] = new NotificationRow(notification,
                TimeText.Ago(notification.ReceivedAt.ToUniversalTime()),
                WidgetRoute.To(notification.AppId, WidgetRouteKind.Notification,
                    notification.Id.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private NotificationRow[] Samples()
    {
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        if (ReferenceEquals(samplesCulture, Loc.Culture) && samplesMinute == minute)
        {
            return samples;
        }

        samplesCulture = Loc.Culture;
        samplesMinute = minute;
        var now = DateTime.Now;
        for (var index = 0; index < samples.Length; index++)
        {
            var body = index switch
            {
                0 => Loc.T(L.WidgetsPeople.SampleMessage),
                1 => Loc.T(L.WidgetsPeople.SampleNotificationMuster),
                _ => Loc.T(L.WidgetsPeople.SampleNotificationLike),
            };
            var title = index == 1 ? Loc.T(L.Apps.Muster) : WidgetSamples.Names[index];
            var received = now.AddMinutes(-SampleMinutesAgo[index]);
            var notification = new PhoneNotification(SampleApps[index], title, body, received,
                AppAccents.For(SampleApps[index]));
            samples[index] = new NotificationRow(notification, TimeText.Ago(received.ToUniversalTime()), default);
        }

        return samples;
    }

    private static void DrawRow(in WidgetContext context, in WidgetInk ink, Rect area, int index, float rowHeight,
        in NotificationRow row, bool separator)
    {
        var scale = context.Scale;
        var rowRect = new Rect(new Vector2(area.Min.X, area.Min.Y + index * rowHeight),
            new Vector2(area.Max.X, area.Min.Y + (index + 1) * rowHeight));
        WidgetControls.Link(context, ink, index + 1, rowRect, row.Route);
        var notification = row.Notification;
        var iconSize = IconUnits * scale;
        var iconMin = new Vector2(area.Min.X, rowRect.Center.Y - iconSize * 0.5f);
        NotificationCard.DrawAppIcon(context.DrawList, notification, iconMin, iconSize, ink.Opacity);
        if (!notification.Read && row.Route.Kind == WidgetRouteKind.Notification)
        {
            PeopleWidgetChrome.Dot(context, ink,
                iconMin + new Vector2(iconSize - UnreadDotInset * scale, UnreadDotInset * scale), false);
        }

        var textLeft = iconMin.X + iconSize + PeopleWidgetChrome.RowTextGap * scale;
        PeopleWidgetChrome.Row(context, ink, rowRect, textLeft, notification.Title, notification.SingleLineBody,
            row.Time, string.Empty, false, false);
        if (separator)
        {
            WidgetChrome.Separator(context, ink, textLeft, area.Max.X, rowRect.Max.Y);
        }
    }

    public void Dispose()
    {
    }
}
