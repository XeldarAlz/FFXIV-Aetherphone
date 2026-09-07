using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VSectionHeader
{
    public static void Overline(string label, string trailing = "", float inset = 0f)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var left = origin.X + inset;
        var right = origin.X + width - inset;
        var overlineMaxWidth = right - left;
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.FootnoteEmphasized);
            overlineMaxWidth -= size.X + 8f * scale;
            Typography.Draw(new Vector2(right - size.X, origin.Y), trailing, VelvetTheme.MutedInk,
                TextStyles.FootnoteEmphasized);
        }

        Typography.Draw(new Vector2(left, origin.Y),
            Typography.FitText(Loc.Upper(label), overlineMaxWidth, TextStyles.FootnoteEmphasized),
            VelvetTheme.HeaderInk, TextStyles.FootnoteEmphasized);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 22f * scale));
    }

    public static void Card(string glyph, string label, string trailing = "")
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var tile = 24f * scale;
        var min = origin;
        var max = new Vector2(origin.X + tile, origin.Y + tile);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, VelvetTheme.Alpha(VelvetTheme.Rose, 0.20f).Packed());
        PhoneIcon.Draw(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), glyph,
            VelvetTheme.RoseInk, 15f * scale);
        var cardLabelLeft = max.X + 10f * scale;
        var cardLabelMaxWidth = origin.X + width - cardLabelLeft;
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.Footnote);
            cardLabelMaxWidth -= size.X + 8f * scale;
            Typography.Draw(new Vector2(origin.X + width - size.X, origin.Y + tile * 0.5f - 7f * scale), trailing,
                VelvetTheme.MutedInk, TextStyles.Footnote);
        }

        Typography.Draw(new Vector2(cardLabelLeft, origin.Y + tile * 0.5f - 9f * scale),
            Typography.FitText(label, cardLabelMaxWidth, TextStyles.Headline), VelvetTheme.TitleInk,
            TextStyles.Headline);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, tile + 10f * scale));
    }
}
