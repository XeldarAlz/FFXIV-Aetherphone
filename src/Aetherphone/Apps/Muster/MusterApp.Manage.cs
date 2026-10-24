using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Muster;

internal sealed partial class MusterApp
{
    private const float InvitedSeconds = 1.6f;
    private const float AttendeeRowHeight = 54f;
    private const float AttendeeAvatarRadius = 16f;
    private const float InviteRadius = RoundButton.SmallRadius;
    private const float InviteGlyphFraction = 0.9f;
    private const float TallyBarHeight = 8f;
    private const float TallyLegendRowHeight = 26f;
    private const float TallyDotRadius = 4f;
    private const float NoticeTileHeight = 72f;
    private const float EmptyAttendeesHeight = 64f;
    private const int RefreshButton = 0;

    private static readonly int[] NoticeCodes =
    {
        MusterNotices.StartingNow, MusterNotices.MovedSpots, MusterNotices.WrappingUp,
    };

    private static readonly int[] TallyStatuses =
    {
        MusterStatuses.OnMyWay, MusterStatuses.RunningLate, MusterStatuses.Here, MusterStatuses.WhereExactly,
    };

    private readonly NavBarButton[] manageButtons = new NavBarButton[1];
    private readonly PullToRefresh manageRefresh = new();
    private readonly int[] tallyCounts = new int[4];
    private readonly CachedText[] tallyTexts = new CachedText[4];
    private MusterAttendeeDto[] lastAttendees = Array.Empty<MusterAttendeeDto>();
    private string[] attendeeIdentities = Array.Empty<string>();
    private string[] attendeeNameKeys = Array.Empty<string>();
    private MusterTally tally;
    private bool noticeBusy;
    private int noticeBusyCode;
    private bool noticeFailed;
    private string invitedUserId = string.Empty;
    private float invitedTimer;
    private Action? manageSync;

    private void ResetManageState()
    {
        noticeBusy = false;
        noticeBusyCode = 0;
        noticeFailed = false;
        invitedTimer = 0f;
        invitedUserId = string.Empty;
    }

    private void OpenManage(bool animate = true)
    {
        ResetManageState();
        store.SyncNow();
        if (router.Current.Screen == MusterScreen.Manage)
        {
            return;
        }

        router.Push(MusterRoute.Manage(RootTitle()), animate);
    }

