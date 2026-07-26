using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float InquiryRowHeight = 74f;
    private const float ComposerHeight = 52f;
    private const int InquiryBodyMax = 1000;

    private string inquiryDraft = string.Empty;
    private string? inquiryAdFilter;
    private bool inquiryBusy;
    private bool inquirySendFailed;

    private void DrawInquiries(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawTabTitle(area, Loc.T(L.YellowPages.InquiriesTitle), 0f, scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var threads = inquiries.Threads;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (inquiryAdFilter is { } adId)
            {
                DrawInquiryFilterChip(adId, scale);
            }

            var shown = 0;
            for (var index = 0; index < threads.Length; index++)
            {
                var thread = threads[index];
                if (inquiryAdFilter is not null && thread.AdId != inquiryAdFilter)
                {
                    continue;
                }

                shown++;
                if (DrawInquiryRow(thread, scale))
                {
                    OpenInquiryThread(thread.Id);
                }
            }

            if (shown == 0)
            {
                DrawInquiriesEmpty(body, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawInquiryFilterChip(string adId, float scale)
    {
        var title = InquiryAdTitle(adId);
        chipLabels[0] = title.Length > 0 ? title : Loc.T(L.YellowPages.InquiriesTitle);
        chipActive[0] = true;
        if (intentRail.Draw(ui, chipLabels.AsSpan(0, 1), chipActive.AsSpan(0, 1)) >= 0)
        {
            inquiryAdFilter = null;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private string InquiryAdTitle(string adId)
    {
        var threads = inquiries.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].AdId == adId)
            {
                return threads[index].AdTitle;
            }
        }

        return string.Empty;
    }

    private bool DrawInquiryRow(AdInquiryDto thread, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = InquiryRowHeight * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Metrics.Space.Md * scale;
        var thumbSide = 42f * scale;
        var thumbMin = new Vector2(card.Min.X + pad, card.Min.Y + (height - thumbSide) * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSide, thumbSide);
        var thumb = string.IsNullOrEmpty(thread.AdMediaUrl) ? null : images.Get(thread.AdMediaUrl!);
        if (thumb is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(thumb.Size);
            drawList.AddImageRounded(thumb.Handle, thumbMin, thumbMax, uv0, uv1, 0xFFFFFFFFu, 10f * scale,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            IconTile.Draw((thumbMin + thumbMax) * 0.5f, thumbSide, IconTile.Surface(ui.Accent),
                AdCategories.Icon(thread.AdCategory));
        }

        var textLeft = thumbMax.X + 11f * scale;
        var textRight = card.Max.X - pad;
        var stamp = TimeText.Short(thread.LastMessageAtUnix);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textRight - stampSize.X, card.Min.Y + 12f * scale), stamp,
            AppPalettes.YellowPages.MutedInk, TextStyles.Caption1);
        var title = Typography.FitText(thread.AdTitle, textRight - textLeft - stampSize.X - 10f * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 11f * scale), title,
            AppPalettes.YellowPages.TitleInk, TextStyles.SubheadlineEmphasized);
        var who = SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
        var whoLine = Typography.FitText(who, textRight - textLeft, TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 30f * scale), whoLine, ui.Accent,
            TextStyles.Caption1);
        var preview = thread.LastBody.Replace('\n', ' ');
        var previewWidth = textRight - textLeft - (thread.UnreadCount > 0 ? 26f * scale : 0f);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + 47f * scale),
            Typography.FitText(preview, previewWidth, TextStyles.Footnote),
            AppPalettes.YellowPages.MutedInk, TextStyles.Footnote);
        if (thread.UnreadCount > 0)
        {
            ActivityBadge.Draw(new Vector2(textRight - 8f * scale, card.Min.Y + 52f * scale), thread.UnreadCount,
                theme, scale);
        }

        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + AdCard.Gap * scale));
        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private void DrawInquiriesEmpty(Rect body, float scale)
    {
        if (inquiries.Loading && !inquiries.LoadedOnce)
        {
            var origin = ImGui.GetCursorScreenPos();
            LoadingPulse.Draw(new Vector2(body.Center.X, origin.Y + 70f * scale), 13f * scale, ui.Accent,
                AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 150f * scale));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Comments, Loc.T(L.YellowPages.NoInquiriesTitle),
            Loc.T(L.YellowPages.NoInquiriesHint));
    }

    private void DrawInquiryThread(Rect area, string inquiryId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var thread = inquiries.Thread(inquiryId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, thread is null ? Loc.T(L.YellowPages.InquiriesTitle) : thread.AdTitle, backFromThread);
        ChatHeaderControls.DrawLock(ui, area, area.Min.Y + AppHeader.Height * scale * 0.5f, inquiries.CanEncrypt,
            inquiries.VaultState, () => router.Push(YellowPagesRoute.Encryption));
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerTop = area.Max.Y - ComposerHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, composerTop));
        if (thread is null)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Comments, Loc.T(L.YellowPages.UnavailableTitle),
                Loc.T(L.YellowPages.UnavailableHint));
            return;
        }

        DrawThreadAdCard(thread, body, scale, out var listTop);
        var list = new Rect(new Vector2(area.Min.X, listTop), new Vector2(area.Max.X, composerTop));
        using (AppSurface.Begin(list))
        {
            var messages = inquiries.Messages;
            var myId = inquiries.MyUserId;
            for (var index = 0; index < messages.Length; index++)
            {
                var message = messages[index];
                var direction = message.SenderId == myId ? MessageDirection.Outgoing : MessageDirection.Incoming;
                ChatBubble.Draw(new ChatLine(direction, inquiries.Reveal(thread.OtherUserId, message),
                    DateTimeOffset.FromUnixTimeSeconds(message.CreatedAtUnix).LocalDateTime), theme);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        }

        DrawInquiryComposer(new Rect(new Vector2(area.Min.X, composerTop), area.Max), inquiryId,
            thread.OtherUserId, scale);
    }

    private void DrawThreadAdCard(AdInquiryDto thread, Rect body, float scale, out float listTop)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Lg * scale;
        var height = 54f * scale;
        var min = new Vector2(body.Min.X + pad, body.Min.Y + Metrics.Space.Xs * scale);
        var max = new Vector2(body.Max.X - pad, min.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Card * scale, elevated: true);
        var thumbSide = 34f * scale;
        var thumbMin = new Vector2(min.X + 10f * scale, min.Y + (height - thumbSide) * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSide, thumbSide);
        var thumb = string.IsNullOrEmpty(thread.AdMediaUrl) ? null : images.Get(thread.AdMediaUrl!);
        if (thumb is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(thumb.Size);
            drawList.AddImageRounded(thumb.Handle, thumbMin, thumbMax, uv0, uv1, 0xFFFFFFFFu, 8f * scale,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            IconTile.Draw((thumbMin + thumbMax) * 0.5f, thumbSide, IconTile.Surface(ui.Accent),
                AdCategories.Icon(thread.AdCategory));
        }

        var textLeft = thumbMax.X + 10f * scale;
        var title = Typography.FitText(thread.AdTitle, max.X - textLeft - 34f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 10f * scale), title,
            AppPalettes.YellowPages.TitleInk, TextStyles.SubheadlineEmphasized);
        var who = SocialIdentity.Name(thread.OtherName, thread.OtherHandle);
        Typography.Draw(drawList, new Vector2(textLeft, min.Y + 29f * scale),
            Typography.FitText(who, max.X - textLeft - 34f * scale, TextStyles.Caption1),
            AppPalettes.YellowPages.MutedInk, TextStyles.Caption1);
        AppSkin.Icon(drawList, new Vector2(max.X - 18f * scale, (min.Y + max.Y) * 0.5f),
            FontAwesomeIcon.ChevronRight.ToIconString(), AppPalettes.YellowPages.MutedInk, 0.7f);
        var hovered = UiInteract.Hover(min, max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(min, max, hovered))
        {
            OpenDetail(thread.AdId);
        }

        listTop = max.Y + Metrics.Space.Xs * scale;
    }

    private void DrawInquiryComposer(Rect bar, string inquiryId, string otherUserId, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
        var inset = 12f * scale;
        var sendSide = 34f * scale;
        var fieldRect = new Rect(new Vector2(bar.Min.X + inset, bar.Min.Y + 8f * scale),
            new Vector2(bar.Max.X - inset - sendSide - 8f * scale, bar.Max.Y - 10f * scale));
        if (!inquiries.CanEncrypt)
        {
            Typography.Draw(new Vector2(fieldRect.Min.X, fieldRect.Center.Y - 8f * scale),
                Loc.T(L.YellowPages.InquiryLocked), AppPalettes.YellowPages.MutedInk, TextStyles.Footnote);
            return;
        }

        var submitted = SubmitField.Draw(fieldRect, "##adInquiryDraft", Loc.T(L.YellowPages.InquiryHint),
            ref inquiryDraft, theme, InquiryBodyMax);
        var sendCenter = new Vector2(bar.Max.X - inset - sendSide * 0.5f, (fieldRect.Min.Y + fieldRect.Max.Y) * 0.5f);
        var canSend = TrimmedLength(inquiryDraft) > 0 && !inquiries.Sending;
        if (inquiries.Sending)
        {
            LoadingPulse.Spinner(sendCenter, 9f * scale, ui.Accent);
            return;
        }

        var tapped = ui.IconButton(sendCenter, sendSide * 0.5f, FontAwesomeIcon.PaperPlane.ToIconString(),
            canSend ? ui.Accent : AppPalettes.YellowPages.MutedInk, AppSkin.Transparent, 0.9f);
        if (!canSend || (!tapped && !submitted))
        {
            return;
        }

        var body = inquiryDraft.Trim();
        inquiryDraft = string.Empty;
        inquirySendFailed = false;
        inquiries.Send(inquiryId, otherUserId, body, ok =>
        {
            if (!ok)
            {
                inquiryDraft = body;
                inquirySendFailed = true;
            }
        });
    }

    private void DrawNewInquiry(Rect area, string adId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var ad = ResolveAd(adId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, ad?.Title ?? Loc.T(L.YellowPages.InquireAction), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerTop = area.Max.Y - ComposerHeight * scale;
        var body = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, composerTop));
        if (ad is null)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Comments, Loc.T(L.YellowPages.UnavailableTitle),
                Loc.T(L.YellowPages.UnavailableHint));
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawCard(ad, NowUnix(), scale);
            ui.HelpText(Loc.T(L.YellowPages.InquiryConsentHint));
            if (inquirySendFailed)
            {
                Typography.Draw(ImGui.GetCursorScreenPos(), Loc.T(L.YellowPages.InquirySendFailed), theme.Danger,
                    TextStyles.Footnote);
            }
        }

        DrawNewInquiryComposer(new Rect(new Vector2(area.Min.X, composerTop), area.Max), adId,
            ad.OwnerId, scale);
    }

    private void DrawNewInquiryComposer(Rect bar, string adId, string ownerId, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(bar.Min, new Vector2(bar.Max.X, bar.Min.Y),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f);
        var inset = 12f * scale;
        var sendSide = 34f * scale;
        var fieldRect = new Rect(new Vector2(bar.Min.X + inset, bar.Min.Y + 8f * scale),
            new Vector2(bar.Max.X - inset - sendSide - 8f * scale, bar.Max.Y - 10f * scale));
        if (!inquiries.CanEncrypt)
        {
            Typography.Draw(new Vector2(fieldRect.Min.X, fieldRect.Center.Y - 8f * scale),
                Loc.T(L.YellowPages.InquiryLocked), AppPalettes.YellowPages.MutedInk, TextStyles.Footnote);
            return;
        }

        var submitted = SubmitField.Draw(fieldRect, "##adInquiryNew", Loc.T(L.YellowPages.InquiryHint),
            ref inquiryDraft, theme, InquiryBodyMax);
        var sendCenter = new Vector2(bar.Max.X - inset - sendSide * 0.5f, (fieldRect.Min.Y + fieldRect.Max.Y) * 0.5f);
        if (inquiryBusy)
        {
            LoadingPulse.Spinner(sendCenter, 9f * scale, ui.Accent);
            return;
        }

        var canSend = TrimmedLength(inquiryDraft) > 0;
        var tapped = ui.IconButton(sendCenter, sendSide * 0.5f, FontAwesomeIcon.PaperPlane.ToIconString(),
            canSend ? ui.Accent : AppPalettes.YellowPages.MutedInk, AppSkin.Transparent, 0.9f);
        if (!canSend || (!tapped && !submitted))
        {
            return;
        }

        var text = inquiryDraft.Trim();
        inquiryDraft = string.Empty;
        inquiryBusy = true;
        inquiries.OpenForAd(adId, ownerId, text, thread =>
        {
            inquiryBusy = false;
            if (thread is null)
            {
                inquiryDraft = text;
                inquirySendFailed = true;
                return;
            }

            router.Pop(false);
            OpenInquiryThread(thread.Id);
        });
    }

    private void OpenInquiryFor(AdDto ad)
    {
        var threads = inquiries.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].AdId == ad.Id && !threads[index].Mine)
            {
                OpenInquiryThread(threads[index].Id);
                return;
            }
        }

        inquiryDraft = string.Empty;
        router.Push(YellowPagesRoute.NewInquiry(ad.Id));
    }

    private void DrawEncryptionInfo(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Encryption.Title), back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            encryptionPane.DrawBody(ui, theme, store.IsSignedIn, inquiries.CanEncrypt);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void OpenInquiryThread(string inquiryId)
    {
        inquiryDraft = string.Empty;
        inquiries.Open(inquiryId);
        router.Push(YellowPagesRoute.Thread(inquiryId));
    }

    private void OpenInquiriesFor(string adId)
    {
        inquiryAdFilter = adId;
        activeTab = YellowPagesTab.Inquiries;
        inquiries.Refresh();
    }
}
