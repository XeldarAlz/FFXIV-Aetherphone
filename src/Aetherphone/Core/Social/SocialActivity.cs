using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal static class SocialActivity
{
    public const int TypeLike = 0;
    public const int TypeComment = 1;
    public const int TypeFollow = 2;
    public const int TypeConnectRequest = 3;
    public const int TypeConnectAccept = 4;
    public const int TypePostRemoved = 5;
    public const int TypeCommentLike = 6;
    public const string ChirperApp = "chirper";
    public const string AethergramApp = "aethergram";
    public const string VelvetApp = "velvet";

    public static bool OpensPost(NotificationDto item) =>
        item.Type is TypeLike or TypeComment or TypeCommentLike && !string.IsNullOrEmpty(item.PostId);

    public static string ActorLabel(NotificationDto item)
    {
        if (!string.IsNullOrEmpty(item.ActorDisplayName))
        {
            return item.ActorDisplayName;
        }

        return string.IsNullOrEmpty(item.ActorHandle) ? item.ActorName : item.ActorHandle;
    }

    public static string Body(NotificationDto item)
    {
        var isPhoto = item.App != ChirperApp;
        switch (item.Type)
        {
            case TypeLike:
                return Loc.T(isPhoto ? L.Social.LikedPhoto : L.Social.LikedChirp);
            case TypeComment:
                var action = Loc.T(isPhoto ? L.Social.CommentedPhoto : L.Social.CommentedChirp);
                return string.IsNullOrEmpty(item.Preview) ? action : $"{action}: “{item.Preview}”";
            case TypeCommentLike:
                var likedAction = Loc.T(L.Social.LikedComment);
                return string.IsNullOrEmpty(item.Preview) ? likedAction : $"{likedAction}: “{item.Preview}”";
            case TypeFollow:
                return Loc.T(L.Social.Followed);
            case TypeConnectRequest:
                return Loc.T(L.Social.ConnectionRequest);
            case TypeConnectAccept:
                return Loc.T(L.Social.ConnectionAccepted);
            case TypePostRemoved:
                return ContentModeration.RemovalMessage(item.Preview);
            default:
                return string.Empty;
        }
    }
}