    private void DrawManage(in PhoneContext context, MusterRoute route)
    {
        if (store.Mine is not { } mine)
        {
            router.Pop(false);
            return;
        }

        var scale = UiScale.Current;
        var nowUnix = NowUnix();
        manageSync ??= SyncPlans;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("muster.manage"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            manageRefresh.Draw(navBar.Body, surface.Pull, surface.Dragging, store.Syncing, ui.MutedInk, manageSync);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawDetailHero(drawList, mine, origin.X, origin.Y, width, nowUnix, false, scale);
            cursorY = DrawAttendees(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawNotices(drawList, mine, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawListing(drawList, mine, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            cursorY = DrawWhere(drawList, mine, origin.X, cursorY + MusterArt.SectionGap * scale, width, false,
                scale);
            cursorY = DrawEndRow(drawList, origin.X, cursorY + MusterArt.SectionGap * scale, width, scale);
            MusterArt.Reserve(origin, width, cursorY + MusterArt.BottomPad * scale);
        }

        manageButtons[RefreshButton] = new NavBarButton(PhoneIcons.Refresh, Loc.T(L.Common.Refresh));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "muster.manage.nav", Loc.T(L.Muster.YourMuster),
            NavBarStyle.From(ui), manageButtons, route.BackTitle, back);
        if (pressed == RefreshButton)
        {
            SyncPlans();
        }
    }

    private void EnsureAttendees(MusterAttendeeDto[] attendees)
    {
        if (ReferenceEquals(attendees, lastAttendees))
        {
            return;
        }

        lastAttendees = attendees;
        tally = MusterTally.Of(attendees);
        tallyCounts[0] = tally.OnTheWay;
        tallyCounts[1] = tally.Late;
        tallyCounts[2] = tally.Here;
        tallyCounts[3] = tally.Asking;
        if (attendeeIdentities.Length != attendees.Length)
        {
            attendeeIdentities = new string[attendees.Length];
            attendeeNameKeys = new string[attendees.Length];
        }

        for (var index = 0; index < attendees.Length; index++)
        {
            var attendee = attendees[index];
            attendeeIdentities[index] = attendee.World.Length > 0
                ? string.Concat(attendee.CharacterName, " · ", attendee.World)
                : attendee.CharacterName;
            attendeeNameKeys[index] = string.Concat("muster.attendee.", attendee.UserId);
        }
    }

    private float DrawAttendees(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var attendees = store.MineAttendees;
        EnsureAttendees(attendees);
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.AttendeesSection), ui.TitleInk);
        UiAnchors.Report("muster.attendees", new Rect(new Vector2(left, top), new Vector2(left + width, cursorY)));
        if (attendees.Length == 0)
        {
            var max = new Vector2(left + width, cursorY + EmptyAttendeesHeight * scale);
            ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
            Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, (cursorY + max.Y) * 0.5f),
                Loc.T(L.Muster.NoAttendees), ui.MutedInk, TextStyles.Subheadline);
            return max.Y;
        }

        cursorY = DrawTally(drawList, left, cursorY, width, scale) + MusterArt.CardGap * scale;
        var rowHeight = AttendeeRowHeight * scale;
        var cardMax = new Vector2(left + width, cursorY + attendees.Length * rowHeight);
        ui.Card(drawList, new Vector2(left, cursorY), cardMax, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        for (var index = 0; index < attendees.Length; index++)
        {
            var rowTop = cursorY + index * rowHeight;
            if (index > 0)
            {
                FeedCell.Hairline(drawList, left + pad + (AttendeeAvatarRadius * 2f + MusterArt.TextGap) * scale,
                    cardMax.X, rowTop, ui.Hairline);
            }

            var row = new Rect(new Vector2(left + pad, rowTop), new Vector2(cardMax.X - pad, rowTop + rowHeight));
            if (ImGui.IsRectVisible(row.Min, row.Max))
            {
                using (UiScope.PushId(index))
                {
                    DrawAttendeeRow(drawList, attendees[index], attendeeIdentities[index], attendeeNameKeys[index],
                        row, scale);
                }
            }
        }

        return cardMax.Y;
    }

    private float DrawTally(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var visible = 0;
        for (var index = 0; index < tallyCounts.Length; index++)
        {
            if (tallyCounts[index] > 0)
            {
                visible++;
            }
        }

        var legendRows = (visible + 1) / 2;
        var height = pad * 2f + TallyBarHeight * scale + Metrics.Space.Md * scale + legendRows * TallyLegendRowHeight * scale;
        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale);
        var barMin = new Vector2(left + pad, top + pad);
        var barMax = new Vector2(max.X - pad, barMin.Y + TallyBarHeight * scale);
        var radius = TallyBarHeight * 0.5f * scale;
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), radius);
        var total = Math.Max(1, tally.Total);
        var cursorX = barMin.X;
        var barWidth = barMax.X - barMin.X;
        for (var index = 0; index < tallyCounts.Length; index++)
        {
            if (tallyCounts[index] == 0)
            {
                continue;
            }

            var segment = barWidth * tallyCounts[index] / total;
            drawList.AddRectFilled(new Vector2(cursorX, barMin.Y), new Vector2(cursorX + segment, barMax.Y),
                ImGui.GetColorU32(MusterArt.StatusColor(TallyStatuses[index])), radius);
            cursorX += segment;
        }

        var legendTop = barMax.Y + Metrics.Space.Md * scale;
        var columnWidth = (width - pad * 2f) * 0.5f;
        var slot = 0;
        for (var index = 0; index < tallyCounts.Length; index++)
        {
            if (tallyCounts[index] == 0)
            {
                continue;
            }

            var column = slot % 2;
            var rowIndex = slot / 2;
            slot++;
            var centerY = legendTop + (rowIndex + 0.5f) * TallyLegendRowHeight * scale;
            var dotCenter = new Vector2(left + pad + column * columnWidth + TallyDotRadius * scale, centerY);
            var color = MusterArt.StatusColor(TallyStatuses[index]);
            drawList.AddCircleFilled(dotCenter, TallyDotRadius * scale, ImGui.GetColorU32(color), 16);
            var text = TallyText(index);
            var textLeft = dotCenter.X + TallyDotRadius * scale + Metrics.Space.Sm * scale;
            var fitted = Typography.FitText(text, MathF.Max(1f, columnWidth - (textLeft - dotCenter.X) - 4f * scale),
                TextStyles.Subheadline);
            var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - lineHeight * 0.5f), fitted, ui.BodyInk,
                TextStyles.Subheadline);
        }

        return max.Y;
    }

    private string TallyText(int index)
    {
        ref var cache = ref tallyTexts[index];
        var count = tallyCounts[index];
        if (cache.IsCurrent(count))
        {
            return cache.Value;
        }

        var entry = index switch
        {
            1 => L.Muster.TallyLate,
            2 => L.Muster.TallyHere,
            3 => L.Muster.TallyAsking,
            _ => L.Muster.TallyOnTheWay,
        };
        return cache.Store(count, Loc.T(entry, count));
    }

    private void DrawAttendeeRow(ImDrawListPtr drawList, MusterAttendeeDto attendee, string identity, string nameKey,
        Rect row, float scale)
    {
        var centerY = row.Center.Y;
        var avatarRadius = AttendeeAvatarRadius * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, centerY);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, attendee.CharacterName, attendee.World,
            null, images, lodestone, 0.9f, 32, 1f, Frames.Of(attendee.FrameId));
        var cursorRight = DrawInviteControl(drawList, attendee, row.Max.X, centerY, scale) - Metrics.Space.Sm * scale;
        var chipLabel = AttendeeStatusLabel(attendee.Status);
        var chipColor = MusterArt.StatusColor(attendee.Status);
        var chipSize = Typography.Measure(chipLabel, TextStyles.FootnoteEmphasized);
        var chipHeight = 24f * scale;
        var chipMin = new Vector2(cursorRight - chipSize.X - 18f * scale, centerY - chipHeight * 0.5f);
        var chipMax = new Vector2(cursorRight, centerY + chipHeight * 0.5f);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(chipColor with { W = 0.18f }));
        Typography.Draw(drawList, new Vector2(chipMin.X + 9f * scale, centerY - chipSize.Y * 0.5f), chipLabel,
            chipColor, TextStyles.FootnoteEmphasized);
        var nameLeft = avatarCenter.X + avatarRadius + MusterArt.TextGap * scale;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        UserName.DrawAuto(drawList, nameKey, identity, attendee.Badges, attendee.BadgeIds,
            nameLeft, centerY - nameHeight * 0.5f, MathF.Max(1f, chipMin.X - Metrics.Space.Sm * scale - nameLeft),
            TextStyles.BodyEmphasized, ui.TitleInk, theme);
    }

    private float DrawInviteControl(ImDrawListPtr drawList, MusterAttendeeDto attendee, float right, float centerY,
        float scale)
    {
        var justInvited = invitedTimer > 0f && string.Equals(invitedUserId, attendee.UserId, StringComparison.Ordinal);
        if (justInvited)
        {
            var label = Loc.T(L.Muster.Invited);
            var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), label, ui.Accent,
                TextStyles.FootnoteEmphasized);
            return right - size.X;
        }

        var radius = InviteRadius * scale;
        var center = new Vector2(right - radius, centerY);
        if (!PartyInvite.CanInvite(attendee.World))
        {
            AppSkin.Icon(drawList, center, IconGlyph.Of(FontAwesomeIcon.UserPlus),
                Palette.WithAlpha(ui.MutedInk, 0.55f), 0.6f);
            var hit = new Vector2(radius, radius);
            HoverTooltip.Show(new Rect(center - hit, center + hit), Loc.T(L.Muster.DifferentDataCenter),
                HoverLabelSide.Above);
            return center.X - radius;
        }

        var inviteLabel = Loc.T(L.Muster.InviteToParty);
        if (RoundButton.FontIcon(drawList, inviteLabel, center, radius, FontAwesomeIcon.UserPlus,
                radius * InviteGlyphFraction, ui.Ink, ButtonStyle.Tinted, inviteLabel) &&
            PartyInvite.Invite(attendee.CharacterName, attendee.World))
        {
            UiFeedback.Play(UiSound.Success);
            invitedUserId = attendee.UserId;
            invitedTimer = InvitedSeconds;
        }

        return center.X - radius;
    }

    private static string AttendeeStatusLabel(int status) =>
        status switch
        {
            MusterStatuses.RunningLate => Loc.T(L.Muster.StatusRunningLate),
            MusterStatuses.Here => Loc.T(L.Muster.AttendeeHere),
            MusterStatuses.WhereExactly => Loc.T(L.Muster.AttendeeAsking),
            _ => Loc.T(L.Muster.AttendeeOnTheWay),
        };

    private float DrawNotices(ImDrawListPtr drawList, MusterDto mine, float left, float top, float width, float scale)
    {
        var cursorY = top + CardSectionHeader.Draw(drawList, new Vector2(left, top), width, Loc.T(L.Muster.NoticesSection), ui.TitleInk);
        var hintWidth = width - Metrics.Space.Lg * 2f * scale;
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(left + Metrics.Space.Lg * scale, cursorY),
            Loc.T(L.Muster.NoticeHint), ui.MutedInk, TextStyles.Footnote, hintWidth);
        var tilesTop = cursorY + hintHeight + Metrics.Space.Md * scale;
        var gap = ActionTileGap * scale;
        var tileWidth = (width - gap * (NoticeCodes.Length - 1)) / NoticeCodes.Length;
        var tileHeight = NoticeTileHeight * scale;
        UiAnchors.Report("muster.notices", new Rect(new Vector2(left, tilesTop),
            new Vector2(left + width, tilesTop + tileHeight)));
        for (var index = 0; index < NoticeCodes.Length; index++)
        {
            var code = NoticeCodes[index];
            var min = new Vector2(left + index * (tileWidth + gap), tilesTop);
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var tapped = MusterArt.ActionTile(drawList, ui, KeyFor("notice", code, mine.Id), rect, NoticeIcon(code),
                NoticeLabel(code), ui.Accent, mine.HostNotice == code, !noticeBusy, scale);
            if (noticeBusy && noticeBusyCode == code)
            {
                LoadingPulse.Spinner(new Vector2(rect.Max.X - 14f * scale, rect.Min.Y + 14f * scale), 6f * scale,
                    ui.Accent);
            }

            if (tapped)
            {
                SendNotice(code);
            }
        }

        var bottom = tilesTop + tileHeight;
        return noticeFailed ? DrawFeedback(left, bottom, width, Loc.T(L.Muster.NoticeFailed), scale) : bottom;
    }

    private void SendNotice(int notice)
    {
        noticeBusy = true;
        noticeBusyCode = notice;
        noticeFailed = false;
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

        store.SetNotice(request, ok =>
        {
            noticeBusy = false;
            noticeBusyCode = 0;
            noticeFailed = !ok;
            UiFeedback.Play(ok ? UiSound.MessageSent : UiSound.Caution);
        });
    }

    private float DrawEndRow(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var row = new Rect(new Vector2(left, top), new Vector2(left + width, top + MusterArt.FieldRowHeight * scale));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        var hovered = MusterArt.RowWash(drawList, ui, row, scale);
        Typography.DrawCentered(drawList, row.Center, Loc.T(L.Muster.EndMuster), ui.Theme.Danger, TextStyles.Body);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            AskEndMuster();
        }

        return row.Max.Y;
    }

    private void AskEndMuster()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Muster.EndMuster),
            Message = Loc.T(L.Muster.EndConfirm),
            ConfirmLabel = Loc.T(L.Muster.EndMuster),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            BusyLabel = Loc.T(L.Muster.Ending),
            FailedMessage = Loc.T(L.Muster.EndFailed),
            Danger = true,
            ConfirmAsync = done => store.EndMine(done),
        });
    }
}
