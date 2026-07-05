using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal sealed class HomeScreen
{
    private const float LongPressSeconds = 0.40f;
    private const float TapSlop = 6f;
    private const float SwipeThreshold = 12f;
    private const float DragThreshold = 8f;
    private const float PageSmoothTime = 0.22f;
    private const float ReflowSmoothTime = 0.12f;
    private const float EdgeZone = 0.10f;
    private const float EdgeFlipSeconds = 0.45f;
    private const float DotsReserve = 26f;

    private struct Wobble
    {
        public Spring X;
        public Spring Y;
    }

    private readonly HomeLayoutService layout;
    private readonly Dictionary<string, Wobble> reflow = new();
    private Spring pageScroll;
    private int pageIndex;
    private bool pageSwiping;
    private float swipeStartX;
    private int swipeStartPage;
    private bool pressActive;
    private Vector2 pressPos;
    private float pressTime;
    private HomeTile? pressTile;
    private bool editing;
    private float editClock;
    private HomeTile? dragItem;
    private int dragPage;
    private Vector2 dragMouse;
    private float edgeDwell;
    private int dropSlot;
    private HomeTile? folderTarget;
    private HomeTile? openFolder;
    private Spring folderAnim;
    private bool folderClosing;
    private Rect folderOrigin;
    private string folderNameBuffer = string.Empty;

    public HomeScreen(IReadOnlyList<IPhoneApp> apps)
    {
        layout = new HomeLayoutService(apps, Plugin.Cfg);
    }

    public void Draw(Rect content, PhoneTheme theme, INavigator navigation)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        editClock += delta;
        var metrics = Compute(content);
        if (!pageSwiping)
        {
            pageScroll.Step(pageIndex, PageSmoothTime, delta);
        }

        if (openFolder is not null)
        {
            DrawPages(content, metrics, theme, delta);
            DrawFolderOverlay(content, metrics, theme, navigation, delta);
            return;
        }

        HandleInput(content, metrics, navigation, delta);
        DrawPages(content, metrics, theme, delta);
        if (dragItem is not null)
        {
            DrawTile(dragItem, dragMouse, metrics, theme, 1.1f, 0f, false);
        }

        DrawPageDots(content, metrics, theme);
        if (editing)
        {
            DrawDoneButton(content, metrics, theme);
        }
    }

    private Metrics Compute(Rect content)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var columnWidth = content.Width / HomeLayoutService.Columns;
        var iconSize = MathF.Min(columnWidth * 0.58f, 58f * scale);
        var topPad = 10f * scale;
        var available = content.Height - topPad - DotsReserve * scale;
        var rowHeight = MathF.Min(iconSize + 30f * scale, available / HomeLayoutService.Rows);
        return new Metrics(scale, columnWidth, iconSize, rowHeight, topPad);
    }

    private readonly struct Metrics
    {
        public readonly float Scale;
        public readonly float ColumnWidth;
        public readonly float IconSize;
        public readonly float RowHeight;
        public readonly float TopPad;

        public Metrics(float scale, float columnWidth, float iconSize, float rowHeight, float topPad)
        {
            Scale = scale;
            ColumnWidth = columnWidth;
            IconSize = iconSize;
            RowHeight = rowHeight;
            TopPad = topPad;
        }
    }

    private Vector2 SlotCenter(Rect content, in Metrics m, int page, int slot)
    {
        var column = slot % HomeLayoutService.Columns;
        var row = slot / HomeLayoutService.Columns;
        var pageX = content.Min.X + (page - pageScroll.Value) * content.Width;
        var centerX = pageX + column * m.ColumnWidth + m.ColumnWidth * 0.5f;
        var centerY = content.Min.Y + m.TopPad + row * m.RowHeight + m.IconSize * 0.5f;
        return new Vector2(centerX, centerY);
    }

    private int SlotFromCursor(Rect content, in Metrics m, int page, Vector2 cursor, int maxSlot)
    {
        var pageX = content.Min.X + (page - pageScroll.Value) * content.Width;
        var column = Math.Clamp((int)MathF.Floor((cursor.X - pageX) / m.ColumnWidth), 0, HomeLayoutService.Columns - 1);
        var row = Math.Clamp((int)MathF.Floor((cursor.Y - content.Min.Y - m.TopPad) / m.RowHeight), 0,
            HomeLayoutService.Rows - 1);
        return Math.Clamp(row * HomeLayoutService.Columns + column, 0, maxSlot);
    }

    private HomeTile? TileAt(Rect content, in Metrics m, Vector2 cursor)
    {
        var page = layout.Page(pageIndex);
        var pageX = content.Min.X + (pageIndex - pageScroll.Value) * content.Width;
        var column = (int)MathF.Floor((cursor.X - pageX) / m.ColumnWidth);
        var row = (int)MathF.Floor((cursor.Y - content.Min.Y - m.TopPad) / m.RowHeight);
        if (column < 0 || column >= HomeLayoutService.Columns || row < 0 || row >= HomeLayoutService.Rows)
        {
            return null;
        }

        var slot = row * HomeLayoutService.Columns + column;
        return slot >= 0 && slot < page.Count ? page[slot] : null;
    }

    private Rect IconRect(Rect content, in Metrics m, HomeTile tile)
    {
        var (page, slot) = layout.Locate(tile);
        if (page < 0)
        {
            page = pageIndex;
            slot = 0;
        }

        var center = SlotCenter(content, m, page, slot);
        var half = m.IconSize * 0.5f;
        return new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half));
    }

    private void HandleInput(Rect content, in Metrics m, INavigator navigation, float delta)
    {
        var mouse = ImGui.GetMousePos();
        if (editing && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && DoneRect(content, m).Contains(mouse))
        {
            editing = false;
            pressActive = false;
            return;
        }

        if (dragItem is not null)
        {
            HandleTileDrag(content, m, delta);
            return;
        }

        if (pageSwiping)
        {
            HandlePageSwipe(content, mouse);
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && content.Contains(mouse))
        {
            pressActive = true;
            pressPos = mouse;
            pressTime = 0f;
            pressTile = TileAt(content, m, mouse);
        }

        if (pressActive && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressTime += delta;
            var move = mouse - pressPos;
            if (layout.PageCount > 1 && MathF.Abs(move.X) > SwipeThreshold * m.Scale &&
                MathF.Abs(move.X) > MathF.Abs(move.Y) * 1.2f && (!editing || pressTile is null))
            {
                BeginPageSwipe(mouse);
                return;
            }

            if (editing && pressTile is not null && move.Length() > DragThreshold * m.Scale)
            {
                BeginTileDrag(pressTile, mouse);
                return;
            }

            if (!editing && pressTime > LongPressSeconds && move.Length() < TapSlop * m.Scale)
            {
                editing = true;
                if (pressTile is not null)
                {
                    BeginTileDrag(pressTile, mouse);
                }
                else
                {
                    pressActive = false;
                }

                return;
            }
        }

        if (pressActive && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var move = mouse - pressPos;
            var tap = move.Length() < TapSlop * m.Scale && pressTime < LongPressSeconds;
            if (tap)
            {
                if (pressTile is not null)
                {
                    if (pressTile.IsFolder)
                    {
                        OpenFolder(content, m, pressTile);
                    }
                    else if (!editing)
                    {
                        navigation.OpenApp(pressTile.App!);
                    }
                }
                else if (editing)
                {
                    editing = false;
                }
            }

            pressActive = false;
        }
    }

    private void BeginPageSwipe(Vector2 mouse)
    {
        pageSwiping = true;
        swipeStartX = mouse.X;
        swipeStartPage = pageIndex;
        pressActive = false;
    }

    private void HandlePageSwipe(Rect content, Vector2 mouse)
    {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var dx = mouse.X - swipeStartX;
            var value = swipeStartPage - dx / content.Width;
            pageScroll.SnapTo(Math.Clamp(value, -0.12f, layout.PageCount - 1 + 0.12f));
            return;
        }

        pageSwiping = false;
        pageIndex = Math.Clamp((int)MathF.Round(pageScroll.Value), 0, layout.PageCount - 1);
    }

    private void BeginTileDrag(HomeTile tile, Vector2 mouse)
    {
        dragItem = tile;
        dragPage = pageIndex;
        dragMouse = mouse;
        edgeDwell = 0f;
        folderTarget = null;
        dropSlot = 0;
        pressActive = false;
    }

    private void HandleTileDrag(Rect content, in Metrics m, float delta)
    {
        dragMouse = ImGui.GetMousePos();
        var edge = EdgeZone * content.Width;
        if (dragMouse.X < content.Min.X + edge && dragPage > 0)
        {
            edgeDwell += delta;
            if (edgeDwell > EdgeFlipSeconds)
            {
                dragPage--;
                pageIndex = dragPage;
                edgeDwell = 0f;
            }
        }
        else if (dragMouse.X > content.Max.X - edge && dragPage < layout.PageCount - 1)
        {
            edgeDwell += delta;
            if (edgeDwell > EdgeFlipSeconds)
            {
                dragPage++;
                pageIndex = dragPage;
                edgeDwell = 0f;
            }
        }
        else
        {
            edgeDwell = 0f;
        }

        ComputeDrop(content, m);
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (folderTarget is not null)
            {
                layout.MakeFolder(folderTarget, dragItem!);
            }
            else
            {
                layout.MoveTile(dragItem!, dragPage, dropSlot);
            }

            pageIndex = Math.Clamp(pageIndex, 0, layout.PageCount - 1);
            dragItem = null;
            folderTarget = null;
        }
    }

    private void ComputeDrop(Rect content, in Metrics m)
    {
        folderTarget = null;
        var page = layout.Page(dragPage);
        var siblings = Siblings(page);
        var radius = m.IconSize * 0.42f;
        for (var index = 0; index < siblings.Count; index++)
        {
            var center = SlotCenter(content, m, dragPage, index);
            if (Vector2.Distance(center, dragMouse) < radius && !(dragItem!.IsFolder && siblings[index].IsFolder))
            {
                folderTarget = siblings[index];
                dropSlot = -1;
                return;
            }
        }

        dropSlot = SlotFromCursor(content, m, dragPage, dragMouse, siblings.Count);
    }

    private List<HomeTile> Siblings(IReadOnlyList<HomeTile> page)
    {
        var siblings = new List<HomeTile>(page.Count);
        for (var index = 0; index < page.Count; index++)
        {
            if (!ReferenceEquals(page[index], dragItem))
            {
                siblings.Add(page[index]);
            }
        }

        return siblings;
    }

    private void DrawPages(Rect content, in Metrics m, PhoneTheme theme, float delta)
    {
        ImGui.GetWindowDrawList().PushClipRect(content.Min, content.Max, true);
        var scroll = pageScroll.Value;
        var first = Math.Max(0, (int)MathF.Floor(scroll) - 1);
        var last = Math.Min(layout.PageCount - 1, (int)MathF.Ceiling(scroll) + 1);
        for (var page = first; page <= last; page++)
        {
            DrawPage(content, m, theme, page, delta);
        }

        ImGui.GetWindowDrawList().PopClipRect();
    }

    private void DrawPage(Rect content, in Metrics m, PhoneTheme theme, int page, float delta)
    {
        var tiles = layout.Page(page);
        var labelAlpha = openFolder is null ? 1f : 0.35f;
        if (dragItem is null || page != dragPage)
        {
            for (var slot = 0; slot < tiles.Count; slot++)
            {
                var tile = tiles[slot];
                if (ReferenceEquals(tile, dragItem))
                {
                    continue;
                }

                var center = SlotCenter(content, m, page, slot) + Jiggle(tile, m);
                DrawTile(tile, center, m, theme, 1f, labelAlpha, false);
                ReportIconAnchor(tile, center, m);
            }

            return;
        }

        var siblings = Siblings(tiles);
        for (var index = 0; index < siblings.Count; index++)
        {
            var tile = siblings[index];
            var naturalSlot = index;
            var visualSlot = folderTarget is null && dropSlot >= 0 && index >= dropSlot ? index + 1 : index;
            var natural = SlotCenter(content, m, page, naturalSlot);
            var targetDelta = SlotCenter(content, m, page, visualSlot) - natural;
            var center = natural + Step(tile.Key, targetDelta, delta) + Jiggle(tile, m);
            DrawTile(tile, center, m, theme, 1f, labelAlpha, ReferenceEquals(tile, folderTarget));
        }
    }

    private Vector2 Step(string key, Vector2 targetDelta, float delta)
    {
        if (!reflow.TryGetValue(key, out var wobble))
        {
            wobble = new Wobble { X = new Spring(targetDelta.X), Y = new Spring(targetDelta.Y) };
        }

        wobble.X.Step(targetDelta.X, ReflowSmoothTime, delta);
        wobble.Y.Step(targetDelta.Y, ReflowSmoothTime, delta);
        reflow[key] = wobble;
        return new Vector2(wobble.X.Value, wobble.Y.Value);
    }

    private Vector2 Jiggle(HomeTile tile, in Metrics m)
    {
        if (!editing)
        {
            return Vector2.Zero;
        }

        var phase = (tile.Key.GetHashCode() & 0x3ff) / 1023f * MathF.PI * 2f;
        var x = MathF.Sin(editClock * 11f + phase);
        var y = MathF.Cos(editClock * 13f + phase * 1.3f);
        return new Vector2(x, y) * 1.1f * m.Scale;
    }

    private void DrawTile(HomeTile tile, Vector2 center, in Metrics m, PhoneTheme theme, float drawScale,
        float labelAlpha, bool highlight)
    {
        if (highlight)
        {
            var ring = m.IconSize * 0.5f * drawScale + 4f * m.Scale;
            ImGui.GetWindowDrawList().AddCircle(center, ring,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.8f)), 32, 2f * m.Scale);
        }

        if (tile.IsFolder)
        {
            HomeTileView.DrawFolder(center, m.IconSize, tile, theme, drawScale, labelAlpha, Loc.T(L.Home.NewFolder));
        }
        else
        {
            HomeTileView.DrawApp(center, m.IconSize, tile.App!, theme, drawScale, labelAlpha);
        }
    }

    private static void ReportIconAnchor(HomeTile tile, Vector2 center, in Metrics m)
    {
        if (!UiAnchors.Recording || tile.App is null)
        {
            return;
        }

        var half = m.IconSize * 0.5f;
        UiAnchors.Report(string.Concat("home.app.", tile.App.Id),
            new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half)));
    }

    private void DrawPageDots(Rect content, in Metrics m, PhoneTheme theme)
    {
        if (layout.PageCount <= 1)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var spacing = 14f * m.Scale;
        var radius = 3f * m.Scale;
        var totalWidth = (layout.PageCount - 1) * spacing;
        var startX = content.Center.X - totalWidth * 0.5f;
        var y = content.Max.Y - 11f * m.Scale;
        var active = Math.Clamp((int)MathF.Round(pageScroll.Value), 0, layout.PageCount - 1);
        for (var index = 0; index < layout.PageCount; index++)
        {
            var alpha = index == active ? 0.95f : 0.32f;
            dl.AddCircleFilled(new Vector2(startX + index * spacing, y), radius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, alpha)), 16);
        }
    }

    private Rect DoneRect(Rect content, in Metrics m)
    {
        var width = 64f * m.Scale;
        var height = 30f * m.Scale;
        var max = new Vector2(content.Max.X, content.Min.Y + height);
        return new Rect(new Vector2(max.X - width, content.Min.Y), max);
    }

    private void DrawDoneButton(Rect content, in Metrics m, PhoneTheme theme)
    {
        var rect = DoneRect(content, m);
        var dl = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        Squircle.Fill(dl, rect.Min, rect.Max, rect.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, hovered ? 1f : 0.88f)));
        Typography.DrawCentered(rect.Center, Loc.T(L.Home.Done), new Vector4(1f, 1f, 1f, 1f), 0.82f,
            FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void OpenFolder(Rect content, in Metrics m, HomeTile folder)
    {
        openFolder = folder;
        folderClosing = false;
        folderAnim.SnapTo(0f);
        folderOrigin = IconRect(content, m, folder);
        folderNameBuffer = folder.FolderName;
    }

    private void CloseFolder()
    {
        ApplyRename();
        folderClosing = true;
    }

    private void ApplyRename()
    {
        if (openFolder is { IsFolder: true } folder &&
            !string.Equals(folderNameBuffer, folder.FolderName, StringComparison.Ordinal))
        {
            layout.Rename(folder, folderNameBuffer);
        }
    }

    private void DrawFolderOverlay(Rect content, in Metrics m, PhoneTheme theme, INavigator navigation, float delta)
    {
        folderAnim.Step(folderClosing ? 0f : 1f, 0.20f, delta);
        if (folderClosing && folderAnim.Value < 0.02f)
        {
            openFolder = null;
            folderClosing = false;
            return;
        }

        var folder = openFolder!;
        var eased = Easing.EaseOutCubic(Math.Clamp(folderAnim.Value, 0f, 1f));
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(content.Min, content.Max, true);
        Material.Veil(dl, content.Min, content.Max, 0.5f * eased);
        var cols = folder.Apps.Count <= 9 ? 3 : 4;
        var rows = (folder.Apps.Count + cols - 1) / cols;
        var panelWidth = content.Width * 0.84f;
        var pad = 18f * m.Scale;
        var cellWidth = (panelWidth - pad * 2f) / cols;
        var iconSize = MathF.Min(cellWidth * 0.62f, 52f * m.Scale);
        var cellHeight = iconSize + 26f * m.Scale;
        var headerHeight = 48f * m.Scale;
        var panelHeight = headerHeight + rows * cellHeight + pad;
        var targetMin = new Vector2(content.Center.X - panelWidth * 0.5f, content.Center.Y - panelHeight * 0.5f);
        var targetRect = new Rect(targetMin, targetMin + new Vector2(panelWidth, panelHeight));
        var panelRect = new Rect(Vector2.Lerp(folderOrigin.Min, targetRect.Min, eased),
            Vector2.Lerp(folderOrigin.Max, targetRect.Max, eased));
        Material.Frosted(dl, panelRect.Min, panelRect.Max, 28f * m.Scale, m.Scale, eased);
        var interactive = !folderClosing && eased > 0.85f;
        if (interactive)
        {
            DrawFolderContents(panelRect, m, theme, navigation, folder, cols, pad, iconSize, cellWidth, cellHeight,
                headerHeight);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !panelRect.Contains(ImGui.GetMousePos()))
            {
                CloseFolder();
            }
        }

        dl.PopClipRect();
    }

    private void DrawFolderContents(Rect panel, in Metrics m, PhoneTheme theme, INavigator navigation, HomeTile folder,
        int cols, float pad, float iconSize, float cellWidth, float cellHeight, float headerHeight)
    {
        ImGui.SetCursorScreenPos(new Vector2(panel.Min.X + pad, panel.Min.Y + 12f * m.Scale));
        ImGui.SetNextItemWidth(panel.Width - pad * 2f);
        if (ImGui.InputTextWithHint("##folderName", Loc.T(L.Home.NewFolder), ref folderNameBuffer, 64,
                ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ApplyRename();
        }

        var gridTop = panel.Min.Y + headerHeight;
        for (var index = 0; index < folder.Apps.Count; index++)
        {
            var column = index % cols;
            var row = index / cols;
            var center = new Vector2(panel.Min.X + pad + (column + 0.5f) * cellWidth,
                gridTop + (row + 0.5f) * cellHeight);
            var app = folder.Apps[index];
            HomeTileView.DrawApp(center, iconSize, app, theme, 1f, 1f);
            var half = iconSize * 0.5f;
            var iconRect = new Rect(new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half));
            if (editing)
            {
                if (DrawRemoveBadge(new Vector2(center.X - half + 2f * m.Scale, center.Y - half + 2f * m.Scale), m,
                        theme))
                {
                    layout.RemoveFromFolder(folder, app, pageIndex, layout.Page(pageIndex).Count);
                    if (folder.Apps.Count <= 1)
                    {
                        CloseFolder();
                    }

                    return;
                }
            }
            else if (ImGui.IsMouseHoveringRect(iconRect.Min, iconRect.Max))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    CloseFolder();
                    navigation.OpenApp(app);
                    return;
                }
            }
        }
    }

    private bool DrawRemoveBadge(Vector2 center, in Metrics m, PhoneTheme theme)
    {
        var radius = 9f * m.Scale;
        var dl = ImGui.GetWindowDrawList();
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, hovered ? 1f : 0.88f)),
            24);
        var arm = radius * 0.4f;
        var ink = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f));
        dl.AddLine(new Vector2(center.X - arm, center.Y), new Vector2(center.X + arm, center.Y), ink, 1.8f * m.Scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
