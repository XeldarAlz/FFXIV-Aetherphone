using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class TableDoor
{
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

    private void DrawRoom(AppSkin ui, CasinoTableDoorDto? door, CasinoTableRowDto? card, float width, float scale)
    {
        Gap(SectionGap, scale);
        ui.SectionHeading(Loc.T(L.Casino.DoorRoomHeading), 0f);
        var present = door?.Present;
        if (present is null || present.Length == 0)
        {
            DrawNote(ui, width, Loc.T(L.Casino.DoorNobodyInRoom), scale);
            return;
        }

        for (var index = 0; index < present.Length; index++)
        {
            using (ImRaii.PushId(index))
            {
                DrawPresent(ui, present[index], card, width, scale);
            }
        }
    }

    private void DrawPresent(AppSkin ui, CasinoTablePresentDto member, CasinoTableRowDto? card, float width,
        float scale)
    {
        var config = card?.Config;
        var coDealer = config is not null && IsCoDealer(config, member.UserId);
        var line = !member.Attached
            ? Loc.T(L.Casino.DoorCoDealerAway)
            : coDealer
                ? Loc.T(L.Tables.CoDealerOn)
                : Loc.T(L.Casino.DoorInRoom);
        DrawMemberRow(ui, member.UserId, member.DisplayName, line, false, true, card, width, scale);
    }

    private void DrawMemberRow(AppSkin ui, string userId, string displayName, string line, bool leaving,
        bool watching, CasinoTableRowDto? card, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = DoorLayout.PlayerHeight(scale, nameHeight, lineHeight);
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, row.Min, row.Max, radius);
        var config = card?.Config;
        var coDealer = config is not null && IsCoDealer(config, userId);
        if (coDealer)
        {
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(ui.Accent with { W = ActiveFillAlpha }));
        }

        KnockCard.DrawAvatar(drawList, ui, DoorLayout.AvatarCenter(row, scale), displayName, scale);
        var removable = Removable(userId, card);
        var dealerMode = coDealerMode && config is not null;
        var label = dealerMode
            ? Loc.T(coDealer ? L.Tables.CoDealerOn : L.Tables.CoDealerOff)
            : Loc.T(L.Casino.DoorRemove);
        var button = DoorLayout.TrailingButton(row, Button.WidthFor(label, ButtonSize.Large), scale);
        var textLeft = DoorLayout.TextLeft(row, scale);
        var textRight = removable ? button.Min.X - DoorLayout.TextGap * scale : row.Max.X - DoorLayout.Pad * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var top = row.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(displayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(line, textWidth, TextStyles.Subheadline), coDealer ? ui.Accent : ui.BodyInk,
            TextStyles.Subheadline);
        var busy = tables.IntentInFlight;
        if (!removable)
        {
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + DoorLayout.RowGap * scale));
            return;
        }

        if (dealerMode)
        {
            var full = !coDealer && (config!.CoDealers?.Length ?? 0) >= CasinoHostingRules.MaxCoDealers;
            if (Button.Draw(drawList, button, label, ui.Ink, coDealer ? ButtonStyle.Prominent : ButtonStyle.Tinted,
                    enabled: !busy && !full, id: "casino.door.codealer"))
            {
                inlineReason = string.Empty;
                tables.SetCoDealers(roomId, Toggled(config!.CoDealers, userId));
            }
        }
        else if (Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                     !busy && !leaving, id: "casino.door.remove"))
        {
            AskRemove(userId, displayName, watching);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + DoorLayout.RowGap * scale));
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
