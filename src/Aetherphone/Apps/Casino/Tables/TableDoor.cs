using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal enum DoorControl : byte
{
    Pause,
    Deal,
    CoDealers,
    Rename,
    Close,
}

internal sealed class TableDoor
{
    private const int ControlCapacity = 5;
    private const float SectionGap = 18f;
    private const float FieldHeight = 44f;
    private const int AvatarSegments = 32;
    private const float ActiveFillAlpha = 0.16f;

    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly Action<string> openTable;
    private readonly TableLedger ledger;
    private readonly Venue.TournamentDoorCard tournament;
    private readonly RoomCodeCard code = new("casino.door.copyCode");
    private readonly CasinoTextCache texts = new();
    private readonly DoorControl[] controls = new DoorControl[ControlCapacity];

    private string roomId = string.Empty;
    private string inviteToken = string.Empty;
    private string tokenSource = string.Empty;
    private string tokenText = string.Empty;
    private string inlineReason = string.Empty;
    private string nameBuffer = string.Empty;
    private bool nameSeeded;
    private bool renaming;
    private bool coDealerMode;

    public TableDoor(CasinoTablesStore tables, ConfirmService confirm, Action<string> openTable,
        Venue.TournamentDoorCard tournament)
    {
        this.tournament = tournament;
        this.tables = tables;
        this.confirm = confirm;
        this.openTable = openTable;
        ledger = new TableLedger(tables, confirm);
    }

    public string RoomId => roomId;

    public void Enter(string tableId, string token)
    {
        roomId = tableId;
        inviteToken = token;
        inlineReason = string.Empty;
        nameBuffer = string.Empty;
        nameSeeded = false;
        renaming = false;
        coDealerMode = false;
        code.Reset();
        tables.RefreshDoorNow(tableId);
        tables.RefreshCard(tableId);
        ledger.Enter(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        inviteToken = string.Empty;
        inlineReason = string.Empty;
        nameBuffer = string.Empty;
        nameSeeded = false;
        renaming = false;
        coDealerMode = false;
        tables.ForgetDoor();
        ledger.Reset();
        tournament.Reset();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        using var surface = AppSurface.Begin(body);
        var width = ScrollLayout.StableContentWidth();
        var door = tables.DoorFor(roomId);
        var card = tables.CardFor(roomId);
        SeedName(card);
        code.Draw(ui, JoinCode(door, card), Token(door), width, scale);
        Gap(Metrics.Space.Md, scale);
        DrawOpenTable(ui, width, scale);
        if (inlineReason.Length > 0)
        {
            Gap(Metrics.Space.Md, scale);
            DrawInlineReason(ui, width, scale);
        }

        if (card is not null)
        {
            Gap(SectionGap, scale);
            ui.SectionHeading(Loc.T(L.Tables.HostPanelHeading), 0f);
            DrawControls(ui, card, width, scale);
            if (renaming)
            {
                Gap(Metrics.Space.Md, scale);
                DrawRenameRow(ui, width, scale);
            }

            if (coDealerMode)
            {
                Gap(Metrics.Space.Sm, scale);
                DrawNote(ui, width, Loc.T(L.Tables.CoDealersNote), scale);
            }

            tournament.Draw(ui, card, roomId);
        }

        var knocks = door?.Knocks;
        Gap(SectionGap, scale);
        ui.SectionHeading(Loc.T(L.Casino.DoorKnocksHeading), 0f);
        if (knocks is null || knocks.Length == 0)
        {
            DrawNote(ui, width, Loc.T(L.Casino.DoorNoKnocks), scale);
        }
        else
        {
            for (var index = 0; index < knocks.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    DrawKnock(ui, knocks[index], width, scale);
                }
            }
        }

        Gap(SectionGap, scale);
        ui.SectionHeading(Loc.T(L.Casino.DoorSeatedHeading), 0f);
        var seated = door?.Seated;
        if (seated is null || seated.Length == 0)
        {
            DrawNote(ui, width, Loc.T(L.Casino.DoorNobodySeated), scale);
        }
        else
        {
            for (var index = 0; index < seated.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    DrawSeated(ui, seated[index], card, width, scale);
                }
            }
        }

