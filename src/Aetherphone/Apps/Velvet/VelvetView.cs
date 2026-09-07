namespace Aetherphone.Apps.Velvet;

internal enum VelvetPage
{
    Discover,
    Feed,
    Messages,
    Me,
}

internal enum VelvetFeedScope
{
    All,
    Connections,
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
    NotInterested,
    ChatImage,
    ImageView,
    Intro,
    Reactions,
    RequestDetail,
    Filters,
    Search,
    CardPreview,
    PostTags,
    TagPosts,
    EditCaption,
    Encryption,
    UserPosts,
}

internal readonly record struct VelvetView(VelvetScreenId Screen, string? Arg = null)
{
    public static readonly VelvetView Root = new(VelvetScreenId.Root);
    public static readonly VelvetView Compose = new(VelvetScreenId.Compose);
    public static readonly VelvetView EditProfile = new(VelvetScreenId.EditProfile);
    public static readonly VelvetView Settings = new(VelvetScreenId.Settings);
    public static readonly VelvetView Activity = new(VelvetScreenId.Activity);
    public static readonly VelvetView Blocked = new(VelvetScreenId.Blocked);
    public static readonly VelvetView NotInterested = new(VelvetScreenId.NotInterested);
    public static readonly VelvetView Filters = new(VelvetScreenId.Filters);
    public static readonly VelvetView Search = new(VelvetScreenId.Search);
    public static readonly VelvetView CardPreview = new(VelvetScreenId.CardPreview);
    public static readonly VelvetView PostTags = new(VelvetScreenId.PostTags);
    public static readonly VelvetView Encryption = new(VelvetScreenId.Encryption);

    public static VelvetView Profile(string userId) => new(VelvetScreenId.Profile, userId);
    public static VelvetView Thread(string userId) => new(VelvetScreenId.Thread, userId);
    public static VelvetView PostDetail(string postId) => new(VelvetScreenId.PostDetail, postId);
    public static VelvetView Likers(string postId) => new(VelvetScreenId.Likers, postId);

    public static VelvetView EditCaption(string postId) => new(VelvetScreenId.EditCaption, postId);
    public static VelvetView ChatImage(string userId) => new(VelvetScreenId.ChatImage, userId);
    public static VelvetView ImageView(string messageId) => new(VelvetScreenId.ImageView, messageId);
    public static VelvetView Intro(string userId) => new(VelvetScreenId.Intro, userId);
    public static VelvetView Reactions(string messageId) => new(VelvetScreenId.Reactions, messageId);
    public static VelvetView RequestDetail(string userId) => new(VelvetScreenId.RequestDetail, userId);
    public static VelvetView UserPosts(string userId) => new(VelvetScreenId.UserPosts, userId);
    public static VelvetView TagPosts(string token) => new(VelvetScreenId.TagPosts, token);
}

internal enum VelvetMessagesTab
{
    Chats,
    Requests,
}
