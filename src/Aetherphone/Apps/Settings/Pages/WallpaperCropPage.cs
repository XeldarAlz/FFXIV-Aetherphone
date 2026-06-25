using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class WallpaperCropPage : ISettingsPage
{
    private const float SmoothTime = 0.10f;

    public string Title => Loc.T(L.Wallpaper.MoveAndScale);

    public string Summary => string.Empty;

    public string Glyph => "W";

    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);

    private readonly string sourcePath;
    private readonly ISettingsNavigator navigator;
    private readonly Action<string> onCommit;

    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);

    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;

    private bool dragging;
    private Vector2 lastDrag;

    public WallpaperCropPage(string sourcePath, ISettingsNavigator navigator, Action<string> onCommit)
    {
        this.sourcePath = sourcePath;
        this.navigator = navigator;
        this.onCommit = onCommit;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var aspect = CropAspect();
        var texture = Plugin.WallpaperImages.Get(sourcePath);
        var dl = ImGui.GetWindowDrawList();

        var footer = 116f * scale;
        var area = new Rect(new Vector2(body.Min.X + 16f * scale, body.Min.Y + 8f * scale), new Vector2(body.Max.X - 16f * scale, body.Max.Y - footer));
        var preview = FitAspect(area, aspect);
        var rounding = 22f * scale;

        if (texture is null)
        {
            Squircle.Fill(dl, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            var failed = Plugin.WallpaperImages.Failed(sourcePath);
            Typography.DrawCentered(preview.Center, Loc.T(failed ? L.Wallpaper.LoadFailed : L.Common.Loading), theme.TextMuted, 1f);
        }
        else
        {
            var size = texture.Size;
            var zoom = zoomSpring.Step(targetZoom, SmoothTime, deltaSeconds);
            var centerX = centerXSpring.Step(targetCenterX, SmoothTime, deltaSeconds);
            var centerY = centerYSpring.Step(targetCenterY, SmoothTime, deltaSeconds);

            var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect);
            var (uv0, uv1) = crop.ComputeUv(size, aspect);

            dl.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
            Material.EdgeSquircle(dl, preview.Min, preview.Max, rounding, scale);

            HandleGestures(preview, size, aspect, uv1 - uv0);
        }

        DrawControls(body, theme, texture, aspect, scale);
    }

    private void HandleGestures(Rect preview, Vector2 size, float aspect, Vector2 visible)
    {
        var hovering = ImGui.IsMouseHoveringRect(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), WallpaperCrop.MinZoom, WallpaperCrop.MaxZoom);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            lastDrag = ImGui.GetMousePos();
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var position = ImGui.GetMousePos();
                var delta = position - lastDrag;
                lastDrag = position;
                if (preview.Width > 0f && preview.Height > 0f)
                {
                    targetCenterX -= delta.X * visible.X / preview.Width;
                    targetCenterY -= delta.Y * visible.Y / preview.Height;
                }
            }
            else
            {
                dragging = false;
            }
        }

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, aspect);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    private void DrawControls(Rect body, PhoneTheme theme, Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture, float aspect, float scale)
    {
        Typography.DrawCentered(new Vector2(body.Center.X, body.Max.Y - 94f * scale), Loc.T(L.Wallpaper.GestureHint), theme.TextMuted, 0.78f);

        var trackWidth = body.Width * 0.62f;
        var track = new Rect(new Vector2(body.Center.X - trackWidth * 0.5f, body.Max.Y - 70f * scale), new Vector2(body.Center.X + trackWidth * 0.5f, body.Max.Y - 66f * scale));
        var zoomNormalized = (targetZoom - WallpaperCrop.MinZoom) / (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
        var updated = Scrubber.Draw(track, zoomNormalized, theme.Accent, theme.SurfaceMuted, 1f);
        targetZoom = WallpaperCrop.MinZoom + updated * (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);

        var pad = 16f * scale;
        var gap = 12f * scale;
        var buttonWidth = (body.Width - pad * 2f - gap) * 0.5f;
        var buttonHeight = 42f * scale;
        var buttonY = body.Max.Y - pad - buttonHeight;
        var cancelRect = new Rect(new Vector2(body.Min.X + pad, buttonY), new Vector2(body.Min.X + pad + buttonWidth, buttonY + buttonHeight));
        var setRect = new Rect(new Vector2(cancelRect.Max.X + gap, buttonY), new Vector2(body.Max.X - pad, buttonY + buttonHeight));

        if (DrawButton(cancelRect, Loc.T(L.Common.Cancel), false, true, theme, scale))
        {
            navigator.Back();
            return;
        }

        if (DrawButton(setRect, Loc.T(L.Wallpaper.Set), true, texture is not null, theme, scale) && texture is not null)
        {
            var finalCrop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(texture.Size, aspect);
            var id = Plugin.Wallpapers.AddCustom(sourcePath, finalCrop);
            onCommit(id);
            navigator.Back();
        }
    }

    private static bool DrawButton(Rect rect, string label, bool primary, bool enabled, PhoneTheme theme, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var rounding = 12f * scale;
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

        var fill = primary ? theme.Accent : theme.SurfaceMuted;
        if (!enabled)
        {
            fill = fill with { W = fill.W * 0.4f };
        }
        else if (hovered)
        {
            fill = Palette.Mix(fill, theme.TextStrong, 0.12f);
        }

        Squircle.Fill(dl, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(dl, rect.Min, rect.Max, rounding, scale);

        var textColor = primary ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;
        Typography.DrawCentered(rect.Center, label, textColor with { W = enabled ? textColor.W : 0.5f }, 1.0f, FontWeight.SemiBold);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static Rect FitAspect(Rect area, float aspect)
    {
        if (aspect <= 0f || area.Width <= 0f || area.Height <= 0f)
        {
            return area;
        }

        var width = area.Width;
        var height = width / aspect;
        if (height > area.Height)
        {
            height = area.Height;
            width = height * aspect;
        }

        var center = area.Center;
        var half = new Vector2(width * 0.5f, height * 0.5f);
        return new Rect(center - half, center + half);
    }

    private static float CropAspect()
    {
        var aspect = Plugin.Wallpapers.CurrentTargetAspect;
        return aspect > 0.1f ? aspect : 0.5f;
    }
}
