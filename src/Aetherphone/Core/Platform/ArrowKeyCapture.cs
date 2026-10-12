using System.Runtime.InteropServices;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Platform;

internal static class ArrowKeyCapture
{
    private const long ArmedMilliseconds = 200;
    private const long ReleaseDelayMilliseconds = 2000;
    private const long RepeatDelayMilliseconds = 350;
    private const long RepeatIntervalMilliseconds = 150;
    private const short PhysicallyDown = unchecked((short)0x8000);

    private static long lastArmTick;
    private static bool hooked;
    private static int pending;
    private static int heldDirection;
    private static long nextRepeatTick;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public static int Take()
    {
        if (!GameFocus.Active || ImGui.GetIO().WantTextInput || UiInteract.InputBlocked || InputShield.Active)
        {
            pending = 0;
            return 0;
        }

        lastArmTick = Environment.TickCount64;
        if (!hooked)
        {
            hooked = true;
            Plugin.Framework.Update += CaptureBeforeGame;
        }

        var direction = pending;
        pending = 0;
        return direction;
    }

    private static void CaptureBeforeGame(IFramework framework)
    {
        var now = Environment.TickCount64;
        var idle = now - lastArmTick;
        if (idle > ReleaseDelayMilliseconds)
        {
            hooked = false;
            Plugin.Framework.Update -= CaptureBeforeGame;
        }

        if (idle > ArmedMilliseconds)
        {
            pending = 0;
            heldDirection = 0;
            return;
        }

        var keyState = Plugin.KeyState;
        var pressed = keyState[VirtualKey.DOWN] ? 1 : keyState[VirtualKey.UP] ? -1 : 0;
        GameKeys.Swallow(VirtualKey.UP);
        GameKeys.Swallow(VirtualKey.DOWN);
        if (pressed != 0 && pressed != heldDirection)
        {
            pending = pressed;
            heldDirection = pressed;
            nextRepeatTick = now + RepeatDelayMilliseconds;
            return;
        }

        if (heldDirection == 0 || !HeldDown(heldDirection))
        {
            heldDirection = 0;
            return;
        }

        if (now >= nextRepeatTick)
        {
            pending = heldDirection;
            nextRepeatTick = now + RepeatIntervalMilliseconds;
        }
    }

    private static bool HeldDown(int direction) =>
        GameWindowFocus.IsFocused &&
        (GetAsyncKeyState((int)(direction > 0 ? VirtualKey.DOWN : VirtualKey.UP)) & PhysicallyDown) != 0;
}
