using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class BlackjackHistorySheet
{
    private const float PanelHeightShare = 0.84f;
    private const float MiniCardWidth = 18f;
    private const float CardGap = 2f;
    private const float LabelShare = 0.3f;
    private const float ResultShare = 0.36f;
    private const int SideLineCapacity = 3;

    private readonly SheetSurface sheet = new("casino.blackjack.history");
    private readonly Action<Rect> drawSheetBody;
    private readonly BlackjackHandLog log;
    private readonly CasinoTextCache texts;
    private readonly RowText[] rows = new RowText[BlackjackHandLog.Capacity];

    private AppSkin skin = null!;
    private LanguageInfo? rowsLanguage;

    public BlackjackHistorySheet(BlackjackHandLog log, CasinoTextCache texts)
    {
        this.log = log;
        this.texts = texts;
        drawSheetBody = DrawSheetBody;
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = new RowText();
        }
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Blackjack.HistoryTitle), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##blackjackHistory", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawList(UiScale.Current);
        }
    }

    private void DrawList(float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        if (log.Count == 0)
        {
            var origin = ImGui.GetCursorScreenPos();
            var height = Typography.DrawWrappedLeft(origin, Loc.T(L.Blackjack.HistoryEmpty), skin.BodyInk,
                TextStyles.Footnote, width);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
            return;
        }

        RefreshLanguage();
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Grouped * scale;
        for (var offset = 0; offset < log.Count; offset++)
        {
            ref readonly var entry = ref log.Newest(offset);
            var row = TextFor(entry);
            var origin = ImGui.GetCursorScreenPos();
            var height = DrawRow(drawList, entry, row, origin, width, scale, false);
            skin.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + height), rounding);
            DrawRow(drawList, entry, row, origin, width, scale, true);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
        }
    }

    private float DrawRow(ImDrawListPtr drawList, in BlackjackHandEntry entry, RowText row, Vector2 origin,
        float width, float scale, bool draw)
    {
        var pad = Metrics.Space.Md * scale;
        var left = origin.X + pad;
        var inner = width - pad * 2f;
        var y = origin.Y + pad;
        y = DrawHeader(drawList, entry, row, left, y, inner, scale, draw);
        y += Metrics.Space.Sm * scale;
        var labelWidth = inner * LabelShare;
        y = DrawCardRow(drawList, row.DealerLabel, entry.DealerCards, row.Dealer, skin.BodyInk, left, y, inner,
            labelWidth, scale, draw);
        var hands = entry.Hands;
        var handCount = Math.Min(hands.Length, row.Results.Length);
        for (var handIndex = 0; handIndex < handCount; handIndex++)
        {
            var hand = hands[handIndex];
            y += Metrics.Space.Xs * scale;
            y = DrawCardRow(drawList, handIndex == 0 ? row.YouLabel : string.Empty,
                hand.Cards ?? Array.Empty<int>(), row.Results[handIndex], ResultInk(hand.Delta), left, y, inner,
                labelWidth, scale, draw);
        }

        for (var sideIndex = 0; sideIndex < row.SideCount; sideIndex++)
        {
            y += Metrics.Space.Xs * scale;
            var ink = row.SideWon[sideIndex] ? CasinoColors.Money : skin.BodyInk;
            y += draw
                ? Typography.DrawWrappedLeft(new Vector2(left, y), row.Sides[sideIndex], ink, TextStyles.Footnote,
                    inner)
                : Typography.MeasureWrappedBlock(row.Sides[sideIndex], TextStyles.Footnote, inner).Y;
        }

        return y + pad - origin.Y;
    }

    private float DrawHeader(ImDrawListPtr drawList, in BlackjackHandEntry entry, RowText row, float left, float y,
        float inner, float scale, bool draw)
    {
        var titleStyle = TextStyles.SubheadlineEmphasized;
        var netSize = Typography.Measure(row.Net, titleStyle);
        var titleWidth = MathF.Max(1f, inner - netSize.X - Metrics.Space.Md * scale);
        var clock = TimeText.Clock(entry.PlayedAtUnixSeconds);
        if (!draw)
        {
            return y + Typography.MeasureWrappedBlock(row.Title, titleStyle, titleWidth).Y
                + Typography.MeasureWrappedBlock(clock, TextStyles.Footnote, titleWidth).Y;
        }

        Typography.Draw(drawList, new Vector2(left + inner - netSize.X, y), row.Net,
            entry.Net > 0 ? CasinoColors.Money : skin.MutedInk, titleStyle);
        y += Typography.DrawWrappedLeft(new Vector2(left, y), row.Title, skin.TitleInk, titleStyle, titleWidth);
        return y + Typography.DrawWrappedLeft(new Vector2(left, y), clock, skin.MutedInk, TextStyles.Footnote,
            titleWidth);
    }

    private float DrawCardRow(ImDrawListPtr drawList, string label, int[] cards, string result, Vector4 resultInk,
        float left, float y, float inner, float labelWidth, float scale, bool draw)
    {
        var cardWidth = MiniCardWidth * scale;
        var cardHeight = PlayingCards.HeightFor(cardWidth);
        var labelHeight = label.Length > 0
            ? Typography.MeasureWrappedBlock(label, TextStyles.Footnote, labelWidth).Y
            : 0f;
        var height = MathF.Max(cardHeight, labelHeight);
        if (!draw)
        {
            return y + height;
        }

        var centerY = y + height * 0.5f;
        if (label.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, centerY - labelHeight * 0.5f), label, skin.MutedInk,
                TextStyles.Footnote, labelWidth);
        }

        var resultStyle = TextStyles.FootnoteEmphasized;
        var shown = Typography.FitText(result, inner * ResultShare, resultStyle);
        var resultSize = Typography.Measure(shown, resultStyle);
        Typography.Draw(drawList, new Vector2(left + inner - resultSize.X, centerY - resultSize.Y * 0.5f), shown,
            resultInk, resultStyle);
        var cardsLeft = left + labelWidth + Metrics.Space.Sm * scale;
        var cardsRight = left + inner - resultSize.X - Metrics.Space.Sm * scale;
        DrawCards(drawList, cards, cardsLeft, cardsRight, centerY, cardWidth, cardHeight, scale);
        return y + height;
    }

    private static void DrawCards(ImDrawListPtr drawList, int[] cards, float cardsLeft, float cardsRight,
        float centerY, float cardWidth, float cardHeight, float scale)
    {
        if (cards.Length == 0)
        {
            return;
        }

        var room = MathF.Max(cardWidth, cardsRight - cardsLeft);
        var step = cards.Length > 1
            ? MathF.Min(cardWidth + CardGap * scale, (room - cardWidth) / (cards.Length - 1))
            : 0f;
        var rounding = PlayingCards.RoundingFor(cardWidth);
        for (var index = 0; index < cards.Length; index++)
        {
            var min = new Vector2(cardsLeft + index * step, centerY - cardHeight * 0.5f);
            var rect = new Rect(min, new Vector2(min.X + cardWidth, min.Y + cardHeight));
            if (PlayingCards.IsCard(cards[index]))
            {
                PlayingCards.DrawFace(drawList, rect, cards[index], rounding, scale, true);
                continue;
            }

            PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
        }
    }

    private Vector4 ResultInk(long delta) =>
        delta > 0 ? CasinoColors.Money : delta < 0 ? skin.MutedInk : skin.BodyInk;

    private void RefreshLanguage()
    {
        if (ReferenceEquals(rowsLanguage, Loc.Current))
        {
            return;
        }

        rowsLanguage = Loc.Current;
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index].Sequence = -1;
        }
    }

    private RowText TextFor(in BlackjackHandEntry entry)
    {
        var row = rows[(int)(entry.Sequence % rows.Length)];
        if (row.Sequence == entry.Sequence)
        {
            return row;
        }

        row.Sequence = entry.Sequence;
        row.Title = texts.Number(L.Blackjack.TableHandLine, entry.HandIndex);
        row.Net = CasinoTextCache.SignedText(entry.Net);
        row.DealerLabel = Loc.T(L.Blackjack.HistoryDealer);
        row.YouLabel = Loc.T(L.Blackjack.HistoryYou);
        row.Dealer = entry.DealerTotal > BlackjackRules.TargetTotal
            ? Loc.T(L.Casino.BlackjackSeatBust)
            : GameNumber.Label(entry.DealerTotal);
        var hands = entry.Hands;
        for (var handIndex = 0; handIndex < row.Results.Length; handIndex++)
        {
            row.Results[handIndex] = handIndex < hands.Length ? ResultOf(hands[handIndex]) : string.Empty;
        }

        row.SideCount = 0;
        var recap = entry.Recap;
        AddSide(row, BlackjackSideBet.PerfectPairs, recap.PairsStake, recap.PairsKind, recap.PairsPaid,
            recap.PairsNet);
        AddSide(row, BlackjackSideBet.TwentyOnePlusThree, recap.ThreeStake, recap.ThreeKind, recap.ThreePaid,
            recap.ThreeNet);
        if (recap.Insurance > 0)
        {
            row.Sides[row.SideCount] = Loc.T(L.Blackjack.HistoryInsurance,
                CasinoTextCache.SignedText(recap.InsuranceWin - recap.Insurance));
            row.SideWon[row.SideCount] = recap.InsurancePaid;
            row.SideCount++;
        }

        return row;
    }

    private static void AddSide(RowText row, BlackjackSideBet bet, long stake, int kind, bool paid, long net)
    {
        if (stake <= 0)
        {
            return;
        }

        var name = Loc.T(BlackjackRecap.BetName(bet));
        row.Sides[row.SideCount] = paid && BlackjackRecap.HasKindName(bet, kind)
            ? Loc.T(L.Blackjack.HistorySideWon, name, Loc.T(BlackjackRecap.KindName(bet, kind)),
                NumberText.Signed(net))
            : Loc.T(L.Blackjack.HistorySideLost, name, NumberText.Group(stake));
        row.SideWon[row.SideCount] = paid;
        row.SideCount++;
    }

    private static string ResultOf(CasinoBlackjackHandDto hand)
    {
        if (hand.Surrendered || hand.Outcome == BlackjackOutcomes.Surrender)
        {
            return Loc.T(L.Blackjack.OutcomeSurrender);
        }

        if (hand.Outcome == BlackjackOutcomes.Blackjack)
        {
            return hand.Delta > 0
                ? Loc.T(L.Blackjack.NaturalPays, NumberText.Group(hand.Delta))
                : Loc.T(L.Casino.BlackjackSeatNatural);
        }

        if (hand.Outcome == BlackjackOutcomes.Push)
        {
            return Loc.T(L.Casino.BlackjackSeatPush);
        }

        if (hand.Outcome == BlackjackOutcomes.Bust)
        {
            return Loc.T(L.Casino.BlackjackSeatBust);
        }

        return CasinoTextCache.SignedText(hand.Delta);
    }

    private sealed class RowText
    {
        public readonly string[] Results = new string[BlackjackRules.MaxHandsPerSeat];
        public readonly string[] Sides = new string[SideLineCapacity];
        public readonly bool[] SideWon = new bool[SideLineCapacity];
        public long Sequence = -1;
        public string Title = string.Empty;
        public string Net = string.Empty;
        public string DealerLabel = string.Empty;
        public string YouLabel = string.Empty;
        public string Dealer = string.Empty;
        public int SideCount;
    }
}
