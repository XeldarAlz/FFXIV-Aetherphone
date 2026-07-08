using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Friends;

internal sealed class FriendsApp : IPhoneApp
{
    private enum FriendsRoute : byte
    {
        List,
        Add,
        Detail,
        Safety,
    }

    private const float RowHeight = 58f;
    private const float FieldHeight = 46f;
    private const int NumberMaxLength = 16;
    private const int AliasMaxLength = 40;
    private const int ReasonMaxLength = 500;
    private const float CopiedSeconds = 1.6f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public string Id => "friends";
    public string DisplayName => Loc.T(L.Apps.Friends);
    public string Glyph => "Fr";
    public int BadgeCount => 0;

    private readonly ContactBook contacts;
    private readonly CallHub calls;
    private readonly AethernetSession session;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly DmLauncher dm;
    private readonly AppSkin ui = new(AppPalettes.Friends);
    private readonly ViewRouter<FriendsRoute> router;
    private readonly RouterDraw<FriendsRoute> drawView;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private string filter = string.Empty;
    private string numberDraft = string.Empty;
    private string aliasDraft = string.Empty;
    private string reasonDraft = string.Empty;
    private string addError = string.Empty;
    private string? selectedUserId;
    private float copiedTimer;
    private volatile bool addBusy;
    private volatile bool requestBusy;
    private volatile int addOutcome;
    private volatile int requestOutcome;
    private volatile bool removePending;

    public FriendsApp(ContactBook contacts, CallHub calls, AethernetSession session, RemoteImageCache images,
        LodestoneService lodestone, DmLauncher dm)
    {
        this.contacts = contacts;
        this.calls = calls;
        this.session = session;
        this.images = images;
        this.lodestone = lodestone;
        this.dm = dm;
        router = new ViewRouter<FriendsRoute>(FriendsRoute.List, Id);
        drawView = DrawView;
    }

    public void OnOpened()
    {
        router.Reset();
        filter = string.Empty;
        addError = string.Empty;
        selectedUserId = null;
        contacts.Refresh(force: true);
    }

    public void OnClosed()
    {
        router.Reset();
        filter = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;
        var delta = ImGui.GetIO().DeltaTime;
        if (copiedTimer > 0f)
        {
            copiedTimer = MathF.Max(0f, copiedTimer - delta);
        }

        ProcessOutcomes();

        var screen = SceneChrome.ScreenFrom(context.Content, theme, ImGuiHelpers.GlobalScale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
    }

    private void ProcessOutcomes()
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
                if (router.Current == FriendsRoute.Add)
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

        if (removePending)
        {
            removePending = false;
            selectedUserId = null;
            if (router.Current == FriendsRoute.Detail)
            {
                router.Pop();
            }
        }
    }

    private void DrawView(FriendsRoute route, Rect area, int depth)
    {
        ui.Body(area);
        switch (route)
        {
            case FriendsRoute.Add:
                DrawAdd(area);
                break;
            case FriendsRoute.Detail:
                DrawDetail(area);
                break;
            case FriendsRoute.Safety:
                DrawSafety(area);
                break;
            default:
                DrawList(area);
                break;
        }
    }

