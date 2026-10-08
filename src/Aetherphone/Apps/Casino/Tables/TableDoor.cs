using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class TableDoor
{
    private const float PillHeight = Button.RegularHeight;
    private const float RowHeight = 56f;
    private const float RowGap = 8f;
    private const float ControlRowUnits = 52f;
    private const float FieldHeight = 36f;

    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly Action<string> openTable;
    private readonly TableLedger ledger;
    private readonly Venue.TournamentDoorCard tournament;
    private readonly CasinoTextCache texts = new();

    private string roomId = string.Empty;
    private string inviteToken = string.Empty;
    private string inlineReason = string.Empty;
    private string nameBuffer = string.Empty;
    private bool nameSeeded;

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
        tables.ForgetDoor();
        ledger.Reset();
        tournament.Reset();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        using var surface = AppSurface.Begin(body);
        var door = tables.DoorFor(roomId);
        var card = tables.CardFor(roomId);
        var token = door is not null && door.InviteToken.Length > 0 ? door.InviteToken : inviteToken;
        SeedName(card);

        DrawInviteCard(ui, token, scale);
        if (inlineReason.Length > 0)
        {
            DrawInlineReason(ui, scale);
        }

        DrawOpenTable(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        if (card is not null)
        {
            ui.SectionHeading(Loc.T(L.Tables.HostPanelHeading), 4f);
            DrawHostControls(ui, card, scale);
            tournament.Draw(ui, card, roomId);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        ui.SectionHeading(Loc.T(L.Casino.DoorKnocksHeading), 4f);
        var knocks = door?.Knocks;
        if (knocks is null || knocks.Length == 0)
        {
            DrawNote(ui, Loc.T(L.Casino.DoorNoKnocks), scale);
        }
        else
        {
            for (var index = 0; index < knocks.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    DrawKnockRow(ui, knocks[index], scale);
                }
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        ui.SectionHeading(Loc.T(L.Casino.DoorSeatedHeading), 4f);
        var seated = door?.Seated;
        if (seated is null || seated.Length == 0)
        {
            DrawNote(ui, Loc.T(L.Casino.DoorNobodySeated), scale);
        }
        else
        {
            var banked = card is not null && CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));
            for (var index = 0; index < seated.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    DrawSeatedRow(ui, seated[index], card, banked, scale);
                }
            }
        }

        if (card is not null && card.Config is not null)
        {
            ledger.Draw(ui, tables.AccountId, card.OwnerUserId, scale);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

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
        if (outcome is not null)
        {
            inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
            if (outcome.Granted)
            {
                tables.RefreshCard(roomId);
            }
        }
    }

    private void DrawHostControls(AppSkin ui, CasinoTableRowDto card, float scale)
    {
        var config = card.Config;
        var banked = CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));
        var hostDeals = config is not null && config.DealerMode == CasinoDealerModes.Host;
        var rows = 2 + (banked ? 1 : 0) + (banked && hostDeals ? 1 : 0);
        var group = GroupCard.Begin(ui, ControlRowUnits * rows);
        DrawRenameRow(ui, group.NextRow(ControlRowUnits), scale);
        if (banked)
        {
            var pauseRow = group.NextRow(ControlRowUnits);
            Label(ui, pauseRow, Loc.T(L.Tables.PauseTable), scale);
            var width = Metrics.Size.ToggleWidth * scale;
            var height = Metrics.Size.ToggleHeight * scale;
            var toggleMin = new Vector2(pauseRow.Max.X - width, pauseRow.Center.Y - height * 0.5f);
            var paused = Toggle.Draw("door.pause", new Rect(toggleMin, toggleMin + new Vector2(width, height)),
                card.Paused, ui.Theme, 1f, !tables.IntentInFlight);
            if (paused != card.Paused)
            {
                inlineReason = string.Empty;
                tables.Pause(roomId, paused);
            }
        }

        if (banked && hostDeals)
        {
            var dealRow = group.NextRow(ControlRowUnits);
            Label(ui, dealRow, Loc.T(L.Tables.DealNow), scale);
            var label = Loc.T(L.Tables.Deal);
            var buttonWidth = Button.WidthFor(label, ButtonSize.Regular);
            var rect = new Rect(new Vector2(dealRow.Max.X - buttonWidth, dealRow.Center.Y - PillHeight * scale * 0.5f),
                new Vector2(dealRow.Max.X, dealRow.Center.Y + PillHeight * scale * 0.5f));
            if (Button.Draw(rect, label, ui.Ink, ButtonStyle.Prominent, enabled: !tables.IntentInFlight,
                    id: "door.deal"))
            {
                inlineReason = string.Empty;
                tables.Deal(roomId);
            }
        }

        var closeRow = group.NextRow(ControlRowUnits);
        var closeLabel = Loc.T(L.Tables.CloseTable);
        var closeWidth = Button.WidthFor(closeLabel, ButtonSize.Regular);
        Label(ui, closeRow, Loc.T(L.Tables.CloseTableHint), scale, closeWidth);
        var closeRect = new Rect(new Vector2(closeRow.Max.X - closeWidth, closeRow.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(closeRow.Max.X, closeRow.Center.Y + PillHeight * scale * 0.5f));
        if (Button.Draw(closeRect, closeLabel, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                !tables.IntentInFlight, id: "door.close"))
        {
            AskClose();
        }

        group.End();
    }

    private void DrawRenameRow(AppSkin ui, in Rect row, float scale)
    {
        var label = Loc.T(L.Tables.Rename);
        var buttonWidth = Button.WidthFor(label, ButtonSize.Regular);
        var height = FieldHeight * scale;
        var field = new Rect(new Vector2(row.Min.X, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X - buttonWidth - Metrics.Space.Sm * scale, row.Center.Y + height * 0.5f));
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##doorName", Loc.T(L.Tables.HostNameHint), ref nameBuffer,
                CasinoHostingRules.NameMaxLength + 1);
        }

        ImGui.SetCursorScreenPos(cursor);
        var rect = new Rect(new Vector2(row.Max.X - buttonWidth, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + PillHeight * scale * 0.5f));
        if (Button.Draw(rect, label, ui.Ink, ButtonStyle.Tinted, enabled: !tables.IntentInFlight, id: "door.rename"))
        {
            inlineReason = string.Empty;
            tables.Rename(roomId, nameBuffer.Trim());
        }
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

    private static void Label(AppSkin ui, in Rect row, string label, float scale, float reserve = 0f)
    {
        var height = Typography.LineHeight(TextStyles.Body);
        var width = row.Width - Metrics.Size.ToggleWidth * scale - reserve - Metrics.Space.Md * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - height * 0.5f),
            Typography.FitText(label, MathF.Max(1f, width), TextStyles.Body), ui.TitleInk, TextStyles.Body);
    }

    private void DrawInviteCard(AppSkin ui, string token, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var shareText = token.Length > 0 ? CasinoShare.Compose(token) : Loc.T(L.Casino.DoorTokenPending);
        var tokenBlock = Typography.MeasureWrappedBlock(shareText, TextStyles.Footnote, width - pad * 2f);
        var height = 20f * scale + tokenBlock.Y + PillHeight * scale + pad * 2f + 8f * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.30f)), 1f * scale);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), Loc.T(L.Casino.DoorInviteHeading),
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, card.Min.Y + pad + 20f * scale), shareText,
            ui.BodyInk, TextStyles.Footnote, width - pad * 2f);

        var pillRect = new Rect(new Vector2(card.Min.X + pad, card.Max.Y - PillHeight * scale - pad),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        if (ui.PillButton(pillRect, Loc.T(L.Casino.DoorCopyInvite), false, token.Length > 0)
            && token.Length > 0)
        {
            ImGui.SetClipboardText(CasinoShare.Compose(token));
            ShellToast.Show();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawInlineReason(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }

    private void DrawOpenTable(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + PillHeight * scale));
        if (ui.PillButton(rect, Loc.T(L.Casino.DoorOpenTable), true, roomId.Length > 0))
        {
            openTable(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PillHeight * scale));
    }

    private static void DrawNote(AppSkin ui, string text, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = 14f * scale;
        var block = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), text, ui.BodyInk, TextStyles.Footnote,
            width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private void DrawKnockRow(AppSkin ui, CasinoTableKnockDto knocker, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = RowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);

        var buttonWidth = 74f * scale;
        var textWidth = width - buttonWidth * 2f - 32f * scale;
        DrawIdentity(drawList, ui, knocker.DisplayName, row, textWidth, scale);

        var approveRect = new Rect(
            new Vector2(row.Max.X - buttonWidth * 2f - 18f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - buttonWidth - 18f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (ui.PillButton(approveRect, Loc.T(L.Casino.DoorApprove), true, !tables.IntentInFlight))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, true);
        }

        var denyRect = new Rect(
            new Vector2(row.Max.X - buttonWidth - 10f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - 10f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (ui.PillButton(denyRect, Loc.T(L.Casino.DoorDeny), false, !tables.IntentInFlight))
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }

    private void DrawSeatedRow(AppSkin ui, CasinoTableSeatedDto occupant, CasinoTableRowDto? card, bool banked,
        float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = RowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);

        var removeWidth = 84f * scale;
        var coDealer = card?.Config is not null && IsCoDealer(card.Config, occupant.UserId);
        var dealerLabel = coDealer ? Loc.T(L.Tables.CoDealerOn) : Loc.T(L.Tables.CoDealerOff);
        var dealerWidth = banked ? Button.WidthFor(dealerLabel, ButtonSize.Regular) : 0f;
        DrawIdentity(drawList, ui, occupant.DisplayName, row,
            width - removeWidth - dealerWidth - 40f * scale, scale);

        var removeRect = new Rect(
            new Vector2(row.Max.X - removeWidth - 10f * scale, row.Center.Y - PillHeight * scale * 0.5f),
            new Vector2(row.Max.X - 10f * scale, row.Center.Y + PillHeight * scale * 0.5f));
        if (banked && card?.Config is { } config)
        {
            var dealerRect = new Rect(
                new Vector2(removeRect.Min.X - Metrics.Space.Sm * scale - dealerWidth, removeRect.Min.Y),
                new Vector2(removeRect.Min.X - Metrics.Space.Sm * scale, removeRect.Max.Y));
            var full = !coDealer && (config.CoDealers?.Length ?? 0) >= CasinoHostingRules.MaxCoDealers;
            if (Button.Draw(dealerRect, dealerLabel, ui.Ink, coDealer ? ButtonStyle.Prominent : ButtonStyle.Tinted,
                    enabled: !tables.IntentInFlight && !full, id: "door.codealer"))
            {
                inlineReason = string.Empty;
                tables.SetCoDealers(roomId, Toggled(config.CoDealers, occupant.UserId));
            }
        }

        if (ui.DangerGhostButton(removeRect, Loc.T(L.Casino.DoorRemove)) && !tables.IntentInFlight)
        {
            AskRemove(occupant.UserId, occupant.DisplayName);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
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

    private static void DrawIdentity(ImDrawListPtr drawList, AppSkin ui, string name, in Rect row,
        float textWidth, float scale)
    {
        var left = row.Min.X + 14f * scale;
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - 8f * scale),
            Typography.FitText(name, MathF.Max(1f, textWidth), TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
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
