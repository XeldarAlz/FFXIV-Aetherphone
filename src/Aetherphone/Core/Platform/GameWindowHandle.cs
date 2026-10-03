using KernelDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device;

namespace Aetherphone.Core.Platform;

internal static class GameWindowHandle
{
    public static unsafe nint Current
    {
        get
        {
            var device = KernelDevice.Instance();
            return device == null ? 0 : (nint)device->hWnd;
        }
    }
}
