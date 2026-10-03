using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float MemberRowHeight = 56f;
    private const float RequestRowHeight = 56f;
    private const float LinkRowHeight = 50f;
    private const float JoinDebounceSeconds = 0.20f;
    private const int CodeFieldLength = 8;

    private const int MemberActionMakeHost = 0;
    private const int MemberActionToggleAdd = 1;
    private const int MemberActionToggleControl = 2;
    private const int MemberActionRemove = 3;

    private string codeInput = string.Empty;
    private string joinQuery = string.Empty;
    private string joinApplied = string.Empty;
    private float joinDebounce;
    private FriendRow[] joinRows = [];
    private bool joinSearching;
    private volatile int joinSearchStamp;
    private bool joinSearchFailed;
    private TextCache watchingCountText;
    private TextCache partyHostText;
    private TextCache partyGraceText;
    private TextCache inviteCodeLabel;
    private string codeDisplaySource = string.Empty;
    private string codeDisplay = string.Empty;

    private readonly record struct FriendRow(UserDto User, string Handle);

    private bool InPartyRoom =>
        watchAlong.IsViewing || watchAlong.IsPartyOpen || (watchAlong.IsHosting && watchAlong.HasCompany);

    private void DrawPartyTab(Rect body, float scale)
    {
        using (AppSurface.BeginEdgeToEdge(body))
        {
            Gap(Metrics.Space.Xs);
            if (watchAlong.IsJoining)
            {
                DrawPartyAwaiting(body, scale);
            }
            else if (InPartyRoom)
            {
                DrawPartyRoom(scale);
            }
            else
            {
                DrawPartyLobby(scale);
            }

            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawPartyLobby(float scale)
    {
        if (nearbyCadence.Advance(ImGui.GetIO().DeltaTime, NearbyRefreshSeconds))
        {
            nearbyCadence.Reset();
            watchAlong.RequestNearbyStreams();
        }

        var signedIn = session.IsSignedIn;
        var title = Loc.T(L.AetherStream.PartyLobbyTitle);
        var hint = Loc.T(signedIn ? L.AetherStream.PartyLobbyHint : L.AetherStream.PartySignInHint);
        var pad = Metrics.Space.Xl * scale;
        var innerWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, innerWidth).Y;
        var tile = PromptTile * scale;
        var buttonHeight = ButtonHeight * scale;
        var card = BeginBlock(pad + tile + Metrics.Space.Lg * scale + titleHeight + Metrics.Space.Xs * scale
            + hintHeight + Metrics.Space.Xl * scale + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, true);
        var top = card.Min.Y + pad;
        var tileMin = new Vector2(card.Center.X - tile * 0.5f, top);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f),
            FontAwesomeIcon.UserFriends, AccentRing.Ink, tile * 0.46f);
        top += tile + Metrics.Space.Lg * scale;
        Typography.DrawCentered(drawList, new Vector2(card.Center.X, top + titleHeight * 0.5f),
            Typography.FitText(title, innerWidth, TextStyles.Title2), Ink.TitleInk, TextStyles.Title2);
        top += titleHeight + Metrics.Space.Xs * scale;
        Typography.DrawWrappedCentered(drawList, new Vector2(card.Center.X, top + hintHeight * 0.5f), hint,
            Ink.MutedInk, TextStyles.Subheadline, innerWidth);
        top += hintHeight + Metrics.Space.Xl * scale;
        var startButton = new Rect(new Vector2(card.Min.X + pad, top),
            new Vector2(card.Max.X - pad, top + buttonHeight));
        UiAnchors.Report("aetherstream.party.start", startButton);
        if (PrimaryButton(startButton, Loc.T(L.AetherStream.StartParty), signedIn))
        {
            watchAlong.OpenParty();
        }

        EndBlock();
        if (!signedIn)
        {
            return;
        }

        Gap(Metrics.Space.Lg);
        SectionLabel(Loc.T(L.AetherStream.JoinWithCode));
        DrawCodeJoin(scale);

        var nearby = watchAlong.Nearby;
        if (nearby.Count > 0)
        {
            Gap(Metrics.Space.Md);
            SectionLabel(Loc.T(L.AetherStream.JoinNearbyHeader));
            for (var index = 0; index < nearby.Count; index++)
            {
                DrawNearbyRow(nearby[index], scale);
            }
        }

        Gap(Metrics.Space.Md);
        if (DrawLinkRow(FontAwesomeIcon.Search, Loc.T(L.AetherStream.FindFriendParty), 0, scale))
        {
            joinQuery = string.Empty;
            router.Push(new StreamRoute(StreamScreen.FindFriend));
        }

        if (DrawLinkRow(FontAwesomeIcon.SlidersH, Loc.T(L.AetherStream.PartySettings), 0, scale))
        {
            router.Push(new StreamRoute(StreamScreen.PartySettings));
        }
    }

    private void DrawCodeJoin(float scale)
    {
        var row = BeginBlock(FieldRowHeight * scale);
        var joinLabel = Loc.T(L.AetherStream.JoinAction);
        var joinWidth = Typography.Measure(joinLabel, SmallButtonStyle).X + 36f * scale;
        var field = new Rect(row.Min, new Vector2(row.Max.X - joinWidth - Metrics.Space.Sm * scale, row.Max.Y));
        var submitted = SubmitField.Draw(field, "##aetherstreamCode", Loc.T(L.AetherStream.CodeHint), ref codeInput,
            accentedTheme, CodeFieldLength, FontAwesomeIcon.Key);
        var button = new Rect(new Vector2(row.Max.X - joinWidth, row.Center.Y - SmallButtonHeight * scale * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + SmallButtonHeight * scale * 0.5f));
        if ((SmallButton(button, joinLabel, true) || submitted) && codeInput.Trim().Length > 0)
        {
            if (watchAlong.JoinByCode(codeInput))
            {
                codeInput = string.Empty;
            }
            else
            {
                ShellToast.Show(Loc.T(L.AetherStream.CodeShape));
            }
        }

        EndBlock();
    }

    private bool DrawLinkRow(FontAwesomeIcon icon, string label, int badge, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, LinkRowHeight * scale, ui.HoverWash);
        var row = cell.Bounds;
        var iconCenter = new Vector2(row.Min.X + PadX * scale + 11f * scale, row.Center.Y);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(icon), Ink.AccentLink, 0.95f);
        var chevronCenter = new Vector2(row.Max.X - PadX * scale - 6f * scale, row.Center.Y);
        PhoneIcon.Draw(drawList, chevronCenter, PhoneIcons.ChevronRight, Ink.FaintInk, 16f * scale);
        if (badge > 0)
        {
            SocialChrome.DrawCountBadge(drawList, new Vector2(chevronCenter.X - 22f * scale, row.Center.Y), badge,
                Ink);
        }

        var textLeft = iconCenter.X + 11f * scale + Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, chevronCenter.X - 40f * scale - textLeft, TextStyles.Body), Ink.TitleInk,
            TextStyles.Body);
        FeedCell.End(drawList, cell, Ink.Hairline);
        return cell.Tapped;
    }

    private void DrawPartyAwaiting(Rect body, float scale)
    {
        var block = BeginBlock(MathF.Max(180f * scale, body.Height * 0.5f));
        LoadingPulse.Draw(new Vector2(block.Center.X, block.Center.Y - 16f * scale), 16f * scale, ui.Accent,
            Ink.MutedInk, Loc.T(L.AetherStream.JoinWaitingApproval));
        EndBlock();
        var button = BeginBlock(ButtonHeight * scale);
        if (DangerButton(button, Loc.T(L.AetherStream.CancelRequest)))
        {
            watchAlong.Leave();
        }

        EndBlock();
    }

    private void DrawPartyRoom(float scale)
    {
        var hosting = !watchAlong.IsViewing;
        DrawPartySummary(hosting, scale);

        if (hosting && watchAlong.PendingRequests.Count > 0)
        {
            Gap(Metrics.Space.Md);
            SectionLabel(Loc.T(L.AetherStream.CastingPendingRequestsHeader));
            var requests = watchAlong.PendingRequests;
            for (var index = 0; index < requests.Count; index++)
            {
                DrawRequestRow(requests[index], scale);
            }
        }

        var roster = watchAlong.Roster;
        if (roster.Count > 0)
        {
            Gap(Metrics.Space.Md);
            SectionLabel(Loc.T(L.AetherStream.PartyMembers));
            for (var index = 0; index < roster.Count; index++)
            {
                DrawMemberRow(roster[index], hosting, scale);
            }
        }

        if (hosting)
        {
            Gap(Metrics.Space.Md);
            if (DrawLinkRow(FontAwesomeIcon.SlidersH, Loc.T(L.AetherStream.PartySettings), 0, scale))
            {
                router.Push(new StreamRoute(StreamScreen.PartySettings));
            }
        }

        Gap(Metrics.Space.Xl);
        var button = BeginBlock(ButtonHeight * scale);
        if (DangerButton(button, Loc.T(hosting ? L.AetherStream.EndParty : L.AetherStream.LeaveParty)))
        {
            if (hosting && watchAlong.HasCompany)
            {
                confirm.Ask(new ConfirmRequest
                {
                    Message = Loc.T(L.AetherStream.EndPartyConfirm),
                    ConfirmLabel = Loc.T(L.AetherStream.EndParty),
                    CancelLabel = Loc.T(L.Common.Cancel),
                    Sheet = true,
                    Confirm = watchAlong.Leave,
                });
            }
            else
            {
                watchAlong.Leave();
            }
        }

        EndBlock();
    }

    private void DrawPartySummary(bool hosting, float scale)
    {
        var code = watchAlong.RoomCode;
        var showCode = watchAlong.ServerSupportsParty && (hosting || code.Length > 0);
        var grace = hosting && !watchAlong.IsPartyOpen ? watchAlong.IdleGraceSeconds : 0f;
        var pad = Metrics.Space.Lg * scale;
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var codeHeight = showCode ? 58f * scale + Metrics.Space.Md * scale : 0f;
        var graceHeight = grace > 0f ? Typography.LineHeight(TextStyles.Footnote) + Metrics.Space.Sm * scale : 0f;
        var card = BeginBlock(pad + titleHeight + subtitleHeight + codeHeight + graceHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale, true);

        var left = card.Min.X + pad;
        var width = card.Width - pad * 2f;
        var top = card.Min.Y + pad;
        var title = hosting
            ? Loc.T(L.AetherStream.PartyYours)
            : partyHostText.Format(Loc.T(L.AetherStream.ViewingStream), watchAlong.HostName() ?? string.Empty);
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, width, TextStyles.Title3),
            Ink.TitleInk, TextStyles.Title3);
        top += titleHeight;
        var watching = watchingCountText.Format(Loc.T(L.AetherStream.WatchingCount),
            Math.Max(1, watchAlong.Roster.Count));
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(watching, width, TextStyles.Subheadline),
            Ink.MutedInk, TextStyles.Subheadline);
        top += subtitleHeight;

        if (showCode)
        {
            top += Metrics.Space.Md * scale;
            DrawCodeWell(drawList, new Rect(new Vector2(left, top), new Vector2(left + width, top + 58f * scale)),
                code, hosting, scale);
            top += 58f * scale;
        }

        if (grace > 0f)
        {
            Typography.Draw(drawList, new Vector2(left, top + Metrics.Space.Sm * scale),
                Typography.FitText(
                    partyGraceText.Clock(Loc.T(L.AetherStream.StandbyHostGrace), (int)MathF.Ceiling(grace)), width,
                    TextStyles.Footnote), Ink.AccentLink, TextStyles.Footnote);
        }

        EndBlock();
    }

    private void DrawCodeWell(ImDrawListPtr drawList, Rect well, string code, bool hosting, float scale)
    {
        Squircle.Fill(drawList, well.Min, well.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(Ink.FieldFill));
        var pad = Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        if (code.Length == 0)
        {
            var hint = Loc.T(L.AetherStream.CodeOffHint);
            var buttonLabel = Loc.T(L.AetherStream.CodeTurnOn);
            var buttonWidth = Typography.Measure(buttonLabel, SmallButtonStyle).X + 30f * scale;
            var button = new Rect(
                new Vector2(well.Max.X - pad - buttonWidth, well.Center.Y - SmallButtonHeight * scale * 0.5f),
                new Vector2(well.Max.X - pad, well.Center.Y + SmallButtonHeight * scale * 0.5f));
            var hintHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(well.Min.X + pad, well.Center.Y - hintHeight * 0.5f),
                Typography.FitText(hint, button.Min.X - pad * 2f - well.Min.X, TextStyles.Subheadline), Ink.MutedInk,
                TextStyles.Subheadline);
            if (hosting && SmallButton(button, buttonLabel, true))
            {
                watchAlong.SetPolicy(watchAlong.Policy with { CodeEnabled = true });
            }

            return;
        }

        if (!ReferenceEquals(code, codeDisplaySource))
        {
            codeDisplaySource = code;
            codeDisplay = PartyCode.Display(code);
        }

        var codeHeight = Typography.LineHeight(TextStyles.Title2);
        var top = well.Center.Y - (labelHeight + codeHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(well.Min.X + pad, top),
            inviteCodeLabel.Upper(Loc.T(L.AetherStream.InviteCode)), Ink.FaintInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(well.Min.X + pad, top + labelHeight), codeDisplay, Ink.TitleInk,
            TextStyles.Title2);

        var copyRadius = 16f * scale;
        var copyCenter = new Vector2(well.Max.X - pad - copyRadius, well.Center.Y);
        if (HoverButton.Circle(drawList, "aetherstream.code.copy", copyCenter, copyRadius, FontAwesomeIcon.Copy,
                Ink.ButtonFill, Ink.TitleInk, ImGui.GetIO().DeltaTime, 1f, true, Loc.T(L.AetherStream.CopyCode)))
        {
            ImGui.SetClipboardText(code);
            ShellToast.Show();
        }
    }

    private void DrawRequestRow(PendingJoinRequest request, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, RequestRowHeight * scale, ui.HoverWash, false);
        var row = cell.Bounds;
        var avatarRadius = 17f * scale;
        var avatarCenter = new Vector2(row.Min.X + PadX * scale + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, request.DisplayName, string.Empty,
            request.AvatarUrl, remoteImages, lodestone, 0.7f, 24);

        var circleRadius = 15f * scale;
        var denyCenter = new Vector2(row.Max.X - PadX * scale - circleRadius, row.Center.Y);
        var approveCenter = new Vector2(denyCenter.X - circleRadius * 2f - Metrics.Space.Sm * scale, row.Center.Y);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - nameHeight * 0.5f),
            Typography.FitText(request.DisplayName,
                approveCenter.X - circleRadius - Metrics.Space.Md * scale - textLeft, TextStyles.BodyEmphasized),
            Ink.TitleInk, TextStyles.BodyEmphasized);

        if (ui.IconButton(approveCenter, circleRadius, IconGlyph.Of(FontAwesomeIcon.Check), Ink.PresenceGreen,
                Palette.WithAlpha(Ink.PresenceGreen, 0.18f), 0.62f, Loc.T(L.AetherStream.CastingApprove)))
        {
            watchAlong.ApproveRequest(request.UserId);
        }

        if (ui.IconButton(denyCenter, circleRadius, IconGlyph.Of(FontAwesomeIcon.Times), Ink.Danger,
                Palette.WithAlpha(Ink.Danger, 0.16f), 0.62f, Loc.T(L.AetherStream.CastingDeny)))
        {
            watchAlong.DenyRequest(request.UserId);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
    }

    private void DrawMemberRow(WatchAlongParticipant member, bool hosting, float scale)
    {
        var manageable = hosting && !member.IsHost;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, MemberRowHeight * scale, ui.HoverWash, manageable);
        var row = cell.Bounds;
        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(row.Min.X + PadX * scale + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, member.DisplayName, string.Empty,
            member.AvatarUrl, remoteImages, lodestone, 0.7f, 24);

        var trailing = row.Max.X - PadX * scale;
        if (manageable)
        {
            PhoneIcon.Draw(drawList, new Vector2(trailing - 9f * scale, row.Center.Y), PhoneIcons.Dots, Ink.FaintInk,
                18f * scale);
            trailing -= 26f * scale;
        }

        var role = MemberRole(member, hosting);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = trailing - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var roleHeight = role.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = row.Center.Y - (nameHeight + roleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(member.DisplayName, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        if (role.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
                Typography.FitText(role, textWidth, TextStyles.Footnote),
                member.IsHost ? Ink.AccentLink : Ink.MutedInk, TextStyles.Footnote);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
        if (cell.Tapped)
        {
            OpenMemberActions(member);
        }
    }

    private string MemberRole(WatchAlongParticipant member, bool hosting)
    {
        if (member.IsHost)
        {
            return Loc.T(L.AetherStream.WatchingHostLabel);
        }

        var held = watchAlong.RoomGuestPermissions
            | (hosting ? watchAlong.GrantsFor(member.UserId) : member.Flags);
        if (PartyPermissions.Allows(held, StreamPermission.ControlPlayback))
        {
            return Loc.T(L.AetherStream.AllowControl);
        }

        return PartyPermissions.Allows(held, StreamPermission.AddToQueue)
            ? Loc.T(L.AetherStream.AllowAdd)
            : string.Empty;
    }

    private void OpenMemberActions(WatchAlongParticipant member)
    {
        actionMember = member;
        BeginActions(SheetPurpose.Member, member.DisplayName);
        if (watchAlong.CanTransferTo(member))
        {
            AddAction(MemberActionMakeHost, Loc.T(L.AetherStream.MakeHost), PhoneIcons.Crown);
        }

        var grants = watchAlong.GrantsFor(member.UserId);
        var policy = watchAlong.Policy;
        if (!policy.GuestsCanAdd)
        {
            AddAction(MemberActionToggleAdd, Loc.T(L.AetherStream.AllowAdd),
                selected: PartyPermissions.Allows(grants, StreamPermission.AddToQueue), checkable: true);
        }

        if (watchAlong.ServerSupportsParty && !policy.GuestsCanControl)
        {
            AddAction(MemberActionToggleControl, Loc.T(L.AetherStream.AllowControl),
                selected: PartyPermissions.Allows(grants, StreamPermission.ControlPlayback), checkable: true);
        }

        AddAction(MemberActionRemove, Loc.T(L.AetherStream.WatchingKick), PhoneIcons.X, true);
        actions.Open();
    }

    private void HandleMemberAction(int code)
    {
        if (actionMember is not { } member)
        {
            return;
        }

        actionMember = null;
        if (!watchAlong.IsHosting || !InRoster(member.UserId))
        {
            return;
        }

        var grants = watchAlong.GrantsFor(member.UserId);
        switch (code)
        {
            case MemberActionMakeHost:
                confirm.Ask(new ConfirmRequest
                {
                    Message = string.Format(Loc.Culture, Loc.T(L.AetherStream.MakeHostConfirm), member.DisplayName),
                    ConfirmLabel = Loc.T(L.AetherStream.MakeHost),
                    CancelLabel = Loc.T(L.Common.Cancel),
                    Danger = false,
                    Sheet = true,
                    Confirm = () => watchAlong.TransferHost(member.UserId),
                });
                return;
            case MemberActionToggleAdd:
                watchAlong.SetGrant(member.UserId, StreamPermission.AddToQueue,
                    !PartyPermissions.Allows(grants, StreamPermission.AddToQueue));
                return;
            case MemberActionToggleControl:
                watchAlong.SetGrant(member.UserId, StreamPermission.ControlPlayback,
                    !PartyPermissions.Allows(grants, StreamPermission.ControlPlayback));
                return;
            case MemberActionRemove:
                watchAlong.KickParticipant(member.UserId);
                return;
        }
    }

    private bool InRoster(string userId)
    {
        var roster = watchAlong.Roster;
        for (var index = 0; index < roster.Count; index++)
        {
            if (string.Equals(roster[index].UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawPartySettings(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.PartySettings), Ink, back, ScreenTitleStyle);
        var content = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(content))
        {
            var policy = watchAlong.Policy;
            var partyRows = !watchAlong.ServerLacksParty;
            SettingsSection.Header(Loc.T(L.AetherStream.PartySettingsGuests), accentedTheme);
            var guests = GroupCard.Begin(accentedTheme, partyRows ? 2 : 1);
            var canAdd = SettingsRow.Bool(guests.NextRow(), Loc.T(L.AetherStream.GuestsCanAdd),
                policy.GuestsCanAdd, accentedTheme, hint: Loc.T(L.AetherStream.GuestsCanAddHint));
            var canControl = policy.GuestsCanControl;
            if (partyRows)
            {
                canControl = SettingsRow.Bool(guests.NextRow(), Loc.T(L.AetherStream.GuestsCanControl), canControl,
                    accentedTheme, hint: Loc.T(L.AetherStream.GuestsCanControlHint));
            }

            guests.End();
            Gap(Metrics.Space.Md);
            SettingsSection.Header(Loc.T(L.AetherStream.PartySettingsJoining), accentedTheme);
            var joining = GroupCard.Begin(accentedTheme, partyRows ? 3 : 2);
            var codeEnabled = policy.CodeEnabled;
            if (partyRows)
            {
                codeEnabled = SettingsRow.Bool(joining.NextRow(), Loc.T(L.AetherStream.InviteCode), codeEnabled,
                    accentedTheme, hint: Loc.T(L.AetherStream.InviteCodeHint));
            }

            var approval = SettingsRow.Bool(joining.NextRow(), Loc.T(L.AetherStream.SettingsApprovalRequired),
                policy.ApprovalRequired, accentedTheme, hint: Loc.T(L.AetherStream.SettingsApprovalRequiredHint));
            var discoverable = SettingsRow.Bool(joining.NextRow(), Loc.T(L.AetherStream.SettingsDiscoverable),
                policy.Discoverable, accentedTheme, hint: Loc.T(L.AetherStream.SettingsDiscoverableHint));
            joining.End();
            var updated = new PartyPolicy(approval, discoverable, codeEnabled,
                (canAdd ? StreamPermission.AddToQueue : 0) | (canControl ? StreamPermission.ControlPlayback : 0));
            if (updated != policy)
            {
                watchAlong.SetPolicy(updated);
            }

            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawFindFriend(Rect area, float scale)
    {
        SocialChrome.DrawScreenHeader(area, Loc.T(L.AetherStream.FindFriendParty), Ink, back, ScreenTitleStyle);
        var top = area.Min.Y + AppHeader.Height * scale + Metrics.Space.Xs * scale;
        var fieldRect = new Rect(new Vector2(area.Min.X + PadX * scale, top),
            new Vector2(area.Max.X - PadX * scale, top + 36f * scale));
        SearchField.Draw(fieldRect, "##aetherstreamJoinSearch", Loc.T(L.AetherStream.JoinSearchHint), ref joinQuery,
            ui.Palette);
        TickJoinSearch();

        var listRect = new Rect(new Vector2(area.Min.X, fieldRect.Max.Y + Metrics.Space.Md * scale), area.Max);
        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var rows = joinRows;
            if (rows.Length > 0)
            {
                for (var index = 0; index < rows.Length; index++)
                {
                    DrawFriendRow(rows[index].User, rows[index].Handle, scale);
                }
            }
            else
            {
                DrawFindFriendEmpty(listRect, scale);
            }

            Gap(Metrics.Space.Lg);
        }
    }

    private void DrawFindFriendEmpty(Rect listRect, float scale)
    {
        var block = BeginBlock(MathF.Max(200f * scale, listRect.Height * 0.6f));
        if (joinSearching)
        {
            LoadingPulse.Draw(block.Center, 16f * scale, ui.Accent, Ink.MutedInk, LoadingPulse.SafeLabel());
        }
        else if (joinSearchFailed)
        {
            EmptyState.Draw(block, ui, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.AetherStream.JoinSearchFailedTitle), Loc.T(L.AetherStream.JoinSearchFailed));
        }
        else if (joinQuery.Trim().Length == 0)
        {
            EmptyState.Draw(block, ui, FontAwesomeIcon.Search, Loc.T(L.AetherStream.FindFriendTitle),
                Loc.T(L.AetherStream.FindFriendHint));
        }
        else
        {
            EmptyState.Draw(block, ui, FontAwesomeIcon.Search, Loc.T(L.PhotoTag.NoPeople), string.Empty);
        }

        EndBlock();
    }

    private void DrawFriendRow(UserDto user, string handle, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, MemberRowHeight * scale, ui.HoverWash);
        var row = cell.Bounds;
        var title = user.DisplayName.Length > 0 ? user.DisplayName : handle;
        var avatarRadius = 18f * scale;
        var avatarCenter = new Vector2(row.Min.X + PadX * scale + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, title, string.Empty, user.AvatarUrl,
            remoteImages, lodestone, 0.8f, 28);
        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textWidth = row.Max.X - PadX * scale - textLeft;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var handleHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (nameHeight + handleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(handle, textWidth, TextStyles.Footnote), Ink.MutedInk, TextStyles.Footnote);
        FeedCell.End(drawList, cell, Ink.Hairline);
        if (cell.Tapped)
        {
            watchAlong.Join(user.Id);
            activeTab = StreamTab.Party;
            router.Pop();
        }
    }

    private void TickJoinSearch()
    {
        var trimmed = joinQuery.Trim();
        if (trimmed.Length == 0)
        {
            joinApplied = string.Empty;
            joinSearchStamp++;
            joinRows = [];
            joinSearching = false;
            joinSearchFailed = false;
            return;
        }

        if (string.Equals(trimmed, joinApplied, StringComparison.Ordinal))
        {
            return;
        }

        joinDebounce += ImGui.GetIO().DeltaTime;
        if (joinDebounce < JoinDebounceSeconds)
        {
            return;
        }

        joinDebounce = 0f;
        joinApplied = trimmed;
        joinSearching = true;
        joinSearchFailed = false;
        var stamp = ++joinSearchStamp;
        joinWork.Run("join search", async token =>
        {
            var result = await joinAccount.SearchAsync(trimmed, token).ConfigureAwait(false);
            var users = result?.Users ?? Array.Empty<UserDto>();
            var rows = new FriendRow[users.Length];
            for (var index = 0; index < users.Length; index++)
            {
                rows[index] = new FriendRow(users[index], "@" + users[index].Handle);
            }

            if (stamp != joinSearchStamp)
            {
                return;
            }

            joinRows = rows;
            joinSearchFailed = result is null;
        }, () =>
        {
            if (stamp == joinSearchStamp)
            {
                joinSearching = false;
            }
        });
    }
}
