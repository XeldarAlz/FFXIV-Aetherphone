using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float ActionHeight = 52f;
    private const float LocationActionHeight = 44f;
    private const float LocationRowHeight = 24f;
    private const float NoticeBannerHeight = 48f;
    private const float HeroTopHeight = 108f;
    private const float HeroStatHeight = 54f;

    private static readonly int[] QuickStatusCodes =
    {
        MusterStatuses.OnMyWay, MusterStatuses.RunningLate, MusterStatuses.Here, MusterStatuses.WhereExactly,
    };

    private readonly string[] locationLines = new string[6];
    private readonly string[] statLabels = new string[3];
    private readonly string[] statValues = new string[3];
    private string? detailFetchId;
    private MusterDto? detailFetched;
    private bool detailLoading;
    private bool detailRsvpToggled;
    private bool rsvpBusy;
    private bool statusBusy;
    private string travelNotice = string.Empty;

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        detailRsvpToggled = false;
        rsvpBusy = false;
        statusBusy = false;
        travelNotice = string.Empty;
        travelNoticeTimer = 0f;
    }

    private void DrawDetail(Rect area, string musterId)
    {
        var muster = ResolveMuster(musterId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, muster is null ? DisplayName : Loc.T(MusterCategories.Label(muster.Category)), back);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        if (muster is null)
        {
            EnsureDetailFetch(musterId);
            if (detailLoading)
            {
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, ui.Accent,
                    AppPalettes.Muster.MutedInk, Loc.T(L.Common.Loading));
                return;
            }

            EmptyState.Draw(body, ui, FontAwesomeIcon.MapMarkerAlt, Loc.T(L.Muster.UnavailableTitle),
                Loc.T(L.Muster.UnavailableHint));
            return;
        }

        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawDetailHero(muster, nowUnix, scale);
            DrawNoticeBanner(muster, nowUnix, scale);
            DrawWrappedDescription(muster.Description, scale);
            DrawLocationBlock(muster, scale, includeTravel: true);
            DrawDetailActions(muster, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private MusterDto? ResolveMuster(string musterId)
    {
        if (store.Mine is { } mine && mine.Id == musterId)
        {
            return mine;
        }

        var contacts = store.ContactMusters;
        for (var index = 0; index < contacts.Length; index++)
        {
            if (contacts[index].Id == musterId)
            {
                return contacts[index];
            }
        }

        var going = store.GoingMusters;
        for (var index = 0; index < going.Length; index++)
        {
            if (going[index].Id == musterId)
            {
                return going[index];
            }
        }

        var directory = store.Directory;
        for (var index = 0; index < directory.Length; index++)
        {
            if (directory[index].Id == musterId)
            {
                return directory[index];
            }
        }

        var fetched = detailFetched;
        return fetched is not null && fetched.Id == musterId ? fetched : null;
    }

    private void EnsureDetailFetch(string musterId)
    {
        if (string.Equals(detailFetchId, musterId, StringComparison.Ordinal))
        {
            return;
        }

        detailFetchId = musterId;
        detailFetched = null;
        detailLoading = true;
        store.FetchDetail(musterId, muster =>
        {
            detailFetched = muster;
            detailLoading = false;
        });
    }

    private void DrawDetailHero(MusterDto muster, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (HeroTopHeight + HeroStatHeight) * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale * 1.3f;
        var live = muster.StartsAtUnix <= nowUnix;
        Elevation.Floating(drawList, card.Min, card.Max, rounding, scale, 0.85f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(ui.Accent, 0.10f) with { W = 0.42f }),
            ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.62f) with { W = 0.30f }));
        var stripTop = card.Min.Y + HeroTopHeight * scale;
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(ui.Accent, 0.45f), 0.32f)), 1f * scale);

        var pad = 16f * scale;
        var avatarRadius = 26f * scale;
        var avatarCenter = new Vector2(card.Min.X + pad + avatarRadius, card.Min.Y + pad + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, MusterText.HostLabel(muster), muster.HostWorld,
            null, images, lodestone, 1.25f, 40);
        drawList.AddCircle(avatarCenter, avatarRadius + 2f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f)), 40, 1.5f * scale);

        var textLeft = avatarCenter.X + avatarRadius + 14f * scale;
        var textRight = card.Max.X - pad;
        UserName.DrawAuto(drawList, "muster.detail.hero.name." + muster.Id, MusterText.HostLabel(muster),
            muster.HostBadges, muster.HostBadgeIds, textLeft, card.Min.Y + pad + 1f * scale, textRight - textLeft,
            TextStyles.Title2, AppPalettes.Muster.TitleInk, theme);
        Marquee.DrawLeftAuto(drawList, "muster.detail.hero.world." + muster.Id, muster.HostWorld, textLeft,
            card.Min.Y + pad + 28f * scale, textRight - textLeft, TextStyles.Subheadline,
            AppPalettes.Muster.BodyInk);

        var badgeLabel = live ? Loc.T(L.Common.Live)
            : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        var badgeTint = live ? MusterCard.LiveGreen : AppPalettes.Muster.TitleInk;
        DrawHeroBadge(drawList, new Vector2(textLeft, card.Min.Y + pad + 56f * scale), badgeLabel, badgeTint, live,
            scale);

        DrawHeroStats(drawList, muster, nowUnix, live, card, stripTop, pad, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private static void DrawHeroBadge(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 tint, bool live,
        float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var height = 24f * scale;
        var dotSpace = live ? 16f * scale : 0f;
        var min = topLeft;
        var max = new Vector2(topLeft.X + textSize.X + dotSpace + 22f * scale, topLeft.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.18f)));
        Squircle.Stroke(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(tint, 0.42f)),
            1f * scale);
        var centerY = (min.Y + max.Y) * 0.5f;
        if (live)
        {
            MusterCard.DrawLiveDot(drawList, new Vector2(min.X + 14f * scale, centerY), scale);
        }

        Typography.Draw(drawList, new Vector2(min.X + 11f * scale + dotSpace, centerY - textSize.Y * 0.5f), label,
            tint, TextStyles.FootnoteEmphasized);
    }

    private void DrawHeroStats(ImDrawListPtr drawList, MusterDto muster, long nowUnix, bool live, Rect card,
        float stripTop, float pad, float scale)
    {
        var capped = muster.MaxAttendees > 0;
        statLabels[0] = Loc.T(L.Muster.StatGoing);
        statValues[0] = capped
            ? $"{muster.RsvpCount}/{muster.MaxAttendees}"
            : muster.RsvpCount.ToString(Loc.Culture);
        statLabels[1] = live ? Loc.T(L.Muster.StatEndsIn) : Loc.T(L.Muster.StatStartsIn);
        statValues[1] = MusterText.Span((live ? muster.EndsAtUnix : muster.StartsAtUnix) - nowUnix);
        var slotCount = 2;
        if (capped)
        {
            statLabels[2] = Loc.T(L.Muster.StatSpots);
            statValues[2] = Math.Max(0, muster.MaxAttendees - muster.RsvpCount).ToString(Loc.Culture);
            slotCount = 3;
        }

        var slotWidth = (card.Width - pad * 2f) / 3f;
        var groupLeft = card.Min.X + (card.Width - slotWidth * slotCount) * 0.5f;
        var centerY = stripTop + (card.Max.Y - stripTop) * 0.5f;
        for (var index = 0; index < slotCount; index++)
        {
            var slotCenterX = groupLeft + slotWidth * (index + 0.5f);
            Typography.DrawCentered(drawList, new Vector2(slotCenterX, centerY - 9f * scale), statValues[index],
                AppPalettes.Muster.TitleInk, TextStyles.Title3);
            Typography.DrawCentered(drawList, new Vector2(slotCenterX, centerY + 12f * scale), statLabels[index],
                AppPalettes.Muster.MutedInk, TextStyles.Caption1);
        }
    }

    private void DrawNoticeBanner(MusterDto muster, long nowUnix, float scale)
    {
        if (muster.HostNotice == MusterNotices.None)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = NoticeBannerHeight * scale;
        var rounding = Metrics.Radius.Card * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)));
        Squircle.Stroke(drawList, origin, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.38f)),
            1f * scale);
        var centerY = origin.Y + height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(origin.X + 22f * scale, centerY),
            FontAwesomeIcon.Bullhorn.ToIconString(), ui.Accent, 0.8f);
        var ago = Loc.T(L.Muster.NoticeAgo, MusterText.Span(nowUnix - muster.HostNoticeAtUnix));
        var agoSize = Typography.Measure(ago, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(max.X - 16f * scale - agoSize.X, centerY - agoSize.Y * 0.5f), ago,
            AppPalettes.Muster.MutedInk, TextStyles.Footnote);
        var label = NoticeLabel(muster.HostNotice);
        var labelLeft = origin.X + 40f * scale;
        var fitted = Typography.FitText(label, max.X - 24f * scale - agoSize.X - labelLeft,
            TextStyles.BodyEmphasized);
        var labelSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), fitted,
            AppPalettes.Muster.TitleInk, TextStyles.BodyEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private static string NoticeLabel(int notice) =>
        notice switch
        {
            MusterNotices.StartingNow => Loc.T(L.Muster.NoticeStartingNow),
            MusterNotices.MovedSpots => Loc.T(L.Muster.NoticeMovedSpots),
            _ => Loc.T(L.Muster.NoticeWrappingUp),
        };

    private void DrawWrappedDescription(string description, float scale)
    {
        if (description.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = Metrics.Space.Md * scale;
        var textWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(description, TextStyles.Callout, textWidth).Y;
        var height = textHeight + pad * 2f;
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), rounding, elevated: true);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), description,
            AppPalettes.Muster.BodyInk, TextStyles.Callout, textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawLocationBlock(MusterDto muster, float scale, bool includeTravel)
    {
        ui.SectionHeading(Loc.T(L.Muster.WhereSection));
        var lineCount = 0;
        if (muster.Spot.Length > 0)
        {
            locationLines[lineCount++] = muster.Spot;
        }

        var place = MusterText.Place(muster);
        if (place.Length > 0 && !string.Equals(place, muster.Spot, StringComparison.Ordinal))
        {
            locationLines[lineCount++] = place;
        }

        var housing = MusterText.HousingLine(muster);
        if (housing.Length > 0)
        {
            locationLines[lineCount++] = housing;
        }

        var coordinates = MusterText.Coordinates(muster);
        if (coordinates.Length > 0)
        {
            locationLines[lineCount++] = coordinates;
        }

        var destination = includeTravel
            ? MusterTravel.Resolve(muster, store.CurrentWorldId, store.CurrentTerritoryId)
            : default;
        var alreadyThere = destination.Kind == MusterTravelKind.AlreadyThere;
        if (alreadyThere)
        {
            locationLines[lineCount++] = Loc.T(L.Muster.YoureHere);
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var consumed = 0f;
        if (lineCount > 0)
        {
            var pad = Metrics.Space.Md * scale;
            var iconLeft = origin.X + pad + 9f * scale;
            var textLeft = origin.X + pad + 26f * scale;
            var cardHeight = pad * 2f + lineCount * LocationRowHeight * scale;
            var rounding = Metrics.Radius.Card * scale;
            ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight), rounding,
                elevated: true);
            AppSkin.Icon(drawList, new Vector2(iconLeft, origin.Y + pad + 9f * scale),
                FontAwesomeIcon.MapMarkerAlt.ToIconString(), ui.Accent, 0.78f);
            for (var index = 0; index < lineCount; index++)
            {
                var lineTop = origin.Y + pad + index * LocationRowHeight * scale;
                var style = index == 0 ? TextStyles.BodyEmphasized : TextStyles.Callout;
                var ink = index == 0 ? AppPalettes.Muster.TitleInk : AppPalettes.Muster.BodyInk;
                if (locationLines[index] == coordinates && coordinates.Length > 0)
                {
                    style = TextStyles.Subheadline;
                    ink = AppPalettes.Muster.MutedInk;
                }
                else if (alreadyThere && index == lineCount - 1)
                {
                    style = TextStyles.SubheadlineEmphasized;
                    ink = MusterCard.LiveGreen;
                }

                Marquee.DrawLeftAuto(drawList, "muster.detail.location." + muster.Id + "." + index,
                    locationLines[index], textLeft, lineTop, origin.X + width - pad - textLeft, style, ink);
            }

            consumed = cardHeight + Metrics.Space.Sm * scale;
        }

        var actionTop = origin.Y + consumed;
        var gap = Metrics.Space.Sm * scale;
        var actionHeight = LocationActionHeight * scale;
        var actionsHeight = 0f;
        if (muster.MapId != 0)
        {
            var flagRect = new Rect(new Vector2(origin.X, actionTop),
                new Vector2(origin.X + width, actionTop + actionHeight));
            if (ui.PillButton(flagRect, Loc.T(L.Muster.FlagOnMap), false, "muster.detail.flagonmap"))
            {
                var location = MusterText.Location(muster);
                LocationShare.OpenMap(in location);
            }

            actionsHeight = actionHeight;
        }

        if (MusterTravel.CanGo(in destination))
        {
            var travelTop = actionTop + (actionsHeight > 0f ? actionsHeight + gap : 0f);
            var travelRect = new Rect(new Vector2(origin.X, travelTop),
                new Vector2(origin.X + width, travelTop + actionHeight));
            var travelLabel = JustCopied("travel")
                ? Loc.T(L.Muster.Copied)
                : destination.Kind == MusterTravelKind.World
                    ? Loc.T(L.Muster.TravelTo, destination.Name)
                    : Loc.T(L.Muster.TeleportTo, destination.Name);
            if (ui.PillButton(travelRect, travelLabel, true, "muster.detail.travel"))
            {
                TravelTo(in destination);
            }

            actionsHeight += (actionsHeight > 0f ? gap : 0f) + actionHeight;
            actionsHeight += DrawTravelNotice(origin.X, travelTop + actionHeight, width, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, consumed + actionsHeight + Metrics.Space.Lg * scale));
    }

    private float DrawTravelNotice(float left, float top, float width, float scale)
    {
        if (travelNoticeTimer <= 0f || travelNotice.Length == 0)
        {
            return 0f;
        }

        var pad = Metrics.Space.Md * scale;
        var textWidth = width - pad * 2f;
        var gap = Metrics.Space.Xs * scale;
        Typography.DrawWrappedLeft(new Vector2(left + pad, top + gap), travelNotice, theme.Danger,
            TextStyles.Footnote, textWidth);
        return gap + Typography.MeasureWrappedBlock(travelNotice, TextStyles.Footnote, textWidth).Y;
    }

    private void TravelTo(in MusterDestination destination)
    {
        travelNotice = string.Empty;
        travelNoticeTimer = 0f;
        var outcome = MusterTravel.Go(in destination);
        lifestreamAvailable = outcome != LifestreamOutcome.NotInstalled;
        if (outcome == LifestreamOutcome.Started)
        {
            return;
        }

        if (outcome == LifestreamOutcome.NotInstalled)
        {
            Copy("travel", MusterTravel.Command(in destination));
            return;
        }

        travelNotice = TravelNotice(outcome, in destination);
        travelNoticeTimer = NoticeSeconds;
    }

    private static string TravelNotice(LifestreamOutcome outcome, in MusterDestination destination) =>
        outcome switch
        {
            LifestreamOutcome.Busy => Loc.T(L.Muster.TravelBusy),
            LifestreamOutcome.NotAttuned => Loc.T(L.Muster.TravelNotAttuned, destination.GatewayName),
            LifestreamOutcome.WorldUnreachable => Loc.T(L.Muster.TravelNoWorld, destination.Name),
            _ => Loc.T(L.Muster.TravelBlocked),
        };

    private void DrawDetailActions(MusterDto muster, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + ActionHeight * scale));
        var isMine = store.Mine is { } mine && mine.Id == muster.Id;
        if (isMine)
        {
            if (ui.PillButton(rect, Loc.T(L.Muster.ManageAction), true))
            {
                router.Pop(false);
                OpenManage(false);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));
            return;
        }

        var going = detailRsvpToggled
            ? store.IsGoing(muster.Id)
            : muster.Going || store.IsGoing(muster.Id);
        var rsvpLabel = going ? Loc.T(L.Muster.CantMakeIt) : Loc.T(L.Muster.ImGoing);
        if (rsvpBusy)
        {
            AppSkin.PillButton(rect, rsvpLabel, !going, false, theme);
        }
        else if (ui.PillButton(rect, rsvpLabel, !going))
        {
            rsvpBusy = true;
            var next = !going;
            store.SetRsvp(muster.Id, next, ok =>
            {
                rsvpBusy = false;
                if (ok)
                {
                    detailRsvpToggled = true;
                }
            });
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));
        if (going)
        {
            DrawQuickStatus(muster, scale);
        }

        var reportOrigin = ImGui.GetCursorScreenPos();
        var reportLabel = Loc.T(L.Report.Action);
        var reportWidth = Typography.Measure(reportLabel, 0.9f, FontWeight.SemiBold).X + 40f * scale;
        var reportRect = new Rect(new Vector2(reportOrigin.X + (width - reportWidth) * 0.5f, reportOrigin.Y),
            new Vector2(reportOrigin.X + (width + reportWidth) * 0.5f, reportOrigin.Y + 34f * scale));
        if (ui.DangerGhostButton(reportRect, reportLabel))
        {
            OpenReport(muster.Id);
        }

        ImGui.SetCursorScreenPos(reportOrigin);
        ImGui.Dummy(new Vector2(width, 34f * scale + Metrics.Space.Md * scale));
    }

    private void DrawQuickStatus(MusterDto muster, float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.YourStatus));
        var current = store.MyStatus(muster.Id);
        chipLabels[0] = Loc.T(L.Muster.OnMyWay);
        chipLabels[1] = Loc.T(L.Muster.StatusRunningLate);
        chipLabels[2] = Loc.T(L.Muster.StatusHere);
        chipLabels[3] = Loc.T(L.Muster.StatusWhereExactly);
        for (var index = 0; index < QuickStatusCodes.Length; index++)
        {
            chipActive[index] = QuickStatusCodes[index] == current;
        }

        var tapped = DrawChipFlow(QuickStatusCodes.Length, scale);
        if (tapped >= 0 && !statusBusy && QuickStatusCodes[tapped] != current)
        {
            statusBusy = true;
            store.SetStatus(muster.Id, QuickStatusCodes[tapped], _ => statusBusy = false);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void OpenReport(string musterId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Muster.ReportTitle),
            Submit = (reason, done) => SubmitReport(musterId, reason, done),
        });
    }
}
