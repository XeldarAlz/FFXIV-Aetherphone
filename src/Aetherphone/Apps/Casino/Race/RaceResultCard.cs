using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceResultCard
{
    private const float WinnerSilk = 48f;
    private const float PlaceSilk = 28f;
    private const float ColumnGap = 12f;
    private const float ChevronSize = 5f;
    private const int Blocks = 6;

    private static readonly Vector4 CardFill = new(0.03f, 0.03f, 0.07f, 0.86f);
    private static readonly Vector4 CardRim = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 Hairline = new(1f, 1f, 1f, 0.07f);

    private readonly int[] paid = new int[RaceRules.MaxTickets];

    private float entrance;
    private long entranceRound = -1;
    private bool dividendsOpen;

    public void Reset()
    {
        entrance = 0f;
        entranceRound = -1;
        dividendsOpen = false;
    }

    public void Draw(ImDrawListPtr drawList, Rect safe, CasinoRaceRoomStateDto board, CasinoRaceRunnerDto[] runners,
        CasinoRaceBetsDto? bets, RaceTexts texts, float deltaSeconds, float scale)
    {
        var order = board.Order;
        if (order is not { Length: RaceRules.FieldSize })
        {
            return;
        }

        if (board.RoundIndex != entranceRound)
        {
            entranceRound = board.RoundIndex;
            entrance = 0f;
            dividendsOpen = false;
        }

        entrance = GameJuice.Advance(entrance, deltaSeconds);
        var tickets = bets?.Tickets ?? Array.Empty<CasinoRaceTicketDto>();
        var paidCount = CollectPaid(tickets);
        var results = board.Results ?? Array.Empty<CasinoRaceResultDto>();
        var dividendCount = Math.Min(results.Length, RaceRules.ResultRows);
        var headline = Headline(bets, texts, out var headlineInk, out var headlineStyle);
        var headlineLines = Typography.WrapText(headline, headlineStyle, RaceResultLayout.ContentWidthFor(safe, scale));
        var layout = RaceResultLayout.Compute(safe, headlineLines.Length, paidCount, dividendCount, dividendsOpen,
            scale);
        var card = layout.Card;
        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(CardFill));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(CardRim), MathF.Max(1f, scale));
        drawList.AddRectFilled(new Vector2(card.Min.X + radius, card.Min.Y),
            new Vector2(card.Max.X - radius, card.Min.Y + RaceResultLayout.AccentHeight * scale),
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.75f }));
        var viewport = layout.Viewport(scale);
        if (viewport.Height <= 1f)
        {
            return;
        }

        ImGui.SetCursorScreenPos(viewport.Min);
        using (ImRaii.Child("##raceResult", viewport.Size, false,
                   ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar))
        {
            var list = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            DrawHeadline(list, Shift(layout.Headline, origin), headlineLines, headlineInk, headlineStyle, Block(0));
            DrawMeta(list, Shift(layout.Meta, origin), board, texts, Block(1), scale);
            DrawWinner(list, Shift(layout.Winner, origin), runners[order[0]], texts, Block(2), scale);
            DrawPlace(list, Shift(layout.Second, origin), runners[order[1]], 1, texts, Block(3), scale);
            DrawPlace(list, Shift(layout.Third, origin), runners[order[2]], 2, texts, Block(3), scale);
            if (layout.HasPaid)
            {
                DrawSection(list, Shift(layout.PaidHeader, origin), Loc.T(L.Race.TicketsHeading), Block(4));
                for (var row = 0; row < paidCount; row++)
                {
                    var ticket = tickets[paid[row]];
                    DrawPaidRow(list, Shift(layout.PaidRow(row, scale), origin), texts.Ticket(tickets, paid[row]),
                        ticket.Payout, Block(4), scale);
                }
            }

            if (dividendCount > 0)
            {
                DrawToggle(list, Shift(layout.Toggle, origin), Block(5), scale);
                for (var row = 0; dividendsOpen && row < dividendCount; row++)
                {
                    DrawDividendRow(list, Shift(layout.DividendRow(row, scale), origin), texts.Result(results, row),
                        results[row].PayHundredths, scale);
                }
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(layout.ContentWidth, layout.ContentHeight));
        }
    }

    private int CollectPaid(CasinoRaceTicketDto[] tickets)
    {
        var count = 0;
        for (var index = 0; index < tickets.Length && count < paid.Length; index++)
        {
            if (tickets[index].Payout > 0)
            {
                paid[count] = index;
                count++;
            }
        }

        return count;
    }

    private float Block(int index) => GameJuice.Stagger(entrance, index, Blocks);

    private static Rect Shift(Rect rect, Vector2 origin) => rect.Translate(origin);

    private static string Headline(CasinoRaceBetsDto? bets, RaceTexts texts, out Vector4 ink, out TextStyle style)
    {
        ink = CasinoColors.InkBody;
        style = TextStyles.Title2;
        var tickets = bets?.Tickets;
        if (bets is null || tickets is null || tickets.Length == 0)
        {
            return Loc.T(L.Race.SatOut);
        }

        if (bets.MyPayout <= 0)
        {
            return Loc.T(L.Race.NoWin);
        }

        if (bets.MyPayout > bets.MyStake)
        {
            ink = CasinoColors.Money;
            style = TextStyles.Title1;
        }

        return texts.Won(bets.MyPayout);
    }

    private static void DrawHeadline(ImDrawListPtr drawList, Rect row, string[] lines, Vector4 ink, in TextStyle style,
        float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var count = Math.Min(lines.Length, RaceResultLayout.HeadlineMaxLines);
        var lineHeight = Typography.LineHeight(style);
        var top = row.Center.Y - lineHeight * count * 0.5f;
        for (var line = 0; line < count; line++)
        {
            Typography.Draw(drawList, new Vector2(row.Min.X, top + line * lineHeight), lines[line],
                ink with { W = alpha }, style);
        }
    }

    private static void DrawMeta(ImDrawListPtr drawList, Rect row, CasinoRaceRoomStateDto board, RaceTexts texts,
        float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var photoWidth = 0f;
        if (board.PhotoFinish)
        {
            var photoStyle = TextStyles.FootnoteEmphasized;
            photoWidth = RaceLine.DrawRight(drawList, row.Max.X, row.Center.Y, Loc.T(L.Race.PhotoFinish),
                row.Width * 0.5f, photoStyle, photoStyle, CasinoColors.LightB with { W = alpha }) + ColumnGap * scale;
        }

        var style = TextStyles.Footnote;
        RaceLine.DrawLeft(drawList, row.Min.X, row.Center.Y, texts.RaceNumber(board.RoundIndex),
            row.Width - photoWidth, style, style, CasinoColors.InkBody with { W = alpha });
    }

    private static void DrawWinner(ImDrawListPtr drawList, Rect row, CasinoRaceRunnerDto runner, RaceTexts texts,
        float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var popped = row.Scaled(0.9f + 0.1f * GameJuice.PopIn(alpha));
        var silk = MathF.Min(WinnerSilk * scale, popped.Height - 4f * scale);
        var silkMin = new Vector2(popped.Min.X + 2f * scale, popped.Center.Y - silk * 0.5f);
        RaceFieldList.DrawSilkBadge(drawList, silkMin, silk, runner, runner.Slot, alpha, scale);
        var left = silkMin.X + silk + ColumnGap * scale;
        var width = MathF.Max(1f, popped.Max.X - left);
        var labelStyle = TextStyles.FootnoteEmphasized;
        var nameStyle = TextStyles.Title2;
        var labelHeight = Typography.LineHeight(labelStyle);
        var nameHeight = Typography.LineHeight(nameStyle);
        var top = popped.Center.Y - (labelHeight + nameHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(texts.Place(0), width, labelStyle),
            CasinoColors.MoneyHighlight with { W = alpha }, labelStyle);
        Typography.Draw(drawList, new Vector2(left, top + labelHeight), Typography.FitText(runner.Name, width, nameStyle),
            CasinoColors.Money with { W = alpha }, nameStyle);
    }

    private static void DrawPlace(ImDrawListPtr drawList, Rect row, CasinoRaceRunnerDto runner, int place,
        RaceTexts texts, float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var silk = MathF.Min(PlaceSilk * scale, row.Height - 6f * scale);
        var silkMin = new Vector2(row.Min.X + 2f * scale + (WinnerSilk - PlaceSilk) * scale * 0.5f,
            row.Center.Y - silk * 0.5f);
        RaceFieldList.DrawSilkBadge(drawList, silkMin, silk, runner, runner.Slot, alpha, scale);
        var left = row.Min.X + 2f * scale + WinnerSilk * scale + ColumnGap * scale;
        var labelStyle = TextStyles.FootnoteEmphasized;
        var labelWidth = RaceLine.DrawLeft(drawList, left, row.Center.Y, texts.Place(place), row.Width * 0.3f,
            labelStyle, labelStyle, CasinoColors.InkBody with { W = alpha });
        var nameLeft = left + labelWidth + ColumnGap * scale;
        var nameStyle = TextStyles.Headline;
        Typography.Draw(drawList, new Vector2(nameLeft, row.Center.Y - Typography.LineHeight(nameStyle) * 0.5f),
            Typography.FitText(runner.Name, MathF.Max(1f, row.Max.X - nameLeft), nameStyle),
            CasinoColors.InkTitle with { W = alpha }, nameStyle);
    }

    private static void DrawSection(ImDrawListPtr drawList, Rect row, string title, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var style = TextStyles.FootnoteEmphasized;
        RaceLine.DrawLeft(drawList, row.Min.X, row.Center.Y, title, row.Width, style, style,
            CasinoColors.InkBody with { W = alpha });
    }

    private static void DrawPaidRow(ImDrawListPtr drawList, Rect row, string label, long payout, float alpha,
        float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        drawList.AddLine(row.Min, new Vector2(row.Max.X, row.Min.Y), ImGui.GetColorU32(Hairline), MathF.Max(1f, scale));
        var amountStyle = TextStyles.SubheadlineEmphasized;
        var amount = RaceAmounts.Text(payout);
        var amountWidth = RaceAmounts.Measure(amount, amountStyle).X;
        RaceAmounts.DrawRight(drawList, row.Max.X, row.Center.Y, amount, CasinoColors.Money with { W = alpha },
            amountStyle, alpha);
        RaceLine.DrawLeft(drawList, row.Min.X, row.Center.Y, label, row.Width - amountWidth - ColumnGap * scale,
            TextStyles.Subheadline, TextStyles.Footnote, CasinoColors.InkTitle with { W = alpha });
    }

    private void DrawToggle(ImDrawListPtr drawList, Rect row, float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            dividendsOpen = !dividendsOpen;
        }

        drawList.AddLine(row.Min, new Vector2(row.Max.X, row.Min.Y), ImGui.GetColorU32(Hairline), MathF.Max(1f, scale));
        var chevron = ChevronSize * scale;
        var chevronCenter = new Vector2(row.Max.X - chevron * 2f, row.Center.Y);
        var direction = dividendsOpen ? 1f : -1f;
        var tip = chevronCenter + new Vector2(0f, chevron * 0.5f * direction);
        var color = ImGui.GetColorU32(CasinoColors.InkBody with { W = alpha });
        var thickness = MathF.Max(1f, 1.6f * scale);
        drawList.AddLine(chevronCenter + new Vector2(-chevron, -chevron * 0.5f * direction), tip, color, thickness);
        drawList.AddLine(tip, chevronCenter + new Vector2(chevron, -chevron * 0.5f * direction), color, thickness);
        RaceLine.DrawLeft(drawList, row.Min.X, row.Center.Y, Loc.T(L.Race.AllDividends), row.Width - chevron * 4f,
            TextStyles.SubheadlineEmphasized, TextStyles.FootnoteEmphasized, CasinoColors.InkTitle with { W = alpha });
    }

    private static void DrawDividendRow(ImDrawListPtr drawList, Rect row, string label, long payHundredths,
        float scale)
    {
        var multipleStyle = TextStyles.FootnoteEmphasized;
        var multiple = CasinoMultiples.Label((int)Math.Min(int.MaxValue, payHundredths));
        var multipleWidth = Typography.Measure(multiple, multipleStyle).X;
        Typography.Draw(drawList,
            new Vector2(row.Max.X - multipleWidth, row.Center.Y - Typography.LineHeight(multipleStyle) * 0.5f),
            multiple, CasinoColors.Money, multipleStyle);
        RaceLine.DrawLeft(drawList, row.Min.X, row.Center.Y, label, row.Width - multipleWidth - ColumnGap * scale,
            TextStyles.Subheadline, TextStyles.Footnote, CasinoColors.InkBody);
    }
}
