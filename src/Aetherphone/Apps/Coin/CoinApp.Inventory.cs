using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin;

internal sealed partial class CoinApp
{
    private const float SwatchDiameter = 78f;
    private const float SwatchGap = 12f;
    private const float SwatchRingOverhang = 6f;
    private const float SwatchLabelHeight = 18f;
    private const float BadgeRowHeight = 52f;
    private const float SectionGap = 18f;
    private const float SectionHeaderHeight = 30f;
    private const float SectionHintGap = 10f;
    private const float NameColorMarkerRadius = 15f;
    private const float NameColorMarkerGap = 8f;
    private const float NameColorGlyphScale = 0.55f;
    private const int NameColorSlot = 1;

    private void DrawInventory(Rect body)
    {
        inventory.EnsureFresh();
        frameCatalog.EnsureFresh();
        catalog.EnsureFresh();

        using var surface = AppSurface.Begin(body);
        var scale = UiScale.Current;
        inventoryRefresh.Draw(body, surface.Pull, surface.Dragging, inventory.Fetching, ui.MutedInk,
            RefreshInventory);

        if (!inventory.LoadedOnce)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var width = ScrollLayout.StableContentWidth();
        DrawFrameSection(width, scale);
        ImGui.Dummy(new Vector2(0f, SectionGap * scale));
        DrawBadgeSection(width, scale);
        ImGui.Dummy(new Vector2(0f, 16f * scale));
    }

