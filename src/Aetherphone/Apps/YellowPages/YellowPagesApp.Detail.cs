using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float ActionHeight = 52f;
    private const float LocationActionHeight = 40f;
    private const float DetailRowHeight = 24f;
    private const float HeroHeight = 224f;
    private const float HeroBannerHeight = 122f;
    private const float HeroOverlap = 24f;
    private const float HeroParallax = 18f;
    private const float CardLabelHeight = 22f;
    private const float PosterCardHeight = 62f;
    private const float StatRowHeight = 26f;
    private const float SubActionHeight = 38f;
    private const float PhotoFadeSeconds = 0.18f;

    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 PrimaryActionInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ScrimClear = new(0f, 0f, 0f, 0f);
    private static readonly Vector4 ScrimDeep = new(0f, 0f, 0f, 0.62f);

    private string? detailFetchId;
    private AdDto? detailFetched;
    private bool detailLoading;
    private bool saveBusy;
    private int detailPhotoIndex;
    private int detailPhotoPrevious;
    private float detailPhotoFade = 1f;

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
        var ad = ResolveAd(adId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, ad is null ? DisplayName : Loc.T(AdCategories.Label(ad.Category)), back);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (ad is null)
        {
            EnsureDetailFetch(adId);
            if (detailLoading)
            {
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, ui.Accent,
                    AppPalettes.YellowPages.MutedInk, Loc.T(L.Common.Loading));
                return;
            }

            EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.YellowPages.UnavailableTitle),
                Loc.T(L.YellowPages.UnavailableHint));
            return;
        }

        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            DrawDetailHero(ad, scale);
            DrawDetailHeadline(ad, nowUnix, scale);
            DrawDetailBody(ad.Body, scale);
            DrawTagRow(ad, scale);
            if (ad.Archetype == AdArchetypes.Place)
            {
                DrawScheduleCard(ad, nowUnix, scale);
                DrawLocationCard(ad, scale);
            }
            else if (ad.Archetype == AdArchetypes.Call)
            {
                DrawCallCard(ad, scale);
            }

            DrawPosterCard(ad, scale);
            DrawDetailActions(ad, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private AdDto? ResolveAd(string adId)
    {
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

        var fetched = detailFetched;
        return fetched is not null && fetched.Id == adId ? fetched : null;
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

    private void DrawDetailHero(AdDto ad, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var pad = Metrics.Space.Lg * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var photos = PostMedia.Photos(ad.MediaUrls, ad.MediaUrl);
        var height = (photos.Length > 0 ? HeroHeight : HeroBannerHeight) * scale;
        var rect = new Rect(new Vector2(origin.X - pad, origin.Y - Metrics.Space.Sm * scale),
            new Vector2(origin.X + width + pad, origin.Y - Metrics.Space.Sm * scale + height));
        if (photos.Length == 0)
        {
            DrawHeroBanner(drawList, rect, ad, scale);
        }
        else
        {
            DrawHeroPhoto(drawList, rect, photos, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height - (Metrics.Space.Sm + HeroOverlap) * scale));
    }

    private void DrawHeroBanner(ImDrawListPtr drawList, Rect rect, AdDto ad, float scale)
    {
        drawList.AddRectFilledMultiColor(rect.Min, rect.Max,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.26f)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.05f)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.12f)));
        drawList.PushClipRect(rect.Min, rect.Max, true);
        var drift = Vector2.Lerp(new Vector2(rect.Min.X + rect.Width * 0.28f, rect.Center.Y),
            new Vector2(rect.Min.X + rect.Width * 0.72f, rect.Center.Y), Pulse.Wave(Pulse.Orbit));
        for (var ring = 3; ring >= 1; ring--)
        {
            drawList.AddCircleFilled(drift, rect.Height * 0.32f * ring,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.05f)), 48);
        }

        drawList.PopClipRect();
        AppSkin.Icon(drawList, rect.Center, AdCategories.Icon(ad.Category).ToIconString(),
            Palette.WithAlpha(ui.Accent, 0.62f), 2.4f);
    }

    private void DrawHeroPhoto(ImDrawListPtr drawList, Rect rect, string[] photos, float scale)
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
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(AppPalettes.YellowPages.FieldSurface));
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
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
        var scrimTop = new Vector2(rect.Min.X, rect.Max.Y - 78f * scale);
        drawList.AddRectFilledMultiColor(scrimTop, rect.Max,
            ImGui.GetColorU32(ScrimClear), ImGui.GetColorU32(ScrimClear),
            ImGui.GetColorU32(ScrimDeep), ImGui.GetColorU32(ScrimDeep));

        var expandRadius = 15f * scale;
        var expandCenter = new Vector2(rect.Max.X - expandRadius - 10f * scale,
            rect.Min.Y + expandRadius + 10f * scale);
        var expandHalf = new Vector2(expandRadius, expandRadius);
        var overExpand = ImGui.IsMouseHoveringRect(expandCenter - expandHalf, expandCenter + expandHalf, false);
        drawList.AddCircleFilled(expandCenter, expandRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, overExpand ? 0.78f : 0.55f)), 28);
        if (overExpand)
        {
            drawList.AddCircle(expandCenter, expandRadius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 28,
                1.4f * scale);
        }

        AppSkin.Icon(drawList, expandCenter, FontAwesomeIcon.Expand.ToIconString(), HeroInk, 0.66f);
        if (overExpand)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(expandCenter - expandHalf, expandCenter + expandHalf, overExpand))
        {
            var viewerUrl = url;
            photoViewer.Open(() => images.Get(viewerUrl));
            return;
        }

        var touchMax = new Vector2(rect.Max.X, rect.Max.Y - HeroOverlap * scale);
        if (photos.Length > 1)
        {
            DrawHeroDots(drawList, rect, photos.Length, scale);
            var midX = rect.Center.X;
            var leftHovered = !overExpand && ImGui.IsMouseHoveringRect(rect.Min, new Vector2(midX, touchMax.Y), false);
            var rightHovered = !overExpand
                && ImGui.IsMouseHoveringRect(new Vector2(midX, rect.Min.Y), touchMax, false);
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
            photoViewer.Open(() => images.Get(viewerUrl));
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
        drawList.AddImage(texture.Handle, min, max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
    }

    private void StepHeroPhoto(int count, int step)
    {
        detailPhotoPrevious = detailPhotoIndex;
        detailPhotoIndex = (detailPhotoIndex + count + step) % count;
        detailPhotoFade = 0f;
    }

    private void DrawHeroDots(ImDrawListPtr drawList, Rect rect, int count, float scale)
    {
        var dotHeight = 5f * scale;
        var activeWidth = 16f * scale;
        var dotWidth = 5f * scale;
        var gap = 5f * scale;
        var totalWidth = activeWidth + (count - 1) * dotWidth + (count - 1) * gap;
        var cursorX = rect.Center.X - totalWidth * 0.5f;
        var dotTop = rect.Max.Y - (HeroOverlap + 15f) * scale;
        for (var index = 0; index < count; index++)
        {
            var active = index == detailPhotoIndex;
            var width = active ? activeWidth : dotWidth;
            var min = new Vector2(cursorX, dotTop);
            var max = new Vector2(cursorX + width, dotTop + dotHeight);
            drawList.AddRectFilled(min, max,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, active ? 1f : 0.45f)), dotHeight * 0.5f);
            cursorX = max.X + gap;
        }
    }

    private void DrawDetailHeadline(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var innerWidth = width - pad * 2f;
        var status = StatusPill(ad, nowUnix, out var fill, out var ink, out var trailing, out var live);
        var titleHeight = Typography.MeasureWrappedBlock(ad.Title, TextStyles.Title2, innerWidth).Y;
        var height = pad * 2f + titleHeight + 24f * scale + StatRowHeight * scale
            + (status.Length > 0 ? 38f * scale : 0f);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.YellowPages.BackdropTop, 0.92f)));
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        Material.TopGlow(drawList, card.Min, card.Max, rounding, ui.Accent, 0.55f, 0.10f);

        var cursorY = origin.Y + pad;
        var left = origin.X + pad;
        if (status.Length > 0)
        {
            var pillWidth = live
                ? DrawLivePill(drawList, new Vector2(left, cursorY), status, fill, ink, scale)
                : DrawPill(drawList, new Vector2(left, cursorY), status, fill, ink,
                    TextStyles.SubheadlineEmphasized, scale);
            if (trailing.Length > 0)
            {
                var fitted = Typography.FitText(trailing, innerWidth - pillWidth - 12f * scale, TextStyles.Footnote);
                Typography.Draw(drawList, new Vector2(left + pillWidth + 12f * scale, cursorY + 8f * scale),
                    fitted, AppPalettes.YellowPages.MutedInk, TextStyles.Footnote);
            }

            cursorY += 38f * scale;
        }

        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), ad.Title,
            AppPalettes.YellowPages.TitleInk, TextStyles.Title2, innerWidth);
        cursorY += 4f * scale;
        var meta = Typography.FitText(BuildMetaLine(ad), innerWidth, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, cursorY), meta, AppPalettes.YellowPages.MutedInk,
            TextStyles.Footnote);
        cursorY += 20f * scale;
        DrawDetailStats(drawList, ad, nowUnix, new Vector2(left, cursorY), innerWidth, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawDetailStats(ImDrawListPtr drawList, AdDto ad, long nowUnix, Vector2 topLeft, float width,
        float scale)
    {
        var centerY = topLeft.Y + StatRowHeight * scale * 0.5f;
        drawList.AddLine(new Vector2(topLeft.X, topLeft.Y), new Vector2(topLeft.X + width, topLeft.Y),
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.YellowPages.MutedInk, 0.18f)), 1f);
        DrawStat(drawList, new Vector2(topLeft.X, centerY), FontAwesomeIcon.Eye,
            Loc.T(L.YellowPages.ViewCount, ad.Views), false, scale);
        var expires = AdText.ExpiresLine(ad, nowUnix);
        var expiresWidth = Typography.Measure(expires, TextStyles.Footnote).X + 20f * scale;
        DrawStat(drawList, new Vector2(topLeft.X + width - expiresWidth, centerY), FontAwesomeIcon.HourglassHalf,
            expires, ad.ExpiresAtUnix - nowUnix < 86400L, scale);
    }

    private void DrawStat(ImDrawListPtr drawList, Vector2 leftCenter, FontAwesomeIcon icon, string label,
        bool urgent, float scale)
    {
        var ink = urgent ? theme.Danger : AppPalettes.YellowPages.MutedInk;
        AppSkin.Icon(drawList, new Vector2(leftCenter.X + 6f * scale, leftCenter.Y), icon.ToIconString(),
            Palette.WithAlpha(urgent ? ink : ui.Accent, 0.85f), 0.62f);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(leftCenter.X + 20f * scale, leftCenter.Y - labelSize.Y * 0.5f), label,
            ink, TextStyles.Footnote);
    }

    private static float DrawLivePill(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 fill,
        Vector4 ink, float scale)
    {
        var style = TextStyles.SubheadlineEmphasized;
        var labelSize = Typography.Measure(label, style);
        var dotSpace = 15f * scale;
        var max = topLeft + labelSize + new Vector2(18f * scale + dotSpace, 8f * scale);
        var height = max.Y - topLeft.Y;
        Squircle.Fill(drawList, topLeft, max, height * 0.5f, ImGui.GetColorU32(fill));
        DrawLiveDot(drawList, new Vector2(topLeft.X + 14f * scale, topLeft.Y + height * 0.5f), ink, scale);
        Typography.Draw(drawList, topLeft + new Vector2(9f * scale + dotSpace, 4f * scale), label, ink, style);
        return max.X - topLeft.X;
    }

    private static void DrawLiveDot(ImDrawListPtr drawList, Vector2 center, Vector4 color, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, 5.4f * scale, ImGui.GetColorU32(Palette.WithAlpha(color, 0.45f * pulse)), 20,
            1.3f * scale);
        drawList.AddCircleFilled(center, 3.1f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(color, 0.65f + 0.35f * pulse)), 20);
    }

    private string StatusPill(AdDto ad, long nowUnix, out Vector4 fill, out Vector4 ink, out string trailing,
        out bool live)
    {
        trailing = string.Empty;
        live = false;
        if (ad.Archetype == AdArchetypes.Place)
        {
            var state = AdText.OpenState(ad, nowUnix);
            if (state.IsOpen)
            {
                fill = AdCard.OpenGreen;
                ink = new Vector4(0.03f, 0.08f, 0.05f, 1f);
                live = true;
                if (state.ClosesAtUnix > 0)
                {
                    trailing = Loc.T(L.YellowPages.ClosesAt, TimeText.Clock(state.ClosesAtUnix));
                }

                return Loc.T(L.YellowPages.OpenNow);
            }

            fill = Palette.WithAlpha(ui.Accent, 0.20f);
            ink = ui.Accent;
            if (state.NextOpeningUnix <= 0)
            {
                return string.Empty;
            }

            return Loc.T(L.YellowPages.OpensAt,
                $"{TimeText.DayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}");
        }

        fill = Palette.WithAlpha(ui.Accent, 0.20f);
        ink = ui.Accent;
        if (AdCategories.IsLinkOnly(ad.Category))
        {
            trailing = LinkHost(ad.LinkUrl);
            return Loc.T(L.YellowPages.ModBadge);
        }

        if (ad.Archetype == AdArchetypes.Service)
        {
            trailing = ad.Turnaround;
            return AdText.PriceLine(ad);
        }

        return ad.SlotsLine;
    }

    private static string LinkHost(string url)
    {
        if (url.Length == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return string.Empty;
        }

        return parsed.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? parsed.Host[4..] : parsed.Host;
    }

    private static string BuildMetaLine(AdDto ad)
    {
        var category = Loc.T(AdCategories.Label(ad.Category));
        var world = ad.WorldId > 0 ? LocationShare.WorldName((uint)ad.WorldId) : string.Empty;
        return world.Length > 0 ? $"{category} · {world}" : category;
    }

    private void DrawDetailBody(string body, float scale)
    {
        if (body.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = Typography.DrawWrappedLeft(origin, body, AppPalettes.YellowPages.BodyInk, TextStyles.Body,
            width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawTagRow(AdDto ad, float scale)
    {
        if (ad.Tags.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = Metrics.Space.Xs * scale;
        var rowHeight = 28f * scale;
        var cursorX = origin.X;
        var lineTop = origin.Y;
        var fill = Palette.WithAlpha(ui.Accent, 0.12f);
        for (var index = 0; index < ad.Tags.Length; index++)
        {
            var label = ad.Tags[index];
            var labelSize = Typography.Measure(label, TextStyles.Footnote);
            var pillWidth = labelSize.X + 18f * scale;
            var pillHeight = labelSize.Y + 8f * scale;
            if (cursorX + pillWidth > origin.X + width && cursorX > origin.X)
            {
                cursorX = origin.X;
                lineTop += rowHeight + gap;
            }

            var pillTop = new Vector2(cursorX, lineTop);
            DrawPill(drawList, pillTop, label, fill, Palette.WithAlpha(ui.Accent, 0.92f), TextStyles.Footnote, scale);
            Squircle.Stroke(drawList, pillTop, pillTop + new Vector2(pillWidth, pillHeight), pillHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f);
            cursorX += pillWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, lineTop - origin.Y + rowHeight + Metrics.Space.Md * scale));
    }

    private Rect DrawInfoCard(string label, FontAwesomeIcon icon, float contentHeight, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var labelHeight = CardLabelHeight * scale;
        var cardHeight = pad * 2f + labelHeight + contentHeight;
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight), rounding, elevated: true);
        Material.TopGlow(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight), rounding, ui.Accent,
            0.5f, 0.06f);
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad),
            Loc.Culture.TextInfo.ToUpper(label), AppPalettes.YellowPages.HeaderInk, TextStyles.Caption1);
        AppSkin.Icon(drawList, new Vector2(origin.X + width - pad - 6f * scale, origin.Y + pad + 6f * scale),
            icon.ToIconString(), Palette.WithAlpha(ui.Accent, 0.55f), 0.72f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Md * scale));
        return new Rect(new Vector2(origin.X + pad, origin.Y + pad + labelHeight),
            new Vector2(origin.X + width - pad, origin.Y + cardHeight - pad));
    }

    private void DrawScheduleCard(AdDto ad, long nowUnix, float scale)
    {
        if (ad.Schedule.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var inner = DrawInfoCard(Loc.T(L.YellowPages.ScheduleYourTime), FontAwesomeIcon.CalendarAlt,
            ad.Schedule.Length * DetailRowHeight * scale, scale);
        var todayLocal = (int)DateTime.Now.DayOfWeek;
        var dayNames = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var index = 0; index < ad.Schedule.Length; index++)
        {
            var slot = ad.Schedule[index];
            var lineTop = inner.Min.Y + index * DetailRowHeight * scale;
            AdText.ToLocalSlot(slot, out var localDay, out _);
            var startUnix = AdText.NextOccurrenceUnix(slot, nowUnix);
            var range = $"{TimeText.Clock(startUnix)} - {TimeText.Clock(startUnix + slot.DurationMinutes * 60L)}";
            var isToday = localDay == todayLocal;
            var style = isToday ? TextStyles.SubheadlineEmphasized : TextStyles.Subheadline;
            var ink = isToday ? AdCard.OpenGreen : AppPalettes.YellowPages.BodyInk;
            if (isToday)
            {
                var highlightMin = new Vector2(inner.Min.X - 7f * scale, lineTop - 2f * scale);
                var highlightMax = new Vector2(inner.Max.X + 7f * scale, lineTop + (DetailRowHeight - 2f) * scale);
                Squircle.Fill(drawList, highlightMin, highlightMax, 7f * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(AdCard.OpenGreen, 0.10f)));
                Squircle.Fill(drawList, highlightMin, new Vector2(highlightMin.X + 3f * scale, highlightMax.Y),
                    1.5f * scale, ImGui.GetColorU32(Palette.WithAlpha(AdCard.OpenGreen, 0.85f)));
            }

            Typography.Draw(drawList, new Vector2(inner.Min.X, lineTop), dayNames[localDay],
                isToday ? AdCard.OpenGreen : AppPalettes.YellowPages.MutedInk, style);
            var rangeSize = Typography.Measure(range, style);
            Typography.Draw(drawList, new Vector2(inner.Max.X - rangeSize.X, lineTop), range, ink, style);
        }
    }

    private void DrawCallCard(AdDto ad, float scale)
    {
        if (ad.Requirements.Length == 0 && ad.SlotsLine.Length == 0)
        {
            return;
        }

        var width = ImGui.GetContentRegionAvail().X - Metrics.Space.Md * 2f * scale;
        var slotsHeight = ad.SlotsLine.Length > 0 ? 26f * scale : 0f;
        var requirementsHeight = ad.Requirements.Length > 0
            ? Typography.MeasureWrappedBlock(ad.Requirements, TextStyles.Subheadline, width).Y
            : 0f;
        var inner = DrawInfoCard(Loc.T(L.YellowPages.CallSection), FontAwesomeIcon.Users,
            slotsHeight + requirementsHeight, scale);
        var cursorY = inner.Min.Y;
        if (ad.SlotsLine.Length > 0)
        {
            Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(inner.Min.X, cursorY), ad.SlotsLine,
                AppPalettes.YellowPages.TitleInk, TextStyles.BodyEmphasized);
            cursorY += slotsHeight;
        }

        if (ad.Requirements.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(inner.Min.X, cursorY), ad.Requirements,
                AppPalettes.YellowPages.BodyInk, TextStyles.Subheadline, inner.Width);
        }
    }

    private void DrawLocationCard(AdDto ad, float scale)
    {
        var hasAddress = ad.Ward > 0 || ad.TerritoryId > 0 || ad.AddressNote.Length > 0;
        if (!hasAddress)
        {
            return;
        }

        var lineCount = 0;
        Span<string> lines = new string[3];
        if (ad.AddressNote.Length > 0)
        {
            lines[lineCount++] = ad.AddressNote;
        }

        var place = AdText.PlaceLine(ad);
        if (place.Length > 0)
        {
            lines[lineCount++] = place;
        }

        if (ad.Ward > 0 && ad.Plot > 0)
        {
            lines[lineCount++] = Loc.T(L.YellowPages.WardPlot, ad.Ward, ad.Plot);
        }

        var drawList = ImGui.GetWindowDrawList();
        var actionHeight = LocationActionHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var canTravel = CanTravelTo(ad);
        var buttonRows = canTravel ? 2 : 1;
        var contentHeight = lineCount * DetailRowHeight * scale + gap
            + buttonRows * actionHeight + (buttonRows - 1) * gap;
        var inner = DrawInfoCard(Loc.T(L.YellowPages.WhereSection), FontAwesomeIcon.MapMarkedAlt, contentHeight,
            scale);
        for (var index = 0; index < lineCount; index++)
        {
            var lineTop = inner.Min.Y + index * DetailRowHeight * scale;
            var style = index == 0 ? TextStyles.BodyEmphasized : TextStyles.Subheadline;
            var ink = index == 0 ? AppPalettes.YellowPages.TitleInk : AppPalettes.YellowPages.BodyInk;
            var fitted = Typography.FitText(lines[index], inner.Width, style);
            Typography.Draw(drawList, new Vector2(inner.Min.X, lineTop), fitted, ink, style);
        }

        var actionTop = inner.Min.Y + lineCount * DetailRowHeight * scale + gap;
        var hasMap = ad.MapId != 0;
        var slots = hasMap ? 2 : 1;
        var slotWidth = (inner.Width - gap * (slots - 1)) / slots;
        var cursor = inner.Min.X;
        if (hasMap)
        {
            var flagRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + slotWidth, actionTop + actionHeight));
            if (ui.PillButton(flagRect, Loc.T(L.YellowPages.FlagOnMap), false))
            {
                var location = AdText.Location(ad);
                LocationShare.OpenMap(in location);
            }

            cursor += slotWidth + gap;
        }

        var copyRect = new Rect(new Vector2(cursor, actionTop),
            new Vector2(cursor + slotWidth, actionTop + actionHeight));
        var copyLabel = JustCopied("detail") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.CopyDetails);
        if (ui.PillButton(copyRect, copyLabel, false))
        {
            Copy("detail", BuildCopySummary(ad));
        }

        if (!canTravel)
        {
            return;
        }

        var travelTop = actionTop + actionHeight + gap;
        var travelRect = new Rect(new Vector2(inner.Min.X, travelTop),
            new Vector2(inner.Max.X, travelTop + actionHeight));
        var travelLabel = JustCopied("travel") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.Travel);
        if (ui.PillButton(travelRect, travelLabel, true))
        {
            TravelTo(ad);
        }
    }

    private void DrawPosterCard(AdDto ad, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = PosterCardHeight * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Card * scale,
            elevated: true);
        var pad = Metrics.Space.Md * scale;
        var avatarRadius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + height * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, AdText.Identity(ad), string.Empty,
            ad.OwnerAvatarUrl.Length > 0 ? ad.OwnerAvatarUrl : null, images, lodestone, 0.9f, 48);
        drawList.AddCircle(avatarCenter, avatarRadius + 2.5f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.55f)), 40, 1.4f * scale);
        var textLeft = avatarCenter.X + avatarRadius + 13f * scale;
        var textWidth = origin.X + width - pad - textLeft;
        var name = Typography.FitText(SocialIdentity.Name(ad.OwnerName, ad.OwnerHandle), textWidth,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 13f * scale), name,
            AppPalettes.YellowPages.TitleInk, TextStyles.Headline);
        var handle = ad.OwnerHandle.Length > 0 ? $"@{ad.OwnerHandle}" : string.Empty;
        var renewed = Loc.T(L.YellowPages.RenewedAgo,
            TimeText.Ago(DateTimeOffset.FromUnixTimeSeconds(ad.RenewedAtUnix)));
        var line = handle.Length > 0 ? $"{handle} · {renewed}" : renewed;
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 34f * scale),
            Typography.FitText(line, textWidth, TextStyles.Footnote), AppPalettes.YellowPages.MutedInk,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
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

        if (lifestreamAvailable)
        {
            LifestreamBridge.Travel(worldName);
            return;
        }

        Copy("travel", $"/li {worldName}");
    }

    private void DrawDetailActions(AdDto ad, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionHeight * scale));
        if (IsMineAd(ad.Id))
        {
            if (DrawPrimaryAction(rect, Loc.T(L.YellowPages.ManageAction), FontAwesomeIcon.SlidersH, scale))
            {
                activeTab = YellowPagesTab.Mine;
                router.Pop(false);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));
            return;
        }

        var hasLink = AdCategories.IsLinkOnly(ad.Category) && ad.LinkUrl.Length > 0;
        if (hasLink)
        {
            var linkLabel = JustCopied("modlink")
                ? Loc.T(L.YellowPages.ModLinkCopied)
                : Loc.T(L.YellowPages.ModLinkAction);
            if (DrawPrimaryAction(rect, linkLabel, FontAwesomeIcon.ExternalLinkAlt, scale))
            {
                OpenModLink(ad);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Xs * scale));
            ui.HelpText(LinkHost(ad.LinkUrl));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            origin = ImGui.GetCursorScreenPos();
            rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionHeight * scale));
        }

        if (!ad.AllowInquiries)
        {
            DrawLockedAction(rect, Loc.T(L.YellowPages.InquiriesClosed), scale);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Xs * scale));
            ui.HelpText(Loc.T(L.YellowPages.InquiriesClosedHint));
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawDetailSubActions(ad, width, scale);
            return;
        }

        var inquireLabel = Loc.T(L.YellowPages.InquireAction);
        var tapped = hasLink
            ? ui.GhostButton(rect, inquireLabel)
            : DrawPrimaryAction(rect, inquireLabel, FontAwesomeIcon.PaperPlane, scale);
        if (tapped)
        {
            OpenInquiry(ad);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Xs * scale));
        ui.HelpText(Loc.T(L.YellowPages.InquireHint));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        DrawDetailSubActions(ad, width, scale);
    }

    private bool DrawPrimaryAction(Rect rect, string label, FontAwesomeIcon icon, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var accent = ui.Accent;
        var glow = hovered ? 0.26f : 0.16f;
        for (var ring = 3; ring >= 1; ring--)
        {
            var spread = ring * 3f * scale;
            Squircle.Fill(drawList, rect.Min - new Vector2(spread, spread), rect.Max + new Vector2(spread, spread),
                radius + spread, ImGui.GetColorU32(Palette.WithAlpha(accent, glow / (ring * 3f))));
        }

        var top = Palette.Lighten(accent, hovered ? 0.34f : 0.22f);
        var bottom = Palette.Darken(accent, hovered ? 0.02f : 0.10f);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom));
        drawList.AddLine(new Vector2(rect.Min.X + radius, rect.Min.Y + 1.5f * scale),
            new Vector2(rect.Max.X - radius, rect.Min.Y + 1.5f * scale),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.38f : 0.24f)), 1.4f * scale);
        var iconWidth = 15f * scale;
        var iconGap = 9f * scale;
        var fitted = Typography.FitText(label, rect.Width - iconWidth - iconGap - rect.Height, TextStyles.Headline);
        var labelSize = Typography.Measure(fitted, TextStyles.Headline);
        var left = rect.Center.X - (labelSize.X + iconGap + iconWidth) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + iconWidth * 0.5f, rect.Center.Y), icon.ToIconString(),
            PrimaryActionInk, 0.78f);
        Typography.Draw(drawList, new Vector2(left + iconWidth + iconGap, rect.Center.Y - labelSize.Y * 0.5f),
            fitted, PrimaryActionInk, TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawLockedAction(Rect rect, string label, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(AppPalettes.YellowPages.FieldSurface));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.YellowPages.MutedInk, 0.28f)), 1f);
        var iconWidth = 14f * scale;
        var iconGap = 9f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        var left = rect.Center.X - (labelSize.X + iconGap + iconWidth) * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + iconWidth * 0.5f, rect.Center.Y),
            FontAwesomeIcon.Lock.ToIconString(), AppPalettes.YellowPages.MutedInk, 0.72f);
        Typography.Draw(drawList, new Vector2(left + iconWidth + iconGap, rect.Center.Y - labelSize.Y * 0.5f),
            label, AppPalettes.YellowPages.MutedInk, TextStyles.Headline);
    }

    private void DrawDetailSubActions(AdDto ad, float width, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = SubActionHeight * scale;
        var slot = width / 3f;
        var centerY = origin.Y + height * 0.5f;

        var saveLabel = ad.Saved ? Loc.T(L.YellowPages.Unsave) : Loc.T(L.YellowPages.Save);
        var saveInk = ad.Saved ? ui.Accent : AppPalettes.YellowPages.BodyInk;
        if (DrawSubAction(drawList, new Vector2(origin.X + slot * 0.5f, centerY), slot, FontAwesomeIcon.Heart,
                saveLabel, saveInk, ad.Saved, scale) && !saveBusy)
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

        var shareLabel = JustCopied("share") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.ShareAd);
        if (DrawSubAction(drawList, new Vector2(origin.X + slot * 1.5f, centerY), slot,
                FontAwesomeIcon.ShareSquare, shareLabel, AppPalettes.YellowPages.BodyInk, false, scale))
        {
            Copy("share", AdShare.Compose(ad.Id));
        }

        if (DrawSubAction(drawList, new Vector2(origin.X + slot * 2.5f, centerY), slot, FontAwesomeIcon.Flag,
                Loc.T(L.Report.Action), theme.Danger, false, scale))
        {
            OpenReport(ad.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private bool DrawSubAction(ImDrawListPtr drawList, Vector2 center, float slotWidth, FontAwesomeIcon icon,
        string label, Vector4 ink, bool active, float scale)
    {
        var half = new Vector2(slotWidth * 0.5f - 4f * scale, SubActionHeight * scale * 0.5f);
        var min = center - half;
        var max = center + half;
        var hovered = UiInteract.Hover(min, max);
        var fill = active
            ? Palette.WithAlpha(ui.Accent, hovered ? 0.24f : 0.16f)
            : hovered ? ui.HoverTint : AppPalettes.YellowPages.FieldSurface;
        Squircle.Fill(drawList, min, max, half.Y, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, min, max, half.Y,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.55f)), 1f);
        }

        var fitted = Typography.FitText(label, slotWidth - 40f * scale, TextStyles.Footnote);
        var labelSize = Typography.Measure(fitted, TextStyles.Footnote);
        var iconGap = 8f * scale;
        var contentWidth = 14f * scale + iconGap + labelSize.X;
        var left = center.X - contentWidth * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left + 7f * scale, center.Y), icon.ToIconString(), ink, 0.78f);
        Typography.Draw(drawList, new Vector2(left + 14f * scale + iconGap, center.Y - labelSize.Y * 0.5f), fitted,
            ink, TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private void OpenModLink(AdDto ad)
    {
        Copy("modlink", ad.LinkUrl);
        Windows.UrlActions.OpenInBrowser(ad.LinkUrl,
            exception => AepLog.Warning($"[YellowPages] mod link failed: {exception.Message}"));
    }

    private void OpenInquiry(AdDto ad) => OpenInquiryFor(ad);

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
