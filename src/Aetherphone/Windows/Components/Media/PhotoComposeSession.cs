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

namespace Aetherphone.Windows.Components;

internal enum PhotoComposeStage
{
    Pick,
    Crop,
    Caption,
}

internal enum PhotoCropMode : byte
{
    Crop,
    Edit,
}

internal readonly record struct PhotoComposeStyle(
    Vector4 Accent,
    Vector4 MutedInk,
    Vector4 PlaceholderFill,
    Vector4 ScrubberActive,
    Vector4 ScrubberTrack,
    bool EdgeFrame);

internal sealed class PhotoComposeSession : IDisposable
{
    public const int GridColumns = 3;
    public const float CanvasFooter = 96f;
    public const float CropModeStripReserve = 44f;
    private const float CropSmoothTime = 0.10f;
    private const float RatioEpsilon = 0.0001f;
    private const float CropModeStripGap = 6f;
    private const float CropModeStripHeight = 30f;
    private const float CropModeStripWidth = 200f;
    private const float EditPanelGap = 8f;
    private readonly string[] cropModeLabels = new string[2];

    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly List<string> selected = new();
    private readonly List<WallpaperCrop> crops = new();
    private readonly List<PostAspect> aspects = new();
    private readonly List<PhotoEdit> edits = new();
    private readonly List<PhotoEditPreview?> previews = new();
    // The ratio each photo was last framed at, so a change of aspect re-frames it exactly once
    // instead of fighting the user's own zoom and pan every frame.
    private readonly List<float?> framedForRatio = new();
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private bool cropDragging;
    private Vector2 cropLastDrag;

    public readonly PhotoEditControls EditControls = new();

    public PhotoComposeSession(PhotoLibrary library, WallpaperImageCache wallpaperImages)
    {
        this.library = library;
        this.wallpaperImages = wallpaperImages;
    }

    private static WallpaperCrop DefaultCrop => new(1f, 0.5f, 0.5f);

    public PhotoComposeStage Stage { get; set; }
    public PhotoCropMode CropMode { get; set; }
    public bool SingleSelect { get; private set; }
    public bool AllowGif { get; private set; }
    public bool GifSelected => AllowGif && selected.Count == 1 && GifMedia.IsGif(selected[0]);

    public float GifAspect =>
        wallpaperImages.Get(FirstSelected) is { } gifTexture && gifTexture.Size.Y > 0f
            ? gifTexture.Size.X / gifTexture.Size.Y
            : 1f;

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

    public PostAspect CurrentAspect => AspectAt(CropIndex);

    public PhotoEdit CurrentEdit => EditControls.Edit;

    public bool CurrentEditIsDirty => !EditControls.Edit.IsIdentity;

    // The first photo sets the carousel frame for the whole post; any other photo whose aspect
    // differs is contain-fit inside it rather than reframing the carousel as you swipe.
    public PostAspect ContainerAspect => AspectAt(0);

    public string[] SelectedArray() => selected.ToArray();

    public WallpaperCrop[] CropsArray() => crops.ToArray();

    public PostAspect[] AspectsArray() => aspects.ToArray();

    public PhotoEdit[] EditsArray()
    {
        SyncCurrentEdit();
        return edits.ToArray();
    }

    public PostAspect AspectAt(int index) => index >= 0 && index < aspects.Count ? aspects[index] : PostAspect.Square;

    public PhotoEdit EditAt(int index) => index >= 0 && index < edits.Count ? edits[index] : PhotoEdit.None;

    public void SetAspect(int index, PostAspect aspect)
    {
        if (index >= 0 && index < aspects.Count)
        {
            aspects[index] = aspect;
        }
    }

    public void SyncCurrentEdit()
    {
        if (CropIndex >= 0 && CropIndex < edits.Count)
        {
            edits[CropIndex] = EditControls.Edit;
        }
    }

    public void ResetCurrentEdit()
    {
        EditControls.Load(PhotoEdit.None);
        SyncCurrentEdit();
    }

