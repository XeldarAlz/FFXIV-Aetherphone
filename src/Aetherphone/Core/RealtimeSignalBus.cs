namespace Aetherphone.Core;

internal sealed class RealtimeSignalBus
{
    public event Action? ChatPinged;
    public event Action? VelvetPinged;
    public event Action? SocialPinged;

    public void PublishChat()
    {
        ChatPinged?.Invoke();
    }

    public void PublishVelvet()
    {
        VelvetPinged?.Invoke();
    }

    public void PublishSocial()
    {
        SocialPinged?.Invoke();
    }
}
