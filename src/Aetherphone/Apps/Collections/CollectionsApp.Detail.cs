using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private void DrawDetail(Rect area, CollectionCategory category, CollectionItem item)
    {
        DrawNavBar(area, item.Name, back);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawDetailHero(item, category);
            DrawDetailDescription(item);
            DrawDetailInfo(item, category);
            DrawDetailSources(item);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
        }
    }

    private void DrawDetailHero(CollectionItem item, CollectionCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var iconBox = 82f * scale;
        var cardHeight = iconBox + 30f * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);
        ui.Card(drawList, min, max, 18f * scale, elevated: true);
        Material.TopGlow(drawList, min, max, 18f * scale, CategoryTint(category), 0.6f, 0.10f);

        var pad = 16f * scale;
        var iconMin = new Vector2(min.X + pad, min.Y + (cardHeight - iconBox) * 0.5f);
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        DrawIcon(drawList, item, iconMin, iconMax, 16f * scale);

        var textLeft = iconMax.X + 16f * scale;
        var textWidth = max.X - pad - textLeft;
        var name = Typography.FitText(item.Name, textWidth, TextStyles.Title3);
        Typography.Draw(new Vector2(textLeft, min.Y + pad), name, ui.TitleInk, TextStyles.Title3);

        var cursorY = min.Y + pad + 32f * scale;
        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;
        if (owned is { State: OwnedState.Ready })
        {
            var isOwned = owned.Ids.Contains(item.Id);
            var label = isOwned ? Loc.T(L.Collections.Owned) : Loc.T(L.Collections.Missing);
            var color = isOwned ? frameTheme.ToggleOn : ui.MutedInk;
            DrawStatusBadge(drawList, new Vector2(textLeft, cursorY), label, color, isOwned, scale);
            cursorY += 28f * scale;
        }

        if (item.Stars > 0)
        {
            var stars = new string('★', Math.Clamp(item.Stars, 1, 5));
            Typography.Draw(new Vector2(textLeft, cursorY), stars, ui.Accent, TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 8f * scale));
    }

    private void DrawStatusBadge(ImDrawListPtr drawList, Vector2 pos, string label, Vector4 color, bool owned,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var padX = 10f * scale;
        var dot = 13f * scale;
        var height = 24f * scale;
        var width = padX * 2f + dot + 6f * scale + textSize.X;
        var min = pos;
        var max = new Vector2(pos.X + width, pos.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.16f)));
        var cy = (min.Y + max.Y) * 0.5f;
        var dotCenter = new Vector2(min.X + padX + dot * 0.5f, cy);
        if (owned)
        {
            drawList.AddCircleFilled(dotCenter, dot * 0.5f, ImGui.GetColorU32(color), 16);
            DrawCheck(drawList, dotCenter, new Vector4(1f, 1f, 1f, 1f), scale * 0.82f);
        }
        else
        {
            drawList.AddCircle(dotCenter, dot * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.7f)), 16,
                1.5f * scale);
        }

        Typography.Draw(new Vector2(dotCenter.X + dot * 0.5f + 6f * scale, cy - textSize.Y * 0.5f), label, color,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawDetailDescription(CollectionItem item)
    {
        if (item.Description.Length == 0)
        {
            return;
        }

        DrawSectionHeader(Loc.T(L.Collections.About));
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = 14f * scale;
        var maxWidth = width - padX * 2f;
        var textHeight = Typography.MeasureWrappedBlock(item.Description, TextStyles.Subheadline, maxWidth).Y;
        var cardHeight = textHeight + 24f * scale;
        var max = new Vector2(origin.X + width, origin.Y + cardHeight);
        ui.Card(ImGui.GetWindowDrawList(), origin, max, Metrics.Radius.Card * scale, elevated: false);
        Typography.DrawWrappedLeft(new Vector2(origin.X + padX, origin.Y + 12f * scale), item.Description, ui.BodyInk,
            TextStyles.Subheadline, maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 10f * scale));
    }

    private void DrawDetailInfo(CollectionItem item, CollectionCategory category)
    {
        var rows = 0;
        if (item.Patch.Length > 0)
        {
            rows++;
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            rows++;
        }

        if (category == CollectionCategory.TriadCards && item.Stats is not null)
        {
            rows++;
        }

        if (item.HasTradeable)
        {
            rows++;
        }

        if (item.Community.Length > 0)
        {
            rows++;
        }

        if (rows == 0)
        {
            return;
        }

        DrawSectionHeader(Loc.T(L.Collections.Details));
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 44f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var max = new Vector2(origin.X + width, origin.Y + rows * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Card * scale, elevated: false);

        var rowIndex = 0;
        void InfoRow(string label, string value)
        {
            var top = origin.Y + rowIndex * rowHeight;
            if (rowIndex > 0)
            {
                drawList.AddLine(new Vector2(origin.X + 14f * scale, top), new Vector2(max.X - 14f * scale, top),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            }

            var cy = top + rowHeight * 0.5f;
            var labelSize = Typography.Measure(label, TextStyles.Subheadline);
            Typography.Draw(new Vector2(origin.X + 14f * scale, cy - labelSize.Y * 0.5f), label, ui.MutedInk,
                TextStyles.Subheadline);
            var valueFit = Typography.FitText(value, width * 0.55f, TextStyles.SubheadlineEmphasized);
            var valueSize = Typography.Measure(valueFit, TextStyles.SubheadlineEmphasized);
            Typography.Draw(new Vector2(max.X - 14f * scale - valueSize.X, cy - valueSize.Y * 0.5f), valueFit,
                ui.TitleInk, TextStyles.SubheadlineEmphasized);
            rowIndex++;
        }

        if (item.Patch.Length > 0)
        {
            InfoRow(Loc.T(L.Collections.Patch), item.Patch);
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            InfoRow(Loc.T(L.Collections.Points), item.Points.ToString(Loc.Culture));
        }

        if (category == CollectionCategory.TriadCards && item.Stats is { } stats)
        {
            InfoRow(Loc.T(L.Collections.CardStats), $"{stats.Top} · {stats.Right} · {stats.Bottom} · {stats.Left}");
        }

        if (item.HasTradeable)
        {
            InfoRow(Loc.T(L.Collections.Tradeable), item.Tradeable ? Loc.T(L.Collections.Yes) : Loc.T(L.Collections.No));
        }

        if (item.Community.Length > 0)
        {
            InfoRow(Loc.T(L.Collections.Community), item.Community);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * rowHeight + 10f * scale));
    }

    private void DrawDetailSources(CollectionItem item)
    {
        if (item.Sources.Length == 0)
        {
            return;
        }

        DrawSectionHeader(Loc.T(L.Collections.HowToObtain));
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = 54f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var max = new Vector2(origin.X + width, origin.Y + item.Sources.Length * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Card * scale, elevated: false);

        for (var index = 0; index < item.Sources.Length; index++)
        {
            var source = item.Sources[index];
            var top = origin.Y + index * rowHeight;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(origin.X + 14f * scale, top), new Vector2(max.X - 14f * scale, top),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            }

            var type = source.Type ?? string.Empty;
            var text = source.Text ?? string.Empty;
            var typeLabel = type.Length > 0 ? type : Loc.T(L.Collections.Source);
            Typography.Draw(new Vector2(origin.X + 14f * scale, top + 11f * scale), typeLabel, ui.TitleInk,
                TextStyles.SubheadlineEmphasized);
            if (text.Length > 0)
            {
                var fitted = Typography.FitText(text, width - 28f * scale, TextStyles.Footnote);
                Typography.Draw(new Vector2(origin.X + 14f * scale, top + 31f * scale), fitted, ui.MutedInk,
                    TextStyles.Footnote);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, item.Sources.Length * rowHeight + 10f * scale));
    }

    private void DrawSectionHeader(string title)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y), Loc.Culture.TextInfo.ToUpper(title), ui.HeaderInk,
            TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(0f, 22f * scale));
    }
}
