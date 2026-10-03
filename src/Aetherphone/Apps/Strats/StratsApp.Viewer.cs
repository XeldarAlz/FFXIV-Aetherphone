using Aetherphone.Apps.Photos;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Strats;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const float ViewerScrimHeight = 64f;
    private const float ViewerBackRadius = 15f;
    private const float ViewerBackHit = 20f;
    private const float PopOutMinimumPixels = 1920f;
    private const float ViewerSpinnerRadius = 13f;

    private static readonly Vector4 ViewerBackdrop = new(0.03f, 0.02f, 0.03f, 1f);
    private static readonly Vector4 ViewerInk = new(1f, 1f, 1f, 1f);

    private void DrawViewer(Rect area, StratsView view)
    {
        var scale = UiScale.Current;
        if (!TryResolveViewerImage(view, out var image, out var mask, out var title))
        {
            CloseViewer();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(ViewerBackdrop));
        var landscape = AppLandscape.Held(Id) && area.IsLandscape();
        var stage = landscape ? area : PortraitStage(area, scale);
        var controls = new Rect(stage.Min, new Vector2(stage.Max.X, stage.Max.Y - Metrics.Space.Md * scale));
        var url = StratsContent.Url(image.Key);
        var texture = images.Sized(url, stage.Width * 2f);
        if (texture is not null)
        {
            if (zoom.Draw(stage, texture, theme, landscape ? 0f : Metrics.Radius.Md * scale, controls: controls))
            {
                PopOutViewer(url, image, mask);
            }

            DrawViewerSpotlight(drawList, stage, texture, mask, scale);
        }
        else
        {
            LoadingPulse.Draw(stage.Center, ViewerSpinnerRadius * scale, ui.Accent, AppPalettes.Strats.MutedInk,
                Loc.T(L.Strats.GuideLoading));
        }

        DrawViewerChrome(drawList, area, title, scale);
    }

    private bool TryResolveViewerImage(StratsView view, out ImageRef image, out SpotlightMask? mask, out string title)
    {
        image = null!;
        mask = null;
        title = string.Empty;
        var current = resolved;
        if (current is null || view.PhaseIndex < 0 || view.PhaseIndex >= current.Phases.Length)
        {
            return false;
        }

        var phase = current.Phases[view.PhaseIndex];
        ImageRef? candidate;
        if (view.MechIndex < 0)
        {
            candidate = phase.Image;
            mask = phase.Spotlight;
            title = phase.Name;
        }
        else if (view.MechIndex < phase.Mechs.Length)
        {
            var mech = phase.Mechs[view.MechIndex];
            candidate = view.PlayerImage ? mech.PlayerImage : mech.Image;
            mask = view.PlayerImage ? mech.PlayerSpotlight : null;
            title = mech.Name;
        }
        else
        {
            return false;
        }

        if (candidate is null)
        {
            return false;
        }

        image = candidate;
        return true;
    }

    private static Rect PortraitStage(Rect area, float scale)
    {
        var top = area.Min.Y + ViewerScrimHeight * scale;
        var pad = Metrics.Space.Sm * scale;
        return new Rect(new Vector2(area.Min.X + pad, top + pad), new Vector2(area.Max.X - pad, area.Max.Y - pad));
    }

    private void DrawViewerSpotlight(ImDrawListPtr drawList, Rect stage, IDalamudTextureWrap texture,
        SpotlightMask? mask, float scale)
    {
        if (mask is null)
        {
            return;
        }

        var fit = PhotoZoomView.FitScale(stage, texture.Size);
        var drawnSize = texture.Size * fit * zoom.Zoom;
        var center = stage.Center + zoom.Pan;
        var frame = new Rect(center - drawnSize * 0.5f, center + drawnSize * 0.5f);
        drawList.PushClipRect(stage.Min, stage.Max, true);
        SpotlightImage.DrawOverlay(drawList, frame, texture, mask, scale);
        drawList.PopClipRect();
    }

    private void DrawViewerChrome(ImDrawListPtr drawList, Rect area, string title, float scale)
    {
        PhotosChrome.TopScrim(drawList, area.Min, area.Max, ViewerScrimHeight * scale);
        var rowCenterY = area.Min.Y + Metrics.Space.Xl * scale;
        var backCenter = new Vector2(area.Min.X + Metrics.Space.Xl * scale, rowCenterY);
        var hit = new Vector2(ViewerBackHit * scale, ViewerBackHit * scale);
        var hovered = UiInteract.Hover(backCenter - hit, backCenter + hit);
        if (BackButton.Draw("strats.viewer.back", backCenter, ViewerBackRadius * scale, ViewerInk, hovered, scale,
                shadow: true))
        {
            CloseViewer();
            return;
        }

        var titleMaxWidth = area.Width - (Metrics.Space.Xl * 2f + ViewerBackHit * 2f) * 2f * scale;
        var fitted = Typography.FitText(title, titleMaxWidth, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, rowCenterY), fitted, ViewerInk,
            TextStyles.Headline.Scale, TextStyles.Headline.Weight);
    }

    private void PopOutViewer(string url, ImageRef image, SpotlightMask? mask)
    {
        var requestPixels = MathF.Max(image.Width, PopOutMinimumPixels);
        Action<ImDrawListPtr, Rect, IDalamudTextureWrap>? overlay = mask is null
            ? null
            : (drawList, frame, texture) => SpotlightImage.DrawOverlay(drawList, frame, texture, mask, UiScale.Current);
        Plugin.PhotoWindow.Open(() => images.Sized(url, requestPixels), this, overlay);
    }
}
