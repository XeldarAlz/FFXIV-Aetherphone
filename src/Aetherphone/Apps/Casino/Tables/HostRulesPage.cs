using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HostRuleRowKind : byte
{
    Segment,
    Toggle,
    Ladder,
}

internal struct HostRuleRow
{
    public HostRuleRowKind Kind;
    public bool On;
    public bool Enabled;
    public int Index;
    public LadderSpan Span;
    public string Id;
    public string Label;
    public string Hint;
    public string Value;
    public string[]? Options;

    public static HostRuleRow Segment(string id, string label, string hint, string[] options, int selected) => new()
    {
        Kind = HostRuleRowKind.Segment,
        Enabled = true,
        Index = Math.Clamp(selected, 0, options.Length - 1),
        Id = id,
        Label = label,
        Hint = hint,
        Value = string.Empty,
        Options = options,
    };

    public static HostRuleRow Toggle(string id, string label, string hint, bool on, bool enabled = true) => new()
    {
        Kind = HostRuleRowKind.Toggle,
        On = on,
        Enabled = enabled,
        Id = id,
        Label = label,
        Hint = hint,
        Value = string.Empty,
    };

    public static HostRuleRow Ladder(string id, string label, string value, string hint, LadderSpan span, int index) =>
        new()
        {
            Kind = HostRuleRowKind.Ladder,
            Enabled = true,
            Index = span.Clamp(index),
            Span = span,
            Id = id,
            Label = label,
            Hint = hint,
            Value = value,
        };
}

internal sealed class HostRulesPage
{
    private const int MaxRows = 8;
    private const int DefaultTurnIndex = 1;
    private const int DefaultDeckIndex = 3;
    private const float SegmentTextScale = 0.86f;
    private const float DisabledToggleAlpha = 0.4f;

    private static readonly LocString[] DealerLabels = { L.Tables.DealerHouse, L.Tables.DealerHost };

    private static readonly LocString[] SplitLabels = { L.Tables.OptionOff, L.Tables.SplitsOnce, L.Tables.SplitsFour };

    private static readonly LocString[] DoubleLabels =
        { L.Tables.DoublesAny, L.Tables.DoublesNineEleven, L.Tables.OptionOff };

    private readonly Venue.VenueHostOptions venueOptions;
    private readonly CasinoTextCache texts;
    private readonly HostRuleRow[] rows = new HostRuleRow[MaxRows];
    private readonly string[] dealerOptions = new string[DealerLabels.Length];
    private readonly string[] paysOptions = new string[HostRules.PaysLabels.Length];
    private readonly string[] splitOptions = new string[SplitLabels.Length];
    private readonly string[] doubleOptions = new string[DoubleLabels.Length];
    private readonly string[] turnOptions = new string[CasinoHostingRules.TurnSeconds.Length];
    private readonly string[] deckOptions = new string[CasinoRuleSheet.Decks.Length];
    private readonly string[] roundOptions = new string[HostLadders.RoundSeconds.Length];
    private readonly string[] anteOptions = new string[HostLadders.AnteTenths.Length];
    private LanguageInfo? optionsLanguage;

    public HostRulesPage(Venue.VenueHostOptions venueOptions, CasinoTextCache texts)
    {
        this.venueOptions = venueOptions;
        this.texts = texts;
    }

