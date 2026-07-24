using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.YellowPages;

internal sealed partial class YellowPagesApp
{
    private const float ActionHeight = 50f;
    private const float LocationActionHeight = 42f;
    private const float LocationRowHeight = 22f;
    private const float HeroHeight = 150f;
    private const float ScheduleRowHeight = 24f;

    private string? detailFetchId;
    private AdDto? detailFetched;
    private bool detailLoading;
    private bool saveBusy;

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        saveBusy = false;
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
            DrawDetailHero(ad, nowUnix, scale);
            DrawWrappedBody(ad.Body, scale);
            if (ad.Archetype == AdArchetypes.Place)
            {
                DrawScheduleCard(ad, nowUnix, scale);
                DrawLocationBlock(ad, scale);
            }
            else if (ad.Archetype == AdArchetypes.Call)
            {
                DrawCallCard(ad, scale);
            }

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

    private void DrawDetailHero(AdDto ad, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cursorY = origin.Y;
        if (ad.MediaUrls.Length > 0)
        {
            var heroRect = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + HeroHeight * scale));
            DrawHeroPhoto(drawList, heroRect, ad, scale);
            cursorY = heroRect.Max.Y + Metrics.Space.Md * scale;
        }

        var title = Typography.FitText(ad.Title, width, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(origin.X, cursorY), title, AppPalettes.YellowPages.TitleInk,
            TextStyles.Title3);
        cursorY += 28f * scale;
        Typography.Draw(drawList, new Vector2(origin.X, cursorY), AdText.Identity(ad),
            AppPalettes.YellowPages.MutedInk, TextStyles.Subheadline);
        cursorY += 22f * scale;

        var statusLine = BuildStatusLine(ad, nowUnix, out var statusColor);
        if (statusLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(origin.X, cursorY), statusLine, statusColor,
                TextStyles.FootnoteEmphasized);
            cursorY += 22f * scale;
        }

        var metaLine = BuildMetaLine(ad, nowUnix);
        Typography.Draw(drawList, new Vector2(origin.X, cursorY), metaLine, AppPalettes.YellowPages.MutedInk,
            TextStyles.Footnote);
        cursorY += 22f * scale;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Md * scale));
    }

    private void DrawHeroPhoto(ImDrawListPtr drawList, Rect rect, AdDto ad, float scale)
    {
        var rounding = Metrics.Radius.Lg * scale;
        var texture = images.Get(ad.MediaUrls[0]);
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(AppPalettes.YellowPages.FieldSurface));
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
            return;
        }

        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        var textureAspect = texture.Height > 0 ? texture.Width / (float)texture.Height : 1f;
        var rectAspect = rect.Height > 0f ? rect.Width / rect.Height : 1f;
        if (textureAspect > rectAspect)
        {
            var crop = rectAspect / textureAspect;
            uv0.X = (1f - crop) * 0.5f;
            uv1.X = 1f - uv0.X;
        }
        else if (textureAspect > 0f)
        {
            var crop = textureAspect / rectAspect;
            uv0.Y = (1f - crop) * 0.5f;
            uv1.Y = 1f - uv0.Y;
        }

        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding);
        if (ad.MediaUrls.Length > 1)
        {
            var badge = Loc.T(L.YellowPages.PhotoCount, ad.MediaUrls.Length);
            var badgeSize = Typography.Measure(badge, TextStyles.Caption1);
            var badgeMax = new Vector2(rect.Max.X - 8f * scale, rect.Max.Y - 8f * scale);
            var badgeMin = badgeMax - badgeSize - new Vector2(14f * scale, 8f * scale);
            Squircle.Fill(drawList, badgeMin, badgeMax, (badgeMax.Y - badgeMin.Y) * 0.5f,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)));
            Typography.Draw(drawList, badgeMin + new Vector2(7f * scale, 4f * scale), badge,
                new Vector4(1f, 1f, 1f, 1f), TextStyles.Caption1);
        }
    }

    private static string BuildStatusLine(AdDto ad, long nowUnix, out Vector4 color)
    {
        if (ad.Archetype == AdArchetypes.Place)
        {
            color = AdCard.OpenGreen;
            return AdText.OpenLine(ad, nowUnix);
        }

        if (ad.Archetype == AdArchetypes.Service)
        {
            color = new Vector4(0.98f, 0.72f, 0.30f, 1f);
            var price = AdText.PriceLine(ad);
            return ad.Turnaround.Length > 0 ? $"{price} · {ad.Turnaround}" : price;
        }

        color = new Vector4(0.98f, 0.72f, 0.30f, 1f);
        return ad.SlotsLine;
    }

    private string BuildMetaLine(AdDto ad, long nowUnix)
    {
        var world = ad.WorldId > 0 ? LocationShare.WorldName((uint)ad.WorldId) : string.Empty;
        var expires = AdText.ExpiresLine(ad, nowUnix);
        var meta = world.Length > 0 ? $"{world} · {expires}" : expires;
        if (ad.Tags.Length > 0)
        {
            meta = $"{meta} · {string.Join(", ", ad.Tags)}";
        }

        return meta;
    }

    private void DrawWrappedBody(string body, float scale)
    {
        if (body.Length == 0)
        {
            return;
        }

        using (Plugin.Fonts.Push(1f))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.YellowPages.BodyInk))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(body);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawScheduleCard(AdDto ad, long nowUnix, float scale)
    {
        if (ad.Schedule.Length == 0)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.YellowPages.ScheduleSection));
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var cardHeight = pad * 2f + ad.Schedule.Length * ScheduleRowHeight * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight),
            Metrics.Radius.Card * scale, elevated: true);
        for (var index = 0; index < ad.Schedule.Length; index++)
        {
            var lineTop = origin.Y + pad + index * ScheduleRowHeight * scale;
            var line = AdText.ScheduleSlotLine(ad.Schedule[index], nowUnix);
            Typography.Draw(drawList, new Vector2(origin.X + pad, lineTop), line,
                AppPalettes.YellowPages.BodyInk, TextStyles.Subheadline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Md * scale));
    }

    private void DrawCallCard(AdDto ad, float scale)
    {
        if (ad.Requirements.Length == 0)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.YellowPages.RequirementsSection));
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var textHeight = Typography.DrawWrappedLeft(origin, ad.Requirements,
            AppPalettes.YellowPages.BodyInk, TextStyles.Subheadline, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, textHeight + Metrics.Space.Md * scale));
    }

    private void DrawLocationBlock(AdDto ad, float scale)
    {
        var hasAddress = ad.Ward > 0 || ad.TerritoryId > 0 || ad.AddressNote.Length > 0;
        if (!hasAddress)
        {
            return;
        }

        ui.SectionHeading(Loc.T(L.YellowPages.WhereSection));
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
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var consumed = 0f;
        if (lineCount > 0)
        {
            var pad = Metrics.Space.Md * scale;
            var cardHeight = pad * 2f + lineCount * LocationRowHeight * scale;
            ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight),
                Metrics.Radius.Card * scale, elevated: true);
            for (var index = 0; index < lineCount; index++)
            {
                var lineTop = origin.Y + pad + index * LocationRowHeight * scale;
                var style = index == 0 ? TextStyles.BodyEmphasized : TextStyles.Subheadline;
                var ink = index == 0 ? AppPalettes.YellowPages.TitleInk : AppPalettes.YellowPages.BodyInk;
                var fitted = Typography.FitText(lines[index], width - pad * 2f, style);
                Typography.Draw(drawList, new Vector2(origin.X + pad, lineTop), fitted, ink, style);
            }

            consumed = cardHeight + Metrics.Space.Sm * scale;
        }

        var actionTop = origin.Y + consumed;
        var gap = Metrics.Space.Sm * scale;
        var actionHeight = LocationActionHeight * scale;
        var hasMap = ad.MapId != 0;
        var slots = hasMap ? 2 : 1;
        var slotWidth = (width - gap * (slots - 1)) / slots;
        var cursor = origin.X;
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

        var travelConsumed = 0f;
        if (CanTravelTo(ad))
        {
            var travelTop = actionTop + actionHeight + gap;
            var travelRect = new Rect(new Vector2(origin.X, travelTop),
                new Vector2(origin.X + width, travelTop + actionHeight));
            var travelLabel = JustCopied("travel") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.Travel);
            if (ui.PillButton(travelRect, travelLabel, true))
            {
                TravelTo(ad);
            }

            travelConsumed = actionHeight + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, consumed + actionHeight + travelConsumed + Metrics.Space.Lg * scale));
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
        var isMine = IsMineAd(ad.Id);
        if (isMine)
        {
            if (ui.PillButton(rect, Loc.T(L.YellowPages.ManageAction), true))
            {
                router.Pop(false);
                router.Push(YellowPagesRoute.Mine);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));
            return;
        }

        if (saveBusy)
        {
            AppSkin.PillButton(rect, ad.Saved ? Loc.T(L.YellowPages.Unsave) : Loc.T(L.YellowPages.Save),
                !ad.Saved, false, theme);
        }
        else if (ui.PillButton(rect, ad.Saved ? Loc.T(L.YellowPages.Unsave) : Loc.T(L.YellowPages.Save), !ad.Saved))
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

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));

        var shareOrigin = ImGui.GetCursorScreenPos();
        var shareRect = new Rect(shareOrigin, new Vector2(shareOrigin.X + width,
            shareOrigin.Y + LocationActionHeight * scale));
        var shareLabel = JustCopied("share") ? Loc.T(L.YellowPages.Copied) : Loc.T(L.YellowPages.ShareAd);
        if (ui.PillButton(shareRect, shareLabel, false))
        {
            Copy("share", AdShare.Compose(ad.Id));
        }

        ImGui.SetCursorScreenPos(shareOrigin);
        ImGui.Dummy(new Vector2(width, LocationActionHeight * scale + Metrics.Space.Md * scale));

        var reportOrigin = ImGui.GetCursorScreenPos();
        var reportLabel = Loc.T(L.Report.Action);
        var reportWidth = Typography.Measure(reportLabel, 0.9f, FontWeight.SemiBold).X + 40f * scale;
        var reportRect = new Rect(new Vector2(reportOrigin.X + (width - reportWidth) * 0.5f, reportOrigin.Y),
            new Vector2(reportOrigin.X + (width + reportWidth) * 0.5f, reportOrigin.Y + 34f * scale));
        if (ui.DangerGhostButton(reportRect, reportLabel))
        {
            OpenReport(ad.Id);
        }

        ImGui.SetCursorScreenPos(reportOrigin);
        ImGui.Dummy(new Vector2(width, 34f * scale + Metrics.Space.Md * scale));
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
