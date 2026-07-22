namespace Aetherphone.Core.Emulation;

/// <summary>
/// Boundary reserved for local and network link-cable transports. The first emulator release uses
/// the null transport; gpSP netpacket bridging can be added without changing the app/session API.
/// </summary>
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
