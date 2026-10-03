using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoViewerOverlay
{
    private const float BottomInset = 16f;
    private const float PageButtonRadius = 19f;
    private const float PageButtonInset = 10f;
    private const float PageGlyphSize = 20f;
    private const float PageDotsInset = 14f;

    private static readonly Vector4 PageButtonFill = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 PageButtonHover = new(0.12f, 0.12f, 0.12f, 0.85f);
    private static readonly Vector4 PageInk = new(1f, 1f, 1f, 0.95f);

    private readonly PhotoZoomView zoomView = new();
    private Aetherphone.Core.Animation.Spring reveal;
    private Func<IDalamudTextureWrap?>? source;
    private Func<int, IDalamudTextureWrap?>? pages;
    private IPhoneApp? owner;
    private int pageCount;
    private int pageIndex;
    private bool open;

    public bool Active => open || reveal.Value > 0.01f;

    public void Open(IPhoneApp app, Func<IDalamudTextureWrap?> textureSource)
    {
        Open(app, 1, 0, _ => textureSource());
    }

    public void Open(IPhoneApp app, int count, int startIndex, Func<int, IDalamudTextureWrap?> pageSource)
    {
        pages = pageSource;
        pageCount = Math.Max(1, count);
        pageIndex = Math.Clamp(startIndex, 0, pageCount - 1);
        source = CurrentPage;
        owner = app;
        zoomView.Reset();
        open = true;
    }

    public void Close() => open = false;

    private IDalamudTextureWrap? CurrentPage() => pages?.Invoke(pageIndex);

    public void Draw(Rect area, PhoneTheme theme)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, Aetherphone.Core.Animation.TransitionTiming.MaxFrameSeconds);
        reveal.Step(open ? 1f : 0f, Aetherphone.Core.Animation.Motion.Appear, delta);
        var eased = Math.Clamp(reveal.Value, 0f, 1f);
        if (eased <= 0.01f)
        {
            if (!open)
            {
                source = null;
                pages = null;
                owner = null;
            }

            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.96f * eased)));
        var grow = 0.94f + 0.06f * eased;
        var contentTop = area.Min.Y + theme.TopZoneHeight * scale;
        var contentLeft = area.Min.X + theme.SidePadding * scale;
        var headerBottom = contentTop + AppHeader.Height * scale;
        var controlsBottom = area.Max.Y - BottomInset * scale;
        var stageTarget = new Rect(new Vector2(area.Min.X, headerBottom),
            new Vector2(area.Max.X, controlsBottom - PhotoZoomView.ControlBandUnits * scale));
        var stageHalf = stageTarget.Size * 0.5f * grow;
        var stage = new Rect(stageTarget.Center - stageHalf, stageTarget.Center + stageHalf);
        var controls = new Rect(new Vector2(stageTarget.Min.X, stageTarget.Max.Y),
            new Vector2(stageTarget.Max.X, controlsBottom));
        var texture = source?.Invoke();
        if (texture is not null)
        {
            if (zoomView.Draw(stage, texture, theme, Metrics.Radius.Sm * scale, open && eased > 0.9f, controls) &&
                source is not null && owner is not null)
            {
                Plugin.PhotoWindow.Open(source, owner);
            }
        }
        else
        {
            LoadingPulse.Draw(new Vector2(stage.Center.X, stage.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Common.Loading));
        }

        if (pageCount > 1 && open)
        {
            DrawPaging(drawList, stage, scale);
        }

        var rowCenterY = contentTop + AppHeader.Height * scale * 0.5f;
        var hitMin = new Vector2(contentLeft, contentTop);
        var hitMax = new Vector2(contentLeft + 44f * scale, headerBottom);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var backCenter = new Vector2(contentLeft + 13f * scale, rowCenterY);
        if (BackButton.Draw("photoviewer.back", backCenter, 15f * scale, new Vector4(1f, 1f, 1f, 0.95f), hovered, scale,
                shadow: true))
        {
            Close();
        }
    }

    private void DrawPaging(ImDrawListPtr drawList, Rect stage, float scale)
    {
        var step = 0;
        if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
        {
            step = -1;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
        {
            step = 1;
        }

        if (!zoomView.IsZoomed)
        {
            var radius = PageButtonRadius * scale;
            var centerY = stage.Center.Y;
            if (pageIndex > 0 && DrawPageButton(drawList,
                    new Vector2(stage.Min.X + PageButtonInset * scale + radius, centerY), radius, PhoneIcons.ChevronLeft,
                    scale))
            {
                step = -1;
            }

            if (pageIndex < pageCount - 1 && DrawPageButton(drawList,
                    new Vector2(stage.Max.X - PageButtonInset * scale - radius, centerY), radius, PhoneIcons.ChevronRight,
                    scale))
            {
                step = 1;
            }
        }

        PhotoCarousel.DrawDots(drawList, new Vector2(stage.Center.X, stage.Max.Y - PageDotsInset * scale), pageCount,
            pageIndex, stage.Width * 0.6f, PageInk);
        if (step == 0)
        {
            return;
        }

        var next = Math.Clamp(pageIndex + step, 0, pageCount - 1);
        if (next == pageIndex)
        {
            return;
        }

        pageIndex = next;
        zoomView.Reset();
    }

    private static bool DrawPageButton(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, float scale)
    {
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? PageButtonHover : PageButtonFill), 32);
        PhoneIcon.Draw(drawList, center, glyph, PageInk, PageGlyphSize * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }
}
