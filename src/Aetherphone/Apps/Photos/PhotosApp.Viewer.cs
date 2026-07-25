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

    private void DrawViewer(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (viewerPaths.Length == 0)
        {
            router.Pop(false);
            return;
        }

        viewerIndex = Math.Clamp(viewerIndex, 0, viewerPaths.Length - 1);
        var path = viewerPaths[viewerIndex];
        var safe = ContentWithin(screen);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.03f, 1f)));

        var texture = GetFull(path) ?? thumbnails.Get(path);
        if (texture is not null)
        {
            zoomView.Draw(screen, texture, frameTheme, 0f, controls: safe);
        }
        else
        {
            LoadingPulse.Draw(new Vector2(screen.Center.X, screen.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
        }

        var rowCenterY = safe.Min.Y + 22f * scale;
        PhotosChrome.TopScrim(drawList, screen.Min, screen.Max, (frameTheme.TopZoneHeight + 52f) * scale);

        var taken = ResolveTaken(path);
        var caption = Typography.FitText(DayLabel(taken), safe.Width - 96f * scale, TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, safe.Min.Y + 15f * scale), caption, White,
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, safe.Min.Y + 35f * scale),
            TimeText.Clock(taken), WhiteMuted, TextStyles.Footnote);

        var backCenter = new Vector2(safe.Min.X + 20f * scale, rowCenterY);
        var backHit = new Vector2(20f * scale, 20f * scale);
        var backHovered = UiInteract.Hover(backCenter - backHit, backCenter + backHit);
        if (BackButton.Draw("photos.viewer.back", backCenter, 15f * scale, White, backHovered, scale, shadow: true))
        {
            router.Pop();
            return;
        }

        if (PhotosChrome.Trash(new Vector2(safe.Max.X - 20f * scale, rowCenterY), frameTheme, scale))
        {
            AskDelete(path);
            return;
        }

        if (viewerPaths.Length <= 1)
        {
            return;
        }

        PhotosChrome.BottomScrim(drawList, screen.Min, screen.Max, (frameTheme.BottomZoneHeight + 40f) * scale);
        Typography.DrawCentered(drawList, new Vector2(screen.Center.X, safe.Max.Y - 15f * scale),
            $"{viewerIndex + 1} / {viewerPaths.Length}", WhiteMuted, TextStyles.SubheadlineEmphasized);
        if (zoomView.IsZoomed)
        {
            return;
        }

        if (PhotosChrome.Arrow(new Vector2(safe.Min.X + 18f * scale, screen.Center.Y), White, true, scale))
        {
            viewerIndex = (viewerIndex - 1 + viewerPaths.Length) % viewerPaths.Length;
            zoomView.Reset();
        }

        if (PhotosChrome.Arrow(new Vector2(safe.Max.X - 18f * scale, screen.Center.Y), White, false, scale))
        {
            viewerIndex = (viewerIndex + 1) % viewerPaths.Length;
            zoomView.Reset();
        }
    }
}
