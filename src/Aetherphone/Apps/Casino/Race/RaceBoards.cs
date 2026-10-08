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
    private const float HeaderRow = 18f;
    private const float MaxRow = 46f;
    private const float PoolColumn = 62f;
    private const float PlaceColumn = 50f;
    private const float OddsColumn = 58f;
    private const float FormColumn = 58f;
    private const float SwatchSize = 24f;
    private const float PipRadius = 2.2f;
    private const float PipGap = 3.4f;
    private const float SlideShare = 0.35f;
    private const float TicketRow = 20f;
    private const float TicketCell = 24f;
    private const int StripColumns = 3;
    private const float TicketPopSeconds = 0.35f;
    private const float ResultRow = 21f;

    private readonly float[] ticketAge = new float[RaceRules.MaxTickets];

    private float toteEntrance;
    private long toteRound = -1;
    private float resultEntrance;
    private long resultRound = -1;
    private int ticketsSeen;
    private long ticketsRound = -1;

    public void Reset()
    {
        toteEntrance = 0f;
        toteRound = -1;
        resultEntrance = 0f;
        resultRound = -1;
        ticketsSeen = 0;
        ticketsRound = -1;
        Array.Clear(ticketAge);
    }

    public int DrawTote(ImDrawListPtr drawList, AppSkin ui, Rect rect, CasinoRaceRunnerDto[] runners,
        RaceTicketBuilder builder, RaceTexts texts, bool selectable, long roundIndex, float deltaSeconds, float scale)
    {
        if (roundIndex != toteRound)
        {
            toteRound = roundIndex;
            toteEntrance = 0f;
        }

        toteEntrance = GameJuice.Advance(toteEntrance, deltaSeconds);
        var wide = rect.Width >= WideBoard * scale;
        var headerHeight = HeaderRow * scale;
        var rowHeight = MathF.Min(MaxRow * scale, (rect.Height - headerHeight) / RaceRules.FieldSize);
        if (rowHeight <= 4f * scale)
        {
            return -1;
        }

        var columns = Columns.For(rect, wide, scale);
        DrawToteHeader(drawList, new Rect(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + headerHeight)), columns,
            wide, scale);
        var maxPool = 1L;
        for (var slot = 0; slot < runners.Length; slot++)
        {
            maxPool = Math.Max(maxPool, runners[slot].Pool);
        }

        var tapped = -1;
        var top = rect.Min.Y + headerHeight;
        for (var slot = 0; slot < RaceRules.FieldSize && slot < runners.Length; slot++)
        {
            var share = GameJuice.Stagger(toteEntrance, slot, RaceRules.FieldSize);
            var slide = (1f - GameJuice.PopIn(share)) * rect.Width * SlideShare;
            var row = new Rect(new Vector2(rect.Min.X + slide, top), new Vector2(rect.Max.X + slide, top + rowHeight));
            if (DrawRunnerRow(drawList, ui, row, runners, slot, builder.PickOf(slot), columns.Shift(slide), wide,
                    maxPool, texts, selectable && share >= 1f, MathF.Min(1f, share * 1.5f), scale))
            {
                tapped = slot;
            }

            top += rowHeight;
        }

        return tapped;
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
            var countWidth = Typography.Measure(count, TextStyles.Caption1).X;
            Typography.Draw(drawList, new Vector2(rect.Min.X, rect.Min.Y),
                Typography.FitText(heading, MathF.Max(1f, rect.Width - countWidth - Pad * scale),
                    TextStyles.FootnoteEmphasized), CasinoColors.InkTitle, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(rect.Max.X - countWidth, rect.Min.Y + 1f * scale), count,
                CasinoColors.InkMuted, TextStyles.Caption1);
            top += HeaderRow * scale + 2f * scale;
        }

        if (list.Length == 0)
        {
            Typography.Draw(drawList, new Vector2(rect.Min.X, top),
                Typography.FitText(Loc.T(L.Race.NoTickets), rect.Width, TextStyles.Caption1), CasinoColors.InkMuted,
                TextStyles.Caption1);
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
        var raceWidth = Typography.Measure(race, TextStyles.Caption1).X;
        Typography.Draw(drawList, inner.Min,
            Typography.FitText(title, MathF.Max(1f, inner.Width - raceWidth - pad), TextStyles.Title3),
            CasinoColors.InkTitle, TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(inner.Max.X - raceWidth, inner.Min.Y + 4f * scale), race,
            CasinoColors.InkMuted, TextStyles.Caption1);
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
            CasinoColors.InkMuted, TextStyles.FootnoteEmphasized);
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

    private static void DrawToteHeader(ImDrawListPtr drawList, Rect row, in Columns columns, bool wide, float scale)
    {
        var style = TextStyles.Caption2;
        var ink = CasinoColors.InkMuted;
        var top = row.Center.Y - Typography.LineHeight(style) * 0.5f;
        Typography.Draw(drawList, new Vector2(columns.NameLeft, top),
            Typography.FitText(Loc.T(L.Race.ColumnRunner), MathF.Max(1f, columns.NameRight - columns.NameLeft), style),
            ink, style);
        if (wide)
        {
            HeaderCell(drawList, Loc.T(L.Race.ColumnForm), columns.FormCenter, FormColumn * scale, top, style, ink);
            HeaderCell(drawList, Loc.T(L.Race.KindPlace), columns.PlaceCenter, PlaceColumn * scale, top, style, ink);
        }

        HeaderCell(drawList, Loc.T(L.Race.ColumnOdds), columns.OddsCenter, OddsColumn * scale, top, style, ink);
        HeaderCell(drawList, Loc.T(L.Race.ColumnPool), columns.PoolCenter, PoolColumn * scale, top, style, ink);
    }

    private static void HeaderCell(ImDrawListPtr drawList, string text, float centerX, float width, float top,
        in TextStyle style, Vector4 ink)
    {
        var fitted = Typography.FitText(text, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, style);
    }

    private static bool DrawRunnerRow(ImDrawListPtr drawList, AppSkin ui, Rect row, CasinoRaceRunnerDto[] runners,
        int slot, int pick, in Columns columns, bool wide, long maxPool, RaceTexts texts, bool selectable,
        float alpha, float scale)
    {
        var runner = runners[slot];
        var inset = new Rect(row.Min + new Vector2(0f, 1.5f * scale), row.Max - new Vector2(0f, 1.5f * scale));
        var radius = MathF.Min(10f * scale, inset.Height * 0.3f);
        var hovered = selectable && UiInteract.Hover(inset.Min, inset.Max);
        var picked = pick >= 0;
        var fill = picked
            ? Palette.Mix(new Vector4(0.05f, 0.05f, 0.09f, 0.78f), ui.Palette.Accent, 0.28f)
            : new Vector4(0.03f, 0.04f, 0.08f, hovered ? 0.72f : 0.55f);
        Squircle.Fill(drawList, inset.Min, inset.Max, radius, ImGui.GetColorU32(fill with { W = fill.W * alpha }));
        if (picked)
        {
            Squircle.Stroke(drawList, inset.Min, inset.Max, radius, ImGui.GetColorU32(ui.Palette.Accent with { W = alpha }),
                1.4f * scale);
        }

        var share = runner.Pool <= 0 ? 0f : runner.Pool / (float)maxPool;
        if (share > 0f)
        {
            var barLeft = inset.Min.X + radius;
            var barWidth = (inset.Width - radius * 2f) * share;
            drawList.AddLine(new Vector2(barLeft, inset.Max.Y - 1f * scale),
                new Vector2(barLeft + barWidth, inset.Max.Y - 1f * scale),
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.45f * alpha }), MathF.Max(1f, 2f * scale));
        }

        var swatch = MathF.Min(inset.Height * 0.66f, SwatchSize * scale);
        var swatchMin = new Vector2(inset.Min.X + Pad * scale, inset.Center.Y - swatch * 0.5f);
        var swatchMax = swatchMin + new Vector2(swatch, swatch);
        var frame = 2f * scale;
        var cloth = RaceBirdArt.ClothOf(slot);
        Squircle.Fill(drawList, swatchMin - new Vector2(frame, frame), swatchMax + new Vector2(frame, frame),
            swatch * 0.3f, ImGui.GetColorU32(RaceBirdArt.PlumageOf(runner.Colour) with { W = alpha }));
        RaceBirdArt.DrawSilk(drawList, swatchMin, swatchMax, slot, runner.Silk, alpha);
        var center = (swatchMin + swatchMax) * 0.5f;
        var number = GameNumber.Label(slot + 1);
        var ink = RaceBirdArt.InkOn(cloth);
        Typography.DrawCentered(drawList, center + new Vector2(0f, 1f * scale), number,
            RaceBirdArt.InkOn(ink) with { W = alpha * 0.6f }, TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, center, number, ink with { W = alpha }, TextStyles.FootnoteEmphasized);
        if (picked)
        {
            DrawPickBadge(drawList, ui, new Vector2(swatchMax.X, inset.Min.Y + 1f * scale), texts.Place(pick), alpha,
                scale);
        }

        var nameWidth = MathF.Max(1f, columns.NameRight - columns.NameLeft);
        var nameStyle = TextStyles.SubheadlineEmphasized;
        var subStyle = TextStyles.Caption2;
        var nameHeight = Typography.LineHeight(nameStyle);
        var subHeight = Typography.LineHeight(subStyle);
        var blockTop = inset.Center.Y - (nameHeight + subHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(columns.NameLeft, blockTop),
            Typography.FitText(runner.Name, nameWidth, nameStyle), CasinoColors.InkTitle with { W = alpha }, nameStyle);
        var subTop = blockTop + nameHeight;
        var pipsWidth = DrawRating(drawList, new Vector2(columns.NameLeft, subTop + subHeight * 0.5f), runner.Rating,
            alpha, scale);
        var subLeft = columns.NameLeft + pipsWidth + 6f * scale;
        var form = texts.Form(runners, slot);
        var subText = wide ? texts.Backers(slot, runner.Backers) : form;
        Typography.Draw(drawList, new Vector2(subLeft, subTop),
            Typography.FitText(subText, MathF.Max(1f, columns.NameRight - subLeft), subStyle),
            CasinoColors.InkMuted with { W = alpha }, subStyle);
        if (wide)
        {
            ValueCell(drawList, form, columns.FormCenter, FormColumn * scale,
                inset.Center.Y, CasinoColors.InkBody with { W = alpha }, TextStyles.Footnote);
            ValueCell(drawList, RaceTexts.Odds(runner.PlaceOddsHundredths), columns.PlaceCenter, PlaceColumn * scale,
                inset.Center.Y, CasinoColors.InkBody with { W = alpha }, TextStyles.Footnote);
            ValueCell(drawList, RaceTexts.Odds(runner.OddsHundredths), columns.OddsCenter, OddsColumn * scale,
                inset.Center.Y, CasinoColors.Money with { W = alpha }, TextStyles.Headline);
            ValueCell(drawList, NumberText.Compact(runner.Pool), columns.PoolCenter, PoolColumn * scale,
                inset.Center.Y, CasinoColors.InkBody with { W = alpha }, TextStyles.Footnote);
        }
        else
        {
            var oddsHeight = Typography.LineHeight(TextStyles.Headline);
            var stackTop = inset.Center.Y - (oddsHeight + subHeight) * 0.5f;
            ValueCell(drawList, RaceTexts.Odds(runner.OddsHundredths), columns.OddsCenter, OddsColumn * scale,
                stackTop + oddsHeight * 0.5f, CasinoColors.Money with { W = alpha }, TextStyles.Headline);
            ValueCell(drawList, RaceTexts.Odds(runner.PlaceOddsHundredths), columns.OddsCenter, OddsColumn * scale,
                stackTop + oddsHeight + subHeight * 0.5f, CasinoColors.InkMuted with { W = alpha }, subStyle);
            var poolHeight = Typography.LineHeight(TextStyles.Footnote);
            var poolTop = inset.Center.Y - (poolHeight + subHeight) * 0.5f;
            ValueCell(drawList, NumberText.Compact(runner.Pool), columns.PoolCenter, PoolColumn * scale,
                poolTop + poolHeight * 0.5f, CasinoColors.InkBody with { W = alpha }, TextStyles.Footnote);
            ValueCell(drawList, texts.Backers(slot, runner.Backers), columns.PoolCenter, PoolColumn * scale,
                poolTop + poolHeight + subHeight * 0.5f, CasinoColors.InkMuted with { W = alpha }, subStyle);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return selectable && UiInteract.Click(inset.Min, inset.Max, hovered);
    }

    private static void DrawPickBadge(ImDrawListPtr drawList, AppSkin ui, Vector2 anchor, string label, float alpha,
        float scale)
    {
        var style = TextStyles.Caption2;
        var size = Typography.Measure(label, style);
        var height = size.Y + 2f * scale;
        var min = new Vector2(anchor.X - size.X * 0.5f - 4f * scale, anchor.Y);
        var max = new Vector2(anchor.X + size.X * 0.5f + 4f * scale, anchor.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(ui.Palette.Accent with { W = alpha }));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, new Vector4(1f, 1f, 1f, alpha), style);
    }

    private static float DrawRating(ImDrawListPtr drawList, Vector2 leftCenter, int rating, float alpha, float scale)
    {
        var radius = PipRadius * scale;
        var step = radius * 2f + PipGap * scale - radius;
        for (var pip = 0; pip < RaceRules.MaxRating; pip++)
        {
            var center = new Vector2(leftCenter.X + radius + pip * step, leftCenter.Y);
            var lit = pip < rating;
            drawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32((lit ? CasinoColors.Money : CasinoColors.InkMuted with { W = 0.35f }) with
                {
                    W = (lit ? 1f : 0.35f) * alpha,
                }), 8);
        }

        return radius * 2f + step * (RaceRules.MaxRating - 1);
    }

    private static void ValueCell(ImDrawListPtr drawList, string text, float centerX, float width, float centerY,
        Vector4 ink, in TextStyle style)
    {
        var fitted = Typography.FitText(text, width, style);
        var size = Typography.Measure(fitted, style);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, centerY - size.Y * 0.5f), fitted, ink, style);
    }

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
            var amountStyle = TextStyles.Caption1;
            var amountWidth = Typography.Measure(amount, amountStyle).X;
            var padX = scaled.Height * 0.4f;
            var labelWidth = MathF.Max(1f, scaled.Width - padX * 2f - amountWidth - 4f * scale);
            var labelStyle = TextStyles.Caption2;
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
            RaceBirdArt.InkOn(cloth) with { W = share }, TextStyles.Caption1);
        var nameLeft = discCenter.X + discRadius + 8f * scale;
        var nameStyle = place == 0 ? TextStyles.Headline : TextStyles.SubheadlineEmphasized;
        var photoText = photo ? Loc.T(L.Race.PhotoFinish) : string.Empty;
        var photoWidth = photo ? Typography.Measure(photoText, TextStyles.Caption2).X + 6f * scale : 0f;
        Typography.Draw(drawList, new Vector2(nameLeft, popped.Center.Y - Typography.LineHeight(nameStyle) * 0.5f),
            Typography.FitText(runner.Name, MathF.Max(1f, popped.Max.X - nameLeft - photoWidth), nameStyle),
            ink with { W = share }, nameStyle);
        if (photo && place == 0)
        {
            Typography.Draw(drawList,
                new Vector2(popped.Max.X - photoWidth + 6f * scale,
                    popped.Center.Y - Typography.LineHeight(TextStyles.Caption2) * 0.5f), photoText,
                CasinoColors.LightB with { W = share }, TextStyles.Caption2);
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

    private readonly struct Columns
    {
        public readonly float NameLeft;
        public readonly float NameRight;
        public readonly float FormCenter;
        public readonly float PlaceCenter;
        public readonly float OddsCenter;
        public readonly float PoolCenter;

        private Columns(float nameLeft, float nameRight, float formCenter, float placeCenter, float oddsCenter,
            float poolCenter)
        {
            NameLeft = nameLeft;
            NameRight = nameRight;
            FormCenter = formCenter;
            PlaceCenter = placeCenter;
            OddsCenter = oddsCenter;
            PoolCenter = poolCenter;
        }

        public Columns Shift(float offset) => new(NameLeft + offset, NameRight + offset, FormCenter + offset,
            PlaceCenter + offset, OddsCenter + offset, PoolCenter + offset);

        public static Columns For(Rect rect, bool wide, float scale)
        {
            var pad = Pad * scale;
            var right = rect.Max.X - pad;
            var poolCenter = right - PoolColumn * scale * 0.5f;
            right -= PoolColumn * scale;
            var placeCenter = right - PlaceColumn * scale * 0.5f;
            if (wide)
            {
                right -= PlaceColumn * scale;
            }

            var oddsCenter = right - OddsColumn * scale * 0.5f;
            right -= OddsColumn * scale;
            var formCenter = right - FormColumn * scale * 0.5f;
            if (wide)
            {
                right -= FormColumn * scale;
            }

            var nameLeft = rect.Min.X + pad + 30f * scale;
            return new Columns(nameLeft, MathF.Max(nameLeft, right - pad), formCenter, placeCenter, oddsCenter,
                poolCenter);
        }
    }
}
