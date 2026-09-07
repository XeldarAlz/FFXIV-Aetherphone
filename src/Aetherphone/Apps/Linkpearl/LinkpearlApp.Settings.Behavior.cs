using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const string ModifierMenuId = "linkpearl.settings.hotkeyModifier";
    private const string KeyMenuId = "linkpearl.settings.hotkeyKey";

    private static readonly VirtualKey[] HotkeyModifiers =
    {
        VirtualKey.NO_KEY, VirtualKey.CONTROL, VirtualKey.MENU, VirtualKey.SHIFT,
    };

    private static readonly VirtualKey[] HotkeyKeys =
    {
        VirtualKey.F1, VirtualKey.F2, VirtualKey.F3, VirtualKey.F4, VirtualKey.F5, VirtualKey.F6,
        VirtualKey.F7, VirtualKey.F8, VirtualKey.F9, VirtualKey.F10, VirtualKey.F11, VirtualKey.F12,
    };

    private static readonly string[] HotkeyKeyLabels =
    {
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
    };

    private void DrawBehaviorSettings(float scale)
    {
        DrawPresenceSettings(scale);
        DrawOpenChatSettings();
    }

    private void DrawPresenceSettings(float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PresenceSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 4);
        var hideInCombat = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.HideInCombat),
            configuration.LinkpearlPopoutHideInCombat, frameTheme, "linkpearl.settings.hideInCombat");
        if (hideInCombat != configuration.LinkpearlPopoutHideInCombat)
        {
            configuration.LinkpearlPopoutHideInCombat = hideInCombat;
            configuration.Save();
        }

        var hideInDuty = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.HideInDuty),
            configuration.LinkpearlPopoutHideInDuty, frameTheme, "linkpearl.settings.hideInDuty");
        if (hideInDuty != configuration.LinkpearlPopoutHideInDuty)
        {
            configuration.LinkpearlPopoutHideInDuty = hideInDuty;
            configuration.Save();
        }

        var fieldOperations = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.FieldOperationsStayOpen),
            configuration.LinkpearlPopoutFieldOperationsExempt, frameTheme, "linkpearl.settings.fieldOperations",
            Loc.T(L.Linkpearl.FieldOperationsHint), !configuration.LinkpearlPopoutHideInDuty);
        if (fieldOperations != configuration.LinkpearlPopoutFieldOperationsExempt)
        {
            configuration.LinkpearlPopoutFieldOperationsExempt = fieldOperations;
            configuration.Save();
        }

        var reopen = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.ReopenAfterCombat),
            configuration.LinkpearlPopoutReopenAfterCombat, frameTheme, "linkpearl.settings.reopenAfterCombat",
            Loc.T(L.Linkpearl.ReopenAfterCombatHint));
        if (reopen != configuration.LinkpearlPopoutReopenAfterCombat)
        {
            configuration.LinkpearlPopoutReopenAfterCombat = reopen;
            configuration.Save();
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.PresenceHint), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawOpenChatSettings()
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.OpenChatSection), frameTheme);
        var card = GroupCard.Begin(frameTheme, 3);
        var hotkeyEnabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.HotkeyEnabled),
            configuration.LinkpearlHotkeyEnabled, frameTheme, "linkpearl.settings.hotkeyEnabled");
        if (hotkeyEnabled != configuration.LinkpearlHotkeyEnabled)
        {
            configuration.LinkpearlHotkeyEnabled = hotkeyEnabled;
            SeedHotkeyChord(hotkeyEnabled);
            configuration.Save();
        }

        var dimmed = !configuration.LinkpearlHotkeyEnabled;
        var modifierRow = card.NextRow();
        if (SettingsRow.Disclosure(modifierRow, Loc.T(L.Linkpearl.HotkeyModifier),
                ModifierLabel(configuration.LinkpearlHotkeyModifier), frameTheme, ModifierMenuId, dimmed))
        {
            settingsMenu.Toggle(ModifierMenuId, modifierRow);
        }

        var keyRow = card.NextRow();
        if (SettingsRow.Disclosure(keyRow, Loc.T(L.Linkpearl.HotkeyKey), KeyLabel(configuration.LinkpearlHotkeyKey),
                frameTheme, KeyMenuId, dimmed))
        {
            settingsMenu.Toggle(KeyMenuId, keyRow);
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Linkpearl.HotkeyHint), frameTheme);
        DrawHotkeyMenus();
    }

    private void SeedHotkeyChord(bool enabled)
    {
        if (!enabled || configuration.LinkpearlHotkeyKey != (int)VirtualKey.NO_KEY)
        {
            return;
        }

        configuration.LinkpearlHotkeyModifier = (int)VirtualKey.CONTROL;
        configuration.LinkpearlHotkeyKey = (int)VirtualKey.F9;
    }

    private void DrawHotkeyMenus()
    {
        var modifierOpen = settingsMenu.IsOpenFor(ModifierMenuId);
        if (!modifierOpen && !settingsMenu.IsOpenFor(KeyMenuId))
        {
            return;
        }

        var origin = ImGui.GetWindowPos();
        var surface = new Rect(origin, origin + ImGui.GetWindowSize());
        settingsItems.Clear();
        if (modifierOpen)
        {
            for (var index = 0; index < HotkeyModifiers.Length; index++)
            {
                settingsItems.Add(new DropdownMenu.Item(ModifierLabel((int)HotkeyModifiers[index]), string.Empty,
                    false, configuration.LinkpearlHotkeyModifier == (int)HotkeyModifiers[index]));
            }

            var picked = settingsMenu.Draw(surface, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
            if (picked >= 0)
            {
                configuration.LinkpearlHotkeyModifier = (int)HotkeyModifiers[picked];
                configuration.Save();
            }

            return;
        }

        for (var index = 0; index < HotkeyKeys.Length; index++)
        {
            settingsItems.Add(new DropdownMenu.Item(HotkeyKeyLabels[index], string.Empty, false,
                configuration.LinkpearlHotkeyKey == (int)HotkeyKeys[index]));
        }

        var choice = settingsMenu.Draw(surface, frameTheme, CollectionsMarshal.AsSpan(settingsItems));
        if (choice < 0)
        {
            return;
        }

        configuration.LinkpearlHotkeyKey = (int)HotkeyKeys[choice];
        configuration.Save();
    }

    private static string ModifierLabel(int modifier) => modifier switch
    {
        (int)VirtualKey.CONTROL => "Ctrl",
        (int)VirtualKey.MENU => "Alt",
        (int)VirtualKey.SHIFT => "Shift",
        _ => Loc.T(L.Linkpearl.HotkeyNoModifier),
    };

    private static string KeyLabel(int key)
    {
        for (var index = 0; index < HotkeyKeys.Length; index++)
        {
            if ((int)HotkeyKeys[index] == key)
            {
                return HotkeyKeyLabels[index];
            }
        }

        return Loc.T(L.Linkpearl.HotkeyNoModifier);
    }
}
