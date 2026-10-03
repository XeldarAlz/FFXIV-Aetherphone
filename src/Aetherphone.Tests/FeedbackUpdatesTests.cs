using System.Text.Json;
using Aetherphone.Apps.Feedback;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Feedback;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedbackUpdatesTests
{
    private static MyFeedbackDto Item(string id, long updatedAt = 0, string status = "open", string reply = "",
        long repliedAt = 0, string category = "bug") =>
        new(id, "text " + id, category, status, Array.Empty<string>(), 100, 0, string.Empty, reply, repliedAt,
            updatedAt);

    [Fact]
    public void FirstFetchIsSilentAndInitializesTheMarkToTheNewestUpdate()
    {
        var marks = new FeedbackUpdateMarks();
        var raised = new List<FeedbackUpdate>();
        var items = new[] { Item("a", 500, "planned"), Item("b", 900, "resolved") };

        var changed = FeedbackUpdates.Apply(marks, items, true, null, raised);

        Assert.True(changed);
        Assert.Empty(raised);
        Assert.True(marks.Initialized);
        Assert.True(marks.HasHistory);
        Assert.Equal(900, marks.NotifiedUnix);
        Assert.Empty(marks.Unseen);
    }

    [Fact]
    public void OnlyItemsNewerThanTheMarkNotifyOldestChangeFirst()
    {
        var marks = new FeedbackUpdateMarks { Initialized = true, HasHistory = true, NotifiedUnix = 500 };
        var raised = new List<FeedbackUpdate>();
        var items = new[] { Item("new", 800, "review"), Item("old", 400, "planned"), Item("mid", 600, "planned") };

        var changed = FeedbackUpdates.Apply(marks, items, true, null, raised);

        Assert.True(changed);
        Assert.Equal(2, raised.Count);
        Assert.Equal("mid", raised[0].Item.Id);
        Assert.Equal("new", raised[1].Item.Id);
        Assert.Equal(800, marks.NotifiedUnix);
        Assert.Equal(2, marks.Unseen.Count);
        Assert.Equal(800, marks.Unseen["new"]);
    }

    [Fact]
    public void NothingNewerLeavesTheMarksUntouched()
    {
        var marks = new FeedbackUpdateMarks { Initialized = true, HasHistory = true, NotifiedUnix = 500 };
        var raised = new List<FeedbackUpdate>();

        var changed = FeedbackUpdates.Apply(marks, new[] { Item("a", 500) }, false, null, raised);

        Assert.False(changed);
        Assert.Empty(raised);
        Assert.Equal(500, marks.NotifiedUnix);
    }

    [Fact]
    public void OldServersWithoutUpdateStampsNeverNotify()
    {
        var marks = new FeedbackUpdateMarks();
        var raised = new List<FeedbackUpdate>();
        var items = new[] { Item("a"), Item("b") };

        FeedbackUpdates.Apply(marks, items, true, null, raised);
        var changed = FeedbackUpdates.Apply(marks, items, true, null, raised);

        Assert.False(changed);
        Assert.Empty(raised);
        Assert.Equal(0, marks.NotifiedUnix);
    }

    [Fact]
    public void TheItemOnScreenAdvancesTheMarkWithoutNotifying()
    {
        var marks = new FeedbackUpdateMarks { Initialized = true, HasHistory = true, NotifiedUnix = 100 };
        var raised = new List<FeedbackUpdate>();

        FeedbackUpdates.Apply(marks, new[] { Item("open-now", 300, "resolved") }, true, "open-now", raised);

        Assert.Empty(raised);
        Assert.Empty(marks.Unseen);
        Assert.Equal(300, marks.NotifiedUnix);
    }

    [Fact]
    public void AReplyNewerThanTheMarkClassifiesAsReplyAndAStatusChangeAfterItAsStatus()
    {
        var replied = Item("a", 700, "review", "Thanks, looking at it", 700);

        Assert.Equal(FeedbackChange.Reply, FeedbackUpdates.Classify(replied, 600));
        Assert.Equal(FeedbackChange.None, FeedbackUpdates.Classify(replied, 700));

        var resolvedLater = Item("a", 710, "resolved", "Thanks, looking at it", 700);
        Assert.Equal(FeedbackChange.Status, FeedbackUpdates.Classify(resolvedLater, 700));
    }

    [Fact]
    public void AClearedReplyIsAStatusChange()
    {
        var cleared = Item("a", 700, "review", string.Empty, 700);

        Assert.Equal(FeedbackChange.Status, FeedbackUpdates.Classify(cleared, 600));
    }

    [Fact]
    public void ACompleteListPrunesUnseenItemsThatNoLongerExist()
    {
        var marks = new FeedbackUpdateMarks { Initialized = true, HasHistory = true, NotifiedUnix = 900 };
        marks.Unseen["gone"] = 800;
        marks.Unseen["kept"] = 850;
        var raised = new List<FeedbackUpdate>();

        var changed = FeedbackUpdates.Apply(marks, new[] { Item("kept", 850) }, true, null, raised);

        Assert.True(changed);
        Assert.False(marks.Unseen.ContainsKey("gone"));
        Assert.True(marks.Unseen.ContainsKey("kept"));
    }

    [Fact]
    public void APartialListKeepsUnseenItemsFromLaterPages()
    {
        var marks = new FeedbackUpdateMarks { Initialized = true, HasHistory = true, NotifiedUnix = 900 };
        marks.Unseen["older-page"] = 800;
        var raised = new List<FeedbackUpdate>();

        FeedbackUpdates.Apply(marks, new[] { Item("kept", 850) }, false, null, raised);

        Assert.True(marks.Unseen.ContainsKey("older-page"));
    }

    [Fact]
    public void ViewingAnItemClearsItsBadgeOnce()
    {
        var marks = new FeedbackUpdateMarks();
        marks.Unseen["a"] = 10;

        Assert.True(FeedbackUpdates.MarkViewed(marks, "a"));
        Assert.False(FeedbackUpdates.MarkViewed(marks, "a"));
        Assert.Empty(marks.Unseen);
    }

    [Fact]
    public void ExcerptKeepsShortTextAndTrimsLongTextWithAnEllipsis()
    {
        Assert.Equal("Hello", FeedbackUpdates.Excerpt("  Hello  "));
        Assert.Equal(string.Empty, FeedbackUpdates.Excerpt(null));

        var excerpt = FeedbackUpdates.Excerpt(new string('x', 400));
        Assert.True(excerpt.Length <= FeedbackUpdates.ExcerptLength);
        Assert.EndsWith("…", excerpt);
    }

    [Fact]
    public void OldServerJsonDeserializesWithDefaults()
    {
        const string json = "{\"items\":[{\"id\":\"f1\",\"text\":\"hi\",\"category\":\"bug\",\"status\":\"open\"," +
                            "\"imageUrls\":[],\"createdAtUnix\":5,\"resolvedAtUnix\":0}],\"nextCursor\":null}";

        var page = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.MyFeedbackPage);

        Assert.NotNull(page);
        var item = Assert.Single(page!.Items);
        Assert.Equal(string.Empty, item.Reason);
        Assert.Equal(string.Empty, item.Reply);
        Assert.Equal(0, item.RepliedAtUnix);
        Assert.Equal(0, item.UpdatedAtUnix);
    }

    [Fact]
    public void NewServerJsonCarriesReplyFields()
    {
        const string json = "{\"items\":[{\"id\":\"f1\",\"text\":\"hi\",\"category\":\"bug\",\"status\":\"dismissed\"," +
                            "\"imageUrls\":[],\"createdAtUnix\":5,\"resolvedAtUnix\":9,\"reason\":\"duplicate\"," +
                            "\"reply\":\"Tracked elsewhere\",\"repliedAtUnix\":9,\"updatedAtUnix\":9}]}";

        var item = Assert.Single(JsonSerializer.Deserialize(json, AethernetJsonContext.Default.MyFeedbackPage)!.Items);

        Assert.Equal("duplicate", item.Reason);
        Assert.Equal("Tracked elsewhere", item.Reply);
        Assert.Equal(9, item.RepliedAtUnix);
        Assert.Equal(9, item.UpdatedAtUnix);
    }

    [Fact]
    public void GroupKeysRoundTrip()
    {
        var key = FeedbackLauncher.GroupKey("abc");

        Assert.True(FeedbackLauncher.TryParseGroupKey(key, out var feedbackId));
        Assert.Equal("abc", feedbackId);
        Assert.False(FeedbackLauncher.TryParseGroupKey("feedback:", out _));
        Assert.False(FeedbackLauncher.TryParseGroupKey("announcements:abc", out _));
        Assert.False(FeedbackLauncher.TryParseGroupKey(null, out _));
    }
}
