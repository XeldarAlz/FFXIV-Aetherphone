using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal static class ContentModeration
{
    public const string Pending = "pending";
    public const string Clean = "clean";
    public const string Flagged = "flagged";
    public const string Rejected = "rejected";

    public static bool IsInReview(string? scanStatus)
    {
        return scanStatus is Pending or Flagged;
    }

    public static string RemovalMessage(string? reasonCode)
    {
        return (reasonCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "adult-content" => Loc.T(L.Moderation.RemovedAdult),
            "violence" => Loc.T(L.Moderation.RemovedViolence),
            "harassment" => Loc.T(L.Moderation.RemovedHarassment),
            "hate" => Loc.T(L.Moderation.RemovedHate),
            "self-harm" => Loc.T(L.Moderation.RemovedSelfHarm),
            _ => Loc.T(L.Moderation.RemovedPolicy),
        };
    }

    public static string CommentRemovalMessage(string? reasonCode)
    {
        return (reasonCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "adult-content" => Loc.T(L.Moderation.RemovedCommentAdult),
            "violence" => Loc.T(L.Moderation.RemovedCommentViolence),
            "harassment" => Loc.T(L.Moderation.RemovedCommentHarassment),
            "hate" => Loc.T(L.Moderation.RemovedCommentHate),
            "self-harm" => Loc.T(L.Moderation.RemovedCommentSelfHarm),
            _ => Loc.T(L.Moderation.RemovedCommentPolicy),
        };
    }
}
