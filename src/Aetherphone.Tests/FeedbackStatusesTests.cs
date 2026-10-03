using Aetherphone.Apps.Feedback;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FeedbackStatusesTests
{
    private static readonly FeedbackStatus[] Statuses =
    {
        FeedbackStatus.Received, FeedbackStatus.InReview, FeedbackStatus.Planned, FeedbackStatus.Resolved,
        FeedbackStatus.Closed,
    };

    private static readonly FeedbackCategory[] Categories =
    {
        FeedbackCategory.Bug, FeedbackCategory.Idea, FeedbackCategory.Praise, FeedbackCategory.Other,
    };

    [Fact]
    public void WireStatusesMapToPlayerStatuses()
    {
        Assert.Equal(FeedbackStatus.Received, FeedbackStatuses.Parse("open"));
        Assert.Equal(FeedbackStatus.InReview, FeedbackStatuses.Parse("review"));
        Assert.Equal(FeedbackStatus.Planned, FeedbackStatuses.Parse("planned"));
        Assert.Equal(FeedbackStatus.Resolved, FeedbackStatuses.Parse("resolved"));
        Assert.Equal(FeedbackStatus.Closed, FeedbackStatuses.Parse("dismissed"));
        Assert.Equal(FeedbackStatus.Planned, FeedbackStatuses.Parse("Planned"));
        Assert.Equal(FeedbackStatus.Received, FeedbackStatuses.Parse("something-new"));
        Assert.Equal(FeedbackStatus.Received, FeedbackStatuses.Parse(null));
    }

    [Fact]
    public void WireReasonsMapToReasons()
    {
        Assert.Equal(FeedbackReason.Duplicate, FeedbackStatuses.ParseReason("duplicate"));
        Assert.Equal(FeedbackReason.WontDo, FeedbackStatuses.ParseReason("wontdo"));
        Assert.Equal(FeedbackReason.CantReproduce, FeedbackStatuses.ParseReason("cantreproduce"));
        Assert.Equal(FeedbackReason.NotABug, FeedbackStatuses.ParseReason("notabug"));
        Assert.Equal(FeedbackReason.None, FeedbackStatuses.ParseReason(string.Empty));
        Assert.Equal(FeedbackReason.None, FeedbackStatuses.ParseReason("other"));
        Assert.Equal(FeedbackReason.None, FeedbackStatuses.ParseReason(null));
    }

    [Fact]
    public void ClosedUsesTheShortTimelineAndEveryOtherStatusTheFullOne()
    {
        Assert.Equal(2, FeedbackStatuses.TimelineSteps(FeedbackStatus.Closed));
        Assert.Equal(2, FeedbackStatuses.CompletedSteps(FeedbackStatus.Closed));
        Assert.Equal(1, FeedbackStatuses.CompletedSteps(FeedbackStatus.Received));
        Assert.Equal(2, FeedbackStatuses.CompletedSteps(FeedbackStatus.InReview));
        Assert.Equal(3, FeedbackStatuses.CompletedSteps(FeedbackStatus.Planned));
        Assert.Equal(4, FeedbackStatuses.CompletedSteps(FeedbackStatus.Resolved));
        for (var index = 0; index < Statuses.Length; index++)
        {
            Assert.InRange(FeedbackStatuses.CompletedSteps(Statuses[index]), 1,
                FeedbackStatuses.TimelineSteps(Statuses[index]));
        }
    }

    [Fact]
    public void EveryStatusAndReasonHasItsOwnExplanation()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Statuses.Length; index++)
        {
            Assert.True(keys.Add(FeedbackStatuses.Explanation(Statuses[index], FeedbackReason.None).Key));
        }

        var reasons = new[]
        {
            FeedbackReason.Duplicate, FeedbackReason.WontDo, FeedbackReason.CantReproduce, FeedbackReason.NotABug,
        };
        for (var index = 0; index < reasons.Length; index++)
        {
            Assert.True(keys.Add(FeedbackStatuses.Explanation(FeedbackStatus.Closed, reasons[index]).Key));
        }
    }

    [Fact]
    public void ReasonsOnlyChangeTheExplanationOfClosedItems()
    {
        Assert.Equal(FeedbackStatuses.Explanation(FeedbackStatus.Resolved, FeedbackReason.None).Key,
            FeedbackStatuses.Explanation(FeedbackStatus.Resolved, FeedbackReason.Duplicate).Key);
    }

    [Fact]
    public void BugsAndIdeasGetTheirOwnUpdateTitles()
    {
        for (var statusIndex = 1; statusIndex < Statuses.Length; statusIndex++)
        {
            var status = Statuses[statusIndex];
            var bug = FeedbackStatuses.UpdateTitle(FeedbackCategory.Bug, status).Key;
            var idea = FeedbackStatuses.UpdateTitle(FeedbackCategory.Idea, status).Key;
            var praise = FeedbackStatuses.UpdateTitle(FeedbackCategory.Praise, status).Key;
            Assert.NotEqual(bug, idea);
            Assert.NotEqual(bug, praise);
            Assert.Equal(praise, FeedbackStatuses.UpdateTitle(FeedbackCategory.Other, status).Key);
        }

        for (var index = 0; index < Categories.Length; index++)
        {
            Assert.Equal(L.Feedback.UpdateReopened.Key,
                FeedbackStatuses.UpdateTitle(Categories[index], FeedbackStatus.Received).Key);
        }
    }

    [Fact]
    public void TimelineLabelsFollowTheStatusPath()
    {
        Assert.Equal(L.Feedback.TimelineSent.Key, FeedbackStatuses.StepLabel(FeedbackStatus.Planned, 0).Key);
        Assert.Equal(L.Feedback.StatusPlanned.Key, FeedbackStatuses.StepLabel(FeedbackStatus.Planned, 2).Key);
        Assert.Equal(L.Feedback.StatusResolved.Key, FeedbackStatuses.StepLabel(FeedbackStatus.Received, 3).Key);
        Assert.Equal(L.Feedback.StatusClosed.Key, FeedbackStatuses.StepLabel(FeedbackStatus.Closed, 1).Key);
    }
}
