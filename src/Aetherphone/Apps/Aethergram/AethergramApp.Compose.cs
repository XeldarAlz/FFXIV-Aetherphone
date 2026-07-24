using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
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
    private float ComposeAspect =>
        composeStoryMode ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight : 1f;

    private string ComposeTitle => composeAvatarMode ? Loc.T(L.Aethergram.NewAvatar)
        : composeStoryMode ? Loc.T(L.Story.NewStory)
        : Loc.T(L.Aethergram.NewPost);

    private bool ComposePosting => composeStoryMode ? stories.Posting : store.Posting;

    private PhotoComposeStyle ComposeStyle => new(Accent, AppPalettes.Aethergram.MutedInk, theme.SurfaceMuted,
        theme.Accent, theme.SurfaceMuted, true);

    private void StartStoryCompose()
    {
        StartCompose(false, true);
    }

    private void StartCompose(bool avatarMode, bool storyMode = false)
    {
        composeAvatarMode = avatarMode;
        composeStoryMode = storyMode;
        caption = string.Empty;
        composeStatus = string.Empty;
        composeTags.Clear();
        composeTagMode = false;
        captionEmoji.Close();
        personPicker.Close();
        composeSession.Open(avatarMode || storyMode);
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
                profile.ForceRefreshSoon();
            }

            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
        }

        composeSession.ConsumePendingImport();
        switch (composeSession.Stage)
        {
            case PhotoComposeStage.Crop:
                DrawComposeCrop(area);
                break;
            case PhotoComposeStage.Caption:
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
            ui.HeaderAction(area, Loc.T(L.Common.Next), composeSession.HasSelection))
        {
            composeSession.BeginCropSequence();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Aethergram.ImportFromPc), true))
        {
            composeSession.LaunchImportDialog(Loc.T(L.Aethergram.NewPost));
        }

        var noticeHeight = composeSession.Notice.Length > 0 ? 20f * scale : 0f;
        if (noticeHeight > 0f)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, importRect.Max.Y + 8f * scale), composeSession.Notice,
                AppPalettes.Aethergram.MutedInk, TextStyles.Footnote);
        }

        var gridTop = importRect.Max.Y + 12f * scale + noticeHeight;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (composeSession.PickerCount == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Photos.NoPhotos), AppPalettes.Aethergram.MutedInk);
                return;
            }

            composeSession.DrawPickGrid(gridRect, scale, ComposeStyle, true);
        }
    }

    private void DrawComposeCrop(Rect area)
    {
        var multi = !composeAvatarMode && composeSession.SelectedCount > 1;
        var title = multi
            ? Loc.T(L.Common.PhotoStep, composeSession.CropIndex + 1, composeSession.SelectedCount)
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

        composeSession.DrawCropCanvas(area, ImGuiHelpers.GlobalScale, ComposeAspect, ComposeStyle,
            Loc.T(L.Aethergram.GestureHint));
    }

    private void CropBack()
    {
        if (composeAvatarMode)
        {
            composeSession.Stage = PhotoComposeStage.Pick;
            return;
        }

        composeSession.CropBack();
    }

    private void CropAdvance()
    {
        if (composeAvatarMode)
        {
            CommitAvatar();
            return;
        }

        if (composeSession.CropAdvance())
        {
            captionFocus = true;
        }
    }

    private void DrawComposeCaption(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, ComposeTitle, () => composeSession.LoadCropStage(composeSession.SelectedCount - 1));
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
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var stripHeight = composeSession.SelectedCount > 1 ? 46f * scale : 0f;
        var stripGap = stripHeight > 0f ? 18f * scale : 0f;
        var tagBarHeight = TagModeBarHeight * scale;
        var tagBarGap = 18f * scale;
        var reserved = stripGap + stripHeight + tagBarGap + tagBarHeight;
        var previewRegion = new Rect(new Vector2(left, top + 14f * scale),
            new Vector2(right, cardRect.Min.Y - 18f * scale - reserved));
        var preview = ImageFit.CenteredRect(previewRegion, ComposeAspect);
        DrawCaptionPreview(preview, scale);
        var stackY = preview.Max.Y;
        if (stripHeight > 0f)
        {
            composeSession.DrawCaptionStrip(new Rect(new Vector2(left, stackY + stripGap),
                new Vector2(right, stackY + stripGap + stripHeight)), scale, ComposeStyle);
            stackY += stripGap + stripHeight;
        }

        DrawTagModeBar(new Rect(new Vector2(left, stackY + tagBarGap),
            new Vector2(right, stackY + tagBarGap + tagBarHeight)), scale);
        DrawCaptionCard(cardRect, area, scale);
        if (composeStatus.Length > 0)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, cardRect.Max.Y + 10f * scale), composeStatus,
                theme.Danger, 0.82f);
        }

        var pickedPerson = personPicker.Draw(area, theme, images, lodestone);
        if (pickedPerson is not null)
        {
            PlaceComposeTag(pickedPerson);
        }

        personPicker.Gate();

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

    private void DrawTagModeBar(Rect bar, float scale)
    {
        if (composeStoryMode)
        {
            Typography.DrawCentered(bar.Center, Loc.T(L.Aethergram.TapToAdjust), AppPalettes.Aethergram.MutedInk,
                TextStyles.Footnote);
            return;
        }

        var pillPadding = 28f * scale;
        var pillLabel = Typography.FitText(Loc.T(L.PhotoTag.TagPeople), bar.Width * 0.5f - pillPadding,
            TextStyles.FootnoteEmphasized);
        var pillWidth = Typography.Measure(pillLabel, TextStyles.FootnoteEmphasized).X + pillPadding;
        var pillMin = new Vector2(bar.Max.X - pillWidth, bar.Min.Y);
        var pillMax = new Vector2(bar.Max.X, bar.Max.Y);
        var drawList = ImGui.GetWindowDrawList();
        var active = composeTagMode;
        var hovered = UiInteract.Hover(pillMin, pillMax);
        var fill = active ? Accent : AppPalettes.Aethergram.FieldSurface;
        Squircle.Fill(drawList, pillMin, pillMax, bar.Height * 0.5f,
            ImGui.GetColorU32(hovered ? Palette.Mix(fill, theme.TextStrong, 0.12f) : fill));
        Typography.DrawCentered(drawList, new Vector2((pillMin.X + pillMax.X) * 0.5f, bar.Center.Y), pillLabel,
            active ? new Vector4(1f, 1f, 1f, 1f) : AppPalettes.Aethergram.MutedInk, TextStyles.FootnoteEmphasized);
        if (UiInteract.HoverClick(pillMin, pillMax))
        {
            composeTagMode = !composeTagMode;
        }

        var hintRight = pillMin.X - 12f * scale;
        var hint = Typography.FitText(composeTagMode ? Loc.T(L.PhotoTag.TapToTag) : Loc.T(L.Aethergram.TapToAdjust),
            hintRight - bar.Min.X, TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2((bar.Min.X + hintRight) * 0.5f, bar.Center.Y), hint,
            AppPalettes.Aethergram.MutedInk, TextStyles.Footnote);
    }

    private void PlaceComposeTag(MentionSuggestDto person)
    {
        for (var index = 0; index < composeTags.Count; index++)
        {
            if (string.Equals(composeTags[index].UserId, person.UserId, StringComparison.Ordinal))
            {
                composeTags[index] = new PhotoTagDto(string.Empty, person.UserId, person.Handle, person.DisplayName,
                    composeTagPhotoIndex, composeTagPoint.X, composeTagPoint.Y, 1);
                return;
            }
        }

        composeTags.Add(new PhotoTagDto(string.Empty, person.UserId, person.Handle, person.DisplayName,
            composeTagPhotoIndex, composeTagPoint.X, composeTagPoint.Y, 1));
    }

    private PhotoTagInput[]? ComposeTagInputs()
    {
        if (composeTags.Count == 0)
        {
            return null;
        }

        var inputs = new PhotoTagInput[composeTags.Count];
        for (var index = 0; index < composeTags.Count; index++)
        {
            var tag = composeTags[index];
            inputs[index] = new PhotoTagInput(tag.UserId, tag.PhotoIndex, tag.X, tag.Y);
        }

        return inputs;
    }

    private void DrawComposeTags(ImDrawListPtr drawList, Rect preview, int photoIndex, float scale)
    {
        for (var index = composeTags.Count - 1; index >= 0; index--)
        {
            var tag = composeTags[index];
            if (tag.PhotoIndex != photoIndex)
            {
                continue;
            }

            var anchor = PhotoTagGeometry.ToScreen(preview, tag.X, tag.Y);
            var label = SocialIdentity.Name(tag.DisplayName, tag.Handle);
            var text = Typography.FitText(label, preview.Width * 0.5f, TextStyles.FootnoteEmphasized);
            var textSize = Typography.Measure(text, TextStyles.FootnoteEmphasized);
            var pillWidth = textSize.X + 26f * scale;
            var pillHeight = textSize.Y + 8f * scale;
            var left = Math.Clamp(anchor.X - pillWidth * 0.5f, preview.Min.X + 4f * scale,
                MathF.Max(preview.Min.X + 4f * scale, preview.Max.X - 4f * scale - pillWidth));
            var top = Math.Clamp(anchor.Y + 6f * scale, preview.Min.Y, preview.Max.Y - pillHeight);
            var min = new Vector2(left, top);
            var max = new Vector2(left + pillWidth, top + pillHeight);
            drawList.AddCircleFilled(anchor, 4f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), 12);
            Squircle.Fill(drawList, min, max, pillHeight * 0.5f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.62f)));
            Typography.Draw(drawList, new Vector2(min.X + 8f * scale, min.Y + 4f * scale), text,
                new Vector4(1f, 1f, 1f, 1f), TextStyles.FootnoteEmphasized);
            var closeCenter = new Vector2(max.X - 9f * scale, (min.Y + max.Y) * 0.5f);
            AppSkin.Icon(closeCenter, FontAwesomeIcon.Times.ToIconString(), new Vector4(1f, 1f, 1f, 0.75f), 0.6f);
            if (UiInteract.HoverClick(closeCenter - new Vector2(8f * scale, 8f * scale),
                    closeCenter + new Vector2(8f * scale, 8f * scale)))
            {
                composeTags.RemoveAt(index);
            }
        }
    }

    private void DrawCaptionPreview(Rect preview, float scale)
    {
        if (preview.Width <= 0f)
        {
            return;
        }

        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, new Vector2(preview.Min.X - 2f * scale, preview.Min.Y + 4f * scale),
            new Vector2(preview.Max.X + 2f * scale, preview.Max.Y + 8f * scale), rounding + 2f * scale,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)));
        var index = composeSession.ClampedPreviewIndex;
        if (!composeSession.TryGetPreviewUv(ComposeAspect, out var texture, out var uv0, out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        drawList.AddImageRounded(texture.Handle, preview.Min, preview.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        if (composeTagMode && !composeStoryMode)
        {
            DrawComposeTags(drawList, preview, index, scale);
        }

        if (!UiInteract.HoverClick(preview.Min, preview.Max))
        {
            return;
        }

        if (!composeTagMode || composeStoryMode)
        {
            composeSession.LoadCropStage(index);
            return;
        }

        if (composeTags.Count >= MaxPhotoTags)
        {
            composeStatus = Loc.T(L.PhotoTag.TagLimit, MaxPhotoTags);
            return;
        }

        composeTagPoint = PhotoTagGeometry.ToNormalized(preview, ImGui.GetMousePos());
        composeTagPhotoIndex = index;
        personPicker.Open();
    }

    private void DrawCaptionCard(Rect card, Rect screen, float scale)
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
            var nameStyle = new TextStyle(0.88f, FontWeight.SemiBold);
            var nameLeft = avatarCenter.X + radius + 8f * scale;
            var nameTop = avatarCenter.Y - 8f * scale;
            var nameMaxWidth = MathF.Max(1f, card.Max.X - padding - nameLeft);
            var nameHeight = Typography.Measure(displayName, nameStyle).Y;
            var nameHovering = ImGui.IsMouseHoveringRect(new Vector2(nameLeft, nameTop),
                new Vector2(nameLeft + nameMaxWidth, nameTop + nameHeight));
            Marquee.DrawLeft("aethergram.compose.author." + me.Handle, displayName, nameLeft, nameTop, nameMaxWidth,
                nameStyle, theme.TextStrong, nameHovering);
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
            SoftWrapField.Multiline("##gramCaption", ref caption, MaxCaptionLength, inputSize, wrapWidth,
                composeMentions);
        }

        var pickedMention = mentionPopup.Draw(composeMentions, screen, theme, images, lodestone);
        if (pickedMention >= 0)
        {
            composeMentions.Pick(pickedMention);
        }

        mentionPopup.Gate(composeMentions);

        if (caption.Length == 0)
        {
            var hint = Typography.FitText(Loc.T(L.Aethergram.CaptionHint),
                inputSize.X - ImGui.GetStyle().FramePadding.X * 2f, 1f, FontWeight.Regular);
            Typography.Draw(inputPos + ImGui.GetStyle().FramePadding, hint,
                AppPalettes.Aethergram.MutedInk, 1f);
        }

        var counterInk = caption.Length >= MaxCaptionLength - 50 ? theme.Danger : AppPalettes.Aethergram.MutedInk;
        Typography.Draw(counterPos, counter, counterInk, 0.72f);

        var emojiRadius = 13f * scale;
        var emojiCenter = new Vector2(card.Min.X + padding + emojiRadius, counterPos.Y + counterSize.Y * 0.5f);
        captionEmoji.DrawToggle(ui, emojiCenter, emojiRadius, Accent, AppPalettes.Aethergram.MutedInk,
            Loc.T(L.Common.Emoji));
        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            captionEmoji.DrawPanel(new Rect(new Vector2(screen.Min.X, card.Min.Y - panelHeight),
                new Vector2(screen.Max.X, card.Min.Y)), ui, ref caption, MaxCaptionLength);
        }
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
        if (!composeSession.HasSelection || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.CreateGram(composeSession.SelectedArray(), composeSession.CropsArray(), caption, ComposeTagInputs(),
            ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitStory()
    {
        if (composeSession.CurrentPath.Length == 0 || stories.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var crop = composeSession.CropCount > 0 ? composeSession.CropAt(0) : composeSession.CurrentTargetCrop;
        stories.CreateStory(composeSession.FirstSelected, crop, caption, ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitAvatar()
    {
        if (composeSession.CurrentPath.Length == 0 || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.UpdateAvatar(composeSession.CurrentPath, composeSession.CurrentTargetCrop,
            ok => composeOutcome = ok ? 1 : 2);
    }
}