        if (card is not null && card.Config is not null)
        {
            ledger.Draw(ui, tables.AccountId, card.OwnerUserId, scale);
        }

        Gap(Metrics.Space.Xl, scale);
    }

    internal static string JoinCode(CasinoTableDoorDto? door, CasinoTableRowDto? card)
    {
        if (door is not null && door.JoinCode.Length > 0)
        {
            return door.JoinCode;
        }

        return card?.JoinCode ?? string.Empty;
    }

    internal static int Controls(bool banked, bool hostDeals, DoorControl[] into)
    {
        var count = 0;
        if (banked)
        {
            into[count++] = DoorControl.Pause;
        }

        if (banked && hostDeals)
        {
            into[count++] = DoorControl.Deal;
        }

        if (banked)
        {
            into[count++] = DoorControl.CoDealers;
        }

        into[count++] = DoorControl.Rename;
        into[count++] = DoorControl.Close;
        return count;
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

    private string Token(CasinoTableDoorDto? door)
    {
        var source = door is not null && door.InviteToken.Length > 0 ? door.InviteToken : inviteToken;
        if (ReferenceEquals(source, tokenSource))
        {
            return tokenText;
        }

        tokenSource = source;
        tokenText = source.Length > 0 ? CasinoShare.Compose(source) : string.Empty;
        return tokenText;
    }

    private static void Gap(float units, float scale) => ImGui.Dummy(new Vector2(0f, units * scale));

    private void SeedName(CasinoTableRowDto? card)
    {
        if (nameSeeded || card is null)
        {
            return;
        }

        nameSeeded = true;
        nameBuffer = card.Name;
    }

    private void ConsumeOutcomes()
    {
        if (tables.TakeIntentFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var outcome = tables.TakeNoticeOutcome();
        if (outcome is null)
        {
            return;
        }

        inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
        if (outcome.Granted)
        {
            tables.RefreshCard(roomId);
        }
    }

    private void DrawOpenTable(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + Button.LargeHeight * scale));
        if (Button.Draw(ImGui.GetWindowDrawList(), rect, Loc.T(L.Casino.DoorOpenTable), ui.Ink, ButtonStyle.Tinted,
                enabled: roomId.Length > 0, id: "casino.door.open"))
        {
            openTable(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rect.Height));
    }

    private void DrawControls(AppSkin ui, CasinoTableRowDto card, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var banked = CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));
        var hostDeals = card.Config is not null && card.Config.DealerMode == CasinoDealerModes.Host;
        var count = Controls(banked, hostDeals, controls);
        var columns = DoorLayout.ControlColumns(width, count, scale);
        var cellWidth = DoorLayout.ControlCellWidth(width, columns, scale);
        var labelBlock = 0f;
        for (var index = 0; index < count; index++)
        {
            labelBlock = MathF.Max(labelBlock, Typography.MeasureWrappedBlock(ControlLabel(controls[index], card),
                TextStyles.FootnoteEmphasized, cellWidth).Y);
        }

        var busy = tables.IntentInFlight;
        for (var index = 0; index < count; index++)
        {
            var control = controls[index];
            var cell = DoorLayout.ControlCell(origin.X, origin.Y, width, index, count, scale, labelBlock);
            var center = DoorLayout.ControlCenter(cell, scale);
            var radius = DoorLayout.ControlButton * 0.5f * scale;
            var active = control switch
            {
                DoorControl.Pause => card.Paused,
                DoorControl.CoDealers => coDealerMode,
                DoorControl.Rename => renaming,
                _ => false,
            };
            var destructive = control == DoorControl.Close;
            var style = active || control == DoorControl.Deal ? ButtonStyle.Prominent : ButtonStyle.Tinted;
            var enabled = !busy || control is DoorControl.CoDealers or DoorControl.Rename;
            if (RoundButton.FontIcon(drawList, ImGui.GetID($"door.control.{index}"), center, radius,
                    ControlIcon(control, card), radius * 0.78f, ui.Ink, destructive ? ButtonStyle.Gray : style,
                    enabled: enabled, glyphInk: destructive ? ui.Theme.Danger : null))
            {
                Press(control, card);
            }

            var labelTop = cell.Min.Y + DoorLayout.ControlButton * scale + DoorLayout.LabelGap * scale;
            Typography.DrawWrappedCentered(drawList, ControlLabel(control, card), TextStyles.FootnoteEmphasized,
                destructive ? ui.Theme.Danger : active ? ui.TitleInk : ui.BodyInk, new Vector2(cell.Center.X, labelTop),
                cellWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, DoorLayout.ControlGridHeight(width, count, scale, labelBlock)));
    }

    private static FontAwesomeIcon ControlIcon(DoorControl control, CasinoTableRowDto card) => control switch
    {
        DoorControl.Pause => card.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause,
        DoorControl.Deal => FontAwesomeIcon.Clone,
        DoorControl.CoDealers => FontAwesomeIcon.UserTie,
        DoorControl.Rename => FontAwesomeIcon.PencilAlt,
        _ => FontAwesomeIcon.PowerOff,
    };

    private static string ControlLabel(DoorControl control, CasinoTableRowDto card) => control switch
    {
        DoorControl.Pause => Loc.T(card.Paused ? L.Tables.Resume : L.Tables.Pause),
        DoorControl.Deal => Loc.T(L.Tables.Deal),
        DoorControl.CoDealers => Loc.T(L.Tables.ControlCoDealers),
        DoorControl.Rename => Loc.T(L.Tables.Rename),
        _ => Loc.T(L.Tables.CloseTable),
    };

    private void Press(DoorControl control, CasinoTableRowDto card)
    {
        switch (control)
        {
            case DoorControl.Pause:
                inlineReason = string.Empty;
                tables.Pause(roomId, !card.Paused);
                break;
            case DoorControl.Deal:
                inlineReason = string.Empty;
                tables.Deal(roomId);
                break;
            case DoorControl.CoDealers:
                coDealerMode = !coDealerMode;
                break;
            case DoorControl.Rename:
                renaming = !renaming;
                break;
            default:
                AskClose();
                break;
        }
    }

    private void DrawRenameRow(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.Tables.RenameSave);
        var buttonWidth = Button.WidthFor(label, ButtonSize.Large);
        var height = FieldHeight * scale;
        var field = new Rect(origin,
            new Vector2(origin.X + width - buttonWidth - DoorLayout.ButtonGap * scale, origin.Y + height));
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var submitted = GlassField.Text(field, "##doorName", Loc.T(L.Tables.HostNameHint), ref nameBuffer, ui.Theme,
            scale, CasinoHostingRules.NameMaxLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        var rect = new Rect(new Vector2(origin.X + width - buttonWidth, origin.Y),
            new Vector2(origin.X + width, origin.Y + height));
        var tapped = Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Prominent,
            enabled: !tables.IntentInFlight, id: "casino.door.rename");
        if ((tapped || submitted) && !tables.IntentInFlight)
        {
            inlineReason = string.Empty;
            renaming = false;
            tables.Rename(roomId, nameBuffer.Trim());
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void AskClose()
    {
        var target = roomId;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Tables.CloseConfirmTitle),
            Message = Loc.T(L.Tables.CloseConfirmBody),
            ConfirmLabel = Loc.T(L.Tables.CloseTable),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.CloseTable(target),
        });
    }

    private void DrawInlineReason(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = DoorLayout.Pad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.12f)));
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), message, ui.TitleInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y));
    }

    private static void DrawNote(AppSkin ui, float width, string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = DoorLayout.Pad * scale;
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), text, ui.BodyInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y + DoorLayout.RowGap * scale));
    }

    private void DrawKnock(AppSkin ui, CasinoTableKnockDto knocker, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = DoorLayout.KnockHeight(scale, nameHeight, lineHeight);
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var avatar = DoorLayout.KnockAvatar(card, scale);
        DrawAvatar(drawList, ui, avatar, knocker.DisplayName, scale);
        var textLeft = DoorLayout.TextLeft(card, scale);
        var textWidth = MathF.Max(1f, card.Max.X - DoorLayout.Pad * scale - textLeft);
        var top = avatar.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(knocker.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(Loc.T(L.Tables.KnockLine), textWidth, TextStyles.Subheadline), ui.BodyInk,
            TextStyles.Subheadline);
        var enabled = !tables.IntentInFlight;
        if (Button.Draw(drawList, DoorLayout.KnockButton(card, 0, scale), Loc.T(L.Casino.DoorApprove), ui.Ink,
                ButtonStyle.Prominent, enabled: enabled, id: "casino.door.approve"))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, true);
        }

        if (Button.Draw(drawList, DoorLayout.KnockButton(card, 1, scale), Loc.T(L.Casino.DoorDeny), ui.Ink,
                ButtonStyle.Gray, enabled: enabled, id: "casino.door.deny"))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + DoorLayout.RowGap * scale));
    }

    private void DrawSeated(AppSkin ui, CasinoTableSeatedDto occupant, CasinoTableRowDto? card, float width,
        float scale)
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
        var coDealer = config is not null && IsCoDealer(config, occupant.UserId);
        if (coDealer)
        {
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(ui.Accent with { W = ActiveFillAlpha }));
        }

        DrawAvatar(drawList, ui, DoorLayout.AvatarCenter(row, scale), occupant.DisplayName, scale);
        var dealerMode = coDealerMode && config is not null;
        var label = dealerMode
            ? Loc.T(coDealer ? L.Tables.CoDealerOn : L.Tables.CoDealerOff)
            : Loc.T(L.Casino.DoorRemove);
        var button = DoorLayout.TrailingButton(row, Button.WidthFor(label, ButtonSize.Large), scale);
        var textLeft = DoorLayout.TextLeft(row, scale);
        var textWidth = MathF.Max(1f, button.Min.X - DoorLayout.TextGap * scale - textLeft);
        var top = row.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(occupant.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var line = coDealer ? Loc.T(L.Tables.CoDealerOn) : texts.Count(L.Tables.SeatLabel, occupant.SeatIndex + 1);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(line, textWidth, TextStyles.Subheadline), coDealer ? ui.Accent : ui.BodyInk,
            TextStyles.Subheadline);
        var busy = tables.IntentInFlight;
        if (dealerMode)
        {
            var full = !coDealer && (config!.CoDealers?.Length ?? 0) >= CasinoHostingRules.MaxCoDealers;
            if (Button.Draw(drawList, button, label, ui.Ink, coDealer ? ButtonStyle.Prominent : ButtonStyle.Tinted,
                    enabled: !busy && !full, id: "casino.door.codealer"))
            {
                inlineReason = string.Empty;
                tables.SetCoDealers(roomId, Toggled(config!.CoDealers, occupant.UserId));
            }
        }
        else if (Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive, !busy,
                     id: "casino.door.remove"))
        {
            AskRemove(occupant.UserId, occupant.DisplayName);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + DoorLayout.RowGap * scale));
    }

    private static void DrawAvatar(ImDrawListPtr drawList, AppSkin ui, Vector2 center, string name, float scale)
    {
        AvatarView.Draw(drawList, center, DoorLayout.Avatar * 0.5f * scale, ui.Accent, Initials.Of(name),
            TextStyles.Headline.Scale, AvatarHandle.Disabled, AvatarSegments);
    }

    private void AskRemove(string userId, string name)
    {
        var targetRoom = roomId;
        var targetUser = userId;
        confirm.Ask(new ConfirmRequest
        {
            Title = texts.Named(L.Casino.DoorRemoveConfirmTitle, name),
            Message = Loc.T(L.Casino.DoorRemoveConfirmBody),
            ConfirmLabel = Loc.T(L.Casino.DoorRemove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.Kick(targetRoom, targetUser),
        });
    }
}
