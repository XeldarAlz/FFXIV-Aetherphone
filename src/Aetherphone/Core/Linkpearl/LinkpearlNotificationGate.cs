namespace Aetherphone.Core.Linkpearl;

internal sealed class LinkpearlNotificationGate
{
    private readonly Configuration configuration;
    public event Action? Changed;

    public LinkpearlNotificationGate(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public bool Paused => configuration.LinkpearlNotificationsPaused;

    public bool Toggle()
    {
        SetPaused(!configuration.LinkpearlNotificationsPaused);
        return configuration.LinkpearlNotificationsPaused;
    }

    public void SetPaused(bool value)
    {
        if (configuration.LinkpearlNotificationsPaused == value)
        {
            return;
        }

        configuration.LinkpearlNotificationsPaused = value;
        configuration.Save();
        Changed?.Invoke();
    }
}
