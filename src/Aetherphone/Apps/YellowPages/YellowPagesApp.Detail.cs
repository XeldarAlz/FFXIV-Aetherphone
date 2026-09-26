using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Translation;
using Aetherphone.Core.Venues;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float HeroHeight = 268f;
    private const float HeroBannerHeight = 176f;
    private const float HeroParallax = 20f;
    private const float HeroScrimShare = 0.6f;
    private const float HeroInset = 16f;
    private const float PhotoFadeSeconds = 0.18f;
    private const float PosterRowHeight = 66f;
    private const float PosterAvatarRadius = 21f;
    private const float ActionBarHeight = 74f;
    private const float CtaHeight = 48f;
    private const float CircleButtonRadius = 23f;
    private const float SectionRowHeight = 28f;
    private const float LocationButtonHeight = 40f;
    private const float TagRowHeight = 28f;
    private const float MetaRowHeight = 30f;
    private const int HeroTitleMaxLines = 2;

    private static readonly TextStyle HeroTitleStyle = TextStyles.Title2;
    private static readonly TextStyle DetailBodyStyle = TextStyles.Body;
    private static readonly TextStyle SectionRowStyle = TextStyles.Subheadline;
    private static readonly TextStyle SectionRowEmphasis = TextStyles.SubheadlineEmphasized;
    private static readonly TextStyle CtaStyle = TextStyles.Headline;
    private static readonly TextStyle DetailMetaStyle = TextStyles.Footnote;

    private readonly ActionSheet.Item[] adSheetItems = new ActionSheet.Item[8];
    private readonly int[] adSheetActions = new int[8];
    private int adSheetCount;
    private string? adSheetAdId;
    private string adSheetTitle = string.Empty;
    private string? detailFetchId;
    private AdDto? detailFetched;
    private bool detailLoading;
    private bool saveBusy;
    private int detailPhotoIndex;
    private int detailPhotoPrevious;
    private float detailPhotoFade = 1f;

    private const int SheetEdit = 0;
    private const int SheetRenew = 1;
    private const int SheetOpen = 2;
    private const int SheetClose = 3;
    private const int SheetAnnounce = 4;
    private const int SheetShare = 5;
    private const int SheetDelete = 6;
    private const int SheetCopyDetails = 7;
    private const int SheetReport = 8;
    private const int SheetSave = 9;

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        saveBusy = false;
        detailPhotoIndex = 0;
        detailPhotoPrevious = 0;
        detailPhotoFade = 1f;
    }

    private void DrawDetail(Rect area, string adId)
    {
        EnsureDetailFetch(adId);
        var ad = ResolveAd(adId);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var title = ad is null ? DisplayName : Loc.T(AdCategories.Label(ad.Category));
        DrawScreenHeader(area, title, ad is null ? 0 : 1);
        if (ad is not null && DrawHeaderIcon(drawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Dots,
                Loc.T(L.YellowPages.MoreActions)))
        {
            OpenAdSheet(ad);
        }

        DrawHairline(drawList, area.Min.X, area.Max.X, area.Min.Y + AppHeader.Height * scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        if (ad is null)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            if (detailLoading)
            {
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, Ink.Accent,
                    Ink.MutedInk, Loc.T(L.Common.Loading));
                return;
            }

            DrawEmptyState(body, Loc.T(L.YellowPages.UnavailableTitle), Loc.T(L.YellowPages.UnavailableHint));
            return;
        }

        var nowUnix = NowUnix();
        var barRect = new Rect(new Vector2(area.Min.X, area.Max.Y - ActionBarHeight * scale), area.Max);
        var listRect = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, barRect.Min.Y));
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            DrawDetailHero(ad, nowUnix, scale);
            DrawDetailMeta(ad, nowUnix, scale);
            DrawPosterRow(ad, scale);
            DrawDetailBody(ad, scale);
            DrawTagRow(ad, scale);
            if (ad.Archetype == AdArchetypes.Place)
            {
                DrawScheduleSection(ad, nowUnix, scale);
                DrawLocationSection(ad, scale);
            }
            else if (ad.Archetype == AdArchetypes.Call)
            {
                DrawCallSection(ad, scale);
            }
            else if (AdCategories.IsLinkOnly(ad.Category))
            {
                DrawLinkSection(ad, scale);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }

        DrawDetailActionBar(ad, barRect, scale);
    }

    private AdDto? ResolveAd(string adId)
    {
        var fetched = detailFetched;
        if (fetched is not null && fetched.Id == adId)
        {
            return fetched;
        }

        var mine = store.Mine;
        for (var index = 0; index < mine.Length; index++)
        {
            if (mine[index].Id == adId)
            {
                return mine[index];
            }
        }

        var directory = store.Directory;
        for (var index = 0; index < directory.Length; index++)
        {
            if (directory[index].Id == adId)
            {
                return directory[index];
            }
        }

        var saved = store.Saved;
        for (var index = 0; index < saved.Length; index++)
        {
            if (saved[index].Id == adId)
            {
                return saved[index];
            }
        }

        return null;
    }

    private void EnsureDetailFetch(string adId)
    {
        if (string.Equals(detailFetchId, adId, StringComparison.Ordinal))
        {
            return;
        }

        detailFetchId = adId;
        detailFetched = null;
        detailLoading = true;
        store.FetchDetail(adId, ad =>
        {
            detailFetched = ad;
            detailLoading = false;
        });
    }

    private void DrawDetailHero(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var photos = PostMedia.Photos(ad.MediaUrls, ad.MediaUrl);
        var height = (photos.Length > 0 ? HeroHeight : HeroBannerHeight) * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var accent = YellowPagesKit.AccentOf(ad);
        if (photos.Length == 0)
        {
            YellowPagesKit.CoverFallback(drawList, rect.Min, rect.Max, 0f, accent, ad.Category, 2.4f);
        }
        else
        {
            DrawHeroPhotos(drawList, rect, photos, scale);
        }

        YellowPagesKit.BottomScrim(drawList, rect.Min, rect.Max, HeroScrimShare);
        var inset = HeroInset * scale;
        var status = YellowPagesKit.StatusOf(ad, nowUnix);
        var cursorX = rect.Min.X + inset;
        var pillTop = rect.Min.Y + inset;
        if (status.Live)
        {
            cursorX += YellowPagesKit.LivePill(drawList, new Vector2(cursorX, pillTop), Loc.T(L.YellowPages.OpenNow),
                scale) + 6f * scale;
        }

        if (ad.Wanted)
        {
            cursorX += YellowPagesKit.Pill(drawList, new Vector2(cursorX, pillTop), Loc.T(L.YellowPages.WantedChip),
                Palette.WithAlpha(YellowPagesKit.WantedTint, 0.85f), YellowPagesKit.White, scale, string.Empty, true)
                + 6f * scale;
        }

        if (ad.AfterDark)
        {
            YellowPagesKit.Pill(drawList, new Vector2(cursorX, pillTop), Loc.T(L.YellowPages.AfterDarkChip),
                Palette.WithAlpha(YellowPagesKit.AfterDarkPink, 0.85f), YellowPagesKit.White, scale, string.Empty, true);
        }

        var textWidth = width - inset * 2f;
        var titleLines = Math.Min(Typography.CountWrappedLines(ad.Title, HeroTitleStyle, textWidth), HeroTitleMaxLines);
        var titleHeight = titleLines * Typography.LineHeight(HeroTitleStyle) * 1.15f;
        var dotsSpace = photos.Length > 1 ? 14f * scale : 0f;
        var titleTop = rect.Max.Y - inset - dotsSpace - titleHeight;
        DrawHeroTitle(drawList, ad.Title, new Vector2(rect.Min.X + inset, titleTop), textWidth, titleLines);
        if (photos.Length > 1)
        {
            PhotoCarousel.DrawDots(drawList, new Vector2(rect.Center.X, rect.Max.Y - 9f * scale), photos.Length,
                detailPhotoIndex, width * 0.6f, YellowPagesKit.White);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawHeroTitle(ImDrawListPtr drawList, string title, Vector2 topLeft, float width, int lines)
    {
        using (Plugin.Fonts.Push(HeroTitleStyle.Scale, HeroTitleStyle.Weight))
        {
            Plugin.Fonts.NoticeText(title);
            var wrapped = Typography.WrapCurrent(title, width);
            var count = Math.Min(wrapped.Length, lines);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing() * 1.05f;
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(YellowPagesKit.White);
            var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f));
            for (var index = 0; index < count; index++)
            {
                var position = new Vector2(topLeft.X, topLeft.Y + index * lineHeight);
                drawList.AddText(font, fontSize, position + new Vector2(0f, 1.5f), shadow, wrapped[index]);
                drawList.AddText(font, fontSize, position, packed, wrapped[index]);
            }
        }
    }

    private void DrawHeroPhotos(ImDrawListPtr drawList, Rect rect, string[] photos, float scale)
    {
        if (detailPhotoIndex >= photos.Length)
        {
            detailPhotoIndex = 0;
            detailPhotoPrevious = 0;
            detailPhotoFade = 1f;
        }

        var url = photos[detailPhotoIndex];
        var texture = images.Get(url);
        if (texture is null)
        {
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Ink.FieldFill));
            LoadingPulse.Spinner(rect.Center, 10f * scale, Ink.Accent);
            return;
        }

        if (detailPhotoFade < 1f)
        {
            detailPhotoFade = MathF.Min(1f, detailPhotoFade + ImGui.GetIO().DeltaTime / PhotoFadeSeconds);
        }

        var range = HeroParallax * scale;
        var shift = Math.Clamp(ImGui.GetScrollY() * 0.30f, 0f, range);
        var imageMin = new Vector2(rect.Min.X, rect.Min.Y - range + shift);
        var imageMax = new Vector2(rect.Max.X, imageMin.Y + rect.Height + range);
        drawList.PushClipRect(rect.Min, rect.Max, true);
        if (detailPhotoFade < 1f && detailPhotoPrevious < photos.Length)
        {
            DrawHeroImage(drawList, images.Get(photos[detailPhotoPrevious]), imageMin, imageMax, 1f);
        }

        DrawHeroImage(drawList, texture, imageMin, imageMax, detailPhotoFade);
        drawList.PopClipRect();

        var inset = HeroInset * scale;
        var expandCenter = new Vector2(rect.Max.X - inset - YellowPagesKit.GlassButtonRadius * scale,
            rect.Min.Y + inset + YellowPagesKit.GlassButtonRadius * scale);
        var expandExtent = new Vector2(YellowPagesKit.GlassButtonRadius * scale, YellowPagesKit.GlassButtonRadius * scale);
        var overExpand = UiInteract.Hover(expandCenter - expandExtent, expandCenter + expandExtent, false);
        if (YellowPagesKit.GlassButton(drawList, expandCenter, PhoneIcons.Photo, Loc.T(L.YellowPages.ViewPhoto), scale))
        {
            var viewerUrl = url;
            photoViewer.Open(this, () => images.Get(viewerUrl));
            return;
        }

        var touchMax = new Vector2(rect.Max.X, rect.Max.Y - rect.Height * 0.4f);
        if (photos.Length > 1)
        {
            var midX = rect.Center.X;
            var leftHovered = !overExpand && UiInteract.Hover(rect.Min, new Vector2(midX, touchMax.Y), false);
            var rightHovered = !overExpand && UiInteract.Hover(new Vector2(midX, rect.Min.Y), touchMax, false);
            if (leftHovered || rightHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, new Vector2(midX, touchMax.Y), leftHovered))
            {
                StepHeroPhoto(photos.Length, -1);
            }
            else if (UiInteract.Click(new Vector2(midX, rect.Min.Y), touchMax, rightHovered))
            {
                StepHeroPhoto(photos.Length, 1);
            }

            return;
        }

        var hovered = !overExpand && UiInteract.Hover(rect.Min, touchMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, touchMax, hovered))
        {
            var viewerUrl = url;
            photoViewer.Open(this, () => images.Get(viewerUrl));
        }
    }

    private static void DrawHeroImage(ImDrawListPtr drawList,
        Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture, Vector2 min, Vector2 max, float alpha)
    {
        if (texture is null)
        {
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - min.X, max.Y - min.Y);
        drawList.AddImage(texture.Handle, min, max, uv0, uv1, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private void StepHeroPhoto(int count, int step)
    {
        detailPhotoPrevious = detailPhotoIndex;
        detailPhotoIndex = (detailPhotoIndex + count + step) % count;
        detailPhotoFade = 0f;
    }

    private void DrawDetailMeta(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var right = origin.X + width - pad;
        var status = YellowPagesKit.StatusOf(ad, nowUnix);
        var rowCenter = origin.Y + Metrics.Space.Md * scale + MetaRowHeight * scale * 0.5f;
        var cursorX = origin.X + pad;
        var rows = 1;
        if (status.Label.Length > 0 && !status.Live)
        {
            var accent = YellowPagesKit.AccentOf(ad);
            cursorX += YellowPagesKit.Pill(drawList,
                new Vector2(cursorX, rowCenter - YellowPagesKit.PillHeight * scale * 0.5f), status.Label,
                Palette.WithAlpha(accent, 0.20f), Palette.Lighten(accent, 0.30f), scale, string.Empty, true)
                + SocialChrome.MetaChipGap * scale;
        }
        else if (status.Live)
        {
            var state = AdText.OpenState(ad, nowUnix);
            var closes = state.ClosesAtUnix > 0
                ? Loc.T(L.YellowPages.ClosesAt, TimeText.Clock(state.ClosesAtUnix))
                : Loc.T(L.YellowPages.OpenNow);
            SocialChrome.DrawMetaChip(drawList, ref cursorX, right, rowCenter, PhoneIcons.Clock, closes, Ink,
                DetailMetaStyle);
        }

        if (ad.Archetype == AdArchetypes.Service && ad.Turnaround.Length > 0)
        {
            SocialChrome.DrawMetaChip(drawList, ref cursorX, right, rowCenter, PhoneIcons.Refresh, ad.Turnaround, Ink,
                DetailMetaStyle);
        }

        var secondRow = rowCenter + MetaRowHeight * scale + 4f * scale;
        var statsX = origin.X + pad;
        rows++;
        SocialChrome.DrawMetaChip(drawList, ref statsX, right, secondRow, PhoneIcons.Eye,
            Loc.T(L.YellowPages.ViewCount, ad.Views), Ink, DetailMetaStyle);
        SocialChrome.DrawMetaChip(drawList, ref statsX, right, secondRow, PhoneIcons.Clock,
            AdText.RemainingShort(ad, nowUnix), Ink, DetailMetaStyle);
        var world = AdText.WorldLine(ad);
        if (world.Length > 0)
        {
            SocialChrome.DrawMetaChip(drawList, ref statsX, right, secondRow, PhoneIcons.World, world, Ink,
                DetailMetaStyle);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Metrics.Space.Md * scale + rows * MetaRowHeight * scale + 4f * scale
            + Metrics.Space.Sm * scale));
    }

    private void DrawPosterRow(AdDto ad, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var height = PosterRowHeight * scale;
        var cell = FeedCell.Begin(drawList, height, Ink.HoverTint, false);
        var pad = CellPadX * scale;
        var avatarRadius = PosterAvatarRadius * scale;
        var avatarCenter = new Vector2(cell.Bounds.Min.X + pad + avatarRadius, cell.Bounds.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, AdText.Identity(ad), string.Empty,
            ad.OwnerAvatarUrl.Length > 0 ? ad.OwnerAvatarUrl : null, images, lodestone, 0.9f, 48, 1f,
            Frames.Of(ad.OwnerFrameId));
        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textWidth = cell.Bounds.Max.X - pad - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var subHeight = Typography.LineHeight(DetailMetaStyle);
        var blockTop = cell.Bounds.Center.Y - (nameHeight + subHeight + 3f * scale) * 0.5f;
        UserName.DrawAuto(drawList, "yellowpages.owner." + ad.Id, SocialIdentity.Name(ad.OwnerName, ad.OwnerHandle),
            ad.OwnerBadges, ad.OwnerBadgeIds, textLeft, blockTop, textWidth, TextStyles.Headline, Ink.TitleInk, theme);
        var handle = ad.OwnerHandle.Length > 0 ? $"@{ad.OwnerHandle}" : string.Empty;
        var renewed = Loc.T(L.YellowPages.RenewedAgo, TimeText.Ago(ad.RenewedAtUnix));
        var line = handle.Length > 0 ? $"{handle} · {renewed}" : renewed;
        Typography.Draw(drawList, new Vector2(textLeft, blockTop + nameHeight + 3f * scale),
            Typography.FitText(line, textWidth, DetailMetaStyle), Ink.MutedInk, DetailMetaStyle);
        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private void DrawDetailBody(AdDto ad, float scale)
    {
        if (ad.Body.Length == 0)
        {
            return;
        }

        var adKey = new TranslationKey(TranslationSurface.Ad, ad.Id);
        var bodyText = translation.View(adKey, ad.Body).Text;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var textTop = origin.Y + Metrics.Space.Md * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + pad, textTop), bodyText, Ink.BodyInk,
            DetailBodyStyle, width - pad * 2f);
        var linkHeight = TranslateLink.Height(translation, adKey, ad.Lang, scale);
        if (linkHeight > 0f)
        {
            TranslateLink.Draw(translation, confirm, adKey, ad.Lang, ad.Body,
                new Vector2(origin.X + pad, textTop + height), width - pad * 2f, Ink.MutedInk, Ink.AccentLink, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Metrics.Space.Md * scale + height + linkHeight + Metrics.Space.Sm * scale));
    }

    private void DrawTagRow(AdDto ad, float scale)
    {
        if (ad.Tags.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var gap = Metrics.Space.Xs * scale;
        var rowHeight = TagRowHeight * scale;
        var left = origin.X + pad;
        var right = origin.X + width - pad;
        var cursorX = left;
        var lineTop = origin.Y + Metrics.Space.Xs * scale;
        for (var index = 0; index < ad.Tags.Length; index++)
        {
            var label = "#" + ad.Tags[index];
            var pillWidth = YellowPagesKit.PillWidth(label, scale, false);
            if (cursorX + pillWidth > right && cursorX > left)
            {
                cursorX = left;
                lineTop += rowHeight + gap;
            }

            YellowPagesKit.Pill(drawList, new Vector2(cursorX, lineTop), label, Ink.ChipFill, Ink.AccentLink, scale,
                string.Empty, false, Ink.ChipStroke);
            cursorX += pillWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, lineTop - origin.Y + rowHeight + Metrics.Space.Md * scale));
    }

    private void DrawScheduleSection(AdDto ad, long nowUnix, float scale)
    {
        if (ad.Schedule.Length == 0)
        {
            return;
        }

        DrawSectionLabel(Loc.T(L.YellowPages.ScheduleYourTime), Metrics.Space.Xs);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var rowHeight = SectionRowHeight * scale;
        var todayLocal = (int)DateTime.Now.DayOfWeek;
        var dayNames = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var index = 0; index < ad.Schedule.Length; index++)
        {
            var slot = ad.Schedule[index];
            var rowTop = origin.Y + index * rowHeight;
            AdText.ToLocalSlot(slot, out var localDay, out _);
            var startUnix = AdText.NextOccurrenceUnix(slot, nowUnix);
            var range = $"{TimeText.Clock(startUnix)} - {TimeText.Clock(startUnix + slot.DurationMinutes * 60L)}";
            var isToday = localDay == todayLocal;
            if (isToday)
            {
                drawList.AddRectFilled(new Vector2(origin.X, rowTop), new Vector2(origin.X + width, rowTop + rowHeight),
                    ImGui.GetColorU32(Palette.WithAlpha(YellowPagesKit.OpenGreen, 0.10f)));
                drawList.AddRectFilled(new Vector2(origin.X, rowTop), new Vector2(origin.X + 3f * scale, rowTop + rowHeight),
                    ImGui.GetColorU32(Palette.WithAlpha(YellowPagesKit.OpenGreen, 0.9f)));
            }

            var style = isToday ? SectionRowEmphasis : SectionRowStyle;
            var lineHeight = Typography.LineHeight(style);
            var textTop = rowTop + (rowHeight - lineHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(origin.X + pad, textTop), dayNames[localDay],
                isToday ? YellowPagesKit.OpenGreen : Ink.MutedInk, style);
            var rangeSize = Typography.Measure(range, style);
            Typography.Draw(drawList, new Vector2(origin.X + width - pad - rangeSize.X, textTop), range,
                isToday ? YellowPagesKit.OpenGreen : Ink.BodyInk, style);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ad.Schedule.Length * rowHeight + Metrics.Space.Sm * scale));
    }

    private void DrawLocationSection(AdDto ad, float scale)
    {
        var hasAddress = ad.Ward > 0 || ad.TerritoryId > 0 || ad.AddressNote.Length > 0;
        if (!hasAddress)
        {
            return;
        }

        DrawSectionLabel(Loc.T(L.YellowPages.WhereSection), Metrics.Space.Xs);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var innerWidth = width - pad * 2f;
        var cursorY = origin.Y;
        cursorY += DrawLocationLine(drawList, ad.AddressNote, new Vector2(origin.X + pad, cursorY), innerWidth,
            SectionRowEmphasis, Ink.TitleInk, scale);
        cursorY += DrawLocationLine(drawList, AdText.PlaceLine(ad), new Vector2(origin.X + pad, cursorY), innerWidth,
            SectionRowStyle, Ink.BodyInk, scale);
        if (ad.Ward > 0 && ad.Plot > 0)
        {
            cursorY += DrawLocationLine(drawList, Loc.T(L.YellowPages.WardPlot, ad.Ward, ad.Plot),
                new Vector2(origin.X + pad, cursorY), innerWidth, SectionRowStyle, Ink.BodyInk, scale);
        }

        cursorY += Metrics.Space.Sm * scale;
        var gap = Metrics.Space.Sm * scale;
        var buttonHeight = LocationButtonHeight * scale;
        var hasMap = ad.MapId != 0;
        var slots = hasMap ? 2 : 1;
        var slotWidth = (innerWidth - gap * (slots - 1)) / slots;
        var cursorX = origin.X + pad;
        if (hasMap)
        {
            var flagRect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + slotWidth, cursorY + buttonHeight));
            if (DrawFlatPill(drawList, flagRect, Loc.T(L.YellowPages.FlagOnMap)))
            {
                var location = AdText.Location(ad);
                LocationShare.OpenMap(in location);
            }

            cursorX += slotWidth + gap;
        }

        var copyRect = new Rect(new Vector2(cursorX, cursorY), new Vector2(cursorX + slotWidth, cursorY + buttonHeight));
        var copyLabel = JustCopied("detail") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.CopyDetails);
        if (DrawFlatPill(drawList, copyRect, copyLabel))
        {
            Copy("detail", BuildCopySummary(ad));
        }

        cursorY += buttonHeight;
        if (CanTravelTo(ad))
        {
            cursorY += gap;
            var travelRect = new Rect(new Vector2(origin.X + pad, cursorY),
                new Vector2(origin.X + width - pad, cursorY + buttonHeight));
            var travelLabel = JustCopied("travel") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.Travel);
            if (SocialPill.Accent(drawList, travelRect, travelLabel, Ink, TextStyles.SubheadlineEmphasized,
                    buttonHeight * 0.5f))
            {
                TravelTo(ad);
            }

            cursorY += buttonHeight;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Md * scale));
    }

    private static float DrawLocationLine(ImDrawListPtr drawList, string text, Vector2 topLeft, float width,
        in TextStyle style, Vector4 ink, float scale)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        var fitted = Typography.FitText(text, width, style);
        Typography.Draw(drawList, topLeft, fitted, ink, style);
        return Typography.LineHeight(style) + 4f * scale;
    }

    private bool DrawFlatPill(ImDrawListPtr drawList, Rect rect, string label) =>
        SocialPill.Flat(drawList, rect, label, Ink.ButtonFill, Ink.ButtonHover, Ink.ChipStroke, Ink.TitleInk,
            TextStyles.SubheadlineEmphasized, rect.Height * 0.5f);

    private void DrawCallSection(AdDto ad, float scale)
    {
        if (ad.Requirements.Length == 0 && ad.SlotsLine.Length == 0)
        {
            return;
        }

        DrawSectionLabel(Loc.T(ad.Wanted ? L.YellowPages.WantedCallSection : L.YellowPages.CallSection),
            Metrics.Space.Xs);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        var cursorY = origin.Y;
        if (ad.SlotsLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(origin.X + pad, cursorY),
                Typography.FitText(ad.SlotsLine, width - pad * 2f, TextStyles.BodyEmphasized), Ink.TitleInk,
                TextStyles.BodyEmphasized);
            cursorY += Typography.LineHeight(TextStyles.BodyEmphasized) + 6f * scale;
        }

        if (ad.Requirements.Length > 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X + pad, cursorY), ad.Requirements, Ink.BodyInk,
                SectionRowStyle, width - pad * 2f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Md * scale));
    }

    private void DrawLinkSection(AdDto ad, float scale)
    {
        if (ad.LinkUrl.Length == 0)
        {
            return;
        }

        DrawSectionLabel(Loc.T(L.YellowPages.ModLinkLabel), Metrics.Space.Xs);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CellPadX * scale;
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y),
            Typography.FitText(LinkHost(ad.LinkUrl), width - pad * 2f, SectionRowStyle), Ink.BodyInk, SectionRowStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Typography.LineHeight(SectionRowStyle) + Metrics.Space.Md * scale));
    }

    private static string LinkHost(string url)
    {
        if (url.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return string.Empty;
        }

        return parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? parsed.Host[4..] : parsed.Host;
    }

    private void DrawDetailActionBar(AdDto ad, Rect bar, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        PaintBarBackdrop(drawList, bar);
        DrawHairline(drawList, bar.Min.X, bar.Max.X, bar.Min.Y + 1f);
        var pad = CellPadX * scale;
        var centerY = bar.Min.Y + (bar.Height - CtaHeight * scale) * 0.5f + CtaHeight * scale * 0.5f;
        var circleRadius = CircleButtonRadius * scale;
        var gap = Metrics.Space.Sm * scale;
        var cursorRight = bar.Max.X - pad;
        var mine = IsMineAd(ad.Id);
        if (DrawCircleButton(drawList, new Vector2(cursorRight - circleRadius, centerY), circleRadius, PhoneIcons.Share,
                Loc.T(L.YellowPages.ShareAd), false, scale))
        {
            Copy("share", AdShare.Compose(ad.Id));
        }

        cursorRight -= circleRadius * 2f + gap;
        if (!mine)
        {
            var saveGlyph = ad.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark;
            var saveLabel = ad.Saved ? Loc.T(L.YellowPages.Unsave) : Loc.T(L.YellowPages.Save);
            if (DrawCircleButton(drawList, new Vector2(cursorRight - circleRadius, centerY), circleRadius, saveGlyph,
                    saveLabel, ad.Saved, scale) && !saveBusy)
            {
                ToggleSaved(ad);
            }

            cursorRight -= circleRadius * 2f + gap;
        }

        var hasLink = AdCategories.IsLinkOnly(ad.Category) && ad.LinkUrl.Length > 0;
        if (hasLink && !mine && ad.AllowInquiries)
        {
            if (DrawCircleButton(drawList, new Vector2(cursorRight - circleRadius, centerY), circleRadius,
                    PhoneIcons.MessageCircle, Loc.T(L.YellowPages.InquireAction), false, scale))
            {
                OpenInquiryFor(ad);
            }

            cursorRight -= circleRadius * 2f + gap;
        }

        var ctaRect = new Rect(new Vector2(bar.Min.X + pad, centerY - CtaHeight * scale * 0.5f),
            new Vector2(cursorRight, centerY + CtaHeight * scale * 0.5f));
        if (mine)
        {
            if (SocialPill.Accent(drawList, ctaRect, Loc.T(L.YellowPages.ManageAction), Ink, CtaStyle,
                    ctaRect.Height * 0.5f))
            {
                OpenAdSheet(ad);
            }

            return;
        }

        if (hasLink)
        {
            var linkLabel = JustCopied("modlink") ? Loc.T(L.YellowPages.ModLinkCopied) : Loc.T(L.YellowPages.ModLinkAction);
            if (SocialPill.Accent(drawList, ctaRect, linkLabel, Ink, CtaStyle, ctaRect.Height * 0.5f))
            {
                OpenModLink(ad);
            }

            return;
        }

        if (!ad.AllowInquiries)
        {
            SocialPill.Flat(drawList, ctaRect, Loc.T(L.YellowPages.InquiriesClosed), Ink.ChipFill, Ink.ChipFill,
                Ink.ChipStroke, Ink.MutedInk, CtaStyle, ctaRect.Height * 0.5f);
            HoverTooltip.Show(ctaRect, Loc.T(L.YellowPages.InquiriesClosedHint), HoverLabelSide.Above);
            return;
        }

        var existing = inquiries.ThreadForAd(ad.Id);
        var label = existing is null ? Loc.T(L.YellowPages.InquireAction) : Loc.T(L.YellowPages.OpenConversation);
        if (SocialPill.Accent(drawList, ctaRect, label, Ink, CtaStyle, ctaRect.Height * 0.5f))
        {
            OpenInquiryFor(ad);
        }
    }

    private bool DrawCircleButton(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, string tooltip,
        bool highlighted, float scale)
    {
        var extent = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - extent, center + extent);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(highlighted ? Ink.AccentWash : hovered ? Ink.ButtonHover : Ink.ButtonFill), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(highlighted ? Palette.WithAlpha(Ink.AccentLink, 0.6f) : Ink.ChipStroke),
            40, 1f);
        PhoneIcon.Draw(drawList, center, glyph, highlighted ? Ink.AccentLink : Ink.TitleInk, 21f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - extent, center + extent), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    private void ToggleSaved(AdDto ad)
    {
        saveBusy = true;
        var next = !ad.Saved;
        store.SetSaved(ad.Id, next, _ =>
        {
            saveBusy = false;
            detailFetched = detailFetched is { } fetched && fetched.Id == ad.Id
                ? fetched with { Saved = next }
                : detailFetched;
        });
    }

    private static bool CanTravelTo(AdDto ad)
    {
        if (ad.WorldId == 0)
        {
            return false;
        }

        var currentWorldId = MusterWorlds.CurrentWorldId();
        return currentWorldId != 0 && currentWorldId != (uint)ad.WorldId;
    }

    private void TravelTo(AdDto ad)
    {
        var worldName = LocationShare.WorldName((uint)ad.WorldId);
        if (worldName.Length == 0)
        {
            return;
        }

        var outcome = LifestreamBridge.TravelToWorld((uint)ad.WorldId, worldName);
        lifestreamAvailable = outcome != LifestreamOutcome.NotInstalled;
        if (outcome == LifestreamOutcome.Started)
        {
            return;
        }

        Copy("travel", LifestreamBridge.TravelCommand(worldName));
    }

    private void OpenModLink(AdDto ad)
    {
        Copy("modlink", ad.LinkUrl);
        Windows.UrlActions.AskThenOpen(ad.LinkUrl,
            exception => AepLog.Warning(exception, "[YellowPages] mod link failed"));
    }

    private bool IsMineAd(string adId)
    {
        var mine = store.Mine;
        for (var index = 0; index < mine.Length; index++)
        {
            if (mine[index].Id == adId)
            {
                return true;
            }
        }

        return false;
    }

    private void OpenReport(string adId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.YellowPages.ReportTitle),
            Submit = (reason, done) => SubmitReport(adId, reason, done),
        });
    }

    private void OpenAdSheet(AdDto ad)
    {
        adSheetAdId = ad.Id;
        adSheetTitle = ad.Title;
        adSheetCount = 0;
        var nowUnix = NowUnix();
        if (IsMineAd(ad.Id))
        {
            if (ad.Status != AdStatuses.Hidden)
            {
                AddSheetItem(SheetEdit, Loc.T(L.YellowPages.EditAd), PhoneIcons.Pencil);
                if (ad.ExpiresAtUnix - nowUnix <= RenewWindowSeconds)
                {
                    AddSheetItem(SheetRenew, Loc.T(L.YellowPages.Renew), PhoneIcons.Refresh);
                }

                if (ad.Archetype == AdArchetypes.Place && ad.Status == AdStatuses.Live)
                {
                    var open = ad.OpenUntilUnix > nowUnix;
                    AddSheetItem(open ? SheetClose : SheetOpen,
                        Loc.T(open ? L.YellowPages.CloseNow : L.YellowPages.OpenNowAction),
                        open ? PhoneIcons.X : PhoneIcons.Flame);
                    if (open && musters.Mine is null && (ad.TerritoryId > 0 || ad.AddressNote.Length > 0))
                    {
                        AddSheetItem(SheetAnnounce, Loc.T(L.YellowPages.AnnounceMuster), PhoneIcons.Users);
                    }
                }

                AddSheetItem(SheetShare, Loc.T(L.YellowPages.ShareAd), PhoneIcons.Share);
            }

            AddSheetItem(SheetDelete, Loc.T(L.YellowPages.DeleteAd), PhoneIcons.Trash, true);
        }
        else
        {
            AddSheetItem(SheetSave, Loc.T(ad.Saved ? L.YellowPages.Unsave : L.YellowPages.Save),
                ad.Saved ? PhoneIcons.BookmarkFilled : PhoneIcons.Bookmark);
            AddSheetItem(SheetShare, Loc.T(L.YellowPages.ShareAd), PhoneIcons.Share);
            AddSheetItem(SheetCopyDetails, Loc.T(L.YellowPages.CopyDetails), PhoneIcons.Copy);
            AddSheetItem(SheetReport, Loc.T(L.Report.Action), PhoneIcons.Flag, true);
        }

        adSheet.Open();
    }

    private void AddSheetItem(int action, string label, string glyph, bool danger = false)
    {
        adSheetActions[adSheetCount] = action;
        adSheetItems[adSheetCount] = new ActionSheet.Item(label, glyph, danger);
        adSheetCount++;
    }

    private void DrawAdSheet(Rect screen)
    {
        if (!adSheet.CapturesPointer)
        {
            return;
        }

        var picked = adSheet.Draw(screen, ActionSheetStyle.From(ui), adSheetItems.AsSpan(0, adSheetCount),
            Loc.T(L.Common.Cancel), false, adSheetTitle);
        if (picked < 0 || adSheetAdId is not { } adId)
        {
            return;
        }

        var ad = ResolveAd(adId);
        if (ad is null)
        {
            return;
        }

        switch (adSheetActions[picked])
        {
            case SheetEdit:
                StartEdit(ad);
                break;
            case SheetRenew:
                mineBusyAdId = ad.Id;
                store.Renew(ad.Id, _ => mineBusyAdId = null);
                break;
            case SheetOpen:
                mineBusyAdId = ad.Id;
                store.SetOpen(ad.Id, true, 0, _ => mineBusyAdId = null);
                break;
            case SheetClose:
                mineBusyAdId = ad.Id;
                store.SetOpen(ad.Id, false, 0, _ => mineBusyAdId = null);
                break;
            case SheetAnnounce:
                AnnounceOnMuster(ad, NowUnix());
                break;
            case SheetShare:
                Copy("share", AdShare.Compose(ad.Id));
                break;
            case SheetDelete:
                AskDeleteAd(ad);
                break;
            case SheetCopyDetails:
                Copy("detail", BuildCopySummary(ad));
                break;
            case SheetReport:
                OpenReport(ad.Id);
                break;
            case SheetSave:
                if (!saveBusy)
                {
                    ToggleSaved(ad);
                }

                break;
        }
    }

    private string BuildCopySummary(AdDto ad)
    {
        var builder = new StringBuilder(256);
        builder.Append(ad.Title);
        builder.Append(" · ");
        builder.Append(AdText.Identity(ad));
        if (ad.Body.Length > 0)
        {
            builder.Append('\n');
            builder.Append(ad.Body);
        }

        if (ad.AddressNote.Length > 0)
        {
            builder.Append('\n');
            builder.Append(ad.AddressNote);
        }

        var location = AdText.Location(ad);
        var summary = LocationShare.Summary(in location);
        if (summary.Length > 0)
        {
            builder.Append('\n');
            builder.Append(summary);
        }

        return builder.ToString();
    }
}
