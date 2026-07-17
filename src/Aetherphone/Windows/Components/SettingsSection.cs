using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SettingsSection
{
    public static void Header(string title, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Metrics.Space.Lg * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.FootnoteEmphasized.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            Typography.Plain(Loc.Culture.TextInfo.ToUpper(title));
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    public static void Hint(string text, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Metrics.Space.Lg * scale);
        using (Plugin.Fonts.Push(TextStyles.Footnote.Scale, TextStyles.Footnote.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(text);
            ImGui.PopTextWrapPos();
        }
    }
}
