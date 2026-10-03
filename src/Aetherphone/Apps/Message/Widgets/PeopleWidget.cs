using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;

namespace Aetherphone.Apps.Message.Widgets;

internal sealed class PeopleWidget : IHomeWidget
{
    private const string MessageAppId = "message";
    private const string PersonKey = "person";
    private const int RefreshMilliseconds = 3000;
    private const long ForcedRefreshMilliseconds = 30000;
    private const int Capacity = 48;
    private const int Columns = 4;
    private const int MediumCount = 4;
    private const int LargeCount = 8;
    private const float CellInset = 4f;
    private const float NameGap = 6f;
    private const float BadgeLift = 0.78f;
    private const int SampleUnread = 2;

    private static readonly Vector4 MonogramGray = new(0.55f, 0.56f, 0.6f, 1f);
    private static readonly string[] SampleFirstNames = FirstNames();

    private readonly struct Person
    {
        public readonly string UserId;
        public readonly string Name;
        public readonly string? AvatarUrl;
        public readonly string UnreadLabel;
        public readonly bool Muted;
        public readonly WidgetRoute Route;

        public Person(string userId, string name, string? avatarUrl, string unreadLabel, bool muted, WidgetRoute route)
        {
            UserId = userId;
            Name = name;
            AvatarUrl = avatarUrl;
            UnreadLabel = unreadLabel;
            Muted = muted;
            Route = route;
        }
    }

    private readonly DirectMessagesStore store;
    private readonly ContactBook contacts;
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly List<Person> people = new(Capacity);
    private readonly List<ConversationDto> direct = new(Capacity);
    private readonly HashSet<string> added = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConversationDto> directByUser = new(StringComparer.Ordinal);
    private readonly WidgetOption[] options;
    private ConversationDto[]? seenConversations;
    private int seenContactsVersion = -1;
    private int seenFavorites = -1;
    private int seenPinned = -1;
    private WidgetRefresh check;
    private long rebuiltAt;

    public PeopleWidget(DirectMessagesStore store, ContactBook contacts, Configuration configuration,
        AethernetSession session, RemoteImageCache images)
    {
        this.store = store;
        this.contacts = contacts;
        this.configuration = configuration;
        this.session = session;
        this.images = images;
        options = new[] { new WidgetOption(PersonKey, L.WidgetsPeople.PersonOption, FillChoices) };
    }

    public string Id => "message.people";
    public string DisplayName => Loc.T(L.WidgetsPeople.People);
    public string Description => Loc.T(L.WidgetsPeople.PeopleDescription);
    public string AppId => MessageAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
    public IReadOnlyList<WidgetOption> Options => options;

    public float Relevance(string config)
    {
        Advance(false);
        var count = Math.Min(people.Count, LargeCount);
        for (var index = 0; index < count; index++)
        {
            if (people[index].UnreadLabel.Length > 0 && !people[index].Muted)
            {
                return 0.6f;
            }
        }

        return 0f;
    }

    public WidgetRoute Target(in WidgetContext context)
    {
        if (context.Size != WidgetSize.Small || !session.IsSignedIn)
        {
            return WidgetRoute.App(MessageAppId);
        }

        var index = Selected(context.Config);
        return index >= 0 ? people[index].Route : WidgetRoute.App(MessageAppId);
    }

    public void Draw(in WidgetContext context)
    {
        Advance(context.Preview);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        var signedIn = session.IsSignedIn;
        var sample = context.Preview && (!signedIn || people.Count == 0);
        if (!signedIn && !sample)
        {
            WidgetChrome.Message(context, ink, content, MessageAppId, Loc.T(L.WidgetsPeople.SignInHint),
                string.Empty);
            return;
        }

        if (!sample && people.Count == 0)
        {
            if (!store.ConversationsLoaded)
            {
                DrawPlaceholder(context, ink, content);
                return;
            }

            WidgetChrome.Message(context, ink, content, MessageAppId, Loc.T(L.WidgetsPeople.NoPeople),
                context.Size == WidgetSize.Small ? string.Empty : Loc.T(L.WidgetsPeople.NoPeopleHint));
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, content, sample);
            return;
        }

        var gridTop = content.Min.Y;
        var rows = 1;
        if (context.Size == WidgetSize.Large)
        {
            gridTop = WidgetChrome.Header(context, ink, MessageAppId, Loc.T(L.WidgetsPeople.People),
                AppAccents.For(MessageAppId)) + WidgetMetrics.Gutter * context.Scale;
            rows = LargeCount / Columns;
        }

