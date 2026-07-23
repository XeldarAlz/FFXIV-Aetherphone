using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteMuted = new(1f, 1f, 1f, 0.74f);

    private void DrawViewer(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (viewerPaths.Length == 0)
        {
            router.Pop(false);
            return;
        }

        viewerIndex = Math.Clamp(viewerIndex, 0, viewerPaths.Length - 1);
        var path = viewerPaths[viewerIndex];
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0.035f, 0.03f, 0.025f, 1f)));

        var texture = GetFull(path) ?? thumbnails.Get(path);
        if (texture is not null)
        {
            zoomView.Draw(area, texture, frameTheme, Metrics.Radius.Sm * scale);
        }
        else
        {
            LoadingPulse.Draw(new Vector2(area.Center.X, area.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
        }

        var rowCenterY = area.Min.Y + 22f * scale;
        PhotosChrome.TopScrim(drawList, area.Min, area.Max, 66f * scale);

        var taken = ResolveTaken(path);
        var caption = Typography.FitText(DayLabel(taken), area.Width - 120f * scale, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, area.Min.Y + 15f * scale), caption, White,
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, area.Min.Y + 35f * scale),
            TimeText.Clock(taken), WhiteMuted, TextStyles.Footnote);

        var backCenter = new Vector2(area.Min.X + 20f * scale, rowCenterY);
        var backHit = new Vector2(20f * scale, 20f * scale);
        var backHovered = UiInteract.Hover(backCenter - backHit, backCenter + backHit);
        if (BackButton.Draw("photos.viewer.back", backCenter, 15f * scale, White, backHovered, scale, shadow: true))
        {
            router.Pop();
            return;
        }

        if (PhotosChrome.Trash(new Vector2(area.Max.X - 20f * scale, rowCenterY), frameTheme, scale))
        {
            AskDelete(path);
            return;
        }

        if (viewerPaths.Length <= 1)
        {
            return;
        }

        PhotosChrome.BottomScrim(drawList, area.Min, area.Max, 52f * scale);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X, area.Max.Y - 19f * scale),
            $"{viewerIndex + 1} / {viewerPaths.Length}", WhiteMuted, TextStyles.SubheadlineEmphasized);
        if (zoomView.IsZoomed)
        {
            return;
        }

        if (PhotosChrome.Arrow(new Vector2(area.Min.X + 18f * scale, area.Center.Y), White, true, scale))
        {
            viewerIndex = (viewerIndex - 1 + viewerPaths.Length) % viewerPaths.Length;
            zoomView.Reset();
        }

        if (PhotosChrome.Arrow(new Vector2(area.Max.X - 18f * scale, area.Center.Y), White, false, scale))
        {
            viewerIndex = (viewerIndex + 1) % viewerPaths.Length;
            zoomView.Reset();
        }
    }
}
