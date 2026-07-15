using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

// The compose / avatar creation flow (pick, crop, caption, share). Kept in its own partial so the
// main AethergramApp.cs stays focused on the feed and app orchestration.
internal sealed partial class AethergramApp
{
    private string ComposeCurrentPath =>
        composeIndex >= 0 && composeIndex < composeSelected.Count ? composeSelected[composeIndex] : string.Empty;

    private float ComposeAspect =>
        composeStoryMode ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight : 1f;

    private string ComposeTitle => composeAvatarMode ? Loc.T(L.Aethergram.NewAvatar)
        : composeStoryMode ? Loc.T(L.Story.NewStory)
        : Loc.T(L.Aethergram.NewPost);

    private bool ComposePosting => composeStoryMode ? stories.Posting : store.Posting;

    private void StartStoryCompose()
    {
        StartCompose(false, true);
    }

    private void StartCompose(bool avatarMode, bool storyMode = false)
    {
        composeAvatarMode = avatarMode;
        composeStoryMode = storyMode;
        composeStage = ComposeStage.Pick;
        composeSelected.Clear();
        composeCrops.Clear();
        composeIndex = 0;
        composePreviewIndex = 0;
        caption = string.Empty;
        composeStatus = string.Empty;
        pendingPickedPath = null;
        pickerPaths = library.List();
        router.Push(AethergramRoute.Compose);
    }

    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            composeStatus = string.Empty;
            if (!composeAvatarMode)
            {
                caption = string.Empty;
                sinceForYou = FeedRefreshSeconds;
                sinceFollowing = FeedRefreshSeconds;
            }

            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            TakePicked(picked);
        }

        switch (composeStage)
        {
            case ComposeStage.Crop:
                DrawComposeCrop(area);
                break;
            case ComposeStage.Caption:
                DrawComposeCaption(area);
                break;
            default:
                DrawComposePick(area);
                break;
        }
    }

    private void DrawComposePick(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, ComposeTitle, back);
        if (!composeAvatarMode && !composeStoryMode &&
            ui.HeaderAction(area, Loc.T(L.Common.Next), composeSelected.Count > 0))
        {
            BeginCropSequence();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Aethergram.ImportFromPc), true))
        {
            LaunchFileDialog();
        }

        var gridTop = importRect.Max.Y + 12f * scale;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Photos.NoPhotos), AppPalettes.Aethergram.MutedInk);
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

    private void DrawLocalThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
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

    private void DrawPickBadge(string path, Vector2 min, Vector2 max, float scale)
    {
        if (composeAvatarMode || composeStoryMode)
        {
            return;
        }

        var order = composeSelected.IndexOf(path);
        if (order < 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(Accent, 0.35f)), 10f * scale);
        var radius = 11f * scale;
        var center = new Vector2(max.X - radius - 6f * scale, min.Y + radius + 6f * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Accent), 20);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 20, 1.5f * scale);
        Typography.DrawCentered(drawList, center, (order + 1).ToString(Loc.Culture), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.FootnoteEmphasized);
    }

    private void TakePicked(string path)
    {
        if (composeAvatarMode || composeStoryMode)
        {
            composeSelected.Clear();
            composeSelected.Add(path);
            BeginCropSequence();
            return;
        }

        var existing = composeSelected.IndexOf(path);
        if (existing >= 0)
        {
            composeSelected.RemoveAt(existing);
            return;
        }

        if (composeSelected.Count >= PostMedia.MaxPhotos)
        {
            composeStatus = Loc.T(L.Common.PhotoLimit, PostMedia.MaxPhotos);
            return;
        }

        composeStatus = string.Empty;
        composeSelected.Add(path);
    }

    private void BeginCropSequence()
    {
        composeCrops.Clear();
        for (var index = 0; index < composeSelected.Count; index++)
        {
            composeCrops.Add(DefaultCrop);
        }

        composePreviewIndex = 0;
        composeStage = ComposeStage.Crop;
        LoadCrop(0);
    }

    private static WallpaperCrop DefaultCrop => new(1f, 0.5f, 0.5f);

    private void SaveCurrentCrop()
    {
        if (composeIndex >= 0 && composeIndex < composeCrops.Count)
        {
            composeCrops[composeIndex] = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        }
    }

    private void LoadCrop(int index)
    {
        if (index < 0 || index >= composeCrops.Count)
        {
            return;
        }

        composeIndex = index;
        var crop = composeCrops[index];
        targetZoom = crop.Zoom;
        targetCenterX = crop.CenterX;
        targetCenterY = crop.CenterY;
        zoomSpring.SnapTo(crop.Zoom);
        centerXSpring.SnapTo(crop.CenterX);
        centerYSpring.SnapTo(crop.CenterY);
        cropDragging = false;
    }

    private void DrawComposeCrop(Rect area)
    {
        var multi = !composeAvatarMode && composeSelected.Count > 1;
        var title = multi
            ? Loc.T(L.Common.PhotoStep, composeIndex + 1, composeSelected.Count)
            : Loc.T(L.Aethergram.MoveAndScale);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, CropBack);
        var canAdvance = !store.Posting;
        var actionLabel = composeAvatarMode
            ? (store.Posting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Use))
            : Loc.T(L.Aethergram.Next);
        if (ui.HeaderAction(area, actionLabel, canAdvance))
        {
            CropAdvance();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y + AppHeader.Height * scale;
        var stage = new Rect(new Vector2(area.Min.X + 16f * scale, top + 12f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Max.Y - 96f * scale));
        var aspect = ComposeAspect;
        var preview = ImageFit.CenteredRect(stage, aspect);
        var rounding = 18f * scale;
        var texture = Plugin.WallpaperImages.Get(ComposeCurrentPath);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
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
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        HandleCropGestures(preview, size, uv1 - uv0);
        Typography.DrawCentered(new Vector2(area.Center.X, area.Max.Y - 70f * scale), Loc.T(L.Aethergram.GestureHint),
            AppPalettes.Aethergram.MutedInk, 0.78f);
        var trackWidth = area.Width * 0.62f;
        var track = new Rect(new Vector2(area.Center.X - trackWidth * 0.5f, area.Max.Y - 48f * scale),
            new Vector2(area.Center.X + trackWidth * 0.5f, area.Max.Y - 44f * scale));
        var zoomNormalized = (targetZoom - WallpaperCrop.MinZoom) / (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
        var updated = Scrubber.Draw(track, zoomNormalized, theme.Accent, theme.SurfaceMuted, 1f);
        targetZoom = WallpaperCrop.MinZoom + updated * (WallpaperCrop.MaxZoom - WallpaperCrop.MinZoom);
    }

    private void CropBack()
    {
        if (composeAvatarMode || composeIndex == 0)
        {
            composeStage = ComposeStage.Pick;
            return;
        }

        SaveCurrentCrop();
        LoadCrop(composeIndex - 1);
    }

    private void CropAdvance()
    {
        if (composeAvatarMode)
        {
            CommitAvatar();
            return;
        }

        SaveCurrentCrop();
        if (composeIndex < composeSelected.Count - 1)
        {
            LoadCrop(composeIndex + 1);
            return;
        }

        composePreviewIndex = 0;
        composeStage = ComposeStage.Caption;
        captionFocus = true;
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

        var clamped = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY).Clamped(size, ComposeAspect);
        targetZoom = clamped.Zoom;
        targetCenterX = clamped.CenterX;
        targetCenterY = clamped.CenterY;
    }

    private void DrawComposeCaption(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, ComposeTitle, () => LoadCropStage(composeSelected.Count - 1));
        var scale = ImGuiHelpers.GlobalScale;
        var margin = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var shareHeight = 46f * scale;
        var shareRect = new Rect(new Vector2(area.Min.X + margin, area.Max.Y - margin - shareHeight),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var statusHeight = composeStatus.Length > 0 ? 24f * scale : 0f;
        var cardHeight = 124f * scale;
        var cardBottom = shareRect.Min.Y - 14f * scale - statusHeight;
        var cardRect = new Rect(new Vector2(area.Min.X + margin, cardBottom - cardHeight),
            new Vector2(area.Max.X - margin, cardBottom));
        var hintY = cardRect.Min.Y - 20f * scale;
        var stripHeight = composeSelected.Count > 1 ? 52f * scale : 0f;
        var previewRegion = new Rect(new Vector2(area.Min.X + margin, top + 14f * scale),
            new Vector2(area.Max.X - margin, hintY - 12f * scale - stripHeight));
        DrawCaptionPreview(previewRegion, scale);
        if (stripHeight > 0f)
        {
            var strip = new Rect(new Vector2(area.Min.X + margin, previewRegion.Max.Y + 6f * scale),
                new Vector2(area.Max.X - margin, previewRegion.Max.Y + 6f * scale + stripHeight - 6f * scale));
            DrawCaptionStrip(strip, scale);
        }

        Typography.DrawCentered(new Vector2(area.Center.X, hintY), Loc.T(L.Aethergram.TapToAdjust),
            AppPalettes.Aethergram.MutedInk, 0.75f);
        DrawCaptionCard(cardRect, scale);
        if (composeStatus.Length > 0)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, cardRect.Max.Y + 10f * scale), composeStatus,
                theme.Danger, 0.82f);
        }

        var busy = ComposePosting;
        if (DrawShareBar(shareRect, busy ? Loc.T(L.Aethergram.Sharing) : Loc.T(L.Aethergram.Share), !busy))
        {
            if (composeStoryMode)
            {
                CommitStory();
                return;
            }

            CommitGram();
        }
    }

    private void LoadCropStage(int index)
    {
        composeStage = ComposeStage.Crop;
        LoadCrop(Math.Clamp(index, 0, composeSelected.Count - 1));
    }

    private void DrawCaptionStrip(Rect strip, float scale)
    {
        var count = composeSelected.Count;
        var gap = 6f * scale;
        var side = MathF.Min(strip.Height, (strip.Width - gap * (count - 1)) / count);
        var span = side * count + gap * (count - 1);
        var startX = strip.Center.X - span * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var min = new Vector2(startX + index * (side + gap), strip.Min.Y);
            var max = min + new Vector2(side, side);
            DrawLocalThumbnail(composeSelected[index], min, max, scale);
            var drawList = ImGui.GetWindowDrawList();
            if (index == composePreviewIndex)
            {
                drawList.AddRect(min, max, ImGui.GetColorU32(Accent), 8f * scale, ImDrawFlags.RoundCornersAll,
                    2f * scale);
            }
            else
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 8f * scale);
            }

            if (UiInteract.HoverClick(min, max))
            {
                composePreviewIndex = index;
            }
        }
    }

    private void DrawCaptionPreview(Rect region, float scale)
    {
        var aspect = ComposeAspect;
        var preview = ImageFit.CenteredRect(region, aspect);
        if (preview.Width <= 0f)
        {
            return;
        }

        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, new Vector2(preview.Min.X - 2f * scale, preview.Min.Y + 4f * scale),
            new Vector2(preview.Max.X + 2f * scale, preview.Max.Y + 8f * scale), rounding + 2f * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)));
        var index = Math.Clamp(composePreviewIndex, 0, Math.Max(0, composeSelected.Count - 1));
        var texture = Plugin.WallpaperImages.Get(index < composeSelected.Count ? composeSelected[index] : string.Empty);
        if (texture is null)
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        var crop = composeCrops[index].Clamped(texture.Size, aspect);
        var (uv0, uv1) = crop.ComputeUv(texture.Size, aspect);
        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        if (UiInteract.HoverClick(preview.Min, preview.Max))
        {
            LoadCropStage(index);
        }
    }

    private void DrawCaptionCard(Rect card, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 14f * scale;
        Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(AppPalettes.Aethergram.FieldSurface));
        Material.EdgeSquircle(drawList, card.Min, card.Max, rounding, scale);
        var padding = 12f * scale;
        var inputTop = card.Min.Y + padding;
        if (store.Me is { } me)
        {
            var radius = 11f * scale;
            var avatarCenter = new Vector2(card.Min.X + padding + radius, card.Min.Y + padding + radius);
            DrawAvatar(avatarCenter, radius, me.Name, me.World, me.AvatarUrl, 0.7f, 24);
            var displayName = SocialIdentity.Name(me.DisplayName, me.Handle);
            Typography.Draw(new Vector2(avatarCenter.X + radius + 8f * scale, avatarCenter.Y - 8f * scale), displayName,
                theme.TextStrong, 0.88f, FontWeight.SemiBold);
            inputTop = avatarCenter.Y + radius + 6f * scale;
        }

        var counter = $"{caption.Length}/{MaxCaptionLength}";
        var counterSize = Typography.Measure(counter, 0.72f);
        var counterPos = new Vector2(card.Max.X - padding - counterSize.X,
            card.Max.Y - padding * 0.75f - counterSize.Y);
        var inputPos = new Vector2(card.Min.X + padding, inputTop);
        var inputSize = new Vector2(card.Width - padding * 2f, counterPos.Y - 4f * scale - inputTop);
        ImGui.SetCursorScreenPos(inputPos);
        if (captionFocus)
        {
            ImGui.SetKeyboardFocusHere();
            captionFocus = false;
        }

        var wrapWidth = inputSize.X - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            SoftWrapField.Multiline("##gramCaption", ref caption, MaxCaptionLength, inputSize, wrapWidth);
        }

        if (caption.Length == 0)
        {
            Typography.Draw(inputPos + ImGui.GetStyle().FramePadding, Loc.T(L.Aethergram.CaptionHint),
                AppPalettes.Aethergram.MutedInk, 1f);
        }

        var counterInk = caption.Length >= MaxCaptionLength - 50 ? theme.Danger : AppPalettes.Aethergram.MutedInk;
        Typography.Draw(counterPos, counter, counterInk, 0.72f);
    }

    private bool DrawShareBar(Rect rect, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = enabled
            ? (hovered ? Palette.Mix(Accent, theme.TextStrong, 0.12f) : Accent)
            : Palette.Mix(Accent, theme.AppBackground, 0.55f);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale, enabled ? 1f : 0.5f);
        var ink = new Vector4(1f, 1f, 1f, enabled ? 1f : 0.75f);
        var textSize = Typography.Measure(label, 1f, FontWeight.SemiBold);
        var iconWidth = 14f * scale;
        var iconGap = 8f * scale;
        var left = rect.Center.X - (iconWidth + iconGap + textSize.X) * 0.5f;
        AppSkin.Icon(new Vector2(left + iconWidth * 0.5f, rect.Center.Y), FontAwesomeIcon.PaperPlane.ToIconString(), ink,
            0.9f);
        Typography.Draw(new Vector2(left + iconWidth + iconGap, rect.Center.Y - textSize.Y * 0.5f), label, ink, 1f,
            FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void CommitGram()
    {
        if (composeSelected.Count == 0 || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.CreateGram(composeSelected.ToArray(), composeCrops.ToArray(), caption,
            ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitStory()
    {
        if (ComposeCurrentPath.Length == 0 || stories.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var crop = composeCrops.Count > 0 ? composeCrops[0] : new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        stories.CreateStory(composeSelected[0], crop, caption, ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitAvatar()
    {
        if (ComposeCurrentPath.Length == 0 || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var crop = new WallpaperCrop(targetZoom, targetCenterX, targetCenterY);
        store.UpdateAvatar(ComposeCurrentPath, crop, ok => composeOutcome = ok ? 1 : 2);
    }

    private void LaunchFileDialog()
    {
        _ = NativeFileDialog.OpenImageAsync(Loc.T(L.Aethergram.NewPost)).ContinueWith(task =>
        {
            if (task.Status == TaskStatus.RanToCompletion && !string.IsNullOrEmpty(task.Result))
            {
                Interlocked.Exchange(ref pendingPickedPath, task.Result);
            }
        });
    }
}
