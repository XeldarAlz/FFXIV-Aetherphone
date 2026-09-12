using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal enum PhotoComposeStage
{
    Pick,
    Edit,
    Caption,
}

internal readonly record struct PhotoComposeStyle(
    Vector4 Accent,
    Vector4 MutedInk,
    Vector4 PlaceholderFill,
    Vector4 Rail,
    bool EdgeFrame);

internal sealed class PhotoComposeSession : IDisposable
{
    public const int GridColumns = 3;
    public const float StripHeight = 46f;

    private const float CropSmoothTime = 0.10f;
    private const float RatioEpsilon = 0.0001f;
    private const float MaxDeltaSeconds = 0.1f;
    private const float WheelZoomStep = 0.12f;
    private const float StageInsetX = 16f;
    private const float StageInsetTop = 12f;
    private const float StageGap = 8f;
    private const float StageRounding = 18f;
    private const float StripGap = 10f;
    private const float StripThumbGap = 6f;
    private const float StripRounding = 8f;
    private const float StripStroke = 2f;
    private const float StripDim = 0.35f;
    private const float GridGap = 6f;
    private const float GridOverscan = 60f;
    private const float TileRounding = 10f;
    private const float AspectButtonRadius = 17f;
    private const float AspectButtonInset = 12f;
    private const float AspectIconScale = 0.8f;
    private const float EmptyIconSize = 34f;
    private const float EmptyTextGap = 12f;
    private const float ImportIconSize = 26f;
    private const float ImportLabelGap = 6f;
    private const float ImportLabelPad = 8f;
    private const float BadgeRadius = 11f;
    private const float BadgeInset = 6f;
    private const float BadgeRing = 1.5f;
    private const float SelectedWashAlpha = 0.35f;
    private const float CurrentRing = 2.5f;
    private const int CircleSegments = 24;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 WhiteRing = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 Scrim = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 ScrimHover = new(0.12f, 0.12f, 0.12f, 0.7f);
    private static readonly Vector4 HoverWash = new(1f, 1f, 1f, 0.1f);
    private static readonly PhotoEditTool[] Tools = { PhotoEditTool.Looks, PhotoEditTool.Adjust };

    private readonly PhotoLibrary library;
    private readonly WallpaperImageCache wallpaperImages;
    private readonly List<string> selected = new();
    private readonly List<WallpaperCrop> crops = new();
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
    private readonly PhotoEditControls editControls = new();

    public PhotoComposeSession(PhotoLibrary library, WallpaperImageCache wallpaperImages)
    {
        this.library = library;
        this.wallpaperImages = wallpaperImages;
    }

    private static WallpaperCrop DefaultCrop => new(1f, 0.5f, 0.5f);

    public PhotoComposeStage Stage { get; set; }
    public bool SingleSelect { get; private set; }
    public bool AllowGif { get; private set; }
    public bool GifSelected => AllowGif && selected.Count == 1 && GifMedia.IsGif(selected[0]);

    public float GifAspect =>
        wallpaperImages.Get(FirstSelected) is { } gifTexture && gifTexture.Size.Y > 0f
            ? gifTexture.Size.X / gifTexture.Size.Y
            : 1f;

    // One aspect frames the whole post, as on Instagram; every photo is positioned inside it.
    public PostAspect Aspect { get; set; }
    public int CurrentIndex { get; private set; }
    public int PreviewIndex { get; set; }
    public string Notice { get; private set; } = string.Empty;
    public int SelectedCount => selected.Count;
    public bool HasSelection => selected.Count > 0;
    public string FirstSelected => selected.Count > 0 ? selected[0] : string.Empty;
    public string CurrentPath => CurrentIndex >= 0 && CurrentIndex < selected.Count ? selected[CurrentIndex] : string.Empty;
    public WallpaperCrop CurrentTargetCrop => new(targetZoom, targetCenterX, targetCenterY);
    public int ClampedPreviewIndex => Math.Clamp(PreviewIndex, 0, Math.Max(0, selected.Count - 1));

    public string[] SelectedArray() => selected.ToArray();

