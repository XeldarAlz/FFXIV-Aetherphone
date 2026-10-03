using Aetherphone.Core;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const float RowHeight = 62f;
    private const float RowPadX = 14f;
    private const float RowIconSize = 42f;
    private const float RowIconRadius = 11f;
    private const float RowTextGap = 12f;
    private const float RowLineGap = 2f;
    private const float RowTrailingGap = 10f;
    private const float RowBadgeRadius = 10f;
    private const float RowWashInset = 4f;
    private const float RowWashRadius = 16f;
    private const float RowPressedWash = 1.6f;
    private const float HairlineAlpha = 0.07f;

    private float DrawRowGroup(ImDrawListPtr drawList, Vector2 origin, float width, List<DigestRow> rows,
        string? anchor)
    {
        var scale = UiScale.Current;
        var rowHeight = RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rows.Count * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale, true);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rect = new Rect(new Vector2(origin.X, origin.Y + index * rowHeight),
                new Vector2(max.X, origin.Y + (index + 1) * rowHeight));
            if (index == 0 && anchor is not null)
            {
                UiAnchors.Report(anchor, rect);
            }

            if (index > 0)
            {
                DrawRowSeparator(drawList, rect, scale);
            }

            if (DrawItemRow(drawList, rect, row.Item, row.Item.Subtitle, row.Trailing, row.Badge))
            {
                OpenItem(row.Item);
            }
        }

        return max.Y;
    }

    private void DrawRowSeparator(ImDrawListPtr drawList, Rect row, float scale)
    {
        var left = row.Min.X + (RowPadX + RowIconSize + RowTextGap) * scale;
        drawList.AddLine(new Vector2(left, row.Min.Y), new Vector2(row.Max.X - RowPadX * scale, row.Min.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)), 1f);
    }

    private bool DrawItemRow(ImDrawListPtr drawList, Rect row, CollectionItem item, string subtitle, string trailing,
        RowBadge badge)
    {
        var scale = UiScale.Current;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var inset = RowWashInset * scale;
            var wash = ImGui.IsMouseDown(ImGuiMouseButton.Left)
                ? Palette.WithAlpha(ui.HoverTint, ui.HoverTint.W * RowPressedWash)
                : ui.HoverTint;
            Squircle.Fill(drawList, row.Min + new Vector2(inset, inset), row.Max - new Vector2(inset, inset),
                RowWashRadius * scale, ImGui.GetColorU32(wash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = RowPadX * scale;
        var iconSize = RowIconSize * scale;
        var iconMin = new Vector2(row.Min.X + pad, row.Center.Y - iconSize * 0.5f);
        DrawIcon(drawList, item, iconMin, iconMin + new Vector2(iconSize, iconSize), RowIconRadius * scale);

        var right = row.Max.X - pad;
        if (badge != RowBadge.None)
        {
            var radius = RowBadgeRadius * scale;
            var center = new Vector2(right - radius, row.Center.Y);
            if (badge == RowBadge.Owned)
            {
                CollectionsArt.CheckBadge(drawList, center, radius, CollectionsArt.OwnedInk, CollectionsArt.White,
                    scale);
            }
            else
            {
                CollectionsArt.EmptyBadge(drawList, center, radius, ui.MutedInk, scale);
            }

            right -= radius * 2f + RowTrailingGap * scale;
        }

        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(right - size.X, row.Center.Y - size.Y * 0.5f), trailing, ui.MutedInk,
                TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
            right -= size.X + RowTrailingGap * scale;
        }

        var textLeft = iconMin.X + iconSize + RowTextGap * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var name = Typography.FitText(item.Name, textWidth, TextStyles.BodyEmphasized);
        var nameHeight = Typography.Measure(name, TextStyles.BodyEmphasized).Y;
        if (subtitle.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameHeight * 0.5f), name, ui.TitleInk,
                TextStyles.BodyEmphasized);
            return UiInteract.Click(row.Min, row.Max, hovered);
        }

        var sub = Typography.FitText(subtitle, textWidth, TextStyles.Footnote);
        var subHeight = Typography.Measure(sub, TextStyles.Footnote).Y;
        var top = row.Center.Y - (nameHeight + subHeight + RowLineGap * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), name, ui.TitleInk, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight + RowLineGap * scale), sub, ui.MutedInk,
            TextStyles.Footnote);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
