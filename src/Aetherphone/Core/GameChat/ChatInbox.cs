using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.GameChat;

internal sealed class InboxRow
{
    public required string Key { get; init; }
    public ChatTab? Tab { get; set; }
    public string StreamKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string World { get; set; } = string.Empty;
    public string PreviewSender { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public string PreviewChannel { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public int Unread { get; set; }
    public Vector4 Tint { get; set; }
    public bool Pinned { get; set; }
    public bool Muted { get; set; }
    public ChatDensity TellDensity { get; set; } = ChatDensity.Bubbles;

    public bool IsTell => Tab is null;

    public ChatDensity Density => Tab?.Density ?? TellDensity;

    public bool HasBadge => Unread > 0 && !Muted;
}

internal sealed class ChatInbox : IDisposable
{
    private static readonly Comparison<InboxRow> ByActivity = static (left, right) =>
        right.LastActivity.CompareTo(left.LastActivity);

    private readonly ChatLog log;
    private readonly TabStore tabs;
    private readonly TellPreferences tellPreferences;
    private readonly Configuration configuration;
    private readonly ChannelStyleStore styles;
    private readonly List<InboxRow> rows = new(16);
    private readonly List<InboxRow> pinned = new(6);
    private readonly List<string> streamScratch = new(32);
    private readonly HashSet<string> attended = new(StringComparer.Ordinal);
    private InboxRow? transient;
    private long expectedRevision = -1;
    private bool stale = true;
    private bool seenDirty;

    public ChatInbox(ChatLog log, TabStore tabs, TellPreferences tellPreferences, Configuration configuration)
    {
        this.log = log;
        this.tabs = tabs;
        this.tellPreferences = tellPreferences;
        this.configuration = configuration;
        styles = ChannelStyles.Bind(configuration);
        log.Appended += OnAppended;
        tabs.Changed += Invalidate;
        tellPreferences.Changed += Invalidate;
    }

    public IReadOnlyList<InboxRow> Rows => rows;

    public IReadOnlyList<InboxRow> Pinned => pinned;

    public int TotalUnread { get; private set; }

    public string Viewing { get; set; } = string.Empty;

    public int Count => rows.Count + pinned.Count;

    public void Invalidate()
    {
        styles.Invalidate();
        stale = true;
    }

    public bool IsViewing(string key) =>
        string.Equals(Viewing, key, StringComparison.Ordinal) || attended.Contains(key);

    public void SetAttended(string key, bool attending)
    {
        if (attending)
        {
            attended.Add(key);
            return;
        }

        attended.Remove(key);
    }

    public InboxRow EnsureTell(string display, string world)
    {
        var target = world.Length > 0 ? string.Concat(display, "@", world) : display;
        var key = ChatStreams.ForTell(target);
        if (Find(key) is { } existing)
        {
            return existing;
        }

        transient = new InboxRow
        {
            Key = key,
            StreamKey = key,
            Title = display,
            World = world,
            Tint = ChannelTints.Tell,
            Pinned = tellPreferences.IsPinned(key),
            Muted = tellPreferences.IsMuted(key),
            TellDensity = tellPreferences.Layout(key),
        };
        rows.Insert(0, transient);
        return transient;
    }

    public void ClearTransient()
    {
        if (transient is null)
        {
            return;
        }

        transient = null;
        stale = true;
    }

    public void Sync()
    {
        if (!stale && expectedRevision == log.Revision)
        {
            return;
        }

        Rebuild();
    }

    public InboxRow? Find(string key)
    {
        for (var index = 0; index < pinned.Count; index++)
        {
            if (string.Equals(pinned[index].Key, key, StringComparison.Ordinal))
            {
                return pinned[index];
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Key, key, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return null;
    }

    public InboxRow? MostRecent()
    {
        InboxRow? best = null;
        for (var index = 0; index < pinned.Count; index++)
        {
            best = Newer(best, pinned[index]);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            best = Newer(best, rows[index]);
        }

        return best;
    }

    public void MarkRead(InboxRow row)
    {
        Stamp(row.Key);
        if (row.Unread == 0)
        {
            return;
        }

        if (!row.Muted)
        {
            TotalUnread -= row.Unread;
        }

        row.Unread = 0;
        if (TotalUnread < 0)
        {
            TotalUnread = 0;
        }
    }

    public void MarkAllRead()
    {
        for (var index = 0; index < pinned.Count; index++)
        {
            MarkRead(pinned[index]);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            MarkRead(rows[index]);
        }

        TotalUnread = 0;
    }

    public bool TogglePinned(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            var pinnedNow = tabs.TogglePin(tab);
            stale = true;
            return pinnedNow;
        }

        tellPreferences.TogglePinned(row.Key);
        stale = true;
        return true;
    }

    public void ToggleMuted(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            tab.Alerts = tab.Alerts == AlertPolicy.Off ? AlertPolicy.Mentions : AlertPolicy.Off;
            tabs.Update(tab);
        }
        else
        {
            tellPreferences.ToggleMuted(row.Key);
        }

        stale = true;
    }

    public void SetDensity(InboxRow row, ChatDensity density)
    {
        if (row.Density == density)
        {
            return;
        }

        if (row.Tab is { } tab)
        {
            tab.Density = density;
            tabs.Update(tab);
        }
        else
        {
            tellPreferences.SetLayout(row.Key, density);
        }

        stale = true;
    }

    public void FlushSeen()
    {
        if (!seenDirty)
        {
            return;
        }

        seenDirty = false;
        configuration.Save();
    }

    public void Dispose()
    {
        log.Appended -= OnAppended;
        tabs.Changed -= Invalidate;
        tellPreferences.Changed -= Invalidate;
    }

    public static string KeyForTab(ChatTab tab) => string.Concat("tab:", tab.Id);

    private static InboxRow? Newer(InboxRow? best, InboxRow candidate) =>
        best is null || candidate.LastActivity > best.LastActivity ? candidate : best;

    private void OnAppended(ChatEntry entry)
    {
        expectedRevision = log.Revision;
        if (stale)
        {
            return;
        }

        if (ChatStreams.IsTell(entry.StreamKey))
        {
            var row = Find(entry.StreamKey);
            if (row is null)
            {
                stale = true;
                return;
            }

            Touch(row, entry, CountsForTell(entry));
            Resort();
            return;
        }

        var touched = false;
        for (var index = 0; index < rows.Count; index++)
        {
            touched |= TouchTab(rows[index], entry);
        }

        for (var index = 0; index < pinned.Count; index++)
        {
            touched |= TouchTab(pinned[index], entry);
        }

        if (touched)
        {
            Resort();
        }
    }

    private bool TouchTab(InboxRow row, ChatEntry entry)
    {
        if (row.Tab is not { } tab || !tab.Includes(entry.ChannelKey))
        {
            return false;
        }

        Touch(row, entry, Counts(tab, entry));
        return true;
    }

    private void Touch(InboxRow row, ChatEntry entry, bool counts)
    {
        row.LastActivity = entry.At;
        row.PreviewSender = entry.IsSelf ? string.Empty : entry.AuthorName;
        row.PreviewText = entry.Text;
        row.PreviewChannel = row.Tab is { Channels.Count: > 1 } ? entry.ChannelKey : string.Empty;
        if (IsViewing(row.Key))
        {
            Stamp(row.Key);
            return;
        }

        if (!counts)
        {
            return;
        }

        row.Unread++;
        if (!row.Muted)
        {
            TotalUnread++;
        }
    }

    private void Rebuild()
    {
        rows.Clear();
        pinned.Clear();
        TotalUnread = 0;
        var all = tabs.Tabs;
        for (var index = 0; index < all.Count; index++)
        {
            Place(BuildTab(all[index]));
        }

        log.CollectStreams(streamScratch);
        for (var index = 0; index < streamScratch.Count; index++)
        {
            var streamKey = streamScratch[index];
            if (ChatStreams.IsTell(streamKey))
            {
                Place(BuildTell(streamKey));
            }
        }

        if (transient is not null)
        {
            if (log.HasLines(transient.Key))
            {
                transient = null;
            }
            else
            {
                transient.Pinned = tellPreferences.IsPinned(transient.Key);
                transient.Muted = tellPreferences.IsMuted(transient.Key);
                transient.TellDensity = tellPreferences.Layout(transient.Key);
                Place(transient);
            }
        }

        Resort();
        expectedRevision = log.Revision;
        stale = false;
    }

    private void Place(InboxRow row)
    {
        if (row.Pinned)
        {
            pinned.Add(row);
        }
        else
        {
            rows.Add(row);
        }
    }

    private InboxRow BuildTab(ChatTab tab)
    {
        var palette = ChannelTints.TabPalette;
        var row = new InboxRow
        {
            Key = KeyForTab(tab),
            Tab = tab,
            Title = tab.Name,
            Tint = palette[Math.Clamp(tab.Tint, 0, palette.Length - 1)],
            Pinned = tab.Pinned,
            Muted = tab.Alerts == AlertPolicy.Off,
        };
        var watermark = Watermark(row.Key);
        var multi = tab.Channels.Count > 1;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            var lines = log.Lines(tab.Channels[index]);
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var entry = lines[lineIndex];
                if (entry.At > row.LastActivity)
                {
                    row.LastActivity = entry.At;
                    row.PreviewSender = entry.IsSelf ? string.Empty : entry.AuthorName;
                    row.PreviewText = entry.Text;
                    row.PreviewChannel = multi ? entry.ChannelKey : string.Empty;
                }

                if (UnixMilliseconds(entry.At) > watermark && Counts(tab, entry))
                {
                    row.Unread++;
                }
            }
        }

        if (!row.Muted)
        {
            TotalUnread += row.Unread;
        }

        return row;
    }

