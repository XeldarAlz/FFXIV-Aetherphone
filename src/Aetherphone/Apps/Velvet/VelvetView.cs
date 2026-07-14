namespace Aetherphone.Apps.Velvet;

internal enum VelvetPage
{
    Discover,
    Feed,
    Messages,
    Me,
}

internal enum VelvetScreenId
{
    Root,
    Profile,
    Thread,
    PostDetail,
    Compose,
    EditProfile,
    Settings,
    Activity,
    Likers,
    Blocked,
    ChatImage,
    ImageView,
    Intro,
    Reactions,
}

internal readonly record struct VelvetView(VelvetScreenId Screen, string? Arg = null)
{
    public static readonly VelvetView Root = new(VelvetScreenId.Root);
    public static readonly VelvetView Compose = new(VelvetScreenId.Compose);
    public static readonly VelvetView EditProfile = new(VelvetScreenId.EditProfile);
    public static readonly VelvetView Settings = new(VelvetScreenId.Settings);
    public static readonly VelvetView Activity = new(VelvetScreenId.Activity);
    public static readonly VelvetView Blocked = new(VelvetScreenId.Blocked);

    public static VelvetView Profile(string userId) => new(VelvetScreenId.Profile, userId);
    public static VelvetView Thread(string userId) => new(VelvetScreenId.Thread, userId);
    public static VelvetView PostDetail(string postId) => new(VelvetScreenId.PostDetail, postId);
    public static VelvetView Likers(string postId) => new(VelvetScreenId.Likers, postId);
    public static VelvetView ChatImage(string userId) => new(VelvetScreenId.ChatImage, userId);
    public static VelvetView ImageView(string messageId) => new(VelvetScreenId.ImageView, messageId);
    public static VelvetView Intro(string userId) => new(VelvetScreenId.Intro, userId);
    public static VelvetView Reactions(string messageId) => new(VelvetScreenId.Reactions, messageId);
}

internal enum VelvetMessagesTab
{
    Chats,
    Requests,
}
