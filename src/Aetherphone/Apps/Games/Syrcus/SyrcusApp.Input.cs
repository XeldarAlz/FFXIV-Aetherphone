using Aetherphone.Core;
using Aetherphone.Core.Emulation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.Syrcus;

internal sealed partial class SyrcusApp
{
    private static readonly (GamepadButtons Pad, EmulatorButtons Button)[] PadBindings =
    {
        (GamepadButtons.DpadUp, EmulatorButtons.Up),
        (GamepadButtons.DpadDown, EmulatorButtons.Down),
        (GamepadButtons.DpadLeft, EmulatorButtons.Left),
        (GamepadButtons.DpadRight, EmulatorButtons.Right),
        (GamepadButtons.East, EmulatorButtons.A),
        (GamepadButtons.South, EmulatorButtons.B),
        (GamepadButtons.North, EmulatorButtons.X),
        (GamepadButtons.West, EmulatorButtons.Y),
        (GamepadButtons.L1, EmulatorButtons.L),
        (GamepadButtons.R1, EmulatorButtons.R),
        (GamepadButtons.L2, EmulatorButtons.L2),
        (GamepadButtons.R2, EmulatorButtons.R2),
        (GamepadButtons.L3, EmulatorButtons.L3),
        (GamepadButtons.R3, EmulatorButtons.R3),
        (GamepadButtons.Start, EmulatorButtons.Start),
        (GamepadButtons.Select, EmulatorButtons.Select),
    };

    private EmulatorButtons KeyboardInput()
    {
        var settings = Settings;
        var result = EmulatorButtons.None;
        for (var index = 0; index < BindingOrder.Length; index++)
        {
            var button = BindingOrder[index];
            if (keyboardClaimed && EmulatorKeyCatalog.IsDown(settings.KeyFor(button)))
            {
                result |= button;
            }
        }

        return result;
    }

    private EmulatorButtons GamepadInput()
    {
        var result = EmulatorButtons.None;
        for (var index = 0; index < PadBindings.Length; index++)
        {
            if (gamepadState.Raw(PadBindings[index].Pad) > 0.5f)
            {
                result |= PadBindings[index].Button;
            }
        }

        if (CurrentSystem.InputProfile is EmulatorInputProfile.Nintendo64 or EmulatorInputProfile.PlayStation)
        {
            return result;
        }

        var leftStick = gamepadState.LeftStick;
        if (leftStick.Y > 0.5f)
        {
            result |= EmulatorButtons.Up;
        }

        if (leftStick.Y < -0.5f)
        {
            result |= EmulatorButtons.Down;
        }

        if (leftStick.X < -0.5f)
        {
            result |= EmulatorButtons.Left;
        }

        if (leftStick.X > 0.5f)
        {
            result |= EmulatorButtons.Right;
        }

        return result;
    }

