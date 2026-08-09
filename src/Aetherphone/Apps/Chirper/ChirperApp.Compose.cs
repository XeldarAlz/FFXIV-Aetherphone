using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
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
            store.RefreshFeed(SocialFeedScope.ForYou);
            store.RefreshFeed(SocialFeedScope.Following);
            feedScrollTopPending = true;
            router.Pop();
            return;
        }

        if (composeOutcome == 2)
        {
            composeOutcome = 0;
            composeStatus = Loc.T(L.Account.CannotReach);
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
        var context = new PhoneContext(area, theme, navigation);
        var title = Loc.T(quoteTarget is not null ? L.Chirper.QuoteTitle : L.Chirper.NewChirp);
        var actionLabel = store.Posting ? Loc.T(L.Chirper.Saving) : Loc.T(L.Chirper.Post);
        var actionReserve = Typography.Measure(actionLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale + 20f * scale;
        AppHeader.Draw(context, string.Empty, back);
        AppHeader.DrawTitleWithReserve(area, "chirper.compose.title", title, actionReserve, theme.TextStrong, scale);
        var canPost = (!string.IsNullOrWhiteSpace(draft) || composeAttachments.Count > 0) && !store.Posting;
        if (ui.HeaderAction(area, actionLabel, canPost))
        {
            Submit();
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var footerHeight = 44f * scale;
            var emojiHeight = composeEmoji.PanelHeight(scale);
            var panelTop = area.Max.Y - footerHeight - emojiHeight;
            var cardMin = origin;
            var cardLimit = emojiHeight > 0f ? panelTop - 8f * scale : panelTop;
            var pad = 14f * scale;
            var radius = 20f * scale;
            var me = store.Me;
            var displayName = me is null ? string.Empty : SocialIdentity.Name(me.DisplayName, me.Handle);
            var inputLeft = pad + radius * 2f + 12f * scale;
            var inputX = cardMin.X + inputLeft;
            var nameMaxWidth = MathF.Max(1f, width - inputLeft - pad);
            displayName = displayName.Length > 0
                ? Typography.FitText(displayName, nameMaxWidth, 1.05f, FontWeight.SemiBold)
                : displayName;
            var nameSize = displayName.Length > 0
                ? Typography.Measure(displayName, 1.05f, FontWeight.SemiBold)
                : Vector2.Zero;
            var inputTop = cardMin.Y + pad + nameSize.Y + 6f * scale;
            var inputWidth = width - inputLeft - pad;
            var framePadding = ImGui.GetStyle().FramePadding.X;
            var composeWrapWidth = inputWidth - framePadding * 2f - 4f * scale;
            var quotePreviewHeight = quoteTarget is not null
                ? QuotedCardHeight(quoteTarget, inputWidth) + 8f * scale
                : 0f;
            var stripGap = 6f * scale;
            var stripTile = composeAttachments.Count > 0
                ? MathF.Min(120f * scale,
                    (inputWidth - stripGap * (composeAttachments.Count - 1)) / MathF.Max(2f, composeAttachments.Count))
                : 0f;
            var stripHeight = composeAttachments.Count > 0 ? stripTile + 8f * scale : 0f;
            var measuredText = draft.Length == 0
                ? Typography.Measure("Ag", 1.15f).Y
                : Typography.MeasureWrapped(draft, composeWrapWidth, 1.15f);
            var statusHeight = composeStatus.Length > 0
                ? Typography.MeasureWrapped(composeStatus, inputWidth, 0.85f) + 6f * scale
                : 0f;
            var availableInput = cardLimit - inputTop - pad - quotePreviewHeight - stripHeight - statusHeight;
            var desiredInput = MathF.Max(measuredText + 34f * scale, 96f * scale);
            var inputHeight = MathF.Max(40f * scale, MathF.Min(desiredInput, availableInput));
            var cardBottom = MathF.Min(cardLimit,
                inputTop + inputHeight + stripHeight + quotePreviewHeight + statusHeight + pad);
            ui.Card(drawList, cardMin, new Vector2(origin.X + width, cardBottom), 18f * scale);
            if (me is not null)
            {
                DrawAvatar(drawList, new Vector2(cardMin.X + pad + radius, cardMin.Y + pad + radius), radius, me.Name,
                    me.World, me.AvatarUrl, 0.95f, 48);
            }

            if (displayName.Length > 0)
            {
                Typography.Draw(new Vector2(inputX, cardMin.Y + pad), displayName, theme.TextStrong, 1.05f,
                    FontWeight.SemiBold);
            }

            ImGui.SetCursorScreenPos(new Vector2(inputX, inputTop));
            ImGui.SetNextItemWidth(inputWidth);
            if (composeFocus)
            {
                ImGui.SetKeyboardFocusHere();
                composeFocus = false;
            }

            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Chirper.TitleInk))
            using (Plugin.Fonts.Push(1.15f))
            {
                SoftWrapField.Multiline("##chirpBody", ref draft, MaxPostLength,
                    new Vector2(inputWidth, inputHeight), composeWrapWidth, composeMentions, allowNewlines: true);
            }

            var pickedMention = mentionPopup.Draw(composeMentions, area, theme, images, lodestone);
            if (pickedMention >= 0)
            {
                composeMentions.Pick(pickedMention);
            }

            mentionPopup.Gate(composeMentions);

            if (draft.Length == 0)
            {
                Typography.Draw(new Vector2(inputX + 4f * scale, inputTop + 2f * scale), Loc.T(L.Chirper.Compose),
                    AppPalettes.Chirper.MutedInk, 1.15f);
            }

            if (composeAttachments.Count > 0)
            {
                DrawComposeAttachmentStrip(drawList, inputX, inputTop + inputHeight + 8f * scale, stripTile, stripGap,
                    scale);
            }

            if (quoteTarget is not null)
            {
                var quoteMin = new Vector2(inputX, inputTop + inputHeight + stripHeight + 8f * scale);
                DrawQuotedCard(drawList, quoteMin, inputWidth, QuotedCardHeight(quoteTarget, inputWidth), quoteTarget,
                    false, "compose.quote");
            }

            if (composeStatus.Length > 0)
            {
                var statusTop = inputTop + inputHeight + stripHeight + quotePreviewHeight + 4f * scale;
                ImGui.SetCursorScreenPos(new Vector2(inputX, statusTop));
                using (Typography.WrapAt(inputX + inputWidth))
                using (Plugin.Fonts.Push(0.85f))
                using (ImRaii.PushColor(ImGuiCol.Text, theme.Danger))
                {
                    Typography.Wrapped(composeStatus);
                }
            }

            if (composeEmoji.Open)
            {
                var panel = new Rect(new Vector2(area.Min.X, panelTop),
                    new Vector2(area.Max.X, area.Max.Y - footerHeight));
                composeEmoji.DrawPanel(panel, ui, ref draft, MaxPostLength);
            }

            var footerY = area.Max.Y - footerHeight * 0.5f;
            var emojiRadius = 17f * scale;
            var emojiCenter = new Vector2(origin.X + 6f * scale + emojiRadius, footerY);
            composeEmoji.DrawToggle(ui, emojiCenter, emojiRadius, Accent, AppPalettes.Chirper.MutedInk,
                Loc.T(L.Common.Emoji));
            var photoCenter = new Vector2(emojiCenter.X + emojiRadius * 2f + 14f * scale, footerY);
            var canAttach = composeAttachments.Count < ChirperStore.MaxImages && !ComposeHasGif();
            var photoInk = canAttach
                ? AppPalettes.Chirper.MutedInk
                : Palette.WithAlpha(AppPalettes.Chirper.MutedInk, 0.4f);
            if (ui.IconButton(photoCenter, emojiRadius, FontAwesomeIcon.Image.ToIconString(), photoInk,
                    new Vector4(0f, 0f, 0f, 0f), 1.2f, Loc.T(L.Chirper.AddPhotos)))
            {
                if (canAttach)
                {
                    OpenComposePicker();
                }
                else
                {
                    composeStatus = ComposeHasGif()
                        ? Loc.T(L.Chirper.GifRidesAlone)
                        : Loc.T(L.Chirper.MaxPhotos, ChirperStore.MaxImages);
                }
            }

            var remaining = MaxPostLength - draft.Length;
            var counterColor = remaining < 40
                ? (remaining < 0 ? theme.Danger : new Vector4(0.95f, 0.65f, 0.20f, 1f))
                : AppPalettes.Chirper.MutedInk;
            var counter = string.Format(Loc.Culture, "{0} / {1}", draft.Length, MaxPostLength);
            var counterSize = Typography.Measure(counter, 1f, FontWeight.Medium);
            Typography.Draw(new Vector2(area.Max.X - 6f * scale - counterSize.X, footerY - counterSize.Y * 0.5f),
                counter, counterColor, 1f, FontWeight.Medium);
        }
    }

    private void Submit()
    {
        if ((string.IsNullOrWhiteSpace(draft) && composeAttachments.Count == 0) || store.Posting)
        {
            return;
        }

        composeStatus = string.Empty;
        var attachments = composeAttachments.ToArray();
        if (quoteTargetId is not null)
        {
            store.Quote(draft, quoteTargetId, attachments, ok => composeOutcome = ok ? 1 : 2);
        }
        else
        {
            store.Compose(draft, attachments, ok => composeOutcome = ok ? 1 : 2);
        }
    }

    private void DrawComposeAttachmentStrip(ImDrawListPtr drawList, float x, float y, float tile, float gap,
        float scale)
    {
        var rounding = 10f * scale;
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
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(theme.SurfaceMuted));
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }

        var badgeRadius = 8.5f * scale;
        var badgeCenter = new Vector2(max.X - badgeRadius - 2f * scale, min.Y + badgeRadius + 2f * scale);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        var badgeHovered = UiInteract.Hover(badgeMin, badgeMax);
        drawList.AddCircleFilled(badgeCenter, badgeRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, badgeHovered ? 0.9f : 0.62f)), 20);
        AppSkin.Icon(badgeCenter, FontAwesomeIcon.Times.ToIconString(), new Vector4(1f, 1f, 1f, 1f), 0.6f);
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
                    Loc.T(L.Common.NoPhotos), AppPalettes.Chirper.MutedInk);
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
        if (hovered)
        {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private void OpenComposePicker()
    {
        composePickerPaths = library.List();
        composePicking = true;
    }

    private bool ComposeHasGif()
    {
        return composeAttachments.Count > 0 && ChirperStore.IsGifPath(composeAttachments[0]);
    }

    private void AddComposeAttachment(string path)
    {
        composePicking = false;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var addingGif = ChirperStore.IsGifPath(path);
        if (ComposeHasGif() || (addingGif && composeAttachments.Count > 0))
        {
            composeStatus = Loc.T(L.Chirper.GifRidesAlone);
            return;
        }

        if (composeAttachments.Count >= ChirperStore.MaxImages)
        {
            composeStatus = Loc.T(L.Chirper.MaxPhotos, ChirperStore.MaxImages);
            return;
        }

        if (addingGif)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > ChirperStore.MaxGifBytes)
            {
                composeStatus = Loc.T(L.Chirper.GifTooLarge);
                return;
            }
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
