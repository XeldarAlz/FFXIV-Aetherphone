using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float ContactRowHeight = 64f;
    private const float ContactRowAvatarRadius = 23f;
    private const float ProfileRowHeight = 84f;
    private const float ProfileAvatarRadius = 30f;
    private const float FieldHeight = 46f;
    private const int NumberMaxLength = 16;
    private const int AliasMaxLength = 40;
    private const int ReasonMaxLength = 500;
    private const float CopiedSeconds = 1.6f;
    private const float PendingChipPadX = 10f;
    private const float PendingChipPadY = 5f;

    private readonly List<ContactDto> favoriteRows = new();
    private readonly List<ContactDto> otherRows = new();

    private string numberDraft = string.Empty;
    private string aliasDraft = string.Empty;
    private string reasonDraft = string.Empty;
    private volatile bool addBusy;
    private volatile bool requestBusy;
    private volatile int addOutcome;
    private volatile int requestOutcome;

    private void ProcessAddOutcomes()
    {
        var outcome = addOutcome;
        if (outcome != 0)
        {
            addOutcome = 0;
            if (outcome == 1)
            {
                numberDraft = string.Empty;
                aliasDraft = string.Empty;
                addError = string.Empty;
                if (router.Current == MessageRoute.AddContact)
                {
                    router.Pop();
                }
            }
            else
            {
                addError = outcome switch
                {
                    2 => Loc.T(L.Friends.InvalidNumber),
                    3 => Loc.T(L.Friends.NotFound),
                    4 => Loc.T(L.Friends.RateLimited),
                    _ => Loc.T(L.Friends.AddFailed),
                };
            }
        }

        var request = requestOutcome;
        if (request != 0)
        {
            requestOutcome = 0;
            if (request == 1)
            {
                reasonDraft = string.Empty;
            }
        }
    }

    private void DrawContactsTab(Rect area)
    {
        var scale = UiScale.Current;
        if (!session.IsSignedIn)
        {
            EmptyState.Draw(area, ui, PhoneIcons.Users, Loc.T(L.Apps.Contacts), Loc.T(L.Message.SignInPrompt));
            return;
        }

        var searchRect = new Rect(new Vector2(area.Min.X + CellPadX * scale, area.Min.Y),
            new Vector2(area.Max.X - CellPadX * scale, area.Min.Y + ChatSearchHeight * scale));
        SearchField.Draw(searchRect, "##msgContactsFilter", Loc.T(L.Phone.FilterHint), ref filter, ui.Palette);
        var listRect = new Rect(new Vector2(area.Min.X, searchRect.Max.Y), area.Max);
        using (var surface = AppSurface.BeginEdgeToEdge(listRect))
        {
            var drawList = ImGui.GetWindowDrawList();
            contactsRefresh.Draw(listRect, surface.Pull, surface.Dragging, contacts.Loading, ui.MutedInk,
                refreshContacts);
            var query = filter.Trim();
            if (query.Length == 0)
            {
                DrawMyProfileRow(drawList);
                if (DrawActionRow(drawList, PhoneIcons.UserPlus, Loc.T(L.Message.NewContact)))
                {
                    addError = string.Empty;
                    router.Push(MessageRoute.AddContact);
                }
            }

            CollectContacts(favoriteRows, otherRows);
            if (favoriteRows.Count == 0 && otherRows.Count == 0)
            {
                DrawInlineEmpty(drawList, query.Length > 0 ? Loc.T(L.Phone.NoOneFound) : Loc.T(L.Friends.EmptyHint));
            }
            else
            {
                if (favoriteRows.Count > 0)
                {
                    DrawSectionLabel(Loc.T(L.Message.Favorites));
                    for (var index = 0; index < favoriteRows.Count; index++)
                    {
                        DrawContactRow(drawList, favoriteRows[index]);
                    }
                }

                if (otherRows.Count > 0)
                {
                    DrawSectionLabel(Loc.T(L.Phone.ContactsSection));
                    var lastLetter = string.Empty;
                    for (var index = 0; index < otherRows.Count; index++)
                    {
                        var letter = TrimmedLetter(ContactBook.DisplayLabel(otherRows[index]));
                        if (!string.Equals(letter, lastLetter, StringComparison.Ordinal))
                        {
                            DrawLetterHeader(drawList, letter);
                            lastLetter = letter;
                        }

                        DrawContactRow(drawList, otherRows[index]);
                    }
                }
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawMyProfileRow(ImDrawListPtr drawList)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, ProfileRowHeight, ProfileAvatarRadius, 0f, true, out var avatarCenter);
        UiAnchors.Report("message.mynumber", row.Bounds);
        var me = session.CurrentUser;
        var name = me is null ? DisplayName : SocialIdentity.Name(me.DisplayName, me.Handle);
        AvatarView.DrawRemote(drawList, avatarCenter, ProfileAvatarRadius * scale, theme, me?.Name ?? name,
            me?.World ?? string.Empty, me?.AvatarUrl, images, lodestone, 1.2f, 40);
        var number = contacts.MyNumber;
        var display = number.Length > 0 ? ContactBook.Format(number) : "…";
        var copyCenter = new Vector2(row.Bounds.Max.X - CellPadX * scale - RowStatusGlyph * 0.5f * scale,
            row.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, copyCenter, PhoneIcons.Copy, copiedTimer > 0f ? ink.AccentLink : ink.MutedInk,
            RowStatusGlyph * 1.2f * scale);
        var textRight = copyCenter.X - RowStatusGlyph * scale;
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        var subHeight = Typography.LineHeight(RowSubStyle);
        var metaHeight = Typography.LineHeight(RowMetaStyle);
        var top = row.Bounds.Center.Y - (titleHeight + subHeight + metaHeight + RowLineGap * 2f * scale) * 0.5f;
        var width = MathF.Max(1f, textRight - row.TextLeft);
        var hovering = UiInteract.Hover(new Vector2(row.TextLeft, top), new Vector2(textRight, top + titleHeight));
        Marquee.DrawLeft(drawList, "messageapp.contacts.me", name, row.TextLeft, top, width, RowTitleStyle,
            ink.TitleInk, hovering);
        Typography.Draw(drawList, new Vector2(row.TextLeft, top + titleHeight + RowLineGap * scale),
            Typography.FitText(display, width, RowSubStyle), ink.BodyInk, RowSubStyle);
        var hint = copiedTimer > 0f ? Loc.T(L.Friends.Copied) : Loc.T(L.Message.ProfileHint);
        Typography.Draw(drawList,
            new Vector2(row.TextLeft, top + titleHeight + subHeight + RowLineGap * 2f * scale),
            Typography.FitText(hint, width, RowMetaStyle), copiedTimer > 0f ? ink.AccentLink : ink.MutedInk,
            RowMetaStyle);
        if (row.Tapped && number.Length > 0)
        {
            ImGui.SetClipboardText(display);
            copiedTimer = CopiedSeconds;
        }

        EndPersonRow(drawList, row);
    }

    private void CollectContacts(List<ContactDto> favoritesTarget, List<ContactDto> othersTarget)
    {
        favoritesTarget.Clear();
        othersTarget.Clear();
        var snapshot = contacts.Contacts;
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var contact = snapshot[index];
            if (query.Length > 0 && !MatchesContact(contact, query))
            {
                continue;
            }

            if (configuration.MessageFavoriteContacts.Contains(contact.UserId))
            {
                favoritesTarget.Add(contact);
            }
            else
            {
                othersTarget.Add(contact);
            }
        }

        favoritesTarget.Sort(CompareContactsByLabel);
        othersTarget.Sort(CompareContactsByLabel);
    }

    private static int CompareContactsByLabel(ContactDto left, ContactDto right) =>
        string.Compare(ContactBook.DisplayLabel(left), ContactBook.DisplayLabel(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool MatchesContact(ContactDto contact, string query)
    {
        return contact.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.Handle.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.PhoneNumber.Contains(query, StringComparison.Ordinal)
            || ContactBook.Format(contact.PhoneNumber).Contains(query, StringComparison.Ordinal);
    }

    private void DrawContactRow(ImDrawListPtr drawList, ContactDto contact)
    {
        var scale = UiScale.Current;
        var pendingLabel = contact.IsMutual ? string.Empty : Loc.T(L.Friends.PendingShort);
        var pendingSize = pendingLabel.Length > 0 ? Typography.Measure(pendingLabel, RowMetaStyle) : Vector2.Zero;
        var reserve = pendingLabel.Length > 0 ? pendingSize.X + PendingChipPadX * 2f * scale + RowTrailingGap * scale : 0f;
        var row = BeginPersonRow(drawList, ContactRowHeight, ContactRowAvatarRadius, reserve, true, out var avatarCenter);
        DrawContactAvatar(drawList, contact, avatarCenter, ContactRowAvatarRadius * scale);
        if (pendingLabel.Length > 0)
        {
            var chipMax = new Vector2(row.Bounds.Max.X - CellPadX * scale,
                row.Bounds.Center.Y + pendingSize.Y * 0.5f + PendingChipPadY * scale);
            var chipMin = new Vector2(chipMax.X - pendingSize.X - PendingChipPadX * 2f * scale,
                row.Bounds.Center.Y - pendingSize.Y * 0.5f - PendingChipPadY * scale);
            Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f, ImGui.GetColorU32(ink.ChipFill));
            Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, pendingLabel, ink.MutedInk, RowMetaStyle);
        }

        DrawRowTitleAndSub(drawList, new MarqueeId("messageapp.contacts.name.", contact.UserId),
            ContactBook.DisplayLabel(contact), ContactBook.Format(contact.PhoneNumber), row.TextLeft, row.TextRight,
            row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        if (row.Tapped)
        {
            router.Push(MessageRoute.Contact(contact.UserId));
        }

        EndPersonRow(drawList, row);
    }

    private void DrawAddContact(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Message.NewContact));
        var sideInset = CellPadX * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var fieldHeight = FieldHeight * scale;
        var numberRect = new Rect(new Vector2(area.Min.X + sideInset, top),
            new Vector2(area.Max.X - sideInset, top + fieldHeight));
        var submitted = PillField(numberRect, "##msgNumber", Loc.T(L.Friends.NumberHint), ref numberDraft,
            NumberMaxLength);
        var aliasTop = numberRect.Max.Y + 12f * scale;
        var aliasRect = new Rect(new Vector2(area.Min.X + sideInset, aliasTop),
            new Vector2(area.Max.X - sideInset, aliasTop + fieldHeight));
        submitted |= PillField(aliasRect, "##msgAlias", Loc.T(L.Friends.NameHint), ref aliasDraft, AliasMaxLength);

        var afterFields = aliasRect.Max.Y + 14f * scale;
        if (addError.Length > 0)
        {
            afterFields += Typography.DrawWrappedLeft(new Vector2(aliasRect.Min.X + 4f * scale, afterFields), addError,
                theme.Danger, TextStyles.Callout, aliasRect.Width - 8f * scale) + 10f * scale;
        }

        var hintTop = afterFields;
        afterFields += Typography.DrawWrappedLeft(new Vector2(aliasRect.Min.X + 4f * scale, hintTop),
            Loc.T(L.Friends.EmptyHint), ink.MutedInk, TextStyles.Footnote, aliasRect.Width - 8f * scale);
        var buttonTop = afterFields + 22f * scale;
        var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
            new Vector2(area.Max.X - sideInset, buttonTop + fieldHeight));
        var canAdd = !addBusy && numberDraft.Trim().Length > 0;
        if (ui.PillButton(buttonRect, addBusy ? Loc.T(L.Friends.Adding) : Loc.T(L.Friends.Add), true) && canAdd)
        {
            SubmitAddContact();
        }

        if (submitted && canAdd)
        {
            SubmitAddContact();
        }
    }

    private void SubmitAddContact()
    {
        var number = numberDraft.Trim();
        var alias = aliasDraft.Trim();
        addBusy = true;
        addError = string.Empty;
        contacts.Add(number, alias.Length > 0 ? alias : null, (outcome, _) =>
        {
            addBusy = false;
            addOutcome = outcome switch
            {
                AddContactOutcome.Added => 1,
                AddContactOutcome.InvalidNumber => 2,
                AddContactOutcome.NotFound => 3,
                AddContactOutcome.RateLimited => 4,
                _ => 5,
            };
        });
    }

    private void DrawSafety(Rect area)
    {
        var scale = UiScale.Current;
        DrawScreenHeader(area, Loc.T(L.Friends.NewNumberTitle));
        var sideInset = CellPadX * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var textWidth = area.Width - sideInset * 2f;
        var afterBody = top + Typography.DrawWrappedLeft(new Vector2(area.Min.X + sideInset, top),
            Loc.T(L.Friends.NewNumberBody), ui.BodyInk, TextStyles.Callout, textWidth) + 18f * scale;

        var request = contacts.NumberChange;
        if (request is not null && request.Status == "pending")
        {
            Typography.DrawWrappedLeft(new Vector2(area.Min.X + sideInset, afterBody), Loc.T(L.Friends.RequestPending),
                ui.Accent, TextStyles.SubheadlineEmphasized, textWidth);
            return;
        }

        if (request is not null && request.Status == "approved")
        {
            afterBody += Typography.DrawWrappedLeft(new Vector2(area.Min.X + sideInset, afterBody),
                Loc.T(L.Friends.RequestApproved), ui.Accent, TextStyles.Subheadline, textWidth) + 14f * scale;
        }
        else if (request is not null && request.Status == "denied")
        {
            afterBody += Typography.DrawWrappedLeft(new Vector2(area.Min.X + sideInset, afterBody),
                Loc.T(L.Friends.RequestDenied), ui.MutedInk, TextStyles.Subheadline, textWidth) + 14f * scale;
        }

        var drawList = ImGui.GetWindowDrawList();
        var fieldMin = new Vector2(area.Min.X + sideInset, afterBody);
        var fieldMax = new Vector2(area.Max.X - sideInset, afterBody + 118f * scale);
        ui.Card(drawList, fieldMin, fieldMax, Metrics.Radius.Md * scale);
        var pad = 12f * scale;
        ImGui.SetCursorScreenPos(fieldMin + new Vector2(pad, pad));
        var inputWidth = fieldMax.X - fieldMin.X - pad * 2f;
        var wrapWidth = inputWidth - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (Plugin.Fonts.Push(1.05f))
        {
            SoftWrapField.Multiline("##msgNumberReason", ref reasonDraft, ReasonMaxLength,
                new Vector2(inputWidth, fieldMax.Y - fieldMin.Y - pad * 2f), wrapWidth);
        }

        if (reasonDraft.Length == 0)
        {
            ImGui.SetCursorScreenPos(fieldMin + new Vector2(pad + 4f * scale, pad + 2f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
            using (Plugin.Fonts.Push(1.05f))
            {
                Typography.Plain(Loc.T(L.Friends.ReasonHint));
            }
        }

        var buttonTop = fieldMax.Y + 18f * scale;
        var buttonRect = new Rect(new Vector2(area.Min.X + sideInset, buttonTop),
            new Vector2(area.Max.X - sideInset, buttonTop + FieldHeight * scale));
        var canSend = !requestBusy && !string.IsNullOrWhiteSpace(reasonDraft);
        if (ui.PillButton(buttonRect, requestBusy ? Loc.T(L.Friends.Sending) : Loc.T(L.Friends.SendRequest), true)
            && canSend)
        {
            requestBusy = true;
            contacts.RequestNumberChange(reasonDraft.Trim(), ok =>
            {
                requestBusy = false;
                requestOutcome = ok ? 1 : 2;
            });
        }
    }

    private void StartCall(ContactDto contact)
    {
        calls.StartCall(new CallContact(contact.UserId, string.Empty, string.Empty,
            ContactBook.DisplayLabel(contact)));
        ShowCallScreen();
    }

    private void StartMessage(ContactDto contact)
    {
        if (composeBusy)
        {
            return;
        }

        composeBusy = true;
        store.CreateDirect(contact.UserId, id =>
        {
            composeBusy = false;
            if (!string.IsNullOrEmpty(id))
            {
                composeResult = id;
            }
        });
    }
}