    private void DrawList(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawListTopBar(area, scale);
        var top = area.Min.Y + AppHeader.Height * scale;
        if (!session.IsSignedIn)
        {
            var body = new Rect(new Vector2(area.Min.X, top), area.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Friends.SignInPrompt), AppPalettes.Friends.MutedInk);
            return;
        }

        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##friendsFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Friends);
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawMyNumberCard(scale);
            var entries = Filtered();
            if (entries.Count == 0)
            {
                var emptyCenter = new Vector2(listRect.Center.X, listRect.Min.Y + 150f * scale);
                Typography.DrawCentered(emptyCenter,
                    filter.Trim().Length > 0 ? Loc.T(L.Phone.NoOneFound) : Loc.T(L.Friends.Empty),
                    AppPalettes.Friends.MutedInk);
                if (filter.Trim().Length == 0)
                {
                    Typography.DrawCentered(emptyCenter + new Vector2(0f, 26f * scale), Loc.T(L.Friends.EmptyHint),
                        AppPalettes.Friends.MutedInk, TextStyles.Subheadline);
                }

                ImGui.Dummy(new Vector2(0f, 200f * scale));
            }
            else
            {
                for (var index = 0; index < entries.Count; index++)
                {
                    DrawContactRow(entries[index], scale);
                }

                ImGui.Dummy(new Vector2(0f, 72f * scale));
            }
        }

        if (ComposeFab.Draw(listRect, "##friendsAddFab", ui.Accent, FontAwesomeIcon.UserPlus.ToIconString(),
                Loc.T(L.Friends.AddFriend)))
        {
            addError = string.Empty;
            router.Push(FriendsRoute.Add);
        }
    }

    private void DrawListTopBar(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), DisplayName, AppPalettes.Friends.TitleInk,
            1.3f, FontWeight.Bold);
        var shieldCenter = new Vector2(area.Max.X - 24f * scale, rowCenterY);
        if (ui.IconButton(shieldCenter, 16f * scale, FontAwesomeIcon.ShieldAlt.ToIconString(),
                AppPalettes.Friends.BodyInk, Transparent, 1.2f, Loc.T(L.Friends.NewNumberTitle),
                HoverLabelSide.Below) && session.IsSignedIn)
        {
            router.Push(FriendsRoute.Safety);
        }

        var request = contacts.NumberChange;
        if (request is not null && request.Status == "pending")
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddCircleFilled(shieldCenter + new Vector2(10f * scale, -10f * scale), 4f * scale,
                ImGui.GetColorU32(ui.Accent), 16);
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
        UiAnchors.Report("friends.mynumber", new Rect(origin, cardMax));
        ui.Card(drawList, origin, cardMax, 18f * scale, elevated: true);
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + 14f * scale), Loc.T(L.Friends.MyNumber),
            AppPalettes.Friends.HeaderInk, TextStyles.FootnoteEmphasized);
        var number = contacts.MyNumber;
        var display = number.Length > 0 ? ContactBook.Format(number) : "…";
        Typography.Draw(new Vector2(origin.X + pad, origin.Y + 34f * scale), display, AppPalettes.Friends.TitleInk,
            TextStyles.Title1);
        var hint = copiedTimer > 0f ? Loc.T(L.Friends.Copied) : Loc.T(L.Friends.ShareHint);
        Typography.Draw(new Vector2(origin.X + pad, cardMax.Y - 26f * scale), hint,
            copiedTimer > 0f ? ui.Accent : AppPalettes.Friends.MutedInk, TextStyles.Footnote);
        if (number.Length > 0)
        {
            AppSkin.Icon(new Vector2(cardMax.X - 24f * scale, origin.Y + cardHeight * 0.5f),
                FontAwesomeIcon.Copy.ToIconString(), AppPalettes.Friends.MutedInk, 1f);
            if (UiInteract.HoverClick(origin, cardMax))
            {
                ImGui.SetClipboardText(display);
                copiedTimer = CopiedSeconds;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + 12f * scale));
    }

    private List<ContactDto> Filtered()
    {
        var snapshot = contacts.Contacts;
        var list = new List<ContactDto>(snapshot.Length);
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (query.Length == 0 || Matches(snapshot[index], query))
            {
                list.Add(snapshot[index]);
            }
        }

        list.Sort(static (left, right) => string.Compare(ContactBook.DisplayLabel(left),
            ContactBook.DisplayLabel(right), StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static bool Matches(ContactDto contact, string query)
    {
        return contact.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.Handle.Contains(query, StringComparison.OrdinalIgnoreCase)
            || contact.PhoneNumber.Contains(query, StringComparison.Ordinal)
            || ContactBook.Format(contact.PhoneNumber).Contains(query, StringComparison.Ordinal);
    }

    private void DrawContactRow(ContactDto contact, float scale)
    {
        var rowHeight = RowHeight * scale;
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
            AppPalettes.Friends.MutedInk, 0.85f);

        float actionLeft;
        if (contact.IsMutual)
        {
            var callCenter = new Vector2(origin.X + width - pad - 16f * scale, origin.Y + rowHeight * 0.5f);
            if (ui.IconButton(callCenter, 16f * scale, FontAwesomeIcon.Phone.ToIconString(), White,
                    AppAccents.For("phone"), 0.85f, Loc.T(L.Friends.Call), HoverLabelSide.Above))
            {
                StartCall(contact);
                return;
            }

            var messageCenter = new Vector2(callCenter.X - 44f * scale, callCenter.Y);
            if (ui.IconButton(messageCenter, 16f * scale, FontAwesomeIcon.Comment.ToIconString(), White,
                    AppAccents.For("dm"), 0.8f, Loc.T(L.DirectMessages.StartChat), HoverLabelSide.Above))
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
                ImGui.GetColorU32(AppPalettes.Friends.FieldSurface));
            Typography.DrawCentered((chipMin + chipMax) * 0.5f, label, AppPalettes.Friends.MutedInk,
                TextStyles.FootnoteEmphasized);
            actionLeft = chipMin.X - 6f * scale;
        }

        if (UiInteract.HoverClick(origin, new Vector2(actionLeft, origin.Y + rowHeight)))
        {
            selectedUserId = contact.UserId;
            router.Push(FriendsRoute.Detail);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawAdd(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Friends.AddFriend), Back);
        var sideInset = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var fieldHeight = FieldHeight * scale;
        var numberRect = new Rect(new Vector2(area.Min.X + sideInset, top),
            new Vector2(area.Max.X - sideInset, top + fieldHeight));
        var submitted = PillField(numberRect, "##friendNumber", Loc.T(L.Friends.NumberHint), ref numberDraft,
            NumberMaxLength);
        var aliasTop = numberRect.Max.Y + 12f * scale;
        var aliasRect = new Rect(new Vector2(area.Min.X + sideInset, aliasTop),
            new Vector2(area.Max.X - sideInset, aliasTop + fieldHeight));
        submitted |= PillField(aliasRect, "##friendAlias", Loc.T(L.Friends.NameHint), ref aliasDraft, AliasMaxLength);

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
            SubmitAdd();
        }

        if (submitted && canAdd)
        {
            SubmitAdd();
        }
    }

    private void SubmitAdd()
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

    private void DrawDetail(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        var contact = selectedUserId is null ? null : contacts.Find(selectedUserId);
        AppHeader.Draw(context, contact is null ? DisplayName : ContactBook.DisplayLabel(contact), Back);
        if (contact is null)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var centerX = area.Center.X;
        var radius = 44f * scale;
        var avatarCenter = new Vector2(centerX, area.Min.Y + AppHeader.Height * scale + 30f * scale + radius);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, ContactBook.DisplayLabel(contact), string.Empty,
            contact.AvatarUrl, images, lodestone, 1.8f, 48);
        var nameY = avatarCenter.Y + radius + 20f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), ContactBook.DisplayLabel(contact),
            AppPalettes.Friends.TitleInk, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(centerX, nameY + 30f * scale), "@" + contact.Handle,
            AppPalettes.Friends.MutedInk, TextStyles.Subheadline);
        Typography.DrawCentered(new Vector2(centerX, nameY + 58f * scale),
            ContactBook.Format(contact.PhoneNumber), AppPalettes.Friends.BodyInk, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(centerX, nameY + 88f * scale),
            contact.IsMutual ? Loc.T(L.Friends.CanCall) : Loc.T(L.Friends.Pending),
            contact.IsMutual ? ui.Accent : AppPalettes.Friends.MutedInk, TextStyles.Subheadline);

        var sideInset = 24f * scale;
        var buttonHeight = FieldHeight * scale;
        var actionTop = nameY + 116f * scale;
        if (contact.IsMutual)
        {
            var gap = 10f * scale;
            var half = (area.Width - sideInset * 2f - gap) * 0.5f;
            var messageRect = new Rect(new Vector2(area.Min.X + sideInset, actionTop),
                new Vector2(area.Min.X + sideInset + half, actionTop + buttonHeight));
            var callRect = new Rect(new Vector2(area.Max.X - sideInset - half, actionTop),
                new Vector2(area.Max.X - sideInset, actionTop + buttonHeight));
            if (ui.PillButton(messageRect, Loc.T(L.DirectMessages.StartChat), true))
            {
                StartMessage(contact);
                return;
            }

            if (ui.PillButton(callRect, Loc.T(L.Friends.Call), true))
            {
                StartCall(contact);
                return;
            }
        }

        var removeTop = contact.IsMutual ? actionTop + buttonHeight + 12f * scale : actionTop;
        var removeRect = new Rect(new Vector2(area.Min.X + sideInset, removeTop),
            new Vector2(area.Max.X - sideInset, removeTop + buttonHeight));
        if (ui.DangerGhostButton(removeRect, Loc.T(L.Friends.Remove)))
        {
            AskRemove(contact);
        }
    }

    private void AskRemove(ContactDto contact)
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

    private void DrawSafety(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Friends.NewNumberTitle), Back);
        var sideInset = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale + 12f * scale;
        var textWidth = area.Width - sideInset * 2f;
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + sideInset, top));
        ImGui.PushTextWrapPos(area.Min.X + sideInset + textWidth - ImGui.GetWindowPos().X);
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Friends.BodyInk))
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
                AppPalettes.Friends.MutedInk, TextStyles.Subheadline);
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
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Friends.TitleInk))
        using (Plugin.Fonts.Push(1.05f))
        {
            SoftWrapField.Multiline("##numberReason", ref reasonDraft, ReasonMaxLength,
                new Vector2(inputWidth, fieldMax.Y - fieldMin.Y - pad * 2f), wrapWidth);
        }

        if (reasonDraft.Length == 0)
        {
            ImGui.SetCursorScreenPos(fieldMin + new Vector2(pad + 4f * scale, pad + 2f * scale));
            using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Friends.MutedInk))
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

    private bool PillField(Rect rect, string imguiId, string hint, ref string value, int maxLength)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, (rect.Max.Y - rect.Min.Y) * 0.5f,
            ImGui.GetColorU32(AppPalettes.Friends.FieldSurface));
        ImGui.SetNextItemWidth(rect.Width - 36f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, AppPalettes.Friends.TitleInk))
        using (Plugin.Fonts.Push(1.05f))
        {
            ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + 18f * scale,
                rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
            return ImGui.InputTextWithHint(imguiId, hint, ref value, maxLength,
                ImGuiInputTextFlags.EnterReturnsTrue);
        }
    }

    private void StartCall(ContactDto contact)
    {
        calls.StartCall(new CallContact(contact.UserId, string.Empty, string.Empty,
            ContactBook.DisplayLabel(contact)));
        navigation.Open("phone", "friends");
    }

    private void StartMessage(ContactDto contact)
    {
        dm.RequestUser(contact.UserId);
        navigation.Open("dm");
    }

    private void Back() => router.Pop();

    public void Dispose()
    {
        contacts.Dispose();
    }
}
