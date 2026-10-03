namespace Aetherphone.Core.SystemMedia;

internal ref struct HString
{
    public nint Handle { get; private set; }

    public HString(string value)
    {
        Handle = WinRt.CreateString(value);
    }

    public void Dispose()
    {
        WinRt.DeleteString(Handle);
        Handle = 0;
    }
}
