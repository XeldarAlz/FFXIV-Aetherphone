using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal enum MemberState : byte
{
    Seat,
    InRoom,
    Leaving,
    Away,
    CoDealer,
    CoDealerAway,
}

internal enum MemberAction : byte
{
    ToggleCoDealer,
    Remove,
}

internal readonly record struct DoorMember(string UserId, string Name, MemberState State, int SeatIndex,
    bool Watching, bool Removable)
{
    public bool CoDealer => State is MemberState.CoDealer or MemberState.CoDealerAway;

    public bool Leaving => State == MemberState.Leaving;
}

internal sealed partial class TableDoor
{
    private const int MenuCapacity = 2;
    private const float RingWidth = 2f;
    private const float EmptyIcon = 44f;
    private const float EmptyPad = 18f;
    private const float EmptyGap = 8f;
    private const float EmptyGlowAlpha = 0.16f;
    private const float MenuGlyph = 16f;

    private readonly List<DoorMember> members = new();
    private readonly List<string> memberLines = new();
    private readonly ActionSheet menu = new();
    private readonly ActionSheet.Item[] menuItems = new ActionSheet.Item[MenuCapacity];
    private readonly MemberAction[] menuActions = new MemberAction[MenuCapacity];

    private CasinoTableDoorDto? membersDoor;
    private CasinoTableRowDto? membersCard;
    private LanguageInfo? membersLanguage;
    private DoorMember menuTarget;

    internal static bool HasSeats(CasinoTableRowDto? card)
    {
        return card is null
            || (VenueKinds.Of(card.GameKind) == VenueRoomKind.None && !CasinoBookRules.IsBook(card.GameKind));
    }

    internal static bool TakesCoDealers(CasinoTableRowDto card)
    {
        return !string.Equals(card.GameKind, HoldemRules.Kind, StringComparison.Ordinal)
            && !CasinoBookRules.IsBook(card.GameKind);
    }

