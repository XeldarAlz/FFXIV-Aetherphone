using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core;

internal readonly record struct ContentRemovalSignal(string? App, string? Kind, string ContentId, string? ParentId);

internal readonly record struct ChatSignal(string? ConversationId, ChatMessageDto? Message);

internal readonly record struct CasinoSignal(string Type, string? Reason, CasinoPayload? Payload);

internal readonly record struct GameSignal(string Type, string? Reason, GamePayload? Payload);

internal readonly record struct SocialSignal(string? App, string? Kind)
{
    public bool CoversNotifications => Kind is null or SocialSignalKinds.Notification;

    public bool CoversNotices => Kind is null or SocialSignalKinds.Notice;

    public bool CoversApp(string app) => CoversNotifications && (App is null || App == app);
}

internal readonly record struct TypingSignal(string Type, string ThreadId);

internal static class SocialSignalKinds
{
    public const string Notification = "notification";
    public const string Notice = "notice";
}

internal static class ContentRemovalKinds
{
    public const string Post = "post";
    public const string Comment = "comment";
    public const string Story = "story";
}

internal sealed class RealtimeSignalBus
{
    private volatile bool realtimeActive;
    private volatile Action<CallControl>? outbound;

    public event Action<ChatSignal>? ChatPinged;
    public event Action? KeysWentStale;
    public event Action? DeviceLinkRequested;
    public event Action? VelvetPinged;
    public event Action? GramPinged;
    public event Action? AdsPinged;
    public event Action<SocialSignal>? SocialPinged;
    public event Action<TypingSignal>? TypingPinged;
    public event Action? MusterPinged;
    public event Action? AnnouncementsPinged;
    public event Action? PollsPinged;
    public event Action? FeedbackPinged;
    public event Action<ContentRemovalSignal>? ContentRemoved;
    public event Action<CasinoSignal>? CasinoReceived;
    public event Action<GameSignal>? GameReceived;
    public event Action<CallControl>? RadioReceived;
    public event Action<bool>? ConnectedChanged;

    public bool RealtimeActive => realtimeActive;

    public void SetActive(bool active)
    {
        if (realtimeActive == active)
        {
            return;
        }

        realtimeActive = active;
        ConnectedChanged?.Invoke(active);
    }

    public void PublishChat(ChatSignal signal)
    {
        ChatPinged?.Invoke(signal);
    }

    public void PublishKeysStale()
    {
        KeysWentStale?.Invoke();
    }

    public void PublishDeviceLinkRequested()
    {
        DeviceLinkRequested?.Invoke();
    }

    public void PublishVelvet()
    {
        VelvetPinged?.Invoke();
    }

    public void PublishGram()
    {
        GramPinged?.Invoke();
    }

    public void PublishAds()
    {
        AdsPinged?.Invoke();
    }

    public void PublishSocial(SocialSignal signal)
    {
        SocialPinged?.Invoke(signal);
    }

    public void PublishTyping(TypingSignal signal)
    {
        TypingPinged?.Invoke(signal);
    }

    public void PublishMuster()
    {
        MusterPinged?.Invoke();
    }

    public void PublishAnnouncements()
    {
        AnnouncementsPinged?.Invoke();
    }

    public void PublishPolls()
    {
        PollsPinged?.Invoke();
    }

    public void PublishFeedback()
    {
        FeedbackPinged?.Invoke();
    }

    public void PublishContentRemoved(ContentRemovalSignal removal)
    {
        ContentRemoved?.Invoke(removal);
    }

    public void PublishCasino(CasinoSignal signal)
    {
        CasinoReceived?.Invoke(signal);
    }

    public void PublishGame(GameSignal signal)
    {
        GameReceived?.Invoke(signal);
    }

    public void PublishRadio(CallControl signal)
    {
        RadioReceived?.Invoke(signal);
    }

    public void BindSender(Action<CallControl>? sender)
    {
        outbound = sender;
    }

    public bool TrySend(CallControl control)
    {
        var sender = outbound;
        if (sender is null || !realtimeActive)
        {
            return false;
        }

        sender(control);
        return true;
    }
}
