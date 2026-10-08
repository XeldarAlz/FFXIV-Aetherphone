using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class OriginalsField
{
    private const int BufferLength = 16;

    private readonly string fieldId;

    private string buffer = string.Empty;
    private bool editing;
    private bool focusPending;

    public OriginalsField(string fieldId)
    {
        this.fieldId = fieldId;
    }

    public bool Editing => editing;

    public void Cancel()
    {
        editing = false;
    }

    public bool Draw(ImDrawListPtr drawList, Rect field, string caption, string display, decimal current, AppSkin ui,
        bool enabled, out decimal value)
    {
        value = current;
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var captionText = Typography.FitText(caption, capsule.Width * 0.5f, TextStyles.Footnote);
        var captionSize = Typography.Measure(captionText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(capsule.Min.X + inset, capsule.Center.Y - captionSize.Y * 0.5f),
            captionText, ui.MutedInk, TextStyles.Footnote);
        var valueLeft = capsule.Min.X + inset + captionSize.X + Metrics.Space.Xs * UiScale.Current;
        var valueWidth = MathF.Max(1f, capsule.Max.X - inset - valueLeft);
        if (editing && enabled)
        {
            return DrawEditor(field, valueLeft, valueWidth, current, out value);
        }

        editing = false;
        var text = Typography.FitText(display, valueWidth, TextStyles.SubheadlineEmphasized);
        var size = Typography.Measure(text, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(capsule.Max.X - inset - size.X, capsule.Center.Y - size.Y * 0.5f), text,
            enabled ? CasinoColors.InkTitle : ui.MutedInk, TextStyles.SubheadlineEmphasized);
        var hovered = enabled && UiInteract.Hover(field.Min, field.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        if (!UiInteract.Click(field.Min, field.Max, hovered))
        {
            return false;
        }

        editing = true;
        focusPending = true;
        buffer = current.ToString("0.##", CultureInfo.InvariantCulture);
        return false;
    }

    private bool DrawEditor(Rect field, float left, float width, decimal current, out decimal value)
    {
        value = current;
        ImGui.SetCursorScreenPos(new Vector2(left, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width);
        if (focusPending)
        {
            ImGui.SetKeyboardFocusHere();
        }

        bool committed;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, CasinoColors.InkTitle))
        {
            committed = ImGui.InputText(fieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll |
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var active = ImGui.IsItemActive();
        if (focusPending)
        {
            focusPending = false;
            return false;
        }

        if (!committed && active)
        {
            return false;
        }

        editing = false;
        if (!decimal.TryParse(buffer, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
