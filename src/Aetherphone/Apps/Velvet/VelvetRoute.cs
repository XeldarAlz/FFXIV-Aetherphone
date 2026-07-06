namespace Aetherphone.Apps.Velvet;

internal enum VelvetTab
{
    Hub,
    Messages,
    Me,
}

internal enum VelvetHubView
{
    Timeline,
    Discover,
}

internal enum VelvetScreen
{
    Root,
    Profile,
    EditProfile,
    Settings,
    Thread,
    Avatar,
    Compose,
    PostDetail,
    ChatImage,
    ImageView,
    Likers,
}

internal readonly record struct VelvetRoute(VelvetScreen Screen, string? Id = null)
{
    public static readonly VelvetRoute Root = new(VelvetScreen.Root);
    public static readonly VelvetRoute EditProfile = new(VelvetScreen.EditProfile);
    public static readonly VelvetRoute Settings = new(VelvetScreen.Settings);
    public static readonly VelvetRoute Avatar = new(VelvetScreen.Avatar);
    public static readonly VelvetRoute Compose = new(VelvetScreen.Compose);
    public static VelvetRoute Profile(string userId) => new(VelvetScreen.Profile, userId);
    public static VelvetRoute Thread(string userId) => new(VelvetScreen.Thread, userId);
    public static VelvetRoute ChatImage(string userId) => new(VelvetScreen.ChatImage, userId);
    public static VelvetRoute ImageView(string messageId) => new(VelvetScreen.ImageView, messageId);
    public static VelvetRoute PostDetail(string postId) => new(VelvetScreen.PostDetail, postId);
    public static VelvetRoute Likers(string postId) => new(VelvetScreen.Likers, postId);
}
