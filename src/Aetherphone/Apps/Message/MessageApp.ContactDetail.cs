using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const int NotesMaxLength = 500;
    private const float NotesSaveDelaySeconds = 1f;

    private string notesDraft = string.Empty;
    private string? notesLoadedFor;
    private float notesSaveTimer;

    private void DrawContactDetail(Rect area, string userId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var contact = contacts.Find(userId);
        if (contact is null)
        {
            return;
        }

        if (notesLoadedFor != userId)
        {
            FlushNotes();
            notesLoadedFor = userId;
            notesDraft = configuration.MessageContactNotes.GetValueOrDefault(userId, string.Empty);
            notesSaveTimer = 0f;
        }

        TickNotes(userId);
        DrawFavoriteToggle(area, userId, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ImGui.GetContentRegionAvail().X;
            var origin = ImGui.GetCursorScreenPos();
            var centerX = origin.X + width * 0.5f;
            var radius = 44f * scale;
            var avatarCenter = new Vector2(centerX, origin.Y + 24f * scale + radius);
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, ContactBook.DisplayLabel(contact),
                string.Empty, contact.AvatarUrl, images, lodestone, 1.8f, 48);
            var nameY = avatarCenter.Y + radius + 20f * scale;
            Typography.DrawCentered(new Vector2(centerX, nameY), ContactBook.DisplayLabel(contact), ui.TitleInk,
                TextStyles.Title2);
            var afterName = nameY + 24f * scale;
            if (!contact.IsMutual)
            {
                Typography.DrawCentered(new Vector2(centerX, afterName), Loc.T(L.Friends.Pending), ui.MutedInk,
                    TextStyles.Subheadline);
                afterName += 26f * scale;
            }

            var actionsY = afterName + 26f * scale;
            DrawContactActions(contact, centerX, actionsY, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsY + 40f * scale));
            ImGui.Dummy(new Vector2(width, 0f));
            DrawContactInfoCard(contact, scale);
            DrawContactNotesCard(userId, scale);
            var sideInset = 16f * scale;
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var removeRect = ActionRect(sideInset, scale);
            if (ui.DangerGhostButton(removeRect, Loc.T(L.Friends.Remove)))
            {
                AskRemoveContact(contact);
            }

            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private void DrawFavoriteToggle(Rect area, string userId, float scale)
    {
        var favorite = configuration.MessageFavoriteContacts.Contains(userId);
        var starCenter = new Vector2(area.Max.X - 24f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var tooltip = Loc.T(favorite ? L.Message.RemoveFavorite : L.Message.AddFavorite);
        var color = favorite ? new Vector4(0.98f, 0.78f, 0.22f, 1f) : ui.MutedInk;
        if (ui.IconButton(starCenter, 16f * scale, FontAwesomeIcon.Star.ToIconString(), color, Transparent,
                favorite ? 1.15f : 1.05f, tooltip, HoverLabelSide.Below))
        {
            if (!configuration.MessageFavoriteContacts.Remove(userId))
            {
                configuration.MessageFavoriteContacts.Add(userId);
            }

            configuration.Save();
        }
    }

    private void DrawContactActions(ContactDto contact, float centerX, float centerY, float scale)
    {
        var buttonRadius = 24f * scale;
        var gap = 38f * scale;
        var canMessage = contact.IsMutual;
        var canCall = contact.IsMutual && calls.Enabled;
        var messageCenter = new Vector2(centerX - gap, centerY);
        var messageBackground = canMessage ? ui.Accent : ui.FieldSurface;
        var messageInk = canMessage ? White : ui.MutedInk;
        if (ui.IconButton(messageCenter, buttonRadius, FontAwesomeIcon.Comment.ToIconString(), messageInk,
                messageBackground, 1.1f, Loc.T(L.DirectMessages.StartChat), HoverLabelSide.Below) && canMessage)
        {
            StartMessage(contact);
        }

        var callCenter = new Vector2(centerX + gap, centerY);
        var callBackground = canCall ? CallGreen : ui.FieldSurface;
        var callInk = canCall ? White : ui.MutedInk;
        if (ui.IconButton(callCenter, buttonRadius, FontAwesomeIcon.Phone.ToIconString(), callInk,
                callBackground, 1.1f, Loc.T(L.Friends.Call), HoverLabelSide.Below) && canCall)
        {
            StartCall(contact);
        }
    }

    private void DrawContactInfoCard(ContactDto contact, float scale)
    {
        var sideInset = 16f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rowHeight = 34f * scale;
        var pad = 14f * scale;
        var rowCount = 3;
        var localTime = ContactLocalTime(contact.UserId);
        if (localTime.Length > 0)
        {
            rowCount++;
        }

        var cardMin = new Vector2(origin.X + sideInset, origin.Y);
        var cardMax = new Vector2(origin.X + width - sideInset, origin.Y + pad * 2f + rowHeight * rowCount);
        ui.Card(ImGui.GetWindowDrawList(), cardMin, cardMax, 16f * scale);
        var rowY = cardMin.Y + pad;
        DrawInfoRow(cardMin.X + pad, cardMax.X - pad, rowY, rowHeight, Loc.T(L.Message.Number),
            ContactBook.Format(contact.PhoneNumber));
        rowY += rowHeight;
        DrawInfoRow(cardMin.X + pad, cardMax.X - pad, rowY, rowHeight, Loc.T(L.Message.Handle),
            "@" + contact.Handle);
        rowY += rowHeight;
        if (localTime.Length > 0)
        {
            DrawInfoRow(cardMin.X + pad, cardMax.X - pad, rowY, rowHeight, Loc.T(L.Message.LocalTime), localTime);
            rowY += rowHeight;
        }

        DrawInfoRow(cardMin.X + pad, cardMax.X - pad, rowY, rowHeight, Loc.T(L.Message.Added),
            DateTimeOffset.FromUnixTimeSeconds(contact.CreatedAtUnix).ToLocalTime().ToString("d", Loc.Culture));
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardMax.Y - cardMin.Y + 12f * scale));
    }

    private void DrawInfoRow(float left, float right, float top, float rowHeight, string label, string value)
    {
        var centerY = top + rowHeight * 0.5f;
        Typography.Draw(new Vector2(left, centerY - 8f * ImGuiHelpers.GlobalScale), label, ui.MutedInk,
            TextStyles.Footnote);
        var valueSize = Typography.Measure(value, TextStyles.Subheadline);
        Typography.Draw(new Vector2(right - valueSize.X, centerY - valueSize.Y * 0.5f), value, ui.BodyInk,
            TextStyles.Subheadline);
    }

    private string ContactLocalTime(string userId)
    {
        var snapshot = store.Conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            var item = snapshot[index];
            if (!item.IsGroup && item.OtherUserId == userId && item.UtcOffsetMinutes is { } offsetMinutes)
            {
                return SocialTimeZone.Describe(offsetMinutes);
            }
        }

        return string.Empty;
    }

    private void DrawContactNotesCard(string userId, float scale)
    {
        var sideInset = 16f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetCursorScreenPos(new Vector2(origin.X + sideInset + 4f * scale, origin.Y));
        ui.SectionLabel(Loc.T(L.Message.Notes));
        var cardTop = ImGui.GetCursorScreenPos().Y;
        var pad = 12f * scale;
        var cardMin = new Vector2(origin.X + sideInset, cardTop);
        var cardMax = new Vector2(origin.X + width - sideInset, cardTop + 96f * scale);
        ui.Card(ImGui.GetWindowDrawList(), cardMin, cardMax, 14f * scale);
        ImGui.SetCursorScreenPos(cardMin + new Vector2(pad, pad));
        var inputWidth = cardMax.X - cardMin.X - pad * 2f;
        var wrapWidth = inputWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        var before = notesDraft;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            SoftWrapField.Multiline("##msgContactNotes", ref notesDraft, NotesMaxLength,
                new Vector2(inputWidth, cardMax.Y - cardMin.Y - pad * 2f), wrapWidth);
        }

        if (notesDraft.Length == 0)
        {
            ImGui.SetCursorScreenPos(cardMin + new Vector2(pad + 4f * scale, pad + 2f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
            {
                ImGui.TextUnformatted(Loc.T(L.Message.NotesHint));
            }
        }

        if (!string.Equals(before, notesDraft, StringComparison.Ordinal))
        {
            notesSaveTimer = NotesSaveDelaySeconds;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardMax.Y - origin.Y + 12f * scale));
    }

    private void TickNotes(string userId)
    {
        if (notesSaveTimer <= 0f)
        {
            return;
        }

        notesSaveTimer -= ImGui.GetIO().DeltaTime;
        if (notesSaveTimer <= 0f)
        {
            SaveNotes(userId);
        }
    }

    private void FlushNotes()
    {
        if (notesLoadedFor is { } userId && notesSaveTimer > 0f)
        {
            notesSaveTimer = 0f;
            SaveNotes(userId);
        }
    }

    private void SaveNotes(string userId)
    {
        var trimmed = notesDraft.Trim();
        if (trimmed.Length == 0)
        {
            if (!configuration.MessageContactNotes.Remove(userId))
            {
                return;
            }
        }
        else
        {
            configuration.MessageContactNotes[userId] = trimmed;
        }

        configuration.Save();
    }

    private void AskRemoveContact(ContactDto contact)
    {
        var userId = contact.UserId;
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Friends.ConfirmRemove, ContactBook.DisplayLabel(contact)),
            ConfirmLabel = Loc.T(L.Friends.Remove),
            CancelLabel = Loc.T(L.Common.Cancel),
            BusyLabel = Loc.T(L.Friends.Sending),
            FailedMessage = Loc.T(L.Friends.RemoveFailed),
            Danger = true,
            ConfirmAsync = done => contacts.Remove(userId, ok =>
            {
                if (ok)
                {
                    removePending = true;
                }

                done(ok);
            }),
        });
    }
}
