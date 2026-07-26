using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float MineActionRowHeight = 34f;
    private const long RenewWindowSeconds = 5L * 86400L;

    private string? mineBusyAdId;

    private void DrawMine(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawTabTitle(area, Loc.T(L.YellowPages.YourAds), 0f, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var mine = store.Mine;
        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (mine.Length == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.NoAdsTitle),
                    Loc.T(L.YellowPages.NoAdsHint));
            }
            else
            {
                ui.HelpText(Loc.T(L.YellowPages.MineHint));
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                for (var index = 0; index < mine.Length; index++)
                {
                    DrawMineRow(mine[index], nowUnix, scale);
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawMineRow(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var headerHeight = 52f * scale;
        var actionsHeight = MineActionRowHeight * scale + Metrics.Space.Sm * scale;
        var height = headerHeight + actionsHeight + Metrics.Space.Sm * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Lg * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = 12f * scale;
        var tileSide = 32f * scale;
        var tileCenter = new Vector2(card.Min.X + pad + tileSide * 0.5f, card.Min.Y + pad + tileSide * 0.5f);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), AdCategories.Icon(ad.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 10f * scale;
        var titleWidth = card.Max.X - pad - textLeft;
        Marquee.DrawLeftAuto(drawList, "yellowpages.mine.title." + ad.Id, ad.Title, textLeft,
            card.Min.Y + 10f * scale, titleWidth, TextStyles.Headline, AppPalettes.YellowPages.TitleInk);
        var status = MineStatusText(ad, nowUnix, out var statusColor);
        Marquee.DrawLeftAuto(drawList, "yellowpages.mine.status." + ad.Id, status, textLeft,
            card.Min.Y + 31f * scale, titleWidth, TextStyles.FootnoteEmphasized, statusColor);

        var inquiryCount = inquiries.CountForAd(ad.Id);
        if (inquiryCount > 0)
        {
            var label = Loc.T(L.YellowPages.InquiryCount, inquiryCount);
            var labelSize = Typography.Measure(label, TextStyles.Caption1);
            var pillMin = new Vector2(card.Max.X - pad - labelSize.X - 18f * scale, card.Min.Y + 12f * scale);
            var pillWidth = DrawPill(drawList, pillMin, label, Palette.WithAlpha(ui.Accent, 0.18f), ui.Accent,
                TextStyles.Caption1, scale);
            var pillMax = new Vector2(pillMin.X + pillWidth, pillMin.Y + 24f * scale);
            var unread = inquiries.UnreadForAd(ad.Id);
            ActivityBadge.Draw(new Vector2(pillMax.X - 2f * scale, pillMin.Y + 2f * scale), unread, theme, scale);
            var pillHovered = UiInteract.Hover(pillMin, pillMax);
            if (pillHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(pillMin, pillMax, pillHovered))
            {
                OpenInquiriesFor(ad.Id);
                return;
            }
        }

        var headerRect = new Rect(card.Min, new Vector2(card.Max.X, card.Min.Y + headerHeight));
        var headerHovered = UiInteract.Hover(headerRect.Min, headerRect.Max);
        if (headerHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(headerRect.Min, headerRect.Max, headerHovered))
        {
            OpenDetail(ad.Id);
        }

        DrawMineActions(ad, nowUnix, card, headerHeight, pad, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawMineActions(AdDto ad, long nowUnix, Rect card, float headerHeight, float pad, float scale)
    {
        var busy = string.Equals(mineBusyAdId, ad.Id, StringComparison.Ordinal);
        var actionTop = card.Min.Y + headerHeight + Metrics.Space.Sm * scale;
        var actionHeight = MineActionRowHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var cursor = card.Min.X + pad;
        var right = card.Max.X - pad;

        if (busy)
        {
            LoadingPulse.Spinner(new Vector2(card.Center.X, actionTop + actionHeight * 0.5f), 9f * scale,
                ui.Accent);
            return;
        }

        if (ad.Status == AdStatuses.Hidden)
        {
            DrawMineDelete(ad, new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + 110f * scale, actionTop + actionHeight)));
            return;
        }

        var canRenew = ad.ExpiresAtUnix - nowUnix <= RenewWindowSeconds;
        if (canRenew)
        {
            var renewLabel = Loc.T(L.YellowPages.Renew);
            var renewWidth = Typography.Measure(renewLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale;
            var renewRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + renewWidth, actionTop + actionHeight));
            if (ui.PillButton(renewRect, renewLabel, true))
            {
                mineBusyAdId = ad.Id;
                store.Renew(ad.Id, _ => mineBusyAdId = null);
            }

            cursor = renewRect.Max.X + gap;
        }

        if (ad.Archetype == AdArchetypes.Place && ad.Status == AdStatuses.Live)
        {
            var open = ad.OpenUntilUnix > nowUnix;
            var openLabel = open ? Loc.T(L.YellowPages.CloseNow) : Loc.T(L.YellowPages.OpenNowAction);
            var openWidth = Typography.Measure(openLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale;
            var openRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + openWidth, actionTop + actionHeight));
            if (ui.PillButton(openRect, openLabel, !open))
            {
                mineBusyAdId = ad.Id;
                store.SetOpen(ad.Id, !open, 0, _ => mineBusyAdId = null);
            }

            cursor = openRect.Max.X + gap;
            if (open && musters.Mine is null && (ad.TerritoryId > 0 || ad.AddressNote.Length > 0))
            {
                var announceLabel = Loc.T(L.YellowPages.AnnounceMuster);
                var announceWidth = Typography.Measure(announceLabel, 0.9f, FontWeight.SemiBold).X + 30f * scale;
                if (cursor + announceWidth < right - 70f * scale)
                {
                    var announceRect = new Rect(new Vector2(cursor, actionTop),
                        new Vector2(cursor + announceWidth, actionTop + actionHeight));
                    if (ui.GhostButton(announceRect, announceLabel))
                    {
                        AnnounceOnMuster(ad, nowUnix);
                    }

                    cursor = announceRect.Max.X + gap;
                }
            }
        }

        var editLabel = Loc.T(L.YellowPages.EditAd);
        var editWidth = Typography.Measure(editLabel, 0.9f, FontWeight.SemiBold).X + 30f * scale;
        if (cursor + editWidth < right - 70f * scale)
        {
            var editRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + editWidth, actionTop + actionHeight));
            if (ui.GhostButton(editRect, editLabel))
            {
                StartEdit(ad);
                router.Push(YellowPagesRoute.Compose);
            }

            cursor = editRect.Max.X + gap;
        }

        var shareLabel = JustCopied(ad.Id) ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.ShareAd);
        var shareWidth = Typography.Measure(shareLabel, 0.9f, FontWeight.SemiBold).X + 34f * scale;
        if (cursor + shareWidth < right - 70f * scale)
        {
            var shareRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + shareWidth, actionTop + actionHeight));
            if (ui.GhostButton(shareRect, shareLabel))
            {
                Copy(ad.Id, AdShare.Compose(ad.Id));
            }
        }

        var deleteLabel = Loc.T(L.YellowPages.DeleteAd);
        var deleteWidth = Typography.Measure(deleteLabel, 0.9f, FontWeight.SemiBold).X + 30f * scale;
        DrawMineDelete(ad, new Rect(new Vector2(right - deleteWidth, actionTop),
            new Vector2(right, actionTop + actionHeight)));
    }

    private void AnnounceOnMuster(AdDto ad, long nowUnix)
    {
        var duration = (int)Math.Clamp((ad.OpenUntilUnix - nowUnix) / 60L, 30L, 480L);
        var request = new CreateMusterRequest(
            MusterCategories.Social,
            ad.Title,
            ad.TerritoryId,
            ad.MapId,
            ad.MapX,
            ad.MapY,
            ad.WorldId,
            ad.Ward,
            ad.Plot,
            0,
            ad.AddressNote,
            ad.Region,
            ad.DataCenterId,
            0,
            duration,
            0,
            false,
            true);
        mineBusyAdId = ad.Id;
        musters.Create(request, _ => mineBusyAdId = null);
    }

    private void DrawMineDelete(AdDto ad, Rect rect)
    {
        if (ui.DangerGhostButton(rect, Loc.T(L.YellowPages.DeleteAd)))
        {
            AskDeleteAd(ad);
        }
    }

    private void AskDeleteAd(AdDto ad)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.YellowPages.DeleteAd),
            Message = Loc.T(L.YellowPages.DeleteConfirm),
            ConfirmLabel = Loc.T(L.YellowPages.DeleteAd),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.YellowPages.Deleting),
            FailedMessage = Loc.T(L.YellowPages.DeleteFailed),
            Danger = true,
            ConfirmAsync = done => store.Delete(ad.Id, done),
        });
    }

    private string MineStatusText(AdDto ad, long nowUnix, out Vector4 color)
    {
        if (ad.Status == AdStatuses.Hidden)
        {
            color = theme.Danger;
            return Loc.T(L.YellowPages.HiddenStatus);
        }

        string status;
        if (ad.Status == AdStatuses.Expired || ad.ExpiresAtUnix <= nowUnix)
        {
            color = AppPalettes.YellowPages.MutedInk;
            status = Loc.T(L.YellowPages.Expired);
        }
        else if (ad.Archetype == AdArchetypes.Place && ad.OpenUntilUnix > nowUnix)
        {
            color = AdCard.OpenGreen;
            status = Loc.T(L.YellowPages.OpenClosesAt, TimeText.Clock(ad.OpenUntilUnix));
        }
        else
        {
            var remaining = ad.ExpiresAtUnix - nowUnix;
            color = remaining <= 86400L ? theme.Danger : ui.Accent;
            status = AdText.ExpiresLine(ad, nowUnix);
        }

        if (ad.Views > 0)
        {
            status = $"{status} · {Loc.T(L.YellowPages.ViewCount, ad.Views)}";
        }

        return ad.AllowInquiries ? status : $"{status} · {Loc.T(L.YellowPages.InquiriesClosed)}";
    }

    private void DrawSaved(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawTabTitle(area, Loc.T(L.YellowPages.SavedTitle), 0f, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var saved = store.Saved;
        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            if (saved.Length == 0)
            {
                if (store.SavedLoading && !store.SavedLoadedOnce)
                {
                    LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 90f * scale), 13f * scale, ui.Accent,
                        AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
                }
                else
                {
                    EmptyState.Draw(body, ui, FontAwesomeIcon.Heart, Loc.T(L.YellowPages.NoSavedTitle),
                        Loc.T(L.YellowPages.NoSavedHint));
                }
            }
            else
            {
                for (var index = 0; index < saved.Length; index++)
                {
                    var ad = saved[index];
                    var origin = ImGui.GetCursorScreenPos();
                    var width = ImGui.GetContentRegionAvail().X;
                    var height = AdCard.Height(ad, width, scale);
                    var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
                    if (ImGui.IsRectVisible(card.Min, card.Max)
                        && AdCard.Draw(card, ad, images, lodestone, theme, ui, nowUnix))
                    {
                        OpenDetail(ad.Id);
                    }

                    ImGui.SetCursorScreenPos(origin);
                    ImGui.Dummy(new Vector2(width, height + AdCard.Gap * scale));
                }

                if (store.SavedHasMore && !store.SavedLoading)
                {
                    var origin = ImGui.GetCursorScreenPos();
                    var width = ImGui.GetContentRegionAvail().X;
                    var label = Loc.T(L.YellowPages.LoadMore);
                    var buttonWidth = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 44f * scale;
                    var rect = new Rect(new Vector2(origin.X + (width - buttonWidth) * 0.5f, origin.Y),
                        new Vector2(origin.X + (width + buttonWidth) * 0.5f, origin.Y + 36f * scale));
                    if (ui.GhostButton(rect, label))
                    {
                        store.LoadMoreSaved();
                    }

                    ImGui.SetCursorScreenPos(origin);
                    ImGui.Dummy(new Vector2(width, 36f * scale + Metrics.Space.Sm * scale));
                }
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }
}
