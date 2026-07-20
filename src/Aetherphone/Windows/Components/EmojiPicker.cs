using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class EmojiPicker
{
    private const int Columns = 8;

    private static readonly Vector4[] ToneSwatches =
    {
        new(1.00f, 0.82f, 0.27f, 1f),
        new(0.98f, 0.85f, 0.74f, 1f),
        new(0.88f, 0.73f, 0.58f, 1f),
        new(0.75f, 0.56f, 0.41f, 1f),
        new(0.61f, 0.39f, 0.24f, 1f),
        new(0.35f, 0.27f, 0.22f, 1f),
    };

    private readonly List<int> view = new();
    private string search = string.Empty;
    private string lastSearch = string.Empty;
    private int category;
    private int lastCategory = -1;
    private int tone;
    private bool resetScroll;

    public string? Draw(Rect area, in AppSkin ui)
    {
        if (!EmojiCatalog.Ready)
        {
            return null;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var theme = ui.Theme;
        string? picked = null;
        ImGui.SetCursorScreenPos(area.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var panel = ImRaii.Child("##emojiPanel", area.Size, false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
        {
            if (!panel)
            {
                return null;
            }

            var drawList = ImGui.GetWindowDrawList();
            var background = theme.AppBackground;
            drawList.AddRectFilled(area.Min, area.Max,
                ImGui.GetColorU32(new Vector4(background.X, background.Y, background.Z, 1f)));
            drawList.AddLine(area.Min, new Vector2(area.Max.X, area.Min.Y), ImGui.GetColorU32(theme.Separator), 1f);

            var pad = 10f * scale;
            var innerLeft = area.Min.X + pad;
            var innerRight = area.Max.X - pad;
            var rowHeight = 30f * scale;
            var tabsTop = area.Min.Y + pad;
            DrawHeader(drawList, innerLeft, innerRight, tabsTop, rowHeight, ui);

            var searchTop = tabsTop + rowHeight + 6f * scale;
            var searchRect = new Rect(new Vector2(innerLeft, searchTop),
                new Vector2(innerRight, searchTop + rowHeight));
            SearchField.Draw(searchRect, "##emojiSearch", Loc.T(L.Common.Search), ref search, theme);

            RebuildViewIfNeeded();

            var gridRect = new Rect(new Vector2(area.Min.X, searchTop + rowHeight + 6f * scale), area.Max);
            picked = DrawGrid(gridRect, ui);
        }

        return picked;
    }

    private void DrawHeader(ImDrawListPtr drawList, float left, float right, float top, float height, in AppSkin ui)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var centerY = top + height * 0.5f;
        var toneRadius = height * 0.5f;
        var toneCenter = new Vector2(right - toneRadius, centerY);
        DrawToneSwatch(drawList, toneCenter, toneRadius, ui);

        var tabsRight = toneCenter.X - toneRadius - 8f * scale;
        var groups = EmojiCatalog.Groups;
        if (groups.Length == 0)
        {
            return;
        }

        var slot = (tabsRight - left) / groups.Length;
        for (var index = 0; index < groups.Length; index++)
        {
            var cellCenter = new Vector2(left + slot * (index + 0.5f), centerY);
            var cellMin = new Vector2(left + slot * index + 2f * scale, top + 2f * scale);
            var cellMax = new Vector2(left + slot * (index + 1) - 2f * scale, top + height - 2f * scale);
            var active = index == category && search.Length == 0;
            if (active)
            {
                Squircle.Fill(drawList, cellMin, cellMax, 8f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.24f)));
            }

            EmojiCatalog.GroupRange(index, out var start, out var end);
            if (end > start)
            {
                var glyph = height * 0.62f;
                var iconMin = new Vector2(cellCenter.X - glyph * 0.5f, centerY - glyph * 0.5f);
                EmojiImages.TryDraw(drawList, EmojiCatalog.Glyphs[start].File, iconMin,
                    iconMin + new Vector2(glyph, glyph), 0xFFFFFFFF);
            }

            if (UiInteract.HoverClick(cellMin, cellMax))
            {
                category = index;
                search = string.Empty;
                resetScroll = true;
            }
        }
    }

    private void DrawToneSwatch(ImDrawListPtr drawList, Vector2 center, float radius, in AppSkin ui)
    {
        drawList.AddCircleFilled(center, radius * 0.7f, ImGui.GetColorU32(ToneSwatches[tone]), 20);
        drawList.AddCircle(center, radius * 0.7f, ImGui.GetColorU32(Palette.WithAlpha(ui.Theme.TextStrong, 0.35f)), 20,
            1f);
        if (UiInteract.HoverClick(center - new Vector2(radius, radius), center + new Vector2(radius, radius)))
        {
            tone = (tone + 1) % ToneSwatches.Length;
        }
    }

    private void RebuildViewIfNeeded()
    {
        if (search == lastSearch && category == lastCategory)
        {
            return;
        }

        lastSearch = search;
        lastCategory = category;
        resetScroll = true;
        view.Clear();
        var glyphs = EmojiCatalog.Glyphs;
        if (search.Length == 0)
        {
            EmojiCatalog.GroupRange(category, out var start, out var end);
            for (var index = start; index < end; index++)
            {
                view.Add(index);
            }

            return;
        }

        var query = search.ToLowerInvariant();
        for (var index = 0; index < glyphs.Length; index++)
        {
            if (glyphs[index].Search.Contains(query, StringComparison.Ordinal))
            {
                view.Add(index);
            }
        }
    }

    private string? DrawGrid(Rect body, in AppSkin ui)
    {
        var scale = ImGuiHelpers.GlobalScale;
        string? picked = null;
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##emojiGrid", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return null;
            }

            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var gap = 4f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (Columns - 1)) / Columns;
            var stride = cell + gap;
            var count = view.Count;
            var rows = (count + Columns - 1) / Columns;
            var total = rows * stride;
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var glyphs = EmojiCatalog.Glyphs;
            var drawList = ImGui.GetWindowDrawList();
            for (var row = 0; row < rows; row++)
            {
                var rowTop = row * stride;
                if (rowTop + cell < scrollY - cell || rowTop > scrollY + viewHeight + cell)
                {
                    continue;
                }

                for (var column = 0; column < Columns; column++)
                {
                    var slot = row * Columns + column;
                    if (slot >= count)
                    {
                        break;
                    }

                    var glyph = glyphs[view[slot]];
                    var min = new Vector2(origin.X + column * stride, origin.Y + rowTop);
                    var max = min + new Vector2(cell, cell);
                    var hovered = UiInteract.Hover(min, max);
                    if (hovered)
                    {
                        Squircle.Fill(drawList, min, max, 8f * scale,
                            ImGui.GetColorU32(Palette.WithAlpha(ui.Theme.TextStrong, 0.10f)));
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    }

                    var inset = cell * 0.14f;
                    EmojiImages.TryDraw(drawList, FileFor(glyph), min + new Vector2(inset, inset),
                        max - new Vector2(inset, inset), 0xFFFFFFFF);
                    if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        picked = CharFor(glyph);
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, total));
        }

        return picked;
    }

    private string FileFor(in EmojiGlyph glyph) =>
        glyph.HasTones && tone > 0 ? glyph.Tones[tone - 1].File : glyph.File;

    private string CharFor(in EmojiGlyph glyph) =>
        glyph.HasTones && tone > 0 ? glyph.Tones[tone - 1].Char : glyph.Char;
}
