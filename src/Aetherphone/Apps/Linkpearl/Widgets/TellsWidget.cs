using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Linkpearl.Widgets;

internal sealed class TellsWidget : IHomeWidget
{
    private const string LinkpearlAppId = "messages";
    private const string SenderSeparator = ": ";
    private const string TagSeparator = " · ";
    private const long RefreshMilliseconds = 1000;
    private const float PortraitPixels = 64f;
    private const float ForcedRefreshSeconds = 30f;
    private const int MediumRows = 3;
    private const int LargeRows = 7;

    private static readonly int[] SampleMinutesAgo = { 1, 18, 75 };
    private static readonly int[] SampleUnread = { 1, 0, 0 };
    private static readonly LocString[] SampleMessages =
    {
        L.WidgetsPeople.SampleMessage,
        L.WidgetsPeople.SampleMessageRaid,
        L.WidgetsPeople.SampleMessageThanks,
    };

    private static readonly Comparison<InboxRow> ByActivity = static (left, right) =>
        right.LastActivity.CompareTo(left.LastActivity);

    private readonly struct TellRow
    {
        public readonly string Title;
        public readonly string World;
        public readonly bool IsTell;
        public readonly string Glyph;
        public readonly Vector4 Tint;
        public readonly string Preview;
        public readonly string Time;
        public readonly string UnreadLabel;
        public readonly bool Muted;
        public readonly WidgetRoute Route;

        public TellRow(string title, string world, bool isTell, string glyph, Vector4 tint, string preview,
            string time, string unreadLabel, bool muted, WidgetRoute route)
        {
            Title = title;
            World = world;
            IsTell = isTell;
            Glyph = glyph;
            Tint = tint;
            Preview = preview;
            Time = time;
            UnreadLabel = unreadLabel;
            Muted = muted;
            Route = route;
        }
    }

    private readonly ChatInbox inbox;
    private readonly ChatLog log;
    private readonly LodestoneService lodestone;
    private readonly List<InboxRow> scratch = new(32);
    private readonly TellRow[] rows = new TellRow[LargeRows];
    private readonly CachedText[] sampleTimes = new CachedText[3];
    private int rowCount;
    private int unreadTotal;
    private long seenRevision = -1;
    private readonly IDalamudTextureWrap?[] portraits = new IDalamudTextureWrap?[LargeRows];
    private bool seenMask;
    private long nextCheckTicks;
    private float sinceRebuild;
    private WidgetFrame frame;

    public TellsWidget(ChatInbox inbox, ChatLog log, LodestoneService lodestone)
    {
        this.inbox = inbox;
        this.log = log;
        this.lodestone = lodestone;
    }

