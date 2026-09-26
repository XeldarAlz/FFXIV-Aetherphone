using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float MineRowHeight = 96f;
    private const float MineThumbSide = 60f;
    private const float MineStatGap = 8f;
    private const float MineMoreRadius = 18f;
    private const int MaxLiveAds = 3;
    private const long RenewWindowSeconds = 3L * 86400L;

    private static readonly TextStyle MineTitleStyle = TextStyles.Headline;
    private static readonly TextStyle MineStatusStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle MineStatStyle = TextStyles.Caption1;

    private string? mineBusyAdId;
    private string mineLiveLabel = string.Empty;
    private int mineLiveLabelCount = -1;

    private void DrawMine(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        DrawScreenHeader(area, Loc.T(L.YellowPages.YourAds), 0, false, false);
        DrawLiveCountPill(area, scale);
        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var mine = store.Mine;
        var nowUnix = NowUnix();
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (mine.Length == 0)
            {
                if (store.Syncing && !store.Primed)
                {
                    DrawEmptyState(listRect, Loc.T(L.Common.Loading), string.Empty);
                }
                else
                {
                    DrawEmptyState(listRect, Loc.T(L.YellowPages.NoAdsTitle), Loc.T(L.YellowPages.NoAdsHint));
                    DrawEmptyAction(listRect, Loc.T(L.YellowPages.PostAd), scale, StartCompose);
                }

                return;
            }

            for (var index = 0; index < mine.Length; index++)
            {
                DrawMineRow(mine[index], nowUnix, scale);
            }

            var hintOrigin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var pad = CellPadX * scale;
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(hintOrigin.X + pad, hintOrigin.Y + Metrics.Space.Md * scale),
                Loc.T(L.YellowPages.MineHint), Ink.MutedInk, TextStyles.Footnote, width - pad * 2f);
            ImGui.SetCursorScreenPos(hintOrigin);
            ImGui.Dummy(new Vector2(width, hintHeight + Metrics.Space.Xl * scale));
        }
    }

    private void DrawEmptyAction(Rect listRect, string label, float scale, Action onTap)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + 48f * scale;
        var top = listRect.Min.Y + 150f * scale;
        var rect = new Rect(new Vector2(listRect.Center.X - width * 0.5f, top),
            new Vector2(listRect.Center.X + width * 0.5f, top + 40f * scale));
        if (SocialPill.Accent(drawList, rect, label, Ink, TextStyles.SubheadlineEmphasized, rect.Height * 0.5f))
        {
            onTap();
        }
    }

    private void DrawLiveCountPill(Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var live = store.LiveMineCount;
        if (mineLiveLabelCount != live)
        {
            mineLiveLabelCount = live;
            mineLiveLabel = Loc.T(L.YellowPages.LiveCount, live, MaxLiveAds);
        }

        var pillWidth = YellowPagesKit.PillWidth(mineLiveLabel, scale, false, true);
        var pillMin = new Vector2(area.Max.X - CellPadX * scale - pillWidth,
            area.Min.Y + AppHeader.Height * scale * 0.5f - YellowPagesKit.PillHeight * scale * 0.5f);
        var full = live >= MaxLiveAds;
        YellowPagesKit.Pill(drawList, pillMin, mineLiveLabel, full ? Palette.WithAlpha(Ink.Danger, 0.18f) : Ink.AccentWash,
            full ? Ink.Danger : Ink.AccentLink, scale, string.Empty, true);
    }

    private void DrawMineRow(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = MineRowHeight * scale;
        var cell = FeedCell.Begin(drawList, height, Ink.HoverTint);
        var bounds = cell.Bounds;
        var pad = CellPadX * scale;
        var thumbSide = MineThumbSide * scale;
        var thumbMin = new Vector2(bounds.Min.X + pad, bounds.Center.Y - thumbSide * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSide, thumbSide);
        var rounding = 14f * scale;
        var texture = string.IsNullOrEmpty(ad.MediaUrl) ? null : images.Get(ad.MediaUrl);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, thumbMin, thumbMax, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            YellowPagesKit.Tile(drawList, thumbMin, thumbMax, YellowPagesKit.AccentOf(ad), AdCategories.Icon(ad.Category),
                rounding, 1.15f);
        }

        var moreRadius = MineMoreRadius * scale;
        var moreCenter = new Vector2(bounds.Max.X - pad - moreRadius, bounds.Center.Y);
        var textLeft = thumbMax.X + 12f * scale;
        var textRight = moreCenter.X - moreRadius - 8f * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var titleTop = bounds.Min.Y + 14f * scale;
        Marquee.DrawLeftAuto(drawList, new MarqueeId("yellowpages.mine.title.", ad.Id), ad.Title, textLeft, titleTop,
            textWidth, MineTitleStyle, Ink.TitleInk);
        var status = MineStatusText(ad, nowUnix, out var statusColor);
        var statusTop = titleTop + Typography.LineHeight(MineTitleStyle) + 3f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, statusTop), Typography.FitText(status, textWidth, MineStatusStyle),
            statusColor, MineStatusStyle);
        var statsTop = statusTop + Typography.LineHeight(MineStatusStyle) + 6f * scale;
        DrawMineStats(drawList, ad, textLeft, textRight, statsTop, scale);

        var busy = string.Equals(mineBusyAdId, ad.Id, StringComparison.Ordinal);
        if (busy)
        {
            LoadingPulse.Spinner(moreCenter, 9f * scale, Ink.Accent);
        }
        else
        {
            var moreExtent = new Vector2(moreRadius, moreRadius);
            var moreHovered = UiInteract.Hover(moreCenter - moreExtent, moreCenter + moreExtent);
            if (moreHovered)
            {
                drawList.AddCircleFilled(moreCenter, moreRadius, ImGui.GetColorU32(Ink.FieldFill), 32);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            PhoneIcon.Draw(drawList, moreCenter, PhoneIcons.DotsVertical, Ink.MutedInk, 20f * scale);
            HoverTooltip.Show(new Rect(moreCenter - moreExtent, moreCenter + moreExtent), Loc.T(L.YellowPages.MoreActions),
                HoverLabelSide.Above);
            if (UiInteract.Click(moreCenter - moreExtent, moreCenter + moreExtent, moreHovered))
            {
                OpenAdSheet(ad);
                FeedCell.End(drawList, cell, Ink.Hairline);
                return;
            }
        }

        if (cell.Tapped)
        {
            OpenDetail(ad.Id);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private void DrawMineStats(ImDrawListPtr drawList, AdDto ad, float left, float right, float top, float scale)
    {
        var centerY = top + Typography.LineHeight(MineStatStyle) * 0.5f;
        var cursorX = left;
        cursorX = DrawMineStat(drawList, cursorX, right, centerY, PhoneIcons.Eye, ad.Views.ToString(Loc.Culture), false,
            scale);
        var inquiryCount = inquiries.CountForAd(ad.Id);
        if (inquiryCount > 0)
        {
            var unread = inquiries.UnreadForAd(ad.Id);
            cursorX = DrawMineStat(drawList, cursorX, right, centerY, PhoneIcons.MessageCircle,
                Loc.T(L.YellowPages.InquiryCount, inquiryCount), unread > 0, scale);
        }

        if (ad.Wanted)
        {
            DrawMineStat(drawList, cursorX, right, centerY, PhoneIcons.Search, Loc.T(L.YellowPages.WantedChip), false,
                scale);
        }
    }

    private static float DrawMineStat(ImDrawListPtr drawList, float left, float right, float centerY, string glyph,
        string label, bool highlighted, float scale)
    {
        var glyphSize = 13f * scale;
        var available = right - left - glyphSize - 4f * scale;
        if (available < 24f * scale)
        {
            return left;
        }

        var ink = highlighted ? Ink.AccentLink : Ink.MutedInk;
        PhoneIcon.Draw(drawList, new Vector2(left + glyphSize * 0.5f, centerY), glyph, ink, glyphSize);
        var fitted = Typography.FitText(label, available, MineStatStyle);
        var size = Typography.Measure(fitted, MineStatStyle);
        Typography.Draw(drawList, new Vector2(left + glyphSize + 4f * scale, centerY - size.Y * 0.5f), fitted, ink,
            MineStatStyle);
        return left + glyphSize + 4f * scale + size.X + MineStatGap * scale * 1.5f;
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

    private void AskDeleteAd(AdDto ad)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.YellowPages.DeleteAd),
            Message = Loc.T(L.YellowPages.DeleteConfirm),
            ConfirmLabel = Loc.T(L.YellowPages.DeleteAd),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            BusyLabel = Loc.T(L.YellowPages.Deleting),
            FailedMessage = Loc.T(L.YellowPages.DeleteFailed),
            Danger = true,
            ConfirmAsync = done => store.Delete(ad.Id, ok =>
            {
                if (ok && router.Current.Screen == YellowPagesScreen.Detail && router.Current.Id == ad.Id)
                {
                    router.Pop(false);
                }

                done(ok);
            }),
        });
    }

    private string MineStatusText(AdDto ad, long nowUnix, out Vector4 color)
    {
        if (ad.Status == AdStatuses.Hidden)
        {
            color = Ink.Danger;
            return Loc.T(L.YellowPages.HiddenStatus);
        }

        string status;
        if (ad.Status == AdStatuses.Expired || ad.ExpiresAtUnix <= nowUnix)
        {
            color = Ink.MutedInk;
            status = Loc.T(L.YellowPages.Expired);
        }
        else if (ad.Archetype == AdArchetypes.Place && ad.OpenUntilUnix > nowUnix)
        {
            color = YellowPagesKit.OpenGreen;
            status = Loc.T(L.YellowPages.OpenClosesAt, TimeText.Clock(ad.OpenUntilUnix));
        }
        else
        {
            var remaining = ad.ExpiresAtUnix - nowUnix;
            color = remaining <= 86400L ? Ink.Danger : Ink.AccentLink;
            status = AdText.ExpiresLine(ad, nowUnix);
            if (remaining <= RenewWindowSeconds)
            {
                status = $"{status} · {Loc.T(L.YellowPages.RenewAvailable)}";
            }
        }

        return ad.AllowInquiries ? status : $"{status} · {Loc.T(L.YellowPages.InquiriesClosed)}";
    }

    private void DrawSaved(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        DrawScreenHeader(area, Loc.T(L.YellowPages.SavedTitle), 0, false, false);
        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var saved = store.Saved;
        var nowUnix = NowUnix();
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (saved.Length == 0)
            {
                if (store.SavedLoading && !store.SavedLoadedOnce)
                {
                    DrawEmptyState(listRect, Loc.T(L.Common.Loading), string.Empty);
                }
                else
                {
                    DrawEmptyState(listRect, Loc.T(L.YellowPages.NoSavedTitle), Loc.T(L.YellowPages.NoSavedHint));
                }

                return;
            }

            var context = CardContext(nowUnix);
            for (var index = 0; index < saved.Length; index++)
            {
                if (AdCard.Draw(saved[index], context))
                {
                    OpenDetail(saved[index].Id);
                }
            }

            if (store.SavedHasMore && !store.SavedLoading && InfiniteScroll.ReachedBottom())
            {
                store.LoadMoreSaved();
            }

            if (store.SavedLoading)
            {
                InfiniteScroll.DrawLoadingRow(listRect.Center.X, Ink.MutedInk);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }
}
