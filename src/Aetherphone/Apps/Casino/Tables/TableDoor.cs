using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
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

internal sealed partial class TableDoor
{
    private const int ControlCapacity = 5;
    private const float SectionGap = 18f;
    private const float StatusGap = 12f;
    private const float StatusLineGap = 3f;
    private const float CaptionGap = 7f;
    private const float StatusRadius = 22f;
    private const float StatusWashAlpha = 0.18f;
    private const float StatusWashFloorAlpha = 0.02f;
    private const float StatusRimAlpha = 0.5f;
    private const float StatusRimWidth = 1.5f;
    private const float NoteInset = 4f;
    private const long CopiedMilliseconds = 2_000;

    private static readonly Vector4 DeepRose = new(0.541f, 0.129f, 0.278f, 1f);

    private readonly CasinoTablesStore tables;
    private readonly ConfirmService confirm;
    private readonly Action<string> openTable;
    private readonly Action<string> openLedger;
    private readonly Venue.TournamentDoorCard tournament;
    private readonly CasinoTradeSync trade;
    private readonly CasinoTextCache texts = new();
    private readonly DoorControl[] controls = new DoorControl[ControlCapacity];
    private readonly CodeTiles codeTiles = new();
    private readonly GilLedgerSummary summary = new("casino.door.tradeSync");

    private string roomId = string.Empty;
    private string inviteToken = string.Empty;
    private string tokenSource = string.Empty;
    private string tokenText = string.Empty;
    private string inlineReason = string.Empty;
    private string nameBuffer = string.Empty;
    private bool nameSeeded;
    private bool renaming;
    private bool scrollToPlayers;
    private long copiedCodeAtTick;
    private long copiedInviteAtTick;
    private CasinoTableRowDto? statusSource;
    private LanguageInfo? statusLanguage;
    private string statusName = string.Empty;
    private string statusLine = string.Empty;

    public TableDoor(CasinoTablesStore tables, ConfirmService confirm, Action<string> openTable,
        Action<string> openLedger, Venue.TournamentDoorCard tournament, CasinoTradeSync trade)
    {
        this.tables = tables;
        this.confirm = confirm;
        this.openTable = openTable;
        this.openLedger = openLedger;
        this.tournament = tournament;
        this.trade = trade;
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
        scrollToPlayers = false;
        copiedCodeAtTick = 0;
        copiedInviteAtTick = 0;
        menu.Close();
        tables.RefreshDoorNow(tableId);
        tables.RefreshCard(tableId);
        tables.RefreshLedgerNow(tableId);
    }

    public void Reset()
    {
        roomId = string.Empty;
        inviteToken = string.Empty;
        inlineReason = string.Empty;
        nameBuffer = string.Empty;
        nameSeeded = false;
        renaming = false;
        scrollToPlayers = false;
        menu.Close();
        tables.ForgetDoor();
        tables.ForgetBookLedgerAdmission();
        tournament.Reset();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        tables.EnsureDoorFresh(roomId);
        var door = tables.DoorFor(roomId);
        var card = tables.CardFor(roomId);
        SeedName(card);
        using var surface = AppSurface.Begin(body);
        var width = ScrollLayout.StableContentWidth();
        Gap(Metrics.Space.Xs, scale);
        DrawStatus(ui, door, card, width, scale);
        if (inlineReason.Length > 0)
        {
            Gap(Metrics.Space.Md, scale);
            DrawInlineReason(ui, width, scale);
        }

        if (card is null)
        {
            Gap(Metrics.Space.Xl, scale);
            return;
        }

        var knocks = door?.Knocks;
        if (knocks is { Length: > 0 })
        {
            Gap(SectionGap, scale);
            DrawTitle(ui, Loc.T(L.HostDesk.Knocking), width, false, scale);
            for (var index = 0; index < knocks.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    DrawKnock(ui, knocks[index], width, scale);
                }
            }
        }

        if (!CasinoBookRules.IsBook(card.GameKind))
        {
            Gap(SectionGap, scale);
            DrawMembers(ui, door, card, width, scale);
        }

        tournament.Draw(ui, card, door, roomId);
        DrawLedger(ui, card, width, scale);
        Gap(SectionGap, scale);
        DrawManage(ui, card, width, scale);
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

    internal static LocString OpenLabelFor(CasinoTableRowDto? card) =>
        card is not null && CasinoBookRules.IsBook(card.GameKind) ? L.Book.OpenLedger : L.HostDesk.GoToTable;

    internal static LocString TitleFor(CasinoTableRowDto? card) =>
        card is not null && CasinoBookRules.IsBook(card.GameKind) ? L.Book.DoorTitle : L.Casino.DoorTitle;

    internal static bool ShowsGilLedger(CasinoTableRowDto card) =>
        CasinoCurrencies.Of(card) == CasinoCurrencies.Gil;

    internal static bool ShowsSessionLedger(CasinoTableRowDto card) => !ShowsGilLedger(card) && HasSeats(card);

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

