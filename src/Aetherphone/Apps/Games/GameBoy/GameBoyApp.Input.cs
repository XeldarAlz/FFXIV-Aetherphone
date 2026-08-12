using System.Reflection;
using Aetherphone.Core;
using Aetherphone.Core.Emulation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.GameBoy;

internal sealed partial class GameBoyApp
{    private EmulatorButtons KeyboardInput()
    {
        var settings = Settings;
        var result = EmulatorButtons.None;
        for (var index = 0; index < BindingOrder.Length; index++)
        {
            var button = BindingOrder[index];
            if (keyboardCapture.IsKeyDown(settings.KeyFor(button)))
            {
                result |= button;
            }
        }

        return result;
    }

    private EmulatorButtons GamepadInput()
    {
        var result = EmulatorButtons.None;
        var leftStick = gamepadState.LeftStick;
        var stickAsDirections = CurrentSystem.InputProfile is not EmulatorInputProfile.Nintendo64 and
            not EmulatorInputProfile.PlayStation and not EmulatorInputProfile.PlayStationPortable;
        if (gamepadState.Raw(GamepadButtons.DpadUp) > 0.5f || stickAsDirections && leftStick.Y > 0.5f)
            result |= EmulatorButtons.Up;
        if (gamepadState.Raw(GamepadButtons.DpadDown) > 0.5f || stickAsDirections && leftStick.Y < -0.5f)
            result |= EmulatorButtons.Down;
        if (gamepadState.Raw(GamepadButtons.DpadLeft) > 0.5f || stickAsDirections && leftStick.X < -0.5f)
            result |= EmulatorButtons.Left;
        if (gamepadState.Raw(GamepadButtons.DpadRight) > 0.5f || stickAsDirections && leftStick.X > 0.5f)
            result |= EmulatorButtons.Right;
        if (gamepadState.Raw(GamepadButtons.East) > 0.5f) result |= EmulatorButtons.A;
        if (gamepadState.Raw(GamepadButtons.South) > 0.5f) result |= EmulatorButtons.B;
        if (gamepadState.Raw(GamepadButtons.North) > 0.5f) result |= EmulatorButtons.X;
        if (gamepadState.Raw(GamepadButtons.West) > 0.5f) result |= EmulatorButtons.Y;
        if (gamepadState.Raw(GamepadButtons.L1) > 0.5f) result |= EmulatorButtons.L;
        if (gamepadState.Raw(GamepadButtons.R1) > 0.5f) result |= EmulatorButtons.R;
        if (gamepadState.Raw(GamepadButtons.L2) > 0.5f) result |= EmulatorButtons.L2;
        if (gamepadState.Raw(GamepadButtons.R2) > 0.5f) result |= EmulatorButtons.R2;
        if (gamepadState.Raw(GamepadButtons.L3) > 0.5f) result |= EmulatorButtons.L3;
        if (gamepadState.Raw(GamepadButtons.R3) > 0.5f) result |= EmulatorButtons.R3;
        if (gamepadState.Raw(GamepadButtons.Start) > 0.5f) result |= EmulatorButtons.Start;
        if (gamepadState.Raw(GamepadButtons.Select) > 0.5f) result |= EmulatorButtons.Select;
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

        if (profile is not EmulatorInputProfile.Nintendo64 and not EmulatorInputProfile.PlayStation and
            not EmulatorInputProfile.PlayStationPortable)
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
            if ((logicalButtons & EmulatorButtons.A) != 0) buttons |= EmulatorButtons.B;
            if ((logicalButtons & EmulatorButtons.B) != 0) buttons |= EmulatorButtons.Y;

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
        if (keyboardCapture.IsKeyDown(Settings.KeyCUp)) result.Y -= 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCDown)) result.Y += 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCLeft)) result.X -= 1f;
        if (keyboardCapture.IsKeyDown(Settings.KeyCRight)) result.X += 1f;
        return result;
    }

    private static bool AnalogIsIdle(Vector2 value) => value.LengthSquared() < 0.04f;

    private static short ToAnalog(float value) =>
        (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);

    private void SuppressGameInput()
    {
        if (!phoneInteractive)
        {
            return;
        }

        var io = ImGui.GetIO();
        io.WantCaptureKeyboard = true;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        keyState.ClearAll();

        if (gamepadNavigationSetter is null)
        {
            io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        }
    }

    private void SetInputCaptured(bool captured)
    {
        if (captured && !phoneInteractive)
        {
            return;
        }

        if (inputCaptured == captured)
        {
            return;
        }

        inputCaptured = captured;
        keyboardCapture.SetCaptured(captured);
        if (captured)
        {
            var io = ImGui.GetIO();
            gamepadCaptureActive = true;
            gamepadUsesImGuiFallback = gamepadNavigationSetter is null;
            if (gamepadUsesImGuiFallback)
            {
                gamepadNavigationWasEnabled =
                    (io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) != 0;
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            }
            else
            {
                gamepadBlockWasEnabled = ReadDalamudGamepadCapture();
                SetDalamudGamepadCapture(true);
            }

            keyState.ClearAll();
            return;
        }

        RestoreGamepadNavigation();
    }

    private void RestoreGamepadNavigation()
    {
        if (!gamepadCaptureActive)
        {
            return;
        }

        if (gamepadUsesImGuiFallback)
        {
            if (!gamepadNavigationWasEnabled)
            {
                var io = ImGui.GetIO();
                io.ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
            }
        }
        else
        {
            SetDalamudGamepadCapture(gamepadBlockWasEnabled);
        }

        gamepadCaptureActive = false;
        gamepadNavigationWasEnabled = false;
        gamepadBlockWasEnabled = false;
        gamepadUsesImGuiFallback = false;
    }

    private bool ReadDalamudGamepadCapture()
    {
        if (gamepadNavigationGetter is null)
        {
            return false;
        }

        try
        {
            return gamepadNavigationGetter(gamepadState);
        }
        catch (Exception exception)
        {
            LogGamepadReflectionFailure(exception);
            return false;
        }
    }

    private void SetDalamudGamepadCapture(bool captured)
    {
        if (gamepadNavigationSetter is null)
        {
            if (!gamepadReflectionWarningLogged)
            {
                gamepadReflectionWarningLogged = true;
                AepLog.Warning("[Emulator] Dalamud gamepad capture property was not found; " +
                               "falling back to ImGuiConfigFlags.NavEnableGamepad.");
            }

            return;
        }

        try
        {
            gamepadNavigationSetter(gamepadState, captured);
        }
        catch (Exception exception)
        {
            LogGamepadReflectionFailure(exception);
        }
    }

    private void LogGamepadReflectionFailure(Exception exception)
    {
        if (gamepadReflectionWarningLogged)
        {
            return;
        }

        gamepadReflectionWarningLogged = true;
        AepLog.Warning($"[Emulator] Could not synchronize Dalamud gamepad capture: {exception.Message}");
    }

    private static Action<object, bool>? CreateSetter(PropertyInfo? property)
    {
        var method = property?.SetMethod;
        if (method?.DeclaringType is null)
        {
            return null;
        }

        try
        {
            var factory = typeof(GameBoyApp).GetMethod(nameof(CreateSetter), BindingFlags.Static |
                BindingFlags.NonPublic, null, new[] { typeof(MethodInfo) }, null);
            return (Action<object, bool>?)factory?.MakeGenericMethod(method.DeclaringType).Invoke(null,
                new object[] { method });
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] Could not bind Dalamud gamepad capture setter: {exception.Message}");
            return null;
        }
    }

    private static Action<object, bool> CreateSetter<T>(MethodInfo method)
    {
        var setter = (Action<T, bool>)Delegate.CreateDelegate(typeof(Action<T, bool>), method);
        return (target, value) => setter((T)target, value);
    }

    private static Func<object, bool>? CreateGetter(PropertyInfo? property)
    {
        var method = property?.GetMethod;
        if (method?.DeclaringType is null)
        {
            return null;
        }

        try
        {
            var factory = typeof(GameBoyApp).GetMethod(nameof(CreateGetter), BindingFlags.Static |
                BindingFlags.NonPublic, null, new[] { typeof(MethodInfo) }, null);
            return (Func<object, bool>?)factory?.MakeGenericMethod(method.DeclaringType).Invoke(null,
                new object[] { method });
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Emulator] Could not bind Dalamud gamepad capture getter: {exception.Message}");
            return null;
        }
    }

    private static Func<object, bool> CreateGetter<T>(MethodInfo method)
    {
        var getter = (Func<T, bool>)Delegate.CreateDelegate(typeof(Func<T, bool>), method);
        return target => getter((T)target);
    }

    private void SetPhoneInteractive(bool interactive)
    {
        phoneInteractive = interactive;
        if (!interactive)
        {
            SetInputCaptured(false);
            return;
        }

        if (session is not null && gameVisible)
        {
            SetInputCaptured(true);
        }
    }

}
