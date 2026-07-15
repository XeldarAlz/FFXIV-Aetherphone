using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed class VelvetPostComposer
{
    private const int GridColumns = 3;
    private const float CropSmoothTime = 0.10f;
    private readonly VelvetStore store;
    private readonly StoryPresenter stories;
    private readonly PhotoLibrary library;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly MentionPopup mentionPopup = new();
    private readonly MentionAutocomplete captionMentions;
    private readonly List<string> selected = new();
    private readonly List<WallpaperCrop> crops = new();
    private ComposeStage stage;
    private bool storyMode;
    private int cropIndex;
    private int previewIndex;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pendingPickedPath;
    private volatile int outcome;
    private bool closeRequested;
    private int visibility = VelvetVisibility.Public;
    private string caption = string.Empty;
    private string limitNotice = string.Empty;
    private string status = string.Empty;
    private Spring zoomSpring = new(1f);
    private Spring centerXSpring = new(0.5f);
    private Spring centerYSpring = new(0.5f);
    private float targetZoom = 1f;
    private float targetCenterX = 0.5f;
    private float targetCenterY = 0.5f;
    private bool cropDragging;
    private Vector2 cropLastDrag;

    public VelvetPostComposer(VelvetStore store, StoryPresenter stories, PhotoLibrary library,
        RemoteImageCache images, LodestoneService lodestone)
    {
        this.store = store;
        this.stories = stories;
        this.library = library;
        this.images = images;
        this.lodestone = lodestone;
        captionMentions = new MentionAutocomplete(store.NewMentionSuggestions());
    }

    private enum ComposeStage
    {
        Pick,
        Crop,
        Caption,
    }

    private static WallpaperCrop DefaultCrop => new(1f, 0.5f, 0.5f);

    private string CurrentPath => cropIndex >= 0 && cropIndex < selected.Count ? selected[cropIndex] : string.Empty;

    private float Aspect => storyMode ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight : 1f;

    private string Title => storyMode ? Loc.T(L.Story.NewStory) : Loc.T(L.Velvet.NewPost);

    private bool Posting => storyMode ? stories.Posting : store.Posting;

    public void Open(bool story = false)
    {
        storyMode = story;
        stage = ComposeStage.Pick;
        selected.Clear();
        crops.Clear();
        cropIndex = 0;
        previewIndex = 0;
        pendingPickedPath = null;
        outcome = 0;
        closeRequested = false;
        visibility = VelvetVisibility.Public;
        caption = string.Empty;
        limitNotice = string.Empty;
        status = string.Empty;
        pickerPaths = library.List();
    }

    public bool Draw(Rect area, AppSkin ui, in PhoneContext context)
    {
        if (outcome == 1)
        {
            outcome = 0;
            return true;
        }

        if (outcome == 2)
        {
            outcome = 0;
            status = Loc.T(L.Account.CannotReach);
        }

        if (closeRequested)
        {
            closeRequested = false;
            return true;
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            TakePicked(picked);
        }

        switch (stage)
        {
            case ComposeStage.Crop:
                DrawCrop(area, ui, context);
                break;
            case ComposeStage.Caption:
                DrawCaption(area, ui, context);
                break;
            default:
                DrawPick(area, ui, context);
                break;
        }

        return false;
    }

    private void DrawPick(Rect area, AppSkin ui, in PhoneContext context)
    {
        AppHeader.Draw(context, Title, () => closeRequested = true);
        if (!storyMode && ui.HeaderAction(area, Loc.T(L.Common.Next), selected.Count > 0))
        {
            BeginCropSequence();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Velvet.ImportFromPc), true))
        {
            LaunchFileDialog();
        }

        var noticeHeight = limitNotice.Length > 0 ? 20f * scale : 0f;
        if (noticeHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, importRect.Max.Y + 8f * scale), limitNotice,
                AppPalettes.Velvet.MutedInk, TextStyles.Footnote);
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale + noticeHeight), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Velvet.NoPhotos), AppPalettes.Velvet.MutedInk);
                return;
            }

            var gap = 6f * scale;
            var cell = (ScrollLayout.StableContentWidth() - gap * (GridColumns - 1)) / GridColumns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < pickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("pick", new Vector2(cell, cell));
                        DrawLocalThumbnail(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        DrawPickBadge(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        if (clicked)
                        {
                            TakePicked(pickerPaths[index]);
                        }
                    }

                    if (index % GridColumns != GridColumns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void DrawPickBadge(string path, Vector2 min, Vector2 max, float scale)
    {
        if (storyMode)
        {
            return;
        }

        var order = selected.IndexOf(path);
        if (order < 0)
        {
            return;
        }

        var accent = AppPalettes.Velvet.Accent;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.35f)), 10f * scale);
        var radius = 11f * scale;
        var center = new Vector2(max.X - radius - 6f * scale, min.Y + radius + 6f * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(accent), 20);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 20, 1.5f * scale);
        Typography.DrawCentered(drawList, center, (order + 1).ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.FootnoteEmphasized);
    }

    private void TakePicked(string path)
    {
        if (storyMode)
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
            limitNotice = Loc.T(L.Common.PhotoLimit, PostMedia.MaxPhotos);
            return;
        }

        limitNotice = string.Empty;
        selected.Add(path);
    }

    private void BeginCropSequence()
    {
        crops.Clear();
        for (var index = 0; index < selected.Count; index++)
        {
            crops.Add(DefaultCrop);
        }

        previewIndex = 0;
        stage = ComposeStage.Crop;
        LoadCrop(0);
    }

    private void SaveCurrentCrop()
    {
        if (cropIndex >= 0 && cropIndex < crops.Count)
        {
            crops[cropIndex] = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        }
    }

    private void LoadCrop(int index)
    {
        if (index < 0 || index >= crops.Count)
        {
            return;
        }

        cropIndex = index;
        var crop = crops[index];
        targetZoom = crop.Zoom;
        targetCenterX = crop.CenterX;
        targetCenterY = crop.CenterY;
        zoomSpring.SnapTo(crop.Zoom);
        centerXSpring.SnapTo(crop.CenterX);
        centerYSpring.SnapTo(crop.CenterY);
        cropDragging = false;
    }

    private void CropBack()
    {
        if (cropIndex == 0)
        {
            stage = ComposeStage.Pick;
            return;
        }

        SaveCurrentCrop();
        LoadCrop(cropIndex - 1);
    }

    private void CropAdvance()
    {
        SaveCurrentCrop();
        if (cropIndex < selected.Count - 1)
        {
            LoadCrop(cropIndex + 1);
            return;
        }

        previewIndex = 0;
        stage = ComposeStage.Caption;
    }

    private static void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
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

    private void DrawCrop(Rect area, AppSkin ui, in PhoneContext context)
    {
        var title = selected.Count > 1
            ? Loc.T(L.Common.PhotoStep, cropIndex + 1, selected.Count)
            : Loc.T(L.Velvet.MoveAndScale);
        AppHeader.Draw(context, title, CropBack);
        if (ui.HeaderAction(area, Loc.T(L.Common.Next), true))
        {
            CropAdvance();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var stageRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Max.Y - 96f * scale));
        var aspect = Aspect;
        var preview = ImageFit.CenteredRect(stageRect, aspect);
        var rounding = 18f * scale;
        var texture = Plugin.WallpaperImages.Get(CurrentPath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        var size = texture.Size;
        var zoom = zoomSpring.Step(targetZoom, CropSmoothTime, deltaSeconds);
        var centerX = centerXSpring.Step(targetCenterX, CropSmoothTime, deltaSeconds);
        var centerY = centerYSpring.Step(targetCenterY, CropSmoothTime, deltaSeconds);
        var crop = new WallpaperCrop(zoom, centerX, centerY).Clamped(size, aspect);
        var (uv0, uv1) = crop.ComputeUv(size, aspect);
        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        HandleCropGestures(preview, size, uv1 - uv0);
        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), Loc.T(L.Velvet.GestureHint),
            AppPalettes.Velvet.MutedInk, 0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale),
            new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomNormalized = (targetZoom - WallpaperCrop.MinZoom) / (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
        var updatedZoom = Scrubber.Draw(track, zoomNormalized, AppPalettes.Velvet.Accent, AppPalettes.Velvet.MutedInk,
            1f);
        targetZoom = WallpaperCrop.MinZoom + updatedZoom * (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
    }

    private void DrawCaption(Rect area, AppSkin ui, in PhoneContext context)
    {
        AppHeader.Draw(context, Title, () =>
        {
            stage = ComposeStage.Crop;
            LoadCrop(selected.Count - 1);
        });

        var busy = Posting;
        if (ui.HeaderAction(area, busy ? Loc.T(L.Velvet.Saving) : Loc.T(L.Velvet.Share), !busy))
        {
            Commit();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var chipsY = area.Max.Y - 78f * scale;
        var captionHeight = 34f * scale;
        var captionY = storyMode ? area.Max.Y - 20f * scale - captionHeight : chipsY - 20f * scale - captionHeight;
        var stripHeight = selected.Count > 1 ? 52f * scale : 0f;
        var statusHeight = status.Length > 0 ? 20f * scale : 0f;
        var previewRegion = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, captionY - 12f * scale - stripHeight - statusHeight));
        DrawCaptionPreview(previewRegion, scale);
        if (statusHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, captionY - 12f * scale), status, context.Theme.Danger,
                TextStyles.Footnote);
        }

        if (stripHeight > 0f)
        {
            var strip = new Rect(new Vector2(area.Min.X + 16f * scale, previewRegion.Max.Y + 6f * scale),
                new Vector2(area.Max.X - 16f * scale, previewRegion.Max.Y + stripHeight));
            DrawCaptionStrip(strip, scale);
        }

        var captionRect = new Rect(new Vector2(area.Min.X + 16f * scale, captionY),
            new Vector2(area.Max.X - 16f * scale, captionY + captionHeight));
        Squircle.Fill(drawList, captionRect.Min, captionRect.Max, 9f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        ImGui.SetCursorScreenPos(new Vector2(captionRect.Min.X + 12f * scale,
            captionRect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(captionRect.Width - 24f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Velvet.TitleInk))
        {
            MentionField.SingleLineWithHint("##velvetCaption", Loc.T(L.Velvet.CaptionHint), ref caption, 500,
                captionMentions);
        }

        var pickedMention = mentionPopup.Draw(captionMentions, area, context.Theme, images, lodestone);
        if (pickedMention >= 0)
        {
            captionMentions.Pick(pickedMention);
        }

        mentionPopup.Gate(captionMentions);

        if (storyMode)
        {
            return;
        }

        DrawChoiceRow(ui, area, chipsY, Loc.T(L.Velvet.VisibilityLabel),
            new[] { VelvetVisibility.Public, VelvetVisibility.Connections }, visibility, value => visibility = value,
            VelvetVisibility.Label);
    }

    private void DrawCaptionPreview(Rect region, float scale)
    {
        var aspect = Aspect;
        var preview = ImageFit.CenteredRect(region, aspect);
        if (preview.Width <= 0f)
        {
            return;
        }

        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var index = Math.Clamp(previewIndex, 0, Math.Max(0, selected.Count - 1));
        var texture = Plugin.WallpaperImages.Get(index < selected.Count ? selected[index] : string.Empty);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Velvet.MutedInk);
            return;
        }

        var crop = crops[index].Clamped(texture.Size, aspect);
        var (uv0, uv1) = crop.ComputeUv(texture.Size, aspect);
        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (UiInteract.HoverClick(preview.Min, preview.Max))
        {
            stage = ComposeStage.Crop;
            LoadCrop(index);
        }
    }

    private void DrawCaptionStrip(Rect strip, float scale)
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
            DrawLocalThumbnail(selected[index], min, max, scale);
            if (index == previewIndex)
            {
                drawList.AddRect(min, max, ImGui.GetColorU32(AppPalettes.Velvet.Accent), 8f * scale,
                    ImDrawFlags.RoundCornersAll, 2f * scale);
            }
            else
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 8f * scale);
            }

            if (UiInteract.HoverClick(min, max))
            {
                previewIndex = index;
            }
        }
    }

    private static float DrawChoiceRow(AppSkin ui, Rect area, float y, string label, int[] values, int selected,
        Action<int> onSelect, Func<int, string> labelFor)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(new Vector2(area.Min.X + 18f * scale, y), label, AppPalettes.Velvet.BodyInk, 0.9f, FontWeight.SemiBold);
        var cursorX = area.Min.X + 18f * scale;
        var chipY = y + 26f * scale;
        var chipHeight = 36f * scale;
        for (var index = 0; index < values.Length; index++)
        {
            var text = labelFor(values[index]);
            var width = Typography.Measure(text, 0.9f, FontWeight.Medium).X + 28f * scale;
            var rect = new Rect(new Vector2(cursorX, chipY), new Vector2(cursorX + width, chipY + chipHeight));
            if (ui.Chip(rect, text, selected == values[index]))
            {
                onSelect(values[index]);
            }

            cursorX += width + 10f * scale;
        }

        return chipY + chipHeight;
    }

    private void HandleCropGestures(Rect preview, Vector2 size, Vector2 visible)
    {
        var hovering = ImGui.IsMouseHoveringRect(preview.Min, preview.Max);
        if (hovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                targetZoom = Math.Clamp(targetZoom * (1f + wheel * 0.12f), WallpaperCrop.MinZoom,
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

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, Aspect);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    private void Commit()
    {
        if (selected.Count == 0 || Posting)
        {
            return;
        }

        status = string.Empty;
        if (storyMode)
        {
            stories.CreateStory(selected[0], crops[0], caption, ok => outcome = ok ? 1 : 2);
            return;
        }

        store.CreatePost(selected.ToArray(), crops.ToArray(), caption, Array.Empty<string>(), visibility,
            ok => outcome = ok ? 1 : 2);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Title).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPickedPath, task.Result);
            }
        });
    }
}
