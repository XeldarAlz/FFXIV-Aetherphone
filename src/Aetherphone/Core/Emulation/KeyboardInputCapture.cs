using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aetherphone.Core.Emulation;

/// <summary>
/// Captures keyboard input for the emulator before the FFXIV window can turn it into game or chat input.
/// The hook is limited to the game's UI thread; it never affects other applications.
/// </summary>
internal sealed class KeyboardInputCapture : IDisposable
{
    private const int WhGetMessage = 3;
    private const uint WmNull = 0x0000;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmChar = 0x0102;
    private const uint WmDeadChar = 0x0103;
    private const uint WmUniChar = 0x0109;
    private const uint WmImeStartComposition = 0x010D;
    private const uint WmImeEndComposition = 0x010E;
    private const uint WmImeComposition = 0x010F;

    private readonly HookProcedure hookProcedure;
    private nint hookHandle;
    private nint windowHandle;
    private bool captured;
    private bool disposed;

    public KeyboardInputCapture()
    {
        hookProcedure = FilterMessage;
    }

    public void SetCaptured(bool value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (value && !EnsureInstalled())
        {
            Volatile.Write(ref captured, false);
            return;
        }

        Volatile.Write(ref captured, value);
    }

    public bool IsKeyDown(int virtualKey)
    {
        if (!Volatile.Read(ref captured) || windowHandle == 0 || GetForegroundWindow() != windowHandle)
        {
            return false;
        }

        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private bool EnsureInstalled()
    {
        if (hookHandle != 0)
        {
            return true;
        }

        windowHandle = ResolveGameWindow();
        if (windowHandle == 0)
        {
            AepLog.Warning("[Emulator] Could not find the FFXIV window for keyboard capture.");
            return false;
        }

        var threadId = GetWindowThreadProcessId(windowHandle, out _);
        if (threadId == 0)
        {
            AepLog.Warning($"[Emulator] Could not find the FFXIV window thread: {Marshal.GetLastWin32Error()}.");
            return false;
        }

        hookHandle = SetWindowsHookExW(WhGetMessage, hookProcedure, 0, threadId);
        if (hookHandle != 0)
        {
            return true;
        }

        AepLog.Warning($"[Emulator] Could not install keyboard capture: {Marshal.GetLastWin32Error()}.");
        windowHandle = 0;
        return false;
    }

    private static nint ResolveGameWindow()
    {
        var mainWindow = Process.GetCurrentProcess().MainWindowHandle;
        if (mainWindow != 0)
        {
            return mainWindow;
        }

        var foreground = GetForegroundWindow();
        if (foreground == 0)
        {
            return 0;
        }

        GetWindowThreadProcessId(foreground, out var processId);
        return processId == Environment.ProcessId ? foreground : 0;
    }

    private unsafe nint FilterMessage(int code, nuint parameter, nint messagePointer)
    {
        if (code >= 0 && messagePointer != 0 && Volatile.Read(ref captured))
        {
            var message = (NativeMessage*)messagePointer;
            if (IsKeyboardMessage(message->Message))
            {
                message->Message = WmNull;
                message->WParam = 0;
                message->LParam = 0;
            }
        }

        return CallNextHookEx(hookHandle, code, parameter, messagePointer);
    }

    internal static bool IsKeyboardMessage(uint message) => message is
        WmKeyDown or WmKeyUp or WmChar or WmDeadChar or WmUniChar or
        WmImeStartComposition or WmImeEndComposition or WmImeComposition;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Volatile.Write(ref captured, false);
        disposed = true;
        var installedHook = Interlocked.Exchange(ref hookHandle, 0);
        if (installedHook != 0 && !UnhookWindowsHookEx(installedHook))
        {
            AepLog.Warning($"[Emulator] Could not remove keyboard capture: {Marshal.GetLastWin32Error()}.");
        }

        windowHandle = 0;
        GC.KeepAlive(hookProcedure);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProcedure(int code, nuint parameter, nint messagePointer);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int hookId, HookProcedure procedure, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nuint parameter, nint messagePointer);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
