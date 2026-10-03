using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class ShareSheet
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                                  ImGuiWindowFlags.NoBackground;

    private const float TileSize = 52f;
    private const float CellWidth = 76f;
    private const float CellLabelGap = 8f;
    private const float RowGap = 12f;
    private const float CancelWidth = 180f;
    private const float LabelAlpha = 0.86f;
    private const float CellHoverAlpha = 0.07f;
    private const int MaxColumns = 4;

    private readonly ShareService service;
    private readonly Sheet sheet = new();
    private ShareKind shownKind;
    private bool wasPending;

    public ShareSheet(ShareService service)
    {
        this.service = service;
    }

    public bool CapturesPointer => service.Pending is not null || sheet.CapturesPointer;

    public void Dismiss() => service.Dismiss();

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var offer = service.Pending;
        var pending = offer is not null;
        if (offer is { } current)
        {
            shownKind = current.Kind;
        }

        if (pending && !wasPending)
        {
            sheet.Open();
        }

        if (!pending && sheet.IsOpen)
        {
            sheet.Close();
        }

        wasPending = pending;
        if (!pending && !sheet.CapturesPointer)
        {
            return;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##shareSheet", screen.Size, false, OverlayFlags))
        {
            var veil = SheetMetrics.VeilFor(WallpaperBackdrop.FlatAvailable);
            var frame = sheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Standard(screen.Height),
                veil);
            if (frame.Visible)
            {
                DrawContent(in frame, theme);
                sheet.End(in frame);
            }
        }

        if (pending && !sheet.IsOpen)
        {
            service.Dismiss();
        }
    }

    private void DrawContent(in SheetFrame frame, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var drawList = frame.DrawList;
        var opacity = frame.Opacity;
        var ink = frame.Ink;
        var content = frame.Content;
        var targets = service.Targets;
        var title = Loc.T(L.Share.Title);
        var titleHeight = Typography.Measure(title, TextStyles.Headline).Y;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, content.Min.Y + titleHeight * 0.5f), title,
            Palette.WithAlpha(ink, ink.W * opacity), TextStyles.Headline);

        var cellWidth = CellWidth * scale;
        var labelHeight = Typography.Measure(" ", TextStyles.Caption1).Y;
        var cellHeight = TileSize * scale + CellLabelGap * scale + labelHeight;
        var innerWidth = content.Width - Metrics.Space.Xl * 2f * scale;
        var columns = Math.Clamp((int)(innerWidth / cellWidth), 1, MaxColumns);
        if (targets.Count < columns)
        {
            columns = Math.Max(1, targets.Count);
        }

        var gridTop = content.Min.Y + titleHeight + Metrics.Space.Lg * scale;
        var gridLeft = content.Center.X - columns * cellWidth * 0.5f;
        IPhoneApp? picked = null;
        for (var index = 0; index < targets.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellMin = new Vector2(gridLeft + column * cellWidth, gridTop + row * (cellHeight + RowGap * scale));
            if (DrawTarget(drawList, targets[index], shownKind, cellMin, cellWidth, cellHeight, scale, opacity, ink,
                    frame.Interactive))
            {
                picked = targets[index];
            }
        }

        var cancelWidth = MathF.Min(innerWidth, CancelWidth * scale);
        var cancelHeight = Metrics.Size.Pill * scale;
        var cancelBottom = frame.Panel.Max.Y - Metrics.Size.HomeIndicatorInset * scale;
        var cancelRect = new Rect(new Vector2(content.Center.X - cancelWidth * 0.5f, cancelBottom - cancelHeight),
            new Vector2(content.Center.X + cancelWidth * 0.5f, cancelBottom));
        if (AppSkin.PillButton(cancelRect, Loc.T(L.Common.Cancel), false, theme) && frame.Interactive)
        {
            service.Dismiss();
        }

        if (picked is { } target && frame.Interactive)
        {
            service.Pick(target);
        }
    }

    private static bool DrawTarget(ImDrawListPtr drawList, IPhoneApp app, ShareKind kind, Vector2 cellMin,
        float cellWidth, float cellHeight, float scale, float opacity, Vector4 ink, bool interactive)
    {
        var cellMax = cellMin + new Vector2(cellWidth, cellHeight);
        var hovered = interactive && UiInteract.Hover(cellMin, cellMax);
        var tileSize = TileSize * scale;
        var tileCenter = new Vector2(cellMin.X + cellWidth * 0.5f, cellMin.Y + tileSize * 0.5f);
        var tileMin = new Vector2(tileCenter.X - tileSize * 0.5f, tileCenter.Y - tileSize * 0.5f);
        var tileMax = new Vector2(tileCenter.X + tileSize * 0.5f, tileCenter.Y + tileSize * 0.5f);
        var radius = tileSize * Metrics.Radius.TileFactor;
        var surface = IconTile.Surface(app.Accent);
        if (hovered)
        {
            Squircle.Fill(drawList, cellMin, cellMax, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ink, CellHoverAlpha * opacity)));
        }

        if (!AppIconTile.TryDraw(drawList, app.Id, app.Accent, tileMin, tileMax, radius, opacity, true, scale))
        {
            Elevation.IconRest(drawList, tileMin, tileMax, radius, scale);
            IconTile.FillShaded(drawList, tileMin, tileMax, radius, Palette.WithAlpha(surface, opacity));
            Material.EdgeSquircle(drawList, tileMin, tileMax, radius, scale);
            var glyphInk = AppAccents.InkFor(app.Id);
            if (!AppIconArt.TryDraw(drawList, app.Id, tileCenter, tileSize, Palette.WithAlpha(glyphInk, opacity),
                    Palette.Mix(surface, glyphInk, 0.28f)))
            {
                var glyphHeight = Typography.Measure(app.Glyph).Y;
                var glyphScale = glyphHeight > 0f ? tileSize * 0.5f / glyphHeight : 1f;
                Typography.DrawCentered(drawList, tileCenter, app.Glyph, Palette.WithAlpha(glyphInk, opacity),
                    glyphScale, FontWeight.Regular);
            }
        }

        var label = app.ShareLabel(kind) is { } custom ? Loc.T(custom) : app.DisplayName;
        var fitted = Typography.FitText(label, cellWidth - Metrics.Space.Xs * scale, TextStyles.Caption1);
        var labelSize = Typography.Measure(fitted, TextStyles.Caption1);
        Typography.DrawCentered(drawList,
            new Vector2(tileCenter.X, tileMax.Y + CellLabelGap * scale + labelSize.Y * 0.5f), fitted,
            Palette.WithAlpha(ink, LabelAlpha * opacity), TextStyles.Caption1);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(cellMin, cellMax, hovered);
    }
}
