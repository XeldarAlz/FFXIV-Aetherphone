using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal static class LinkRow
{
    private const float Icon = 44f;
    private const float Pad = 14f;
    private const float TextGap = 12f;
    private const float ChevronReserve = 18f;
    private const float LineGap = 2f;
    private const float IconGlyphShare = 0.42f;
    private const float Radius = 18f;

    public static float Height(string line, float width, float scale)
    {
        var block = Typography.LineHeight(TextStyles.Headline) + LineGap * scale
            + PitText.Height(line, TextStyles.Footnote, TextWidth(width, scale));
        return MathF.Max(Icon * scale, block) + Pad * 2f * scale;
    }

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title, string line,
        FontAwesomeIcon icon, Vector4 tint, bool circle, float scale)
    {
        var height = Height(line, width, scale);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Radius * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        ui.Card(drawList, card.Min, card.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var size = Icon * scale;
        var center = new Vector2(origin.X + Pad * scale + size * 0.5f, card.Center.Y);
        var half = new Vector2(size * 0.5f, size * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half,
            circle ? size * 0.5f : size * Metrics.Radius.TileFactor, IconTile.Surface(tint));
        ProgressRing.CenterIcon(drawList, center, icon, CasinoArt.White, size * IconGlyphShare);
        var textLeft = origin.X + (Pad + Icon + TextGap) * scale;
        var textWidth = TextWidth(width, scale);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(line, TextStyles.Footnote, textWidth);
        var top = card.Center.Y - (titleHeight + LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        PitText.Draw(drawList, line, TextStyles.Footnote, ui.BodyInk, textLeft, top + titleHeight + LineGap * scale,
            textWidth, false);
        CasinoArt.Chevron(drawList, new Vector2(card.Max.X - Pad * scale - ChevronReserve * 0.3f * scale, card.Center.Y),
            ui.MutedInk);
        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static float TextWidth(float width, float scale) =>
        MathF.Max(1f, width - (Pad * 2f + Icon + TextGap + ChevronReserve) * scale);
}