    private void RefreshStatus(CasinoTableRowDto? card)
    {
        if (ReferenceEquals(card, statusSource) && ReferenceEquals(statusLanguage, Loc.Current))
        {
            return;
        }

        statusSource = card;
        statusLanguage = Loc.Current;
        statusName = card is null ? string.Empty : TableRowViews.NameOf(card);
        statusLine = card is null ? string.Empty : TableRowViews.HostLine(card);
    }

    private void DrawStatus(AppSkin ui, CasinoTableDoorDto? door, CasinoTableRowDto? card, float width, float scale)
    {
        RefreshStatus(card);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var now = Environment.TickCount64;
        var pad = DoorLayout.Pad * scale;
        var inner = width - pad * 2f;
        var left = origin.X + pad;
        var paused = card?.Paused ?? false;
        var pill = TableStatusPill.Size(paused, scale);
        var tile = DoorLayout.StatusTile * scale;
        var textLeft = left + tile + DoorLayout.TextGap * scale;
        var textRight = origin.X + width - pad - (card is null ? 0f : pill.X + Metrics.Space.Sm * scale);
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(statusLine, TextStyles.Footnote, textWidth);
        var textBlock = nameHeight + StatusLineGap * scale + lineHeight;
        var header = MathF.Max(tile, textBlock);
        var caption = Loc.T(L.HostDesk.CodeCaption);
        var captionTop = origin.Y + pad + header + StatusGap * scale;
        var captionHeight = PitText.Height(caption, TextStyles.FootnoteEmphasized, inner);
        var code = JoinCode(door, card);
        var hasCode = code.Length == CasinoRoomCodes.Length;
        var codeCopied = now - copiedCodeAtTick < CopiedMilliseconds;
        var copyLabel = Loc.T(codeCopied ? L.Tables.CodeCopied : L.Tables.CodeCopy);
        var touch = DoorLayout.Touch * scale;
        var codeTop = captionTop + captionHeight + CaptionGap * scale;
        var codeRow = DoorLayout.CodeRow(left, codeTop, inner, DoorPill.WidthFor(copyLabel, touch, scale), scale);
        var codeHeight = DoorLayout.CodeRowHeight(codeRow, scale);
        var buttonsTop = codeTop + codeHeight + StatusGap * scale;
        var buttonHeight = Button.LargeHeight * scale;
        var bottom = buttonsTop + buttonHeight + pad;
        var frame = new Rect(origin, new Vector2(origin.X + width, bottom));
        DrawWash(drawList, ui, frame, scale);
        var gameId = card is null ? CasinoGames.Blackjack : TableRowViews.GameIdOf(card.GameKind);
        CasinoArt.GameTile(drawList, gameId, new Vector2(left + tile * 0.5f, origin.Y + pad + header * 0.5f), tile);
        var textTop = origin.Y + pad + (header - textBlock) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(statusName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        PitText.Draw(drawList, statusLine, TextStyles.Footnote, ui.BodyInk, textLeft,
            textTop + nameHeight + StatusLineGap * scale, textWidth, false);
        if (card is not null)
        {
            TableStatusPill.Draw(drawList,
                new Vector2(origin.X + width - pad - pill.X, origin.Y + pad + (header - pill.Y) * 0.5f), paused, scale);
        }

        PitText.Draw(drawList, caption, TextStyles.FootnoteEmphasized, ui.BodyInk, left, captionTop, inner, false);
        codeTiles.Draw(drawList, new Vector2(left, codeTop + (codeHeight - codeRow.TileHeight) * 0.5f),
            codeRow.TileWidth, codeRow.TileHeight, codeRow.TileGap, hasCode ? code : string.Empty, TextStyles.Title2,
            scale);
        if (DoorPill.Draw(drawList, codeRow.Copy, copyLabel, ui.Ink,
                codeCopied ? ButtonStyle.Gray : ButtonStyle.Prominent, null, hasCode, "casino.door.copyCode", scale))
        {
            ImGui.SetClipboardText(code);
            copiedCodeAtTick = now;
        }

        var go = new Rect(new Vector2(left, buttonsTop),
            new Vector2(left + DoorLayout.PrimaryShare(inner, scale), buttonsTop + buttonHeight));
        if (DoorPill.Draw(drawList, go, Loc.T(OpenLabelFor(card)), ui.Ink.WithAccent(CasinoArt.White),
                ButtonStyle.Prominent, DeepRose, roomId.Length > 0, "casino.door.open", scale))
        {
            openTable(roomId);
        }

        var token = Token(door);
        var inviteCopied = now - copiedInviteAtTick < CopiedMilliseconds;
        var invite = new Rect(new Vector2(go.Max.X + DoorLayout.ButtonGap * scale, buttonsTop),
            new Vector2(origin.X + width - pad, buttonsTop + buttonHeight));
        if (DoorPill.Draw(drawList, invite, Loc.T(inviteCopied ? L.Tables.CodeCopied : L.Casino.DoorCopyInvite),
                ui.Ink, ButtonStyle.Gray, null, token.Length > 0, "casino.door.copyInvite", scale))
        {
            ImGui.SetClipboardText(token);
            copiedInviteAtTick = now;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y));
    }

