using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float StatusCardHeight = 96f;
    private const float AttendeeRowHeight = 52f;
    private const float ManageActionHeight = 48f;

    private static readonly Vector4 StatusAmber = new(0.98f, 0.72f, 0.30f, 1f);
    private static readonly Vector4 StatusEnRoute = new(0.42f, 0.72f, 0.98f, 1f);

    private static readonly int[] NoticeCodes =
    {
        MusterNotices.StartingNow, MusterNotices.MovedSpots, MusterNotices.WrappingUp,
    };

    private readonly PullToRefresh manageRefresh = new();
    private MusterAttendeeDto[] lastAttendees = Array.Empty<MusterAttendeeDto>();
    private string[] attendeeIdentities = Array.Empty<string>();
    private bool noticeBusy;
    private float invitedTimer;
    private string invitedUserId = string.Empty;

    private void ResetManageState()
    {
        noticeBusy = false;
        invitedTimer = 0f;
        invitedUserId = string.Empty;
    }

    private void OpenManage(bool animate = true)
    {
        ResetManageState();
        store.SyncNow();
        router.Push(MusterRoute.Manage, animate);
    }

    private void DrawManage(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Muster.YourMuster), back);
        if (store.Mine is not { } mine)
        {
            router.Pop(false);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        DrawManageHeaderAction(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var nowUnix = NowUnix();
        using (var surface = AppSurface.Begin(body))
        {
            manageRefresh.Draw(body, surface.Pull, surface.Dragging, store.Syncing, AppPalettes.Muster.MutedInk,
                store.SyncNow);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawManageStatus(mine, nowUnix, scale);
            DrawWrappedDescription(mine.Description, scale);
            DrawLocationBlock(mine, scale, includeTravel: false);
            DrawAttendees(scale);
            DrawNotices(mine, scale);
            DrawManageActions(mine, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawManageHeaderAction(Rect area, float scale)
    {
        var center = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        if (store.Syncing)
        {
            LoadingPulse.Spinner(center, 8f * scale, ui.Accent);
            return;
        }

        if (ui.IconButton(center, 14f * scale, FontAwesomeIcon.Sync.ToIconString(), AppPalettes.Muster.BodyInk,
                AppSkin.Transparent, 0.9f))
        {
            store.SyncNow();
        }
    }

    private void DrawManageStatus(MusterDto mine, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StatusCardHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = Metrics.Radius.Card * scale * 1.2f;
        Elevation.Floating(drawList, origin, max, rounding, scale, 0.8f);
        Squircle.FillVerticalGradient(drawList, origin, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.26f)),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.06f)));
        Squircle.Stroke(drawList, origin, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.38f)), 1f * scale);
        var tileSide = 42f * scale;
        var tileCenter = new Vector2(origin.X + 16f * scale + tileSide * 0.5f, origin.Y + 17f * scale
            + tileSide * 0.5f);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), MusterCategories.Icon(mine.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 13f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 16f * scale),
            Loc.T(MusterCategories.Label(mine.Category)), AppPalettes.Muster.TitleInk, TextStyles.Title3);
        var live = mine.StartsAtUnix <= nowUnix;
        var status = live
            ? $"{Loc.T(L.Common.Live)} · {Loc.T(L.Muster.EndsIn, MusterText.Span(mine.EndsAtUnix - nowUnix))}"
            : $"{Loc.T(L.Muster.StartsAt, TimeText.Clock(mine.StartsAtUnix))} · {Loc.T(L.Muster.RunsFor, MusterText.Span(mine.EndsAtUnix - mine.StartsAtUnix))}";
        var statusLeft = textLeft;
        if (live)
        {
            MusterCard.DrawLiveDot(drawList, new Vector2(textLeft + 4f * scale, origin.Y + 47f * scale), scale);
            statusLeft += 14f * scale;
        }

        var fittedStatus = Typography.FitText(status, max.X - 16f * scale - statusLeft,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(statusLeft, origin.Y + 40f * scale), fittedStatus,
            live ? MusterCard.LiveGreen : AppPalettes.Muster.BodyInk, TextStyles.SubheadlineEmphasized);
        var capacity = mine.MaxAttendees > 0
            ? Loc.T(L.Muster.CapacityLine, mine.RsvpCount, mine.MaxAttendees)
            : Loc.T(L.Muster.GoingCount, mine.RsvpCount);
        var listed = mine.IsPublic ? Loc.T(L.Muster.ListedPublicly) : Loc.T(L.Muster.ListedPrivately);
        var meta = $"{capacity} · {listed}";
        var fitted = Typography.FitText(meta, max.X - 16f * scale - textLeft, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 64f * scale), fitted,
            AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void EnsureAttendeeIdentities(MusterAttendeeDto[] attendees)
    {
        if (ReferenceEquals(attendees, lastAttendees))
        {
            return;
        }

        lastAttendees = attendees;
        if (attendeeIdentities.Length != attendees.Length)
        {
            attendeeIdentities = new string[attendees.Length];
        }

        for (var index = 0; index < attendees.Length; index++)
        {
            var attendee = attendees[index];
            attendeeIdentities[index] = attendee.World.Length > 0
                ? $"{attendee.CharacterName} · {attendee.World}"
                : attendee.CharacterName;
        }
    }

    private void DrawAttendees(float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.AttendeesSection));
        var attendees = store.MineAttendees;
        EnsureAttendeeIdentities(attendees);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rounding = Metrics.Radius.Card * scale;
        if (attendees.Length == 0)
        {
            var emptyHeight = 56f * scale;
            ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + emptyHeight), rounding,
                elevated: true);
            Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, origin.Y + emptyHeight * 0.5f),
                Loc.T(L.Muster.NoAttendees), AppPalettes.Muster.MutedInk, TextStyles.Callout);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, emptyHeight + Metrics.Space.Md * scale));
            return;
        }

        var rowHeight = AttendeeRowHeight * scale;
        var pad = Metrics.Space.Sm * scale;
        var cardHeight = attendees.Length * rowHeight + pad * 2f;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + cardHeight), rounding, elevated: true);
        var rowInset = Metrics.Space.Md * scale;
        for (var index = 0; index < attendees.Length; index++)
        {
            var rowTop = origin.Y + pad + index * rowHeight;
            DrawAttendeeRow(drawList, attendees[index], attendeeIdentities[index],
                new Vector2(origin.X + rowInset, origin.Y), rowTop, width - rowInset * 2f, rowHeight, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + Metrics.Space.Md * scale));
    }

    private void DrawAttendeeRow(ImDrawListPtr drawList, MusterAttendeeDto attendee, string identity,
        Vector2 origin, float rowTop, float width, float rowHeight, float scale)
    {
        var centerY = rowTop + rowHeight * 0.5f;
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, centerY);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, attendee.CharacterName, attendee.World,
            null, images, lodestone, 0.9f, 32);
        var rowRight = origin.X + width;
        var cursorRight = rowRight;
        var inviteRadius = 14f * scale;
        var justInvited = invitedTimer > 0f && string.Equals(invitedUserId, attendee.UserId, StringComparison.Ordinal);
        if (justInvited)
        {
            var invitedLabel = Loc.T(L.Muster.Invited);
            var invitedSize = Typography.Measure(invitedLabel, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(rowRight - invitedSize.X, centerY - invitedSize.Y * 0.5f),
                invitedLabel, ui.Accent, TextStyles.FootnoteEmphasized);
            cursorRight -= invitedSize.X + 8f * scale;
        }
        else
        {
            var inviteCenter = new Vector2(rowRight - inviteRadius, centerY);
            if (MusterPartyInvite.CanInvite(attendee.World))
            {
                if (ui.IconButton(inviteCenter, inviteRadius, FontAwesomeIcon.UserPlus.ToIconString(), ui.Accent,
                        AppPalettes.Muster.FieldSurface, 0.6f, Loc.T(L.Muster.InviteToParty))
                    && MusterPartyInvite.Invite(attendee.CharacterName, attendee.World))
                {
                    invitedUserId = attendee.UserId;
                    invitedTimer = CopiedSeconds;
                }
            }
            else
            {
                AppSkin.Icon(drawList, inviteCenter, FontAwesomeIcon.UserPlus.ToIconString(),
                    Palette.WithAlpha(AppPalettes.Muster.MutedInk, 0.55f), 0.6f);
                var hit = new Vector2(inviteRadius, inviteRadius);
                HoverTooltip.Show(new Rect(inviteCenter - hit, inviteCenter + hit),
                    Loc.T(L.Muster.DifferentDataCenter), HoverLabelSide.Above);
            }

            cursorRight -= inviteRadius * 2f + 8f * scale;
        }

        if (attendee.Status >= MusterStatuses.OnMyWay)
        {
            var chipLabel = StatusLabel(attendee.Status);
            var chipColor = StatusColor(attendee.Status);
            var chipTextSize = Typography.Measure(chipLabel, TextStyles.FootnoteEmphasized);
            var chipHeight = 22f * scale;
            var chipWidth = chipTextSize.X + 18f * scale;
            var chipMin = new Vector2(cursorRight - chipWidth, centerY - chipHeight * 0.5f);
            var chipMax = new Vector2(cursorRight, centerY + chipHeight * 0.5f);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(chipColor, 0.20f)));
            Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(chipColor, 0.38f)), 1f * scale);
            Typography.Draw(drawList, new Vector2(chipMin.X + 9f * scale, centerY - chipTextSize.Y * 0.5f),
                chipLabel, chipColor, TextStyles.FootnoteEmphasized);
            cursorRight = chipMin.X - 8f * scale;
        }

        var nameLeft = avatarCenter.X + avatarRadius + 11f * scale;
        var fitted = Typography.FitText(identity, cursorRight - 4f * scale - nameLeft, TextStyles.BodyEmphasized);
        var nameSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(nameLeft, centerY - nameSize.Y * 0.5f), fitted,
            AppPalettes.Muster.TitleInk, TextStyles.BodyEmphasized);
    }

    private static string StatusLabel(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => Loc.T(L.Muster.StatusRunningLate),
            MusterStatuses.Here => Loc.T(L.Muster.StatusHere),
            MusterStatuses.WhereExactly => Loc.T(L.Muster.StatusWhereExactly),
            _ => Loc.T(L.Muster.OnMyWay),
        };

    private Vector4 StatusColor(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => StatusAmber,
            MusterStatuses.Here => MusterCard.LiveGreen,
            MusterStatuses.WhereExactly => ui.Accent,
            _ => StatusEnRoute,
        };

    private void DrawNotices(MusterDto mine, float scale)
    {
        ui.SectionHeading(Loc.T(L.Muster.NoticesSection));
        chipLabels[0] = Loc.T(L.Muster.NoticeStartingNow);
        chipLabels[1] = Loc.T(L.Muster.NoticeMovedSpots);
        chipLabels[2] = Loc.T(L.Muster.NoticeWrappingUp);
        for (var index = 0; index < NoticeCodes.Length; index++)
        {
            chipActive[index] = mine.HostNotice == NoticeCodes[index];
        }

        var tapped = DrawChipFlow(NoticeCodes.Length, scale);
        if (tapped >= 0 && !noticeBusy)
        {
            SendNotice(NoticeCodes[tapped]);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    // Moved spots captures the host's live location on the framework thread (tap handler); on a failed
    // capture the notice still goes out with zeroed fields and the server keeps the previous spot.
    private void SendNotice(int notice)
    {
        noticeBusy = true;
        SetMusterNoticeRequest request;
        if (notice == MusterNotices.MovedSpots && LocationShare.Capture() is { } location)
        {
            request = new SetMusterNoticeRequest(notice, (int)location.TerritoryId, (int)location.MapId,
                location.MapX, location.MapY, (int)location.WorldId, location.Ward, location.Plot, location.Room,
                null);
        }
        else
        {
            request = new SetMusterNoticeRequest(notice, 0, 0, 0f, 0f, 0, 0, 0, 0, null);
        }

        store.SetNotice(request, _ => noticeBusy = false);
    }

    private void DrawManageActions(MusterDto mine, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = ManageActionHeight * scale;
        var copyRect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var copyLabel = JustCopied("invite") ? Loc.T(L.Muster.Copied) : Loc.T(L.Muster.CopyInvite);
        if (ui.PillButton(copyRect, copyLabel, false))
        {
            Copy("invite", MusterShare.Compose(mine.Id));
        }

        var endTop = copyRect.Max.Y + Metrics.Space.Md * scale;
        var endRect = new Rect(new Vector2(origin.X, endTop), new Vector2(origin.X + width, endTop + height));
        if (ui.DangerGhostButton(endRect, Loc.T(L.Muster.EndMuster)))
        {
            AskEndMuster();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, endRect.Max.Y - origin.Y));
    }

    private void AskEndMuster()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Muster.EndMuster),
            Message = Loc.T(L.Muster.EndConfirm),
            ConfirmLabel = Loc.T(L.Muster.EndMuster),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Muster.Ending),
            FailedMessage = Loc.T(L.Muster.EndFailed),
            Danger = true,
            ConfirmAsync = done => store.EndMine(done),
        });
    }
}
