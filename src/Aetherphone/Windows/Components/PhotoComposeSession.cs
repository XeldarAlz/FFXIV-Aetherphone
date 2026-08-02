using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal enum PhotoComposeStage
{
    Pick,
    Crop,
    Caption,
}

internal readonly record struct PhotoComposeStyle(
    Vector4 Accent,
    Vector4 MutedInk,
    Vector4 PlaceholderFill,
    Vector4 ScrubberActive,
    Vector4 ScrubberTrack,
    bool EdgeFrame);

internal sealed class PhotoComposeSession
{
    public const int GridColumns = 3;
    private const float CropSmoothTime = 0.10f;

    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly List<string> selected = new();
    private readonly List<WallpaperCrop> crops = new();
    // One aspect choice per selected photo (Instagram lets each photo in a multi-photo post keep
    // its own framing) - parallel to crops/selected, rebuilt in BeginCropSequence.
    private readonly List<PostAspect> aspects = new();
    // The target aspect ratio the reveal-whole-image zoom (see DrawCropCanvas) was last computed
    // for, per photo. Re-applied whenever the photo's chosen aspect changes (including the first
    // time, tracked as null), but never re-applied for the same aspect twice, so it doesn't fight
    // the user's own manual zoom/pan adjustments on every frame.
    private readonly List<float?> revealedForRatio = new();
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private float currentMinZoom = WallpaperCrop.MinZoom;
    private bool cropDragging;
    private Vector2 cropLastDrag;

    public PhotoComposeSession(PhotoLibrary library, WallpaperImageCache wallpaperImages)
    {
        this.library = library;
        this.wallpaperImages = wallpaperImages;
    }

    private static WallpaperCrop DefaultCrop => new(1f, 0.5f, 0.5f);

    public PhotoComposeStage Stage { get; set; }
    public bool SingleSelect { get; private set; }
    public int CropIndex { get; private set; }
    public int PreviewIndex { get; set; }
    public string Notice { get; private set; } = string.Empty;
    public int SelectedCount => selected.Count;
    public int CropCount => crops.Count;
    public int PickerCount => pickerPaths.Length;
    public bool HasSelection => selected.Count > 0;
    public string FirstSelected => selected.Count > 0 ? selected[0] : string.Empty;
    public string CurrentPath => CropIndex >= 0 && CropIndex < selected.Count ? selected[CropIndex] : string.Empty;
    public WallpaperCrop CurrentTargetCrop => new(targetZoom, targetCenterX, targetCenterY);
    public int ClampedPreviewIndex => Math.Clamp(PreviewIndex, 0, Math.Max(0, selected.Count - 1));

    // The photo currently being individually framed (crop stage) - each photo keeps its own
    // choice, so this changes as CropAdvance/CropBack/LoadCropStage move between photos.
    public PostAspect CurrentAspect => AspectAt(CropIndex);

    // The container aspect for the whole post: the first photo's choice, matching Instagram's own
    // multi-photo posts, where the first photo sets the carousel frame and any other photo whose
    // own aspect differs gets contain-fit inside that same frame rather than restretched to match.
    public PostAspect ContainerAspect => AspectAt(0);

    public string[] SelectedArray() => selected.ToArray();

    public WallpaperCrop[] CropsArray() => crops.ToArray();

    public PostAspect[] AspectsArray() => aspects.ToArray();

    public PostAspect AspectAt(int index) => index >= 0 && index < aspects.Count ? aspects[index] : PostAspect.Square;

    public void SetAspect(int index, PostAspect aspect)
    {
        if (index >= 0 && index < aspects.Count)
        {
            aspects[index] = aspect;
        }
    }

    public void Open(bool singleSelect)
    {
        SingleSelect = singleSelect;
        Stage = PhotoComposeStage.Pick;
        selected.Clear();
        crops.Clear();
        aspects.Clear();
        revealedForRatio.Clear();
        CropIndex = 0;
        PreviewIndex = 0;
        Notice = string.Empty;
        pendingPickedPath = null;
        pickerPaths = library.List();
    }

    public void LaunchImportDialog(string title)
    {
        FilePicker.PickImage(title, path => Interlocked.Exchange(ref pendingPickedPath, path));
    }

    public void ConsumePendingImport()
    {
        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (string.IsNullOrEmpty(picked))
        {
            return;
        }

        pickerPaths = PickerPaths.WithImported(pickerPaths, picked);
        if (!SingleSelect && selected.Contains(picked))
        {
            return;
        }

        TakePicked(picked);
    }

