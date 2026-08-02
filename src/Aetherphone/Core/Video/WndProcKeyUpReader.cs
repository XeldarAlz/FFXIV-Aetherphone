using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Video;

//Reading KeyUp Events from the window itself since Dalamud is consuming the entire KeyState when disabling KeyDown
internal class WndProcKeyUpReader : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_KEYUP = 0x0101;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly Hook<WndProcDelegate>? _hook;
    private readonly HashSet<int> _releasedKeys = new();
    private readonly Lock _lock = new();

    public WndProcKeyUpReader(IntPtr hwnd, IGameInteropProvider interop)
    {
        IntPtr wndProcAddr = GetWindowLongPtrW(hwnd, GWLP_WNDPROC);
        if (wndProcAddr == IntPtr.Zero) { return; }

        _hook = interop.HookFromAddress<WndProcDelegate>(wndProcAddr, WndProcDetour);
        _hook.Enable();
    }

    private IntPtr WndProcDetour(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_KEYUP)
        {
            lock (_lock) { _releasedKeys.Add((int)(wParam.ToInt64() & 0xFFFF)); }
        }
        return _hook!.Original(hWnd, msg, wParam, lParam);
    }

    public HashSet<int> Consume()
    {
        lock (_lock)
        {
            var result = _releasedKeys.ToHashSet();
            _releasedKeys.Clear();
            return result;
        }
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        GC.SuppressFinalize(this);
    }
}