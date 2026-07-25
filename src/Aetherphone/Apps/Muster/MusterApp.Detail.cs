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
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float ActionHeight = 50f;
    private const float LocationActionHeight = 42f;
    private const float LocationRowHeight = 22f;
    private const float NoticeBannerHeight = 44f;

    private static readonly int[] QuickStatusCodes =
    {
        MusterStatuses.OnMyWay, MusterStatuses.RunningLate, MusterStatuses.Here, MusterStatuses.WhereExactly,
    };

    private readonly string[] locationLines = new string[5];
    private string? detailFetchId;
    private MusterDto? detailFetched;
    private bool detailLoading;
    private bool detailRsvpToggled;
    private bool rsvpBusy;
    private bool statusBusy;

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        detailRsvpToggled = false;
        rsvpBusy = false;
        statusBusy = false;
    }

    private void DrawDetail(Rect area, string musterId)
    {
        var muster = ResolveMuster(musterId);
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, muster is null ? DisplayName : Loc.T(MusterCategories.Label(muster.Category)), back);
        var scale = ImGuiHelpers.GlobalScale;
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
            DrawDetailHero(muster, nowUnix, scale);
            DrawNoticeBanner(muster, nowUnix, scale);
            DrawWrappedDescription(muster.Description, scale);
            DrawLocationBlock(muster, "detail", scale, includeTravel: true);
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
        var centerX = origin.X + width * 0.5f;
        var avatarRadius = 34f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + 10f * scale + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, muster.HostCharacter, muster.HostWorld,
            null, images, lodestone, 1.3f, 48);
        var cursorY = avatarCenter.Y + avatarRadius + 22f * scale;
        Typography.DrawCentered(new Vector2(centerX, cursorY), Loc.T(MusterCategories.Label(muster.Category)),
            AppPalettes.Muster.TitleInk, TextStyles.Title3);
        cursorY += 26f * scale;
        Typography.DrawCentered(new Vector2(centerX, cursorY), MusterText.Identity(muster),
            AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
        cursorY += 24f * scale;
        var live = muster.StartsAtUnix <= nowUnix;
        var status = live
            ? $"{Loc.T(L.Common.Live)} · {Loc.T(L.Muster.EndsIn, MusterText.Span(muster.EndsAtUnix - nowUnix))}"
            : $"{Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix))} · {Loc.T(L.Muster.RunsFor, MusterText.Span(muster.EndsAtUnix - muster.StartsAtUnix))}";
        Typography.DrawCentered(new Vector2(centerX, cursorY), status, live ? MusterCard.LiveGreen : ui.Accent,
            TextStyles.FootnoteEmphasized);
        cursorY += 22f * scale;
        var going = Loc.T(L.Muster.GoingCount, muster.RsvpCount);
        if (muster.MaxAttendees > 0 && muster.RsvpCount >= muster.MaxAttendees)
        {
            going = $"{going} · {Loc.T(L.Muster.AtCapacity)}";
        }

        Typography.DrawCentered(new Vector2(centerX, cursorY), going, AppPalettes.Muster.MutedInk,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + 26f * scale));
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
        var rounding = Metrics.Radius.Md * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)));
        Squircle.Stroke(drawList, origin, max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)),
            1f);
        var centerY = origin.Y + height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(origin.X + 20f * scale, centerY),
            FontAwesomeIcon.Bullhorn.ToIconString(), ui.Accent, 0.8f);
        var ago = Loc.T(L.Muster.NoticeAgo, MusterText.Span(nowUnix - muster.HostNoticeAtUnix));
        var agoSize = Typography.Measure(ago, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(max.X - 14f * scale - agoSize.X, centerY - agoSize.Y * 0.5f), ago,
            AppPalettes.Muster.MutedInk, TextStyles.Footnote);
        var label = NoticeLabel(muster.HostNotice);
        var labelLeft = origin.X + 36f * scale;
        var fitted = Typography.FitText(label, max.X - 22f * scale - agoSize.X - labelLeft,
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

        using (Plugin.Fonts.Push(1f))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Muster.BodyInk))
        {
            ImGui.PushTextWrapPos(0f);
            Typography.Wrapped(description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawLocationBlock(MusterDto muster, string copyKey, float scale, bool includeTravel)
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
                var ink = index == 0 ? AppPalettes.Muster.TitleInk : AppPalettes.Muster.BodyInk;
                if (locationLines[index] == coordinates && coordinates.Length > 0)
                {
                    style = TextStyles.Footnote;
                    ink = AppPalettes.Muster.MutedInk;
                }

                var fitted = Typography.FitText(locationLines[index], width - pad * 2f, style);
                Typography.Draw(drawList, new Vector2(origin.X + pad, lineTop), fitted, ink, style);
            }

            consumed = cardHeight + Metrics.Space.Sm * scale;
        }

        var actionTop = origin.Y + consumed;
        var gap = Metrics.Space.Sm * scale;
        var actionHeight = LocationActionHeight * scale;
        var hasMap = muster.MapId != 0;
        var slots = hasMap ? 2 : 1;
        var slotWidth = (width - gap * (slots - 1)) / slots;
        var cursor = origin.X;
        if (hasMap)
        {
            var flagRect = new Rect(new Vector2(cursor, actionTop),
                new Vector2(cursor + slotWidth, actionTop + actionHeight));
            if (ui.PillButton(flagRect, Loc.T(L.Muster.FlagOnMap), false))
            {
                var location = MusterText.Location(muster);
                LocationShare.OpenMap(in location);
            }

            cursor += slotWidth + gap;
        }

        var copyRect = new Rect(new Vector2(cursor, actionTop),
            new Vector2(cursor + slotWidth, actionTop + actionHeight));
        var copyLabel = JustCopied(copyKey) ? Loc.T(L.Muster.Copied) : Loc.T(L.Muster.CopyDetails);
        if (ui.PillButton(copyRect, copyLabel, false))
        {
            Copy(copyKey, BuildCopySummary(muster));
        }

        var travelConsumed = 0f;
        if (includeTravel && CanTravelTo(muster))
        {
            var travelTop = actionTop + actionHeight + gap;
            var travelRect = new Rect(new Vector2(origin.X, travelTop),
                new Vector2(origin.X + width, travelTop + actionHeight));
            var travelLabel = JustCopied("travel") ? Loc.T(L.Muster.Copied) : Loc.T(L.Muster.Travel);
            if (ui.PillButton(travelRect, travelLabel, true))
            {
                TravelTo(muster);
            }

            travelConsumed = actionHeight + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, consumed + actionHeight + travelConsumed + Metrics.Space.Lg * scale));
    }

    private static bool CanTravelTo(MusterDto muster)
    {
        if (muster.WorldId == 0)
        {
            return false;
        }

        var currentWorldId = MusterWorlds.CurrentWorldId();
        return currentWorldId != 0 && currentWorldId != (uint)muster.WorldId;
    }

    private void TravelTo(MusterDto muster)
    {
        var worldName = LocationShare.WorldName((uint)muster.WorldId);
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
                router.Push(MusterRoute.Manage);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, ActionHeight * scale + Metrics.Space.Md * scale));
            return;
        }

        var going = detailRsvpToggled
            ? store.IsGoing(muster.Id)
            : muster.Going || store.IsGoing(muster.Id);
        if (rsvpBusy)
        {
            AppSkin.PillButton(rect, going ? Loc.T(L.Muster.CantMakeIt) : Loc.T(L.Muster.OnMyWay), !going, false,
                theme);
        }
        else if (ui.PillButton(rect, going ? Loc.T(L.Muster.CantMakeIt) : Loc.T(L.Muster.OnMyWay), !going))
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

    private static string BuildCopySummary(MusterDto muster)
    {
        var builder = new StringBuilder(256);
        builder.Append(Loc.T(MusterCategories.Label(muster.Category)));
        builder.Append(" · ");
        builder.Append(MusterText.Identity(muster));
        if (muster.Description.Length > 0)
        {
            builder.Append('\n');
            builder.Append(muster.Description);
        }

        if (muster.Spot.Length > 0)
        {
            builder.Append('\n');
            builder.Append(muster.Spot);
        }

        var location = MusterText.Location(muster);
        var summary = LocationShare.Summary(in location);
        if (summary.Length > 0)
        {
            builder.Append('\n');
            builder.Append(summary);
        }

        return builder.ToString();
    }
}
