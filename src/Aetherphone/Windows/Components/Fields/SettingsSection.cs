using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SettingsSection
{
    public static void Header(string title, PhoneTheme theme, string? hint = null) =>
        ListSection.Header(title, theme.TextMuted, theme, hint);

    public static void Hint(string text, PhoneTheme theme) => Hint(text, theme.TextMuted);

    public static void Hint(string text, Vector4 ink)
    {
        var scale = UiScale.Current;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Metrics.Space.Lg * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.Footnote.Weight))
        using (UiScope.PushColor(ImGuiCol.Text, ink))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(text);
            ImGui.PopTextWrapPos();
        }
    }
}
