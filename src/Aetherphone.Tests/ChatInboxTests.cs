using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ChatInboxTests
{
    private static ChatEntry Entry(ChatLog log, string channelKey, string name, DateTime at,
        ChatEntryFlags flags = ChatEntryFlags.None) =>
        new(log.NextSequence(), channelKey, name, "Siren", "hello", new[] { ChatChunk.Plain("hello") }, at, flags);

    private static ChatTab Tab(string name, params string[] channels) => new()
    {
        Id = name,
        Name = name,
        Channels = new List<string>(channels),
        Alerts = AlertPolicy.All,
    };

    private static (ChatLog Log, ChatInbox Inbox, Configuration Configuration) Build(params ChatTab[] tabs)
    {
        var configuration = new Configuration();
        configuration.LinkpearlTabs.AddRange(tabs);
        var log = new ChatLog();
        var store = new TabStoreStub(configuration);
        return (log, new ChatInbox(log, store.Store, new TellPreferences(configuration), configuration), configuration);
    }

    [Fact]
    public void MutedTellsKeepTheirUnreadCountOutOfTheBadge()
    {
        var (log, inbox, configuration) = Build();
        using var scope = inbox;
        var tell = new ChatEntry(log.NextSequence(), GameChannels.TellKey, "Rin", "Siren", "psst",
            new[] { ChatChunk.Plain("psst") }, DateTime.Now, ChatEntryFlags.None);
        configuration.LinkpearlMutedTells.Add(tell.StreamKey);
        var muted = new ChatInbox(log, new TabStoreStub(configuration).Store, new TellPreferences(configuration),
            configuration);
        using var mutedScope = muted;
        log.Append(tell);
        muted.Sync();

        var row = muted.Find(tell.StreamKey);
        Assert.NotNull(row);
        Assert.True(row!.Muted);
        Assert.Equal(1, row.Unread);
        Assert.False(row.HasBadge);
        Assert.Equal(0, muted.TotalUnread);
    }

    [Fact]
    public void PinnedTellsLiveInThePinnedList()
    {
        var (log, inbox, configuration) = Build();
        using var scope = inbox;
        var tell = new ChatEntry(log.NextSequence(), GameChannels.TellKey, "Rin", "Siren", "hey",
            new[] { ChatChunk.Plain("hey") }, DateTime.Now, ChatEntryFlags.None);
        log.Append(tell);
        inbox.Sync();
        Assert.NotNull(inbox.Find(tell.StreamKey));
        Assert.Empty(inbox.Pinned);
        Assert.Single(inbox.Rows);

        configuration.LinkpearlPinnedTells.Add(tell.StreamKey);
        var pinned = new ChatInbox(log, new TabStoreStub(configuration).Store, new TellPreferences(configuration),
            configuration);
        using var pinnedScope = pinned;
        pinned.Sync();
        Assert.Single(pinned.Pinned);
        Assert.Empty(pinned.Rows);
        Assert.True(pinned.Find(tell.StreamKey)!.Pinned);
        Assert.Equal(1, pinned.TotalUnread);
    }

    [Fact]
    public void AttendedConversationsNeverAccumulateUnread()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        inbox.Sync();
        inbox.SetAttended("tab:FC", true);
        log.Append(Entry(log, "fc", "Rin", DateTime.Now));
        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(0, inbox.TotalUnread);

        inbox.SetAttended("tab:FC", false);
        log.Append(Entry(log, "fc", "Mira", DateTime.Now.AddSeconds(1)));
        Assert.Equal(1, inbox.Find("tab:FC")!.Unread);

        inbox.MarkAllRead();
        Assert.Equal(0, inbox.TotalUnread);
        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
    }

    [Fact]
    public void TabRowsCountIncomingLinesAsUnread()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", now));
        log.Append(Entry(log, "fc", "Mira", now.AddSeconds(1)));
        inbox.Sync();

        var row = inbox.Find("tab:FC");
        Assert.NotNull(row);
        Assert.Equal(2, row!.Unread);
        Assert.Equal(2, inbox.TotalUnread);
        Assert.Equal("Mira", row.PreviewSender);
    }

    [Fact]
    public void OwnLinesNeverCount()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Me", DateTime.Now, ChatEntryFlags.Self));
        inbox.Sync();

        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(0, inbox.TotalUnread);
    }

    [Fact]
    public void MentionsOnlyTabsCountOnlyMentions()
    {
        var tab = Tab("FC", "fc");
        tab.Alerts = AlertPolicy.Mentions;
        var (log, inbox, _) = Build(tab);
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", now));
        log.Append(Entry(log, "fc", "Mira", now.AddSeconds(1), ChatEntryFlags.Mention));
        inbox.Sync();

        Assert.Equal(1, inbox.Find("tab:FC")!.Unread);
    }

    [Fact]
    public void SilencedTabsAndMutedChannelsDoNotCount()
    {
        var silent = Tab("Local", "say");
        silent.Alerts = AlertPolicy.Off;
        var partly = Tab("Group", "party", "alliance");
        partly.MutedChannels.Add("alliance");
        var (log, inbox, _) = Build(silent, partly);
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "say", "Stranger", now));
        log.Append(Entry(log, "alliance", "Someone", now.AddSeconds(1)));
        log.Append(Entry(log, "party", "Nala", now.AddSeconds(2)));
        inbox.Sync();

        var silenced = inbox.Find("tab:Local")!;
        Assert.True(silenced.Muted);
        Assert.Equal(1, silenced.Unread);
        Assert.False(silenced.HasBadge);
        Assert.Equal(1, inbox.Find("tab:Group")!.Unread);
        Assert.Equal(1, inbox.TotalUnread);
    }

    [Fact]
    public void NeverUnreadChannelsAreInvisibleToTheBadge()
    {
        var tab = Tab("Group", "party", "alliance");
        var (log, inbox, configuration) = Build(tab);
        using var scope = inbox;
        configuration.LinkpearlChannelStyles["alliance"] = new ChannelStyle { NeverUnread = true };
        var now = DateTime.Now;
        log.Append(Entry(log, "alliance", "Someone", now));
        log.Append(Entry(log, "party", "Nala", now.AddSeconds(1)));
        inbox.Sync();

        var row = inbox.Find("tab:Group")!;
        Assert.Equal(1, row.Unread);
        Assert.Equal(1, inbox.TotalUnread);
        Assert.Equal("Nala", row.PreviewSender);
    }

    [Fact]
    public void NeverUnreadAppliesToLiveLinesAndToTells()
    {
        var (log, inbox, configuration) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        configuration.LinkpearlChannelStyles["fc"] = new ChannelStyle { NeverUnread = true };
        configuration.LinkpearlChannelStyles[GameChannels.TellKey] = new ChannelStyle { NeverUnread = true };
        inbox.Sync();

        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", now));
        log.Append(Entry(log, GameChannels.TellKey, "Aria", now.AddSeconds(1)));
        inbox.Sync();
        log.Append(Entry(log, GameChannels.TellKey, "Aria", now.AddSeconds(2)));

        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(0, inbox.Rows[0].Unread);
        Assert.Equal(0, inbox.TotalUnread);
    }

    [Fact]
    public void ANeverUnreadChannelStillCountsWhenTheRuleIsLifted()
    {
        var (log, inbox, configuration) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        configuration.LinkpearlChannelStyles["fc"] = new ChannelStyle { NeverUnread = true };
        log.Append(Entry(log, "fc", "Rin", DateTime.Now));
        inbox.Sync();
        Assert.Equal(0, inbox.TotalUnread);

        configuration.LinkpearlChannelStyles.Remove("fc");
        inbox.Invalidate();
        inbox.Sync();

        Assert.Equal(1, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(1, inbox.TotalUnread);
    }

    [Fact]
    public void TellsBecomeTheirOwnRows()
    {
        var (log, inbox, _) = Build();
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, GameChannels.TellKey, "Aria", now));
        log.Append(Entry(log, GameChannels.TellKey, "Kael", now.AddSeconds(1)));
        inbox.Sync();

        Assert.Equal(2, inbox.Rows.Count);
        Assert.Equal("Kael", inbox.Rows[0].Title);
        Assert.Equal("Aria", inbox.Rows[1].Title);
        Assert.True(inbox.Rows[0].IsTell);
    }

    [Fact]
    public void RowsSortByRecency()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"), Tab("Party", "party"));
        using var scope = inbox;
        var now = DateTime.Now;
        log.Append(Entry(log, "fc", "Rin", now));
        log.Append(Entry(log, "party", "Nala", now.AddMinutes(1)));
        inbox.Sync();

        Assert.Equal("tab:Party", inbox.Rows[0].Key);

        log.Append(Entry(log, "fc", "Mira", now.AddMinutes(2)));
        Assert.Equal("tab:FC", inbox.Rows[0].Key);
    }

    [Fact]
    public void MarkReadClearsTheBadgeAndPersists()
    {
        var (log, inbox, configuration) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", DateTime.Now));
        inbox.Sync();
        var row = inbox.Find("tab:FC")!;
        Assert.Equal(1, row.Unread);

        inbox.MarkRead(row);

        Assert.Equal(0, row.Unread);
        Assert.Equal(0, inbox.TotalUnread);
        Assert.True(configuration.LinkpearlSeen.ContainsKey("tab:FC"));
    }

    [Fact]
    public void ReadWatermarkSurvivesARebuild()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", DateTime.Now));
        inbox.Sync();
        inbox.MarkRead(inbox.Find("tab:FC")!);

        inbox.Invalidate();
        inbox.Sync();

        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(0, inbox.TotalUnread);
    }

    [Fact]
    public void ViewedConversationsDoNotAccumulateUnread()
    {
        var (log, inbox, _) = Build(Tab("FC", "fc"));
        using var scope = inbox;
        inbox.Sync();
        inbox.Viewing = "tab:FC";

        log.Append(Entry(log, "fc", "Rin", DateTime.Now));

        Assert.Equal(0, inbox.Find("tab:FC")!.Unread);
        Assert.Equal(0, inbox.TotalUnread);
    }

    [Fact]
    public void PinnedTabsLeaveTheMainList()
    {
        var tab = Tab("FC", "fc");
        tab.Pinned = true;
        var (log, inbox, _) = Build(tab);
        using var scope = inbox;
        log.Append(Entry(log, "fc", "Rin", DateTime.Now));
        inbox.Sync();

        Assert.Single(inbox.Pinned);
        Assert.Empty(inbox.Rows);
        Assert.NotNull(inbox.Find("tab:FC"));
    }

    [Fact]
    public void EnsureTellCreatesAConversationBeforeTheFirstLine()
    {
        var (log, inbox, _) = Build();
        using var scope = inbox;
        inbox.Sync();

        var row = inbox.EnsureTell("Aria Solveig", "Siren");
        Assert.Equal(ChatStreams.ForTell("Aria Solveig@Siren"), row.Key);
        Assert.NotNull(inbox.Find(row.Key));

        inbox.Invalidate();
        inbox.Sync();
        Assert.NotNull(inbox.Find(row.Key));

        inbox.ClearTransient();
        inbox.Sync();
        Assert.Null(inbox.Find(row.Key));
    }

    [Fact]
    public void TellsShowBubblesUntilALayoutIsStoredForThem()
    {
        var (log, inbox, configuration) = Build();
        using var scope = inbox;
        var tell = new ChatEntry(log.NextSequence(), GameChannels.TellKey, "Rin", "Siren", "hey",
            new[] { ChatChunk.Plain("hey") }, DateTime.Now, ChatEntryFlags.None);
        log.Append(tell);
        inbox.Sync();
        Assert.Equal(ChatDensity.Bubbles, inbox.Find(tell.StreamKey)!.Density);

        configuration.LinkpearlTellLayouts[tell.StreamKey] = (int)ChatDensity.Log;
        var stored = new ChatInbox(log, new TabStoreStub(configuration).Store, new TellPreferences(configuration),
            configuration);
        using var storedScope = stored;
        stored.Sync();
        Assert.Equal(ChatDensity.Log, stored.Find(tell.StreamKey)!.Density);
    }

    [Fact]
    public void TabRowsReportTheirTabLayout()
    {
        var tab = Tab("FC", "fc");
        tab.Density = ChatDensity.Bubbles;
        var (_, inbox, _) = Build(tab);
        using var scope = inbox;
        inbox.Sync();

        var row = inbox.Find("tab:FC");
        Assert.NotNull(row);
        Assert.Equal(ChatDensity.Bubbles, row!.Density);

        tab.Density = ChatDensity.Log;
        Assert.Equal(ChatDensity.Log, row.Density);
    }

    private sealed class TabStoreStub
    {
        public TabStoreStub(Configuration configuration) =>
            Store = new TabStore(configuration, new Aetherphone.Core.Game.CharacterWatch(null!));

        public TabStore Store { get; }
    }
}