    private EmulatorInputState BuildInputState(EmulatorButtons buttons, Vector2 touchCButtons,
        Vector2 touchLeftAnalog, Vector2 touchRightAnalog, Rect imageRect)
    {
        var profile = CurrentSystem.InputProfile;
        if (profile == EmulatorInputProfile.NintendoDs)
        {
            var pointer = ReadPointerInput(imageRect);
            var touchJoystick = gamepadState.RightStick;
            return new EmulatorInputState(buttons,
                RightX: ToAnalog(touchJoystick.X), RightY: ToAnalog(-touchJoystick.Y),
                PointerX: pointer.X, PointerY: pointer.Y, PointerPressed: pointer.Pressed);
        }

        if (profile is not EmulatorInputProfile.Nintendo64 and not EmulatorInputProfile.PlayStation)
        {
            return new EmulatorInputState(buttons);
        }

        var left = gamepadState.LeftStick;
        if (AnalogIsIdle(left) && !AnalogIsIdle(touchLeftAnalog))
        {
            left = touchLeftAnalog;
        }

        if (profile == EmulatorInputProfile.Nintendo64)
        {
            var logicalButtons = buttons;
            buttons &= ~(EmulatorButtons.A | EmulatorButtons.B);
            if ((logicalButtons & EmulatorButtons.A) != 0)
            {
                buttons |= EmulatorButtons.B;
            }

            if ((logicalButtons & EmulatorButtons.B) != 0)
            {
                buttons |= EmulatorButtons.Y;
            }

            if (MathF.Abs(left.X) < 0.2f)
            {
                left.X = (buttons & EmulatorButtons.Left) != 0 ? -1f :
                    (buttons & EmulatorButtons.Right) != 0 ? 1f : 0f;
            }

            if (MathF.Abs(left.Y) < 0.2f)
            {
                left.Y = (buttons & EmulatorButtons.Up) != 0 ? 1f :
                    (buttons & EmulatorButtons.Down) != 0 ? -1f : 0f;
            }

            buttons &= ~(EmulatorButtons.Up | EmulatorButtons.Down | EmulatorButtons.Left | EmulatorButtons.Right);
        }

        var right = gamepadState.RightStick;
        if (profile == EmulatorInputProfile.PlayStation && AnalogIsIdle(right) &&
            !AnalogIsIdle(touchRightAnalog))
        {
            right = touchRightAnalog;
        }

        if (profile == EmulatorInputProfile.Nintendo64)
        {
            var keyboardC = KeyboardCButtons();
            if (MathF.Abs(right.X) < 0.2f)
            {
                right.X = Math.Clamp(keyboardC.X + touchCButtons.X, -1f, 1f);
            }

            if (MathF.Abs(right.Y) < 0.2f)
            {
                right.Y = -Math.Clamp(keyboardC.Y + touchCButtons.Y, -1f, 1f);
            }
        }

        return new EmulatorInputState(buttons, ToAnalog(left.X), ToAnalog(-left.Y),
            ToAnalog(right.X), ToAnalog(-right.Y));
    }

    private (short X, short Y, bool Pressed) ReadPointerInput(Rect imageRect)
    {
        if (CurrentSystem.InputProfile != EmulatorInputProfile.NintendoDs || imageRect.Width <= 1f ||
            imageRect.Height <= 1f)
        {
            return default;
        }

        var mouse = ImGui.GetMousePos();
        var inside = ImGui.IsMouseHoveringRect(imageRect.Min, imageRect.Max, false);
        var pressed = inside && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var normalizedX = Math.Clamp((mouse.X - imageRect.Min.X) / imageRect.Width, 0f, 1f);
        var localY = Math.Clamp((mouse.Y - imageRect.Min.Y) / imageRect.Height, 0f, 1f);
        var normalizedY = 0.5f + localY * 0.5f;
        return (ToPointer(normalizedX), ToPointer(normalizedY), pressed);
    }

    private static short ToPointer(float normalized) =>
        (short)MathF.Round(Math.Clamp(normalized, 0f, 1f) * 65534f - 32767f);

    private Vector2 KeyboardCButtons()
    {
        var result = Vector2.Zero;
        if (!keyboardClaimed)
        {
            return result;
        }

        if (EmulatorKeyCatalog.IsDown(Settings.KeyCUp))
        {
            result.Y -= 1f;
        }

        if (EmulatorKeyCatalog.IsDown(Settings.KeyCDown))
        {
            result.Y += 1f;
        }

        if (EmulatorKeyCatalog.IsDown(Settings.KeyCLeft))
        {
            result.X -= 1f;
        }

        if (EmulatorKeyCatalog.IsDown(Settings.KeyCRight))
        {
            result.X += 1f;
        }

        return result;
    }

    private static bool AnalogIsIdle(Vector2 value) => value.LengthSquared() < 0.04f;

    private static short ToAnalog(float value) =>
        (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);

    private void SetInputCaptured(bool captured)
    {
        if (inputCaptured == captured)
        {
            return;
        }

        inputCaptured = captured;
        var io = ImGui.GetIO();
        if (captured)
        {
            gamepadNavigationWasEnabled = (io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) != 0;
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            return;
        }

        if (!gamepadNavigationWasEnabled)
        {
            io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
        }

        gamepadNavigationWasEnabled = false;
    }
}