    public void Open(bool singleSelect, bool allowGif = false)
    {
        SingleSelect = singleSelect;
        AllowGif = allowGif && !singleSelect;
        Stage = PhotoComposeStage.Pick;
        CropMode = PhotoCropMode.Crop;
        selected.Clear();
        crops.Clear();
        aspects.Clear();
        edits.Clear();
        framedForRatio.Clear();
        ClosePreviews();
        EditControls.Reset();
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

        if (AllowGif)
        {
            if (GifMedia.IsGif(path))
            {
                if (selected.Count > 0)
                {
                    Notice = Loc.T(L.Common.GifRidesAlone);
                    return;
                }

                if (!GifMedia.FitsSizeCap(path))
                {
                    Notice = Loc.T(L.Common.GifTooLarge);
                    return;
                }
            }
            else if (GifSelected)
            {
                Notice = Loc.T(L.Common.GifRidesAlone);
                return;
            }
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
        edits.Clear();
        framedForRatio.Clear();
        ClosePreviews();
        for (var index = 0; index < selected.Count; index++)
        {
            crops.Add(DefaultCrop);
            aspects.Add(PostAspect.Square);
            edits.Add(PhotoEdit.None);
            framedForRatio.Add(null);
            previews.Add(null);
        }

        PreviewIndex = 0;
        CropMode = PhotoCropMode.Crop;
        if (GifSelected)
        {
            Stage = PhotoComposeStage.Caption;
            return;
        }

        Stage = PhotoComposeStage.Crop;
        LoadCrop(0);
    }

    public void CaptionBack()
    {
        if (GifSelected)
        {
            Stage = PhotoComposeStage.Pick;
            return;
        }

        LoadCropStage(selected.Count - 1);
    }

    public void SaveCurrentCrop()
    {
        if (CropIndex >= 0 && CropIndex < crops.Count)
        {
            crops[CropIndex] = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        }

        SyncCurrentEdit();
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
        EditControls.Load(EditAt(index));
    }

    private PhotoEditPreview PreviewFor(int index)
    {
        while (previews.Count <= index)
        {
            previews.Add(null);
        }

        if (previews[index] is { } existing)
        {
            return existing;
        }

        var opened = new PhotoEditPreview();
        opened.Open(selected[index]);
        previews[index] = opened;
        return opened;
    }

    private IDalamudTextureWrap? TextureFor(int index, double now)
    {
        if (index < 0 || index >= selected.Count)
        {
            return null;
        }

        var source = wallpaperImages.Get(selected[index]);
        var edit = EditAt(index);
        if (edit.IsIdentity)
        {
            return source;
        }

        return PreviewFor(index).Texture(edit, now) ?? source;
    }

    private void ClosePreviews()
    {
        for (var index = 0; index < previews.Count; index++)
        {
            previews[index]?.Dispose();
        }

        previews.Clear();
    }

    public static float EditFooterReserve => PhotoEditPanel.ComposerHeight + EditPanelGap - CanvasFooter;

    public static Rect EditPanelRect(Rect area, float scale)
    {
        return new Rect(new Vector2(area.Min.X, area.Max.Y - (PhotoEditPanel.ComposerHeight * scale)), area.Max);
    }

    public void DrawCropModeStrip(Rect area, AppSkin ui, float scale, string id)
    {
        cropModeLabels[0] = Loc.T(L.Photos.ToolCrop);
        cropModeLabels[1] = Loc.T(L.Photos.Edit);
        var top = area.Min.Y + ((AppHeader.Height + CropModeStripGap) * scale);
        var width = MathF.Min(area.Width - (32f * scale), CropModeStripWidth * scale);
        var row = new Rect(new Vector2(area.Center.X - (width * 0.5f), top),
            new Vector2(area.Center.X + (width * 0.5f), top + (CropModeStripHeight * scale)));
        var picked = SegmentStrip.Draw(id, row, cropModeLabels, (int)CropMode, ui.Palette);
        CropMode = picked == 1 ? PhotoCropMode.Edit : PhotoCropMode.Crop;
    }

    public void DrawPickGrid(Rect gridRect, float scale, in PhotoComposeStyle style, bool showBadges)
    {
        var gap = 6f * scale;
        var avail = ScrollLayout.StableContentWidth();
        var cell = (avail - gap * (GridColumns - 1)) / GridColumns;
        var origin = ImGui.GetCursorScreenPos();
        var scrollY = ImGui.GetScrollY();
        var viewHeight = ImGui.GetWindowSize().Y;
        var margin = cell + 60f * scale;
        for (var index = 0; index < pickerPaths.Length; index++)
        {
            var column = index % GridColumns;
            var rowIndex = index / GridColumns;
            var top = rowIndex * (cell + gap);
            if (top + cell < scrollY - margin || top > scrollY + viewHeight + margin)
            {
                continue;
            }

            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + top);
            var max = new Vector2(min.X + cell, min.Y + cell);
            var path = pickerPaths[index];
            var hovered = UiInteract.Hover(min, max);
            DrawLocalThumbnail(path, min, max, scale, style.PlaceholderFill, hovered);
            if (showBadges)
            {
                DrawPickBadge(path, min, max, scale, style.Accent);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                TakePicked(path);
            }
        }

        var rows = (pickerPaths.Length + GridColumns - 1) / GridColumns;
        var totalHeight = rows * (cell + gap);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, totalHeight));
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

    public void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale, Vector4 placeholderFill,
        bool hovered)
    {
        DrawThumbnail(wallpaperImages.Get(path), min, max, scale, placeholderFill, hovered);
    }

    private static void DrawThumbnail(IDalamudTextureWrap? texture, Vector2 min, Vector2 max, float scale,
        Vector4 placeholderFill, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(placeholderFill));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    // Avatar and story crops pass allowReveal false: a letterboxed avatar, or a story that does not
    // fill the screen, would read as broken rather than as framing. With interactive false the
    // canvas only shows the framed photo, for the edit mode that owns the space below it.
    public void DrawCropCanvas(Rect area, float scale, float aspect, in PhotoComposeStyle style, string gestureHint,
        float footerReserve, bool allowReveal, float topInset = 0f, bool interactive = true)
    {
        SyncCurrentEdit();
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + (AppHeader.Height + topInset) * scale;
        var stageRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Max.Y - (CanvasFooter + footerReserve) * scale));
        var preview = ImageFit.CenteredRect(stageRect, aspect);
        var rounding = 18f * scale;
        var texture = TextureFor(CropIndex, ImGui.GetTime());
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(style.PlaceholderFill));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), style.MutedInk);
            return;
        }

        var size = texture.Size;
        var minZoom = allowReveal ? WallpaperCrop.MinZoomToReveal(size, aspect) : WallpaperCrop.MinZoom;
        ApplyAspectFraming(size, aspect, allowReveal);
        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect, minZoom);
        var (uv0, uv1) = crop.ComputeUv(size, aspect, minZoom);
        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        if (style.EdgeFrame)
        {
            Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        }

        if (!interactive)
        {
            return;
        }

        HandleCropGestures(preview, size, uv1 - uv0, aspect, minZoom);
        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), gestureHint, style.MutedInk,
            0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale),
            new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomRange = WallpaperCrop.MaxZoom - minZoom;
        var zoomNormalized = zoomRange > 0f ? (targetZoom - minZoom) / zoomRange : 0f;
        var updatedZoom = Scrubber.Draw(track, zoomNormalized, style.ScrubberActive, style.ScrubberTrack, 1f);
        targetZoom = minZoom + updatedZoom * zoomRange;
    }

    // The ratio is recorded on every aspect change, not just revealing ones, so that switching
    // Portrait to Square and back still re-reveals rather than keeping the cover crop Square left
    // behind.
    private void ApplyAspectFraming(Vector2 imageSize, float aspect, bool allowReveal)
    {
        if (CropIndex < 0 || CropIndex >= framedForRatio.Count)
        {
            return;
        }

        if (framedForRatio[CropIndex] is { } prior && MathF.Abs(prior - aspect) < RatioEpsilon)
        {
            return;
        }

        framedForRatio[CropIndex] = aspect;
        if (!allowReveal)
        {
            return;
        }

        targetZoom = WallpaperCrop.MinZoomToReveal(imageSize, aspect);
        targetCenterX = 0.5f;
        targetCenterY = 0.5f;
        zoomSpring.SnapTo(targetZoom);
        centerXSpring.SnapTo(targetCenterX);
        centerYSpring.SnapTo(targetCenterY);
    }

    private void HandleCropGestures(Rect preview, Vector2 size, Vector2 visible, float aspect, float minZoom)
    {
        var hovering = UiInteract.Hover(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), minZoom, WallpaperCrop.MaxZoom);
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

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, aspect, minZoom);
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
        var now = ImGui.GetTime();
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(startX + index * (side + gap), strip.Min.Y);
            var max = min + new Vector2(side, side);
            DrawThumbnail(TextureFor(index, now), min, max, scale, style.PlaceholderFill, UiInteract.Hover(min, max));
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

    // aspect is the previewed photo's own aspect, not the shared container the caller frames it in,
    // so the caller must contain-fit the returned uv window rather than stretch it.
    public bool TryGetPreviewUv(float aspect, bool allowReveal, out IDalamudTextureWrap texture, out Vector2 uv0,
        out Vector2 uv1)
    {
        var index = ClampedPreviewIndex;
        var loaded = TextureFor(index, ImGui.GetTime());
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
        (uv0, uv1) = crop.ComputeUv(loaded.Size, aspect, minZoom);
        return true;
    }

    public void Dispose()
    {
        ClosePreviews();
    }
}
