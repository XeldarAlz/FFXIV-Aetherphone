using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VDisclosure
{
    public const float HeaderHeight = 52f;
    public const float PanelPadX = VCard.Pad;
    public const float PanelPadY = 12f;
    public const float RevealSmoothTime = 0.14f;
    private const float TileGap = 10f;
    private const float SummaryGap = 8f;
    private const float TitleSummaryGap = 12f;

    public static bool Card(ImDrawListPtr drawList, in Rect header, float visible, string glyph, Vector4 tone,
        string title, string summary, float reveal, float scale)
    {
        var radius = Metrics.Radius.Card * scale;
        VCard.Paint(drawList, header.Min, new Vector2(header.Max.X, header.Max.Y + visible), scale);
        var hovered = UiInteract.Hover(header.Min, header.Max);
        if (hovered)
        {
            var wash = VelvetTheme.HoverWash.Packed();
            if (visible > 0f)
            {
                Squircle.FillCap(drawList, header.Min, header.Max, radius, wash, true);
            }
            else
            {
                Squircle.Fill(drawList, header.Min, header.Max, radius, wash);
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var padX = PanelPadX * scale;
        var centerY = header.Center.Y;
        var textLeft = header.Min.X + padX;
        if (glyph.Length > 0)
        {
            var tile = VCard.HeaderTile * scale;
            var tileMin = new Vector2(textLeft, centerY - tile * 0.5f);
            var tileMax = new Vector2(textLeft + tile, centerY + tile * 0.5f);
            VCard.Tile(drawList, tileMin, tileMax, glyph, tone, VCard.HeaderGlyph * scale, scale);
            textLeft = tileMax.X + TileGap * scale;
        }

        var chevronCenter = new Vector2(header.Max.X - padX - VIcon.Row * scale * 0.5f, centerY);
        var summaryRight = chevronCenter.X - VIcon.Row * scale * 0.5f - SummaryGap * scale;
        var titleWidth = Typography.Measure(title, TextStyles.Headline).X;
        var summaryMaxWidth = MathF.Max(1f, summaryRight - textLeft - titleWidth - TitleSummaryGap * scale);
        var fittedSummary = Typography.FitText(summary, summaryMaxWidth, TextStyles.Subheadline);
        var summarySize = Typography.Measure(fittedSummary, TextStyles.Subheadline);
        Typography.Draw(drawList,
            new Vector2(textLeft, centerY - Typography.LineHeight(TextStyles.Headline) * 0.5f), title,
            VelvetTheme.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(summaryRight - summarySize.X, centerY - summarySize.Y * 0.5f),
            fittedSummary, VelvetTheme.MutedInk, TextStyles.Subheadline);
        Chevron(drawList, chevronCenter, reveal, scale);
        if (visible > 0f)
        {
            FeedCell.Hairline(drawList, header.Min.X + padX, header.Max.X - padX, header.Max.Y, VelvetTheme.Hairline);
        }

        return UiInteract.Click(header.Min, header.Max, hovered);
    }

    public static void Chevron(ImDrawListPtr drawList, Vector2 center, float reveal, float scale)
    {
        var size = VIcon.Row * scale;
        if (reveal < 0.999f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronRight,
                VelvetTheme.Alpha(VelvetTheme.MutedInk, 1f - reveal), size);
        }

        if (reveal > 0.001f)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.ChevronDown, VelvetTheme.Alpha(VelvetTheme.RoseInk, reveal),
                size);
        }
    }
}
