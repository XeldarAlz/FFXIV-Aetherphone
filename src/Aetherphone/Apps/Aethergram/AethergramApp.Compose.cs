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
    private const float ComposeRowHeight = 52f;
    private const float ComposeRowGlyph = 22f;
    private const float ComposeRowLabelGap = 12f;
    private const float ComposeShareHeight = 46f;
    private const float ComposeCardRounding = 16f;
    private const float ComposeCardPad = 14f;
    private const float ComposeCardGap = 12f;
    private const float ComposeCaptionFieldHeight = 72f;
    private const float ComposeMetaGap = 6f;
    private const float ComposeMetaRowHeight = 34f;
    private const float ComposeEmojiRadius = 17f;
    private const float ComposePreviewGap = 14f;
    private const float ComposePreviewWidthFraction = 0.62f;
    private const float ComposePreviewRounding = 14f;
    private const float ComposeStripGap = 10f;
    private const float ComposePaneFraction = 0.5f;
    private const float ComposeGridGap = 6f;
    private const float ComposeNoticeHeight = 22f;
    private const float ComposeStatusGap = 8f;
    private const float ComposeToggleWidth = 48f;
    private const float ComposeToggleHeight = 28f;
    private const float ComposeTagHintGap = 8f;
    private const int ComposeCounterWarning = 50;
    private const int CircleSegments = 24;

    private static readonly TextStyle ComposeActionStyle = TextStyles.Headline;
    private static readonly TextStyle ComposeRowStyle = TextStyles.Body;
    private static readonly TextStyle ComposeCounterStyle = TextStyles.Caption1;

    private readonly Action composeEditBack;
    private readonly Action composeCaptionBack;
    private readonly Action composeExitTagMode;
    private string composeCounter = string.Empty;
    private int composeCounterLength = -1;

    private static float StoryAspect => (float)StoryStore.StoryWidth / StoryStore.StoryHeight;

    private float ComposeAspect => composeStoryMode
        ? StoryAspect
        : composeAvatarMode
            ? PostAspects.SquareRatio
            : composeSession.GifSelected
                ? composeSession.GifAspect
                : PostAspects.Ratio(composeSession.Aspect);

    private bool ComposeAllowsAspectChoice => !composeStoryMode && !composeAvatarMode;

    private bool ComposeAllowsReveal => composeSession.GifSelected
        || (ComposeAllowsAspectChoice && PostAspects.RevealsWholeImage(composeSession.Aspect));

    private string ComposeTitle => composeAvatarMode ? Loc.T(L.Aethergram.NewAvatar)
        : composeStoryMode ? Loc.T(L.Story.NewStory)
        : Loc.T(L.Aethergram.NewPost);

    private bool ComposePosting => composeStoryMode ? stories.Posting : store.Posting;

    private PhotoComposeStyle ComposeStyle => new(Accent, Ink.MutedInk, theme.SurfaceMuted, theme.SurfaceMuted, true);

    private PhotoEditPanelStyle ComposeEditStyle =>
        PhotoEditPanelStyle.ForComposer(ComposeStyle, Ink.TitleInk, theme.SurfaceMuted);

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
        composeSession.BeginEdit();
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
                store.RefreshFeed(SocialFeedScope.Latest);
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
            case PhotoComposeStage.Edit:
                DrawComposeEdit(area);
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
        var actionLabel = composeAvatarMode
            ? (store.Posting ? Loc.T(L.Aethergram.Saving) : Loc.T(L.Aethergram.Use))
            : Loc.T(L.Aethergram.Next);
        if (DrawComposeHeader(area, ComposeTitle, true, back, actionLabel,
                composeSession.HasSelection && !store.Posting))
        {
            PickAdvance();
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var paneHeight = MathF.Min(area.Width, (area.Max.Y - top) * ComposePaneFraction);
        var pane = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + paneHeight));
        composeSession.DrawPickPane(pane, scale, ComposeStyle, ComposeAspect, ComposeAllowsReveal,
            ComposeAllowsAspectChoice, !store.Posting);
        var gridTop = pane.Max.Y + ComposeGridGap * scale;
        if (composeSession.Notice.Length > 0)
        {
            var notice = Typography.FitText(composeSession.Notice, area.Width - CellPadX * 2f * scale,
                TextStyles.Footnote);
            Typography.DrawCentered(ImGui.GetWindowDrawList(),
                new Vector2(area.Center.X, gridTop + ComposeNoticeHeight * 0.5f * scale), notice, Ink.MutedInk,
                TextStyles.Footnote);
            gridTop += ComposeNoticeHeight * scale;
        }

        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            composeSession.DrawPickGrid(gridRect, scale, ComposeStyle, true, Loc.T(L.Aethergram.ImportFromPc),
                ComposeTitle);
        }
    }

    private void PickAdvance()
    {
        if (composeAvatarMode)
        {
            CommitAvatar();
            return;
        }

        composeSession.BeginEdit();
        if (composeSession.Stage == PhotoComposeStage.Caption)
        {
            captionFocus = true;
        }
    }

    private void DrawComposeEdit(Rect area)
    {
        var scale = UiScale.Current;
        if (DrawComposeHeader(area, Loc.T(L.Social.ComposeEditTitle), false, composeEditBack,
                Loc.T(L.Aethergram.Next), !store.Posting))
        {
            composeSession.EditAdvance();
            captionFocus = true;
        }

        composeSession.DrawEditCanvas(area, scale, ComposeAspect, ComposeStyle, ComposeAllowsReveal, !store.Posting);
        composeSession.DrawComposerFooter(area, scale, ComposeEditStyle, !store.Posting);
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

        DrawComposeHeader(area, ComposeTitle, false, composeCaptionBack, string.Empty, false);
        var busy = ComposePosting;
        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var shareRect = new Rect(new Vector2(left, area.Max.Y - margin - ComposeShareHeight * scale),
            new Vector2(right, area.Max.Y - margin));
        var statusHeight = composeStatus.Length > 0
            ? Typography.MeasureWrappedBlock(composeStatus, TextStyles.Footnote, right - left).Y
              + ComposeStatusGap * scale
            : 0f;
        var cardsBottom = shareRect.Min.Y - ComposeCardGap * scale - statusHeight;
        var showOptions = !composeStoryMode;
        var optionsHeight = showOptions ? ComposeRowHeight * 2f * scale : 0f;
        var optionsCard = new Rect(new Vector2(left, cardsBottom - optionsHeight), new Vector2(right, cardsBottom));
        var captionBottom = showOptions ? optionsCard.Min.Y - ComposeCardGap * scale : cardsBottom;
        var captionHeight = (ComposeCardPad * 2f + ComposeCaptionFieldHeight + ComposeMetaGap + ComposeMetaRowHeight)
                            * scale;
        var captionCard = new Rect(new Vector2(left, captionBottom - captionHeight), new Vector2(right, captionBottom));
        var stripHeight = composeSession.SelectedCount > 1 ? PhotoComposeSession.StripHeight * scale : 0f;
        var stripBlock = stripHeight > 0f ? stripHeight + ComposeStripGap * scale : 0f;
        var previewHalfWidth = area.Width * ComposePreviewWidthFraction * 0.5f;
        var previewRegion = new Rect(new Vector2(area.Center.X - previewHalfWidth, top + ComposePreviewGap * scale),
            new Vector2(area.Center.X + previewHalfWidth, captionCard.Min.Y - ComposePreviewGap * scale - stripBlock));
        var preview = ImageFit.CenteredRect(previewRegion, ComposeAspect);
        if (DrawCaptionPreview(preview, scale, Loc.T(L.Social.ComposeTapToEdit)) && !composeSession.GifSelected)
        {
            composeSession.OpenEdit(composeSession.ClampedPreviewIndex);
            return;
        }

        if (stripHeight > 0f)
        {
            var stripTop = preview.Max.Y + ComposeStripGap * scale;
            var tapped = composeSession.DrawPhotoStrip(
                new Rect(new Vector2(left, stripTop), new Vector2(right, stripTop + stripHeight)), scale, ComposeStyle,
                composeSession.ClampedPreviewIndex);
            if (tapped >= 0)
            {
                composeSession.PreviewIndex = tapped;
            }
        }

        DrawCaptionCard(captionCard, area, scale, "##gramCaption", ref caption, composeMentions);
        if (showOptions && DrawComposeOptionsCard(optionsCard, scale, ref composeSensitive))
        {
            composeTagMode = true;
        }

        if (statusHeight > 0f)
        {
            Typography.DrawWrappedCentered(new Vector2(area.Center.X, shareRect.Min.Y - statusHeight), composeStatus,
                Ink.Danger, TextStyles.Footnote, right - left);
        }

        var panelHeight = captionEmoji.PanelHeight(scale);
        if (panelHeight > 0f)
        {
            var panelBottom = shareRect.Min.Y - ComposeCardGap * scale;
            captionEmoji.DrawPanel(new Rect(new Vector2(area.Min.X, panelBottom - panelHeight),
                new Vector2(area.Max.X, panelBottom)), ui, ref caption, MaxCaptionLength);
        }

        var pickedPerson = personPicker.Draw(area, theme, images, lodestone);
        if (pickedPerson is not null)
        {
            PlaceComposeTag(pickedPerson);
        }

        var shareLabel = busy ? Loc.T(L.Aethergram.Sharing) : Loc.T(L.Aethergram.Share);
        if (DrawAccentPill(shareRect, shareLabel, !busy))
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

    private void DrawCaptionCard(Rect card, Rect screen, float scale, string fieldId, ref string text,
        MentionAutocomplete mentions)
    {
        ui.Card(ImGui.GetWindowDrawList(), card.Min, card.Max, ComposeCardRounding * scale, true);
        var pad = ComposeCardPad * scale;
        var field = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Max.X - pad, card.Min.Y + pad + ComposeCaptionFieldHeight * scale));
        DrawCaptionField(field, screen, scale, fieldId, ref text, mentions);
        var metaTop = field.Max.Y + ComposeMetaGap * scale;
        DrawCaptionMetaRow(new Rect(new Vector2(field.Min.X, metaTop),
            new Vector2(field.Max.X, metaTop + ComposeMetaRowHeight * scale)), scale, text.Length);
    }

    private void DrawCaptionField(Rect field, Rect screen, float scale, string fieldId, ref string text,
        MentionAutocomplete mentions)
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
            SoftWrapField.Multiline(fieldId, ref text, MaxCaptionLength, field.Size, wrapWidth, mentions);
        }

        var pickedMention = mentionPopup.Draw(mentions, screen, theme, images, lodestone);
        if (pickedMention >= 0)
        {
            mentions.Pick(pickedMention);
        }

        mentionPopup.Gate(mentions);
        if (text.Length > 0)
        {
            return;
        }

        var hint = Typography.FitText(Loc.T(L.Aethergram.CaptionHint),
            field.Width - ImGui.GetStyle().FramePadding.X * 2f, TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), field.Min + ImGui.GetStyle().FramePadding, hint, Ink.MutedInk,
            TextStyles.Body);
    }

    private void DrawCaptionMetaRow(Rect row, float scale, int textLength)
    {
        var drawList = ImGui.GetWindowDrawList();
        var emojiRadius = ComposeEmojiRadius * scale;
        var emojiCenter = new Vector2(row.Min.X + emojiRadius, row.Center.Y);
        var emojiExtent = new Vector2(emojiRadius, emojiRadius);
        var emojiLit = captionEmoji.Open || UiInteract.Hover(emojiCenter - emojiExtent, emojiCenter + emojiExtent);
        drawList.AddCircleFilled(emojiCenter, emojiRadius, ImGui.GetColorU32(emojiLit ? Ink.ButtonHover : Ink.ButtonFill),
            CircleSegments);
        captionEmoji.DrawToggle(ui, emojiCenter, emojiRadius, Accent, Ink.TitleInk, Loc.T(L.Common.Emoji));
        SyncComposeCounter(textLength);
        var counterSize = Typography.Measure(composeCounter, ComposeCounterStyle);
        var counterInk = textLength >= MaxCaptionLength - ComposeCounterWarning ? Ink.Danger : Ink.MutedInk;
        Typography.Draw(drawList, new Vector2(row.Max.X - counterSize.X, row.Center.Y - counterSize.Y * 0.5f),
            composeCounter, counterInk, ComposeCounterStyle);
    }

    private void SyncComposeCounter(int textLength)
    {
        if (composeCounterLength == textLength)
        {
            return;
        }

        composeCounterLength = textLength;
        composeCounter = string.Concat(composeCounterLength.ToString(Loc.Culture), "/",
            MaxCaptionLength.ToString(Loc.Culture));
    }

    private bool DrawComposeOptionsCard(Rect card, float scale, ref bool sensitive)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = ComposeCardRounding * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        var tagRow = new Rect(card.Min, new Vector2(card.Max.X, card.Min.Y + ComposeRowHeight * scale));
        var tagRowTapped = DrawComposeLinkRow(tagRow, PhoneIcons.UserPlus, Loc.T(L.PhotoTag.TagPeople),
            composeTags.Count, rounding);
        DrawHairline(drawList, ComposeRowLabelLeft(tagRow, scale), card.Max.X - ComposeCardPad * scale, tagRow.Max.Y);
        var sensitiveRow = new Rect(new Vector2(card.Min.X, tagRow.Max.Y), card.Max);
        DrawComposeToggleRow(sensitiveRow, PhoneIcons.EyeOff, Loc.T(L.Moderation.MarkSensitive), ref sensitive);
        return tagRowTapped;
    }

    private bool DrawComposeLinkRow(Rect row, string glyph, string label, int count, float rounding)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(Ink.HoverTint), rounding,
                ImDrawFlags.RoundCornersTop);
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
    }

    private static float ComposeRowLabelLeft(Rect row, float scale) =>
        row.Min.X + (CellPadX + ComposeRowGlyph + ComposeRowLabelGap) * scale;

    private static void DrawComposeRowLabel(ImDrawListPtr drawList, Rect row, string glyph, string label,
        float labelRight)
    {
        var scale = UiScale.Current;
        var glyphCenter = new Vector2(row.Min.X + CellPadX * scale + ComposeRowGlyph * 0.5f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, glyphCenter, glyph, Ink.TitleInk, ComposeRowGlyph * scale);
        var labelLeft = ComposeRowLabelLeft(row, scale);
        var fitted = Typography.FitText(label, MathF.Max(1f, labelRight - labelLeft), ComposeRowStyle);
        var size = Typography.Measure(fitted, ComposeRowStyle);
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - size.Y * 0.5f), fitted, Ink.TitleInk,
            ComposeRowStyle);
    }

    private void DrawComposeTagging(Rect area, float scale)
    {
        if (DrawComposeHeader(area, Loc.T(L.PhotoTag.TagPeople), false, composeExitTagMode, Loc.T(L.Aethergram.Done),
                true))
        {
            composeTagMode = false;
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var margin = CellPadX * scale;
        var left = area.Min.X + margin;
        var right = area.Max.X - margin;
        var stripHeight = composeSession.SelectedCount > 1 ? PhotoComposeSession.StripHeight * scale : 0f;
        var stripGap = stripHeight > 0f ? ComposeStripGap * scale : 0f;
        var hintHeight = Typography.LineHeight(TextStyles.Footnote) + ComposeTagHintGap * scale;
        var previewRegion = new Rect(new Vector2(left, top + ComposePreviewGap * scale),
            new Vector2(right, area.Max.Y - margin - hintHeight - stripGap - stripHeight));
        var preview = ImageFit.CenteredRect(previewRegion, ComposeAspect);
        if (DrawCaptionPreview(preview, scale, string.Empty))
        {
            PlaceTagAt(preview, composeSession.ClampedPreviewIndex);
        }

        var stackY = preview.Max.Y;
        if (stripHeight > 0f)
        {
            var tapped = composeSession.DrawPhotoStrip(new Rect(new Vector2(left, stackY + stripGap),
                new Vector2(right, stackY + stripGap + stripHeight)), scale, ComposeStyle,
                composeSession.ClampedPreviewIndex);
            if (tapped >= 0)
            {
                composeSession.PreviewIndex = tapped;
            }

            stackY += stripGap + stripHeight;
        }

        DrawTaggingFooter(area, stackY, margin, hintHeight, scale);
    }

    private void DrawTaggingFooter(Rect area, float stackY, float margin, float hintHeight, float scale)
    {
        var hint = composeTags.Count >= MaxPhotoTags
            ? Loc.T(L.PhotoTag.TagLimit, MaxPhotoTags)
            : Loc.T(L.PhotoTag.TapToTag);
        Typography.DrawWrappedCentered(new Vector2(area.Center.X, stackY + ComposeTagHintGap * scale), hint,
            Ink.MutedInk, TextStyles.Footnote, area.Width - margin * 2f);
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

    private void PlaceTagAt(Rect preview, int photoIndex)
    {
        if (composeTags.Count >= MaxPhotoTags)
        {
            composeStatus = Loc.T(L.PhotoTag.TagLimit, MaxPhotoTags);
            return;
        }

        composeTagPoint = PhotoTagGeometry.ToNormalized(preview, ImGui.GetMousePos());
        composeTagPhotoIndex = photoIndex;
        personPicker.Open();
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

    private bool DrawComposeTags(ImDrawListPtr drawList, Rect preview, int photoIndex, float scale)
    {
        var removed = false;
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
                removed = true;
            }
        }

        return removed;
    }

    private bool DrawCaptionPreview(Rect preview, float scale, string tooltip)
    {
        if (preview.Width <= 0f || preview.Height <= 0f)
        {
            return false;
        }

        var rounding = ComposePreviewRounding * scale;
        var drawList = ImGui.GetWindowDrawList();
        Elevation.Card(drawList, preview.Min, preview.Max, rounding, scale);
        if (!composeSession.TryGetPreviewUv(ComposeAspect, ComposeAllowsReveal, out var texture, out var uv0,
                out var uv1))
        {
            Squircle.Fill(drawList, preview.Min, preview.Max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            Typography.DrawCentered(preview.Center, Loc.T(L.Common.Loading), Ink.MutedInk);
            return false;
        }

        ImageFit.DrawLetterboxed(drawList, texture, preview, uv0, uv1, rounding);
        Material.EdgeSquircle(drawList, preview.Min, preview.Max, rounding, scale);
        if (DrawComposeTags(drawList, preview, composeSession.ClampedPreviewIndex, scale))
        {
            return false;
        }

        var hovered = UiInteract.Hover(preview.Min, preview.Max);
        if (tooltip.Length > 0)
        {
            HoverTooltip.Show(preview, tooltip, HoverLabelSide.Below);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(preview.Min, preview.Max, hovered);
    }

    private void CommitGram()
    {
        if (!composeSession.HasSelection || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        store.CreateGram(composeSession.SelectedArray(), composeSession.CropsArray(), composeSession.AspectsArray(),
            composeSession.EditsArray(),
            caption, ComposeTagInputs(), composeSensitive, ok => composeOutcome = ok ? 1 : 2);
    }

    private void CommitStory()
    {
        if (composeSession.CurrentPath.Length == 0 || stories.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        stories.CreateStory(composeSession.FirstSelected, composeSession.CropAt(0), composeSession.EditAt(0), caption,
            ok => composeOutcome = ok ? 1 : 2);
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
