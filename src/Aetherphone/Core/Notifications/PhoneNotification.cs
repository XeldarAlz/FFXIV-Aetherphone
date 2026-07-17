namespace Aetherphone.Core.Notifications;

internal sealed record PhoneNotification(
    string AppId,
    string Title,
    string Body,
    DateTime ReceivedAt,
    Vector4 Accent,
    string? GroupKey = null)
{
    public long Id { get; init; }

    public string? ActorId { get; init; }

    public string? PostId { get; init; }

    public int SocialType { get; init; } = -1;

    public string StackKey => string.IsNullOrEmpty(GroupKey) ? AppId : GroupKey;
}
