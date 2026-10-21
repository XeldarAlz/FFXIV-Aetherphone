using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float GuideStepUnits = 24f;

    private static readonly LocString[] GuideTitles =
    {
        L.Prism.GuideEnterTitle, L.Prism.JudgeHeat, L.Prism.FinalTitle, L.Prism.GuideRevealTitle,
    };

    private static readonly LocString[] GuideBodies =
    {
        L.Prism.GuideEnterBody, L.Prism.GuideJudgeBody, L.Prism.GuideFinalBody, L.Prism.GuideRevealBody,
    };

    private void DrawGuide(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var strip = StripRect(content, scale);
        if (PrismArt.Strip(drawList, strip, Loc.T(L.Prism.Done), string.Empty, true))
        {
            Go(PrismScreen.Cover, -1);
            introDelay = IntroSeconds;
            return;
        }

        var body = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, strip.Min.Y - Metrics.Space.Lg * scale));
        using var surface = AppSurface.BeginEdgeToEdge(body);
        var list = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var gutter = PrismArt.Gutter(scale);
        var width = ScrollLayout.StableContentWidth() - gutter * 2f;
        var left = origin.X + gutter;
        var cursor = DrawSectionTitle(list, left, origin.Y, width, Loc.T(L.Prism.HowItWorks), scale);
        var indent = SectionIndexUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        var titlePixels = GuideStepUnits * scale;
        for (var index = 0; index < GuideTitles.Length; index++)
        {
            PrismType.Draw(list, new Vector2(left, cursor + (titlePixels - labelPixels) * 0.5f), SectionIndex[index],
                PrismArt.Muted, PrismFace.Label, labelPixels);
            PrismType.Draw(list, new Vector2(left + indent, cursor), Loc.T(GuideTitles[index]), PrismArt.Fore,
                PrismFace.Headline, titlePixels);
            cursor += PrismType.LineHeight(PrismFace.Headline, titlePixels) + Metrics.Space.Xs * scale;
            var copy = PrismType.Sentences(Loc.T(GuideBodies[index]), TextStyles.Subheadline, width - indent);
            cursor += Typography.DrawWrappedLeft(new Vector2(left + indent, cursor), copy, PrismArt.Muted,
                TextStyles.Subheadline, PrismType.BalancedWidth(copy, TextStyles.Subheadline, width - indent)) +
                      Metrics.Space.Xl * scale;
        }

        ImGui.Dummy(new Vector2(width, cursor - origin.Y));
    }
}
