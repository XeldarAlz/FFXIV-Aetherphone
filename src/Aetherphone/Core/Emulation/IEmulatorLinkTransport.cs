namespace Aetherphone.Core.Emulation;

internal interface IEmulatorLinkTransport : IDisposable
{
    bool IsConnected { get; }
    void Pump();
    void Reset();
}

internal sealed class NullEmulatorLinkTransport : IEmulatorLinkTransport
{
    public static readonly NullEmulatorLinkTransport Instance = new();
    public bool IsConnected => false;
    public void Pump()
    {
    }

    public void Reset()
    {
    }

    public void Dispose()
    {
    }
}
