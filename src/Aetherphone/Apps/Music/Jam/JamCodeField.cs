using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Music.Jam;

internal sealed class JamCodeField
{
    private const int InputCapacity = 64;
    private const float CellGap = 6f;
    private const float GroupGap = 18f;
    private const float DashWidth = 8f;
    private const float DashThickness = 2f;
    private const float CaretWidth = 2f;
    private const float CaretHeightShare = 0.46f;
    private const float CaretBlinkHertz = 1.1f;
    private const float FocusStroke = 1.6f;
    private const float FilledAlpha = 0.55f;
    private const float EmptyAlpha = 0.35f;
    private const int DigitCount = 10;
    private const int LetterCount = 26;

    private static readonly Vector4 Invisible = new(0f, 0f, 0f, 0f);
    private static readonly string[] Glyphs = BuildGlyphs();

    private string buffer = string.Empty;
    private string code = string.Empty;

    public string Code => code;

    public bool Complete => code.Length == JamCodeInput.Length;

    public void Set(string value)
    {
        code = JamCodeInput.Sanitize(value);
        buffer = code;
    }

    public void Clear()
    {
        code = string.Empty;
        buffer = string.Empty;
    }

    public bool Draw(string imguiId, Rect field, AppSkin ui, float clock)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var submitted = DrawInput(imguiId, field);
        var focused = ImGui.IsItemActive();
        var cellGap = CellGap * scale;
        var groupGap = GroupGap * scale;
        var cellWidth = MathF.Max(1f, (field.Width - cellGap * (JamCodeInput.Length - 2) - groupGap)
            / JamCodeInput.Length);
        var radius = Metrics.Radius.Md * scale;
        var left = field.Min.X;
        for (var index = 0; index < JamCodeInput.Length; index++)
        {
            if (index == JamCodeInput.GroupLength)
            {
                DrawDash(drawList, new Vector2(left + groupGap * 0.5f, field.Center.Y), ui.MutedInk, scale);
                left += groupGap;
            }
            else if (index > 0)
            {
                left += cellGap;
            }

            var cell = new Rect(new Vector2(left, field.Min.Y), new Vector2(left + cellWidth, field.Max.Y));
            DrawCell(drawList, cell, index, focused, radius, ui, clock, scale);
            left += cellWidth;
        }

        if (UiInteract.Hover(field.Min, field.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        return submitted;
    }

    private bool DrawInput(string imguiId, Rect field)
    {
        var restore = ImGui.GetCursorScreenPos();
        var fontHeight = ImGui.GetFontSize();
        ImGui.SetCursorScreenPos(field.Min);
        ImGui.SetNextItemWidth(field.Width);
        var submitted = false;
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding,
                   new Vector2(0f, MathF.Max(0f, (field.Height - fontHeight) * 0.5f))))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Invisible))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, Invisible))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, Invisible))
        using (ImRaii.PushColor(ImGuiCol.Text, Invisible))
        using (ImRaii.PushColor(ImGuiCol.TextSelectedBg, Invisible))
        using (ImRaii.PushColor(ImGuiCol.Border, Invisible))
        {
            submitted = ImGui.InputText(imguiId, ref buffer, InputCapacity,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CharsUppercase);
        }

        if (ImGui.IsItemEdited())
        {
            code = JamCodeInput.Sanitize(buffer);
        }
        else if (!ImGui.IsItemActive() && !string.Equals(buffer, code, StringComparison.Ordinal))
        {
            buffer = code;
        }

        ImGui.SetCursorScreenPos(restore);
        return submitted;
    }

    private void DrawCell(ImDrawListPtr drawList, Rect cell, int index, bool focused, float radius, AppSkin ui,
        float clock, float scale)
    {
        var filled = index < code.Length;
        var active = focused && (index == code.Length || (index == JamCodeInput.Length - 1 && Complete));
        Squircle.Fill(drawList, cell.Min, cell.Max, radius, ImGui.GetColorU32(ui.FieldSurface));
        var stroke = active
            ? ui.Accent
            : Palette.WithAlpha(ui.MutedInk, filled ? FilledAlpha : EmptyAlpha);
        Squircle.Stroke(drawList, cell.Min, cell.Max, radius, ImGui.GetColorU32(stroke),
            (active ? FocusStroke : Metrics.Stroke.Hairline) * scale);
        if (filled)
        {
            var glyph = GlyphFor(code[index]);
            Typography.DrawCentered(drawList, cell.Center, glyph, ui.TitleInk, TextStyles.Title2);
            return;
        }

        if (!active || MathF.Sin(clock * MathF.Tau * CaretBlinkHertz) < 0f)
        {
            return;
        }

        var half = cell.Height * CaretHeightShare * 0.5f;
        var caretHalf = CaretWidth * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(cell.Center.X - caretHalf, cell.Center.Y - half),
            new Vector2(cell.Center.X + caretHalf, cell.Center.Y + half), ImGui.GetColorU32(ui.Accent), caretHalf);
    }

    private static void DrawDash(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var halfWidth = DashWidth * 0.5f * scale;
        var halfThickness = DashThickness * 0.5f * scale;
        drawList.AddRectFilled(new Vector2(center.X - halfWidth, center.Y - halfThickness),
            new Vector2(center.X + halfWidth, center.Y + halfThickness), ImGui.GetColorU32(color), halfThickness);
    }

    private static string GlyphFor(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return Glyphs[character - '0'];
        }

        return character is >= 'A' and <= 'Z' ? Glyphs[DigitCount + character - 'A'] : string.Empty;
    }

    private static string[] BuildGlyphs()
    {
        var glyphs = new string[DigitCount + LetterCount];
        for (var digit = 0; digit < DigitCount; digit++)
        {
            glyphs[digit] = ((char)('0' + digit)).ToString();
        }

        for (var letter = 0; letter < LetterCount; letter++)
        {
            glyphs[DigitCount + letter] = ((char)('A' + letter)).ToString();
        }

        return glyphs;
    }
}
