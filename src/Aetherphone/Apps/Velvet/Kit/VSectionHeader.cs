using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VSectionHeader
{
    public static void Overline(string label, string trailing = "")
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var overlineMaxWidth = width;
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.FootnoteEmphasized);
            overlineMaxWidth -= size.X + 8f * scale;
            Typography.Draw(new Vector2(origin.X + width - size.X, origin.Y), trailing, VelvetTheme.MutedInk,
                TextStyles.FootnoteEmphasized);
        }

        Typography.Draw(origin,
            Typography.FitText(Loc.Culture.TextInfo.ToUpper(label), overlineMaxWidth, TextStyles.FootnoteEmphasized),
            VelvetTheme.HeaderInk, TextStyles.FootnoteEmphasized);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 22f * scale));
    }

    public static void Bar(string label, string trailing = "")
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var barWidth = 3f * scale;
        var barHeight = 15f * scale;
        Squircle.Fill(drawList, new Vector2(origin.X, origin.Y + 2f * scale),
            new Vector2(origin.X + barWidth, origin.Y + 2f * scale + barHeight), barWidth * 0.5f,
            VelvetTheme.Rose.Packed());
        var barLabelLeft = origin.X + barWidth + 9f * scale;
        var barLabelMaxWidth = origin.X + width - barLabelLeft;
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.Subheadline);
            barLabelMaxWidth -= size.X + 8f * scale;
            Typography.Draw(new Vector2(origin.X + width - size.X, origin.Y + 1f * scale), trailing,
                VelvetTheme.MutedInk, TextStyles.Subheadline);
        }

        barLabelMaxWidth = MathF.Max(1f, barLabelMaxWidth);
        Typography.Draw(new Vector2(barLabelLeft, origin.Y), Typography.FitText(label, barLabelMaxWidth,
            TextStyles.Headline), VelvetTheme.TitleInk, TextStyles.Headline);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    public static void Card(FontAwesomeIcon icon, string label, string trailing = "")
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var tile = 24f * scale;
        var min = origin;
        var max = new Vector2(origin.X + tile, origin.Y + tile);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, VelvetTheme.Alpha(VelvetTheme.Rose, 0.20f).Packed());
        AppSkin.Icon(new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), icon.ToIconString(),
            VelvetTheme.RoseInk, 0.72f);
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
