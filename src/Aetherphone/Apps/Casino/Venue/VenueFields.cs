using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal static class VenueFields
{
    public const float RowUnits = 52f;
    public const float SegmentRowUnits = 80f;
    public const int AmountDigits = 11;

    private const float FieldWidthFraction = 0.46f;
    private const float FieldHeight = 36f;

    public static void Label(AppSkin ui, in Rect row, string label, float scale)
    {
        var width = row.Width * (1f - FieldWidthFraction) - Metrics.Space.Sm * scale;
        var height = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - height * 0.5f),
            Typography.FitText(label, width, TextStyles.Body), ui.TitleInk, TextStyles.Body);
    }

    public static void TextRow(AppSkin ui, in Rect row, string id, string label, string hint, ref string buffer,
        int maxLength, bool numeric, float scale)
    {
        Label(ui, row, label, scale);
        var height = FieldHeight * scale;
        var field = new Rect(new Vector2(row.Max.X - row.Width * FieldWidthFraction, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + height * 0.5f));
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        var flags = numeric
            ? ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll
            : ImGuiInputTextFlags.None;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint(id, hint, ref buffer, maxLength + 1, flags);
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    public static bool ToggleRow(AppSkin ui, in Rect row, string id, string label, bool value, float scale)
    {
        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, row.Width - width - Metrics.Space.Md * scale, TextStyles.Body), ui.TitleInk,
            TextStyles.Body);
        var toggleMin = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(id, new Rect(toggleMin, toggleMin + new Vector2(width, height)), value, ui.Theme);
    }

    public static int Segment(AppSkin ui, in Rect row, string id, string label, string[] options, int selected,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = row.Min.Y + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, top), Typography.FitText(label, row.Width, TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        var stripTop = top + Typography.LineHeight(TextStyles.Body) + Metrics.Space.Sm * scale;
        var strip = new Rect(new Vector2(row.Min.X, stripTop),
            new Vector2(row.Max.X, MathF.Max(stripTop + 30f * scale, row.Max.Y - Metrics.Space.Sm * scale)));
        return SegmentStrip.Draw(id, strip, options, Math.Clamp(selected, 0, options.Length - 1), ui.Palette);
    }

    public static void ValueRow(AppSkin ui, in Rect row, string label, string value, float scale)
    {
        Label(ui, row, label, scale);
        var width = row.Width * FieldWidthFraction;
        var fitted = Typography.FitText(value, width, TextStyles.Body);
        var size = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f),
            fitted, ui.BodyInk, TextStyles.Body);
    }

    public static float Hint(AppSkin ui, string text, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var top = origin.Y + Metrics.Space.Xs * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Lg * scale, top), text,
            ui.BodyInk, TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Xs * 2f * scale));
        return height;
    }

    public static long Parse(string text)
    {
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 0;
    }

    public static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }
}
