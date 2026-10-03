using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Home;

internal readonly struct GalleryPage
{
    public readonly IHomeWidget? Widget;
    public readonly WidgetSize Size;

    public GalleryPage(IHomeWidget? widget, WidgetSize size)
    {
        Widget = widget;
        Size = size;
    }
}

internal sealed partial class WidgetGallery
{
    private const float SheetTopUnits = 48f;
    private const float SidePaddingUnits = 16f;
    private const float RootParallax = 0.3f;
    private const float RootDim = 0.45f;
    private const float PushVisible = 0.001f;
    private const float PushSettled = 0.999f;
    private const float EdgeShadowUnits = 14f;
    private const float EdgeShadowAlpha = 0.14f;
    private const float SecondaryAlpha = 0.62f;

    private readonly HomeLayoutService layout;
    private readonly WidgetHost widgetHost;
    private readonly WidgetGalleryIndex index;
    private readonly Sheet sheet = new();
    private readonly HashSet<HomeTile> knownTiles = new(ReferenceEqualityComparer.Instance);
    private Spring push;
    private bool detailOpen;
    private int targetPage;
    private bool refreshPending;
    private HomeTile? placedTile;
    private Vector2 placedCenter;
    private float placedScale = 1f;

    public WidgetGallery(HomeLayoutService layout, WidgetRegistry widgets, WidgetHost widgetHost)
    {
        this.layout = layout;
        this.widgetHost = widgetHost;
        index = new WidgetGalleryIndex(widgets, layout.IsInstalled);
        buttonPress.SnapTo(1f);
    }

    public bool Active => sheet.CapturesPointer;

    public void Open(int pageIndex)
    {
        if (sheet.IsOpen)
        {
            return;
        }

        targetPage = pageIndex;
        query = string.Empty;
        scrollY = 0f;
        railOffset = 0f;
        detailOpen = false;
        push.SnapTo(0f);
        railDrag.Reset();
        pagerDrag.Reset();
        placedPage = -1;
        refreshPending = true;
        sheet.Open();
    }

    public void Close() => sheet.Close();

    public void CloseImmediate() => sheet.CloseImmediately();

    public bool TryTakePlacement(out HomeTile tile, out Vector2 center, out float startScale)
    {
        center = placedCenter;
        startScale = placedScale;
        if (placedTile is not { } placed)
        {
            tile = null!;
            return false;
        }

        tile = placed;
        placedTile = null;
        return true;
    }

    public void Draw(Rect screen, PhoneTheme theme, float delta, in HomeMetrics metrics)
    {
        if (!sheet.CapturesPointer)
        {
            return;
        }

        index.Refresh(query, refreshPending);
        refreshPending = false;
        var scale = metrics.Scale;
        var drawList = ImGui.GetWindowDrawList();
        var detents = SheetDetents.Fitted(MathF.Max(1f, screen.Height - SheetTopUnits * scale));
        var frame = sheet.Begin(drawList, screen, theme, detents, SheetMetrics.HomeVeil);
        if (!frame.Visible)
        {
            return;
        }

        var vertexStart = drawList.VtxBuffer.Size;
        push.Step(detailOpen ? 1f : 0f, Motion.Sheet, delta);
        var progress = Math.Clamp(push.Value, 0f, 1f);
        var width = frame.Panel.Width;
        var detailLeft = frame.Panel.Min.X + (1f - progress) * width;
        if (progress < PushSettled)
        {
            var rootInteractive = frame.Interactive && !detailOpen && progress < PushVisible;
            DrawRoot(in frame, theme, in metrics, delta, -RootParallax * width * progress, detailLeft, progress,
                rootInteractive);
        }

        if (progress > PushVisible)
        {
            DrawEdgeShadow(drawList, frame.Panel, detailLeft, scale);
            DrawDetail(in frame, theme, in metrics, delta, detailLeft - frame.Panel.Min.X,
                frame.Interactive && detailOpen && progress > PushSettled);
        }

        LayerCompositor.Fade(drawList, vertexStart, frame.Opacity);
        HandleKeyboard(frame.Interactive);
        sheet.End(in frame);
    }

    private static Vector4 Secondary(Vector4 ink) => Palette.WithAlpha(ink, ink.W * SecondaryAlpha);

    private static void DrawEdgeShadow(ImDrawListPtr drawList, Rect panel, float edgeX, float scale)
    {
        if (edgeX <= panel.Min.X + 0.5f)
        {
            return;
        }

        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, EdgeShadowAlpha));
        var clear = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f));
        drawList.AddRectFilledMultiColor(new Vector2(edgeX - EdgeShadowUnits * scale, panel.Min.Y),
            new Vector2(edgeX, panel.Max.Y), clear, shadow, shadow, clear);
    }

    private void HandleKeyboard(bool interactive)
    {
        if (!interactive || !sheet.IsOpen)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            if (detailOpen)
            {
                Pop();
                return;
            }

            Close();
            return;
        }

        if (!detailOpen || pages.Count == 0)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
        {
            pagerPage = Math.Max(0, pagerPage - 1);
        }

        if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
        {
            pagerPage = Math.Min(pages.Count - 1, pagerPage + 1);
        }
    }

    private void Place(in GalleryPage page)
    {
        SnapshotTiles();
        if (page.Widget is { } widget)
        {
            layout.AddWidget(widget, page.Size, targetPage);
        }
        else
        {
            AddSmartStack(page.Size, targetPage);
        }

        placedTile = FindNewTile();
        knownTiles.Clear();
        placedCenter = previewRect.Center;
        placedScale = previewFactor;
        placedPage = pagerPage;
        sheet.Close();
    }

    private void AddSmartStack(WidgetSize size, int page)
    {
        var suggestions = index.Suggestions(size);
        if (suggestions.Count == 0)
        {
            return;
        }

        layout.AddStack(suggestions, size, page);
    }

    private void SnapshotTiles()
    {
        knownTiles.Clear();
        for (var pageIndex = 0; pageIndex < layout.PageCount; pageIndex++)
        {
            var tiles = layout.Page(pageIndex);
            for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                knownTiles.Add(tiles[tileIndex]);
            }
        }
    }

    private HomeTile? FindNewTile()
    {
        for (var pageIndex = 0; pageIndex < layout.PageCount; pageIndex++)
        {
            var tiles = layout.Page(pageIndex);
            for (var tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                var tile = tiles[tileIndex];
                if (tile.IsWidget && !knownTiles.Contains(tile))
                {
                    return tile;
                }
            }
        }

        return null;
    }
}