    public void TakePicked(string path)
    {
        if (SingleSelect)
        {
            selected.Clear();
            selected.Add(path);
            BeginCropSequence();
            return;
        }

        var existing = selected.IndexOf(path);
        if (existing >= 0)
        {
            selected.RemoveAt(existing);
            return;
        }

        if (selected.Count >= PostMedia.MaxPhotos)
        {
            Notice = Loc.T(L.Common.PhotoLimit, PostMedia.MaxPhotos);
            return;
        }

        Notice = string.Empty;
        selected.Add(path);
    }

    public void BeginCropSequence()
    {
        crops.Clear();
        aspects.Clear();
        revealedForRatio.Clear();
        for (var index = 0; index < selected.Count; index++)
        {
            crops.Add(DefaultCrop);
            aspects.Add(PostAspect.Square);
            revealedForRatio.Add(null);
        }

        PreviewIndex = 0;
        Stage = PhotoComposeStage.Crop;
        LoadCrop(0);
    }

    public void SaveCurrentCrop()
    {
        if (CropIndex >= 0 && CropIndex < crops.Count)
        {
            crops[CropIndex] = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        }
    }

    public WallpaperCrop CropAt(int index) => index >= 0 && index < crops.Count ? crops[index] : DefaultCrop;

    public void LoadCropStage(int index)
    {
        Stage = PhotoComposeStage.Crop;
        LoadCrop(Math.Clamp(index, 0, Math.Max(0, selected.Count - 1)));
    }

    public void CropBack()
    {
        if (CropIndex == 0)
        {
            Stage = PhotoComposeStage.Pick;
            return;
        }

        SaveCurrentCrop();
        LoadCrop(CropIndex - 1);
    }

    public bool CropAdvance()
    {
        SaveCurrentCrop();
        if (CropIndex < selected.Count - 1)
        {
            LoadCrop(CropIndex + 1);
            return false;
        }

        PreviewIndex = 0;
        Stage = PhotoComposeStage.Caption;
        return true;
    }

    private void LoadCrop(int index)
    {
        if (index < 0 || index >= crops.Count)
        {
            return;
        }

        CropIndex = index;
        var crop = crops[index];
        targetZoom = crop.Zoom;
        targetCenterX = crop.CenterX;
        targetCenterY = crop.CenterY;
        zoomSpring.SnapTo(crop.Zoom);
        centerXSpring.SnapTo(crop.CenterX);
        centerYSpring.SnapTo(crop.CenterY);
        cropDragging = false;
    }

    public void DrawPickGrid(Rect gridRect, float scale, in PhotoComposeStyle style, bool showBadges)
    {
        var gap = 6f * scale;
        var cell = (ScrollLayout.StableContentWidth() - gap * (GridColumns - 1)) / GridColumns;
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
        {
            for (var index = 0; index < pickerPaths.Length; index++)
            {
                ImGui.Dummy(new Vector2(cell, cell));
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                DrawLocalThumbnail(pickerPaths[index], min, max, scale, style.PlaceholderFill);
                if (showBadges)
                {
                    DrawPickBadge(pickerPaths[index], min, max, scale, style.Accent);
                }

                if (UiInteract.Click(min, max, UiInteract.Hover(min, max)))
                {
                    TakePicked(pickerPaths[index]);
                }

                if (index % GridColumns != GridColumns - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
    }

    private void DrawPickBadge(string path, Vector2 min, Vector2 max, float scale, Vector4 accent)
    {
        if (SingleSelect)
        {
            return;
        }

        var order = selected.IndexOf(path);
        if (order < 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.35f)), 10f * scale);
        var radius = 11f * scale;
        var center = new Vector2(max.X - radius - 6f * scale, min.Y + radius + 6f * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 20);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 20, 1.5f * scale);
        Typography.DrawCentered(drawList, center, (order + 1).ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.FootnoteEmphasized);
    }

