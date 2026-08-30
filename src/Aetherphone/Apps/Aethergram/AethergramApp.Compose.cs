using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float AspectPickerReserve = 42f;
    private const float ComposeThumbSize = 64f;
    private const float ComposeThumbRounding = 6f;
    private const float ComposeRowHeight = 52f;
    private const float ComposeRowGlyph = 22f;
    private const float ComposeShareHeight = 46f;
    private const float ComposeAspectChipWidth = 92f;
    private const float ComposeAspectChipGap = 8f;
    private const float ComposeImportPillHeight = 32f;
    private const float ComposeMetaRowHeight = 26f;
    private const float ComposeToggleWidth = 48f;
    private const float ComposeToggleHeight = 28f;
    private const float ComposeCountBadgeRadius = 10f;

    private static readonly TextStyle ComposeActionStyle = TextStyles.Headline;
    private static readonly TextStyle ComposeRowStyle = TextStyles.Body;
    private static readonly TextStyle ComposeCounterStyle = TextStyles.Caption1;

    private float ComposeCropAspect => composeStoryMode
        ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight
        : composeAvatarMode
            ? PostAspects.SquareRatio
            : PostAspects.Ratio(composeSession.CurrentAspect);

    private float ComposeContainerAspect => composeStoryMode
        ? (float)StoryStore.StoryWidth / StoryStore.StoryHeight
        : composeAvatarMode
            ? PostAspects.SquareRatio
            : composeSession.GifSelected
                ? composeSession.GifAspect
                : PostAspects.Ratio(composeSession.ContainerAspect);

    private float ComposePreviewAspect => composeStoryMode || composeAvatarMode
        ? ComposeContainerAspect
        : composeSession.GifSelected
            ? composeSession.GifAspect
            : PostAspects.Ratio(composeSession.AspectAt(composeSession.ClampedPreviewIndex));

    private bool ComposeAllowsAspectChoice => !composeStoryMode && !composeAvatarMode;

    private bool ComposeCropAllowsReveal =>
        ComposeAllowsAspectChoice && PostAspects.RevealsWholeImage(composeSession.CurrentAspect);

    private bool ComposePreviewAllowsReveal => composeSession.GifSelected
        || (ComposeAllowsAspectChoice
            && PostAspects.RevealsWholeImage(composeSession.AspectAt(composeSession.ClampedPreviewIndex)));

    private string ComposeTitle => composeAvatarMode ? Loc.T(L.Aethergram.NewAvatar)
        : composeStoryMode ? Loc.T(L.Story.NewStory)
        : Loc.T(L.Aethergram.NewPost);

    private bool ComposePosting => composeStoryMode ? stories.Posting : store.Posting;

    private PhotoComposeStyle ComposeStyle => new(Accent, Ink.MutedInk, theme.SurfaceMuted,
        theme.Accent, theme.SurfaceMuted, true);

    private void StartStoryCompose()
    {
        StartCompose(false, true);
    }

    public void OnShare(in ShareItem item)
    {
        if (item.Kind != ShareKind.Photo)
        {
            return;
        }

        pendingSharedPhoto = item.LocalPath;
    }

    private void ConsumeSharedPhoto()
    {
        var path = pendingSharedPhoto;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        pendingSharedPhoto = null;
        if (!store.IsSignedIn)
        {
            return;
        }

        StartCompose(false);
        composeSession.TakePicked(path);
        composeSession.BeginCropSequence();
    }

    private void StartCompose(bool avatarMode, bool storyMode = false)
    {
        composeAvatarMode = avatarMode;
        composeStoryMode = storyMode;
        caption = string.Empty;
        composeStatus = string.Empty;
        composeSensitive = false;
        composeTags.Clear();
        composeTagMode = false;
        captionEmoji.Close();
        personPicker.Close();
        composeSession.Open(avatarMode || storyMode, !avatarMode && !storyMode);
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
                composeSensitive = false;
                store.RefreshFeed(SocialFeedScope.ForYou);
                store.RefreshFeed(SocialFeedScope.Following);
                feedScrollTopPending = true;
            }

            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = composeAvatarMode
                ? Loc.T(AvatarUpload.Message(store.AvatarFailure))
                : Loc.T(L.Account.CannotReach);
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

    private bool DrawComposeHeader(Rect area, string title, bool closeGlyph, Action backAction, string actionLabel,
        bool actionEnabled)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        bool leftTapped;
        if (closeGlyph)
        {
            var closeCenter = new Vector2(area.Min.X + (CellPadX + SocialChrome.HeaderIconRadius) * scale, rowCenterY);
            leftTapped = DrawHeaderIcon(drawList, closeCenter, PhoneIcons.X, Loc.T(L.Common.Cancel));
        }
        else
        {
            var chipRadius = SocialChrome.BackChipRadius * scale;
            var chipCenter = new Vector2(area.Min.X + 12f * scale + chipRadius, rowCenterY);
            leftTapped = SocialChrome.DrawBackChip(drawList, chipCenter, chipRadius, Ink);
        }

        if (leftTapped)
        {
            backAction();
        }

        var actionWidth = 0f;
        var clicked = false;
        if (actionLabel.Length > 0)
        {
            var size = Typography.Measure(actionLabel, ComposeActionStyle);
            actionWidth = size.X;
            var min = new Vector2(area.Max.X - CellPadX * scale - size.X - 8f * scale, area.Min.Y);
            var max = new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale);
            var hovered = actionEnabled && UiInteract.Hover(min, max);
            var ink = !actionEnabled ? Ink.FaintInk : hovered ? Ink.TitleInk : Ink.AccentLink;
            Typography.Draw(drawList, new Vector2(area.Max.X - CellPadX * scale - size.X, rowCenterY - size.Y * 0.5f),
                actionLabel, ink, ComposeActionStyle);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            clicked = UiInteract.Click(min, max, hovered);
        }

        var reserve = MathF.Max(actionWidth / scale + 8f, SocialChrome.HeaderIconRadius * 2f + 8f);
        SocialChrome.DrawScreenHeader(area, title, Ink, backAction, ScreenTitleStyle, reserve, string.Empty, false,
            true);
        return clicked;
    }

    private void DrawComposePick(Rect area)
    {
        var scale = UiScale.Current;
        var showNext = !composeAvatarMode && !composeStoryMode;
        if (DrawComposeHeader(area, ComposeTitle, true, back, showNext ? Loc.T(L.Common.Next) : string.Empty,
                composeSession.HasSelection))
        {
            composeSession.BeginCropSequence();
            if (composeSession.Stage == PhotoComposeStage.Caption)
            {
                captionFocus = true;
            }
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var importLabel = Loc.T(L.Aethergram.ImportFromPc);
        var importWidth = Typography.Measure(importLabel, PillStyle).X + 28f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + margin, top + 8f * scale),
            new Vector2(area.Min.X + margin + importWidth, top + 8f * scale + ComposeImportPillHeight * scale));
        if (DrawGrayPill(importRect, importLabel))
        {
            composeSession.LaunchImportDialog(Loc.T(L.Aethergram.NewPost));
        }

        var noticeHeight = 0f;
        if (composeSession.Notice.Length > 0)
        {
            noticeHeight = Typography.DrawWrappedLeft(new Vector2(area.Min.X + margin, importRect.Max.Y + 6f * scale),
                composeSession.Notice, Ink.MutedInk, TextStyles.Footnote, area.Width - margin * 2f) + 6f * scale;
        }

        var gridTop = importRect.Max.Y + 10f * scale + noticeHeight;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            if (composeSession.PickerCount == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Photos.NoPhotos), Ink.MutedInk);
                return;
            }

            composeSession.DrawPickGrid(gridRect, scale, ComposeStyle, true);
        }
    }

    private void DrawComposeCrop(Rect area)
    {
        var scale = UiScale.Current;
        var multi = !composeAvatarMode && composeSession.SelectedCount > 1;
        var title = multi
            ? Loc.T(L.Common.PhotoStep, composeSession.CropIndex + 1, composeSession.SelectedCount)
            : Loc.T(L.Aethergram.MoveAndScale);
        var actionLabel = composeAvatarMode
            ? (store.Posting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Use))
            : Loc.T(L.Aethergram.Next);
        if (DrawComposeHeader(area, title, false, CropBack, actionLabel, !store.Posting))
        {
            CropAdvance();
        }

        var reserve = ComposeAllowsAspectChoice ? AspectPickerReserve : 0f;
        composeSession.DrawCropCanvas(area, scale, ComposeCropAspect, ComposeStyle,
            Loc.T(L.Aethergram.GestureHint), reserve, ComposeCropAllowsReveal);
        if (ComposeAllowsAspectChoice)
        {
            DrawAspectPicker(area, scale);
        }
    }

    private void DrawAspectPicker(Rect area, float scale)
    {
        var count = PostAspects.All.Length;
        var chipWidth = ComposeAspectChipWidth * scale;
        var gap = ComposeAspectChipGap * scale;
        var total = count * chipWidth + (count - 1) * gap;
        var left = area.Center.X - total * 0.5f;
        var rowTop = area.Max.Y - (96f + AspectPickerReserve - 8f) * scale;
        var current = composeSession.CurrentAspect;
        for (var index = 0; index < count; index++)
        {
            var aspect = PostAspects.All[index];
            var rect = new Rect(new Vector2(left + index * (chipWidth + gap), rowTop),
                new Vector2(left + index * (chipWidth + gap) + chipWidth, rowTop + PillHeight * scale));
            var label = Loc.T(AspectLabels.For(aspect));
            var active = aspect == current;
            var clicked = active ? DrawAccentPill(rect, label) : DrawGrayPill(rect, label);
            if (clicked && !active)
            {
                composeSession.SetAspect(composeSession.CropIndex, aspect);
            }
        }
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
        personPicker.Gate();
        var scale = UiScale.Current;
        if (composeTagMode && !composeStoryMode)
        {
            DrawComposeTagging(area, scale);
            return;
        }

        var busy = ComposePosting;
        var shareLabel = busy ? Loc.T(L.Aethergram.Sharing) : Loc.T(L.Aethergram.Share);
        var submit = DrawComposeHeader(area, ComposeTitle, false, composeSession.CaptionBack, shareLabel, !busy);
        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var shareRect = new Rect(new Vector2(area.Min.X + margin, area.Max.Y - margin - ComposeShareHeight * scale),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var thumb = new Rect(new Vector2(area.Min.X + margin, top + 12f * scale),
            new Vector2(area.Min.X + margin + ComposeThumbSize * scale, top + 12f * scale + ComposeThumbSize * scale));
        DrawComposeThumb(thumb, scale);
        var fieldLeft = thumb.Max.X + 12f * scale;
        var fieldRight = area.Max.X - margin;
        DrawCaptionField(new Rect(new Vector2(fieldLeft, thumb.Min.Y), new Vector2(fieldRight, thumb.Max.Y)), area,
            scale);
        var metaTop = thumb.Max.Y + 6f * scale;
        DrawCaptionMetaRow(new Rect(new Vector2(fieldLeft, metaTop),
            new Vector2(fieldRight, metaTop + ComposeMetaRowHeight * scale)), scale);
        var rowTop = metaTop + ComposeMetaRowHeight * scale + 8f * scale;
        var drawList = ImGui.GetWindowDrawList();
        DrawHairline(drawList, area.Min.X, area.Max.X, rowTop);
        if (!composeStoryMode && !composeAvatarMode)
        {
            var tagRow = new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + ComposeRowHeight * scale));
            if (DrawComposeLinkRow(tagRow, PhoneIcons.UserPlus, Loc.T(L.PhotoTag.TagPeople), composeTags.Count))
            {
                composeTagMode = true;
            }

            var sensitiveRow = new Rect(new Vector2(area.Min.X, tagRow.Max.Y),
                new Vector2(area.Max.X, tagRow.Max.Y + ComposeRowHeight * scale));
            DrawComposeToggleRow(sensitiveRow, PhoneIcons.EyeOff, Loc.T(L.Moderation.MarkSensitive), ref composeSensitive);
        }

        if (composeStatus.Length > 0)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, shareRect.Min.Y - 28f * scale), composeStatus,
                Ink.Danger, TextStyles.Footnote, area.Width - margin * 2f);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            var panelBottom = shareRect.Min.Y - 10f * scale;
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, panelBottom - panelHeight),
                new Vector2(area.Max.X, panelBottom)), ui, ref caption, MaxCaptionLength);
        }

        var pickedPerson = personPicker.Draw(area, theme, images, lodestone);
        if (pickedPerson is not null)
        {
            PlaceComposeTag(pickedPerson);
        }

        if (DrawAccentPill(shareRect, shareLabel, !busy) || submit)
        {
            SubmitCompose();
        }
    }

    private void SubmitCompose()
    {
        if (composeStoryMode)
        {
            CommitStory();
            return;
        }

        CommitGram();
    }

    private void DrawComposeThumb(Rect thumb, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(thumb.Min, thumb.Max);
        composeSession.DrawLocalThumbnail(composeSession.FirstSelected, thumb.Min, thumb.Max, scale,
            theme.SurfaceMuted, hovered);
        if (composeSession.SelectedCount > 1)
        {
            var badgeRadius = ComposeCountBadgeRadius * scale;
            var badgeCenter = new Vector2(thumb.Max.X - badgeRadius - 4f * scale, thumb.Min.Y + badgeRadius + 4f * scale);
            drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Ink.Scrim), 20);
            Typography.DrawCentered(drawList, badgeCenter, composeSession.SelectedCount.ToString(Loc.Culture),
                Ink.White, TextStyles.Caption2);
        }

        HoverTooltip.Show(thumb, Loc.T(L.Aethergram.TapToAdjust), HoverLabelSide.Below);
        if (!UiInteract.Click(thumb.Min, thumb.Max, hovered) || composeSession.GifSelected)
        {
            return;
        }

        composeSession.LoadCropStage(composeSession.ClampedPreviewIndex);
    }

    private void DrawCaptionField(Rect field, Rect screen, float scale)
    {
        ImGui.SetCursorScreenPos(field.Min);
        if (captionFocus)
        {
            ImGui.SetKeyboardFocusHere();
            captionFocus = false;
        }

        var wrapWidth = field.Width - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, Ink.TitleInk))
        {
            SoftWrapField.Multiline("##gramCaption", ref caption, MaxCaptionLength, field.Size, wrapWidth,
                composeMentions);
        }

        var pickedMention = mentionPopup.Draw(composeMentions, screen, theme, images, lodestone);
        if (pickedMention >= 0)
        {
            composeMentions.Pick(pickedMention);
        }

        mentionPopup.Gate(composeMentions);
        if (caption.Length > 0)
        {
            return;
        }

        var hint = Typography.FitText(Loc.T(L.Aethergram.CaptionHint),
            field.Width - ImGui.GetStyle().FramePadding.X * 2f, TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), field.Min + ImGui.GetStyle().FramePadding, hint, Ink.MutedInk,
            TextStyles.Body);
    }

    private void DrawCaptionMetaRow(Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var emojiRadius = 12f * scale;
        var emojiCenter = new Vector2(row.Min.X + emojiRadius, row.Center.Y);
        captionEmoji.DrawToggle(ui, emojiCenter, emojiRadius, Accent, Ink.MutedInk, Loc.T(L.Common.Emoji));
        var counter = $"{caption.Length}/{MaxCaptionLength}";
        var counterSize = Typography.Measure(counter, ComposeCounterStyle);
        var counterInk = caption.Length >= MaxCaptionLength - 50 ? Ink.Danger : Ink.MutedInk;
        Typography.Draw(drawList, new Vector2(row.Max.X - counterSize.X, row.Center.Y - counterSize.Y * 0.5f), counter,
            counterInk, ComposeCounterStyle);
    }

    private bool DrawComposeLinkRow(Rect row, string glyph, string label, int count)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Ink.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var chevronCenter = new Vector2(row.Max.X - CellPadX * scale - 8f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Ink.MutedInk, 18f * scale);
        var trailingRight = chevronCenter.X - 14f * scale;
        if (count > 0)
        {
            var countText = count.ToString(Loc.Culture);
            var countSize = Typography.Measure(countText, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(trailingRight - countSize.X, row.Center.Y - countSize.Y * 0.5f),
                countText, Ink.MutedInk, TextStyles.Subheadline);
            trailingRight -= countSize.X + 10f * scale;
        }

        DrawComposeRowLabel(drawList, row, glyph, label, trailingRight);
        DrawHairline(drawList, row.Min.X, row.Max.X, row.Max.Y);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void DrawComposeToggleRow(Rect row, string glyph, string label, ref bool value)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var toggleMax = new Vector2(row.Max.X - CellPadX * scale, row.Center.Y + ComposeToggleHeight * 0.5f * scale);
        var toggleMin = new Vector2(toggleMax.X - ComposeToggleWidth * scale, row.Center.Y - ComposeToggleHeight * 0.5f * scale);
        DrawComposeRowLabel(drawList, row, glyph, label, toggleMin.X - 12f * scale);
        value = Toggle.Draw("aethergram.compose.sensitive", new Rect(toggleMin, toggleMax), value, theme);
        DrawHairline(drawList, row.Min.X, row.Max.X, row.Max.Y);
    }

    private static void DrawComposeRowLabel(ImDrawListPtr drawList, Rect row, string glyph, string label,
        float labelRight)
    {
        var scale = UiScale.Current;
        var glyphCenter = new Vector2(row.Min.X + CellPadX * scale + ComposeRowGlyph * 0.5f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, Ink.TitleInk, ComposeRowGlyph * scale);
        var labelLeft = glyphCenter.X + ComposeRowGlyph * 0.5f * scale + 12f * scale;
        var fitted = Typography.FitText(label, MathF.Max(1f, labelRight - labelLeft), ComposeRowStyle);
        var size = Typography.Measure(fitted, ComposeRowStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - size.Y * 0.5f), fitted, Ink.TitleInk,
            ComposeRowStyle);
    }

    private void DrawComposeTagging(Rect area, float scale)
    {
        if (DrawComposeHeader(area, Loc.T(L.PhotoTag.TagPeople), false, ExitTagMode, Loc.T(L.Aethergram.Done), true))
        {
            composeTagMode = false;
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var stripHeight = composeSession.SelectedCount > 1 ? 46f * scale : 0f;
        var stripGap = stripHeight > 0f ? 14f * scale : 0f;
        var hintHeight = Typography.LineHeight(TextStyles.Footnote) + 8f * scale;
        var previewRegion = new Rect(new Vector2(left, top + 12f * scale),
            new Vector2(right, area.Max.Y - margin - hintHeight - stripGap - stripHeight));
        var preview = ImageFit.CenteredRect(previewRegion, ComposeContainerAspect);
        DrawCaptionPreview(preview, scale);
        var stackY = preview.Max.Y;
        if (stripHeight > 0f)
        {
            composeSession.DrawCaptionStrip(new Rect(new Vector2(left, stackY + stripGap),
                new Vector2(right, stackY + stripGap + stripHeight)), scale, ComposeStyle);
            stackY += stripGap + stripHeight;
        }

        var hint = composeTags.Count >= MaxPhotoTags
            ? Loc.T(L.PhotoTag.TagLimit, MaxPhotoTags)
            : Loc.T(L.PhotoTag.TapToTag);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, stackY + 8f * scale), hint, Ink.MutedInk,
            TextStyles.Footnote, area.Width - margin * 2f);
        if (composeStatus.Length > 0)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, area.Max.Y - margin - hintHeight), composeStatus,
                Ink.Danger, TextStyles.Footnote, area.Width - margin * 2f);
        }

        var pickedPerson = personPicker.Draw(area, theme, images, lodestone);
        if (pickedPerson is not null)
        {
            PlaceComposeTag(pickedPerson);
        }
    }

    private void ExitTagMode()
    {
        composeTagMode = false;
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
            Squircle.Fill(drawList, min, max, pillHeight * 0.5f, ImGui.GetColorU32(Ink.Scrim));
            Typography.Draw(drawList, new Vector2(min.X + 8f * scale, min.Y + 4f * scale), text, Ink.White,
                TextStyles.FootnoteEmphasized);
            var closeCenter = new Vector2(max.X - 9f * scale, (min.Y + max.Y) * 0.5f);
            PhoneIcon.Draw(drawList, closeCenter, PhoneIcons.X, Palette.WithAlpha(Ink.White, 0.75f), 10f * scale);
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
        if (!composeSession.TryGetPreviewUv(ComposePreviewAspect, ComposePreviewAllowsReveal, out var texture,
            out var uv0, out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), Ink.MutedInk);
            return;
        }

        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        DrawComposeTags(drawList, preview, index, scale);
        if (!UiInteract.HoverClick(preview.Min, preview.Max))
        {
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

    private void CommitGram()
    {
        if (!composeSession.HasSelection || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.CreateGram(composeSession.SelectedArray(), composeSession.CropsArray(), composeSession.AspectsArray(),
            caption, ComposeTagInputs(), composeSensitive, ok => composeOutcome = ok ? 1 : 2);
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
