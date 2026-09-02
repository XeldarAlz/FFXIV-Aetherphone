using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness.Host;

internal static class BrowserKeys
{
    private static readonly string[] DigitPrefixes = { "Key", "_", "D", "" };

    public static bool TryMap(string name, out ImGuiKey key, out ImGuiKey modifier)
    {
        modifier = ImGuiKey.None;
        switch (name)
        {
            case "Enter": key = ImGuiKey.Enter; return true;
            case "Backspace": key = ImGuiKey.Backspace; return true;
            case "Escape": key = ImGuiKey.Escape; return true;
            case "Tab": key = ImGuiKey.Tab; return true;
            case "Delete": key = ImGuiKey.Delete; return true;
            case "Insert": key = ImGuiKey.Insert; return true;
            case "Home": key = ImGuiKey.Home; return true;
            case "End": key = ImGuiKey.End; return true;
            case "PageUp": key = ImGuiKey.PageUp; return true;
            case "PageDown": key = ImGuiKey.PageDown; return true;
            case "ArrowLeft": key = ImGuiKey.LeftArrow; return true;
            case "ArrowRight": key = ImGuiKey.RightArrow; return true;
            case "ArrowUp": key = ImGuiKey.UpArrow; return true;
            case "ArrowDown": key = ImGuiKey.DownArrow; return true;
            case " ": key = ImGuiKey.Space; return true;
            case "CapsLock": key = ImGuiKey.CapsLock; return true;
            case "Shift": key = ImGuiKey.LeftShift; modifier = ImGuiKey.ModShift; return true;
            case "Control": key = ImGuiKey.LeftCtrl; modifier = ImGuiKey.ModCtrl; return true;
            case "Alt": key = ImGuiKey.LeftAlt; modifier = ImGuiKey.ModAlt; return true;
            case "Meta": key = ImGuiKey.LeftSuper; modifier = ImGuiKey.ModSuper; return true;
        }

        if (Enum.TryParse(name, true, out key))
        {
            return true;
        }

        if (name.Length != 1)
        {
            key = ImGuiKey.None;
            return false;
        }

        var character = char.ToUpperInvariant(name[0]);
        if (char.IsAsciiLetter(character))
        {
            return Enum.TryParse(character.ToString(), out key);
        }

        if (char.IsAsciiDigit(character))
        {
            for (var index = 0; index < DigitPrefixes.Length; index++)
            {
                if (Enum.TryParse(DigitPrefixes[index] + character, out key))
                {
                    return true;
                }
            }
        }

        key = ImGuiKey.None;
        return false;
    }
}