    public bool Draw(Rect body, AppSkin ui, HostDraft draft, string summary, float scale)
    {
        RefreshOptions();
        var block = HostCards.FooterBlock(string.Empty, summary, string.Empty, body.Width, scale);
        var scroll = new Rect(body.Min, new Vector2(body.Max.X, body.Max.Y - HostFlowLayout.FooterHeight(scale, block)));
        using (UiScope.PushId("casino.host.rules"))
        {
            using (AppSurface.Begin(scroll))
            using (UiScope.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
            {
                DrawSections(ui, draft, ScrollLayout.StableContentWidth(), scale);
            }

            var footer = new Rect(new Vector2(body.Min.X, scroll.Max.Y), body.Max);
            return HostCards.Footer(ui, footer, string.Empty, summary, string.Empty, Loc.T(L.HostSetup.Done), true,
                "casino.host.rules.done", scale);
        }
    }

    private void DrawSections(AppSkin ui, HostDraft draft, float width, float scale)
    {
        HostCards.Gap(HostFlowLayout.LineGap, scale);
        HostCards.Note(ui, Loc.T(L.HostSetup.RulesIntro), width, scale);
        var sections = HostRules.Sections(draft.Game, draft.Currency);
        if ((sections & HostRuleSection.Dealing) != 0)
        {
            DrawDealing(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.Turns) != 0)
        {
            DrawTurns(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.Blackjack) != 0)
        {
            DrawBlackjack(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.Holdem) != 0)
        {
            DrawHoldem(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.Dice) != 0)
        {
            DrawDice(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.FreePlay) != 0)
        {
            DrawFreePlay(ui, draft, width, scale);
        }

        if ((sections & HostRuleSection.Gil) != 0)
        {
            DrawGil(ui, draft, width, scale);
        }

        DrawRoom(ui, draft, width, scale);
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
    }

    private void DrawDealing(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.HostSetup.SectionDealing), width, scale);
        if (!draft.SeatBanked)
        {
            HostCards.InfoCard(ui, Loc.T(L.HostSetup.ChipClockNote), width, scale);
            return;
        }

        var hostDeals = draft.DealerMode == CasinoDealerModes.Host;
        rows[0] = HostRuleRow.Segment("##hostDealerMode", Loc.T(L.Tables.DealerMode),
            Loc.T(hostDeals ? L.HostSetup.YouDealHint : L.HostSetup.ClockDealsHint), dealerOptions, draft.DealerMode);
        rows[1] = HostRuleRow.Toggle("host.autodeal", Loc.T(L.HostSetup.AutoDeal), Loc.T(L.HostSetup.AutoDealHint),
            draft.AutoDeal);
        var count = hostDeals ? 2 : 1;
        Card(ui, rows.AsSpan(0, count), width, scale);
        draft.DealerMode = rows[0].Index;
        if (hostDeals)
        {
            draft.AutoDeal = rows[1].On;
        }

