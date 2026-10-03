using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Message.Widgets;

internal sealed class ChatsWidget : IHomeWidget
{
    private const string MessageAppId = "message";
    private const float RefreshSeconds = 2f;
    private const float ForcedRefreshSeconds = 30f;
    private const int MediumRows = 3;
    private const int LargeRows = 7;
    private const int PhotoKind = 1;
    private const int VoiceKind = 3;

    private static readonly Vector4 MonogramGray = new(0.55f, 0.56f, 0.6f, 1f);
    private static readonly int[] SampleMinutesAgo = { 2, 34, 190 };
    private static readonly int[] SampleUnread = { 2, 0, 0 };
    private static readonly LocString[] SampleMessages =
    {
        L.WidgetsPeople.SampleMessage,
        L.WidgetsPeople.SampleMessageRaid,
        L.WidgetsPeople.SampleMessageThanks,
    };

    private readonly struct ChatRow
    {
        public readonly string Title;
        public readonly string Preview;
        public readonly int Kind;
        public readonly string Time;
        public readonly string UnreadLabel;
        public readonly bool Muted;
        public readonly bool IsGroup;
        public readonly string? AvatarUrl;
        public readonly WidgetRoute Route;

        public ChatRow(string title, string preview, int kind, string time, string unreadLabel, bool muted,
            bool isGroup, string? avatarUrl, WidgetRoute route)
        {
            Title = title;
            Preview = preview;
            Kind = kind;
            Time = time;
            UnreadLabel = unreadLabel;
            Muted = muted;
            IsGroup = isGroup;
            AvatarUrl = avatarUrl;
            Route = route;
        }
    }

    private readonly DirectMessagesStore store;
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly List<ConversationDto> sorted = new(LargeRows * 2);
    private readonly ChatRow[] rows = new ChatRow[LargeRows];
    private readonly CachedText[] sampleTimes = new CachedText[3];
    private int rowCount;
    private int unreadTotal;
    private ConversationDto[]? seenConversations;
    private int seenArchived = -1;
    private float sinceCheck = RefreshSeconds;
    private float sinceRebuild;
    private WidgetFrame frame;

    public ChatsWidget(DirectMessagesStore store, Configuration configuration, AethernetSession session,
        RemoteImageCache images)
    {
        this.store = store;
        this.configuration = configuration;
        this.session = session;
        this.images = images;
    }

