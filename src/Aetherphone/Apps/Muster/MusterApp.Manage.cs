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
    private const float StatusCardHeight = 92f;
    private const float AttendeeRowHeight = 44f;
    private const float ManageActionHeight = 46f;

    private static readonly Vector4 StatusAmber = new(0.98f, 0.72f, 0.30f, 1f);

    private static readonly int[] NoticeCodes =
    {
        MusterNotices.StartingNow, MusterNotices.MovedSpots, MusterNotices.WrappingUp,
    };

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
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var nowUnix = NowUnix();
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
            DrawManageStatus(mine, nowUnix, scale);
            DrawWrappedDescription(mine.Description, scale);
            DrawLocationBlock(mine, "manage", scale, includeTravel: false);
            DrawAttendees(scale);
            DrawNotices(mine, scale);
            DrawManageActions(mine, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawManageStatus(MusterDto mine, long nowUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = StatusCardHeight * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), Metrics.Radius.Card * scale,
            elevated: true);
        var tileSide = 40f * scale;
        var tileCenter = new Vector2(origin.X + 14f * scale + tileSide * 0.5f, origin.Y + 14f * scale
            + tileSide * 0.5f);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), MusterCategories.Icon(mine.Category));
        var textLeft = tileCenter.X + tileSide * 0.5f + 12f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 14f * scale),
            Loc.T(MusterCategories.Label(mine.Category)), AppPalettes.Muster.TitleInk, TextStyles.Headline);
        var live = mine.StartsAtUnix <= nowUnix;
        var status = live
            ? $"{Loc.T(L.Common.Live)} · {Loc.T(L.Muster.EndsIn, MusterText.Span(mine.EndsAtUnix - nowUnix))}"
            : $"{Loc.T(L.Muster.StartsAt, TimeText.Clock(mine.StartsAtUnix))} · {Loc.T(L.Muster.RunsFor, MusterText.Span(mine.EndsAtUnix - mine.StartsAtUnix))}";
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 37f * scale), status,
            live ? MusterCard.LiveGreen : ui.Accent, TextStyles.FootnoteEmphasized);
        var capacity = mine.MaxAttendees > 0
            ? Loc.T(L.Muster.CapacityLine, mine.RsvpCount, mine.MaxAttendees)
            : Loc.T(L.Muster.GoingCount, mine.RsvpCount);
        var listed = mine.IsPublic ? Loc.T(L.Muster.ListedPublicly) : Loc.T(L.Muster.ListedPrivately);
        var meta = $"{capacity} · {listed}";
        var fitted = Typography.FitText(meta, width - (textLeft - origin.X) - 14f * scale, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + 60f * scale), fitted,
            AppPalettes.Muster.MutedInk, TextStyles.Footnote);
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
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (attendees.Length == 0)
        {
            Typography.Draw(new Vector2(origin.X, origin.Y), Loc.T(L.Muster.NoAttendees),
                AppPalettes.Muster.MutedInk, TextStyles.Subheadline);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, 26f * scale + Metrics.Space.Md * scale));
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = AttendeeRowHeight * scale;
        for (var index = 0; index < attendees.Length; index++)
        {
            DrawAttendeeRow(drawList, attendees[index], attendeeIdentities[index], origin,
                origin.Y + index * rowHeight, width, rowHeight, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, attendees.Length * rowHeight + Metrics.Space.Md * scale));
    }

    private void DrawAttendeeRow(ImDrawListPtr drawList, MusterAttendeeDto attendee, string identity,
        Vector2 origin, float rowTop, float width, float rowHeight, float scale)
    {
        var centerY = rowTop + rowHeight * 0.5f;
        var avatarRadius = 15f * scale;
        var avatarCenter = new Vector2(origin.X + avatarRadius, centerY);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, attendee.CharacterName, attendee.World,
            null, images, lodestone, 0.85f, 32);
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

        if (attendee.Status > MusterStatuses.OnMyWay)
        {
            string chipLabel;
            Vector4 chipColor;
            switch (attendee.Status)
            {
                case MusterStatuses.RunningLate:
                    chipLabel = Loc.T(L.Muster.StatusRunningLate);
                    chipColor = StatusAmber;
                    break;
                case MusterStatuses.Here:
                    chipLabel = Loc.T(L.Muster.StatusHere);
                    chipColor = MusterCard.LiveGreen;
                    break;
                default:
                    chipLabel = Loc.T(L.Muster.StatusWhereExactly);
                    chipColor = ui.Accent;
                    break;
            }

            var chipTextSize = Typography.Measure(chipLabel, TextStyles.Caption1);
            var chipHeight = 20f * scale;
            var chipWidth = chipTextSize.X + 14f * scale;
            var chipMin = new Vector2(cursorRight - chipWidth, centerY - chipHeight * 0.5f);
            var chipMax = new Vector2(cursorRight, centerY + chipHeight * 0.5f);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(chipColor, 0.18f)));
            Typography.Draw(drawList, new Vector2(chipMin.X + 7f * scale, centerY - chipTextSize.Y * 0.5f),
                chipLabel, chipColor, TextStyles.Caption1);
            cursorRight = chipMin.X - 8f * scale;
        }

        var nameLeft = avatarCenter.X + avatarRadius + 10f * scale;
        var fitted = Typography.FitText(identity, cursorRight - 4f * scale - nameLeft, TextStyles.BodyEmphasized);
        var nameSize = Typography.Measure(fitted, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(nameLeft, centerY - nameSize.Y * 0.5f), fitted,
            AppPalettes.Muster.TitleInk, TextStyles.BodyEmphasized);
    }

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
