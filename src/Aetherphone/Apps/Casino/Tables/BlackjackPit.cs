using Aetherphone.Apps.Casino.Live;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackPit
{
    private const float CardGap = 10f;
    private const float CardPad = 14f;
    private const float QuickHeight = 124f;
    private const float RowGap = 8f;
    private const float BulbInset = 5f;
    private const string HouseActionPrefix = "blackjack.pit.house.";

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoor;
    private readonly Action enterHouse;
    private readonly Action openBrowser;
    private readonly Action openHostSheet;
    private readonly CasinoTextCache text = new();
    private readonly BlackjackRoomRows houseRows = new();
    private readonly string[] houseActionIds = new string[CasinoRoomIds.BlackjackHouse.Length];

    private CasinoTableRowDto[] hostedSource = Array.Empty<CasinoTableRowDto>();
    private TableRowView[] hostedViews = Array.Empty<TableRowView>();
    private LanguageInfo? hostedLanguage;
    private float phase;

    public BlackjackPit(CasinoTablesStore tables, CasinoStore chips, Action<string> openTable,
        Action<string> openDoor, Action enterHouse, Action openBrowser, Action openHostSheet)
    {
        this.tables = tables;
        this.chips = chips;
        this.openTable = openTable;
        this.openDoor = openDoor;
        this.enterHouse = enterHouse;
        this.openBrowser = openBrowser;
        this.openHostSheet = openHostSheet;
        for (var index = 0; index < houseActionIds.Length; index++)
        {
            houseActionIds[index] = HouseActionPrefix
                + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public void Enter()
    {
        tables.RefreshNow();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        phase += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        RefreshHosted();
        using var surface = AppSurface.Begin(body);
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        DrawQuickSeat(drawList, ui, width, scale);
        ui.SectionHeading(Loc.T(L.LivePage.HouseTitle), 4f);
        DrawHouse(drawList, ui, width, scale);
        ui.SectionHeading(Loc.T(L.Blackjack.PitHostedHeading), 4f);
        DrawHosted(drawList, ui, width, scale);
        DrawLinks(ui, width, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void RefreshHosted()
    {
        var source = tables.Listed;
        if (ReferenceEquals(source, hostedSource) && ReferenceEquals(hostedLanguage, Loc.Current))
        {
            return;
        }

        hostedSource = source;
        hostedLanguage = Loc.Current;
        var account = tables.AccountId;
        hostedViews = new TableRowView[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            if (IsHostedBlackjack(source[index]))
            {
                hostedViews[index] = TableRowViews.ViewOf(source[index], account);
            }
        }
    }

    private static bool IsHostedBlackjack(CasinoTableRowDto row)
    {
        return CasinoTableFilters.IsPrivate(row)
            && string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal);
    }

    private void DrawQuickSeat(ImDrawListPtr drawList, AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = CardPad * scale;
        var inner = width - pad * 2f;
        var floor = HouseBlackjack.Of(tables.Tables);
        var title = Loc.T(L.Casino.QuickSeatTitle);
        var hint = Loc.T(L.Blackjack.PitQuickHint);
        var stakes = text.Compacts(L.Casino.TableStakes, floor.MinBet, floor.TopBetFor(chips.Ceiling.MaxBet));
        var players = text.Count(L.Casino.LivePlayers, floor.Seated);
        var height = pad * 2f + PitText.Height(title, TextStyles.Title3, inner)
            + PitText.Height(hint, TextStyles.Subheadline, inner)
            + PitText.Height(stakes, TextStyles.SubheadlineEmphasized, inner)
            + PitText.Height(players, TextStyles.Footnote, inner)
            + (Metrics.Space.Sm + Button.LargeHeight) * scale;
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + MathF.Max(QuickHeight * scale, height)));
        var rounding = Metrics.Radius.Grouped * scale;
        DrawLitCard(drawList, card, rounding, scale);
        var left = card.Min.X + pad;
        var y = PitText.Draw(drawList, title, TextStyles.Title3, StageText.Strong, left, card.Min.Y + pad, inner, false);
        y = PitText.Draw(drawList, hint, TextStyles.Subheadline, StageText.Body, left, y, inner, false);
        y = PitText.Draw(drawList, stakes, TextStyles.SubheadlineEmphasized, CasinoColors.Money, left, y, inner,
            false);
        PitText.Draw(drawList, players, TextStyles.Footnote, StageText.Body, left, y, inner, false);
        var pill = new Rect(new Vector2(left, card.Max.Y - pad - Button.LargeHeight * scale),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        if (DeckActions.DrawPrimary(pill, Loc.T(L.Casino.QuickSeatAction), ui.Ink, !tables.IntentInFlight,
                "blackjack.pit.quick"))
        {
            enterHouse();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + CardGap * scale));
    }

    private void DrawHouse(ImDrawListPtr drawList, AppSkin ui, float width, float scale)
    {
        var seatedAt = CasinoWire.SittingFor(chips.State, CasinoWire.BlackjackKind)?.TableId ?? string.Empty;
        houseRows.Refresh(tables.Tables, seatedAt, chips.Ceiling.MaxBet);
        var rows = houseRows.Rows;
        for (var index = 0; index < rows.Length; index++)
        {
            var origin = ImGui.GetCursorScreenPos();
            if (HouseRoomCard.Draw(drawList, ui, rows[index], origin, width, houseActionIds[index], true, false, scale))
            {
                openTable(rows[index].RoomId);
            }

            ImGui.Dummy(new Vector2(width, HouseRoomCard.Height(rows[index], width, scale) + RowGap * scale));
        }

        var rulesTop = ImGui.GetCursorScreenPos();
        var rulesBottom = PitText.Draw(drawList, Loc.T(L.LivePage.BlackjackRoomsLine), TextStyles.Footnote,
            ui.BodyInk, rulesTop.X, rulesTop.Y, width, false);
        ImGui.Dummy(new Vector2(width, rulesBottom - rulesTop.Y + RowGap * scale));
    }

    private void DrawLitCard(ImDrawListPtr drawList, Rect card, float rounding, float scale)
    {
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(CasinoColors.FeltTop),
            ImGui.GetColorU32(CasinoColors.FeltBottom));
        var glow = new Vector2(card.Center.X, card.Min.Y + card.Height * 0.35f);
        drawList.AddCircleFilled(glow, card.Width * 0.45f,
            ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.7f, 0.05f)), 40);
        var inset = new Vector2(BulbInset * scale, BulbInset * scale);
        var bulbs = new Rect(card.Min + inset, card.Max - inset);
        CasinoLights.BulbChase(drawList, bulbs, MathF.Max(0f, rounding - inset.X), scale, phase,
            CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, 1f);
    }

    private void DrawHosted(ImDrawListPtr drawList, AppSkin ui, float width, float scale)
    {
        var source = hostedSource;
        var drawn = 0;
        for (var index = 0; index < source.Length && index < hostedViews.Length; index++)
        {
            var row = source[index];
            if (!IsHostedBlackjack(row))
            {
                continue;
            }

            var origin = ImGui.GetCursorScreenPos();
            var height = TableRow.HeightOf(hostedViews[index], width) * scale;
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            using (UiScope.PushId(index))
            {
                if (TableRow.Draw(drawList, rect, ui, hostedViews[index], scale))
                {
                    if (hostedViews[index].Mine)
                    {
                        openDoor(row.TableId);
                    }
                    else
                    {
                        openTable(row.TableId);
                    }
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height + RowGap * scale));
            drawn++;
        }

        if (drawn > 0)
        {
            return;
        }

        var start = ImGui.GetCursorScreenPos();
        var message = Loc.T(tables.Loaded ? L.Blackjack.PitNoHosted : L.Casino.TablesLoading);
        var pad = CardPad * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Subheadline, width - pad * 2f);
        var max = new Vector2(start.X + width, start.Y + block.Y + pad * 2f);
        ui.Card(drawList, start, max, Metrics.Radius.Grouped * scale);
        Typography.DrawWrappedLeft(new Vector2(start.X + pad, start.Y + pad), message, ui.BodyInk,
            TextStyles.Subheadline, width - pad * 2f);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, max.Y - start.Y + RowGap * scale));
    }

    private void DrawLinks(AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Button.LargeHeight * scale;
        var browse = Loc.T(L.Blackjack.PitBrowse);
        var host = Loc.T(L.Tables.HostTitle);
        var style = Button.LabelStyle(height);
        var sideBySide = TablePairLayout.SideBySide(width, height, Typography.Measure(browse, style).X,
            Typography.Measure(host, style).X, scale);
        if (DeckActions.DrawSecondary(TablePairLayout.Cell(origin.X, origin.Y, width, height, 0, sideBySide, scale),
                browse, ui.Ink, true, "blackjack.pit.browse"))
        {
            openBrowser();
        }

        if (DeckActions.DrawSecondary(TablePairLayout.Cell(origin.X, origin.Y, width, height, 1, sideBySide, scale),
                host, ui.Ink, true, "blackjack.pit.host"))
        {
            openHostSheet();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, TablePairLayout.Height(sideBySide, height, scale) + RowGap * scale));
    }
}
