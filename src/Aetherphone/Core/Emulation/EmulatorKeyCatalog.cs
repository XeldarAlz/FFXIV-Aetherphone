using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Core.Emulation;

internal static class EmulatorKeyCatalog
{
    private static readonly int[] Supported = BuildSupported();
    private static readonly VirtualKey[] Virtual = BuildVirtual();

    public static IReadOnlyList<int> SupportedKeys => Supported;

    public static VirtualKey[] VirtualKeys => Virtual;

    public static bool IsDown(int virtualKey)
    {
        switch (virtualKey)
        {
            case 0x10:
                return ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift);
            case 0x11:
                return ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl);
            case 0x12:
                return ImGui.IsKeyDown(ImGuiKey.LeftAlt) || ImGui.IsKeyDown(ImGuiKey.RightAlt);
            default:
                var key = ToImGuiKey(virtualKey);
                return key != ImGuiKey.None && ImGui.IsKeyDown(key);
        }
    }

    private static ImGuiKey ToImGuiKey(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ImGuiKey.Key0 + (virtualKey - 0x30);
        }

        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ImGuiKey.A + (virtualKey - 0x41);
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            return ImGuiKey.Keypad0 + (virtualKey - 0x60);
        }

        if (virtualKey is >= 0x70 and <= 0x7B)
        {
            return ImGuiKey.F1 + (virtualKey - 0x70);
        }

        return virtualKey switch
        {
            0x08 => ImGuiKey.Backspace,
            0x09 => ImGuiKey.Tab,
            0x0D => ImGuiKey.Enter,
            0x1B => ImGuiKey.Escape,
            0x20 => ImGuiKey.Space,
            0x21 => ImGuiKey.PageUp,
            0x22 => ImGuiKey.PageDown,
            0x23 => ImGuiKey.End,
            0x24 => ImGuiKey.Home,
            0x25 => ImGuiKey.LeftArrow,
            0x26 => ImGuiKey.UpArrow,
            0x27 => ImGuiKey.RightArrow,
            0x28 => ImGuiKey.DownArrow,
            0x2D => ImGuiKey.Insert,
            0x2E => ImGuiKey.Delete,
            0x6A => ImGuiKey.KeypadMultiply,
            0x6B => ImGuiKey.KeypadAdd,
            0x6D => ImGuiKey.KeypadSubtract,
            0x6E => ImGuiKey.KeypadDecimal,
            0x6F => ImGuiKey.KeypadDivide,
            0xBA => ImGuiKey.Semicolon,
            0xBB => ImGuiKey.Equal,
            0xBC => ImGuiKey.Comma,
            0xBD => ImGuiKey.Minus,
            0xBE => ImGuiKey.Period,
            0xBF => ImGuiKey.Slash,
            0xC0 => ImGuiKey.GraveAccent,
            0xDB => ImGuiKey.LeftBracket,
            0xDC => ImGuiKey.Backslash,
            0xDD => ImGuiKey.RightBracket,
            0xDE => ImGuiKey.Apostrophe,
            _ => ImGuiKey.None,
        };
    }

    private static VirtualKey[] BuildVirtual()
    {
        var keys = new VirtualKey[Supported.Length];
        for (var index = 0; index < Supported.Length; index++)
        {
            keys[index] = (VirtualKey)Supported[index];
        }

        return keys;
    }

    public static string Name(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 || virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x7B)
        {
            return $"F{virtualKey - 0x6F}";
        }

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0x60 => "Num 0",
            0x61 => "Num 1",
            0x62 => "Num 2",
            0x63 => "Num 3",
            0x64 => "Num 4",
            0x65 => "Num 5",
            0x66 => "Num 6",
            0x67 => "Num 7",
            0x68 => "Num 8",
            0x69 => "Num 9",
            0x6A => "Num *",
            0x6B => "Num +",
            0x6D => "Num -",
            0x6E => "Num .",
            0x6F => "Num /",
            0xBA => ";",
            0xBB => "+",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"VK {virtualKey:X2}",
        };
    }

    private static int[] BuildSupported()
    {
        var keys = new List<int>
        {
            0x08, 0x09, 0x0D, 0x10, 0x11, 0x12, 0x1B, 0x20, 0x21, 0x22, 0x23, 0x24,
            0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E,
        };
        AddRange(keys, 0x30, 0x39);
        AddRange(keys, 0x41, 0x5A);
        AddRange(keys, 0x60, 0x69);
        keys.Add(0x6A);
        keys.Add(0x6B);
        keys.Add(0x6D);
        keys.Add(0x6E);
        keys.Add(0x6F);
        AddRange(keys, 0x70, 0x7B);
        AddRange(keys, 0xBA, 0xC0);
        AddRange(keys, 0xDB, 0xDE);
        return keys.ToArray();
    }

    private static void AddRange(List<int> keys, int first, int last)
    {
        for (var key = first; key <= last; key++)
        {
            keys.Add(key);
        }
    }
}
