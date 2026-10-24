using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Chirper;

internal enum ChirperScreen
{
    Home,
    Compose,
    Profile,
    EditProfile,
    Avatar,
    Banner,
    Discover,
    Thread,
    UserList,
    Activity,
    Hashtag,
    BadgeProgress,
    Dm,
}

internal readonly record struct ChirperRoute(
    ChirperScreen Screen,
    string? UserId = null,
    string? PostId = null,
    UserListKind Kind = UserListKind.Followers,
    string? Tag = null,
    SocialDmScreen DmScreen = SocialDmScreen.Inbox,
    string? DmId = null)
{
    public static readonly ChirperRoute Home = new(ChirperScreen.Home);
    public static readonly ChirperRoute Compose = new(ChirperScreen.Compose);
    public static readonly ChirperRoute EditProfile = new(ChirperScreen.EditProfile);
    public static readonly ChirperRoute Avatar = new(ChirperScreen.Avatar);
    public static readonly ChirperRoute Banner = new(ChirperScreen.Banner);
    public static readonly ChirperRoute Discover = new(ChirperScreen.Discover);
    public static readonly ChirperRoute Activity = new(ChirperScreen.Activity);
    public static readonly ChirperRoute BadgeProgress = new(ChirperScreen.BadgeProgress);
    public static ChirperRoute Profile(string userId) => new(ChirperScreen.Profile, userId);
    public static ChirperRoute Thread(string postId) => new(ChirperScreen.Thread, PostId: postId);
    public static ChirperRoute Hashtag(string tag) => new(ChirperScreen.Hashtag, Tag: tag);

    public static ChirperRoute Dm(SocialDmScreen screen, string? id = null) =>
        new(ChirperScreen.Dm, DmScreen: screen, DmId: id);

    public static ChirperRoute UserList(string sourceId, UserListKind kind) =>
        new(ChirperScreen.UserList, UserId: sourceId, Kind: kind);
}