    public WallpaperCrop[] CropsArray()
    {
        SaveCurrentCrop();
        return crops.ToArray();
    }

    public PostAspect[] AspectsArray()
    {
        var aspects = new PostAspect[selected.Count];
        Array.Fill(aspects, Aspect);
        return aspects;
    }

    public PhotoEdit[] EditsArray()
    {
        SyncCurrentEdit();
        return edits.ToArray();
    }

    public PhotoEdit EditAt(int index) => index >= 0 && index < edits.Count ? edits[index] : PhotoEdit.None;

    public WallpaperCrop CropAt(int index) => index >= 0 && index < crops.Count ? crops[index] : DefaultCrop;

    public void Open(bool singleSelect, bool allowGif = false)
    {
        SingleSelect = singleSelect;
        AllowGif = allowGif && !singleSelect;
        Stage = PhotoComposeStage.Pick;
        selected.Clear();
        crops.Clear();
        edits.Clear();
        framedForRatio.Clear();
        ClosePreviews();
        editControls.Reset();
        OpenLooksTool();
        Aspect = PostAspect.Square;
        CurrentIndex = 0;
        PreviewIndex = 0;
        Notice = string.Empty;
        pendingPickedPath = null;
        pickerPaths = library.List();
        LoadCrop(DefaultCrop);
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
            SelectCurrent(selected.IndexOf(picked));
            return;
        }

