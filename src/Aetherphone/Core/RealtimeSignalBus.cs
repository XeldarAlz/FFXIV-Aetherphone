namespace Aetherphone.Core;

internal sealed class RealtimeSignalBus
{
    private volatile bool realtimeActive;

    public event Action? ChatPinged;
    public event Action? VelvetPinged;
    public event Action? GramPinged;
    public event Action? SocialPinged;

    public bool RealtimeActive => realtimeActive;

    public void SetActive(bool active)
    {
        realtimeActive = active;
    }

    public void PublishChat()
    {
        ChatPinged?.Invoke();
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
}
