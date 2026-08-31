using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core;

internal readonly record struct ContentRemovalSignal(string? App, string? Kind, string ContentId, string? ParentId);

internal readonly record struct ChatSignal(string? ConversationId, ChatMessageDto? Message);

internal readonly record struct CasinoSignal(string Type, string? Reason, CasinoPayload? Payload);

internal readonly record struct GameSignal(string Type, string? Reason, GamePayload? Payload);

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
    public event Action? SocialPinged;
    public event Action? MusterPinged;
    public event Action? AnnouncementsPinged;
    public event Action? PollsPinged;
    public event Action<ContentRemovalSignal>? ContentRemoved;
    public event Action<CasinoSignal>? CasinoReceived;
    public event Action<GameSignal>? GameReceived;
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

    public void PublishSocial()
    {
        SocialPinged?.Invoke();
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
