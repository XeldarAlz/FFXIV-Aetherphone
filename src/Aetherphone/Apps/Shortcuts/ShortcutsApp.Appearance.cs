using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp
{
    private const int SwatchColumns = 6;
    private const float PreviewHeight = 150f;
    private const float PreviewTile = 84f;
    private const float PreviewGlowCoverage = 0.8f;
    private const float PreviewGlowStrength = 0.22f;
    private const float PreviewNameGap = 12f;
    private const float GridPad = 12f;
    private const float SwatchFraction = 0.34f;
    private const float SwatchRingGap = 4f;
    private const float HexRowHeight = 40f;
    private const float HexDot = 12f;
    private const float SourceTileHeight = 76f;
    private const float SourceIcon = 36f;
    private const float GlyphTileFraction = 0.78f;
    private const float GlyphFraction = 0.42f;

    private string hexBuffer = string.Empty;

    private void OpenAppearance()
    {
        if (draft is null)
        {
            return;
        }

        hexBuffer = draft.Tint;
        Push(ShortcutsRoute.Appearance, EditorTitle());
    }

    private void DrawAppearance(in PhoneContext context, ShortcutsView view)
    {
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        if (draft is not null)
        {
            using (AppSurface.Begin(navBar.Body))
            {
                var drawList = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                var width = ScrollLayout.StableContentWidth();
                var cursorY = DrawAppearancePreview(drawList, origin, width, scale);
                cursorY = DrawColorSection(drawList, new Vector2(origin.X, cursorY), width, scale);
                cursorY = DrawImageSection(drawList, new Vector2(origin.X, cursorY), width, scale);
                cursorY = DrawSymbolSection(drawList, new Vector2(origin.X, cursorY), width, scale);
                ShortcutsArt.ReserveTo(origin, width, cursorY + ShortcutsArt.BottomPad * scale);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "shortcuts.nav.appearance", Loc.T(L.Shortcuts.Appearance),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private float DrawAppearancePreview(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + PreviewHeight * scale);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        Material.TopGlow(drawList, origin, max, Metrics.Radius.Widget * scale, ShortcutTint.Resolve(draft!.Tint),
            PreviewGlowCoverage, PreviewGlowStrength);
        var tile = PreviewTile * scale;
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var blockTop = origin.Y + (max.Y - origin.Y - tile - PreviewNameGap * scale - nameHeight) * 0.5f;
        var center = new Vector2(origin.X + width * 0.5f, blockTop + tile * 0.5f);
        ShortcutArt.DrawSurface(drawList, center, tile, draft, store.Icon(draft), scale);
        var name = Typography.FitText(EditorTitle(), width - Metrics.Space.Lg * 2f * scale, TextStyles.Headline);
        var size = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, blockTop + tile + PreviewNameGap * scale),
            name, ui.TitleInk, TextStyles.Headline);
        return max.Y;
    }

    private float DrawColorSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale * 0.5f;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width, Loc.T(L.Shortcuts.Color),
            ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
        var pad = GridPad * scale;
        var cell = (width - pad * 2f) / SwatchColumns;
        var rows = (ShortcutPalette.Wheel.Length + SwatchColumns - 1) / SwatchColumns;
        var gridHeight = rows * cell;
        var hexHeight = HexRowHeight * scale;
        var max = new Vector2(origin.X + width, top + pad + gridHeight + hexHeight + pad * 2f);
        ui.Card(drawList, new Vector2(origin.X, top), max, Metrics.Radius.Grouped * scale);
        var selected = ShortcutTint.Resolve(draft!.Tint);
        var radius = cell * SwatchFraction;
        for (var index = 0; index < ShortcutPalette.Wheel.Length; index++)
        {
            var color = ShortcutPalette.Wheel[index];
            var center = new Vector2(origin.X + pad + (index % SwatchColumns + 0.5f) * cell,
                top + pad + (index / SwatchColumns + 0.5f) * cell);
            var hit = new Vector2(radius + SwatchRingGap * scale);
            var hovered = UiInteract.Hover(center - hit, center + hit);
            var grow = PressFx.Scale(ImGui.GetID($"##shortcutSwatch{index}"),
                hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), PressFx.IconPressedScale);
            drawList.AddCircleFilled(center, radius * grow, ImGui.GetColorU32(color), 32);
            if (SameColor(color, selected))
            {
                drawList.AddCircle(center, radius + SwatchRingGap * scale, ImGui.GetColorU32(ui.TitleInk), 36,
                    Metrics.Stroke.Ring * scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(center - hit, center + hit, hovered))
            {
                draft.Tint = HexColor.ToDigits(color);
                hexBuffer = draft.Tint;
            }
        }

        var hexTop = top + pad + gridHeight + pad;
        var field = new Rect(new Vector2(origin.X + pad, hexTop), new Vector2(max.X - pad, hexTop + hexHeight));
        SearchBar.Surface(drawList, field, ui.Ink);
        var dot = HexDot * scale;
        var dotCenter = new Vector2(field.Min.X + Metrics.Space.Md * scale + dot * 0.5f, field.Center.Y);
        drawList.AddCircleFilled(dotCenter, dot * 0.5f, ImGui.GetColorU32(selected), 20);
        var inputLeft = dotCenter.X + dot * 0.5f + Metrics.Space.Sm * scale;
        ImGui.SetCursorScreenPos(new Vector2(inputLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(field.Max.X - Metrics.Space.Md * scale - inputLeft);
        var edited = false;
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            edited = ImGui.InputTextWithHint("##shortcutHex", Loc.T(L.Shortcuts.CustomColor), ref hexBuffer, 7,
                ImGuiInputTextFlags.CharsNoBlank);
        }

        if (edited && HexColor.TryParse(hexBuffer, out var custom))
        {
            draft.Tint = HexColor.ToDigits(custom);
        }

        return max.Y;
    }

    private static bool SameColor(Vector4 first, Vector4 second) =>
        MathF.Abs(first.X - second.X) < 0.004f && MathF.Abs(first.Y - second.Y) < 0.004f &&
        MathF.Abs(first.Z - second.Z) < 0.004f;

    private float DrawImageSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale * 0.5f;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width,
            Loc.T(L.Shortcuts.ImageSection), ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
        var gap = ShortcutsArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var height = SourceTileHeight * scale;
        var pluginRect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + tileWidth, top + height));
        var photoRect = new Rect(new Vector2(pluginRect.Max.X + gap, top), new Vector2(origin.X + width, top + height));
        var entry = draft!;
        var pluginActive = entry.IconPlugin.Length > 0 && entry.IconImage.Length == 0;
        var photoActive = entry.IconImage.Length > 0;
        var pluginValue = pluginActive ? catalog.DisplayName(entry.IconPlugin) : Loc.T(L.Shortcuts.PluginIconNone);
        var photoValue = photoActive ? Loc.T(L.Shortcuts.CustomIcon) : Loc.T(L.Shortcuts.PluginIconNone);
        if (DrawSourceTile(drawList, pluginRect, "##shortcutSourcePlugin", FontAwesomeIcon.PuzzlePiece,
                Loc.T(L.Shortcuts.PluginIcon), pluginValue, pluginActive, ShortcutsArt.PluginTint, scale))
        {
            OpenPluginPicker(PickerPurpose.Icon, -1);
        }

        if (DrawSourceTile(drawList, photoRect, "##shortcutSourcePhoto", FontAwesomeIcon.Image,
                Loc.T(L.Shortcuts.CustomIcon), photoValue, photoActive, ShortcutsArt.LinkTint, scale))
        {
            OpenCustomIconPicker();
        }

        return top + height;
    }

    private bool DrawSourceTile(ImDrawListPtr drawList, Rect rect, string id, FontAwesomeIcon glyph, string title,
        string value, bool active, Vector4 tint, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var drawn = Pressed(rect, ImGui.GetID(id), hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, drawn.Min, drawn.Max, Metrics.Radius.Grouped * scale);
        if (hovered)
        {
            Squircle.Fill(drawList, drawn.Min, drawn.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (active)
        {
            Squircle.Stroke(drawList, drawn.Min, drawn.Max, radius, ImGui.GetColorU32(ui.Accent),
                Metrics.Stroke.Ring * scale);
        }

        var pad = Metrics.Space.Md * scale;
        var icon = SourceIcon * scale;
        var iconMin = new Vector2(drawn.Min.X + pad, drawn.Center.Y - icon * 0.5f);
        var texture = active ? store.Icon(draft!) : null;
        if (texture is not null)
        {
            Squircle.FillImage(drawList, iconMin, iconMin + new Vector2(icon), icon * Metrics.Radius.TileFactor,
                texture.Handle, 0xFFFFFFFFu);
        }
        else
        {
            IconTile.FillShaded(drawList, iconMin, iconMin + new Vector2(icon), icon * Metrics.Radius.TileFactor,
                IconTile.Surface(tint));
            ProgressRing.CenterIcon(drawList, iconMin + new Vector2(icon * 0.5f), glyph, AccentRing.Ink, icon * 0.44f);
        }

        var textLeft = iconMin.X + icon + Metrics.Space.Sm * scale;
        Labels(drawList, textLeft, drawn.Max.X - pad, drawn.Center.Y, title, value, ui.TitleInk,
            active ? ui.Accent : ui.MutedInk, scale);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private float DrawSymbolSection(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y + ShortcutsArt.SectionGap * scale * 0.5f;
        top += CardSectionHeader.Draw(drawList, new Vector2(origin.X, top), width, Loc.T(L.Shortcuts.Symbol),
            ui.TitleInk) + ShortcutsArt.HeaderGap * scale;
        var pad = GridPad * scale;
        var cell = (width - pad * 2f) / SwatchColumns;
        var total = ShortcutPalette.Glyphs.Length + 1;
        var rows = (total + SwatchColumns - 1) / SwatchColumns;
        var max = new Vector2(origin.X + width, top + pad * 2f + rows * cell);
        ui.Card(drawList, new Vector2(origin.X, top), max, Metrics.Radius.Grouped * scale);
        var entry = draft!;
        var usesImage = entry.IconPlugin.Length > 0 || entry.IconImage.Length > 0;
        var tint = ShortcutTint.Resolve(entry.Tint);
        var clipTop = drawList.GetClipRectMin().Y;
        var clipBottom = drawList.GetClipRectMax().Y;
        for (var index = 0; index < total; index++)
        {
            var center = new Vector2(origin.X + pad + (index % SwatchColumns + 0.5f) * cell,
                top + pad + (index / SwatchColumns + 0.5f) * cell);
            if (center.Y + cell < clipTop || center.Y - cell > clipBottom)
            {
                continue;
            }

            var glyph = index == 0 ? 0 : (int)ShortcutPalette.Glyphs[index - 1];
            var active = !usesImage && entry.Glyph == glyph;
            var size = cell * GlyphTileFraction;
            var half = new Vector2(size * 0.5f);
            var hovered = UiInteract.Hover(center - half, center + half);
            var tileRadius = size * Metrics.Radius.TileFactor;
            if (active)
            {
                IconTile.FillShaded(drawList, center - half, center + half, tileRadius, IconTile.Surface(tint));
            }
            else
            {
                Squircle.Fill(drawList, center - half, center + half, tileRadius,
                    ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk,
                        hovered ? FillLevel.Secondary : FillLevel.Tertiary)));
            }

            var ink = active ? AccentRing.Ink : ui.TitleInk;
            if (index == 0)
            {
                Typography.DrawCentered(drawList, center, ShortcutsArt.Monogram(entry.Name), ink,
                    TextStyles.Headline.Scale, TextStyles.Headline.Weight);
            }
            else
            {
                ProgressRing.CenterIcon(drawList, center, ShortcutPalette.Glyphs[index - 1], ink, size * GlyphFraction);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!UiInteract.Click(center - half, center + half, hovered))
            {
                continue;
            }

            var unsavedIcon = UnsavedIconOf(entry);
            if (unsavedIcon.Length > 0)
            {
                store.ReleaseIcon(unsavedIcon);
            }

            entry.Glyph = glyph;
            entry.IconPlugin = string.Empty;
            entry.IconImage = string.Empty;
        }

        return max.Y;
    }
}
