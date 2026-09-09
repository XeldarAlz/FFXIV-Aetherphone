using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private const float ComposeToolbarHeight = 52f;
    private const float ComposeTileMax = 104f;
    private const float ComposeTileRounding = 14f;
    private const float ComposeRingRadius = 9f;
    private const float ComposeRingStroke = 2.6f;
    private const int ComposeWarnRemaining = 40;

    private static readonly TextStyle ComposeTitleStyle = new(1.1f, FontWeight.Bold);
    private static readonly TextStyle ComposeInputStyle = new(1.17f, FontWeight.Regular);
    private static readonly TextStyle ComposeActionStyle = new(0.93f, FontWeight.Bold);
    private static readonly TextStyle ComposeCancelStyle = new(0.97f, FontWeight.Medium);
    private static readonly TextStyle GifChipStyle = new(0.73f, FontWeight.Bold);
    private static readonly TextStyle RemainingStyle = new(0.87f, FontWeight.Bold);
    private static readonly Vector4 RingTrack = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 DisabledPill = new(1f, 1f, 1f, 0.09f);
    private static readonly Vector4 RemoveChipFill = new(0f, 0f, 0f, 0.6f);

    private void DrawCompose(Rect area)
    {
        if (composeOutcome == 1)
        {
            composeOutcome = 0;
            draft = string.Empty;
            composeStatus = string.Empty;
            composeEmoji.Close();
            quoteTarget = null;
            quoteTargetId = null;
            composeAttachments.Clear();
            composePicking = false;
            composeSensitive = false;
            store.RefreshFeed(SocialFeedScope.Latest);
            store.RefreshFeed(SocialFeedScope.Following);
            feedScrollTopPending = true;
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = composeFailure.Failed ? composeFailure.Text() : Loc.T(L.Account.CannotReach);
        }

        var pickedPath = Interlocked.Exchange(ref pendingComposePickedPath, null);
        if (pickedPath is not null)
        {
            AddComposeAttachment(pickedPath);
        }

        if (composePicking)
        {
            DrawComposePicker(area);
            return;
        }

        var scale = UiScale.Current;
        DrawComposeHeader(area);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var toolbarHeight = ComposeToolbarHeight * scale;
            var emojiHeight = composeEmoji.PanelHeight(scale);
            var panelTop = area.Max.Y - toolbarHeight - emojiHeight;
            var contentLimit = panelTop - 8f * scale;
            var padX = CellPadX * scale;
            var avatarRadius = FeedAvatarRadius * scale;
            var me = store.Me;
            var displayName = me is null ? string.Empty : SocialIdentity.Name(me.DisplayName, me.Handle);
            var inputX = origin.X + padX + avatarRadius * 2f + AvatarGap * scale;
            var inputWidth = MathF.Max(1f, origin.X + width - padX - inputX);
            var nameHeight = displayName.Length > 0 ? Typography.LineHeight(NameStyle) : 0f;
            var inputTop = origin.Y + CellPadTop * scale + nameHeight + 4f * scale;
            var framePadding = ImGui.GetStyle().FramePadding.X;
            var composeWrapWidth = inputWidth - framePadding * 2f - 4f * scale;
            var quotePreviewHeight = quoteTarget is not null
                ? QuotedCardHeight(quoteTarget, inputWidth) + 10f * scale
                : 0f;
            var stripGap = 8f * scale;
            var stripTile = composeAttachments.Count > 0
                ? MathF.Min(ComposeTileMax * scale,
                    (inputWidth - stripGap * (composeAttachments.Count - 1)) / MathF.Max(2f, composeAttachments.Count))
                : 0f;
            var stripHeight = composeAttachments.Count > 0 ? stripTile + 10f * scale : 0f;
            var measuredText = draft.Length == 0
                ? Typography.Measure("Ag", ComposeInputStyle).Y
                : Typography.MeasureWrapped(draft, composeWrapWidth, ComposeInputStyle.Scale);
            var statusHeight = composeStatus.Length > 0
                ? Typography.MeasureWrapped(composeStatus, inputWidth, 0.85f) + 6f * scale
                : 0f;
            var availableInput = contentLimit - inputTop - quotePreviewHeight - stripHeight - statusHeight
                - 8f * scale;
            var desiredInput = MathF.Max(measuredText + 34f * scale, 96f * scale);
            var inputHeight = MathF.Max(40f * scale, MathF.Min(desiredInput, availableInput));
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(origin.X + padX + avatarRadius, origin.Y + CellPadTop * scale + avatarRadius),
                    avatarRadius, me.Name, me.World, me.AvatarUrl, 0.95f, 48, Frames.Of(me.FrameId));
            }

            if (displayName.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(inputX, origin.Y + CellPadTop * scale),
                    Typography.FitText(displayName, inputWidth, NameStyle), ChirperInk.TitleInk, NameStyle);
            }

            ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
            ImGui.SetNextItemWidth(inputWidth);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.TitleInk))
            using (Plugin.Fonts.Push(ComposeInputStyle.Scale))
            {
                SoftWrapField.Multiline("##chirpBody", ref draft, MaxPostLength,
                    new Vector2(inputWidth, inputHeight), composeWrapWidth, composeMentions);
            }

            var pickedMention = mentionPopup.Draw(composeMentions, area, theme, images, lodestone);
            if (pickedMention >= 0)
            {
                composeMentions.Pick(pickedMention);
            }

            mentionPopup.Gate(composeMentions);

            if (draft.Length == 0)
            {
                Typography.Draw(drawList, new Vector2(inputX + 4f * scale, inputTop + 2f * scale),
                    Typography.FitText(Loc.T(L.Chirper.Compose), inputWidth, ComposeInputStyle), ChirperInk.MutedInk,
                    ComposeInputStyle);
            }

            if (composeAttachments.Count > 0)
            {
                DrawComposeAttachmentStrip(drawList, inputX, inputTop + inputHeight + 10f * scale, stripTile,
                    stripGap, scale);
            }

            if (quoteTarget is not null)
            {
                var quoteMin = new Vector2(inputX, inputTop + inputHeight + stripHeight + 10f * scale);
                DrawQuotedCard(drawList, quoteMin, inputWidth, QuotedCardHeight(quoteTarget, inputWidth), quoteTarget,
                    false, "compose.quote");
            }

            if (composeStatus.Length > 0)
            {
                var statusTop = inputTop + inputHeight + stripHeight + quotePreviewHeight + 4f * scale;
                ImGui.SetCursorScreenPos(new Vector2(inputX, statusTop));
                using (Typography.WrapAt(inputX + inputWidth))
                using (Plugin.Fonts.Push(0.85f))
                using (ImRaii.PushColor(ImGuiCol.Text, ChirperInk.Danger))
                {
                    Typography.Wrapped(composeStatus);
                }
            }

            if (composeEmoji.Open)
            {
                var panel = new Rect(new Vector2(area.Min.X, panelTop),
                    new Vector2(area.Max.X, area.Max.Y - toolbarHeight));
                composeEmoji.DrawPanel(panel, ui, ref draft, MaxPostLength);
            }

            DrawComposeToolbar(area, toolbarHeight);
        }
    }

    private void DrawComposeHeader(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var headerHeight = AppHeader.Height * scale;
        var rowCenterY = area.Min.Y + headerHeight * 0.5f;
        var cancelLabel = Loc.T(L.Common.Cancel);
        var cancelSize = Typography.Measure(cancelLabel, ComposeCancelStyle);
        var cancelMin = area.Min;
        var cancelMax = new Vector2(area.Min.X + CellPadX * scale + cancelSize.X + 12f * scale,
            area.Min.Y + headerHeight);
        var cancelHovered = UiInteract.Hover(cancelMin, cancelMax);
        Typography.Draw(drawList, new Vector2(area.Min.X + CellPadX * scale, rowCenterY - cancelSize.Y * 0.5f),
            cancelLabel, cancelHovered ? ChirperInk.TitleInk : ChirperInk.BodyInk, ComposeCancelStyle);
        if (cancelHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(cancelMin, cancelMax, cancelHovered))
        {
            back();
        }

        var actionLabel = store.Posting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.ChirpAction);
        var actionSize = Typography.Measure(actionLabel, ComposeActionStyle);
        var pillHeight = 33f * scale;
        var pillWidth = actionSize.X + 30f * scale;
        var pillMax = new Vector2(area.Max.X - 12f * scale, rowCenterY + pillHeight * 0.5f);
        var pillMin = new Vector2(pillMax.X - pillWidth, rowCenterY - pillHeight * 0.5f);
        var canPost = (!string.IsNullOrWhiteSpace(draft) || composeAttachments.Count > 0)
            && draft.Length <= MaxPostLength && !store.Posting;
        var pillHovered = canPost && UiInteract.Hover(pillMin, pillMax);
        var rounding = pillHeight * 0.5f;
        if (canPost)
        {
            ChirperPill.PaintAccent(drawList, pillMin, pillMax, rounding, pillHovered);
        }
        else
        {
            Squircle.Fill(drawList, pillMin, pillMax, rounding, ImGui.GetColorU32(DisabledPill));
        }

        Typography.DrawCentered(drawList, (pillMin + pillMax) * 0.5f, actionLabel,
            canPost ? ChirperInk.White : ChirperInk.FaintInk, ComposeActionStyle);
        if (pillHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(pillMin, pillMax, pillHovered))
        {
            Submit();
        }

        var title = Loc.T(quoteTarget is not null ? L.Chirper.QuoteTitle : L.Chirper.NewChirp);
        var leftReserve = (cancelMax.X - area.Min.X) / scale + 8f;
        AppHeader.DrawTitleWithReserve(area, "chirper.compose.title", title, pillWidth + 24f * scale,
            ChirperInk.TitleInk, scale, ComposeTitleStyle, leftReserve);
    }

    private void DrawComposeToolbar(Rect area, float height)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var barMin = new Vector2(area.Min.X, area.Max.Y - height);
        PaintBarBackdrop(drawList, new Rect(barMin, area.Max));
        drawList.AddLine(barMin, new Vector2(area.Max.X, barMin.Y), ImGui.GetColorU32(ChirperInk.Hairline), 1f);
        var centerY = barMin.Y + height * 0.5f;
        var iconRadius = 17f * scale;
        var canAttach = composeAttachments.Count < ChirperStore.MaxImages && !ComposeHasGif();
        var photoInk = canAttach ? ChirperInk.MutedInk : Palette.WithAlpha(ChirperInk.MutedInk, 0.4f);
        var photoCenter = new Vector2(area.Min.X + CellPadX * scale + iconRadius, centerY);
        var photoExtent = new Vector2(iconRadius, iconRadius);
        var photoHovered = UiInteract.Hover(photoCenter - photoExtent, photoCenter + photoExtent);
        if (photoHovered)
        {
            drawList.AddCircleFilled(photoCenter, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, photoCenter, PhoneIcons.Photo,
            canAttach ? ChirperInk.AccentLink : photoInk, 19f * scale);
        HoverTooltip.Show(new Rect(photoCenter - photoExtent, photoCenter + photoExtent), Loc.T(L.Chirper.AddPhotos),
            HoverLabelSide.Above);
        if (UiInteract.Click(photoCenter - photoExtent, photoCenter + photoExtent, photoHovered))
        {
            if (canAttach)
            {
                OpenComposePicker(false);
            }
            else
            {
                toast.Show(ComposeHasGif()
                    ? Loc.T(L.Common.GifRidesAlone)
                    : Loc.T(L.Chirper.MaxPhotos, ChirperStore.MaxImages));
            }
        }

        var gifSize = Typography.Measure("GIF", GifChipStyle);
        var chipHeight = 22f * scale;
        var gifMin = new Vector2(photoCenter.X + iconRadius + 8f * scale, centerY - chipHeight * 0.5f);
        var gifMax = new Vector2(gifMin.X + gifSize.X + 12f * scale, centerY + chipHeight * 0.5f);
        var gifEnabled = composeAttachments.Count == 0;
        var gifHovered = UiInteract.Hover(gifMin, gifMax);
        var gifInk = !gifEnabled ? Palette.WithAlpha(ChirperInk.MutedInk, 0.4f)
            : gifHovered ? ChirperInk.MineInk
            : ChirperInk.AccentLink;
        Squircle.Stroke(drawList, gifMin, gifMax, 6f * scale, ImGui.GetColorU32(gifInk), 1.5f);
        Typography.DrawCentered(drawList, (gifMin + gifMax) * 0.5f, "GIF", gifInk, GifChipStyle);
        if (gifHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(gifMin, gifMax), Loc.T(L.Settings.ChirperShowGifs), HoverLabelSide.Above);
        if (UiInteract.Click(gifMin, gifMax, gifHovered))
        {
            if (gifEnabled)
            {
                OpenComposePicker(true);
            }
            else
            {
                toast.Show(Loc.T(L.Common.GifRidesAlone));
            }
        }

        var emojiCenter = new Vector2(gifMax.X + 8f * scale + iconRadius, centerY);
        var emojiExtent = new Vector2(iconRadius, iconRadius);
        if (UiInteract.Hover(emojiCenter - emojiExtent, emojiCenter + emojiExtent))
        {
            drawList.AddCircleFilled(emojiCenter, iconRadius, ImGui.GetColorU32(ChirperInk.AccentWash), 32);
        }

        composeEmoji.DrawToggle(ui, emojiCenter, iconRadius, ChirperInk.MineInk, ChirperInk.AccentLink,
            Loc.T(L.Common.Emoji));
        if (composeAttachments.Count > 0)
        {
            var pillHeight = 30f * scale;
            var pillWidth = 42f * scale;
            var pillMin = new Vector2(emojiCenter.X + iconRadius + 8f * scale, centerY - pillHeight * 0.5f);
            var pillMax = new Vector2(pillMin.X + pillWidth, centerY + pillHeight * 0.5f);
            var pillHovered = UiInteract.Hover(pillMin, pillMax);
            var fill = composeSensitive ? ChirperInk.MineFill : pillHovered ? ChirperInk.ChipHover : ChirperInk.ChipFill;
            var stroke = composeSensitive ? ChirperInk.MineStroke : ChirperInk.ChipStroke;
            var ink = composeSensitive ? ChirperInk.MineInk : ChirperInk.MutedInk;
            Squircle.Fill(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(fill));
            Squircle.Stroke(drawList, pillMin, pillMax, pillHeight * 0.5f, ImGui.GetColorU32(stroke), 1f);
            PhoneIcon.Draw(drawList, (pillMin + pillMax) * 0.5f, PhoneIcons.EyeOff, ink, 15f * scale);
            if (pillHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            HoverTooltip.Show(new Rect(pillMin, pillMax),
                Loc.T(composeSensitive ? L.Moderation.SensitiveOn : L.Moderation.MarkSensitive), HoverLabelSide.Above);
            if (UiInteract.Click(pillMin, pillMax, pillHovered))
            {
                composeSensitive = !composeSensitive;
            }
        }

        var remaining = MaxPostLength - draft.Length;
        var ringRadius = ComposeRingRadius * scale;
        var ringCenter = new Vector2(area.Max.X - CellPadX * scale - 13f * scale, centerY);
        var ringColor = remaining < 0 ? ChirperInk.Danger
            : remaining < ComposeWarnRemaining ? ChirperInk.Warning
            : ChirperInk.Accent;
        ProgressRing.Track(ringCenter, ringRadius, ComposeRingStroke * scale, RingTrack);
        ProgressRing.Fill(ringCenter, ringRadius, ComposeRingStroke * scale, draft.Length / (float)MaxPostLength,
            ringColor);
        if (remaining < ComposeWarnRemaining)
        {
            var remainingText = remaining.ToString(Loc.Culture);
            var remainingSize = Typography.Measure(remainingText, RemainingStyle);
            Typography.Draw(drawList,
                new Vector2(ringCenter.X - 13f * scale - 8f * scale - remainingSize.X, centerY - remainingSize.Y * 0.5f),
                remainingText, ringColor, RemainingStyle);
        }
    }

    private void Submit()
    {
        if ((string.IsNullOrWhiteSpace(draft) && composeAttachments.Count == 0) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        composeFailure.Clear();
        var attachments = composeAttachments.ToArray();
        if (quoteTargetId is not null)
        {
            store.Quote(draft, quoteTargetId, attachments, ok => composeOutcome = ok ? 1 : 2);
        }
        else
        {
            store.Compose(draft, attachments, composeSensitive && attachments.Length > 0,
                ok => composeOutcome = ok ? 1 : 2, composeFailure.Set);
        }
    }

    private void DrawComposeAttachmentStrip(ImDrawListPtr drawList, float x, float y, float tile, float gap,
        float scale)
    {
        var rounding = ComposeTileRounding * scale;
        var removeIndex = -1;
        for (var index = 0; index < composeAttachments.Count; index++)
        {
            var min = new Vector2(x + (tile + gap) * index, y);
            var max = min + new Vector2(tile, tile);
            if (DrawComposeAttachmentThumb(drawList, composeAttachments[index], min, max, rounding, scale))
            {
                removeIndex = index;
            }
        }

        if (removeIndex >= 0)
        {
            composeAttachments.RemoveAt(removeIndex);
        }
    }

    private bool DrawComposeAttachmentThumb(ImDrawListPtr drawList, string path, Vector2 min, Vector2 max,
        float rounding, float scale)
    {
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipFill));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ChirperInk.ChipStroke), 1f);
        if (GifMedia.IsGif(path))
        {
            GifBadge.Draw(drawList, new Rect(min, max));
        }

        var badgeRadius = 11f * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - 6f * scale, min.Y + badgeRadius + 6f * scale);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        var badgeHovered = UiInteract.Hover(badgeMin, badgeMax);
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(badgeHovered ? Palette.WithAlpha(RemoveChipFill, 0.85f) : RemoveChipFill), 24);
        PhoneIcon.Draw(drawList, badgeCenter, PhoneIcons.X, ChirperInk.White, 11f * scale);
        if (badgeHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(badgeMin, badgeMax, badgeHovered);
    }

    private void DrawComposePicker(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Chirper.AddPhotos), () => composePicking = false);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Chirper.ImportFromPc), true))
        {
            FilePicker.PickImage(Loc.T(L.Chirper.AddPhotos),
                path => Interlocked.Exchange(ref pendingComposePickedPath, path));
        }

        var gridTop = importRect.Max.Y + 12f * scale;
        var gridRect = new Rect(new Vector2(area.Min.X, gridTop), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (composePickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Common.NoPhotos), ChirperInk.MutedInk);
                return;
            }

            const int pickerColumns = 3;
            var gap = 6f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (pickerColumns - 1)) / pickerColumns;
            var origin = ImGui.GetCursorScreenPos();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var cullMargin = cell + 60f * scale;
            for (var index = 0; index < composePickerPaths.Length; index++)
            {
                var column = index % pickerColumns;
                var rowIndex = index / pickerColumns;
                var rowTop = rowIndex * (cell + gap);
                if (rowTop + cell < scrollY - cullMargin || rowTop > scrollY + viewHeight + cullMargin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var hovered = UiInteract.Hover(min, max);
                DrawComposePickerThumb(composePickerPaths[index], min, max, scale, hovered);
                if (UiInteract.Click(min, max, hovered))
                {
                    AddComposeAttachment(composePickerPaths[index]);
                }
            }

            var rows = (composePickerPaths.Length + pickerColumns - 1) / pickerColumns;
            var totalHeight = rows * (cell + gap);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }

    private void DrawComposePickerThumb(string path, Vector2 min, Vector2 max, float scale, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = wallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
            return;
        }

        var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (GifMedia.IsGif(path))
        {
            GifBadge.Draw(drawList, new Rect(min, max));
        }

        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void OpenComposePicker(bool gifsOnly)
    {
        var all = library.List();
        if (!gifsOnly)
        {
            composePickerPaths = all;
        }
        else
        {
            var gifCount = 0;
            for (var index = 0; index < all.Length; index++)
            {
                if (GifMedia.IsGif(all[index]))
                {
                    gifCount++;
                }
            }

            var gifs = new string[gifCount];
            var cursor = 0;
            for (var index = 0; index < all.Length; index++)
            {
                if (GifMedia.IsGif(all[index]))
                {
                    gifs[cursor++] = all[index];
                }
            }

            composePickerPaths = gifs;
        }

        composePicking = true;
    }

    private bool ComposeHasGif()
    {
        return composeAttachments.Count > 0 && GifMedia.IsGif(composeAttachments[0]);
    }

    private void AddComposeAttachment(string path)
    {
        composePicking = false;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var addingGif = GifMedia.IsGif(path);
        if (ComposeHasGif() || (addingGif && composeAttachments.Count > 0))
        {
            composeStatus = Loc.T(L.Common.GifRidesAlone);
            return;
        }

        if (composeAttachments.Count >= ChirperStore.MaxImages)
        {
            composeStatus = Loc.T(L.Chirper.MaxPhotos, ChirperStore.MaxImages);
            return;
        }

        if (addingGif && !GifMedia.FitsSizeCap(path))
        {
            composeStatus = Loc.T(L.Common.GifTooLarge);
            return;
        }

        for (var index = 0; index < composeAttachments.Count; index++)
        {
            if (string.Equals(composeAttachments[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        composeStatus = string.Empty;
        composeAttachments.Add(path);
    }
}