        DrawGrid(context, ink, new Rect(new Vector2(content.Min.X, gridTop), content.Max), rows, sample);
    }

    private void Advance(bool preview)
    {
        if (!check.Due(RefreshMilliseconds))
        {
            return;
        }

        if (!session.IsSignedIn)
        {
            if (people.Count > 0)
            {
                people.Clear();
            }

            return;
        }

        if (!preview)
        {
            contacts.Refresh();
        }

        var conversations = store.Conversations;
        var changed = !ReferenceEquals(conversations, seenConversations)
                      || contacts.Version != seenContactsVersion
                      || configuration.MessageFavoriteContacts.Count != seenFavorites
                      || configuration.MessagePinnedChats.Count != seenPinned
                      || Environment.TickCount64 - rebuiltAt >= ForcedRefreshMilliseconds;
        if (changed)
        {
            Rebuild(conversations);
        }
    }

    private void Rebuild(ConversationDto[] conversations)
    {
        rebuiltAt = Environment.TickCount64;
        seenConversations = conversations;
        seenContactsVersion = contacts.Version;
        seenFavorites = configuration.MessageFavoriteContacts.Count;
        seenPinned = configuration.MessagePinnedChats.Count;
        people.Clear();
        added.Clear();
        directByUser.Clear();
        direct.Clear();
        for (var index = 0; index < conversations.Length; index++)
        {
            var conversation = conversations[index];
            if (conversation.IsGroup || conversation.OtherUserId.Length == 0)
            {
                continue;
            }

            directByUser.TryAdd(conversation.OtherUserId, conversation);
            if (!configuration.MessageArchivedChats.Contains(conversation.Id))
            {
                direct.Add(conversation);
            }
        }

        var favorites = configuration.MessageFavoriteContacts;
        for (var index = 0; index < favorites.Count; index++)
        {
            Add(favorites[index]);
        }

        var pinned = configuration.MessagePinnedChats;
        for (var index = 0; index < pinned.Count; index++)
        {
            for (var conversationIndex = 0; conversationIndex < direct.Count; conversationIndex++)
            {
                if (string.Equals(direct[conversationIndex].Id, pinned[index], StringComparison.Ordinal))
                {
                    Add(direct[conversationIndex].OtherUserId);
                    break;
                }
            }
        }

        direct.Sort(static (left, right) => right.LastMessageAtUnix.CompareTo(left.LastMessageAtUnix));
        for (var index = 0; index < direct.Count; index++)
        {
            Add(direct[index].OtherUserId);
        }

        var book = contacts.Contacts;
        for (var index = 0; index < book.Length; index++)
        {
            if (book[index].IsMutual)
            {
                Add(book[index].UserId);
            }
        }
    }

    private void Add(string userId)
    {
        if (people.Count >= Capacity || userId.Length == 0 || !added.Add(userId))
        {
            return;
        }

        var contact = contacts.Find(userId);
        directByUser.TryGetValue(userId, out var conversation);
        if (contact is null && conversation is null)
        {
            added.Remove(userId);
            return;
        }

        var name = contact is not null ? ContactBook.DisplayLabel(contact) : store.DisplayTitle(conversation!);
        var avatarUrl = !string.IsNullOrEmpty(contact?.AvatarUrl) ? contact.AvatarUrl : conversation?.OtherAvatarUrl;
        var unread = conversation is null ? string.Empty : PeopleWidgetChrome.CountLabel(conversation.UnreadCount);
        var route = conversation is not null
            ? WidgetRoute.To(MessageAppId, WidgetRouteKind.Conversation, conversation.Id)
            : WidgetRoute.To(MessageAppId, WidgetRouteKind.DirectUser, userId);
        people.Add(new Person(userId, name, avatarUrl, unread, conversation?.Muted ?? false, route));
    }

    private void FillChoices(List<WidgetChoice> target)
    {
        if (session.IsSignedIn)
        {
            Rebuild(store.Conversations);
        }

        for (var index = 0; index < people.Count; index++)
        {
            target.Add(new WidgetChoice(people[index].UserId, people[index].Name));
        }
    }

    private int Selected(string config)
    {
        if (people.Count == 0)
        {
            return -1;
        }

        var userId = WidgetConfig.Get(config, PersonKey);
        if (userId.Length == 0)
        {
            return 0;
        }

        for (var index = 0; index < people.Count; index++)
        {
            if (string.Equals(people[index].UserId, userId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Rect content, bool sample)
    {
        var scale = context.Scale;
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var gap = NameGap * scale;
        var radius = MathF.Min(content.Width, content.Height - nameHeight - gap) * 0.5f;
        var center = new Vector2(content.Center.X, content.Min.Y + radius);
        if (sample)
        {
            DrawPerson(context, ink, center, radius, WidgetSamples.Names[0], null,
                PeopleWidgetChrome.CountLabel(SampleUnread), false);
            DrawName(context, ink, WidgetSamples.Names[0], content.Center.X, center.Y + radius + gap, content.Width,
                WidgetType.Headline);
            return;
        }

        var person = people[Math.Max(0, Selected(context.Config))];
        DrawPerson(context, ink, center, radius, person.Name, person.AvatarUrl, person.UnreadLabel, person.Muted);
        DrawName(context, ink, person.Name, content.Center.X, center.Y + radius + gap, content.Width,
            WidgetType.Headline);
    }

    private void DrawGrid(in WidgetContext context, in WidgetInk ink, Rect area, int rows, bool sample)
    {
        var scale = context.Scale;
        var cellWidth = area.Width / Columns;
        var cellHeight = area.Height / rows;
        var style = context.Size == WidgetSize.Large ? WidgetType.Headline : WidgetType.Caption;
        var nameHeight = Typography.Measure("A", style).Y;
        var gap = NameGap * scale;
        var inset = CellInset * scale;
        var radius = MathF.Max(1f, MathF.Min(cellWidth * 0.5f - inset, (cellHeight - nameHeight - gap - inset * 2f) * 0.5f));
        var count = sample ? Math.Min(WidgetSamples.Names.Length, rows * Columns) : Math.Min(people.Count, rows * Columns);
        for (var index = 0; index < count; index++)
        {
            var column = index % Columns;
            var row = index / Columns;
            var cell = new Rect(new Vector2(area.Min.X + column * cellWidth, area.Min.Y + row * cellHeight),
                new Vector2(area.Min.X + (column + 1) * cellWidth, area.Min.Y + (row + 1) * cellHeight));
            var blockHeight = radius * 2f + gap + nameHeight;
            var top = cell.Center.Y - blockHeight * 0.5f;
            var center = new Vector2(cell.Center.X, top + radius);
            if (sample)
            {
                DrawPerson(context, ink, center, radius, WidgetSamples.Names[index], null,
                    index == 0 ? PeopleWidgetChrome.CountLabel(SampleUnread) : string.Empty, false);
                DrawName(context, ink, SampleFirstNames[index], cell.Center.X, top + radius * 2f + gap,
                    cellWidth - inset, style);
                continue;
            }

            var person = people[index];
            WidgetControls.Link(context, ink, index + 1, cell, person.Route);
            DrawPerson(context, ink, center, radius, person.Name, person.AvatarUrl, person.UnreadLabel, person.Muted);
            DrawName(context, ink, person.Name, cell.Center.X, top + radius * 2f + gap, cellWidth - inset, style);
        }
    }

    private void DrawPerson(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius, string name,
        string? avatarUrl, string unreadLabel, bool muted)
    {
        var texture = string.IsNullOrEmpty(avatarUrl) ? null : images.Avatar(avatarUrl, radius * 2f).Texture;
        PeopleWidgetChrome.Avatar(context, ink, center, radius, name, texture, MonogramGray);
        if (unreadLabel.Length == 0)
        {
            return;
        }

        var badgeRight = center.X + radius * BadgeLift + PeopleWidgetChrome.BadgeHeight * 0.5f * context.Scale;
        PeopleWidgetChrome.Badge(context, ink, badgeRight, center.Y - radius * BadgeLift, unreadLabel, muted);
    }

    private static void DrawName(in WidgetContext context, in WidgetInk ink, string name, float centerX, float top,
        float width, in TextStyle style)
    {
        var fitted = Typography.FitText(name, MathF.Max(1f, width), style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(context.DrawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink.Primary, style);
    }

    private static string[] FirstNames()
    {
        var names = WidgetSamples.Names;
        var first = new string[names.Length];
        for (var index = 0; index < names.Length; index++)
        {
            var space = names[index].IndexOf(' ');
            first[index] = space > 0 ? names[index][..space] : names[index];
        }

        return first;
    }

    private static void DrawPlaceholder(in WidgetContext context, in WidgetInk ink, Rect content)
    {
        var count = context.Size == WidgetSize.Small ? 1 : MediumCount;
        var cellWidth = content.Width / count;
        var radius = MathF.Min(cellWidth * 0.5f - CellInset * context.Scale, content.Height * 0.3f);
        for (var index = 0; index < count; index++)
        {
            var center = new Vector2(content.Min.X + cellWidth * (index + 0.5f), content.Center.Y);
            WidgetChrome.Redacted(context.DrawList,
                new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)), ink);
        }
    }

    public void Dispose()
    {
    }
}
