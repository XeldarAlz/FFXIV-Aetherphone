using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackPit
{
    private const float CardHeight = 168f;
    private const float CardGap = 10f;
    private const float CardPad = 14f;
    private const float QuickHeight = 124f;
    private const float RowGap = 8f;
    private const float PuckRadius = 15f;
    private const float BulbInset = 5f;
    private const int Columns = 2;

    private static readonly LocString[] TierNames =
    {
        L.Casino.TierPit, L.Casino.TierParlour, L.Casino.TierSalon, L.Blackjack.TierVault,
    };

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Action<string> openTable;
    private readonly Action<string> openDoor;
    private readonly Action openBrowser;
    private readonly Action openHostSheet;
    private readonly CasinoTextCache text = new();
    private readonly CasinoTableRowDto?[] house = new CasinoTableRowDto?[BlackjackRules.HouseTierCount];

    private CasinoTableRowDto[] hostedSource = Array.Empty<CasinoTableRowDto>();
    private TableRowView[] hostedViews = Array.Empty<TableRowView>();
    private LanguageInfo? hostedLanguage;
    private float phase;

    public BlackjackPit(CasinoTablesStore tables, CasinoStore chips, Action<string> openTable,
        Action<string> openDoor, Action openBrowser, Action openHostSheet)
    {
        this.tables = tables;
        this.chips = chips;
        this.openTable = openTable;
        this.openDoor = openDoor;
        this.openBrowser = openBrowser;
        this.openHostSheet = openHostSheet;
    }

    public void Enter()
    {
        tables.RefreshNow();
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        phase += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        CollectHouse();
        RefreshHosted();
        using var surface = AppSurface.Begin(body);
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        DrawQuickSeat(drawList, ui, width, scale);
        ui.SectionHeading(Loc.T(L.Blackjack.PitHouseHeading), 4f);
        DrawHouseGrid(drawList, ui, width, scale);
        ui.SectionHeading(Loc.T(L.Blackjack.PitHostedHeading), 4f);
        DrawHosted(drawList, ui, width, scale);
        DrawLinks(ui, width, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    internal static int TierOf(CasinoTableRowDto row)
    {
        return row.Kind == CasinoTableKinds.House && row.StakeTier >= 0 && row.StakeTier < BlackjackRules.HouseTierCount
            && string.Equals(row.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal)
            ? row.StakeTier
            : -1;
    }

    private void CollectHouse()
    {
        Array.Clear(house);
        var rows = tables.Tables;
        for (var index = 0; index < rows.Length; index++)
        {
            var tier = TierOf(rows[index]);
            if (tier >= 0 && house[tier] is null)
            {
                house[tier] = rows[index];
            }
        }
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
                hostedViews[index] = TableBrowser.ViewOf(source[index], account);
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
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + QuickHeight * scale));
        var rounding = Metrics.Radius.Grouped * scale;
        DrawLitCard(drawList, card, rounding, 1f, scale);
        var pad = CardPad * scale;
        var titleTop = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, titleTop),
            Typography.FitText(Loc.T(L.Casino.QuickSeatTitle), card.Width - pad * 2f, TextStyles.Title3),
            StageText.Strong, TextStyles.Title3);
        var hintTop = titleTop + Typography.LineHeight(TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, hintTop),
            Typography.FitText(Loc.T(L.Blackjack.PitQuickHint), card.Width - pad * 2f, TextStyles.Subheadline),
            StageText.Body, TextStyles.Subheadline);
        var pill = new Rect(new Vector2(card.Min.X + pad, card.Max.Y - pad - Button.LargeHeight * scale),
            new Vector2(card.Max.X - pad, card.Max.Y - pad));
        if (DeckActions.DrawPrimary(pill, Loc.T(L.Casino.QuickSeatAction), ui.Ink, !tables.IntentInFlight,
                "blackjack.pit.quick"))
        {
            tables.QuickSeat(CasinoStakeTiers.ForHouseTier(BlackjackRules.QuickTierFor(chips.Ceiling.MaxBet)));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, card.Height + CardGap * scale));
    }

    private void DrawHouseGrid(ImDrawListPtr drawList, AppSkin ui, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var gap = CardGap * scale;
        var cardWidth = (width - gap * (Columns - 1)) / Columns;
        var cardHeight = CardHeight * scale;
        var rows = (BlackjackRules.HouseTierCount + Columns - 1) / Columns;
        for (var tier = 0; tier < BlackjackRules.HouseTierCount; tier++)
        {
            var column = tier % Columns;
            var row = tier / Columns;
            var min = new Vector2(origin.X + column * (cardWidth + gap), origin.Y + row * (cardHeight + gap));
            using (ImRaii.PushId(tier))
            {
                DrawHouseCard(drawList, ui, new Rect(min, min + new Vector2(cardWidth, cardHeight)), tier, scale);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * (cardHeight + gap)));
    }

    private void DrawHouseCard(ImDrawListPtr drawList, AppSkin ui, Rect card, int tier, float scale)
    {
        var row = house[tier];
        var rounding = Metrics.Radius.Grouped * scale;
        var seated = row?.SeatedCount ?? 0;
        var seats = row is { MaxSeats: > 0 } ? row.MaxSeats : BlackjackRules.SeatCount;
        var hovered = row is not null && UiInteract.Hover(card.Min, card.Max);
        DrawLitCard(drawList, card, rounding, seated > 0 ? 1f : hovered ? 0.7f : 0.35f, scale);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = CardPad * scale;
        var inner = card.Width - pad * 2f;
        var puck = new Vector2(card.Center.X, card.Min.Y + pad + PuckRadius * scale);
        BlackjackDealer.DrawPuck(drawList, puck, PuckRadius * scale, phase, scale);
        var y = puck.Y + PuckRadius * scale + Metrics.Space.Sm * scale;
        y = Line(drawList, Loc.T(TierNames[tier]), card.Center.X, y, inner, StageText.Strong, TextStyles.Headline);
        var band = text.Compacts(L.Casino.TableStakes, BlackjackRules.HouseTierMinBets[tier],
            BlackjackRules.HouseTierMaxBets[tier]);
        y = Line(drawList, band, card.Center.X, y, inner, CasinoColors.Money, TextStyles.FootnoteEmphasized);
        var dotsWidth = MathF.Min(inner, seats * 10f * scale);
        CasinoArt.SeatDots(drawList, new Vector2(card.Center.X - dotsWidth * 0.5f, y + 6f * scale), seated, seats,
            CasinoColors.LightB, StageText.Body with { W = 0.35f }, scale);
        y += 14f * scale;
        var occupancy = row is null
            ? Loc.T(L.Casino.TablesLoading)
            : text.Counts(L.Casino.TableSeats, seated, seats);
        y = Line(drawList, occupancy, card.Center.X, y, inner, StageText.Body, TextStyles.Footnote);
        var watching = row is null ? 0 : CasinoTableFilters.SpectatorsOf(row);
        if (chips.Ceiling.MaxBet > 0 && chips.Ceiling.MaxBet < BlackjackRules.HouseTierMinBets[tier])
        {
            Line(drawList, Loc.T(L.Blackjack.PitAboveCeiling), card.Center.X, y, inner, CasinoColors.LightA,
                TextStyles.Footnote);
        }
        else if (watching > 0)
        {
            Line(drawList, text.Count(L.Casino.TableSpectators, watching), card.Center.X, y, inner, StageText.Body,
                TextStyles.Footnote);
        }

        if (row is not null && UiInteract.Click(card.Min, card.Max, hovered))
        {
            openTable(row.TableId);
        }
    }

    private static float Line(ImDrawListPtr drawList, string value, float centerX, float top, float width,
        Vector4 ink, in TextStyle style)
    {
        var fitted = Typography.FitText(value, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, style);
        return top + Typography.LineHeight(style);
    }

    private void DrawLitCard(ImDrawListPtr drawList, Rect card, float rounding, float lit, float scale)
    {
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding, ImGui.GetColorU32(CasinoColors.FeltTop),
            ImGui.GetColorU32(CasinoColors.FeltBottom));
        var glow = new Vector2(card.Center.X, card.Min.Y + card.Height * 0.35f);
        drawList.AddCircleFilled(glow, card.Width * 0.45f,
            ImGui.GetColorU32(new Vector4(1f, 0.9f, 0.7f, 0.05f)), 40);
        var inset = new Vector2(BulbInset * scale, BulbInset * scale);
        var bulbs = new Rect(card.Min + inset, card.Max - inset);
        CasinoLights.BulbChase(drawList, bulbs, MathF.Max(0f, rounding - inset.X), scale, phase,
            CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, lit);
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
            var height = TableRow.HeightOf(hostedViews[index]) * scale;
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
            using (ImRaii.PushId(index))
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
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (DeckActions.DrawSecondary(DeckActions.Slice(row, 0, 2, scale), Loc.T(L.Blackjack.PitBrowse), ui.Ink,
                true, "blackjack.pit.browse"))
        {
            openBrowser();
        }

        if (DeckActions.DrawSecondary(DeckActions.Slice(row, 1, 2, scale), Loc.T(L.Tables.HostTitle), ui.Ink, true,
                "blackjack.pit.host"))
        {
            openHostSheet();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + RowGap * scale));
    }
}
