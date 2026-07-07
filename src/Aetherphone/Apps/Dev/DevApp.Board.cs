using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Dev;

internal sealed partial class DevApp
{
    private void DrawBoard(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var segmentHeight = 38f * scale;
        var segmentRect = new Rect(new Vector2(area.Min.X + 16f * scale, area.Min.Y + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, area.Min.Y + 8f * scale + segmentHeight));
        DrawSegmentStrip(segmentRect, ColumnSegmentLabels(), boardColumn, ref boardSegmentAnim,
            column => boardColumn = column);
        var bodyRect = new Rect(new Vector2(area.Min.X, segmentRect.Max.Y + 8f * scale), area.Max);
        var cards = store.Column(boardColumn);
        if (cards.Length == 0)
        {
            DrawBoardEmpty(bodyRect);
        }
        else
        {
            using (AppSurface.Begin(bodyRect))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < cards.Length; index++)
                {
                    DrawCardRow(cards[index]);
                }

                ImGui.Dummy(new Vector2(0f, 96f * scale));
            }
        }

        if (ComposeFab.Draw(area, ui, "##devComposeFab", Accent, FontAwesomeIcon.Plus.ToIconString(), "New card"))
        {
            cardTitleDraft = string.Empty;
            cardBodyDraft = string.Empty;
            router.Push(DevRoute.CardCompose);
        }
    }

    private string[] ColumnSegmentLabels()
    {
        var changed = false;
        for (var status = 0; status < DevStore.ColumnCount; status++)
        {
            if (segmentLabelCounts[status] != store.Column(status).Length)
            {
                changed = true;
                break;
            }
        }

        if (!changed)
        {
            return segmentLabels;
        }

        for (var status = 0; status < DevStore.ColumnCount; status++)
        {
            var count = store.Column(status).Length;
            segmentLabelCounts[status] = count;
            segmentLabels[status] = count > 0 ? $"{ColumnLabels[status]} ({count})" : ColumnLabels[status];
        }

        return segmentLabels;
    }

    private void DrawBoardEmpty(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(area.Center.X, area.Center.Y - 20f * scale);
        AppSkin.Icon(center, FontAwesomeIcon.Columns.ToIconString(),
            Palette.WithAlpha(AppPalettes.Dev.MutedInk, 0.7f), 2.4f);
        Typography.DrawCentered(new Vector2(area.Center.X, center.Y + 42f * scale),
            store.LoadingBoard && !store.BoardLoaded ? "Loading" : "Nothing here yet", AppPalettes.Dev.MutedInk, 0.9f);
    }

    private void DrawCardRow(DevBoardCardDto card)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 12f * scale;
        var hasBody = card.Body.Length > 0;
        var cardHeight = (hasBody ? 88f : 68f) * scale;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        ui.Card(drawList, origin, cardMax, 16f * scale);
        var chevronZone = 40f * scale;
        var textRight = origin.X + width - chevronZone;
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + pad), UiText.Truncate(card.Title, 34), theme.TextStrong,
            0.98f, FontWeight.SemiBold);
        var metaY = origin.Y + cardHeight - pad - 14f * scale;
        if (hasBody)
        {
            Typography.Draw(new Vector2(origin.X + pad, origin.Y + pad + 22f * scale), UiText.Truncate(card.Body, 44),
                AppPalettes.Dev.MutedInk, 0.84f);
        }

        var avatarRadius = 8f * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, metaY + 7f * scale);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            Monogram(card.CreatedByDisplayName, card.CreatedByHandle), 0.55f,
            AvatarFor(card.CreatedById, card.CreatedByAvatarUrl), 24);
        var author = string.IsNullOrEmpty(card.CreatedByDisplayName) ? card.CreatedByHandle : card.CreatedByDisplayName;
        Typography.Draw(new Vector2(avatarCenter.X + avatarRadius + 6f * scale, metaY),
            $"{UiText.Truncate(author, 18)} · {TimeText.Short(card.UpdatedAtUnix)}", AppPalettes.Dev.MutedInk, 0.76f);
        var chevronX = origin.X + width - chevronZone * 0.5f;
        var upCenter = new Vector2(chevronX, origin.Y + cardHeight * 0.30f);
        var downCenter = new Vector2(chevronX, origin.Y + cardHeight * 0.70f);
        if (card.Status > 0 && ui.IconButton(upCenter, 12f * scale, FontAwesomeIcon.ChevronLeft.ToIconString(),
                AppPalettes.Dev.MutedInk, new Vector4(1f, 1f, 1f, 0.07f), 0.72f, ColumnLabels[card.Status - 1]))
        {
            store.MoveCard(card.Id, card.Status - 1, null);
        }

        if (card.Status < DevStore.ColumnCount - 1 && ui.IconButton(downCenter, 12f * scale,
                FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.Dev.MutedInk,
                new Vector4(1f, 1f, 1f, 0.07f), 0.72f, ColumnLabels[card.Status + 1]))
        {
            store.MoveCard(card.Id, card.Status + 1, null);
        }

        if (UiInteract.HoverClick(origin, new Vector2(textRight, origin.Y + cardHeight)))
        {
            router.Push(DevRoute.CardDetail(card.Id));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 10f * scale));
    }

    private void DrawCardDetail(Rect area, string cardId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, "Card", back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var card = store.FindCard(cardId);
        if (card is null)
        {
            Typography.DrawCentered(body.Center, store.LoadingBoard ? "Loading" : "Card no longer exists",
                AppPalettes.Dev.MutedInk);
            return;
        }

        if (detailSegmentCardId != cardId)
        {
            detailSegmentCardId = cardId;
            detailSegmentAnim = card.Status;
        }

        using (AppSurface.Begin(body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var segmentOrigin = ImGui.GetCursorScreenPos();
            var segmentRect = new Rect(segmentOrigin, segmentOrigin + new Vector2(width, 34f * scale));
            DrawSegmentStrip(segmentRect, ColumnLabels, card.Status, ref detailSegmentAnim,
                status => store.MoveCard(cardId, status, null));
            ImGui.Dummy(new Vector2(width, 34f * scale + 16f * scale));

            DrawCardDetailPanel(card, width, scale);
            ImGui.Dummy(new Vector2(0f, 18f * scale));

            var buttonHeight = 42f * scale;
            var gap = 12f * scale;
            var buttonWidth = (width - gap) * 0.5f;
            var buttonsOrigin = ImGui.GetCursorScreenPos();
            var editRect = new Rect(buttonsOrigin, buttonsOrigin + new Vector2(buttonWidth, buttonHeight));
            var deleteRect = new Rect(new Vector2(editRect.Max.X + gap, buttonsOrigin.Y),
                new Vector2(buttonsOrigin.X + width, buttonsOrigin.Y + buttonHeight));
            if (ui.PillButton(editRect, "Edit", true))
            {
                cardEditLoadedFor = null;
                router.Push(DevRoute.CardEdit(cardId));
            }

            if (ui.DangerGhostButton(deleteRect, "Delete"))
            {
                AskDeleteCard(cardId);
            }

            ImGui.Dummy(new Vector2(width, buttonHeight + 24f * scale));
        }
    }

    private void DrawCardDetailPanel(DevBoardCardDto card, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cardPad = 18f * scale;
        var innerWidth = width - cardPad * 2f;
        var hasBody = card.Body.Length > 0;
        var titleStyle = TextStyles.Title2;
        var nameStyle = TextStyles.Headline;
        var metaStyle = TextStyles.Footnote;
        var author = string.IsNullOrEmpty(card.CreatedByDisplayName)
            ? card.CreatedByHandle
            : card.CreatedByDisplayName;
        var authorLabel = UiText.Truncate(author, 24);
        var when = card.UpdatedAtUnix > card.CreatedAtUnix
            ? $"{TimeText.Short(card.CreatedAtUnix)} · edited {TimeText.Short(card.UpdatedAtUnix)}"
            : TimeText.Short(card.CreatedAtUnix);
        float titleHeight;
        using (Plugin.Fonts.Push(titleStyle.Scale, titleStyle.Weight))
        {
            titleHeight = ImGui.CalcTextSize(card.Title, false, innerWidth).Y;
        }

        var nameSize = Typography.Measure(authorLabel, nameStyle);
        var whenSize = Typography.Measure(when, metaStyle);
        var metaLineGap = 3f * scale;
        var metaBlockHeight = nameSize.Y + metaLineGap + whenSize.Y;
        var avatarRadius = 14f * scale;
        var metaHeight = MathF.Max(avatarRadius * 2f, metaBlockHeight);
        var titleMetaGap = 16f * scale;
        var dividerTopGap = 16f * scale;
        var dividerBottomGap = 16f * scale;
        var bodyHeight = hasBody ? ImGui.CalcTextSize(card.Body, false, innerWidth).Y : 0f;
        var panelHeight = cardPad * 2f + titleHeight + titleMetaGap + metaHeight +
                          (hasBody ? dividerTopGap + 1f * scale + dividerBottomGap + bodyHeight : 0f);
        var panelMin = ImGui.GetCursorScreenPos();
        var panelMax = new Vector2(panelMin.X + width, panelMin.Y + panelHeight);
        ui.Card(drawList, panelMin, panelMax, Metrics.Radius.Lg * scale, true);

        var contentX = panelMin.X + cardPad;
        var cursorY = panelMin.Y + cardPad;
        ImGui.SetCursorScreenPos(new Vector2(contentX, cursorY));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + innerWidth);
        using (Plugin.Fonts.Push(titleStyle.Scale, titleStyle.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.TextWrapped(card.Title);
        }

        ImGui.PopTextWrapPos();
        cursorY += titleHeight + titleMetaGap;

        var avatarCenter = new Vector2(contentX + avatarRadius, cursorY + metaHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            Monogram(card.CreatedByDisplayName, card.CreatedByHandle), 0.72f,
            AvatarFor(card.CreatedById, card.CreatedByAvatarUrl), 32);
        var metaTextX = avatarCenter.X + avatarRadius + 12f * scale;
        var metaBlockTop = avatarCenter.Y - metaBlockHeight * 0.5f;
        Typography.Draw(new Vector2(metaTextX, metaBlockTop), authorLabel, theme.TextStrong, nameStyle);
        Typography.Draw(new Vector2(metaTextX, metaBlockTop + nameSize.Y + metaLineGap), when,
            AppPalettes.Dev.MutedInk, metaStyle);
        cursorY += metaHeight;

        if (hasBody)
        {
            cursorY += dividerTopGap;
            drawList.AddLine(new Vector2(contentX, cursorY), new Vector2(panelMax.X - cardPad, cursorY),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
            cursorY += 1f * scale + dividerBottomGap;
            ImGui.SetCursorScreenPos(new Vector2(contentX, cursorY));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + innerWidth);
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Dev.BodyInk))
            {
                ImGui.TextWrapped(card.Body);
            }

            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorScreenPos(panelMin);
        ImGui.Dummy(new Vector2(width, panelHeight));
    }

    private void AskDeleteCard(string cardId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = "Delete this card for everyone?",
            ConfirmLabel = "Delete",
            CancelLabel = "Cancel",
            BusyLabel = "Deleting",
            FailedMessage = "Delete failed.",
            ConfirmAsync = done => store.DeleteCard(cardId, ok =>
            {
                if (ok)
                {
                    router.Pop();
                }

                done(ok);
            }),
        });
    }

    private void DrawCardCompose(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, "New Card", back);
        DrawCardForm(area, "Create", () => store.CreateCard(cardTitleDraft.Trim(), cardBodyDraft.Trim(), ok =>
        {
            if (ok)
            {
                router.Pop();
            }
        }));
    }

    private void DrawCardEdit(Rect area, string cardId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, "Edit Card", back);
        if (cardEditLoadedFor != cardId)
        {
            cardEditLoadedFor = cardId;
            var card = store.FindCard(cardId);
            cardTitleDraft = card?.Title ?? string.Empty;
            cardBodyDraft = card?.Body ?? string.Empty;
        }

        DrawCardForm(area, "Save", () => store.UpdateCard(cardId, cardTitleDraft.Trim(), cardBodyDraft.Trim(), ok =>
        {
            if (ok)
            {
                router.Pop();
            }
        }));
    }

    private void DrawCardForm(Rect area, string submitLabel, Action submit)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ui.Field("Title", "##devCardTitle", ref cardTitleDraft, CardTitleMax, false);
            ImGui.Dummy(new Vector2(0f, 12f * scale));
            ui.Field("Details", "##devCardBody", ref cardBodyDraft, CardBodyMax, true);
            ImGui.Dummy(new Vector2(0f, 18f * scale));
            var buttonHeight = 42f * scale;
            var buttonsOrigin = ImGui.GetCursorScreenPos();
            var submitRect = new Rect(buttonsOrigin, buttonsOrigin + new Vector2(width, buttonHeight));
            var canSubmit = cardTitleDraft.Trim().Length > 0 && !store.CardBusy;
            if (ui.PillButton(submitRect, store.CardBusy ? "Saving" : submitLabel, canSubmit) && canSubmit)
            {
                submit();
            }

            ImGui.Dummy(new Vector2(width, buttonHeight + 24f * scale));
        }
    }
}
