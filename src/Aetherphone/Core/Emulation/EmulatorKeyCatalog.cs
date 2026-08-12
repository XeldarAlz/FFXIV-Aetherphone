namespace Aetherphone.Core.Emulation;

internal static class EmulatorKeyCatalog
{
    private static readonly int[] Supported = BuildSupported();

    public static IReadOnlyList<int> SupportedKeys => Supported;

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
        for (var key = 0x30; key <= 0x39; key++) keys.Add(key);
        for (var key = 0x41; key <= 0x5A; key++) keys.Add(key);
        for (var key = 0x60; key <= 0x69; key++) keys.Add(key);
        keys.AddRange(new[] { 0x6A, 0x6B, 0x6D, 0x6E, 0x6F });
        for (var key = 0x70; key <= 0x7B; key++) keys.Add(key);
        keys.AddRange(new[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE });
        return keys.ToArray();
    }
}
