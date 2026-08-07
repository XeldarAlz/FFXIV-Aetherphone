using Aetherphone.Core;
using Aetherphone.Core.Animation;
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

    private const float RevealSmoothTime = 0.16f;
    private const float MaxDim = 0.55f;
    private const float PanelRounding = 26f;
    private const float TileSize = 52f;
    private const float CellWidth = 76f;
    private const float CellLabelGap = 8f;
    private const float RowGap = 12f;
    private const float CancelHeight = 40f;
    private const int MaxColumns = 4;

    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);

    private readonly ShareService service;
    private Spring reveal;
    private ShareKind shownKind;
    private bool wasPending;
    private int openedFrame;

    public ShareSheet(ShareService service)
    {
        this.service = service;
    }

    public bool CapturesPointer => service.Pending is not null || !reveal.IsResting(0f, 0.001f, 0.005f);

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
            openedFrame = ImGui.GetFrameCount();
        }

        wasPending = pending;

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        reveal.Step(pending ? 1f : 0f, RevealSmoothTime, delta);
        if (!pending && reveal.IsResting(0f, 0.001f, 0.005f))
        {
            reveal.SnapTo(0f);
            return;
        }

        var opacity = Math.Clamp(reveal.Value, 0f, 1f);
        var slide = Easing.EaseOutQuint(opacity);
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("##shareSheet", screen.Size, false, OverlayFlags))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(screen.Min, screen.Max,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, MaxDim * opacity)));
            var panel = DrawPanel(screen, theme, opacity, slide, pending);
            if (!pending || opacity <= 0.5f)
            {
                return;
            }

            if (ImGui.GetFrameCount() != openedFrame && UiInteract.ClickedOutside(panel.Min, panel.Max))
            {
                service.Dismiss();
            }
        }
    }

    private Rect DrawPanel(Rect screen, PhoneTheme theme, float opacity, float slide, bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var targets = service.Targets;
        var pad = Metrics.Space.Xl * scale;
        var cellWidth = CellWidth * scale;
        var labelHeight = Typography.Measure(" ", TextStyles.Caption1).Y;
        var cellHeight = TileSize * scale + CellLabelGap * scale + labelHeight;
        var innerWidth = screen.Width - pad * 2f;
        var columns = Math.Clamp((int)(innerWidth / cellWidth), 1, MaxColumns);
        if (targets.Count < columns)
        {
            columns = Math.Max(1, targets.Count);
        }

        var rows = (targets.Count + columns - 1) / columns;
        var titleHeight = Typography.Measure(Loc.T(L.Share.Title), TextStyles.Headline).Y;
        var gridHeight = rows * cellHeight + Math.Max(0, rows - 1) * RowGap * scale;
        var panelHeight = pad + titleHeight + Metrics.Space.Lg * scale + gridHeight + Metrics.Space.Xl * scale +
                          CancelHeight * scale + pad;
        var panelBottom = screen.Max.Y + panelHeight * (1f - slide);
        var panelTop = panelBottom - panelHeight;
        var panelMin = new Vector2(screen.Min.X, panelTop);
        var panelMax = new Vector2(screen.Max.X, panelBottom);
        var rounding = PanelRounding * scale;
        Squircle.Fill(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Surface, opacity)));
        Squircle.Stroke(drawList, panelMin, panelMax, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f * opacity)), Metrics.Stroke.Hairline);

        var centerX = screen.Center.X;
        Typography.DrawCentered(drawList, new Vector2(centerX, panelTop + pad + titleHeight * 0.5f),
            Loc.T(L.Share.Title), Palette.WithAlpha(theme.TextStrong, opacity), TextStyles.Headline);

        var gridTop = panelTop + pad + titleHeight + Metrics.Space.Lg * scale;
        var gridWidth = columns * cellWidth;
        var gridLeft = centerX - gridWidth * 0.5f;
        IPhoneApp? picked = null;
        for (var index = 0; index < targets.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellMin = new Vector2(gridLeft + column * cellWidth, gridTop + row * (cellHeight + RowGap * scale));
            if (DrawTarget(drawList, theme, targets[index], shownKind, cellMin, cellWidth, cellHeight, scale, opacity,
                    interactive))
            {
                picked = targets[index];
            }
        }

        var cancelWidth = MathF.Min(innerWidth, 180f * scale);
        var cancelMin = new Vector2(centerX - cancelWidth * 0.5f, panelMax.Y - pad - CancelHeight * scale);
        var cancelRect = new Rect(cancelMin, cancelMin + new Vector2(cancelWidth, CancelHeight * scale));
        if (ConfirmDialog.DrawPillButton(cancelRect, Loc.T(L.Common.Cancel), true, theme, 1f, opacity,
                ConfirmButtonTone.Neutral, "##shareCancel") && interactive)
        {
            service.Dismiss();
        }

        if (picked is { } target && interactive)
        {
            service.Pick(target);
        }

        return new Rect(panelMin, panelMax);
    }

    private static bool DrawTarget(ImDrawListPtr drawList, PhoneTheme theme, IPhoneApp app, ShareKind kind,
        Vector2 cellMin, float cellWidth, float cellHeight, float scale, float opacity, bool interactive)
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
                ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.07f * opacity)));
        }

        Elevation.IconRest(drawList, tileMin, tileMax, radius, scale);
        IconTile.FillShaded(drawList, tileMin, tileMax, radius, Palette.WithAlpha(surface, opacity));
        Material.EdgeSquircle(drawList, tileMin, tileMax, radius, scale);
        if (!AppIconArt.TryDraw(drawList, app.Id, tileCenter, tileSize, Palette.WithAlpha(GlyphInk, opacity),
                Palette.Darken(surface, 0.25f)))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? tileSize * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(drawList, tileCenter, app.Glyph, Palette.WithAlpha(GlyphInk, opacity), glyphScale,
                FontWeight.Regular);
        }

        var label = app.ShareLabel(kind) is { } custom ? Loc.T(custom) : app.DisplayName;
        var fitted = Typography.FitText(label, cellWidth - Metrics.Space.Xs * scale, TextStyles.Caption1);
        var labelSize = Typography.Measure(fitted, TextStyles.Caption1);
        Typography.DrawCentered(drawList,
            new Vector2(tileCenter.X, tileMax.Y + CellLabelGap * scale + labelSize.Y * 0.5f), fitted,
            Palette.WithAlpha(theme.TextStrong, 0.86f * opacity), TextStyles.Caption1);

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