        TakePicked(picked);
    }

    // Tapping a selected photo brings it into the pane to frame it; tapping the framed photo
    // deselects it. That is Instagram's multi-select, and it keeps one tap per intent.
    public void TakePicked(string path)
    {
        if (SingleSelect)
        {
            ClearSelection();
            Append(path);
            return;
        }

        var existing = selected.IndexOf(path);
        if (existing >= 0)
        {
            if (existing == CurrentIndex)
            {
                RemoveAt(existing);
                return;
            }

            SelectCurrent(existing);
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
        Append(path);
    }

    public void CycleAspect()
    {
        var all = PostAspects.All;
        var index = Array.IndexOf(all, Aspect);
        Aspect = all[(index + 1) % all.Length];
    }

    public void BeginEdit()
    {
        if (selected.Count == 0)
        {
            return;
        }

        SaveCurrentCrop();
        PreviewIndex = 0;
        if (GifSelected)
        {
            Stage = PhotoComposeStage.Caption;
            return;
        }

        OpenLooksTool();
        Stage = PhotoComposeStage.Edit;
        SelectCurrent(0);
    }

    public void EditBack()
    {
        SaveCurrentCrop();
        Stage = PhotoComposeStage.Pick;
    }

    public void EditAdvance()
    {
        SaveCurrentCrop();
        PreviewIndex = 0;
        Stage = PhotoComposeStage.Caption;
    }

    public void CaptionBack()
    {
        if (GifSelected)
        {
            Stage = PhotoComposeStage.Pick;
            return;
        }

        OpenEdit(ClampedPreviewIndex);
    }

    public void OpenEdit(int index)
    {
        Stage = PhotoComposeStage.Edit;
        SelectCurrent(Math.Clamp(index, 0, Math.Max(0, selected.Count - 1)));
    }

    public void SelectCurrent(int index)
    {
        if (index < 0 || index >= selected.Count)
        {
            return;
        }

        SaveCurrentCrop();
        CurrentIndex = index;
        LoadCrop(crops[index]);
        editControls.Load(edits[index]);
    }

    private void OpenLooksTool()
    {
        editControls.Tool = PhotoEditTool.Looks;
        editControls.DockSpring.SnapTo(Array.IndexOf(Tools, PhotoEditTool.Looks));
    }

    private void Append(string path)
    {
        SaveCurrentCrop();
        selected.Add(path);
        crops.Add(DefaultCrop);
        edits.Add(PhotoEdit.None);
        framedForRatio.Add(null);
        previews.Add(null);
        CurrentIndex = selected.Count - 1;
        LoadCrop(DefaultCrop);
        editControls.Load(PhotoEdit.None);
    }

    private void RemoveAt(int index)
    {
        selected.RemoveAt(index);
        crops.RemoveAt(index);
        edits.RemoveAt(index);
        framedForRatio.RemoveAt(index);
        previews[index]?.Dispose();
        previews.RemoveAt(index);
        Notice = string.Empty;
        if (selected.Count == 0)
        {
            CurrentIndex = 0;
            LoadCrop(DefaultCrop);
            editControls.Load(PhotoEdit.None);
            return;
        }

        CurrentIndex = Math.Min(index, selected.Count - 1);
        LoadCrop(crops[CurrentIndex]);
        editControls.Load(edits[CurrentIndex]);
    }

    private void ClearSelection()
    {
        selected.Clear();
        crops.Clear();
        edits.Clear();
        framedForRatio.Clear();
        ClosePreviews();
        CurrentIndex = 0;
        Notice = string.Empty;
    }

    private void SaveCurrentCrop()
    {
        if (CurrentIndex >= 0 && CurrentIndex < crops.Count)
        {
            crops[CurrentIndex] = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        }

        SyncCurrentEdit();
    }

    private void SyncCurrentEdit()
    {
        if (CurrentIndex >= 0 && CurrentIndex < edits.Count)
        {
            edits[CurrentIndex] = editControls.Edit;
        }
    }

    private void LoadCrop(WallpaperCrop crop)
    {
        targetZoom = crop.Zoom;
        targetCenterX = crop.CenterX;
        targetCenterY = crop.CenterY;
        zoomSpring.SnapTo(crop.Zoom);
        centerXSpring.SnapTo(crop.CenterX);
        centerYSpring.SnapTo(crop.CenterY);
        cropDragging = false;
    }

    private PhotoEditPreview PreviewFor(int index)
    {
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

    public void DrawPickPane(Rect pane, float scale, in PhotoComposeStyle style, float aspect, bool allowReveal,
        bool allowAspectChoice, bool interactive)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pane.Min, pane.Max, ImGui.GetColorU32(style.PlaceholderFill));
        var texture = TextureFor(CurrentIndex, ImGui.GetTime());
        if (texture is null)
        {
            DrawPaneEmpty(drawList, pane, scale, style);
            return;
        }

        var preview = ImageFit.CenteredRect(pane, aspect);
        DrawFramedPhoto(drawList, preview, texture, aspect, allowReveal, 0f, interactive && !GifSelected);
        if (allowAspectChoice && !GifSelected)
        {
            DrawAspectButton(drawList, pane, scale, interactive);
        }
    }

    private void DrawPaneEmpty(ImDrawListPtr drawList, Rect pane, float scale, in PhotoComposeStyle style)
    {
        if (HasSelection)
        {
            Typography.DrawCentered(drawList, pane.Center, Loc.T(L.Common.Loading), style.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        var iconSize = EmptyIconSize * scale;
        var textHeight = Typography.LineHeight(TextStyles.Subheadline);
        var blockTop = pane.Center.Y - (iconSize + EmptyTextGap * scale + textHeight) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(pane.Center.X, blockTop + iconSize * 0.5f), PhoneIcons.Photo,
            style.MutedInk, iconSize);
        Typography.DrawCentered(drawList,
            new Vector2(pane.Center.X, blockTop + iconSize + EmptyTextGap * scale + textHeight * 0.5f),
            Loc.T(L.Social.ComposeChoosePhoto), style.MutedInk, TextStyles.Subheadline);
    }

    private void DrawAspectButton(ImDrawListPtr drawList, Rect pane, float scale, bool interactive)
    {
        var radius = AspectButtonRadius * scale;
        var inset = AspectButtonInset * scale;
        var center = new Vector2(pane.Min.X + inset + radius, pane.Max.Y - inset - radius);
        var hit = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - hit, center + hit);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? ScrimHover : Scrim), CircleSegments);
        AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.Expand), White, AspectIconScale);
        HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(AspectLabels.For(Aspect)));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - hit, center + hit, hovered))
        {
            CycleAspect();
        }
    }

    public void DrawPickGrid(Rect gridRect, float scale, in PhotoComposeStyle style, bool showBadges,
        string importLabel, string importTitle)
    {
        var gap = GridGap * scale;
        var avail = ScrollLayout.StableContentWidth();
        var cell = (avail - gap * (GridColumns - 1)) / GridColumns;
        var origin = ImGui.GetCursorScreenPos();
        var scrollY = ImGui.GetScrollY();
        var viewHeight = ImGui.GetWindowSize().Y;
        var margin = cell + GridOverscan * scale;
        var cellCount = pickerPaths.Length + 1;
        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            var column = cellIndex % GridColumns;
            var rowIndex = cellIndex / GridColumns;
            var top = rowIndex * (cell + gap);
            if (top + cell < scrollY - margin || top > scrollY + viewHeight + margin)
            {
                continue;
            }

            var min = new Vector2(origin.X + column * (cell + gap), origin.Y + top);
            var max = new Vector2(min.X + cell, min.Y + cell);
            var hovered = UiInteract.Hover(min, max);
            if (cellIndex == 0)
            {
                DrawImportTile(min, max, scale, style, importLabel, hovered);
                if (UiInteract.Click(min, max, hovered))
                {
                    LaunchImportDialog(importTitle);
                }

                continue;
            }

            var path = pickerPaths[cellIndex - 1];
            DrawThumbnail(wallpaperImages.Get(path), min, max, scale, style.PlaceholderFill, hovered);
            if (showBadges)
            {
                DrawPickBadge(path, min, max, scale, style.Accent);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                TakePicked(path);
            }
        }

        var rows = (cellCount + GridColumns - 1) / GridColumns;
        var totalHeight = rows * (cell + gap);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, totalHeight));
    }

    private static void DrawImportTile(Vector2 min, Vector2 max, float scale, in PhotoComposeStyle style,
        string label, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = TileRounding * scale;
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(style.PlaceholderFill));
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var center = (min + max) * 0.5f;
        var iconSize = ImportIconSize * scale;
        var fitted = Typography.FitText(label, max.X - min.X - ImportLabelPad * 2f * scale, TextStyles.Caption1);
        var textHeight = Typography.LineHeight(TextStyles.Caption1);
        var blockTop = center.Y - (iconSize + ImportLabelGap * scale + textHeight) * 0.5f;
        PhoneIcon.Draw(drawList, new Vector2(center.X, blockTop + iconSize * 0.5f), PhoneIcons.Plus, style.MutedInk,
            iconSize);
        Typography.DrawCentered(drawList,
            new Vector2(center.X, blockTop + iconSize + ImportLabelGap * scale + textHeight * 0.5f), fitted,
            style.MutedInk, TextStyles.Caption1);
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
        var rounding = TileRounding * scale;
        if (order == CurrentIndex)
        {
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(accent), CurrentRing * scale);
        }
        else
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(accent, SelectedWashAlpha)), rounding);
        }

        var radius = BadgeRadius * scale;
        var center = new Vector2(max.X - radius - BadgeInset * scale, min.Y + radius + BadgeInset * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), CircleSegments);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(WhiteRing), CircleSegments, BadgeRing * scale);
        Typography.DrawCentered(drawList, center, (order + 1).ToString(Loc.Culture), White,
            TextStyles.FootnoteEmphasized);
    }

    private static void DrawThumbnail(IDalamudTextureWrap? texture, Vector2 min, Vector2 max, float scale,
        Vector4 placeholderFill, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = TileRounding * scale;
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
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(HoverWash), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    // The canvas stays pannable in every tool so a photo can be reframed without leaving the
    // Edit step; the aspect itself is chosen while picking.
    public void DrawEditCanvas(Rect area, float scale, float aspect, in PhotoComposeStyle style, bool allowReveal,
        bool interactive)
    {
        SyncCurrentEdit();
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var bottom = area.Max.Y - (PhotoEditPanel.Height + StageGap) * scale;
        var left = area.Min.X + StageInsetX * scale;
        var right = area.Max.X - StageInsetX * scale;
        if (selected.Count > 1)
        {
            var strip = new Rect(new Vector2(left, bottom - StripHeight * scale), new Vector2(right, bottom));
            var tapped = DrawPhotoStrip(strip, scale, style, CurrentIndex);
            if (tapped >= 0)
            {
                SelectCurrent(tapped);
            }

            bottom = strip.Min.Y - StripGap * scale;
        }

        var stage = new Rect(new Vector2(left, top + StageInsetTop * scale), new Vector2(right, bottom));
        var preview = ImageFit.CenteredRect(stage, aspect);
        var rounding = StageRounding * scale;
        var texture = TextureFor(CurrentIndex, ImGui.GetTime());
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(style.PlaceholderFill));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), style.MutedInk);
            return;
        }

        DrawFramedPhoto(drawList, preview, texture, aspect, allowReveal, rounding, interactive);
        if (style.EdgeFrame)
        {
            Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        }
    }

    public void DrawComposerFooter(Rect area, float scale, in PhotoEditPanelStyle editStyle, bool interactive)
    {
        var footer = PhotoEditPanel.FooterRect(area, area.Max.Y, scale);
        if (editControls.Tool == PhotoEditTool.Adjust)
        {
            PhotoEditPanel.DrawAdjust(editControls, footer, editStyle, scale, interactive, true);
        }
        else
        {
            PhotoEditPanel.DrawLooks(editControls, PreviewFor(CurrentIndex), footer, editStyle, scale, interactive);
        }

        PhotoEditPanel.DrawDock(editControls, footer, editStyle, scale, interactive, Tools, true);
    }

    private void DrawFramedPhoto(ImDrawListPtr drawList, Rect preview, IDalamudTextureWrap texture, float aspect,
        bool allowReveal, float rounding, bool interactive)
    {
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, MaxDeltaSeconds);
        var size = texture.Size;
        var minZoom = allowReveal ? WallpaperCrop.MinZoomToReveal(size, aspect) : WallpaperCrop.MinZoom;
        ApplyAspectFraming(size, aspect, allowReveal);
        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect, minZoom);
        var (uv0, uv1) = crop.ComputeUv(size, aspect, minZoom);
        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        if (interactive)
        {
            HandleCropGestures(preview, size, uv1 - uv0, aspect, minZoom);
        }
    }

    // The ratio is recorded on every aspect change, not just revealing ones, so that switching
    // Portrait to Square and back still re-reveals rather than keeping the cover crop Square left
    // behind.
    private void ApplyAspectFraming(Vector2 imageSize, float aspect, bool allowReveal)
    {
        if (CurrentIndex < 0 || CurrentIndex >= framedForRatio.Count)
        {
            return;
        }

        if (framedForRatio[CurrentIndex] is { } prior && MathF.Abs(prior - aspect) < RatioEpsilon)
        {
            return;
        }

        framedForRatio[CurrentIndex] = aspect;
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
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * WheelZoomStep), minZoom, WallpaperCrop.MaxZoom);
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

    public int DrawPhotoStrip(Rect strip, float scale, in PhotoComposeStyle style, int activeIndex)
    {
        var count = selected.Count;
        var gap = StripThumbGap * scale;
        var side = MathF.Min(strip.Height, (strip.Width - gap * (count - 1)) / count);
        var span = side * count + gap * (count - 1);
        var startX = strip.Center.X - span * 0.5f;
        var drawList = ImGui.GetWindowDrawList();
        var now = ImGui.GetTime();
        var rounding = StripRounding * scale;
        var tapped = -1;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(startX + index * (side + gap), strip.Center.Y - side * 0.5f);
            var max = min + new Vector2(side, side);
            var hovered = UiInteract.Hover(min, max);
            DrawThumbnail(TextureFor(index, now), min, max, scale, style.PlaceholderFill, hovered);
            if (index == activeIndex)
            {
                Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(style.Accent), StripStroke * scale);
            }
            else
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, StripDim)), rounding);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                tapped = index;
            }
        }

        return tapped;
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
        var crop = CropAt(index).Clamped(loaded.Size, aspect, minZoom);
        (uv0, uv1) = crop.ComputeUv(loaded.Size, aspect, minZoom);
        return true;
    }

    public void Dispose()
    {
        ClosePreviews();
    }
}
