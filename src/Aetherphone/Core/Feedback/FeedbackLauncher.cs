using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Feedback;

internal sealed class FeedbackLauncher
{
    public const string GroupKeyPrefix = "feedback:";

    private readonly LaunchIntent detail = new();

    public static string GroupKey(string feedbackId) => GroupKeyPrefix + feedbackId;

    public static bool TryParseGroupKey(string? groupKey, out string feedbackId)
    {
        if (groupKey is null || groupKey.Length <= GroupKeyPrefix.Length ||
            !groupKey.StartsWith(GroupKeyPrefix, StringComparison.Ordinal))
        {
            feedbackId = string.Empty;
            return false;
        }

        feedbackId = groupKey[GroupKeyPrefix.Length..];
        return true;
    }

    public void RequestDetail(string feedbackId) => detail.Request(feedbackId);

    public bool TryConsumeDetail(out string feedbackId) => detail.TryConsume(out feedbackId);
}