    private InboxRow BuildTell(string streamKey)
    {
        var lines = log.Lines(streamKey);
        var row = new InboxRow
        {
            Key = streamKey,
            StreamKey = streamKey,
            Tint = ChannelTints.Tell,
            Pinned = tellPreferences.IsPinned(streamKey),
            Muted = tellPreferences.IsMuted(streamKey),
            TellDensity = tellPreferences.Layout(streamKey),
        };
        var watermark = Watermark(streamKey);
        for (var index = 0; index < lines.Count; index++)
        {
            var entry = lines[index];
            row.Title = entry.AuthorName;
            row.World = entry.AuthorWorld;
            if (entry.At > row.LastActivity)
            {
                row.LastActivity = entry.At;
                row.PreviewSender = string.Empty;
                row.PreviewText = entry.Text;
            }

            if (CountsForTell(entry) && UnixMilliseconds(entry.At) > watermark)
            {
                row.Unread++;
            }
        }

        if (!row.Muted)
        {
            TotalUnread += row.Unread;
        }

        return row;
    }

    private void Resort()
    {
        rows.Sort(ByActivity);
        pinned.Sort(ByActivity);
    }

    private long Watermark(string key) =>
        configuration.LinkpearlSeen.TryGetValue(key, out var value) ? value : 0L;

    private void Stamp(string key)
    {
        configuration.LinkpearlSeen[key] = Now();
        seenDirty = true;
    }

    private bool Counts(ChatTab tab, ChatEntry entry)
    {
        if (entry.IsSelf || tab.IsMuted(entry.ChannelKey) || styles.NeverUnread(entry.ChannelKey))
        {
            return false;
        }

        return tab.Alerts != AlertPolicy.Mentions || entry.IsMention;
    }

    private bool CountsForTell(ChatEntry entry) => !entry.IsSelf && !styles.NeverUnread(entry.ChannelKey);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long UnixMilliseconds(DateTime at) => new DateTimeOffset(at).ToUnixTimeMilliseconds();
}
