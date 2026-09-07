using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const int NotesMaxLength = 500;
    private const float NotesSaveDelaySeconds = 1f;
    private const float HeroAvatarRadius = 48f;
    private const float HeroTopPad = 18f;
    private const float HeroNameGap = 16f;
    private const float HeroActionHeight = 62f;
    private const float HeroActionGap = 8f;
    private const float NotesCardHeight = 96f;

    private static readonly Vector4 TintGold = new(0.93f, 0.68f, 0.18f, 1f);
    private static readonly Vector4 TintRed = new(0.92f, 0.33f, 0.31f, 1f);
    private static readonly Vector4 TintTeal = new(0.13f, 0.63f, 0.62f, 1f);
    private static readonly Vector4 TintAzure = new(0.20f, 0.55f, 0.92f, 1f);
    private static readonly Vector4 TintSlate = new(0.50f, 0.54f, 0.60f, 1f);
    private static readonly Vector4 TintGreen = new(0.20f, 0.68f, 0.38f, 1f);
    private static readonly Vector4 TintViolet = new(0.60f, 0.45f, 0.92f, 1f);

    private string notesDraft = string.Empty;
    private string? notesLoadedFor;
    private float notesSaveTimer;
    private string contactNameDraft = string.Empty;
    private string renameError = string.Empty;
    private bool editingContactName;
    private volatile bool renameBusy;
    private volatile int renameOutcome;

    private void DrawContactDetail(Rect area, string userId)
    {
        var scale = UiScale.Current;
        var headerDrawList = ImGui.GetWindowDrawList();
        var contact = contacts.Find(userId);
        DrawScreenHeader(area, string.Empty, contact is null ? 0 : 1);
        if (contact is null)
        {
            return;
        }

        if (DrawHeaderIcon(headerDrawList, SocialChrome.HeaderSlot(area, 0), PhoneIcons.Pencil, Loc.T(L.Friends.EditName),
                editingContactName))
        {
            editingContactName = !editingContactName;
            contactNameDraft = contact.Alias;
            renameError = string.Empty;
        }

        if (notesLoadedFor != userId)
        {
            FlushNotes();
            notesLoadedFor = userId;
            notesDraft = configuration.MessageContactNotes.GetValueOrDefault(userId, string.Empty);
            notesSaveTimer = 0f;
            editingContactName = false;
            renameError = string.Empty;
            renameOutcome = 0;
        }

        ProcessRenameOutcome();
        TickNotes(userId);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var width = ScrollLayout.StableContentWidth();
            var origin = ImGui.GetCursorScreenPos();
            var centerX = origin.X + width * 0.5f;
            var radius = HeroAvatarRadius * scale;
            var avatarCenter = new Vector2(centerX, origin.Y + HeroTopPad * scale + radius);
            DrawContactAvatar(drawList, contact, avatarCenter, radius);
            avatarLightbox.TryOpen(avatarCenter, radius, contact.AvatarUrl, images);
            var nameY = avatarCenter.Y + radius + HeroNameGap * scale;
            float afterName;
            if (editingContactName)
            {
                afterName = DrawContactNameEditor(contact, origin.X, width, nameY, scale);
            }
            else
            {
                var label = ContactBook.DisplayLabel(contact);
                var labelHeight = Typography.DrawWrappedCentered(new Vector2(centerX, nameY), label, ink.TitleInk,
                    TextStyles.Title2, width - 24f * scale);
                afterName = nameY + labelHeight + 4f * scale;
                var number = ContactBook.Format(contact.PhoneNumber);
                afterName += Typography.DrawWrappedCentered(new Vector2(centerX, afterName), number, ink.MutedInk,
                    TextStyles.Subheadline, width - 24f * scale) + 4f * scale;
            }

            if (!contact.IsMutual)
            {
                afterName += Typography.DrawWrappedCentered(new Vector2(centerX, afterName), Loc.T(L.Friends.Pending),
                    ink.MutedInk, TextStyles.Footnote, width - 24f * scale) + 4f * scale;
            }

            var actionsTop = afterName + 14f * scale;
            var actionsBottom = DrawContactActions(drawList, contact, userId, origin.X, width, actionsTop, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, actionsBottom + Metrics.Space.Lg * scale));
            ImGui.Dummy(new Vector2(width, 0f));
            DrawContactInfoCard(drawList, contact);
            DrawContactChatCard(drawList, userId);
            DrawContactNotesCard(userId, scale);
            var dangerCard = GroupCard.Begin(ui, 1, SettingRowHeight);
            if (DrawCardDangerRow(drawList, dangerCard.NextRow(), PhoneIcons.Trash, Loc.T(L.Message.RemoveContact)))
            {
                AskRemoveContact(contact);
            }

            dangerCard.End();
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    private float DrawContactActions(ImDrawListPtr drawList, ContactDto contact, string userId, float left,
        float width, float top, float scale)
    {
        var gap = HeroActionGap * scale;
        var buttonWidth = (width - gap * 2f) / 3f;
        var height = HeroActionHeight * scale;
        var canMessage = contact.IsMutual;
        var canCall = contact.IsMutual && calls.Enabled;
        var favorite = configuration.MessageFavoriteContacts.Contains(userId);
        var messageRect = new Rect(new Vector2(left, top), new Vector2(left + buttonWidth, top + height));
        if (DrawHeroActionButton(drawList, messageRect, PhoneIcons.MessageCircle, Loc.T(L.DirectMessages.StartChat),
                canMessage))
        {
            StartMessage(contact);
        }

        var callRect = new Rect(new Vector2(messageRect.Max.X + gap, top),
            new Vector2(messageRect.Max.X + gap + buttonWidth, top + height));
        if (DrawHeroActionButton(drawList, callRect, PhoneIcons.Phone, Loc.T(L.Friends.Call), canCall))
        {
            StartCall(contact);
        }

        var favoriteRect = new Rect(new Vector2(callRect.Max.X + gap, top), new Vector2(left + width, top + height));
        if (DrawHeroActionButton(drawList, favoriteRect, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
                Loc.T(favorite ? L.Message.FavoritedLabel : L.Message.FavoriteLabel), true))
        {
            if (!configuration.MessageFavoriteContacts.Remove(userId))
            {
                configuration.MessageFavoriteContacts.Add(userId);
            }

            configuration.Save();
        }

        return top + height;
    }

    private float DrawContactNameEditor(ContactDto contact, float left, float width, float nameY, float scale)
    {
        var fieldHeight = FieldHeight * scale;
        var iconRadius = 15f * scale;
        var gap = 10f * scale;
        var iconSpan = (iconRadius * 2f + gap) * 2f;
        var top = nameY;
        var fieldRect = new Rect(new Vector2(left, top), new Vector2(left + width - iconSpan, top + fieldHeight));
        var submitted = PillField(fieldRect, "##msgContactRename", contact.DisplayName, ref contactNameDraft,
            AliasMaxLength);
        var centerY = top + fieldHeight * 0.5f;
        var saveCenter = new Vector2(fieldRect.Max.X + gap + iconRadius, centerY);
        var canSave = !renameBusy && !string.Equals(contactNameDraft.Trim(), contact.Alias, StringComparison.Ordinal);
        var saveBackground = canSave ? ui.Accent : ui.FieldSurface;
        var saveInk = canSave ? White : ui.MutedInk;
        if ((ui.IconButton(saveCenter, iconRadius, PhoneIcons.Check, saveInk, saveBackground, 0.95f,
                Loc.T(L.DirectMessages.Save), HoverLabelSide.Below) || submitted) && canSave)
        {
            SubmitRename(contact.UserId);
        }

        var cancelCenter = new Vector2(saveCenter.X + iconRadius + gap + iconRadius, centerY);
        if (ui.IconButton(cancelCenter, iconRadius, PhoneIcons.X, ui.MutedInk, ui.FieldSurface, 0.95f,
                Loc.T(L.Common.Cancel), HoverLabelSide.Below))
        {
            editingContactName = false;
            renameError = string.Empty;
        }

        var afterName = top + fieldHeight + 10f * scale;
        if (renameError.Length > 0)
        {
            afterName += Typography.DrawWrappedCentered(new Vector2(left + width * 0.5f, afterName), renameError,
                theme.Danger, TextStyles.Footnote, width - 24f * scale) + 6f * scale;
        }

        return afterName;
    }

    private void SubmitRename(string userId)
    {
        renameBusy = true;
        renameError = string.Empty;
        contacts.Rename(userId, contactNameDraft.Trim(), ok => renameOutcome = ok ? 1 : 2);
    }

    private void ProcessRenameOutcome()
    {
        var outcome = renameOutcome;
        if (outcome == 0)
        {
            return;
        }

        renameOutcome = 0;
        renameBusy = false;
        if (outcome == 1)
        {
            editingContactName = false;
            renameError = string.Empty;
        }
        else
        {
            renameError = Loc.T(L.Friends.RenameFailed);
        }
    }

    private void DrawContactInfoCard(ImDrawListPtr drawList, ContactDto contact)
    {
        var rowCount = 3;
        var localTime = ContactLocalTime(contact.UserId);
        if (localTime.Length > 0)
        {
            rowCount++;
        }

        var card = GroupCard.Begin(ui, rowCount, 42f);
        DrawInfoRow(drawList, card.NextRow(), Loc.T(L.Message.Number), ContactBook.Format(contact.PhoneNumber));
        DrawInfoRow(drawList, card.NextRow(), Loc.T(L.Message.Handle), "@" + contact.Handle);
        if (localTime.Length > 0)
        {
            DrawInfoRow(drawList, card.NextRow(), Loc.T(L.Message.LocalTime), localTime);
        }

        DrawInfoRow(drawList, card.NextRow(), Loc.T(L.Message.Added),
            DateTimeOffset.FromUnixTimeSeconds(contact.CreatedAtUnix).ToLocalTime().ToString("d", Loc.Culture));
        card.End();
        DrawCardGap();
    }

    private void DrawContactChatCard(ImDrawListPtr drawList, string userId)
    {
        var conversation = FindDirectConversation(userId);
        if (conversation is null)
        {
            return;
        }

        var starredCount = StarredCountIn(conversation.Id);
        var card = GroupCard.Begin(ui, 4, SettingRowHeight);
        if (DrawCardRow(drawList, card.NextRow(), PhoneIcons.Star, TintGold, Loc.T(L.Message.StarredTitle),
                starredCount > 0 ? starredCount.ToString(Loc.Culture) : string.Empty))
        {
            router.Push(MessageRoute.StarredIn(conversation.Id));
        }

        var muted = DrawCardSwitchRow(drawList, card.NextRow(), PhoneIcons.BellOff, TintRed,
            Loc.T(L.Message.MuteNotifications), conversation.Muted, "message.contact.mute");
        if (muted != conversation.Muted)
        {
            store.SetMuted(conversation.Id, muted, _ => { });
        }

        if (DrawCardRow(drawList, card.NextRow(), PhoneIcons.Wallpaper, TintTeal, Loc.T(L.Message.Wallpaper)))
        {
            router.Push(MessageRoute.ChatWallpaper(conversation.Id));
        }

        if (DrawCardRow(drawList, card.NextRow(), PhoneIcons.Lock, TintAzure, Loc.T(L.Encryption.InfoTitle)))
        {
            router.Push(MessageRoute.Encryption(conversation.Id));
        }

        card.End();
        DrawCardGap();
    }

    private ConversationDto? FindDirectConversation(string userId)
    {
        var snapshot = store.Conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (!snapshot[index].IsGroup && snapshot[index].OtherUserId == userId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private string ContactLocalTime(string userId)
    {
        var conversation = FindDirectConversation(userId);
        return conversation?.UtcOffsetMinutes is { } offsetMinutes
            ? SocialTimeZone.Describe(offsetMinutes)
            : string.Empty;
    }

    private void DrawContactNotesCard(string userId, float scale)
    {
        DrawInsetSectionLabel(Loc.T(L.Message.Notes));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = 12f * scale;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + NotesCardHeight * scale);
        ui.Card(ImGui.GetWindowDrawList(), cardMin, cardMax, Metrics.Radius.Md * scale);
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
                Typography.Plain(Loc.T(L.Message.NotesHint));
            }
        }

        if (!string.Equals(before, notesDraft, StringComparison.Ordinal))
        {
            notesSaveTimer = NotesSaveDelaySeconds;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardMax.Y - origin.Y));
        DrawCardGap();
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
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Friends.ConfirmRemove, ContactBook.DisplayLabel(contact)),
            ConfirmLabel = Loc.T(L.Friends.Remove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
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
