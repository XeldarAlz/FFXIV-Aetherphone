using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Housing;

internal static class HousingChrome
{
    private const float StepGlyphFraction = 0.8f;

    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public static bool Hover(Vector2 min, Vector2 max, bool overlay) =>
        overlay ? UiInteract.HoverWindowOnly(min, max) : UiInteract.Hover(min, max);

    public static Vector4 FreshnessHue(HousingDataFreshness freshness, Vector4 accent) => freshness switch
    {
        HousingDataFreshness.Live => accent,
        HousingDataFreshness.Recent => AppPalettes.HousingBrass,
        HousingDataFreshness.Stale => AppPalettes.HousingResults,
        HousingDataFreshness.Cached => AppPalettes.HousingParchment,
        _ => AppPalettes.HousingClosed,
    };

    public static int Segment(Rect rect, string first, string second, int selected, AppSkin ui, bool overlay = false,
        float thumb = -1f, bool surface = true)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        if (surface)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(ui.FieldSurface));
        }

        var half = rect.Width * 0.5f;
        var inset = 2f * scale;
        var position = thumb < 0f ? selected : Math.Clamp(thumb, 0f, 1f);
        var thumbMin = new Vector2(rect.Min.X + inset + position * half, rect.Min.Y + inset);
        var thumbMax = new Vector2(thumbMin.X + half - inset * 2f, rect.Max.Y - inset);
        Squircle.Fill(drawList, thumbMin, thumbMax, radius - inset, ImGui.GetColorU32(ui.Accent));
        var result = selected;
        for (var index = 0; index < 2; index++)
        {
            var min = new Vector2(rect.Min.X + index * half, rect.Min.Y);
            var max = new Vector2(min.X + half, rect.Max.Y);
            var hovered = Hover(min, max, overlay);
            var active = index == selected;
            var ink = active ? AccentRing.Ink : hovered ? ui.TitleInk : ui.BodyInk;
            var label = index == 0 ? first : second;
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, rect.Center.Y),
                Typography.FitText(label, half - 12f * scale, TextStyles.SubheadlineEmphasized), ink,
                TextStyles.SubheadlineEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                result = index;
                UiFeedback.Play(UiSound.Tap);
            }
        }

        return result;
    }

    private static readonly Dictionary<string, string> NumberBuffers = new(StringComparer.Ordinal);

    public static int NumberStepper(Rect rect, string id, int value, int minimum, int maximum, int step, string suffix,
        AppSkin ui, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var buttonRadius = rect.Height * 0.5f;
        var minusCenter = new Vector2(rect.Min.X + buttonRadius, rect.Center.Y);
        var plusCenter = new Vector2(rect.Max.X - buttonRadius, rect.Center.Y);
        var fieldMin = new Vector2(minusCenter.X + buttonRadius + 5f * scale, rect.Min.Y);
        var fieldMax = new Vector2(plusCenter.X - buttonRadius - 5f * scale, rect.Max.Y);
        var result = value;
        var ink = ui.Ink;
        var key = ImGui.GetID(id);
        if (StepButton(drawList, unchecked(key + 1u), minusCenter, buttonRadius, FontAwesomeIcon.Minus, ink,
                value > minimum, overlay))
        {
            result = value - step;
        }

        if (StepButton(drawList, unchecked(key + 2u), plusCenter, buttonRadius, FontAwesomeIcon.Plus, ink,
                value < maximum, overlay))
        {
            result = value + step;
        }

        SearchBar.Surface(drawList, new Rect(fieldMin, fieldMax), ink);
        var typing = NumberBuffers.TryGetValue(id, out var buffer) && buffer is not null;
        var text = typing ? buffer! : value.ToString(Loc.Culture);
        var suffixWidth = suffix.Length > 0
            ? Typography.Measure(suffix, TextStyles.Caption1).X + 4f * scale
            : 0f;
        var inputWidth = MathF.Max(18f * scale, fieldMax.X - fieldMin.X - 10f * scale - suffixWidth);
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 5f * scale,
            rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);
        using (UiScope.PushColor(ImGuiCol.FrameBg, Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputText(id, ref text, 6,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        if (ImGui.IsItemActive())
        {
            NumberBuffers[id] = text;
        }
        else if (typing)
        {
            NumberBuffers.Remove(id);
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer, Loc.Culture, out var typed))
            {
                result = typed;
            }
        }

        if (suffix.Length > 0)
        {
            var suffixSize = Typography.Measure(suffix, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(fieldMax.X - 5f * scale - suffixSize.X,
                rect.Center.Y - suffixSize.Y * 0.5f), suffix, ui.MutedInk, TextStyles.Caption1);
        }

        return Math.Clamp(result, minimum, maximum);
    }

    private static bool StepButton(ImDrawListPtr drawList, uint key, Vector2 center, float radius,
        FontAwesomeIcon icon, in ControlInk ink, bool enabled, bool overlay)
    {
        var clicked = RoundButton.Draw(drawList, key, center, radius, ink, ButtonStyle.Tinted, enabled, overlay,
            out var face);
        var grow = face.Face.Width / MathF.Max(radius * 2f, 0.0001f);
        ProgressRing.CenterIcon(drawList, center, icon, face.LabelInk, radius * StepGlyphFraction * grow);
        return clicked;
    }
}
