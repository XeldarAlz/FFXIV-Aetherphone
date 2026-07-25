namespace Aetherphone.Core;

internal sealed class RealtimeSignalBus
{
    private volatile bool realtimeActive;

    public event Action? ChatPinged;
    public event Action? VelvetPinged;
    public event Action? GramPinged;
    public event Action? SocialPinged;
    public event Action? MusterPinged;
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

    public void PublishMuster()
    {
        MusterPinged?.Invoke();
    }
}