        HostCards.Note(ui, Loc.T(L.HostSetup.CoDealersNote), width, scale);
    }

    private void DrawTurns(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.HostSetup.SectionTurns), width, scale);
        var turn = Array.IndexOf(CasinoHostingRules.TurnSeconds, draft.TurnSeconds);
        rows[0] = HostRuleRow.Segment("##hostTurnClock", Loc.T(L.Tables.TurnClock), string.Empty, turnOptions,
            turn < 0 ? DefaultTurnIndex : turn);
        rows[1] = HostRuleRow.Toggle("host.timebank", Loc.T(L.HostSetup.TimeBank),
            texts.Counts(L.HostSetup.TimeBankHint, CasinoHostingRules.TimeBankUses,
                CasinoHostingRules.TimeBankSecondsPerUse), draft.TimeBank);
        Card(ui, rows.AsSpan(0, 2), width, scale);
        draft.TurnSeconds = CasinoHostingRules.TurnSeconds[rows[0].Index];
        draft.TimeBank = rows[1].On;
    }

    private void DrawBlackjack(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.HostSetup.SectionBlackjack), width, scale);
        var deck = Array.IndexOf(CasinoRuleSheet.Decks, draft.Decks);
        rows[0] = HostRuleRow.Segment("##hostRulesPays", Loc.T(L.Tables.RulesPays), string.Empty, paysOptions,
            draft.Pays);
        rows[1] = HostRuleRow.Segment("##hostRulesDecks", Loc.T(L.Tables.RulesDecks), string.Empty, deckOptions,
            deck < 0 ? DefaultDeckIndex : deck);
        rows[2] = HostRuleRow.Segment("##hostRulesSplits", Loc.T(L.Tables.RulesSplits), string.Empty, splitOptions,
            draft.Splits);
        rows[3] = HostRuleRow.Segment("##hostRulesDoubles", Loc.T(L.Tables.RulesDoubles), string.Empty,
            doubleOptions, draft.Doubles);
        rows[4] = HostRuleRow.Toggle("host.rules.soft17", Loc.T(L.Tables.RulesSoft17), string.Empty,
            draft.HitsSoft17);
        rows[5] = HostRuleRow.Toggle("host.rules.charlie", Loc.T(L.Tables.RulesCharlie),
            Loc.T(L.HostSetup.CharlieHint), draft.Charlie);
        rows[6] = HostRuleRow.Toggle("host.rules.peek", Loc.T(L.Tables.RulesPeek), Loc.T(L.HostSetup.PeekHint),
            draft.Peek);
        Card(ui, rows.AsSpan(0, 7), width, scale);
        draft.Pays = rows[0].Index;
        draft.Decks = CasinoRuleSheet.Decks[rows[1].Index];
        draft.Splits = rows[2].Index;
        draft.Doubles = rows[3].Index;
        draft.HitsSoft17 = rows[4].On;
        draft.Charlie = rows[5].On;
        draft.Peek = rows[6].On;
    }

    private void DrawHoldem(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.Casino.GameHoldem), width, scale);
        rows[0] = HostRuleRow.Segment("##hostAnte", Loc.T(L.Holdem.HostAnte), Loc.T(L.HostSetup.AnteHint),
            anteOptions, draft.Ante);
        Card(ui, rows.AsSpan(0, 1), width, scale);
        draft.Ante = rows[0].Index;
    }

    private void DrawDice(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.HostSetup.SectionDice), width, scale);
        rows[0] = HostRuleRow.Segment("##hostRoundSeconds", Loc.T(L.HostSetup.RoundLength), string.Empty,
            roundOptions, draft.RoundSeconds);
        rows[1] = HostRuleRow.Toggle("host.highest", Loc.T(L.HostSetup.HighestWins),
            Loc.T(L.HostSetup.HighestWinsHint), draft.HighestWins);
        var staked = HostRules.StakesEachRound(draft);
        if (staked)
        {
            var stake = draft.DiceStakeValue;
            rows[2] = HostRuleRow.Ladder("##hostRoundStake", Loc.T(L.Venue.RoundStake),
                stake == 0 ? Loc.T(L.Venue.NoStake) : TableAmounts.Amount(texts, stake, draft.Currency),
                Loc.T(L.Venue.RoundStakeHint), draft.DiceStakeSpan, draft.DiceStake);
        }

        Card(ui, rows.AsSpan(0, staked ? 3 : 2), width, scale);
        draft.RoundSeconds = rows[0].Index;
        draft.HighestWins = rows[1].On;
        if (staked)
        {
            draft.DiceStake = rows[2].Index;
        }
    }

    private void DrawFreePlay(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.Tables.CurrencyPractice), width, scale);
        var seats = HostGames.HasSeats(draft.Game);
        rows[0] = HostRuleRow.Ladder("##hostStack", Loc.T(L.Tables.PracticeStack),
            NumberText.Compact(draft.TableStackValue), Loc.T(L.HostSetup.StackHint), draft.StackSpan, draft.Stack);
        rows[1] = HostRuleRow.Toggle("host.rebuy", Loc.T(L.Tables.Rebuys), Loc.T(L.HostSetup.RebuysHint),
            draft.PracticeRebuy);
        rows[2] = HostRuleRow.Toggle("host.faceup", Loc.T(L.Tables.FaceUp), Loc.T(L.HostSetup.FaceUpHint),
            draft.FaceUp);
        Card(ui, rows.AsSpan(0, seats ? 3 : 1), width, scale);
        draft.Stack = rows[0].Index;
        if (!seats)
        {
            return;
        }

        draft.PracticeRebuy = rows[1].On;
        draft.FaceUp = rows[2].On;
        if (draft.Game == HostGame.Blackjack)
        {
            HostCards.Note(ui, Loc.T(L.Tables.RebuysTournamentNote), width, scale);
        }
    }

    private void DrawGil(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.Tables.CurrencyGil), width, scale);
        var index = draft.PayoutCapped ? draft.MaxPayout : draft.Bank;
        rows[0] = HostRuleRow.Ladder("##hostPayout", Loc.T(L.HostSetup.LargestPayout),
            TableAmounts.Amount(texts, draft.PayoutValue, draft.Currency), Loc.T(L.HostSetup.PayoutHint),
            draft.PayoutSpan, index);
        Card(ui, rows.AsSpan(0, 1), width, scale);
        draft.PayoutCapped = rows[0].Index != draft.Bank;
        draft.MaxPayout = rows[0].Index;
    }

    private void DrawRoom(AppSkin ui, HostDraft draft, float width, float scale)
    {
        Caption(ui, Loc.T(L.HostSetup.SectionRoom), width, scale);
        var seats = HostGames.HasSeats(draft.Game);
        var inWard = venueOptions.InWard;
        var pinRow = seats ? 1 : 0;
        rows[0] = HostRuleRow.Toggle("host.spectators", Loc.T(L.Tables.HostSpectators),
            Loc.T(L.HostSetup.SpectatorsHint), draft.Spectators);
        rows[pinRow] = HostRuleRow.Toggle("venue.pin", Loc.T(L.HostSetup.PinVenue),
            inWard ? texts.Named(L.HostSetup.PinHint, venueOptions.Place()) : Loc.T(L.HostSetup.PinNoWard),
            venueOptions.Pinned, inWard);
        Card(ui, rows.AsSpan(0, pinRow + 1), width, scale);
        if (seats)
        {
            draft.Spectators = rows[0].On;
        }

        if (inWard)
        {
            venueOptions.PinLocation = rows[pinRow].On;
        }

        var notes = venueOptions.Notes(draft.Listing);
        if ((notes & Venue.VenuePinNotes.InviteOnly) != 0)
        {
            HostCards.Note(ui, Loc.T(draft.Game == HostGame.Book
                ? L.HostSetup.BookInviteOnlyNote
                : L.HostSetup.InviteOnlyNote), width, scale);
        }

        if ((notes & Venue.VenuePinNotes.NoPlot) != 0)
        {
            HostCards.Note(ui, Loc.T(L.HostSetup.PinNoPlotNote), width, scale);
        }
    }

    private static void Caption(AppSkin ui, string title, float width, float scale)
    {
        HostCards.Gap(HostFlowLayout.SectionGap, scale);
        var origin = ImGui.GetCursorScreenPos();
        var inset = HostFlowLayout.LineGap * scale;
        ListSection.PaintOverline(ImGui.GetWindowDrawList(), new Vector2(origin.X + inset, origin.Y), title,
            ui.MutedInk, MathF.Max(1f, width - inset * 2f));
        ImGui.Dummy(new Vector2(width, ListSection.OverlineHeight + HostFlowLayout.HeadingGap * scale));
    }

    private static void Card(AppSkin ui, Span<HostRuleRow> cardRows, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var pad = HostFlowLayout.CardPad * scale;
        var inner = MathF.Max(1f, width - pad * 2f);
        Span<float> heights = stackalloc float[cardRows.Length];
        var total = 0f;
        for (var index = 0; index < cardRows.Length; index++)
        {
            heights[index] = RowHeight(cardRows[index], inner, scale);
            total += heights[index];
        }

        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + total), Metrics.Radius.Grouped * scale);
        var top = origin.Y;
        for (var index = 0; index < cardRows.Length; index++)
        {
            if (index > 0)
            {
                drawList.AddLine(new Vector2(origin.X + pad, top), new Vector2(origin.X + width, top),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            DrawRow(drawList, ui, ref cardRows[index], new Vector2(origin.X + pad, top), inner, heights[index], scale);
            top += heights[index];
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total));
    }

    private static float RowHeight(in HostRuleRow row, float inner, float scale)
    {
        switch (row.Kind)
        {
            case HostRuleRowKind.Segment:
                return HostFlowLayout.SegmentRowHeight(scale, Typography.LineHeight(TextStyles.Body),
                    HintHeight(row.Hint, inner));
            case HostRuleRowKind.Ladder:
                return HostFlowLayout.LadderRowHeight(scale,
                    HostCards.LadderBlockHeight(row.Hint, inner, TextStyles.Title3, scale));
            default:
                var textWidth = HostFlowLayout.ToggleTextWidth(inner, scale);
                return HostFlowLayout.ToggleRowHeight(scale, ToggleTextHeight(row, textWidth, scale));
        }
    }

    private static float HintHeight(string hint, float width) =>
        hint.Length > 0 ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, width).Y : 0f;

    private static float ToggleTextHeight(in HostRuleRow row, float textWidth, float scale)
    {
        var label = Typography.MeasureWrappedBlock(row.Label, TextStyles.Body, textWidth).Y;
        return row.Hint.Length > 0 ? label + 2f * scale + HintHeight(row.Hint, textWidth) : label;
    }

    private static void DrawRow(ImDrawListPtr drawList, AppSkin ui, ref HostRuleRow row, Vector2 topLeft,
        float inner, float height, float scale)
    {
        switch (row.Kind)
        {
            case HostRuleRowKind.Segment:
                DrawSegment(drawList, ui, ref row, topLeft, inner, scale);
                break;
            case HostRuleRowKind.Ladder:
                var low = row.Span.First;
                HostCards.LadderBlock(drawList, ui, row.Id,
                    new Vector2(topLeft.X, topLeft.Y + HostFlowLayout.RowPad * scale), inner, row.Label, row.Value,
                    row.Hint, row.Span, ref low, ref row.Index, false, TextStyles.Title3, scale);
                break;
            default:
                DrawToggle(drawList, ui, ref row, topLeft, inner, height, scale);
                break;
        }
    }

    private static void DrawSegment(ImDrawListPtr drawList, AppSkin ui, ref HostRuleRow row, Vector2 topLeft,
        float inner, float scale)
    {
        var top = topLeft.Y + HostFlowLayout.RowPad * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(topLeft.X, top), Typography.FitText(row.Label, inner, TextStyles.Body),
            ui.TitleInk, TextStyles.Body);
        var stripTop = top + labelHeight + HostFlowLayout.CardGap * scale;
        var strip = new Rect(new Vector2(topLeft.X, stripTop),
            new Vector2(topLeft.X + inner, stripTop + HostFlowLayout.SegmentTrack * scale));
        row.Index = SegmentStrip.Draw(row.Id, strip, row.Options!, row.Index, ui.Palette, HostFlowLayout.SegmentTrack,
            SegmentTextScale);
        if (row.Hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(topLeft.X, strip.Max.Y + HostFlowLayout.CardGap * scale),
                row.Hint, ui.MutedInk, TextStyles.Footnote, inner);
        }
    }

    private static void DrawToggle(ImDrawListPtr drawList, AppSkin ui, ref HostRuleRow row, Vector2 topLeft,
        float inner, float height, float scale)
    {
        var textWidth = HostFlowLayout.ToggleTextWidth(inner, scale);
        var labelHeight = Typography.MeasureWrappedBlock(row.Label, TextStyles.Body, textWidth).Y;
        var block = ToggleTextHeight(row, textWidth, scale);
        var centerY = topLeft.Y + height * 0.5f;
        var top = centerY - block * 0.5f;
        var labelInk = row.Enabled ? ui.TitleInk : ui.MutedInk;
        Typography.DrawWrappedLeft(new Vector2(topLeft.X, top), row.Label, labelInk, TextStyles.Body, textWidth);
        if (row.Hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(topLeft.X, top + labelHeight + 2f * scale), row.Hint, ui.MutedInk,
                TextStyles.Footnote, textWidth);
        }

        var size = new Vector2(Metrics.Size.ToggleWidth * scale, Metrics.Size.ToggleHeight * scale);
        var toggleMin = new Vector2(topLeft.X + inner - size.X, centerY - size.Y * 0.5f);
        row.On = Toggle.Draw(row.Id, new Rect(toggleMin, toggleMin + size), row.On, ui.Theme,
            row.Enabled ? 1f : DisabledToggleAlpha, row.Enabled);
    }

    private void RefreshOptions()
    {
        if (ReferenceEquals(optionsLanguage, Loc.Current) && dealerOptions[0] is not null)
        {
            return;
        }

        optionsLanguage = Loc.Current;
        Fill(dealerOptions, DealerLabels);
        Fill(paysOptions, HostRules.PaysLabels);
        Fill(splitOptions, SplitLabels);
        Fill(doubleOptions, DoubleLabels);
        for (var index = 0; index < turnOptions.Length; index++)
        {
            turnOptions[index] = Loc.T(L.Tables.SecondsValue,
                CasinoHostingRules.TurnSeconds[index].ToString(Loc.Culture));
        }

        for (var index = 0; index < deckOptions.Length; index++)
        {
            deckOptions[index] = CasinoRuleSheet.Decks[index].ToString(Loc.Culture);
        }

        for (var index = 0; index < roundOptions.Length; index++)
        {
            roundOptions[index] = HostRules.RoundText(HostLadders.RoundSeconds[index]);
        }

        anteOptions[0] = Loc.T(L.Tables.OptionOff);
        for (var index = 1; index < anteOptions.Length; index++)
        {
            anteOptions[index] = HostRules.AnteText(index);
        }
    }

    private static void Fill(string[] target, LocString[] source)
    {
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = Loc.T(source[index]);
        }
    }
}
