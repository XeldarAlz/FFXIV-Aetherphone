using Dalamud.Bindings.ImGui;
using Silk.NET.Input;

namespace Aetherphone.Harness.Viewer;

internal static class SilkKeys
{
    public static bool TryMap(Key key, out ImGuiKey imGuiKey, out ImGuiKey modifier)
    {
        modifier = ImGuiKey.None;
        switch (key)
        {
            case Key.Enter: imGuiKey = ImGuiKey.Enter; return true;
            case Key.KeypadEnter: imGuiKey = ImGuiKey.KeypadEnter; return true;
            case Key.Escape: imGuiKey = ImGuiKey.Escape; return true;
            case Key.Backspace: imGuiKey = ImGuiKey.Backspace; return true;
            case Key.Tab: imGuiKey = ImGuiKey.Tab; return true;
            case Key.Space: imGuiKey = ImGuiKey.Space; return true;
            case Key.Left: imGuiKey = ImGuiKey.LeftArrow; return true;
            case Key.Right: imGuiKey = ImGuiKey.RightArrow; return true;
            case Key.Up: imGuiKey = ImGuiKey.UpArrow; return true;
            case Key.Down: imGuiKey = ImGuiKey.DownArrow; return true;
            case Key.Home: imGuiKey = ImGuiKey.Home; return true;
            case Key.End: imGuiKey = ImGuiKey.End; return true;
            case Key.PageUp: imGuiKey = ImGuiKey.PageUp; return true;
            case Key.PageDown: imGuiKey = ImGuiKey.PageDown; return true;
            case Key.Insert: imGuiKey = ImGuiKey.Insert; return true;
            case Key.Delete: imGuiKey = ImGuiKey.Delete; return true;
            case Key.CapsLock: imGuiKey = ImGuiKey.CapsLock; return true;
            case Key.BackSlash: imGuiKey = ImGuiKey.Backslash; return true;
            case Key.ShiftLeft: imGuiKey = ImGuiKey.LeftShift; modifier = ImGuiKey.ModShift; return true;
            case Key.ShiftRight: imGuiKey = ImGuiKey.RightShift; modifier = ImGuiKey.ModShift; return true;
            case Key.ControlLeft: imGuiKey = ImGuiKey.LeftCtrl; modifier = ImGuiKey.ModCtrl; return true;
            case Key.ControlRight: imGuiKey = ImGuiKey.RightCtrl; modifier = ImGuiKey.ModCtrl; return true;
            case Key.AltLeft: imGuiKey = ImGuiKey.LeftAlt; modifier = ImGuiKey.ModAlt; return true;
            case Key.AltRight: imGuiKey = ImGuiKey.RightAlt; modifier = ImGuiKey.ModAlt; return true;
            case Key.SuperLeft: imGuiKey = ImGuiKey.LeftSuper; modifier = ImGuiKey.ModSuper; return true;
            case Key.SuperRight: imGuiKey = ImGuiKey.RightSuper; modifier = ImGuiKey.ModSuper; return true;
        }

        var name = key.ToString();
        if (name.StartsWith("Number", StringComparison.Ordinal))
        {
            name = "Key" + name["Number".Length..];
        }

        return Enum.TryParse(name, out imGuiKey);
    }
}
