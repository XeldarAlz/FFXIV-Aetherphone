namespace Aetherphone.Apps.Feedback;

internal enum FeedbackScreen : byte
{
    Hub,
    Compose,
    Photos,
    Sent,
    History,
    Detail,
}

internal readonly record struct FeedbackRoute(FeedbackScreen Screen, string? Id = null)
{
    public static readonly FeedbackRoute Hub = new(FeedbackScreen.Hub);
    public static readonly FeedbackRoute Compose = new(FeedbackScreen.Compose);
    public static readonly FeedbackRoute Photos = new(FeedbackScreen.Photos);
    public static readonly FeedbackRoute Sent = new(FeedbackScreen.Sent);
    public static readonly FeedbackRoute History = new(FeedbackScreen.History);

    public static FeedbackRoute Detail(string feedbackId) => new(FeedbackScreen.Detail, feedbackId);
}