    private static void DrawWash(ImDrawListPtr drawList, AppSkin ui, in Rect frame, float scale)
    {
        var radius = StatusRadius * scale;
        ui.Card(drawList, frame.Min, frame.Max, radius);
        Squircle.FillVerticalGradient(drawList, frame.Min, frame.Max, radius,
            ImGui.GetColorU32(ui.Accent with { W = StatusWashAlpha }),
            ImGui.GetColorU32(ui.Accent with { W = StatusWashFloorAlpha }));
        Squircle.Stroke(drawList, frame.Min, frame.Max, radius,
            ImGui.GetColorU32(ui.Accent with { W = StatusRimAlpha }), StatusRimWidth * scale);
    }

    private static void DrawTitle(AppSkin ui, string title, float width, bool live, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = Typography.LineHeight(TextStyles.Title3);
        var trailing = 0f;
        if (live)
        {
            var tag = Loc.T(L.HostDesk.LiveTag);
            var tagSize = Typography.Measure(tag, TextStyles.Footnote);
            var dot = CasinoArt.LiveDotRadius * scale;
            trailing = tagSize.X + dot * 2f + Metrics.Space.Xs * scale;
            var tagLeft = origin.X + width - tagSize.X;
            Typography.Draw(drawList, new Vector2(tagLeft, origin.Y + (height - tagSize.Y) * 0.5f), tag, ui.MutedInk,
                TextStyles.Footnote);
            CasinoArt.LiveDot(drawList, new Vector2(tagLeft - Metrics.Space.Xs * scale - dot, origin.Y + height * 0.5f),
                scale, TableLedger.SettledInk, true);
        }

        Typography.Draw(drawList, origin,
            Typography.FitText(title, MathF.Max(1f, width - trailing - Metrics.Space.Sm * scale), TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
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

    private static void DrawNote(AppSkin ui, string text, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var inset = NoteInset * scale;
        var height = PitText.Draw(ImGui.GetWindowDrawList(), text, TextStyles.Footnote, ui.MutedInk, origin.X + inset,
            origin.Y, width - inset * 2f, false) - origin.Y;
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawKnock(AppSkin ui, CasinoTableKnockDto knocker, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var answer = KnockCard.Draw(ImGui.GetWindowDrawList(), ui, knocker, origin, width, !tables.IntentInFlight,
            scale);
        if (answer != KnockAnswer.None)
        {
            inlineReason = string.Empty;
            tables.AnswerKnock(roomId, knocker.UserId, answer == KnockAnswer.Approve);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, KnockCard.Height(width, scale) + DoorLayout.RowGap * scale));
    }

    private void DrawLedger(AppSkin ui, CasinoTableRowDto card, float width, float scale)
    {
        if (ShowsSessionLedger(card))
        {
            Gap(SectionGap, scale);
            var origin = ImGui.GetCursorScreenPos();
            var line = Loc.T(L.HostDesk.SessionLine);
            if (LinkRow.Draw(ImGui.GetWindowDrawList(), ui, origin, width, Loc.T(L.Tables.LedgerHeading), line,
                    FontAwesomeIcon.Book, AccentRing.Violet, false, scale))
            {
                openLedger(roomId);
            }

            ImGui.Dummy(new Vector2(width, LinkRow.Height(line, width, scale)));
            return;
        }

        if (!ShowsGilLedger(card))
        {
            return;
        }

        tables.EnsureLedgerFresh(roomId);
        summary.Refresh(tables.LedgerFor(roomId), tables.AccountId);
        Gap(SectionGap, scale);
        DrawTitle(ui, Loc.T(L.Tables.GilLedgerHeading), width, false, scale);
        var top = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var syncHeight = GilLedgerSummary.SyncHeight(width, scale);
        var totalsHeight = GilLedgerSummary.TotalsHeight(scale);
        var frame = new Rect(top, new Vector2(top.X + width, top.Y + syncHeight + totalsHeight));
        ui.Card(drawList, frame.Min, frame.Max, Metrics.Radius.Grouped * scale);
        var sync = new Rect(top, new Vector2(frame.Max.X, top.Y + syncHeight));
        var enabled = summary.DrawSync(drawList, ui, sync, trade.Enabled, scale);
        if (enabled != trade.Enabled)
        {
            trade.Enabled = enabled;
        }

        drawList.AddLine(new Vector2(frame.Min.X + DoorLayout.Pad * scale, sync.Max.Y),
            new Vector2(frame.Max.X, sync.Max.Y), ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        var totals = new Rect(new Vector2(frame.Min.X, sync.Max.Y), frame.Max);
        if (summary.DrawTotals(drawList, ui, totals, true, scale))
        {
            openLedger(roomId);
        }

        ImGui.SetCursorScreenPos(top);
        ImGui.Dummy(new Vector2(width, frame.Height));
    }
}
