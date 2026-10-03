using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Apps.Feedback;

internal enum FeedbackCategory : byte
{
    Bug,
    Idea,
    Praise,
    Other,
}

internal readonly record struct FeedbackKind(
    FeedbackCategory Category,
    string WireName,
    FontAwesomeIcon Icon,
    Vector4 Tint,
    LocString Title,
    LocString Subtitle,
    LocString Prompt,
    LocString Placeholder,
    LocString Thanks);

internal static class FeedbackKinds
{
    public static readonly FeedbackKind[] All =
    {
        new(FeedbackCategory.Bug, "bug", FontAwesomeIcon.Bug, AccentRing.Red, L.Feedback.KindBug,
            L.Feedback.KindBugSubtitle, L.Feedback.PromptBug, L.Feedback.PlaceholderBug, L.Feedback.ThanksBug),
        new(FeedbackCategory.Idea, "idea", FontAwesomeIcon.Lightbulb, AccentRing.Gold, L.Feedback.KindIdea,
            L.Feedback.KindIdeaSubtitle, L.Feedback.PromptIdea, L.Feedback.PlaceholderIdea, L.Feedback.ThanksIdea),
        new(FeedbackCategory.Praise, "praise", FontAwesomeIcon.Heart, AccentRing.Rose, L.Feedback.KindPraise,
            L.Feedback.KindPraiseSubtitle, L.Feedback.PromptPraise, L.Feedback.PlaceholderPraise,
            L.Feedback.ThanksPraise),
        new(FeedbackCategory.Other, "other", FontAwesomeIcon.CommentDots, AccentRing.Indigo, L.Feedback.KindOther,
            L.Feedback.KindOtherSubtitle, L.Feedback.PromptOther, L.Feedback.PlaceholderOther,
            L.Feedback.ThanksOther),
    };

    public static ref readonly FeedbackKind Of(FeedbackCategory category) => ref All[(int)category];

    public static FeedbackCategory Parse(string? wireName)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (string.Equals(All[index].WireName, wireName, StringComparison.OrdinalIgnoreCase))
            {
                return All[index].Category;
            }
        }

        return FeedbackCategory.Other;
    }
}

internal enum FeedbackStatus : byte
{
    Received,
    InReview,
    Planned,
    Resolved,
    Closed,
}

internal enum FeedbackReason : byte
{
    None,
    Duplicate,
    WontDo,
    CantReproduce,
    NotABug,
}

internal static class FeedbackStatuses
{
    public const int FullTimelineSteps = 4;
    public const int ClosedTimelineSteps = 2;

    private static readonly LocString[] BugTitles =
    {
        L.Feedback.UpdateReopened, L.Feedback.UpdateBugInReview, L.Feedback.UpdateBugPlanned,
        L.Feedback.UpdateBugResolved, L.Feedback.UpdateBugClosed,
    };

    private static readonly LocString[] IdeaTitles =
    {
        L.Feedback.UpdateReopened, L.Feedback.UpdateIdeaInReview, L.Feedback.UpdateIdeaPlanned,
        L.Feedback.UpdateIdeaResolved, L.Feedback.UpdateIdeaClosed,
    };

    private static readonly LocString[] GeneralTitles =
    {
        L.Feedback.UpdateReopened, L.Feedback.UpdateInReview, L.Feedback.UpdatePlanned,
        L.Feedback.UpdateResolved, L.Feedback.UpdateClosed,
    };

    public static FeedbackStatus Parse(string? wireName)
    {
        if (string.Equals(wireName, "review", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.InReview;
        }

        if (string.Equals(wireName, "planned", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.Planned;
        }

        if (string.Equals(wireName, "resolved", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.Resolved;
        }

        if (string.Equals(wireName, "dismissed", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackStatus.Closed;
        }

        return FeedbackStatus.Received;
    }

    public static FeedbackReason ParseReason(string? wireName)
    {
        if (string.Equals(wireName, "duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackReason.Duplicate;
        }

        if (string.Equals(wireName, "wontdo", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackReason.WontDo;
        }

        if (string.Equals(wireName, "cantreproduce", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackReason.CantReproduce;
        }

        if (string.Equals(wireName, "notabug", StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackReason.NotABug;
        }

        return FeedbackReason.None;
    }

    public static LocString Label(FeedbackStatus status) => status switch
    {
        FeedbackStatus.InReview => L.Feedback.TimelineReview,
        FeedbackStatus.Planned => L.Feedback.StatusPlanned,
        FeedbackStatus.Resolved => L.Feedback.StatusResolved,
        FeedbackStatus.Closed => L.Feedback.StatusClosed,
        _ => L.Feedback.StatusReceived,
    };

    public static LocString Explanation(FeedbackStatus status, FeedbackReason reason)
    {
        if (status != FeedbackStatus.Closed)
        {
            return status switch
            {
                FeedbackStatus.InReview => L.Feedback.StatusReviewHint,
                FeedbackStatus.Planned => L.Feedback.StatusPlannedHint,
                FeedbackStatus.Resolved => L.Feedback.StatusResolvedHint,
                _ => L.Feedback.StatusReceivedHint,
            };
        }

        return reason switch
        {
            FeedbackReason.Duplicate => L.Feedback.ReasonDuplicateHint,
            FeedbackReason.WontDo => L.Feedback.ReasonWontDoHint,
            FeedbackReason.CantReproduce => L.Feedback.ReasonCantReproduceHint,
            FeedbackReason.NotABug => L.Feedback.ReasonNotABugHint,
            _ => L.Feedback.StatusClosedHint,
        };
    }

    public static Vector4 Tint(FeedbackStatus status) => status switch
    {
        FeedbackStatus.InReview => AccentRing.Violet,
        FeedbackStatus.Planned => AccentRing.Orange,
        FeedbackStatus.Resolved => AccentRing.Green,
        FeedbackStatus.Closed => AccentRing.Slate,
        _ => AccentRing.Azure,
    };

    public static int TimelineSteps(FeedbackStatus status) =>
        status == FeedbackStatus.Closed ? ClosedTimelineSteps : FullTimelineSteps;

    public static int CompletedSteps(FeedbackStatus status) => status switch
    {
        FeedbackStatus.InReview => 2,
        FeedbackStatus.Planned => 3,
        FeedbackStatus.Resolved => FullTimelineSteps,
        FeedbackStatus.Closed => ClosedTimelineSteps,
        _ => 1,
    };

    public static LocString StepLabel(FeedbackStatus status, int step)
    {
        if (step == 0)
        {
            return L.Feedback.TimelineSent;
        }

        if (status == FeedbackStatus.Closed)
        {
            return L.Feedback.StatusClosed;
        }

        return step switch
        {
            1 => L.Feedback.TimelineReview,
            2 => L.Feedback.StatusPlanned,
            _ => L.Feedback.StatusResolved,
        };
    }

    public static LocString UpdateTitle(FeedbackCategory category, FeedbackStatus status)
    {
        var titles = category switch
        {
            FeedbackCategory.Bug => BugTitles,
            FeedbackCategory.Idea => IdeaTitles,
            _ => GeneralTitles,
        };
        return titles[(int)status];
    }
}