    private void DrawFrameSection(float width, float scale)
    {
        var section = inventory.Section(LoadoutStore.FrameKind);
        var items = section?.Items ?? Array.Empty<InventoryItemDto>();
        DrawSectionHeader(Loc.T(L.Loadout.FramesTitle), WornCount(items), section?.Slots ?? 0, width, scale);

        if (items.Length == 0)
        {
            DrawSectionEmpty(Loc.T(L.Loadout.FramesEmpty), Loc.T(L.Loadout.FramesEmptyHint), width, scale);
            return;
        }

        var diameter = SwatchDiameter * scale;
        var gap = SwatchGap * scale;
        var ringPad = SwatchRingOverhang * scale;
        var cellWidth = diameter + gap;
        var perRow = MathF.Max(1f, MathF.Floor((width - ringPad) / cellWidth));
        var cellHeight = diameter + SwatchLabelHeight * scale + gap;
        var totalCells = items.Length + 1;
        var rows = MathF.Ceiling(totalCells / perRow);

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < totalCells; index++)
        {
            var column = index % (int)perRow;
            var row = index / (int)perRow;
            var cellMin = new Vector2(origin.X + ringPad + column * cellWidth, origin.Y + row * cellHeight);
            var center = new Vector2(cellMin.X + diameter * 0.5f, cellMin.Y + diameter * 0.5f);

            if (index == 0)
            {
                DrawNoneSwatch(drawList, center, diameter * 0.5f, cellMin, diameter, scale);
                continue;
            }

            DrawFrameSwatch(drawList, items[index - 1], center, diameter * 0.5f, cellMin, diameter, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * cellHeight));
    }

    private void DrawNoneSwatch(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 cellMin, float diameter,
        float scale)
    {
        var selected = WornFrameId().Length == 0;
        DrawSwatchRing(drawList, center, radius, selected, scale);
        drawList.AddCircleFilled(center, radius - 4f * scale, ImGui.GetColorU32(ui.Palette.FieldSurface), 48);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Ban, ui.MutedInk, radius * 0.6f);
        DrawSwatchLabel(drawList, Loc.T(L.Loadout.NoneOption), cellMin, diameter, scale);

        if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius))
            && !selected)
        {
            inventory.Equip(LoadoutStore.FrameKind, WornFrameId(), 0);
        }
    }

    private void DrawFrameSwatch(ImDrawListPtr drawList, InventoryItemDto item, Vector2 center, float radius,
        Vector2 cellMin, float diameter, float scale)
    {
        var selected = item.Slot > 0;
        DrawSwatchRing(drawList, center, radius, selected, scale);

        var style = item.Frame is null ? null : FrameStyle.From(item.Frame);
        var avatarRadius = radius / (style?.Scale ?? 1f);
        var user = session.CurrentUser;
        AvatarView.DrawRemote(drawList, center, avatarRadius, theme, user?.Name ?? string.Empty,
            user?.World ?? string.Empty, user?.AvatarUrl, images, lodestone, 0.8f, 40, 1f, style);

        DrawSwatchLabel(drawList, style?.Name ?? string.Empty, cellMin, diameter, scale);

        if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
        {
            inventory.Equip(LoadoutStore.FrameKind, item.Id, selected ? 0 : 1);
        }
    }

    private void DrawSwatchRing(ImDrawListPtr drawList, Vector2 center, float radius, bool selected, float scale)
    {
        if (!selected)
        {
            return;
        }

        drawList.AddCircle(center, radius + 3f * scale, ImGui.GetColorU32(ui.Palette.Accent), 48,
            Metrics.Stroke.Ring * scale * 1.6f);
    }

    private void DrawSwatchLabel(ImDrawListPtr drawList, string label, Vector2 cellMin, float diameter, float scale)
    {
        if (label.Length == 0)
        {
            return;
        }

        var fitted = Typography.FitText(label, diameter, TextStyles.Caption2);
        var size = Typography.Measure(fitted, TextStyles.Caption2);
        var position = new Vector2(cellMin.X + (diameter - size.X) * 0.5f, cellMin.Y + diameter + 4f * scale);
        Typography.Draw(drawList, position, fitted, ui.MutedInk, TextStyles.Caption2);
    }

    private void DrawBadgeSection(float width, float scale)
    {
        var section = inventory.Section(LoadoutStore.BadgeKind);
        var items = section?.Items ?? Array.Empty<InventoryItemDto>();
        DrawSectionHeader(Loc.T(L.Loadout.BadgesTitle), WornCount(items), section?.Slots ?? 0, width, scale);

        if (items.Length == 0)
        {
            DrawSectionEmpty(Loc.T(L.Loadout.BadgesEmpty), Loc.T(L.Loadout.BadgesEmptyHint), width, scale);
            return;
        }

        DrawSectionHint(Loc.T(L.Loadout.BadgesHint), width, scale);

        var rowHeight = BadgeRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < items.Length; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            var row = new Rect(origin, origin + new Vector2(width, rowHeight));
            DrawBadgeRow(drawList, items[index], row, index, scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private void DrawSectionHint(string hint, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(origin, hint, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + SectionHintGap * scale));
    }

    private void DrawBadgeRow(ImDrawListPtr drawList, InventoryItemDto item, Rect row, int index, float scale)
    {
        var light = RoleInk.IsLight(theme);
        var worn = item.Slot > 0;
        var inset = 14f * scale;
        var glyph = 22f * scale;
        var center = new Vector2(row.Min.X + inset + glyph * 0.5f, row.Center.Y);

        var style = item.Badge is null ? null : BadgeStyle.From(item.Badge);
        BadgeStrip.DrawOne(drawList, center, style, images, light, glyph);

        var pillWidth = 74f * scale;
        var pillHeight = 30f * scale;
        var pill = new Rect(
            new Vector2(row.Max.X - inset - pillWidth, row.Center.Y - pillHeight * 0.5f),
            new Vector2(row.Max.X - inset, row.Center.Y + pillHeight * 0.5f));

        var labelRight = worn ? DrawNameColorMarker(item, pill, index, scale) : pill.Min.X;
        var labelLeft = center.X + glyph * 0.5f + 12f * scale;
        var labelWidth = MathF.Max(1f, labelRight - 10f * scale - labelLeft);
        var name = style?.Name ?? string.Empty;
        var labelSize = Typography.Measure(name, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(new MarqueeId("inventory.badge.", index), name, labelLeft, row.Center.Y - labelSize.Y * 0.5f,
            labelWidth, TextStyles.BodyEmphasized, theme.TextStrong);

        var label = worn ? item.Slot.ToString(Loc.Culture) : Loc.T(L.Loadout.Wear);
        if (ui.PillButton(pill, label, worn, "inventory.badge.toggle." + index))
        {
            inventory.Equip(LoadoutStore.BadgeKind, item.Id, worn ? 0 : null);
        }
    }

    private float DrawNameColorMarker(InventoryItemDto item, Rect pill, int index, float scale)
    {
        var radius = NameColorMarkerRadius * scale;
        var center = new Vector2(pill.Min.X - NameColorMarkerGap * scale - radius, pill.Center.Y);
        var glyph = IconGlyph.Of(FontAwesomeIcon.Palette);
        if (item.Slot == NameColorSlot)
        {
            AppSkin.Icon(center, glyph, ui.Palette.Accent, NameColorGlyphScale);
            HoverTooltip.Show("inventory.badge.colors." + index,
                new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
                Loc.T(L.Loadout.ColorsName));
            return center.X - radius;
        }

        if (ui.IconButton(center, radius, glyph, ui.MutedInk, ui.Palette.FieldSurface, NameColorGlyphScale,
                Loc.T(L.Loadout.UseForNameColor)))
        {
            inventory.Equip(LoadoutStore.BadgeKind, item.Id, NameColorSlot);
        }

        return center.X - radius;
    }

    private void DrawSectionHeader(string title, int worn, int slots, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, origin, title, theme.TextStrong, TextStyles.Title3);

        var counter = Loc.T(L.Loadout.SlotsUsed, worn, slots);
        var size = Typography.Measure(counter, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(origin.X + width - size.X, origin.Y + 4f * scale), counter,
            ui.MutedInk, TextStyles.Caption1);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, SectionHeaderHeight * scale));
    }

    private void DrawSectionEmpty(string title, string hint, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = 96f * scale;
        EmptyState.Draw(new Rect(origin, origin + new Vector2(width, height)), ui, FontAwesomeIcon.Store, title, hint);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private string WornFrameId()
    {
        var section = inventory.Section(LoadoutStore.FrameKind);
        if (section is null)
        {
            return string.Empty;
        }

        for (var index = 0; index < section.Items.Length; index++)
        {
            if (section.Items[index].Slot > 0)
            {
                return section.Items[index].Id;
            }
        }

        return string.Empty;
    }

    private static int WornCount(InventoryItemDto[] items)
    {
        var worn = 0;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].Slot > 0)
            {
                worn++;
            }
        }

        return worn;
    }
}
