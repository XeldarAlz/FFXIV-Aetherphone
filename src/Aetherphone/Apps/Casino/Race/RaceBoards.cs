using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceBoards
{
    public const float WideBoard = 420f;

    private const float Pad = 8f;
    private const float HeaderRow = 20f;
    private const float TicketRow = 20f;
    private const float TicketCell = 28f;
    private const int StripColumns = 3;
    private const float TicketPopSeconds = 0.35f;
    private const float ResultRow = 24f;

    private readonly float[] ticketAge = new float[RaceRules.MaxTickets];

    private float resultEntrance;
    private long resultRound = -1;
    private int ticketsSeen;
    private long ticketsRound = -1;

    public void Reset()
    {
        resultEntrance = 0f;
        resultRound = -1;
        ticketsSeen = 0;
        ticketsRound = -1;
        Array.Clear(ticketAge);
    }

    public void DrawTickets(ImDrawListPtr drawList, Rect rect, CasinoRaceTicketDto[]? tickets,
        RaceTexts texts, bool showPayout, bool compact, long roundIndex, float deltaSeconds, float scale)
    {
        var list = tickets ?? Array.Empty<CasinoRaceTicketDto>();
        TrackTicketPops(list.Length, roundIndex, deltaSeconds);
        var top = rect.Min.Y;
        if (!compact)
        {
            var heading = Loc.T(L.Race.TicketsHeading);
            var count = texts.Count(list.Length);
            var countWidth = Typography.Measure(count, TextStyles.Footnote).X;
            Typography.Draw(drawList, new Vector2(rect.Min.X, rect.Min.Y),
                Typography.FitText(heading, MathF.Max(1f, rect.Width - countWidth - Pad * scale),
                    TextStyles.FootnoteEmphasized), CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(rect.Max.X - countWidth, rect.Min.Y + 1f * scale), count,
                CasinoColors.InkBody, TextStyles.Footnote);
            top += HeaderRow * scale + 2f * scale;
        }

        if (list.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(rect.Min.X, top),
                Typography.FitText(Loc.T(L.Race.NoTickets), rect.Width, TextStyles.Footnote), CasinoColors.InkBody,
                TextStyles.Footnote);
            return;
        }

        if (compact)
        {
            DrawTicketGrid(drawList, new Rect(new Vector2(rect.Min.X, top), rect.Max), list, texts, showPayout,
                scale);
            return;
        }

        var rowHeight = TicketRow * scale;
        for (var index = 0; index < list.Length && index < RaceRules.MaxTickets; index++)
        {
            if (top + rowHeight > rect.Max.Y + 1f)
            {
                break;
            }

            var pop = Pop(index);
            var row = new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, top + rowHeight));
            DrawTicketRow(drawList, row, list[index], texts.Ticket(list, index), showPayout, pop, scale);
            top += rowHeight;
        }
    }

    public void DrawResult(ImDrawListPtr drawList, Rect rect, CasinoRaceRoomStateDto board,
        CasinoRaceRunnerDto[] runners, RaceTexts texts, long roundIndex, float phase, float deltaSeconds, float scale)
    {
        if (roundIndex != resultRound)
        {
            resultRound = roundIndex;
            resultEntrance = 0f;
        }

        resultEntrance = GameJuice.Advance(resultEntrance, deltaSeconds);
        var pad = Pad * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Card * scale,
            ImGui.GetColorU32(new Vector4(0.02f, 0.03f, 0.06f, 0.72f)));
        CasinoLights.BulbChase(drawList, rect, Metrics.Radius.Card * scale, scale, phase,
            CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, 0.55f);
        var inner = rect.Inset(pad * 2f);
        var title = Loc.T(L.Race.ResultTitle);
        var race = texts.RaceNumber(roundIndex);
        var raceWidth = Typography.Measure(race, TextStyles.Footnote).X;
        Typography.Draw(drawList, inner.Min,
            Typography.FitText(title, MathF.Max(1f, inner.Width - raceWidth - pad), TextStyles.Title3),
            CasinoColors.InkTitle, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(inner.Max.X - raceWidth, inner.Min.Y + 4f * scale), race,
            CasinoColors.InkBody, TextStyles.Footnote);
        var top = inner.Min.Y + Typography.LineHeight(TextStyles.Title3) + pad;
        var order = board.Order;
        if (order is not { Length: RaceRules.FieldSize })
        {
            return;
        }

        var podiumHeight = MathF.Min(30f * scale, inner.Height * 0.14f);
        for (var place = 0; place < RaceRules.PlacesPaid; place++)
        {
            var share = GameJuice.Stagger(resultEntrance, place, RaceRules.PlacesPaid + RaceRules.ResultRows);
            var row = new Rect(new Vector2(inner.Min.X, top), new Vector2(inner.Max.X, top + podiumHeight));
            DrawPodiumRow(drawList, row, place, runners[order[place]], texts, share, board.PhotoFinish && place < 2,
                scale);
            top += podiumHeight + 2f * scale;
        }

        top += pad * 0.5f;
        Typography.Draw(drawList, new Vector2(inner.Min.X, top),
            Typography.FitText(Loc.T(L.Race.Dividends), inner.Width, TextStyles.FootnoteEmphasized),
            CasinoColors.InkBody, TextStyles.FootnoteEmphasized);
        top += Typography.LineHeight(TextStyles.FootnoteEmphasized) + 2f * scale;
        var results = board.Results;
        if (results is null || results.Length == 0)
        {
            return;
        }

        var twoColumns = inner.Width >= WideBoard * scale;
        var columnWidth = twoColumns ? (inner.Width - pad) * 0.5f : inner.Width;
        var rowHeight = MathF.Min(ResultRow * scale,
            MathF.Max(1f, (inner.Max.Y - top) / (twoColumns ? 3f : RaceRules.ResultRows)));
        for (var index = 0; index < results.Length && index < RaceRules.ResultRows; index++)
        {
            var column = twoColumns ? index / 3 : 0;
            var line = twoColumns ? index % 3 : index;
            var left = inner.Min.X + column * (columnWidth + pad);
            var rowTop = top + line * rowHeight;
            if (rowTop + rowHeight > inner.Max.Y + 1f)
            {
                break;
            }

            var share = GameJuice.Stagger(resultEntrance, RaceRules.PlacesPaid + index,
                RaceRules.PlacesPaid + RaceRules.ResultRows);
            DrawDividend(drawList, new Rect(new Vector2(left, rowTop), new Vector2(left + columnWidth, rowTop + rowHeight)),
                texts.Result(results, index), results[index].PayHundredths, share, scale);
        }
    }

    public static void DrawTotal(ImDrawListPtr drawList, Rect row, CasinoRaceBetsDto? bets, RaceTexts texts,
        float scale)
    {
        var tickets = bets?.Tickets;
        string text;
        Vector4 ink;
        if (tickets is null || tickets.Length == 0)
        {
            text = Loc.T(L.Race.SatOut);
            ink = CasinoColors.InkMuted;
        }
        else if (bets!.MyPayout > 0)
        {
            text = texts.Won(bets.MyPayout);
            ink = bets.MyPayout > bets.MyStake ? CasinoColors.Money : CasinoColors.InkBody;
        }
        else
        {
            text = Loc.T(L.Race.NoReturn);
            ink = CasinoColors.InkMuted;
        }

        var style = TextStyles.SubheadlineEmphasized;
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - Typography.LineHeight(style) * 0.5f),
            Typography.FitText(text, row.Width, style), ink, style);
    }

    private void TrackTicketPops(int count, long roundIndex, float deltaSeconds)
    {
        if (roundIndex != ticketsRound)
        {
            ticketsRound = roundIndex;
            ticketsSeen = count;
            for (var index = 0; index < ticketAge.Length; index++)
            {
                ticketAge[index] = TicketPopSeconds;
            }
        }

        for (var index = ticketsSeen; index < count && index < ticketAge.Length; index++)
        {
            ticketAge[index] = 0f;
        }

        ticketsSeen = Math.Max(ticketsSeen, count);
        if (count < ticketsSeen)
        {
            ticketsSeen = count;
        }

        for (var index = 0; index < ticketAge.Length; index++)
        {
            ticketAge[index] = MathF.Min(TicketPopSeconds, ticketAge[index] + deltaSeconds);
        }
    }

    private float Pop(int index) => GameJuice.PopIn(ticketAge[index] / TicketPopSeconds);

    private void DrawTicketGrid(ImDrawListPtr drawList, Rect rect, CasinoRaceTicketDto[] list,
        RaceTexts texts, bool showPayout, float scale)
    {
        var gap = 4f * scale;
        var cellWidth = (rect.Width - gap * (StripColumns - 1)) / StripColumns;
        var cellHeight = MathF.Min(TicketCell * scale, (rect.Height - gap) * 0.5f);
        for (var index = 0; index < list.Length && index < RaceRules.MaxTickets; index++)
        {
            var column = index % StripColumns;
            var line = index / StripColumns;
            var min = new Vector2(rect.Min.X + column * (cellWidth + gap), rect.Min.Y + line * (cellHeight + gap));
            var cell = new Rect(min, min + new Vector2(cellWidth, cellHeight));
            var pop = Pop(index);
            var scaled = cell.Scaled(0.6f + 0.4f * pop);
            var ticket = list[index];
            var won = showPayout && ticket.Payout > 0;
            Squircle.Fill(drawList, scaled.Min, scaled.Max, scaled.Height * 0.5f,
                ImGui.GetColorU32(won ? CasinoColors.Money with { W = 0.22f } : new Vector4(0f, 0f, 0f, 0.4f)));
            if (won)
            {
                Squircle.Stroke(drawList, scaled.Min, scaled.Max, scaled.Height * 0.5f,
                    ImGui.GetColorU32(CasinoColors.Money), MathF.Max(1f, scale));
            }

            var amount = NumberText.Compact(showPayout ? ticket.Payout : ticket.Amount);
            var amountStyle = TextStyles.FootnoteEmphasized;
            var amountWidth = Typography.Measure(amount, amountStyle).X;
            var padX = scaled.Height * 0.4f;
            var labelWidth = MathF.Max(1f, scaled.Width - padX * 2f - amountWidth - 4f * scale);
            var labelStyle = TextStyles.Footnote;
            Typography.Draw(drawList,
                new Vector2(scaled.Min.X + padX, scaled.Center.Y - Typography.LineHeight(labelStyle) * 0.5f),
                Typography.FitText(texts.Ticket(list, index), labelWidth, labelStyle), CasinoColors.InkBody,
                labelStyle);
            Typography.Draw(drawList,
                new Vector2(scaled.Max.X - padX - amountWidth,
                    scaled.Center.Y - Typography.LineHeight(amountStyle) * 0.5f), amount,
                showPayout && ticket.Payout <= 0 ? CasinoColors.InkMuted : CasinoColors.Money, amountStyle);
        }
    }

    private static void DrawTicketRow(ImDrawListPtr drawList, Rect row, CasinoRaceTicketDto ticket, string label,
        bool showPayout, float pop, float scale)
    {
        var style = TextStyles.Footnote;
        var lineHeight = Typography.LineHeight(style);
        var top = row.Center.Y - lineHeight * 0.5f;
        var offset = (1f - pop) * 12f * scale;
        var stake = NumberText.Compact(ticket.Amount);
        var stakeSize = CurrencyGlyph.MeasureAmount(stake, style);
        var payoutWidth = 0f;
        if (showPayout)
        {
            var payout = ticket.Payout > 0 ? NumberText.Compact(ticket.Payout) : "-";
            payoutWidth = Typography.Measure(payout, TextStyles.FootnoteEmphasized).X;
            Typography.Draw(drawList, new Vector2(row.Max.X - payoutWidth + offset, top), payout,
                ticket.Payout > 0 ? CasinoColors.Money : CasinoColors.InkMuted, TextStyles.FootnoteEmphasized);
            payoutWidth += 8f * scale;
        }

        var stakeLeft = row.Max.X - payoutWidth - stakeSize.X + offset;
        CurrencyGlyph.DrawAmount(drawList, new Vector2(stakeLeft, top), stake, CurrencyKind.Chips,
            showPayout ? CasinoColors.InkMuted : CasinoColors.Money, style, pop);
        Typography.Draw(drawList, new Vector2(row.Min.X + offset, top),
            Typography.FitText(label, MathF.Max(1f, stakeLeft - row.Min.X - 6f * scale), style),
            CasinoColors.InkBody with { W = pop }, style);
    }

    private static void DrawPodiumRow(ImDrawListPtr drawList, Rect row, int place, CasinoRaceRunnerDto runner,
        RaceTexts texts, float share, bool photo, float scale)
    {
        if (share <= 0f)
        {
            return;
        }

        var popped = row.Scaled(0.85f + 0.15f * GameJuice.PopIn(share));
        var ink = place == 0 ? CasinoColors.Money : CasinoColors.InkTitle;
        var placeStyle = TextStyles.FootnoteEmphasized;
        var label = texts.Place(place);
        var labelWidth = Typography.Measure(label, placeStyle).X;
        Typography.Draw(drawList, new Vector2(popped.Min.X, popped.Center.Y - Typography.LineHeight(placeStyle) * 0.5f),
            label, ink with { W = share }, placeStyle);
        var discRadius = popped.Height * 0.36f;
        var discCenter = new Vector2(popped.Min.X + labelWidth + 8f * scale + discRadius, popped.Center.Y);
        var cloth = RaceBirdArt.ClothOf(runner.Slot);
        drawList.AddCircleFilled(discCenter, discRadius + 1.5f * scale,
            ImGui.GetColorU32(RaceBirdArt.PlumageOf(runner.Colour) with { W = share }), 18);
        drawList.AddCircleFilled(discCenter, discRadius, ImGui.GetColorU32(cloth with { W = share }), 18);
        Typography.DrawCentered(drawList, discCenter, GameNumber.Label(runner.Slot + 1),
            RaceBirdArt.InkOn(cloth) with { W = share }, TextStyles.FootnoteEmphasized);
        var nameLeft = discCenter.X + discRadius + 8f * scale;
        var nameStyle = place == 0 ? TextStyles.Headline : TextStyles.SubheadlineEmphasized;
        var photoText = photo ? Loc.T(L.Race.PhotoFinish) : string.Empty;
        var photoWidth = photo ? Typography.Measure(photoText, TextStyles.Footnote).X + 6f * scale : 0f;
        Typography.Draw(drawList, new Vector2(nameLeft, popped.Center.Y - Typography.LineHeight(nameStyle) * 0.5f),
            Typography.FitText(runner.Name, MathF.Max(1f, popped.Max.X - nameLeft - photoWidth), nameStyle),
            ink with { W = share }, nameStyle);
        if (photo && place == 0)
        {
            Typography.Draw(drawList,
                new Vector2(popped.Max.X - photoWidth + 6f * scale,
                    popped.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f), photoText,
                CasinoColors.LightB with { W = share }, TextStyles.Footnote);
        }
    }

    private static void DrawDividend(ImDrawListPtr drawList, Rect row, string label, long payHundredths,
        float share, float scale)
    {
        if (share <= 0f)
        {
            return;
        }

        var style = TextStyles.Footnote;
        var top = row.Center.Y - Typography.LineHeight(style) * 0.5f;
        var multiple = CasinoMultiples.Label((int)Math.Min(int.MaxValue, payHundredths));
        var multipleWidth = Typography.Measure(multiple, TextStyles.FootnoteEmphasized).X;
        var slide = (1f - share) * 10f * scale;
        Typography.Draw(drawList, new Vector2(row.Max.X - multipleWidth + slide, top), multiple,
            CasinoColors.Money with { W = share }, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(row.Min.X + slide, top),
            Typography.FitText(label, MathF.Max(1f, row.Width - multipleWidth - 8f * scale), style),
            CasinoColors.InkBody with { W = share }, style);
    }
}
