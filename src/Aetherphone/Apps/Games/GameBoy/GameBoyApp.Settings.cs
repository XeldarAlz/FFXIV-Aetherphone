using System.Reflection;
using Aetherphone.Apps.Games.Framework;
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
{    private void DrawEmulatorSettings(PhoneTheme theme, float scale)
    {
        var settings = Settings;
        var changed = false;
        changed |= DrawCoreSpecificSettings(CurrentSystem, settings, theme);

        SettingsSection.Header(Loc.T(L.Games.Video), theme);
        var videoCard = GroupCard.Begin(theme, 2, SettingsRowHeight);
        var filter = DrawLabeledSegments("gameboy.filter", videoCard.NextRow(), Loc.T(L.Games.VideoFilter),
            new[]
            {
                Loc.T(L.Games.FilterPixel), Loc.T(L.Games.FilterBalanced), Loc.T(L.Games.FilterSharp),
                Loc.T(L.Games.FilterSmooth),
            },
            VideoFilterIndex(settings.VideoFilter), theme);
        var nextFilter = filter switch
        {
            0 => EmulatorVideoFilter.Pixel,
            1 => EmulatorVideoFilter.Balanced,
            2 => EmulatorVideoFilter.Sharp,
            _ => EmulatorVideoFilter.Smooth,
        };
        if (nextFilter != settings.VideoFilter)
        {
            settings.VideoFilter = nextFilter;
            changed = true;
        }

        var orientation = DrawLabeledSegments("gameboy.orientation", videoCard.NextRow(),
            Loc.T(L.Games.Orientation), new[] { Loc.T(L.Games.Portrait), Loc.T(L.Games.Landscape) },
            settings.GameplayOrientation == EmulatorGameplayOrientation.Landscape ? 1 : 0, theme);
        var nextOrientation = orientation == 1
            ? EmulatorGameplayOrientation.Landscape
            : EmulatorGameplayOrientation.Portrait;
        if (nextOrientation != settings.GameplayOrientation)
        {
            settings.GameplayOrientation = nextOrientation;
            changed = true;
        }

        videoCard.End();
        SettingsSection.Hint(Loc.T(L.Games.PixelFilterHint), theme);

        SettingsSection.Header(Loc.T(L.Games.FastForward), theme);
        var fastForwardCard = GroupCard.Begin(theme, 1, SettingsRowHeight);
        var fastForwardSpeed = DrawLabeledSegments("gameboy.fastForwardSpeed", fastForwardCard.NextRow(),
            Loc.T(L.Games.FastForwardSpeed), new[] { "2x", "3x", "4x" },
            Math.Clamp(settings.FastForwardSpeed, 2, 4) - 2, theme) + 2;
        if (fastForwardSpeed != settings.FastForwardSpeed)
        {
            settings.FastForwardSpeed = fastForwardSpeed;
            changed = true;
        }

        fastForwardCard.End();
        SettingsSection.Hint(Loc.T(L.Games.FastForwardHint), theme);

        SettingsSection.Header(Loc.T(L.Games.SaveStates), theme);
        var autoStateCard = GroupCard.Begin(theme, 2);
        var autoSave = SettingsRow.Bool(autoStateCard.NextRow(), Loc.T(L.Games.AutoSaveState),
            settings.AutoSaveState, theme);
        var autoLoad = SettingsRow.Bool(autoStateCard.NextRow(), Loc.T(L.Games.AutoLoadState),
            settings.AutoLoadState, theme);
        autoStateCard.End();
        if (autoSave != settings.AutoSaveState)
        {
            settings.AutoSaveState = autoSave;
            changed = true;
        }

        if (autoLoad != settings.AutoLoadState)
        {
            settings.AutoLoadState = autoLoad;
            changed = true;
        }

        SettingsSection.Hint(Loc.T(L.Games.AutoStateHint), theme);

        SettingsSection.Header(Loc.T(L.Games.EmulatorShortcuts), theme);
        var shortcutCard = GroupCard.Begin(theme, 4);
        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.FastForward),
                ShortcutValue(EmulatorShortcutAction.FastForward), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.FastForward);
        }

        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.SaveState),
                ShortcutValue(EmulatorShortcutAction.SaveState), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.SaveState);
        }

        if (SettingsRow.Disclosure(shortcutCard.NextRow(), Loc.T(L.Games.LoadState),
                ShortcutValue(EmulatorShortcutAction.LoadState), theme))
        {
            BeginShortcutBinding(EmulatorShortcutAction.LoadState);
        }

        if (SettingsRow.Action(shortcutCard.NextRow(), Loc.T(L.Games.ClearShortcuts), theme.TextMuted, theme))
        {
            CancelAllBindings();
            settings.FastForwardShortcut.Clear();
            settings.SaveStateShortcut.Clear();
            settings.LoadStateShortcut.Clear();
            changed = true;
        }

        shortcutCard.End();
        SettingsSection.Hint(Loc.T(L.Games.ShortcutsHint), theme);

        SettingsSection.Header(Loc.T(L.Games.InterfaceLayout), theme);
        var layoutCard = GroupCard.Begin(theme, 2);
        if (SettingsRow.Disclosure(layoutCard.NextRow(), Loc.T(L.Games.EditInterface), string.Empty, theme))
        {
            CancelAllBindings();
            editingLayout = true;
            selectedLayoutElement = IsNintendoDs
                ? EmulatorLayoutElement.DsTopScreen
                : EmulatorLayoutElement.Screen;
        }

        var hideOnScreenControls = SettingsRow.Bool(layoutCard.NextRow(),
            Loc.T(L.Games.HideOnScreenControls), settings.HideOnScreenControls, theme);
        layoutCard.End();
        if (hideOnScreenControls != settings.HideOnScreenControls)
        {
            settings.HideOnScreenControls = hideOnScreenControls;
            activeTouchAnalog = null;
            changed = true;
        }
        SettingsSection.Hint(Loc.T(L.Games.InterfaceLayoutHint), theme);

        SettingsSection.Header(Loc.T(L.Games.KeyboardControls), theme);
        var visibleBindingCount = 0;
        for (var index = 0; index < BindingOrder.Length; index++)
        {
            var button = BindingOrder[index];
            if ((CurrentSystem.Controls & button) != 0)
            {
                visibleBindings[visibleBindingCount++] = button;
            }
        }

        var auxiliaryBindings = CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64 ? 4 : 0;
        var controlsCard = GroupCard.Begin(theme, visibleBindingCount + auxiliaryBindings + 1);
        for (var index = 0; index < visibleBindingCount; index++)
        {
            var button = visibleBindings[index];
            var value = bindingTarget == button
                ? Loc.T(L.Games.PressKey)
                : EmulatorKeyCatalog.Name(settings.KeyFor(button));
            if (SettingsRow.Disclosure(controlsCard.NextRow(), ControlLabel(button), value, theme))
            {
                BeginKeyBinding(button);
            }
        }

        if (CurrentSystem.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CUp, "C Up", settings.KeyCUp,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CDown, "C Down", settings.KeyCDown,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CLeft, "C Left", settings.KeyCLeft,
                theme);
            DrawAuxiliaryBindingRow(controlsCard.NextRow(), EmulatorLayoutElement.CRight, "C Right", settings.KeyCRight,
                theme);
        }

        if (SettingsRow.Action(controlsCard.NextRow(), Loc.T(L.Games.ResetControls), theme.Accent, theme))
        {
            CancelKeyBinding();
            settings.ResetKeys();
            changed = true;
        }

        controlsCard.End();
        SettingsSection.Hint(Loc.T(L.Games.ControlsHint), theme);

        SettingsSection.Header(Loc.T(L.Games.RomFolders), theme);
        var folderCard = GroupCard.Begin(theme, settings.RomFolders.Count + 1);
        var removeFolder = -1;
        for (var index = 0; index < settings.RomFolders.Count; index++)
        {
            var path = settings.RomFolders[index];
            var label = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (string.IsNullOrEmpty(label))
            {
                label = path;
            }

            if (SettingsRow.Disclosure(folderCard.NextRow(), label, Loc.T(L.Games.RemoveFolder), theme))
            {
                removeFolder = index;
            }
        }

        if (SettingsRow.Action(folderCard.NextRow(), Loc.T(L.Games.ScanFolder), Accent, theme))
        {
            OpenFolderBrowser();
        }

        folderCard.End();
        if (removeFolder >= 0)
        {
            settings.RomFolders.RemoveAt(removeFolder);
            RefreshLibrary();
            changed = true;
        }

        SettingsSection.Hint(Loc.T(L.Games.RomFolderHint), theme);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
        if (changed)
        {
            configuration.Save();
        }
    }

    private bool DrawCoreSpecificSettings(EmulatorSystemDefinition system, EmulatorSettings settings,
        PhoneTheme theme)
    {
        var changed = false;
        SettingsSection.Header(Loc.T(L.Games.EmulatorCore), theme);
        var infoCard = GroupCard.Begin(theme, 2);
        SettingsRow.Info(infoCard.NextRow(), Loc.T(L.Games.LibretroCore), system.CoreFileName, theme);
        SettingsRow.Info(infoCard.NextRow(), Loc.T(L.Games.PersistentStorage),
            Loc.T(system.LocalizedSaveDescription), theme);
        infoCard.End();

        if (system.CoreOptions.Count > 0)
        {
            SettingsSection.Header(Loc.T(L.Games.CoreOptions), theme);
            var optionsCard = GroupCard.Begin(theme, system.CoreOptions.Count);
            for (var index = 0; index < system.CoreOptions.Count; index++)
            {
                var option = system.CoreOptions[index];
                var current = settings.CoreOptions.TryGetValue(option.Key, out var configured) &&
                              option.Values.Contains(configured, StringComparer.OrdinalIgnoreCase)
                    ? configured
                    : system.DefaultCoreOptions.TryGetValue(option.Key, out var defaultValue) &&
                      option.Values.Contains(defaultValue, StringComparer.OrdinalIgnoreCase)
                        ? defaultValue
                        : option.Values[0];
                var optionLabel = option.LocalizedLabel is { } localizedLabel
                    ? Loc.T(localizedLabel)
                    : option.Label;
                if (!SettingsRow.Disclosure(optionsCard.NextRow(), optionLabel, option.Display(current, Loc.T), theme))
                {
                    continue;
                }

                var currentIndex = 0;
                for (var valueIndex = 0; valueIndex < option.Values.Count; valueIndex++)
                {
                    if (string.Equals(option.Values[valueIndex], current, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = valueIndex;
                        break;
                    }
                }

                settings.CoreOptions[option.Key] = option.Values[(currentIndex + 1) % option.Values.Count];
                changed = true;
            }

            optionsCard.End();
            SettingsSection.Hint(Loc.T(L.Games.CoreOptionsRestartHint), theme);
        }

        if (system.Firmware.Count > 0)
        {
            SettingsSection.Header(Loc.T(L.Games.BiosFirmware), theme);
            var firmwareCard = GroupCard.Begin(theme, system.Firmware.Count);
            var systemDirectory = Path.Combine(emulatorRoot, "system");
            for (var index = 0; index < system.Firmware.Count; index++)
            {
                var firmware = system.Firmware[index];
                var firmwarePath = Path.Combine(systemDirectory, firmware.FileName);
                var present = File.Exists(firmwarePath) || Directory.Exists(firmwarePath);
                var state = present
                    ? Loc.T(L.Games.FirmwareInstalled)
                    : firmware.Required
                        ? Loc.T(L.Games.FirmwareRequired)
                        : Loc.T(L.Games.FirmwareOptional);
                SettingsRow.Info(firmwareCard.NextRow(), Loc.T(firmware.LocalizedDescription),
                    $"{firmware.FileName} · {state}", theme);
            }

            firmwareCard.End();
            SettingsSection.Hint(Loc.T(L.Games.FirmwarePathHint, systemDirectory), theme);
        }

        if (system.InputProfile == EmulatorInputProfile.PlayStation)
        {
            SettingsSection.Header(Loc.T(L.Games.MemoryCards), theme);
            var memoryCard = GroupCard.Begin(theme, 2);
            SettingsRow.Info(memoryCard.NextRow(), Loc.T(L.Games.Storage), @"saves\ps1", theme);
            var protect = SettingsRow.Bool(memoryCard.NextRow(), Loc.T(L.Games.ProtectMemoryCards),
                settings.ProtectSaveMemoryOnStateLoad, theme);
            memoryCard.End();
            if (protect != settings.ProtectSaveMemoryOnStateLoad)
            {
                settings.ProtectSaveMemoryOnStateLoad = protect;
                changed = true;
            }

            SettingsSection.Hint(Loc.T(L.Games.ProtectMemoryCardsHint), theme);
        }

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            SettingsSection.Hint(Loc.T(L.Games.N64AccessoryHint), theme);
        }

        if (system.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            SettingsSection.Hint(Loc.T(L.Games.DsTouchHint), theme);
        }

        if (system.InputProfile == EmulatorInputProfile.PlayStationPortable)
        {
            SettingsSection.Hint(Loc.T(L.Games.PspSoftwareRendererHint), theme);
        }

        return changed;
    }

    private void DrawAuxiliaryBindingRow(Rect row, EmulatorLayoutElement control, string label, int key,
        PhoneTheme theme)
    {
        var value = auxiliaryBindingTarget == control
            ? Loc.T(L.Games.PressKey)
            : EmulatorKeyCatalog.Name(key);
        if (SettingsRow.Disclosure(row, label, value, theme))
        {
            BeginAuxiliaryKeyBinding(control);
        }
    }

    private static int DrawLabeledSegments(string id, Rect row, string label, IReadOnlyList<string> options,
        int selected, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 7f * scale), label, theme.TextStrong,
            TextStyles.FootnoteEmphasized);
        var segments = new Rect(new Vector2(row.Min.X, row.Min.Y + 29f * scale),
            new Vector2(row.Max.X, row.Max.Y - 7f * scale));
        return SegmentStrip.Draw(id, segments, options, selected, theme);
    }

    private static int VideoFilterIndex(EmulatorVideoFilter filter) => filter switch
    {
        EmulatorVideoFilter.Pixel => 0,
        EmulatorVideoFilter.Balanced => 1,
        EmulatorVideoFilter.Sharp => 2,
        _ => 3,
    };

    private string ControlLabel(EmulatorButtons button)
    {
        if (CurrentSystem.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            return button switch
            {
                EmulatorButtons.L2 => Loc.T(L.Games.DsControlMicrophone),
                EmulatorButtons.R2 => Loc.T(L.Games.DsControlNextLayout),
                EmulatorButtons.L3 => Loc.T(L.Games.DsControlCloseLid),
                EmulatorButtons.R3 => Loc.T(L.Games.DsControlTouchJoystick),
                _ => DefaultControlLabel(button),
            };
        }

        return DefaultControlLabel(button);
    }

    private string DefaultControlLabel(EmulatorButtons button) => button switch
    {
        EmulatorButtons.Up => Loc.T(L.Games.ControlUp),
        EmulatorButtons.Down => Loc.T(L.Games.ControlDown),
        EmulatorButtons.Left => Loc.T(L.Games.ControlLeft),
        EmulatorButtons.Right => Loc.T(L.Games.ControlRight),
        _ => CurrentSystem.ButtonLabel(button),
    };

    private void BeginKeyBinding(EmulatorButtons button)
    {
        CancelShortcutBinding();
        auxiliaryBindingTarget = null;
        bindingTarget = button;
        keyboardCapture.SetCaptured(true);
        Array.Clear(bindingKeyStates);
        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            bindingKeyStates[key] = keyboardCapture.IsKeyDown(key);
        }
    }

    private void ProcessKeyBinding()
    {
        if (bindingTarget == EmulatorButtons.None && auxiliaryBindingTarget is null)
        {
            return;
        }

        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            var down = keyboardCapture.IsKeyDown(key);
            if (down && !bindingKeyStates[key])
            {
                if (key != 0x1B)
                {
                    if (bindingTarget != EmulatorButtons.None)
                    {
                        Settings.SetKey(bindingTarget, key);
                    }
                    else if (auxiliaryBindingTarget is { } auxiliary)
                    {
                        SetAuxiliaryKey(auxiliary, key);
                    }
                    configuration.Save();
                }

                CancelKeyBinding();
                return;
            }

            bindingKeyStates[key] = down;
        }
    }

    private void CancelKeyBinding()
    {
        if (bindingTarget == EmulatorButtons.None && auxiliaryBindingTarget is null)
        {
            return;
        }

        bindingTarget = EmulatorButtons.None;
        auxiliaryBindingTarget = null;
        Array.Clear(bindingKeyStates);
        keyboardCapture.SetCaptured(inputCaptured || shortcutTarget != EmulatorShortcutAction.None);
    }

    private void BeginAuxiliaryKeyBinding(EmulatorLayoutElement control)
    {
        CancelShortcutBinding();
        bindingTarget = EmulatorButtons.None;
        auxiliaryBindingTarget = control;
        keyboardCapture.SetCaptured(true);
        Array.Clear(bindingKeyStates);
        var keys = EmulatorKeyCatalog.SupportedKeys;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            bindingKeyStates[key] = keyboardCapture.IsKeyDown(key);
        }
    }

    private void SetAuxiliaryKey(EmulatorLayoutElement control, int key)
    {
        switch (control)
        {
            case EmulatorLayoutElement.CUp: Settings.KeyCUp = key; break;
            case EmulatorLayoutElement.CDown: Settings.KeyCDown = key; break;
            case EmulatorLayoutElement.CLeft: Settings.KeyCLeft = key; break;
            case EmulatorLayoutElement.CRight: Settings.KeyCRight = key; break;
        }
    }

    private void BeginShortcutBinding(EmulatorShortcutAction action)
    {
        CancelKeyBinding();
        shortcutTarget = action;
        shortcutCaptureKeys.Clear();
        shortcutCaptureButtons = 0;
        shortcutWaitingForRelease = true;
        shortcutHasInput = false;
        keyboardCapture.SetCaptured(true);
    }

    private void ProcessShortcutBinding()
    {
        if (shortcutTarget == EmulatorShortcutAction.None)
        {
            return;
        }

        var keys = EmulatorKeyCatalog.SupportedKeys;
        var anyKeyDown = false;
        var escapeDown = false;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (!keyboardCapture.IsKeyDown(key))
            {
                continue;
            }

            anyKeyDown = true;
            escapeDown |= key == 0x1B;
        }

        var currentButtons = CurrentShortcutGamepadButtons();
        var anyDown = anyKeyDown || currentButtons != 0;
        if (shortcutWaitingForRelease)
        {
            if (!anyDown)
            {
                shortcutWaitingForRelease = false;
            }

            return;
        }

        if (escapeDown)
        {
            CancelShortcutBinding();
            return;
        }

        if (anyDown)
        {
            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                if (key != 0x1B && keyboardCapture.IsKeyDown(key))
                {
                    shortcutCaptureKeys.Add(key);
                }
            }

            shortcutCaptureButtons |= currentButtons;
            shortcutHasInput = shortcutCaptureKeys.Count > 0 || shortcutCaptureButtons != 0;
            return;
        }

        if (!shortcutHasInput)
        {
            return;
        }

        ShortcutFor(shortcutTarget).Set(shortcutCaptureKeys, shortcutCaptureButtons);
        configuration.Save();
        CancelShortcutBinding();
    }

    private void CancelShortcutBinding()
    {
        if (shortcutTarget == EmulatorShortcutAction.None)
        {
            return;
        }

        shortcutTarget = EmulatorShortcutAction.None;
        shortcutCaptureKeys.Clear();
        shortcutCaptureButtons = 0;
        shortcutWaitingForRelease = false;
        shortcutHasInput = false;
        keyboardCapture.SetCaptured(inputCaptured || bindingTarget != EmulatorButtons.None);
    }

    private void CancelAllBindings()
    {
        CancelKeyBinding();
        CancelShortcutBinding();
    }

    private ushort CurrentShortcutGamepadButtons()
    {
        ushort result = 0;
        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if (gamepadState.Raw(button) > 0.5f)
            {
                result |= (ushort)button;
            }
        }

        return result;
    }

    private bool ShortcutIsDown(EmulatorShortcutSettings shortcut)
    {
        if (shortcut.IsEmpty)
        {
            return false;
        }

        for (var index = 0; index < shortcut.Keys.Count; index++)
        {
            if (!keyboardCapture.IsKeyDown(shortcut.Keys[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if ((shortcut.GamepadButtons & (ushort)button) != 0 && gamepadState.Raw(button) <= 0.5f)
            {
                return false;
            }
        }

        return true;
    }

    private string ShortcutValue(EmulatorShortcutAction action)
    {
        if (shortcutTarget == action)
        {
            return Loc.T(L.Games.PressCombination);
        }

        var shortcut = ShortcutFor(action);
        if (shortcut.IsEmpty)
        {
            return Loc.T(L.Games.Unassigned);
        }

        var parts = new List<string>(shortcut.Keys.Count + ShortcutGamepadButtons.Length);
        for (var index = 0; index < shortcut.Keys.Count; index++)
        {
            parts.Add(EmulatorKeyCatalog.Name(shortcut.Keys[index]));
        }

        for (var index = 0; index < ShortcutGamepadButtons.Length; index++)
        {
            var button = ShortcutGamepadButtons[index];
            if ((shortcut.GamepadButtons & (ushort)button) != 0)
            {
                parts.Add(GamepadButtonName(button));
            }
        }

        return string.Join(" + ", parts);
    }

    private EmulatorShortcutSettings ShortcutFor(EmulatorShortcutAction action) => action switch
    {
        EmulatorShortcutAction.FastForward => Settings.FastForwardShortcut,
        EmulatorShortcutAction.SaveState => Settings.SaveStateShortcut,
        EmulatorShortcutAction.LoadState => Settings.LoadStateShortcut,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static string GamepadButtonName(GamepadButtons button) => button switch
    {
        GamepadButtons.DpadUp => "D-pad Up",
        GamepadButtons.DpadDown => "D-pad Down",
        GamepadButtons.DpadLeft => "D-pad Left",
        GamepadButtons.DpadRight => "D-pad Right",
        GamepadButtons.North => "Pad North",
        GamepadButtons.South => "Pad South",
        GamepadButtons.West => "Pad West",
        GamepadButtons.East => "Pad East",
        _ => button.ToString(),
    };

}