    public void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale, Vector4 placeholderFill)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(placeholderFill));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    // allowReveal scopes the "show the whole image by default, letterboxed" behavior to ordinary
    // post photos - avatar and story crops keep the old cover-only, MinZoom=1 floor, since a
    // letterboxed avatar or a story that doesn't fill the screen would just look broken.
    public void DrawCropCanvas(Rect area, float scale, float aspect, in PhotoComposeStyle style, string gestureHint,
        float footerReserve = 0f, bool allowReveal = true)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var stageRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Max.Y - (96f + footerReserve) * scale));
        var preview = ImageFit.CenteredRect(stageRect, aspect);
        var rounding = 18f * scale;
        var texture = wallpaperImages.Get(CurrentPath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(style.PlaceholderFill));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), style.MutedInk);
            return;
        }

        var size = texture.Size;
        currentMinZoom = allowReveal ? WallpaperCrop.MinZoomToReveal(size, aspect) : WallpaperCrop.MinZoom;
        if (allowReveal)
        {
            ApplyRevealZoomIfNeeded(size, aspect);
        }

        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect, currentMinZoom);
        var (uv0, uv1) = crop.ComputeUv(size, aspect);
        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        if (style.EdgeFrame)
        {
            Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        }

        HandleCropGestures(preview, size, uv1 - uv0, aspect);
        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), gestureHint, style.MutedInk,
            0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale),
            new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomRange = WallpaperCrop.MaxZoom - currentMinZoom;
        var zoomNormalized = zoomRange > 0f ? (targetZoom - currentMinZoom) / zoomRange : 0f;
        var updatedZoom = Scrubber.Draw(track, zoomNormalized, style.ScrubberActive, style.ScrubberTrack, 1f);
        targetZoom = currentMinZoom + updatedZoom * zoomRange;
    }

    // Applies WallpaperCrop.MinZoomToReveal once per distinct target aspect for the current photo
    // (see revealedForRatio's doc comment), so a freshly-loaded or newly-reframed photo opens
    // showing the whole image instead of a cover crop, without fighting the user's own zoom/pan
    // once they've touched it for that aspect.
    private void ApplyRevealZoomIfNeeded(Vector2 imageSize, float aspect)
    {
        if (CropIndex < 0 || CropIndex >= revealedForRatio.Count)
        {
            return;
        }

        if (revealedForRatio[CropIndex] is { } prior && MathF.Abs(prior - aspect) < 0.0001f)
        {
            return;
        }

        revealedForRatio[CropIndex] = aspect;
        targetZoom = WallpaperCrop.MinZoomToReveal(imageSize, aspect);
        targetCenterX = 0.5f;
        targetCenterY = 0.5f;
        zoomSpring.SnapTo(targetZoom);
        centerXSpring.SnapTo(targetCenterX);
        centerYSpring.SnapTo(targetCenterY);
    }

    private void HandleCropGestures(Rect preview, Vector2 size, Vector2 visible, float aspect)
    {
        var hovering = UiInteract.Hover(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), currentMinZoom,
                    WallpaperCrop.MaxZoom);
            }
        }

        if (hovering && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            cropDragging = true;
            cropLastDrag = ImGui.GetMousePos();
        }

        if (cropDragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var position = ImGui.GetMousePos();
                var delta = position - cropLastDrag;
                cropLastDrag = position;
                if (preview.Width > 0f && preview.Height > 0f)
                {
                    targetCenterX -= delta.X * visible.X / preview.Width;
                    targetCenterY -= delta.Y * visible.Y / preview.Height;
                }
            }
            else
            {
                cropDragging = false;
            }
        }

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, aspect,
            currentMinZoom);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    public void DrawCaptionStrip(Rect strip, float scale, in PhotoComposeStyle style)
    {
        var count = selected.Count;
        var gap = 6f * scale;
        var side = MathF.Min(strip.Height, (strip.Width - gap * (count - 1)) / count);
        var span = side * count + gap * (count - 1);
        var startX = strip.Center.X - span * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(startX + index * (side + gap), strip.Min.Y);
            var max = min + new Vector2(side, side);
            DrawLocalThumbnail(selected[index], min, max, scale, style.PlaceholderFill);
            if (index == PreviewIndex)
            {
                drawList.AddRect(min, max, ImGui.GetColorU32(style.Accent), 8f * scale, ImDrawFlags.RoundCornersAll,
                    2f * scale);
            }
            else
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 8f * scale);
            }

            if (UiInteract.HoverClick(min, max))
            {
                PreviewIndex = index;
            }
        }
    }

    // aspect is the currently-previewed photo's own chosen aspect (session.AspectAt), not
    // necessarily the shared container aspect the caller frames the preview at - the caller is
    // expected to contain-fit the returned uv window into its own frame (see
    // ImageFit.VisibleAspect/CenteredRect) rather than stretch it, the same way DrawCropCanvas
    // does, so a photo whose own aspect differs from the container still shows undistorted.
    // allowReveal mirrors DrawCropCanvas's own flag - only Portrait ever reveals below a cover
    // crop; Square and Landscape (and avatar/story, which never call this with true) stay cover.
    public bool TryGetPreviewUv(float aspect, bool allowReveal, out IDalamudTextureWrap texture, out Vector2 uv0,
        out Vector2 uv1)
    {
        var index = ClampedPreviewIndex;
        var loaded = wallpaperImages.Get(index < selected.Count ? selected[index] : string.Empty);
        if (loaded is null)
        {
            texture = null!;
            uv0 = Vector2.Zero;
            uv1 = Vector2.One;
            return false;
        }

        texture = loaded;
        var minZoom = allowReveal ? WallpaperCrop.MinZoomToReveal(loaded.Size, aspect) : WallpaperCrop.MinZoom;
        var crop = crops[index].Clamped(loaded.Size, aspect, minZoom);
        (uv0, uv1) = crop.ComputeUv(loaded.Size, aspect);
        return true;
    }
}
