using Aetherphone.Core;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private void DrawRoot(Rect area)
    {
        DrawNavBar(area, DisplayName, null);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            if (lodestoneId is null)
            {
                DrawLinkHint();
            }

            DrawCategoryTiles();
        }
    }

    private void DrawLinkHint()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var text = Loc.T(L.Collections.LinkHint);
        var glyphSpace = 30f * scale;
        var maxWidth = width - glyphSpace - 26f * scale;
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, maxWidth).Y;
        var height = MathF.Max(46f * scale, textHeight + 24f * scale);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.13f)));
        Squircle.Stroke(drawList, min, max, 12f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.28f)),
            1f * scale);
        var iconCenter = new Vector2(min.X + 21f * scale, (min.Y + max.Y) * 0.5f);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Link.ToIconString(), ui.Accent, 0.85f);
        var textLeft = iconCenter.X + 15f * scale;
        Typography.DrawWrappedCentered(new Vector2(textLeft + maxWidth * 0.5f, (min.Y + max.Y) * 0.5f - textHeight * 0.5f),
            text, ui.BodyInk, TextStyles.Footnote, maxWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 12f * scale));
    }

    private void DrawCategoryTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var available = ImGui.GetContentRegionAvail().Y;
        var gap = TileGap * scale;
        const int columns = 2;
        var count = CollectionCategories.All.Length;
        var rows = (count + columns - 1) / columns;
        var bottomMargin = 12f * scale;
        var tileWidth = (width - gap) / columns;
        var tileHeight = (available - (rows - 1) * gap - bottomMargin) / rows;
        tileHeight = Math.Clamp(tileHeight, TileHeight * scale, MaxTileHeight * scale);
        var summary = lodestoneId is not null ? catalog.RequestSummary(lodestoneId) : null;

        for (var index = 0; index < count; index++)
        {
            var category = CollectionCategories.All[index];
            var column = index % columns;
            var rowIndex = index / columns;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + rowIndex * (tileHeight + gap));
            var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
            var tileRect = new Rect(min, max);
            if (category == CollectionCategory.Mounts)
            {
                UiAnchors.Report("collections.tile.mounts", tileRect);
            }

            if (DrawTile(tileRect, category, summary, scale))
            {
                OpenCategory(category);
            }
        }

        var totalHeight = rows * tileHeight + (rows - 1) * gap;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight + bottomMargin));
    }

    private bool DrawTile(Rect rect, CollectionCategory category, SummaryEntry? summary, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 18f * scale;
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var tint = CategoryTint(category);
        ui.Card(drawList, rect.Min, rect.Max, rounding, elevated: true);
        Material.TopGlow(drawList, rect.Min, rect.Max, rounding, tint, 0.55f, hovered ? 0.12f : 0.07f);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
        }

        var pad = 14f * scale;
        var tileSize = 38f * scale;
        var tileCenter = new Vector2(rect.Min.X + pad + tileSize * 0.5f, rect.Min.Y + pad + tileSize * 0.5f);
        IconTile.Draw(tileCenter, tileSize, IconTile.Surface(tint), CategoryIcon(category));

        var progress = summary is { State: SummaryState.Ready } ? summary.For(category) : null;
        var total = progress is { Total: > 0 } ? progress.Total : catalog.RequestCatalog(category).Total;
        DrawTileRing(rect, summary, progress, pad, scale);

        var name = Typography.FitText(CategoryLabel(category), rect.Width - pad * 2f, TextStyles.Headline);
        Typography.Draw(new Vector2(rect.Min.X + pad, rect.Max.Y - pad - 36f * scale), name, ui.TitleInk,
            TextStyles.Headline);
        var countLabel = total > 0 ? total.ToString(Loc.Culture) : "-";
        Typography.Draw(new Vector2(rect.Min.X + pad, rect.Max.Y - pad - 15f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawTileRing(Rect rect, SummaryEntry? summary, CategoryProgress? progress, float pad, float scale)
    {
        if (summary is null)
        {
            return;
        }

        var radius = 23f * scale;
        var thickness = 4.2f * scale;
        var center = new Vector2(rect.Max.X - pad - radius, rect.Min.Y + pad + radius);
        var track = Palette.WithAlpha(ui.TitleInk, 0.14f);
        if (progress is null)
        {
            ProgressRing.Track(center, radius, thickness, track);
            if (summary.State == SummaryState.Loading)
            {
                ProgressRing.Sweep(center, radius, thickness, ui.Accent, 900.0, 1.6f, 0.85f);
            }

            return;
        }

        if (!progress.HasPercent)
        {
            ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(ui.TitleInk, 0.10f));
            AppSkin.Icon(center, FontAwesomeIcon.Lock.ToIconString(), Palette.WithAlpha(ui.MutedInk, 0.9f), 0.6f);
            return;
        }

        var fraction = progress.Total > 0 ? Math.Clamp(progress.Count / (float)progress.Total, 0f, 1f) : 0f;
        ProgressRing.Track(center, radius, thickness, track);
        ProgressRing.Fill(center, radius, thickness, fraction, ui.Accent);
        var percent = (int)MathF.Round(fraction * 100f);
        DrawRingPercent(center, radius, thickness, percent + "%", ui.TitleInk);
    }

    private static void DrawRingPercent(Vector2 center, float radius, float thickness, string text, Vector4 color)
    {
        const FontWeight weight = FontWeight.SemiBold;
        var maxWidth = (radius - thickness) * 2f * 0.84f;
        var fontScale = 1.02f;
        var reference = Typography.Measure("100%", fontScale, weight).X;
        if (reference > maxWidth)
        {
            fontScale *= maxWidth / reference;
        }

        var size = Typography.Measure(text, fontScale, weight);
        var position = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f + size.Y * 0.06f);
        Typography.Draw(position, text, color, fontScale, weight);
    }

    private void DrawCategory(Rect area, CollectionCategory category)
    {
        DrawNavBar(area, CategoryLabel(category), back);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        contentBottom = area.Max.Y;
        var entry = catalog.RequestCatalog(category);
        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;
        var summary = lodestoneId is not null ? catalog.RequestSummary(lodestoneId) : null;
        var progress = summary is { State: SummaryState.Ready } ? summary.For(category) : null;
        var trackable = progress?.HasPercent ?? true;
        var ownedUi = trackable ? owned : null;
        var searchBar = new Rect(new Vector2(area.Min.X + pad, top),
            new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        UiAnchors.Report("collections.search", searchBar);
        SearchField.Draw(searchBar, "##collectSearch", Loc.T(L.Collections.Search), ref search, ui.Palette, 60);
        if (search != lastSearch)
        {
            lastSearch = search;
            page = 0;
            resetScroll = true;
        }

        var rowTop = searchBar.Max.Y;
        var hasOwned = ownedUi is { State: OwnedState.Ready };
        if (hasOwned)
        {
            var segmentBar = new Rect(new Vector2(area.Min.X + pad, rowTop),
                new Vector2(area.Max.X - pad, rowTop + SegmentHeight * scale));
            UiAnchors.Report("collections.filters", segmentBar);
            DrawOwnershipSegments(segmentBar);
            rowTop = segmentBar.Max.Y + 4f * scale;
        }

        var hasSources = entry.State == CollectionState.Ready && BuildSourceList(entry);
        if (hasSources)
        {
            var sourceBar = new Rect(new Vector2(area.Min.X + pad, rowTop),
                new Vector2(area.Max.X - pad, rowTop + ChipRowHeight * scale));
            DrawSourceDropdownButton(sourceBar);
            rowTop = sourceBar.Max.Y;
        }
        else
        {
            sourceMenuOpen = false;
        }

        var body = new Rect(new Vector2(area.Min.X, rowTop), area.Max);
        if (entry.State == CollectionState.Failed)
        {
            DrawFailed(body, category);
            return;
        }

        if (entry.State != CollectionState.Ready)
        {
            DrawSpinnerState(body);
            return;
        }

        var sourceFilter = sourceIndex > 0 && sourceIndex <= sourceList.Count
            ? sourceList[sourceIndex - 1]
            : string.Empty;
        CollectionFilter.Apply(entry.Items, filtered, search, ownership, sourceFilter, ownedUi);
        var total = filtered.Count;
        var totalPages = Math.Max(1, (total + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, totalPages - 1);
        var start = page * PageSize;
        var end = Math.Min(start + PageSize, total);
        using (AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            ImGui.Dummy(new Vector2(0f, 2f * scale));
            DrawSummary(entry, ownedUi);
            DrawAccessNotice(progress, owned);
            if (total == 0)
            {
                DrawNoResults(scale);
            }
            else
            {
                DrawList(category, ownedUi, start, end);
                if (totalPages > 1)
                {
                    DrawPager(totalPages);
                }
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
            DrawSourceMenuOverlay();
        }
    }

    private void DrawSummary(CatalogEntry entry, OwnedEntry? owned)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        if (owned is { State: OwnedState.Ready } && entry.Total > 0)
        {
            var fraction = Math.Clamp(owned.Count / (float)entry.Total, 0f, 1f);
            var percent = (int)MathF.Round(fraction * 100f);
            var label = $"{owned.Count} / {entry.Total}";
            Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + 2f * scale), label, ui.TitleInk,
                TextStyles.Title3);
            var pctLabel = Loc.T(L.Collections.CompletePercent, percent);
            var pctSize = Typography.Measure(pctLabel, TextStyles.SubheadlineEmphasized);
            Typography.Draw(new Vector2(origin.X + width - pctSize.X - 2f * scale, origin.Y + 7f * scale), pctLabel,
                ui.Accent, TextStyles.SubheadlineEmphasized);
            var barMin = new Vector2(origin.X + 2f * scale, origin.Y + 32f * scale);
            var barMax = new Vector2(origin.X + width - 2f * scale, barMin.Y + 7f * scale);
            var barRadius = (barMax.Y - barMin.Y) * 0.5f;
            Squircle.Fill(drawList, barMin, barMax, barRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.10f)));
            if (fraction > 0f)
            {
                var fillMax = new Vector2(barMin.X + (barMax.X - barMin.X) * fraction, barMax.Y);
                Squircle.Fill(drawList, barMin, fillMax, barRadius, ImGui.GetColorU32(ui.Accent));
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 48f * scale));
            return;
        }

        var totalLabel = entry.Total > 0 ? entry.Total.ToString(Loc.Culture) : string.Empty;
        if (totalLabel.Length > 0)
        {
            Typography.Draw(new Vector2(origin.X + 2f * scale, origin.Y + 2f * scale), totalLabel, ui.TitleInk,
                TextStyles.Title3);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 34f * scale));
    }

    private void DrawAccessNotice(CategoryProgress? progress, OwnedEntry? owned)
    {
        string message;
        if (progress is { HasPercent: false })
        {
            message = progress.Access == CollectionAccess.Private
                ? Loc.T(L.Collections.CollectionPrivate)
                : Loc.T(L.Collections.CollectionNotTracked);
        }
        else
        {
            message = owned?.State switch
            {
                OwnedState.Private => Loc.T(L.Collections.CollectionPrivate),
                OwnedState.Failed => Loc.T(L.Collections.OwnedUnavailable),
                _ => string.Empty,
            };
        }

        if (message.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var maxWidth = width - 8f * scale;
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y), message, ui.MutedInk,
            TextStyles.Footnote, maxWidth);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void DrawNoResults(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.Dummy(new Vector2(width, 24f * scale));
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        AppSkin.Icon(new Vector2(centerX, origin.Y + 4f * scale), FontAwesomeIcon.SearchMinus.ToIconString(),
            ui.MutedInk, 1.5f);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 40f * scale), Loc.T(L.Collections.NoResults),
            ui.MutedInk, TextStyles.Subheadline);
        ImGui.Dummy(new Vector2(width, 66f * scale));
    }

    private void DrawList(CollectionCategory category, OwnedEntry? owned, int start, int end)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = RowHeight * scale;
        var count = end - start;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var panelMax = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, panelMax, Metrics.Radius.Card * scale, elevated: true);
        var hasOwned = owned is { State: OwnedState.Ready };
        var padding = 14f * scale;
        var separatorLeft = origin.X + padding + IconSize * scale + 12f * scale;
        for (var index = start; index < end; index++)
        {
            var item = filtered[index];
            var rowTop = origin.Y + (index - start) * rowHeight;
            var rowMin = new Vector2(origin.X, rowTop);
            var rowMax = new Vector2(panelMax.X, rowTop + rowHeight);
            var hovered = !sourceMenuOpen && UiInteract.Hover(rowMin, rowMax);
            if (hovered)
            {
                Squircle.Fill(drawList, new Vector2(rowMin.X + 4f * scale, rowMin.Y + 3f * scale),
                    new Vector2(rowMax.X - 4f * scale, rowMax.Y - 3f * scale), 12f * scale,
                    ImGui.GetColorU32(ui.HoverTint));
            }
            else if (index > start)
            {
                drawList.AddLine(new Vector2(separatorLeft, rowTop), new Vector2(rowMax.X - padding, rowTop),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            }

            var row = new Rect(new Vector2(rowMin.X + padding, rowMin.Y), new Vector2(rowMax.X - padding, rowMax.Y));
            DrawRow(row, item, hasOwned && owned!.Ids.Contains(item.Id), hasOwned, scale);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    OpenItem(category, item);
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, count * rowHeight));
    }

    private void DrawRow(Rect row, CollectionItem item, bool isOwned, bool hasOwned, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconBox = IconSize * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconBox * 0.5f);
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        DrawIcon(drawList, item, iconMin, iconMax, 11f * scale);
        var textLeft = iconMax.X + 12f * scale;
        var textRight = row.Max.X - (hasOwned ? 30f * scale : 4f * scale);
        var textWidth = MathF.Max(24f * scale, textRight - textLeft);
        var subtitle = SubtitleOf(item);
        var name = Typography.FitText(item.Name, textWidth, TextStyles.BodyEmphasized);
        if (subtitle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, row.Center.Y - 16f * scale), name, ui.TitleInk,
                TextStyles.BodyEmphasized);
            var fittedSub = Typography.FitText(subtitle, textWidth, TextStyles.Footnote);
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), fittedSub, ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            var nameSize = Typography.Measure(name, TextStyles.BodyEmphasized);
            Typography.Draw(new Vector2(textLeft, row.Center.Y - nameSize.Y * 0.5f), name, ui.TitleInk,
                TextStyles.BodyEmphasized);
        }

        if (hasOwned)
        {
            DrawOwnedTick(drawList, new Vector2(row.Max.X - 11f * scale, row.Center.Y), isOwned, scale);
        }
    }

    private void DrawOwnedTick(ImDrawListPtr drawList, Vector2 center, bool isOwned, float scale)
    {
        var radius = 10f * scale;
        if (!isOwned)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.45f)), 20,
                1.6f * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(frameTheme.ToggleOn), 20);
        DrawCheck(drawList, center, new Vector4(1f, 1f, 1f, 1f), scale);
    }

    private void DrawPager(int totalPages)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PagerHeight * scale;
        var centerY = origin.Y + height * 0.5f;
        if (DrawPagerButton(new Vector2(origin.X + 22f * scale, centerY), true, page > 0, scale))
        {
            page--;
            resetScroll = true;
        }

        if (DrawPagerButton(new Vector2(origin.X + width - 22f * scale, centerY), false, page < totalPages - 1, scale))
        {
            page++;
            resetScroll = true;
        }

        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, centerY), $"{page + 1} / {totalPages}",
            ui.MutedInk, TextStyles.SubheadlineEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawPagerButton(Vector2 center, bool left, bool enabled, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 17f * scale;
        var hovered = enabled &&
            UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        var fill = hovered ? Palette.WithAlpha(ui.Accent, 0.22f) : ui.FieldSurface;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 28);
        var arrowColor = enabled ? ui.Accent : Palette.WithAlpha(ui.MutedInk, 0.4f);
        var color = ImGui.GetColorU32(arrowColor);
        var thickness = 2f * scale;
        var tipX = center.X + (left ? -3.5f : 3.5f) * scale;
        var baseX = center.X + (left ? 3.5f : -3.5f) * scale;
        drawList.AddLine(new Vector2(baseX, center.Y - 5.5f * scale), new Vector2(tipX, center.Y), color, thickness);
        drawList.AddLine(new Vector2(baseX, center.Y + 5.5f * scale), new Vector2(tipX, center.Y), color, thickness);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawOwnershipSegments(Rect bar)
    {
        var selected = SegmentStrip.Draw("collections.ownership", bar, ownershipLabels, (int)ownership, ui.Palette);
        if (selected != (int)ownership)
        {
            ownership = (OwnershipFilter)selected;
            resetScroll = true;
            page = 0;
        }
    }

    private bool BuildSourceList(CatalogEntry entry)
    {
        CollectionFilter.CollectSourceTypes(entry.Items, sourceSet);
        sourceList.Clear();
        foreach (var type in sourceSet)
        {
            sourceList.Add(type);
        }

        if (sourceIndex > sourceList.Count)
        {
            sourceIndex = 0;
        }

        return sourceList.Count > 0;
    }

    private void DrawSourceDropdownButton(Rect bar)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var height = DropdownHeight * scale;
        var min = new Vector2(bar.Min.X, bar.Center.Y - height * 0.5f);
        var max = new Vector2(bar.Max.X, bar.Center.Y + height * 0.5f);
        sourceMenuAnchor = new Rect(min, max);
        var active = sourceIndex != 0;
        var hovered = UiInteract.Hover(min, max);
        var radius = height * 0.5f;
        var fill = active ? Palette.WithAlpha(ui.Accent, 0.18f) : ui.FieldSurface;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.5f)),
                1f * scale);
        }

        if (hovered || sourceMenuOpen)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
        }

        var label = sourceIndex == 0 ? Loc.T(L.Collections.AllSources) : sourceList[sourceIndex - 1];
        var ink = active ? ui.Accent : ui.TitleInk;
        var fitted = Typography.FitText(label, max.X - min.X - 46f * scale, TextStyles.FootnoteEmphasized);
        var textSize = Typography.Measure(fitted, TextStyles.FootnoteEmphasized);
        Typography.Draw(new Vector2(min.X + 16f * scale, bar.Center.Y - textSize.Y * 0.5f), fitted, ink,
            TextStyles.FootnoteEmphasized);
        DrawChevron(drawList, new Vector2(max.X - 16f * scale, bar.Center.Y), 4.5f * scale, sourceMenuOpen, ui.MutedInk,
            scale);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            sourceMenuOpen = !sourceMenuOpen;
        }
    }

    private void DrawSourceMenuOverlay()
    {
        if (!sourceMenuOpen)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var pad = 6f * scale;
        var rowHeight = MenuRowHeight * scale;
        var optionCount = sourceList.Count + 1;
        var menuTop = sourceMenuAnchor.Max.Y + 5f * scale;
        var available = contentBottom - menuTop - 10f * scale;
        var maxRows = Math.Max(1, (int)((available - pad * 2f) / rowHeight));
        var visible = Math.Min(optionCount, maxRows);
        var height = visible * rowHeight + pad * 2f;
        var min = new Vector2(sourceMenuAnchor.Min.X, menuTop);
        var max = new Vector2(sourceMenuAnchor.Max.X, menuTop + height);
        UiInteract.HoverOverlay(new Rect(min, max));
        Elevation.Floating(drawList, min, max, 14f * scale, scale);
        Material.Frosted(drawList, min, max, 14f * scale, scale);
        var clicked = -1;
        for (var index = 0; index < visible; index++)
        {
            var rowMin = new Vector2(min.X + pad, menuTop + pad + index * rowHeight);
            var rowMax = new Vector2(max.X - pad, rowMin.Y + rowHeight);
            var centerY = (rowMin.Y + rowMax.Y) * 0.5f;
            var label = index == 0 ? Loc.T(L.Collections.AllSources) : sourceList[index - 1];
            var selected = index == sourceIndex;
            var hovered = ImGui.IsMouseHoveringRect(rowMin, rowMax);
            if (hovered)
            {
                Squircle.Fill(drawList, rowMin, rowMax, 9f * scale, ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var ink = selected ? ui.Accent : ui.TitleInk;
            var fitted = Typography.FitText(label, rowMax.X - rowMin.X - 34f * scale, TextStyles.Subheadline);
            var textSize = Typography.Measure(fitted, TextStyles.Subheadline);
            Typography.Draw(new Vector2(rowMin.X + 12f * scale, centerY - textSize.Y * 0.5f), fitted, ink,
                TextStyles.Subheadline);
            if (selected)
            {
                DrawCheck(drawList, new Vector2(rowMax.X - 13f * scale, centerY), ui.Accent, scale);
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                clicked = index;
            }
        }

        if (clicked >= 0)
        {
            sourceIndex = clicked;
            sourceMenuOpen = false;
            resetScroll = true;
            page = 0;
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max, false) &&
            !ImGui.IsMouseHoveringRect(sourceMenuAnchor.Min, sourceMenuAnchor.Max, false))
        {
            sourceMenuOpen = false;
        }
    }

    private static void DrawCheck(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var check = ImGui.GetColorU32(color);
        var thickness = 1.8f * scale;
        drawList.AddLine(center + new Vector2(-4f * scale, 0f), center + new Vector2(-1.2f * scale, 3.2f * scale),
            check, thickness);
        drawList.AddLine(center + new Vector2(-1.2f * scale, 3.2f * scale), center + new Vector2(4.4f * scale,
            -3.6f * scale), check, thickness);
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, float size, bool up, Vector4 color,
        float scale)
    {
        var col = ImGui.GetColorU32(color);
        var thickness = 1.6f * scale;
        var dy = (up ? -1f : 1f) * size * 0.5f;
        var tip = new Vector2(center.X, center.Y + dy);
        drawList.AddLine(new Vector2(center.X - size, center.Y - dy), tip, col, thickness);
        drawList.AddLine(new Vector2(center.X + size, center.Y - dy), tip, col, thickness);
    }

    private void DrawFailed(Rect body, CollectionCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Collections.Failed), string.Empty);
        var label = Loc.T(L.Collections.TryAgain);
        var width = Typography.Measure(label, TextStyles.BodyEmphasized).X + 44f * scale;
        var height = 38f * scale;
        var min = new Vector2(body.Center.X - width * 0.5f, body.Center.Y + 34f * scale);
        var rect = new Rect(min, min + new Vector2(width, height));
        if (ui.PillButton(rect, label, true))
        {
            catalog.Retry(category);
        }
    }

    private void DrawSpinnerState(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = body.Center;
        LoadingPulse.Draw(new Vector2(center.X, center.Y - 14f * scale), 13f * scale, ui.Accent, ui.MutedInk,
            Loc.T(L.Common.Loading));
    }
}
