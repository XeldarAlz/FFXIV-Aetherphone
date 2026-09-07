using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float CallRowHeight = 64f;
    private const float CallSearchHeight = 52f;
    private const float ReturnBannerHeight = 44f;
    private const float CallRowAvatarRadius = 23f;
    private const float CallDirectionGlyph = 15f;
    private const float CallInfoRadius = 16f;
    private const float CallInfoGlyph = 22f;
    private const float CallFavoriteGlyph = 20f;
    private const float CallFavoriteRadius = 17f;
    private const int MicSilentWarningSeconds = 30;

    private readonly PullToRefresh contactsRefresh = new();
    private readonly List<ContactDto> favoriteContacts = new();
    private readonly List<ContactDto> callableContacts = new();

    private float DrawReturnToCallBanner(Rect rect)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var min = new Vector2(rect.Min.X + 14f * scale, rect.Min.Y + 3f * scale);
        var max = new Vector2(rect.Max.X - 14f * scale, rect.Max.Y - 5f * scale);
        var hovered = UiInteract.Hover(min, max);
        var fill = hovered ? Palette.Mix(CallGreen, White, 0.12f) : CallGreen;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(fill));
        var centerY = (min.Y + max.Y) * 0.5f;
        var pulse = 0.45f + 0.55f * MathF.Abs(MathF.Sin((float)ImGui.GetTime() * 2.4f));
        drawList.AddCircleFilled(new Vector2(min.X + 16f * scale, centerY), 4f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, pulse)), 16);
        var status = StatusLine(currentCall);
        var statusSize = Typography.Measure(status, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(max.X - 16f * scale - statusSize.X, centerY - statusSize.Y * 0.5f),
            status, White, TextStyles.Footnote);
        var label = Loc.T(L.Phone.ReturnToCall);
        var labelWidth = max.X - min.X - 48f * scale - statusSize.X;
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(min.X + 28f * scale, centerY - labelSize.Y * 0.5f),
            Typography.FitText(label, labelWidth, TextStyles.SubheadlineEmphasized), White,
            TextStyles.SubheadlineEmphasized);
        if (UiInteract.HoverClick(min, max))
        {
            router.Push(MessageRoute.Call);
        }

        return rect.Max.Y;
    }

    private void DrawCallRoute(Rect area)
    {
        var view = currentCall;
        if (view.State is not CallState.Dialing and not CallState.Connecting and not CallState.Active)
        {
            return;
        }

        var scale = UiScale.Current;
        DrawScreenHeader(area, string.Empty);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawCallScreen(new PhoneContext(body, theme, navigation), view);
    }

    private void ShowCallScreen()
    {
        var state = calls.Snapshot().State;
        if (state is not CallState.Dialing and not CallState.Connecting and not CallState.Active)
        {
            return;
        }

        if (router.Current.Screen != MessageScreen.Call)
        {
            router.Push(MessageRoute.Call);
        }
    }

    private void DrawCallsTab(Rect area)
    {
        var scale = UiScale.Current;
        if (!session.IsSignedIn)
        {
            EmptyState.Draw(area, ui, PhoneIcons.Phone, Loc.T(L.Phone.SignInTitle), Loc.T(L.Phone.SignInPrompt));
            return;
        }

        if (!calls.Enabled)
        {
            DrawEnablePrompt(area, scale);
            return;
        }

        calls.MarkLogSeen();
        socialNotifications.MarkSeen(SocialActivity.MessageApp);
        var log = calls.CallLog;
        CollectFavorites(favoriteContacts);
        using (AppSurface.BeginEdgeToEdge(area))
        {
            var drawList = ImGui.GetWindowDrawList();
            DrawSectionLabel(Loc.T(L.Message.Favorites));
            for (var index = 0; index < favoriteContacts.Count; index++)
            {
                DrawFavoriteCallRow(drawList, favoriteContacts[index]);
            }

            if (DrawActionRow(drawList, PhoneIcons.Star, Loc.T(L.Message.AddFavorite)))
            {
                SelectTab(MessageTab.Contacts);
            }

            DrawSectionLabel(Loc.T(L.Message.Recent));
            if (log.Length == 0)
            {
                DrawInlineEmpty(drawList, Loc.T(L.Phone.NoRecentCallsHint));
            }

            for (var index = 0; index < log.Length; index++)
            {
                DrawLogRow(drawList, log[index]);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        DrawConnectingHint(area, scale);
    }

    private void DrawInlineEmpty(ImDrawListPtr drawList, string text)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.DrawWrappedCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 12f * scale), text,
            ink.MutedInk, RowSubStyle, width - CellPadX * 4f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 28f * scale));
    }

    private void CollectFavorites(List<ContactDto> target)
    {
        target.Clear();
        var snapshot = contacts.Contacts;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].IsMutual && configuration.MessageFavoriteContacts.Contains(snapshot[index].UserId))
            {
                target.Add(snapshot[index]);
            }
        }

        target.Sort(CompareContactsByLabel);
    }

    private void DrawFavoriteCallRow(ImDrawListPtr drawList, ContactDto contact)
    {
        var scale = UiScale.Current;
        var label = ContactBook.DisplayLabel(contact);
        var row = BeginPersonRow(drawList, CallRowHeight, CallRowAvatarRadius, CallFavoriteRadius * 2f * scale, true,
            out var avatarCenter);
        DrawContactAvatar(drawList, contact, avatarCenter, CallRowAvatarRadius * scale);
        DrawRowTitleAndSub(drawList, new MarqueeId("messageapp.calls.favorite.", contact.UserId), label,
            ContactBook.Format(contact.PhoneNumber), row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk,
            ink.MutedInk);
        var callCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - CallFavoriteRadius * scale,
            row.Bounds.Center.Y);
        var callExtent = new Vector2(CallFavoriteRadius * scale, CallFavoriteRadius * scale);
        var callHovered = UiInteract.Hover(callCenter - callExtent, callCenter + callExtent);
        PhoneIcon.Draw(drawList, callCenter, PhoneIcons.Phone, callHovered ? ink.AccentLink : ink.Accent,
            CallFavoriteGlyph * scale);
        HoverTooltip.Show(new Rect(callCenter - callExtent, callCenter + callExtent), Loc.T(L.Friends.Call),
            HoverLabelSide.Above);
        if (UiInteract.Click(callCenter - callExtent, callCenter + callExtent, callHovered) || row.Tapped)
        {
            Place(new CallContact(contact.UserId, string.Empty, string.Empty, label), addMode: false);
        }

        EndPersonRow(drawList, row);
    }

    private void DrawLogRow(ImDrawListPtr drawList, CallLogEntry entry)
    {
        var scale = UiScale.Current;
        var known = contacts.Find(entry.UserId);
        var name = known is not null ? ContactBook.DisplayLabel(known) : entry.DisplayName;
        var title = entry.Count > 1 ? $"{name} ({entry.Count})" : name;
        var callable = known is not null && known.IsMutual;
        var infoReserve = known is not null ? CallInfoRadius * 2f * scale + RowTrailingGap * scale : 0f;
        var row = BeginPersonRow(drawList, CallRowHeight, CallRowAvatarRadius, infoReserve, known is not null,
            out var avatarCenter);
        if (known is not null && !string.IsNullOrEmpty(known.AvatarUrl))
        {
            DrawContactAvatar(drawList, known, avatarCenter, CallRowAvatarRadius * scale);
        }
        else
        {
            AvatarView.Draw(drawList, avatarCenter, CallRowAvatarRadius * scale, theme.Accent, Initials.Of(name), 1f,
                lodestone.Avatar(entry.Name, entry.World, CallRowAvatarRadius * 2f * scale), 32);
        }

        var right = row.TextRight;
        if (known is not null)
        {
            var infoCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - CallInfoRadius * scale,
                row.Bounds.Center.Y);
            var infoExtent = new Vector2(CallInfoRadius * scale, CallInfoRadius * scale);
            var infoHovered = UiInteract.Hover(infoCenter - infoExtent, infoCenter + infoExtent);
            PhoneIcon.Draw(drawList, infoCenter, PhoneIcons.InfoCircle, infoHovered ? ink.AccentLink : ink.MutedInk,
                CallInfoGlyph * scale);
            HoverTooltip.Show(new Rect(infoCenter - infoExtent, infoCenter + infoExtent), Loc.T(L.Phone.ContactInfo),
                HoverLabelSide.Above);
            if (UiInteract.Click(infoCenter - infoExtent, infoCenter + infoExtent, infoHovered))
            {
                router.Push(MessageRoute.Contact(known.UserId));
            }
        }

        var time = TimeText.Short(entry.TimestampUnix);
        var timeSize = Typography.Measure(time, RowMetaStyle);
        Typography.Draw(drawList, new Vector2(right - timeSize.X, row.Bounds.Center.Y - timeSize.Y * 0.5f), time,
            ink.MutedInk, RowMetaStyle);
        right -= timeSize.X + RowTrailingGap * scale;

        var missed = entry.Direction == CallDirection.Missed;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subHeight = Typography.LineHeight(RowSubStyle);
        var top = row.Bounds.Center.Y - (titleHeight + RowLineGap * scale + subHeight) * 0.5f;
        var width = MathF.Max(1f, right - row.TextLeft);
        var hovering = UiInteract.Hover(new Vector2(row.TextLeft, top), new Vector2(right, top + titleHeight));
        Marquee.DrawLeft(drawList, new MarqueeId("messageapp.calls.title.", entry.UserId + entry.TimestampUnix), title,
            row.TextLeft, top, width, RowTitleStyle, missed ? ink.Danger : ink.TitleInk, hovering);
        var directionGlyph = entry.Direction switch
        {
            CallDirection.Outgoing => PhoneIcons.PhoneOutgoing,
            CallDirection.Incoming => PhoneIcons.PhoneIncoming,
            _ => PhoneIcons.PhoneX,
        };
        var directionInk = missed ? ink.Danger : ink.MutedInk;
        var directionLabel = entry.Direction switch
        {
            CallDirection.Outgoing => Loc.T(L.Phone.Outgoing),
            CallDirection.Incoming => Loc.T(L.Phone.Incoming),
            _ => Loc.T(L.Phone.Missed),
        };
        var subTop = top + titleHeight + RowLineGap * scale;
        PhoneIcon.Draw(drawList, new Vector2(row.TextLeft + CallDirectionGlyph * 0.5f * scale, subTop + subHeight * 0.5f),
            directionGlyph, directionInk, CallDirectionGlyph * scale);
        var labelLeft = row.TextLeft + CallDirectionGlyph * scale + 5f * scale;
        Typography.Draw(drawList, new Vector2(labelLeft, subTop),
            Typography.FitText(directionLabel, MathF.Max(1f, right - labelLeft), RowSubStyle), ink.MutedInk,
            RowSubStyle);

        if (row.Tapped)
        {
            if (callable)
            {
                Place(new CallContact(entry.UserId, entry.Name, entry.World, name), addMode: false);
            }
            else if (known is not null)
            {
                router.Push(MessageRoute.Contact(known.UserId));
            }
        }

        EndPersonRow(drawList, row);
    }

    private void DrawNewCall(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Phone.NewCall));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawContactPicker(body, addMode: false);
    }

    private void DrawAddToCall(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Phone.AddToCall));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawContactPicker(body, addMode: true);
    }

    private void DrawContactPicker(Rect body, bool addMode)
    {
        var scale = UiScale.Current;
        var searchRect = new Rect(new Vector2(body.Min.X + CellPadX * scale, body.Min.Y),
            new Vector2(body.Max.X - CellPadX * scale, body.Min.Y + CallSearchHeight * scale));
        SearchField.Draw(searchRect, "##msgCallSearch", Loc.T(L.Phone.FilterHint), ref searchDraft, ui.Palette);
        var listRect = new Rect(new Vector2(body.Min.X, searchRect.Max.Y), body.Max);

        var query = searchDraft.Trim();
        callableContacts.Clear();
        var snapshot = contacts.Contacts;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var entry = snapshot[index];
            if (!entry.IsMutual)
            {
                continue;
            }

            if (query.Length > 0 && !MatchesContact(entry, query))
            {
                continue;
            }

            callableContacts.Add(entry);
        }

        callableContacts.Sort(CompareContactsByLabel);
        if (callableContacts.Count == 0)
        {
            if (query.Length > 0)
            {
                EmptyState.Draw(listRect, ui, PhoneIcons.Search, Loc.T(L.Phone.NoOneFound), string.Empty);
            }
            else
            {
                EmptyState.Draw(listRect, ui, PhoneIcons.Users, Loc.T(L.Phone.NoContactsTitle),
                    Loc.T(L.Message.NoContacts));
            }

            return;
        }

        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var lastLetter = string.Empty;
            for (var index = 0; index < callableContacts.Count; index++)
            {
                var letter = TrimmedLetter(ContactBook.DisplayLabel(callableContacts[index]));
                if (!string.Equals(letter, lastLetter, StringComparison.Ordinal))
                {
                    DrawLetterHeader(drawList, letter);
                    lastLetter = letter;
                }

                DrawPickerRow(drawList, callableContacts[index], addMode);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawPickerRow(ImDrawListPtr drawList, ContactDto contact, bool addMode)
    {
        var scale = UiScale.Current;
        var label = ContactBook.DisplayLabel(contact);
        var row = BeginPersonRow(drawList, CallRowHeight, CallRowAvatarRadius, CallFavoriteRadius * 2f * scale, true,
            out var avatarCenter);
        DrawContactAvatar(drawList, contact, avatarCenter, CallRowAvatarRadius * scale);
        DrawRowTitleAndSub(drawList, new MarqueeId("messageapp.calls.pick.", contact.UserId), label,
            ContactBook.Format(contact.PhoneNumber), row.TextLeft, row.TextRight, row.Bounds.Center.Y, ink.TitleInk,
            ink.MutedInk);
        var callCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - CallFavoriteRadius * scale,
            row.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, callCenter, PhoneIcons.Phone, ink.Accent, CallFavoriteGlyph * scale);
        if (row.Tapped)
        {
            Place(new CallContact(contact.UserId, string.Empty, string.Empty, label), addMode);
        }

        EndPersonRow(drawList, row);
    }

    private void DrawEnablePrompt(Rect body, float scale)
    {
        var centerX = body.Center.X;
        var baseY = body.Center.Y - 60f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, baseY);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        PhoneIcon.Draw(drawList, iconCenter, PhoneIcons.Phone, CallGreen, 34f * scale);
        Typography.DrawCentered(new Vector2(centerX, baseY + 56f * scale), Loc.T(L.Phone.EnableTitle), ui.TitleInk,
            TextStyles.Title3);
        var maxWidth = MathF.Min(body.Width - 56f * scale, 300f * scale);
        Typography.DrawWrappedCentered(new Vector2(centerX, baseY + 82f * scale), Loc.T(L.Phone.EnableBody),
            ui.MutedInk, TextStyles.Subheadline, maxWidth);
        var buttonWidth = 190f * scale;
        var buttonTop = baseY + 128f * scale;
        var buttonRect = new Rect(new Vector2(centerX - buttonWidth * 0.5f, buttonTop),
            new Vector2(centerX + buttonWidth * 0.5f, buttonTop + 46f * scale));
        if (ui.PillButton(buttonRect, Loc.T(L.Phone.Enable), true))
        {
            calls.SetEnabled(true);
        }
    }

    private void DrawConnectingHint(Rect body, float scale)
    {
        if (currentCall.Connected)
        {
            return;
        }

        Typography.DrawCentered(new Vector2(body.Center.X, body.Max.Y - 16f * scale), Loc.T(L.Phone.Connecting),
            ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawCallScreen(in PhoneContext context, CallView view)
    {
        var scale = UiScale.Current;
        var screenTheme = context.Theme;
        var content = context.Content;
        var drawList = ImGui.GetWindowDrawList();
        var others = Others(view);
        var centerX = content.Center.X;
        var connecting = view.State is CallState.Dialing or CallState.Connecting;
        var avatarTop = content.Min.Y + 54f * scale;
        if (others.Count <= 1)
        {
            var radius = 56f * scale;
            var avatarCenter = new Vector2(centerX, avatarTop + radius);
            DrawAvatarBloom(drawList, avatarCenter, radius, ui.Accent, scale);
            if (connecting)
            {
                DrawCallingPulse(drawList, avatarCenter, radius, scale);
            }

            if (others.Count == 1)
            {
                if (view.State == CallState.Active)
                {
                    DrawSpeakingHalo(drawList, avatarCenter, radius, calls.LevelOf(others[0]), scale);
                }

                AvatarView.Draw(drawList, avatarCenter, radius, ui.Accent, Initial(view.PeerLabel), 2.6f,
                    lodestone.Avatar(others[0].Name, others[0].World, radius * 2f), 64);
            }
            else
            {
                drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(ui.Accent), 64);
                Typography.DrawCentered(avatarCenter, Initial(view.PeerLabel), White, TextStyles.LargeTitle);
            }

            DrawCallHeadings(centerX, avatarCenter.Y + radius + 34f * scale, view, screenTheme, scale);
        }
        else
        {
            DrawParticipantGrid(content, others, screenTheme, scale, avatarTop);
            DrawCallHeadings(centerX, avatarTop + 150f * scale, view, screenTheme, scale);
        }

        DrawCallControls(context, view, scale, screenTheme);
    }

    private void DrawCallHeadings(float centerX, float nameCenterY, CallView view, PhoneTheme screenTheme, float scale)
    {
        Typography.DrawCentered(new Vector2(centerX, nameCenterY), view.PeerLabel, ui.TitleInk, TextStyles.Title1);
        var statusColor = view.Connected
            ? Palette.WithAlpha(ui.TitleInk, 0.72f)
            : Palette.WithAlpha(CallGreen, 0.95f);
        Typography.DrawCentered(new Vector2(centerX, nameCenterY + 32f * scale), StatusLine(view), statusColor,
            TextStyles.Callout);
        if (view.State == CallState.Active)
        {
            var micSilent = !view.Muted
                && view.Seconds >= MicSilentWarningSeconds
                && view.PeakMicLevel < Core.Telephony.Audio.AudioCapture.GateOpenRms;
            if (micSilent)
            {
                var warning = Typography.FitText(Loc.T(L.Phone.MicNotReaching),
                    ImGui.GetWindowSize().X - 32f * scale, TextStyles.Footnote);
                Typography.DrawCentered(new Vector2(centerX, nameCenterY + 58f * scale), warning,
                    Palette.WithAlpha(screenTheme.Danger, 0.92f), TextStyles.Footnote);
            }
            else
            {
                Typography.DrawCentered(new Vector2(centerX, nameCenterY + 58f * scale), Loc.T(L.Phone.UseHeadphones),
                    Palette.WithAlpha(ui.TitleInk, 0.45f), TextStyles.Footnote);
            }
        }
    }

    private static void DrawAvatarBloom(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent,
        float scale)
    {
        for (var ring = 3; ring >= 1; ring--)
        {
            var bloomRadius = radius + ring * 15f * scale;
            drawList.AddCircleFilled(center, bloomRadius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.05f)), 64);
        }
    }

    private static void DrawCallingPulse(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var phase = (float)(ImGui.GetTime() % 2.0) * 0.5f;
        var spread = (8f + 34f * phase) * scale;
        var alpha = 0.4f * (1f - phase);
        drawList.AddCircle(center, radius + spread, ImGui.GetColorU32(Palette.WithAlpha(CallGreen, alpha)), 64,
            2.2f * scale);
    }

    private void DrawParticipantGrid(Rect content, List<ParticipantInfo> others, PhoneTheme screenTheme, float scale,
        float top)
    {
        const int columns = 4;
        var radius = 26f * scale;
        var cellWidth = content.Width / columns;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < others.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellCenterX = content.Min.X + column * cellWidth + cellWidth * 0.5f;
            var cellCenterY = top + radius + row * (radius * 2f + 22f * scale);
            var center = new Vector2(cellCenterX, cellCenterY);
            DrawSpeakingHalo(drawList, center, radius, calls.LevelOf(others[index]), scale);
            AvatarView.Draw(drawList, center, radius, ui.Accent, Initial(others[index].DisplayName), 1.2f,
                lodestone.Avatar(others[index].Name, others[index].World, radius * 2f), 48);
            Typography.DrawCentered(new Vector2(cellCenterX, cellCenterY + radius + 12f * scale),
                UiText.Truncate(others[index].DisplayName, 10), ui.TitleInk, 0.78f);
        }
    }

    private void DrawCallControls(in PhoneContext context, CallView view, float scale, PhoneTheme screenTheme)
    {
        var content = context.Content;
        var centerX = content.Center.X;
        var controlsY = content.Max.Y - 74f * scale;
        var spacing = 84f * scale;
        var frost = Palette.WithAlpha(ui.TitleInk, 0.16f);
        var labelColor = Palette.WithAlpha(ui.TitleInk, 0.72f);
        var muteFill = view.Muted ? White : frost;
        var muteInk = view.Muted ? MessageThemes.Body : ui.TitleInk;
        if (ControlButton(new Vector2(centerX - spacing, controlsY), 27f * scale,
                view.Muted ? PhoneIcons.MicrophoneOff : PhoneIcons.Microphone, muteFill, muteInk,
                Loc.T(L.Message.MuteAction), labelColor, 24f, true))
        {
            calls.ToggleMute();
        }

        if (ControlButton(new Vector2(centerX, controlsY), 33f * scale, PhoneIcons.PhoneX, screenTheme.Danger,
                White, Loc.T(L.Phone.End), labelColor, 30f, true))
        {
            calls.Hangup();
        }

        var canAdd = view.State == CallState.Active;
        if (ControlButton(new Vector2(centerX + spacing, controlsY), 27f * scale, PhoneIcons.UserPlus, frost,
                ui.TitleInk, Loc.T(L.Friends.Add), labelColor, 24f, canAdd) && canAdd)
        {
            router.Push(MessageRoute.AddToCall);
        }
    }

    private static bool ControlButton(Vector2 center, float radius, string glyph, Vector4 fill, Vector4 glyphInk,
        string label, Vector4 labelColor, float glyphSize, bool enabled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && UiInteract.Hover(min, max);
        var baseFill = hovered ? Palette.Mix(fill, White, 0.14f) : fill;
        var fillAlpha = enabled ? MathF.Max(baseFill.W, 0.16f) : baseFill.W * 0.4f;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(baseFill, fillAlpha)), 40);
        PhoneIcon.Draw(drawList, center, glyph, Palette.WithAlpha(glyphInk, enabled ? 1f : 0.45f), glyphSize * scale);
        if (label.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(center.X, max.Y + 14f * scale), label,
                Palette.WithAlpha(labelColor, enabled ? 1f : 0.45f), TextStyles.Caption1);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawSpeakingHalo(ImDrawListPtr drawList, Vector2 center, float radius, float level, float scale)
    {
        if (level <= 0.03f)
        {
            return;
        }

        var spread = (3f + 10f * Math.Clamp(level * 6f, 0f, 1f)) * scale;
        drawList.AddCircle(center, radius + spread, ImGui.GetColorU32(Palette.WithAlpha(CallGreen, 0.5f)), 48,
            2.5f * scale);
    }

    private void Place(CallContact contact, bool addMode)
    {
        if (addMode)
        {
            calls.AddParticipant(contact);
            router.Pop();
            return;
        }

        if (router.Current.Screen == MessageScreen.NewCall)
        {
            router.Pop(false);
        }

        calls.StartCall(contact);
        ShowCallScreen();
    }

    private List<ParticipantInfo> Others(CallView view)
    {
        var list = new List<ParticipantInfo>();
        for (var index = 0; index < view.Participants.Length; index++)
        {
            if (view.Participants[index].UserId != view.LocalUserId)
            {
                list.Add(view.Participants[index]);
            }
        }

        return list;
    }

    private static string StatusLine(CallView view)
    {
        if (!view.Connected)
        {
            return Loc.T(L.Phone.Reconnecting);
        }

        return view.State switch
        {
            CallState.Dialing => Loc.T(L.Phone.StatusCalling),
            CallState.Connecting => Loc.T(L.Phone.StatusConnecting),
            CallState.Active => TimeText.Duration(view.Seconds),
            _ => string.Empty,
        };
    }

    private static string Initial(string value) => value.Length > 0 ? value.Substring(0, 1).ToUpperInvariant() : "?";
}