    internal static bool OffersCoDealers(CasinoTableRowDto card) =>
        TakesCoDealers(card) && CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));

    internal static int WithoutCoDealers(DoorControl[] controls, int count)
    {
        var write = 0;
        for (var index = 0; index < count; index++)
        {
            if (controls[index] != DoorControl.CoDealers)
            {
                controls[write++] = controls[index];
            }
        }

        return write;
    }

    internal static bool Removable(string userId, CasinoTableRowDto? card)
    {
        return card is null || !string.Equals(card.OwnerUserId, userId, StringComparison.Ordinal);
    }

    internal static bool IsCoDealer(CasinoTableConfigDto config, string userId)
    {
        var coDealers = config.CoDealers;
        if (coDealers is null)
        {
            return false;
        }

        for (var index = 0; index < coDealers.Length; index++)
        {
            if (string.Equals(coDealers[index], userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string[] Toggled(string[]? coDealers, string userId)
    {
        var current = coDealers ?? Array.Empty<string>();
        var present = false;
        for (var index = 0; index < current.Length; index++)
        {
            present |= string.Equals(current[index], userId, StringComparison.Ordinal);
        }

        var next = new string[present ? current.Length - 1 : current.Length + 1];
        var write = 0;
        for (var index = 0; index < current.Length; index++)
        {
            if (!string.Equals(current[index], userId, StringComparison.Ordinal))
            {
                next[write++] = current[index];
            }
        }

        if (!present)
        {
            next[write] = userId;
        }

        return next;
    }

    internal static bool CoDealersFull(CasinoTableConfigDto? config) =>
        (config?.CoDealers?.Length ?? 0) >= CasinoHostingRules.MaxCoDealers;

    internal static void Merge(CasinoTableDoorDto? door, CasinoTableRowDto? card, List<DoorMember> into)
    {
        into.Clear();
        var config = card?.Config;
        var seated = door?.Seated;
        if (seated is not null)
        {
            for (var index = 0; index < seated.Length; index++)
            {
                var occupant = seated[index];
                var coDealer = config is not null && IsCoDealer(config, occupant.UserId);
                var state = occupant.Leaving ? MemberState.Leaving : coDealer ? MemberState.CoDealer : MemberState.Seat;
                into.Add(new DoorMember(occupant.UserId, occupant.DisplayName, state, occupant.SeatIndex, false,
                    Removable(occupant.UserId, card)));
            }
        }

        var present = door?.Present;
        if (present is null)
        {
            return;
        }

        for (var index = 0; index < present.Length; index++)
        {
            var member = present[index];
            if (!Removable(member.UserId, card) || Contains(into, member.UserId))
            {
                continue;
            }

            var coDealer = config is not null && IsCoDealer(config, member.UserId);
            var state = member.Attached
                ? coDealer ? MemberState.CoDealer : MemberState.InRoom
                : coDealer ? MemberState.CoDealerAway : MemberState.Away;
            into.Add(new DoorMember(member.UserId, member.DisplayName, state, -1, true, true));
        }
    }

    internal static int MenuFor(in DoorMember member, CasinoTableRowDto card, MemberAction[] into)
    {
        if (!member.Removable)
        {
            return 0;
        }

        var count = 0;
        if (OffersCoDealers(card) && card.Config is not null)
        {
            into[count++] = MemberAction.ToggleCoDealer;
        }

        if (!member.Leaving)
        {
            into[count++] = MemberAction.Remove;
        }

        return count;
    }

    private static bool Contains(List<DoorMember> list, string userId)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void Gate() => menu.Gate();

    public void DrawOverlay(Rect screen, AppSkin ui, bool active)
    {
        if (!active)
        {
            if (menu.IsOpen)
            {
                menu.Close();
            }

            return;
        }

        if (!menu.IsOpen && !menu.CapturesPointer)
        {
            return;
        }

        var card = tables.CardFor(roomId);
        var count = card is null ? 0 : MenuFor(menuTarget, card, menuActions);
        for (var index = 0; index < count; index++)
        {
            menuItems[index] = menuActions[index] == MemberAction.Remove
                ? new ActionSheet.Item(Loc.T(L.HostDesk.RemoveFromTable), Danger: true)
                : new ActionSheet.Item(Loc.T(menuTarget.CoDealer ? L.HostDesk.RemoveCoDealer : L.HostDesk.MakeCoDealer));
        }

        var picked = menu.Draw(screen, ActionSheetStyle.From(ui), menuItems.AsSpan(0, count), Loc.T(L.Common.Cancel),
            false, menuTarget.Name);
        if (picked < 0 || card is null)
        {
            return;
        }

        if (menuActions[picked] == MemberAction.Remove)
        {
            AskRemove(menuTarget.UserId, menuTarget.Name, menuTarget.Watching);
            return;
        }

        ToggleCoDealer(card, menuTarget);
    }

    private void ToggleCoDealer(CasinoTableRowDto card, in DoorMember member)
    {
        var config = card.Config;
        if (config is null || tables.IntentInFlight)
        {
            return;
        }

        if (!member.CoDealer && CoDealersFull(config))
        {
            ShellToast.Show(texts.Count(L.HostDesk.CoDealersFull, CasinoHostingRules.MaxCoDealers));
            return;
        }

        inlineReason = string.Empty;
        tables.SetCoDealers(roomId, Toggled(config.CoDealers, member.UserId));
    }

    private void RefreshMembers(CasinoTableDoorDto? door, CasinoTableRowDto card)
    {
        if (ReferenceEquals(door, membersDoor) && ReferenceEquals(card, membersCard)
            && ReferenceEquals(membersLanguage, Loc.Current))
        {
            return;
        }

        membersDoor = door;
        membersCard = card;
        membersLanguage = Loc.Current;
        Merge(door, card, members);
        memberLines.Clear();
        for (var index = 0; index < members.Count; index++)
        {
            memberLines.Add(LineOf(members[index]));
        }
    }

    private string LineOf(in DoorMember member) => member.State switch
    {
        MemberState.Seat => texts.Count(L.Tables.SeatLabel, member.SeatIndex + 1),
        MemberState.Leaving => Loc.T(L.Casino.StandQueued),
        MemberState.Away => Loc.T(L.HostDesk.Away),
        MemberState.CoDealer => Loc.T(L.Tables.CoDealerOn),
        MemberState.CoDealerAway => Loc.T(L.Casino.DoorCoDealerAway),
        _ => Loc.T(L.Casino.DoorInRoom),
    };

    private void DrawMembers(AppSkin ui, CasinoTableDoorDto? door, CasinoTableRowDto card, float width, float scale)
    {
        RefreshMembers(door, card);
        if (scrollToPlayers)
        {
            scrollToPlayers = false;
            ImGui.SetScrollHereY(0f);
        }

        var title = members.Count > 0 ? texts.Count(L.HostDesk.PlayersCount, members.Count) : Loc.T(L.HostDesk.Players);
        DrawTitle(ui, title, width, true, scale);
        if (members.Count == 0)
        {
            DrawWaiting(ui, width, scale);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = DoorLayout.MemberHeight(scale, nameHeight, lineHeight);
        var frame = new Rect(origin, new Vector2(origin.X + width, origin.Y + rowHeight * members.Count));
        ui.Card(drawList, frame.Min, frame.Max, Metrics.Radius.Grouped * scale);
        for (var index = 0; index < members.Count; index++)
        {
            var row = new Rect(new Vector2(origin.X, origin.Y + rowHeight * index),
                new Vector2(frame.Max.X, origin.Y + rowHeight * (index + 1)));
            if (index > 0)
            {
                drawList.AddLine(new Vector2(DoorLayout.TextLeft(row, scale), row.Min.Y),
                    new Vector2(row.Max.X, row.Min.Y), ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            using (ImRaii.PushId(index))
            {
                DrawMember(drawList, ui, card, members[index], memberLines[index], row, nameHeight, lineHeight, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, frame.Height));
    }

    private void DrawMember(ImDrawListPtr drawList, AppSkin ui, CasinoTableRowDto card, in DoorMember member,
        string line, in Rect row, float nameHeight, float lineHeight, float scale)
    {
        var avatar = DoorLayout.AvatarCenter(row, scale);
        if (member.CoDealer)
        {
            drawList.AddCircle(avatar, (DoorLayout.Avatar * 0.5f + RingWidth) * scale, ImGui.GetColorU32(ui.Accent), 32,
                RingWidth * scale);
        }

        KnockCard.DrawAvatar(drawList, ui, avatar, member.Name, scale);
        var hasMenu = MenuFor(member, card, menuActions) > 0;
        var textLeft = DoorLayout.TextLeft(row, scale);
        var textWidth = MathF.Max(1f, DoorLayout.MemberTextRight(row, hasMenu, scale) - textLeft);
        var top = row.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(member.Name, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(line, textWidth, TextStyles.Footnote), member.CoDealer ? ui.Accent : ui.MutedInk,
            TextStyles.Footnote);
        if (!hasMenu)
        {
            return;
        }

        var center = DoorLayout.MenuCenter(row, scale);
        if (GlassCircle.Icon(drawList, ImGui.GetID("door.member.menu"), center, DoorLayout.MenuButton * 0.5f * scale,
                MenuGlyph * scale, PhoneIcons.Dots, ui.TitleInk, scale, GlassTone.Dark,
                texts.Named(L.HostDesk.PlayerOptions, member.Name), HoverLabelSide.Below, !tables.IntentInFlight))
        {
            menuTarget = member;
            menu.Open();
        }
    }

    private static void DrawWaiting(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = EmptyPad * scale;
        var inner = width - pad * 2f;
        var icon = EmptyIcon * scale;
        var title = Loc.T(L.HostDesk.WaitingTitle);
        var line = Loc.T(L.HostDesk.WaitingLine);
        var gap = EmptyGap * scale;
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, inner).Y;
        var lineHeight = Typography.MeasureWrappedBlock(line, TextStyles.Subheadline, inner).Y;
        var height = pad + icon + gap + titleHeight + gap + lineHeight + pad;
        var frame = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, frame.Min, frame.Max, Metrics.Radius.Grouped * scale);
        var center = new Vector2(frame.Center.X, origin.Y + pad + icon * 0.5f);
        var wave = Pulse.Wave(Pulse.Calm);
        drawList.AddCircleFilled(center, icon * 0.5f * (1f + 0.18f * wave),
            ImGui.GetColorU32(ui.Accent with { W = EmptyGlowAlpha * (1f - wave) }), 40);
        drawList.AddCircleFilled(center, icon * 0.5f, ImGui.GetColorU32(ui.Accent with { W = EmptyGlowAlpha }), 40);
        ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.UserFriends, ui.Accent, icon * 0.44f);
        var top = Typography.DrawWrappedCentered(drawList, title, TextStyles.Headline, ui.TitleInk,
            new Vector2(frame.Center.X, center.Y + icon * 0.5f + gap), inner);
        Typography.DrawWrappedCentered(drawList, line, TextStyles.Subheadline, ui.BodyInk,
            new Vector2(frame.Center.X, top + gap), inner);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void AskRemove(string userId, string name, bool watching)
    {
        var targetRoom = roomId;
        var targetUser = userId;
        confirm.Ask(new ConfirmRequest
        {
            Title = texts.Named(L.Casino.DoorRemoveConfirmTitle, name),
            Message = Loc.T(watching ? L.Casino.DoorRemoveMemberConfirmBody : L.Casino.DoorRemoveConfirmBody),
            ConfirmLabel = Loc.T(L.Casino.DoorRemove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.Kick(targetRoom, targetUser),
        });
    }
}
