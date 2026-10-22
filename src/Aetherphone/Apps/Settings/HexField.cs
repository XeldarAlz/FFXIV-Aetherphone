using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings;

internal static class HexField
{
    private const int DigitCount = 6;
    private const int ChipSegments = 24;
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public static bool Draw(string id, ref string digits, Vector4 chip, PhoneTheme theme, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Metrics.Size.FieldHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        SearchBar.Surface(drawList, field, ControlInk.From(theme));
        var chipRadius = SearchBar.Capsule(field).Height * 0.5f - Metrics.Space.Xxs * scale;
        var chipCenter = new Vector2(origin.X + Metrics.Space.Md * scale + chipRadius, origin.Y + height * 0.5f);
        drawList.AddCircleFilled(chipCenter, chipRadius, ImGui.GetColorU32(chip), ChipSegments);
        drawList.AddCircle(chipCenter, chipRadius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.18f)),
            ChipSegments, Metrics.Stroke.Hairline * scale);
        var hashLeft = chipCenter.X + chipRadius + Metrics.Space.Md * scale;
        var hashSize = Typography.Measure("#", TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(hashLeft, origin.Y + height * 0.5f - hashSize.Y * 0.5f), "#",
            theme.TextMuted, TextStyles.BodyEmphasized);
        var inputLeft = hashLeft + hashSize.X + Metrics.Space.Xxs * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(origin.X + width - Metrics.Space.Md * scale - inputLeft);
        bool edited;
        using (UiScope.PushColor(ImGuiCol.FrameBg, Transparent)
                   .Push(ImGuiCol.FrameBgHovered, Transparent)
                   .Push(ImGuiCol.FrameBgActive, Transparent)
                   .Push(ImGuiCol.Text, theme.TextStrong))
        {
            edited = ImGui.InputText(id, ref digits, DigitCount,
                ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        return edited;
    }
}
