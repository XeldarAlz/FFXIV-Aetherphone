using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal sealed class RaceTicketStrip
{
    private const float Pad = 12f;
    private const float ColumnGap = 10f;
    private const float ChevronSize = 5f;
    private const float PopSeconds = 0.35f;

    private static readonly Vector4 StripFill = new(0.03f, 0.03f, 0.07f, 0.78f);
    private static readonly Vector4 StripRim = new(1f, 1f, 1f, 0.08f);

    private readonly float[] age = new float[RaceRules.MaxTickets];

    private int seen;
    private long seenRound = -1;

    public bool Expanded { get; private set; } = true;

    public void Reset()
    {
        seen = 0;
        seenRound = -1;
        Expanded = true;
        Array.Clear(age);
    }

    public void Draw(ImDrawListPtr drawList, Rect rect, int rows, CasinoRaceTicketDto[] tickets,
        CasinoRaceRunnerDto[] runners, RaceTexts texts, long roundIndex, float deltaSeconds, float scale)
    {
        Track(tickets.Length, roundIndex, deltaSeconds);
        if (rect.Height <= 0f)
        {
            return;
        }

        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(StripFill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(StripRim), MathF.Max(1f, scale));
        var header = new Rect(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + RaceOpenLayout.StripHeader * scale));
        DrawHeader(drawList, header, tickets, texts, scale);
        var top = header.Max.Y;
        var rowHeight = RaceOpenLayout.StripRow * scale;
        for (var index = 0; index < rows && index < tickets.Length; index++)
        {
            var row = new Rect(new Vector2(rect.Min.X, top), new Vector2(rect.Max.X, top + rowHeight));
            DrawRow(drawList, row, tickets, runners, texts, index, Pop(index), scale);
            top += rowHeight;
        }
    }

    private void DrawHeader(ImDrawListPtr drawList, Rect header, CasinoRaceTicketDto[] tickets, RaceTexts texts,
        float scale)
    {
        var hovered = UiInteract.Hover(header.Min, header.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(header.Min, header.Max, hovered))
        {
            Expanded = !Expanded;
        }

        var pad = Pad * scale;
        var chevron = ChevronSize * scale;
        var chevronCenter = new Vector2(header.Max.X - pad - chevron, header.Center.Y);
        DrawChevron(drawList, chevronCenter, chevron, Expanded, scale);
        var total = 0L;
        for (var index = 0; index < tickets.Length; index++)
        {
            total += tickets[index].Amount;
        }

        var totalStyle = TextStyles.SubheadlineEmphasized;
        var totalText = RaceAmounts.Text(total);
        var totalRight = chevronCenter.X - chevron - ColumnGap * scale;
        var totalWidth = RaceAmounts.Measure(totalText, totalStyle).X;
        RaceAmounts.DrawRight(drawList, totalRight, header.Center.Y, totalText, CasinoColors.Money, totalStyle);
        var titleStyle = TextStyles.SubheadlineEmphasized;
        var countStyle = TextStyles.Footnote;
        var count = texts.Count(tickets.Length);
        var countWidth = Typography.Measure(count, countStyle).X;
        var titleLeft = header.Min.X + pad;
        var titleRoom = MathF.Max(1f, totalRight - totalWidth - ColumnGap * scale - countWidth - ColumnGap * scale
                                      - titleLeft);
        var title = Typography.FitText(Loc.T(L.Race.TicketsHeading), titleRoom, titleStyle);
        var titleWidth = Typography.Measure(title, titleStyle).X;
        Typography.Draw(drawList, new Vector2(titleLeft, header.Center.Y - Typography.LineHeight(titleStyle) * 0.5f),
            title, CasinoColors.InkTitle, titleStyle);
        Typography.Draw(drawList,
            new Vector2(titleLeft + titleWidth + ColumnGap * scale,
                header.Center.Y - Typography.LineHeight(countStyle) * 0.5f), count, CasinoColors.InkBody, countStyle);
    }

    private static void DrawRow(ImDrawListPtr drawList, Rect row, CasinoRaceTicketDto[] tickets,
        CasinoRaceRunnerDto[] runners, RaceTexts texts, int index, float pop, float scale)
    {
        var pad = Pad * scale;
        var gap = ColumnGap * scale;
        var offset = (1f - pop) * 12f * scale;
        drawList.AddLine(new Vector2(row.Min.X + pad, row.Min.Y), new Vector2(row.Max.X - pad, row.Min.Y),
            ImGui.GetColorU32(StripRim), MathF.Max(1f, scale));
        var ticket = tickets[index];
        var paysStyle = TextStyles.FootnoteEmphasized;
        var pays = texts.TicketPays(tickets, runners, index);
        var paysWidth = MathF.Min(row.Width * 0.4f, Typography.Measure(pays, paysStyle).X);
        var right = row.Max.X - pad + offset;
        var shownPays = Typography.FitText(pays, paysWidth, paysStyle);
        Typography.Draw(drawList,
            new Vector2(right - Typography.Measure(shownPays, paysStyle).X,
                row.Center.Y - Typography.LineHeight(paysStyle) * 0.5f), shownPays,
            CasinoColors.Money with { W = pop }, paysStyle);
        var stakeStyle = TextStyles.Footnote;
        var stake = RaceAmounts.Text(ticket.Amount);
        var stakeWidth = RaceAmounts.Measure(stake, stakeStyle).X;
        var stakeRight = right - paysWidth - gap;
        RaceAmounts.DrawRight(drawList, stakeRight, row.Center.Y, stake, CasinoColors.InkBody with { W = pop },
            stakeStyle, pop);
        var labelStyle = TextStyles.Subheadline;
        var labelLeft = row.Min.X + pad + offset;
        Typography.Draw(drawList, new Vector2(labelLeft, row.Center.Y - Typography.LineHeight(labelStyle) * 0.5f),
            Typography.FitText(texts.Ticket(tickets, index), MathF.Max(1f, stakeRight - stakeWidth - gap - labelLeft),
                labelStyle), CasinoColors.InkTitle with { W = pop }, labelStyle);
    }

    private static void DrawChevron(ImDrawListPtr drawList, Vector2 center, float size, bool open, float scale)
    {
        var color = ImGui.GetColorU32(CasinoColors.InkBody);
        var thickness = MathF.Max(1f, 1.6f * scale);
        var direction = open ? 1f : -1f;
        var tip = center + new Vector2(0f, size * 0.5f * direction);
        drawList.AddLine(center + new Vector2(-size, -size * 0.5f * direction), tip, color, thickness);
        drawList.AddLine(tip, center + new Vector2(size, -size * 0.5f * direction), color, thickness);
    }

    private void Track(int count, long roundIndex, float deltaSeconds)
    {
        if (roundIndex != seenRound)
        {
            seenRound = roundIndex;
            seen = count;
            for (var index = 0; index < age.Length; index++)
            {
                age[index] = PopSeconds;
            }
        }

        for (var index = seen; index < count && index < age.Length; index++)
        {
            age[index] = 0f;
        }

        seen = count;
        for (var index = 0; index < age.Length; index++)
        {
            age[index] = MathF.Min(PopSeconds, age[index] + deltaSeconds);
        }
    }

    private float Pop(int index) => GameJuice.PopIn(age[index] / PopSeconds);
}
