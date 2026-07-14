using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float CallRowHeight = 62f;
    private const float CallSearchHeight = 52f;
    private const float ReturnBannerHeight = 44f;

    private string searchDraft = string.Empty;

    private float DrawReturnToCallBanner(Rect rect)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
        Typography.Draw(new Vector2(max.X - 16f * scale - statusSize.X, centerY - statusSize.Y * 0.5f), status,
            White, TextStyles.Footnote);
        var label = Loc.T(L.Phone.ReturnToCall);
        var labelWidth = max.X - min.X - 48f * scale - statusSize.X;
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        Typography.Draw(new Vector2(min.X + 28f * scale, centerY - labelSize.Y * 0.5f),
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

        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), string.Empty, back);
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
        var scale = ImGuiHelpers.GlobalScale;
        if (!session.IsSignedIn)
        {
            EmptyState.Draw(area, ui, FontAwesomeIcon.Phone, Loc.T(L.Phone.SignInTitle), Loc.T(L.Phone.SignInPrompt));
            return;
        }

        if (!calls.Enabled)
        {
            DrawEnablePrompt(area, scale);
            return;
        }

        calls.MarkLogSeen();
        var log = calls.CallLog;
        if (log.Length == 0)
        {
            EmptyState.Draw(area, ui, FontAwesomeIcon.Phone, Loc.T(L.Phone.NoRecentCalls),
                Loc.T(L.Phone.NoRecentCallsHint));
        }
        else
        {
            using (AppSurface.Begin(area))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < log.Length; index++)
                {
                    DrawLogRow(log[index], scale);
                }

                ImGui.Dummy(new Vector2(0f, 72f * scale));
            }
        }

        DrawConnectingHint(area, scale);
        if (ComposeFab.Draw(area, "##msgNewCallFab", ui.Accent, FontAwesomeIcon.Phone.ToIconString(),
                Loc.T(L.Phone.NewCall), "message.newcall"))
        {
            searchDraft = string.Empty;
            router.Push(MessageRoute.NewCall);
        }
    }

    private void DrawLogRow(CallLogEntry entry, float scale)
    {
        var known = contacts.Find(entry.UserId);
        var name = known is not null ? ContactBook.DisplayLabel(known) : entry.DisplayName;
        var title = entry.Count > 1 ? $"{name} ({entry.Count})" : name;
        var rowHeight = CallRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var callable = known is not null && known.IsMutual;
        ui.Card(drawList, origin, rowMax, rounding);
        if ((callable || known is not null) && UiInteract.Hover(origin, rowMax))
        {
            Squircle.Fill(drawList, origin, rowMax, rounding, ImGui.GetColorU32(ui.HoverTint));
        }

        var pad = 14f * scale;
        var radius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        if (known is not null && !string.IsNullOrEmpty(known.AvatarUrl))
        {
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, name, string.Empty, known.AvatarUrl, images,
                lodestone, 1f, 32);
        }
        else
        {
            AvatarView.Draw(drawList, avatarCenter, radius, theme.Accent, Initials.Of(name), 1f,
                lodestone.Avatar(entry.Name, entry.World), 32);
        }

        var actionLeft = rowMax.X - pad;
        if (known is not null)
        {
            var infoCenter = new Vector2(rowMax.X - pad - 16f * scale, avatarCenter.Y);
            actionLeft = infoCenter.X - 22f * scale;
            if (ui.IconButton(infoCenter, 16f * scale, FontAwesomeIcon.InfoCircle.ToIconString(), ui.MutedInk,
                    Transparent, 1.1f, Loc.T(L.Phone.ContactInfo), HoverLabelSide.Above))
            {
                router.Push(MessageRoute.Contact(known.UserId));
            }
        }

        var time = TimeText.Short(entry.TimestampUnix);
        if (time.Length > 0)
        {
            var timeSize = Typography.Measure(time, TextStyles.Footnote);
            Typography.Draw(new Vector2(actionLeft - timeSize.X, avatarCenter.Y - timeSize.Y * 0.5f), time,
                ui.MutedInk, TextStyles.Footnote);
            actionLeft -= timeSize.X + 10f * scale;
        }

        var missed = entry.Direction == CallDirection.Missed;
        var textLeft = avatarCenter.X + radius + 14f * scale;
        var textWidth = actionLeft - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y + 13f * scale),
            Typography.FitText(title, textWidth, TextStyles.Headline), missed ? theme.Danger : ui.TitleInk,
            TextStyles.Headline);
        var directionIcon = entry.Direction == CallDirection.Outgoing
            ? FontAwesomeIcon.ArrowUp
            : FontAwesomeIcon.ArrowDown;
        var directionInk = missed ? theme.Danger : ui.MutedInk;
        var directionLabel = entry.Direction switch
        {
            CallDirection.Outgoing => Loc.T(L.Phone.Outgoing),
            CallDirection.Incoming => Loc.T(L.Phone.Incoming),
            _ => Loc.T(L.Phone.Missed),
        };
        AppSkin.Icon(new Vector2(textLeft + 5f * scale, origin.Y + 42f * scale), directionIcon.ToIconString(),
            directionInk, 0.6f);
        Typography.Draw(new Vector2(textLeft + 15f * scale, origin.Y + 35f * scale),
            Typography.FitText(directionLabel, textWidth - 15f * scale, TextStyles.Footnote), directionInk,
            TextStyles.Footnote);

        if (UiInteract.HoverClick(origin, new Vector2(actionLeft, rowMax.Y)))
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

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawNewCall(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.Phone.NewCall), back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawContactPicker(body, addMode: false);
    }

    private void DrawAddToCall(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.Phone.AddToCall), back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        DrawContactPicker(body, addMode: true);
    }

    private void DrawContactPicker(Rect body, bool addMode)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var searchRect = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + CallSearchHeight * scale));
        SearchField.DrawSubmit(searchRect, "##msgCallSearch", Loc.T(L.Phone.FilterHint), ref searchDraft,
            AppPalettes.Message);
        var listRect = new Rect(new Vector2(body.Min.X, searchRect.Max.Y), body.Max);

        var query = searchDraft.Trim();
        var callable = new List<ContactDto>();
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

            callable.Add(entry);
        }

        callable.Sort(CompareContactsByLabel);
        if (callable.Count == 0)
        {
            if (query.Length > 0)
            {
                EmptyState.Draw(listRect, ui, FontAwesomeIcon.Search, Loc.T(L.Phone.NoOneFound), string.Empty);
            }
            else
            {
                EmptyState.Draw(listRect, ui, FontAwesomeIcon.Users, Loc.T(L.Phone.NoContactsTitle),
                    Loc.T(L.Message.NoContacts));
            }

            return;
        }

        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < callable.Count; index++)
            {
                DrawPickerRow(callable[index], scale, addMode);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawPickerRow(ContactDto contact, float scale, bool addMode)
    {
        var label = ContactBook.DisplayLabel(contact);
        var rowHeight = CallRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        var rounding = 18f * scale;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, rowMax, rounding);
        if (UiInteract.Hover(origin, rowMax))
        {
            Squircle.Fill(drawList, origin, rowMax, rounding, ImGui.GetColorU32(ui.HoverTint));
        }

        var pad = 14f * scale;
        var radius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, contact.AvatarUrl, images,
            lodestone, 1f, 32);
        var callCenter = new Vector2(rowMax.X - pad - 18f * scale, avatarCenter.Y);
        var actionLeft = callCenter.X - 24f * scale;
        var textLeft = avatarCenter.X + radius + 14f * scale;
        var textWidth = actionLeft - textLeft;
        Typography.Draw(new Vector2(textLeft, origin.Y + 13f * scale),
            Typography.FitText(label, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, origin.Y + 35f * scale),
            Typography.FitText(ContactBook.Format(contact.PhoneNumber), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        if (ui.IconButton(callCenter, 18f * scale, FontAwesomeIcon.Phone.ToIconString(), White, CallGreen, 0.9f,
                Loc.T(L.Friends.Call), HoverLabelSide.Above)
            || UiInteract.HoverClick(origin, new Vector2(actionLeft, rowMax.Y)))
        {
            Place(new CallContact(contact.UserId, string.Empty, string.Empty, label), addMode);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawEnablePrompt(Rect body, float scale)
    {
        var centerX = body.Center.X;
        var baseY = body.Center.Y - 60f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var iconCenter = new Vector2(centerX, baseY);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        AppSkin.Icon(iconCenter, FontAwesomeIcon.Phone.ToIconString(), CallGreen, 1.7f);
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
        var scale = ImGuiHelpers.GlobalScale;
        var screenTheme = context.Theme;
        var content = context.Content;
        var drawList = ImGui.GetWindowDrawList();
        var others = Others(view);
        var centerX = content.Center.X;
        var avatarTop = content.Min.Y + 40f * scale;
        if (others.Count <= 1)
        {
            var radius = 52f * scale;
            var avatarCenter = new Vector2(centerX, avatarTop + radius);
            if (others.Count == 1)
            {
                DrawSpeakingHalo(drawList, avatarCenter, radius, calls.LevelOf(others[0]), scale);
                AvatarView.Draw(drawList, avatarCenter, radius, screenTheme.Accent, Initial(view.PeerLabel), 2.4f,
                    lodestone.Avatar(others[0].Name, others[0].World), 64);
            }
            else
            {
                drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(screenTheme.Accent), 64);
                Typography.DrawCentered(avatarCenter, Initial(view.PeerLabel), White, 2.4f);
            }
        }
        else
        {
            DrawParticipantGrid(content, others, screenTheme, scale, avatarTop);
        }

        var labelY = others.Count <= 1 ? avatarTop + 104f * scale + 22f * scale : avatarTop + 150f * scale;
        Typography.DrawCentered(new Vector2(centerX, labelY), view.PeerLabel, screenTheme.TextStrong, 1.6f);
        Typography.DrawCentered(new Vector2(centerX, labelY + 28f * scale), StatusLine(view),
            Palette.WithAlpha(screenTheme.TextStrong, 0.75f), 0.95f);
        if (view.State == CallState.Active)
        {
            Typography.DrawCentered(new Vector2(centerX, labelY + 50f * scale), Loc.T(L.Phone.UseHeadphones),
                screenTheme.TextMuted, 0.75f);
        }

        DrawCallControls(context, view, scale, screenTheme);
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
            AvatarView.Draw(drawList, center, radius, screenTheme.Accent, Initial(others[index].DisplayName), 1.2f,
                lodestone.Avatar(others[index].Name, others[index].World), 48);
            Typography.DrawCentered(new Vector2(cellCenterX, cellCenterY + radius + 12f * scale),
                Truncate(others[index].DisplayName, 10), screenTheme.TextStrong, 0.78f);
        }
    }

    private void DrawCallControls(in PhoneContext context, CallView view, float scale, PhoneTheme screenTheme)
    {
        var content = context.Content;
        var centerX = content.Center.X;
        var controlsY = content.Max.Y - 54f * scale;
        var muteFill = view.Muted ? CallGreen : Palette.WithAlpha(screenTheme.TextStrong, 0.16f);
        if (CircleButton(new Vector2(centerX - 76f * scale, controlsY), 24f * scale,
                view.Muted ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, muteFill,
                screenTheme.TextStrong))
        {
            calls.ToggleMute();
        }

        if (CircleButton(new Vector2(centerX, controlsY), 30f * scale, FontAwesomeIcon.PhoneSlash, screenTheme.Danger,
                White))
        {
            calls.Hangup();
        }

        var canAdd = view.State == CallState.Active;
        if (CircleButton(new Vector2(centerX + 76f * scale, controlsY), 24f * scale, FontAwesomeIcon.UserPlus,
                Palette.WithAlpha(screenTheme.TextStrong, 0.16f), screenTheme.TextStrong, canAdd) && canAdd)
        {
            router.Push(MessageRoute.AddToCall);
        }
    }

    private static bool CircleButton(Vector2 center, float radius, FontAwesomeIcon icon, Vector4 fill, Vector4 ink,
        bool enabled = true)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = enabled && ImGui.IsMouseHoveringRect(min, max);
        var color = hovered ? Palette.Mix(fill, White, 0.14f) : fill;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(color, enabled ? color.W : 0.4f)),
            32);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(ink, enabled ? 1f : 0.5f)))
            {
                Typography.Plain(glyph);
            }
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

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }
}