    public string Id => "messages.tells";
    public string DisplayName => Loc.T(L.Apps.Linkpearl);
    public string Description => Loc.T(L.WidgetsPeople.TellsDescription);
    public string AppId => LinkpearlAppId;
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        inbox.Sync();
        return inbox.TotalUnread > 0 ? 0.7f : 0f;
    }

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
        var headerBottom = WidgetChrome.Header(context, ink, LinkpearlAppId, L.Apps.Linkpearl,
            AppAccents.For(LinkpearlAppId));
        if (!sample && unreadTotal > 0)
        {
            PeopleWidgetChrome.Badge(context, ink, content.Max.X, content.Min.Y + (headerBottom - content.Min.Y) * 0.5f,
                PeopleWidgetChrome.CountLabel(unreadTotal), false);
        }

        var area = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * 0.5f * context.Scale),
            content.Max);
        var maximum = context.Size == WidgetSize.Large ? LargeRows : MediumRows;
        var slots = PeopleWidgetChrome.RowCount(area.Height, context.Scale, maximum);
        if (sample)
        {
            DrawSamples(context, ink, area, Math.Min(slots, SampleMessages.Length));
            return;
        }

        if (rowCount == 0)
        {
            WidgetChrome.Message(context, ink, area, LinkpearlAppId, Loc.T(L.WidgetsPeople.NoTells),
                Loc.T(L.WidgetsPeople.NoTellsHint));
            return;
        }

        var rowHeight = area.Height / slots;
        var count = Math.Min(slots, rowCount);
        for (var index = 0; index < count; index++)
        {
            DrawRow(context, ink, area, index, rowHeight, rows[index], portraits[index], index < count - 1);
        }
    }

    private void Advance(float delta)
    {
        sinceRebuild += delta;
        var now = Environment.TickCount64;
        if (now < nextCheckTicks)
        {
            return;
        }

        nextCheckTicks = now + RefreshMilliseconds;
        inbox.Sync();
        var unreadChanged = inbox.TotalUnread != unreadTotal;
        unreadTotal = inbox.TotalUnread;
        if (unreadChanged || log.Revision != seenRevision || NameMask.Enabled != seenMask ||
            sinceRebuild >= ForcedRefreshSeconds)
        {
            Rebuild();
        }

        RefreshPortraits();
    }

    private void RefreshPortraits()
    {
        for (var index = 0; index < rowCount; index++)
        {
            var row = rows[index];
            portraits[index] = row.IsTell && !NameMask.Enabled && row.World.Length > 0
                ? lodestone.Avatar(row.Title, row.World, PortraitPixels).Texture
                : null;
        }
    }

    private void Rebuild()
    {
        sinceRebuild = 0f;
        seenRevision = log.Revision;
        seenMask = NameMask.Enabled;
        scratch.Clear();
        var pinned = inbox.Pinned;
        for (var index = 0; index < pinned.Count; index++)
        {
            scratch.Add(pinned[index]);
        }

        var regular = inbox.Rows;
        for (var index = 0; index < regular.Count; index++)
        {
            scratch.Add(regular[index]);
        }

        scratch.Sort(ByActivity);
        rowCount = 0;
        for (var index = 0; index < scratch.Count && rowCount < LargeRows; index++)
        {
            var row = scratch[index];
            if (row.LastActivity == default)
            {
                continue;
            }

            var glyph = row.Tab is { } tab ? GameChatTiles.GlyphFor(tab) : string.Empty;
            rows[rowCount++] = new TellRow(row.Title, row.World, row.IsTell, glyph, row.Tint, PreviewOf(row),
                TimeText.Short(row.LastActivity), PeopleWidgetChrome.CountLabel(row.Unread), row.Muted,
                WidgetRoute.To(LinkpearlAppId, WidgetRouteKind.Linkpearl, row.Key));
        }
    }

    private static string PreviewOf(InboxRow row)
    {
        var tag = string.Empty;
        if (row.PreviewChannel.Length > 0 && GameChannels.TryByKey(row.PreviewChannel, out var channel))
        {
            tag = LinkshellNames.Label(channel);
        }

        var sender = string.Empty;
        if (row.PreviewSender.Length > 0)
        {
            sender = NameMask.Enabled ? NameMask.Of(row.PreviewSender) : FirstName(row.PreviewSender);
        }

        var text = row.PreviewText;
        var body = sender.Length > 0 ? string.Concat(sender, SenderSeparator, text) : text;
        return tag.Length > 0 ? string.Concat(tag, TagSeparator, body) : body;
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }

    private static void DrawRow(in WidgetContext context, in WidgetInk ink, Rect area, int index, float rowHeight,
        in TellRow row, IDalamudTextureWrap? portrait, bool separator)
    {
        var scale = context.Scale;
        var rowRect = new Rect(new Vector2(area.Min.X, area.Min.Y + index * rowHeight),
            new Vector2(area.Max.X, area.Min.Y + (index + 1) * rowHeight));
        WidgetControls.Link(context, ink, index + 1, rowRect, row.Route);
        var radius = PeopleWidgetChrome.RowAvatarRadius * scale;
        var center = new Vector2(area.Min.X + radius, rowRect.Center.Y);
        var title = row.IsTell ? NameMask.Display(row.Title) : row.Title;
        if (!row.IsTell)
        {
            PeopleWidgetChrome.Tile(context, ink, center, radius, row.Tint, row.Glyph);
        }
        else
        {
            PeopleWidgetChrome.Avatar(context, ink, center, radius, title, portrait, row.Tint);
        }

        var textLeft = center.X + radius + PeopleWidgetChrome.RowTextGap * scale;
        PeopleWidgetChrome.Row(context, ink, rowRect, textLeft, title, row.Preview, row.Time, row.UnreadLabel,
            row.Muted, row.UnreadLabel.Length > 0);
        if (separator)
        {
            WidgetChrome.Separator(context, ink, textLeft, area.Max.X, rowRect.Max.Y);
        }
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
            var row = new TellRow(WidgetSamples.Names[index + 1], string.Empty, true, string.Empty,
                ChannelTints.Tell, Loc.T(SampleMessages[index]), time,
                PeopleWidgetChrome.CountLabel(SampleUnread[index]), false, default);
            DrawRow(context, ink, area, index, rowHeight, row, null, index < count - 1);
        }
    }

    public void Dispose()
    {
    }
}