    public string Id => "message.recent";
    public string DisplayName => Loc.T(L.WidgetsPeople.Chats);
    public string Description => Loc.T(L.WidgetsPeople.ChatsDescription);
    public string AppId => MessageAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config) => session.IsSignedIn && store.UnreadTotal > 0 ? 0.75f : 0f;

    public void Draw(in WidgetContext context)
    {
        var signedIn = session.IsSignedIn;
        if (frame.First())
        {
            Advance(context.Delta, signedIn);
        }

        if (signedIn && !context.Preview && context.Opacity > 0f)
        {
            store.NoteInboxWatched();
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var sample = context.Preview && (!signedIn || rowCount == 0);
        var accent = AppAccents.For(MessageAppId);
        var headerBottom = WidgetChrome.Header(context, ink, MessageAppId, L.Apps.Message, accent);
        if (!sample && signedIn && unreadTotal > 0)
        {
            var eyebrowCenter = content.Min.Y + (headerBottom - content.Min.Y) * 0.5f;
            PeopleWidgetChrome.Badge(context, ink, content.Max.X, eyebrowCenter,
                PeopleWidgetChrome.CountLabel(unreadTotal), false);
        }

        var area = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * 0.5f * context.Scale),
            content.Max);
        if (!signedIn && !sample)
        {
            WidgetChrome.Message(context, ink, area, MessageAppId, Loc.T(L.WidgetsPeople.SignInHint),
                string.Empty);
            return;
        }

        var maximum = context.Size == WidgetSize.Large ? LargeRows : MediumRows;
        var count = PeopleWidgetChrome.RowCount(area.Height, context.Scale, maximum);
        if (sample)
        {
            DrawSamples(context, ink, area, Math.Min(count, SampleMessages.Length));
            return;
        }

        if (rowCount == 0)
        {
            if (!store.ConversationsLoaded)
            {
                WidgetChrome.RedactedRows(context, ink, area, count, WidgetRowLead.Avatar);
                return;
            }

            WidgetChrome.Message(context, ink, area, MessageAppId, Loc.T(L.WidgetsPeople.NoChats),
                Loc.T(L.WidgetsPeople.NoChatsHint));
            return;
        }

        count = Math.Min(count, rowCount);
        var rowHeight = area.Height / PeopleWidgetChrome.RowCount(area.Height, context.Scale, maximum);
        for (var index = 0; index < count; index++)
        {
            DrawRow(context, ink, area, index, rowHeight, rows[index], index < count - 1);
        }
    }

    private void Advance(float delta, bool signedIn)
    {
        sinceCheck += delta;
        sinceRebuild += delta;
        if (sinceCheck < RefreshSeconds)
        {
            return;
        }

        sinceCheck = 0f;
        if (!signedIn)
        {
            rowCount = 0;
            unreadTotal = 0;
            return;
        }

        unreadTotal = store.UnreadTotal;
        var conversations = store.Conversations;
        if (ReferenceEquals(conversations, seenConversations)
            && configuration.MessageArchivedChats.Count == seenArchived
            && sinceRebuild < ForcedRefreshSeconds)
        {
            return;
        }

        Rebuild(conversations);
    }

    private void Rebuild(ConversationDto[] conversations)
    {
        sinceRebuild = 0f;
        seenConversations = conversations;
        seenArchived = configuration.MessageArchivedChats.Count;
        sorted.Clear();
        for (var index = 0; index < conversations.Length; index++)
        {
            if (!configuration.MessageArchivedChats.Contains(conversations[index].Id))
            {
                sorted.Add(conversations[index]);
            }
        }

        sorted.Sort(static (left, right) => right.LastMessageAtUnix.CompareTo(left.LastMessageAtUnix));
        rowCount = Math.Min(sorted.Count, LargeRows);
        for (var index = 0; index < rowCount; index++)
        {
            var item = sorted[index];
            var preview = item.LastMessagePreview.Length > 0 ? ChatText.ListPreview(item.LastMessagePreview) : string.Empty;
            var time = item.LastMessageAtUnix > 0 ? TimeText.Short(item.LastMessageAtUnix) : string.Empty;
            rows[index] = new ChatRow(store.DisplayTitle(item), preview, item.LastMessageKind, time,
                PeopleWidgetChrome.CountLabel(item.UnreadCount), item.Muted, item.IsGroup,
                item.IsGroup ? item.AvatarUrl : item.OtherAvatarUrl,
                WidgetRoute.To(MessageAppId, WidgetRouteKind.Conversation, item.Id));
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect area, int index, float rowHeight,
        in ChatRow row, bool separator)
    {
        var scale = context.Scale;
        var rowRect = new Rect(new Vector2(area.Min.X, area.Min.Y + index * rowHeight),
            new Vector2(area.Max.X, area.Min.Y + (index + 1) * rowHeight));
        WidgetControls.Link(context, ink, index + 1, rowRect, row.Route);
        var radius = PeopleWidgetChrome.RowAvatarRadius * scale;
        var center = new Vector2(area.Min.X + radius, rowRect.Center.Y);
        if (row.IsGroup && string.IsNullOrEmpty(row.AvatarUrl))
        {
            PeopleWidgetChrome.GroupAvatar(context, ink, center, radius);
        }
        else
        {
            var texture = string.IsNullOrEmpty(row.AvatarUrl) ? null : images.Avatar(row.AvatarUrl, radius * 2f).Texture;
            PeopleWidgetChrome.Avatar(context, ink, center, radius, row.Title, texture, MonogramGray);
        }

        var textLeft = center.X + radius + PeopleWidgetChrome.RowTextGap * scale;
        PeopleWidgetChrome.Row(context, ink, rowRect, textLeft, row.Title, Preview(row), row.Time, row.UnreadLabel,
            row.Muted, row.UnreadLabel.Length > 0);
        if (separator)
        {
            WidgetChrome.Separator(context, ink, textLeft, area.Max.X, rowRect.Max.Y);
        }
    }

    private static string Preview(in ChatRow row)
    {
        if (row.Preview.Length > 0)
        {
            return row.Preview;
        }

        return row.Kind switch
        {
            PhotoKind => Loc.T(L.DirectMessages.PhotoPreview),
            VoiceKind => Loc.T(L.DirectMessages.VoicePreview),
            _ => string.Empty,
        };
    }

    private void DrawSamples(in WidgetContext context, in WidgetInk ink, Rect area, int count)
    {
        var rowHeight = area.Height / Math.Max(1, count);
        var now = DateTime.Now;
        for (var index = 0; index < count; index++)
        {
            var moment = now.AddMinutes(-SampleMinutesAgo[index]);
            var key = moment.Ticks / TimeSpan.TicksPerMinute;
            var time = sampleTimes[index].IsCurrent(key)
                ? sampleTimes[index].Value
                : sampleTimes[index].Store(key, TimeText.Clock(moment));
            var row = new ChatRow(WidgetSamples.Names[index], Loc.T(SampleMessages[index]), 0, time,
                PeopleWidgetChrome.CountLabel(SampleUnread[index]), false, false, null, default);
            DrawRow(context, ink, area, index, rowHeight, row, index < count - 1);
        }
    }

    public void Dispose()
    {
    }
}
