using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Social;

internal static class SocialAudience
{
    public const int Everyone = 0;

    public const int Following = 1;

    public const int NoOne = 2;

    public static readonly LocString[] Options =
    {
        L.Social.AudienceEveryone,
        L.Social.AudienceFollowing,
        L.Social.AudienceNoOne,
    };

    public static LocString Label(int policy) => policy switch
    {
        Following => L.Social.AudienceFollowing,
        NoOne => L.Social.AudienceNoOne,
        _ => L.Social.AudienceEveryone,
    };

    public static bool IsDefined(int policy) => policy is Everyone or Following or NoOne;
}
