using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Report;
using Aetherphone.Core.Theme;
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

    private readonly string[] locationLines = new string[5];
    private string? detailFetchId;
    private MusterDto? detailFetched;
    private bool detailLoading;
    private bool detailRsvpToggled;
    private bool rsvpBusy;

    private void ResetDetailState()
    {
        detailFetchId = null;
        detailFetched = null;
        detailLoading = false;
        detailRsvpToggled = false;
        rsvpBusy = false;
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
            DrawWrappedDescription(muster.Description, scale);
            DrawLocationBlock(muster, "detail", scale);
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
        var iconCenter = new Vector2(centerX, origin.Y + 44f * scale);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 48);
        AppSkin.Icon(drawList, iconCenter, MusterCategories.Icon(muster.Category).ToIconString(), ui.Accent, 1.45f);
        var cursorY = iconCenter.Y + 34f * scale + 22f * scale;
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

    private void DrawLocationBlock(MusterDto muster, string copyKey, float scale)
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

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, consumed + actionHeight + Metrics.Space.Lg * scale));
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

        var reportTop = rect.Max.Y + Metrics.Space.Md * scale;
        var reportLabel = Loc.T(L.Report.Action);
        var reportWidth = Typography.Measure(reportLabel, 0.9f, FontWeight.SemiBold).X + 40f * scale;
        var reportRect = new Rect(new Vector2(origin.X + (width - reportWidth) * 0.5f, reportTop),
            new Vector2(origin.X + (width + reportWidth) * 0.5f, reportTop + 34f * scale));
        if (ui.DangerGhostButton(reportRect, reportLabel))
        {
            OpenReport(muster.Id);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width,
            ActionHeight * scale + Metrics.Space.Md * scale + 34f * scale + Metrics.Space.Md * scale));
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
        builder.Append(muster.HostDisplayName);
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
