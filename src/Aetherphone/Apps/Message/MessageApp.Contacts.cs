using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Telephony;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float ContactRowHeight = 58f;
    private const float FieldHeight = 46f;
    private const int NumberMaxLength = 16;
    private const int AliasMaxLength = 40;
    private const int ReasonMaxLength = 500;
    private const float CopiedSeconds = 1.6f;

    private string numberDraft = string.Empty;
    private string aliasDraft = string.Empty;
    private string reasonDraft = string.Empty;
    private string addError = string.Empty;
    private float copiedTimer;
    private volatile bool addBusy;
    private volatile bool requestBusy;
    private volatile int addOutcome;
    private volatile int requestOutcome;
    private volatile bool removePending;

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
        var scale = ImGuiHelpers.GlobalScale;
        if (!session.IsSignedIn)
        {
            Typography.DrawCentered(area.Center, Loc.T(L.Message.SignInPrompt), ui.MutedInk);
            return;
        }

        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)),
            "##msgContactsFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Message);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight), area.Max);
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawMyNumberCard(scale);
            var favorites = new List<ContactDto>();
            var others = new List<ContactDto>();
            CollectContacts(favorites, others);
            if (favorites.Count == 0 && others.Count == 0)
            {
                var emptyCenter = new Vector2(listRect.Center.X, listRect.Min.Y + 150f * scale);
                Typography.DrawCentered(emptyCenter,
                    filter.Trim().Length > 0 ? Loc.T(L.Phone.NoOneFound) : Loc.T(L.Friends.Empty), ui.MutedInk);
                if (filter.Trim().Length == 0)
                {
                    Typography.DrawCentered(emptyCenter + new Vector2(0f, 26f * scale), Loc.T(L.Friends.EmptyHint),
                        ui.MutedInk, TextStyles.Subheadline);
                }

                ImGui.Dummy(new Vector2(0f, 200f * scale));
            }
            else
            {
                if (favorites.Count > 0)
                {
                    ui.SectionLabel(Loc.T(L.Message.Favorites));
                    for (var index = 0; index < favorites.Count; index++)
                    {
                        DrawContactRow(favorites[index], scale);
                    }
                }

                if (others.Count > 0)
                {
                    if (favorites.Count > 0)
                    {
                        ui.SectionLabel(Loc.T(L.Phone.ContactsSection));
                    }

                    for (var index = 0; index < others.Count; index++)
                    {
                        DrawContactRow(others[index], scale);
                    }
                }

                ImGui.Dummy(new Vector2(0f, 72f * scale));
            }
        }

        if (ComposeFab.Draw(listRect, "##msgAddFab", ui.Accent, FontAwesomeIcon.UserPlus.ToIconString(),
                Loc.T(L.Friends.AddFriend)))
        {
            addError = string.Empty;
            router.Push(MessageRoute.AddContact);
        }
    }

    private void DrawMyNumberCard(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = 16f * scale;
        var cardHeight = 104f * scale;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        UiAnchors.Report("message.mynumber", new Rect(origin, cardMax));
        ui.Card(drawList, origin, cardMax, 18f * scale, elevated: true);
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + 14f * scale), Loc.T(L.Friends.MyNumber),
            ui.HeaderInk, TextStyles.FootnoteEmphasized);
        var number = contacts.MyNumber;
        var display = number.Length > 0 ? ContactBook.Format(number) : "…";
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + 34f * scale), display, ui.TitleInk, TextStyles.Title1);
        var hint = copiedTimer > 0f ? Loc.T(L.Friends.Copied) : Loc.T(L.Friends.ShareHint);
        Typography.Draw(new Vector2(origin.X + pad, cardMax.Y - 26f * scale), hint,
            copiedTimer > 0f ? ui.Accent : ui.MutedInk, TextStyles.Footnote);
        if (number.Length > 0)
        {
            AppSkin.Icon(new Vector2(cardMax.X - 24f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.Copy.ToIconString(), ui.MutedInk, 1f);
            if (UiInteract.HoverClick(origin, cardMax))
            {
                ImGui.SetClipboardText(display);
                copiedTimer = CopiedSeconds;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 12f * scale));
    }

    private void CollectContacts(List<ContactDto> favoritesTarget, List<ContactDto> othersTarget)
    {
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

    private void DrawContactRow(ContactDto contact, float scale)
    {
        var rowHeight = ContactRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), 16f * scale);
        var pad = 12f * scale;
        var radius = 20f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, ContactBook.DisplayLabel(contact), string.Empty,
            contact.AvatarUrl, images, lodestone, 0.95f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale), ContactBook.DisplayLabel(contact),
            theme.TextStrong, 1f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale), ContactBook.Format(contact.PhoneNumber),
            ui.MutedInk, 0.85f);

        float actionLeft;
        if (contact.IsMutual)
        {
            var callCenter = new Vector2(origin.X + width - pad - 16f * scale, origin.Y + rowHeight * 0.5f);
            if (ui.IconButton(callCenter, 16f * scale, FontAwesomeIcon.Phone.ToIconString(), White,
                    CallGreen, 0.85f, Loc.T(L.Friends.Call), HoverLabelSide.Above))
            {
                StartCall(contact);
                return;
            }

            var messageCenter = new Vector2(callCenter.X - 44f * scale, callCenter.Y);
            if (ui.IconButton(messageCenter, 16f * scale, FontAwesomeIcon.Comment.ToIconString(), White,
                    ui.Accent, 0.8f, Loc.T(L.DirectMessages.StartChat), HoverLabelSide.Above))
            {
                StartMessage(contact);
                return;
            }

            actionLeft = messageCenter.X - 22f * scale;
        }
        else
        {
            var label = Loc.T(L.Friends.PendingShort);
            var labelSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
            var chipPad = 10f * scale;
            var chipMin = new Vector2(origin.X + width - pad - labelSize.X - chipPad * 2f,
                origin.Y + rowHeight * 0.5f - labelSize.Y * 0.5f - 5f * scale);
            var chipMax = new Vector2(origin.X + width - pad, origin.Y + rowHeight * 0.5f + labelSize.Y * 0.5f + 5f * scale);
            Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
                ImGui.GetColorU32(ui.FieldSurface));
            Typography.DrawCentered((chipMin + chipMax) * 0.5f, label, ui.MutedInk, TextStyles.FootnoteEmphasized);
            actionLeft = chipMin.X - 6f * scale;
        }

        if (UiInteract.HoverClick(origin, new Vector2(actionLeft, origin.Y + rowHeight)))
        {
            router.Push(MessageRoute.Contact(contact.UserId));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawAddContact(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Friends.AddFriend), back);
        var sideInset = 16f * scale;
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
            Typography.Draw(new Vector2(aliasRect.Min.X + 4f * scale, afterFields), addError, theme.Danger,
                TextStyles.Callout);
            afterFields += 28f * scale;
        }

        var buttonTop = afterFields + 16f * scale;
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
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Friends.NewNumberTitle), back);
        var sideInset = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var textWidth = area.Width - sideInset * 2f;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + sideInset, top));
        ImGui.PushTextWrapPos(area.Min.X + sideInset + textWidth - ImGui.GetWindowPos().X);
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.BodyInk))
        {
            ImGui.TextWrapped(Loc.T(L.Friends.NewNumberBody));
        }

        ImGui.PopTextWrapPos();
        var afterBody = ImGui.GetItemRectMax().Y + 18f * scale;

        var request = contacts.NumberChange;
        if (request is not null && request.Status == "pending")
        {
            Typography.Draw(new Vector2(area.Min.X + sideInset, afterBody), Loc.T(L.Friends.RequestPending),
                ui.Accent, TextStyles.SubheadlineEmphasized);
            return;
        }

        if (request is not null && request.Status == "approved")
        {
            Typography.Draw(new Vector2(area.Min.X + sideInset, afterBody), Loc.T(L.Friends.RequestApproved),
                ui.Accent, TextStyles.Subheadline);
            afterBody += 32f * scale;
        }
        else if (request is not null && request.Status == "denied")
        {
            Typography.Draw(new Vector2(area.Min.X + sideInset, afterBody), Loc.T(L.Friends.RequestDenied),
                ui.MutedInk, TextStyles.Subheadline);
            afterBody += 32f * scale;
        }

        var drawList = ImGui.GetWindowDrawList();
        var fieldMin = new Vector2(area.Min.X + sideInset, afterBody);
        var fieldMax = new Vector2(area.Max.X - sideInset, afterBody + 118f * scale);
        ui.Card(drawList, fieldMin, fieldMax, 14f * scale);
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
                ImGui.TextUnformatted(Loc.T(L.Friends.ReasonHint));
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
        router.Reset();
        activeTab = MessageTab.Calls;
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
