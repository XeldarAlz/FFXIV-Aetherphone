using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Feedback;

namespace Aetherphone.Apps.Feedback;

internal enum FeedbackChange : byte
{
    None,
    Status,
    Reply,
}

internal readonly record struct FeedbackUpdate(MyFeedbackDto Item, FeedbackChange Change);

internal static class FeedbackUpdates
{
    public const int ExcerptLength = 160;

    private const char Ellipsis = '…';

    private static readonly Comparison<FeedbackUpdate> ByUpdateTime =
        (first, second) => first.Item.UpdatedAtUnix.CompareTo(second.Item.UpdatedAtUnix);

    public static long NewestUpdate(MyFeedbackDto[] items)
    {
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].UpdatedAtUnix > newest)
            {
                newest = items[index].UpdatedAtUnix;
            }
        }

        return newest;
    }

    public static FeedbackChange Classify(MyFeedbackDto item, long markUnix)
    {
        if (item.UpdatedAtUnix <= markUnix)
        {
            return FeedbackChange.None;
        }

        if (item.RepliedAtUnix > markUnix && !string.IsNullOrWhiteSpace(item.Reply))
        {
            return FeedbackChange.Reply;
        }

        return FeedbackChange.Status;
    }

    public static bool Apply(FeedbackUpdateMarks marks, MyFeedbackDto[] items, bool complete, string? viewingId,
        List<FeedbackUpdate> raised)
    {
        var changed = false;
        if (items.Length > 0 && !marks.HasHistory)
        {
            marks.HasHistory = true;
            changed = true;
        }

        var newest = NewestUpdate(items);
        if (!marks.Initialized)
        {
            marks.Initialized = true;
            marks.NotifiedUnix = newest;
            return true;
        }

        var markUnix = marks.NotifiedUnix;
        if (newest > markUnix)
        {
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var change = Classify(item, markUnix);
                if (change == FeedbackChange.None || string.Equals(item.Id, viewingId, StringComparison.Ordinal))
                {
                    continue;
                }

                raised.Add(new FeedbackUpdate(item, change));
                marks.Unseen[item.Id] = item.UpdatedAtUnix;
            }

            raised.Sort(ByUpdateTime);
            marks.NotifiedUnix = newest;
            changed = true;
        }

        if (complete && PruneUnseen(marks, items))
        {
            changed = true;
        }

        return changed;
    }

    public static bool MarkViewed(FeedbackUpdateMarks marks, string feedbackId) => marks.Unseen.Remove(feedbackId);

    public static string Excerpt(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length <= ExcerptLength)
        {
            return trimmed;
        }

        var cut = ExcerptLength - 1;
        if (char.IsHighSurrogate(trimmed[cut - 1]))
        {
            cut--;
        }

        return string.Concat(trimmed.AsSpan(0, cut).TrimEnd(), Ellipsis.ToString());
    }

    private static bool PruneUnseen(FeedbackUpdateMarks marks, MyFeedbackDto[] items)
    {
        if (marks.Unseen.Count == 0)
        {
            return false;
        }

        List<string>? stale = null;
        foreach (var feedbackId in marks.Unseen.Keys)
        {
            if (!Contains(items, feedbackId))
            {
                stale ??= new List<string>();
                stale.Add(feedbackId);
            }
        }

        if (stale is null)
        {
            return false;
        }

        for (var index = 0; index < stale.Count; index++)
        {
            marks.Unseen.Remove(stale[index]);
        }

        return true;
    }

    private static bool Contains(MyFeedbackDto[] items, string feedbackId)
    {
        for (var index = 0; index < items.Length; index++)
        {
            if (string.Equals(items[index].Id, feedbackId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
