using System.Runtime.InteropServices;

namespace Aetherphone.Core.Platform;

internal static class GameWindowFocus
{
    private const long RecheckMilliseconds = 250;

    private static long checkedAtTicks = long.MinValue;
    private static bool focused = true;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    internal static bool IsFocused
    {
        get
        {
            var now = Environment.TickCount64;
            if (checkedAtTicks != long.MinValue && now - checkedAtTicks < RecheckMilliseconds)
            {
                return focused;
            }

            checkedAtTicks = now;
            focused = ReadFocused();
            return focused;
        }
    }

    private static bool ReadFocused()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == nint.Zero)
            {
                return false;
            }

            _ = GetWindowThreadProcessId(window, out var processId);
            return processId == (uint)Environment.ProcessId;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
