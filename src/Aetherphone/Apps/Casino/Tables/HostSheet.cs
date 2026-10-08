using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed class HostDraft
{
    public static readonly long[] BuyInCoinSteps = { 20, 50, 100, 250, 500, 1_000, 2_500, 5_000 };

    public string Name = string.Empty;
    public int Seats = CasinoHostingRules.DefaultSeats;
    public int Listing = CasinoListings.Private;
    public bool Spectators = true;
    public bool FaceUp;
    public int Currency = CasinoCurrencies.Chips;
    public long ChipMinBet = BlackjackRules.MinBet;
    public long ChipMaxBet = CasinoHostingRules.DefaultChipMaxBet;
    public int MinBuyInStep;
    public int MaxBuyInStep = BuyInCoinSteps.Length - 1;
    public string PracticeStack = string.Empty;
    public string PracticeMinBet = string.Empty;
    public string PracticeMaxBet = string.Empty;
    public bool PracticeRebuy = true;
    public string Bank = string.Empty;
    public string GilMinBet = string.Empty;
    public string GilMaxBet = string.Empty;
    public string MaxPayout = string.Empty;
    public int TurnSeconds = CasinoHostingRules.DefaultTurnSeconds;
    public bool TimeBank = true;
    public int DealerMode = CasinoDealerModes.House;
    public bool AutoDeal = true;
    public int Pays = CasinoRuleSheet.PaysThreeToTwo;
    public bool HitsSoft17;
    public int Decks = 6;
    public int Splits = CasinoRuleSheet.SplitsToFour;
    public int Doubles = CasinoRuleSheet.DoublesAny;
    public bool Charlie;
    public bool Peek = true;

    public bool SeatBanked => CasinoCurrencies.SeatBanked(Currency);

    public void Reset()
    {
        Name = string.Empty;
        Seats = CasinoHostingRules.DefaultSeats;
        Listing = CasinoListings.Private;
        Spectators = true;
        FaceUp = false;
        Currency = CasinoCurrencies.Chips;
        ChipMinBet = BlackjackRules.MinBet;
        ChipMaxBet = CasinoHostingRules.DefaultChipMaxBet;
        MinBuyInStep = 0;
        MaxBuyInStep = BuyInCoinSteps.Length - 1;
        PracticeStack = string.Empty;
        PracticeMinBet = string.Empty;
        PracticeMaxBet = string.Empty;
        PracticeRebuy = true;
        Bank = string.Empty;
        GilMinBet = string.Empty;
        GilMaxBet = string.Empty;
        MaxPayout = string.Empty;
        TurnSeconds = CasinoHostingRules.DefaultTurnSeconds;
        TimeBank = true;
        DealerMode = CasinoDealerModes.House;
        AutoDeal = true;
        Pays = CasinoRuleSheet.PaysThreeToTwo;
        HitsSoft17 = false;
        Decks = 6;
        Splits = CasinoRuleSheet.SplitsToFour;
        Doubles = CasinoRuleSheet.DoublesAny;
        Charlie = false;
        Peek = true;
    }

    public CasinoTableConfigDto Build(long rate)
    {
        var practice = Currency == CasinoCurrencies.Practice;
        var gil = Currency == CasinoCurrencies.Gil;
        var rules = SeatBanked
            ? new CasinoBlackjackRuleSheetDto(Pays, HitsSoft17, Decks, Splits, Doubles, Charlie, Peek)
            : null;
        var stack = Parse(PracticeStack);
        return new CasinoTableConfigDto(
            GameKind: CasinoWire.BlackjackKind,
            Name: Name.Trim(),
            Seats: Seats,
            MinBet: practice ? Parse(PracticeMinBet) : gil ? Parse(GilMinBet) : ChipMinBet,
            MaxBet: practice ? Parse(PracticeMaxBet) : gil ? Parse(GilMaxBet) : ChipMaxBet,
            MinBuyIn: SeatBanked ? 0 : BuyInCoinSteps[MinBuyInStep] * rate,
            MaxBuyIn: SeatBanked ? 0 : BuyInCoinSteps[MaxBuyInStep] * rate,
            Practice: practice,
            PracticeStack: practice && stack > 0 ? stack : CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy: practice && PracticeRebuy,
            TurnSeconds: TurnSeconds,
            TimeBankUses: TimeBank ? CasinoHostingRules.TimeBankUses : CasinoHostingRules.TimeBankOff,
            Listing: Listing,
            Spectators: Spectators,
            FaceUp: practice && FaceUp,
            DealerMode: SeatBanked ? DealerMode : CasinoDealerModes.House,
            CoDealers: null,
            AutoDeal: !SeatBanked || DealerMode == CasinoDealerModes.House || AutoDeal,
            HouseRules: rules,
            Currency: Currency,
            Bank: gil ? Parse(Bank) : 0,
            MaxPayout: gil ? Parse(MaxPayout) : 0);
    }

    public static long Parse(string text)
    {
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 0;
    }
}

internal sealed class HostSheet
{
    private const float RowUnits = 52f;
    private const float SegmentRowUnits = 80f;
    private const float FieldWidthFraction = 0.46f;
    private const float StepperWidth = 160f;
    private const float FieldHeight = 36f;
    private const int NameMaxLength = CasinoHostingRules.NameMaxLength;
    private const int AmountDigits = 11;

    private static readonly LocString[] CurrencyLabels =
        { L.Tables.CurrencyChips, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    private static readonly LocString[] CurrencyHints =
        { L.Tables.CurrencyChipsHint, L.Tables.CurrencyPracticeHint, L.Tables.CurrencyGilHint };

    private static readonly LocString[] ListingLabels =
        { L.Tables.ListingPrivate, L.Tables.ListingKnock, L.Tables.ListingOpen };

    private static readonly LocString[] ListingHints =
        { L.Tables.ListingPrivateHint, L.Tables.ListingKnockHint, L.Tables.ListingOpenHint };

    private static readonly LocString[] DealerLabels = { L.Tables.DealerHouse, L.Tables.DealerHost };

    private static readonly LocString[] PaysLabels = { L.Tables.Pays32, L.Tables.Pays21, L.Tables.Pays11 };

    private static readonly LocString[] SplitLabels = { L.Tables.OptionOff, L.Tables.SplitsOnce, L.Tables.SplitsFour };

    private static readonly LocString[] DoubleLabels =
        { L.Tables.DoublesAny, L.Tables.DoublesNineEleven, L.Tables.OptionOff };

    private readonly CasinoTablesStore tables;
    private readonly CasinoStore chips;
    private readonly Venue.VenueHostOptions venueOptions;
    private readonly HostDraft draft = new();
    private readonly CasinoTextCache texts = new();
    private readonly string[] currencyOptions = new string[3];
    private readonly string[] chipCurrencyOptions = new string[2];
    private readonly string[] listingOptions = new string[3];
    private readonly string[] dealerOptions = new string[2];
    private readonly string[] paysOptions = new string[3];
    private readonly string[] splitOptions = new string[3];
    private readonly string[] doubleOptions = new string[3];
    private readonly string[] turnOptions = new string[CasinoHostingRules.TurnSeconds.Length];
    private readonly string[] deckOptions = new string[CasinoRuleSheet.Decks.Length];
    private readonly Action seatsDown;
    private readonly Action seatsUp;
    private readonly Action minBetDown;
    private readonly Action minBetUp;
    private readonly Action maxBetDown;
    private readonly Action maxBetUp;
    private readonly Action minBuyInDown;
    private readonly Action minBuyInUp;
    private readonly Action maxBuyInDown;
    private readonly Action maxBuyInUp;
    private LanguageInfo? optionsLanguage;
    private string inlineReason = string.Empty;

    public HostSheet(CasinoTablesStore tables, CasinoStore chips, Venue.VenueHostOptions venueOptions)
    {
        this.tables = tables;
        this.chips = chips;
        this.venueOptions = venueOptions;
        seatsDown = () => draft.Seats = Math.Max(CasinoHostingRules.MinSeats, draft.Seats - 1);
        seatsUp = () => draft.Seats = Math.Min(CasinoHostingRules.MaxSeats, draft.Seats + 1);
        minBetDown = () => draft.ChipMinBet = Math.Max(BlackjackRules.MinBet, CasinoLadder.StepDown(draft.ChipMinBet));
        minBetUp = () => draft.ChipMinBet = Math.Min(draft.ChipMaxBet, CasinoLadder.StepUp(draft.ChipMinBet));
        maxBetDown = () => draft.ChipMaxBet = Math.Max(draft.ChipMinBet, CasinoLadder.StepDown(draft.ChipMaxBet));
        maxBetUp = () => draft.ChipMaxBet = CasinoLadder.StepUp(draft.ChipMaxBet);
        minBuyInDown = () => draft.MinBuyInStep = Math.Max(0, draft.MinBuyInStep - 1);
        minBuyInUp = () => draft.MinBuyInStep = Math.Min(draft.MaxBuyInStep, draft.MinBuyInStep + 1);
        maxBuyInDown = () => draft.MaxBuyInStep = Math.Max(draft.MinBuyInStep, draft.MaxBuyInStep - 1);
        maxBuyInUp = () => draft.MaxBuyInStep = Math.Min(HostDraft.BuyInCoinSteps.Length - 1, draft.MaxBuyInStep + 1);
    }

    public void Enter()
    {
        draft.Reset();
        venueOptions.Reset();
        inlineReason = string.Empty;
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        ConsumeOutcomes();
        RefreshOptions();
        using var surface = AppSurface.Begin(body);
        var gilOpen = chips.HasFeature(CasinoFeatures.GilTables);
        if (!gilOpen && draft.Currency == CasinoCurrencies.Gil)
        {
            draft.Currency = CasinoCurrencies.Chips;
        }

        venueOptions.DrawGameCard(ui, scale);
        if (venueOptions.IsVenue)
        {
            DrawVenue(ui, gilOpen, scale);
            return;
        }

        ui.SectionHeading(Loc.T(L.Tables.SectionTable), Metrics.Space.Md);
        DrawTableCard(ui, scale);
        Hint(ui, Loc.T(ListingHints[draft.Listing]), scale);

        ui.SectionHeading(Loc.T(L.Tables.SectionMoney), Metrics.Space.Md);
        DrawMoneyCard(ui, gilOpen, scale);
        Hint(ui, Loc.T(CurrencyHints[draft.Currency]), scale);

        ui.SectionHeading(Loc.T(L.Tables.SectionClock), Metrics.Space.Md);
        DrawClockCard(ui);

        if (draft.SeatBanked)
        {
            ui.SectionHeading(Loc.T(L.Tables.SectionDealer), Metrics.Space.Md);
            DrawDealerCard(ui);
            Hint(ui, Loc.T(L.Tables.CoDealersNote), scale);
            ui.SectionHeading(Loc.T(L.Tables.SectionRules), Metrics.Space.Md);
            DrawRulesCard(ui);
        }

        ui.SectionHeading(Loc.T(L.Venue.Location), Metrics.Space.Md);
        venueOptions.DrawLocation(ui, scale);
        DrawFooter(ui, scale);
    }

    private void DrawVenue(AppSkin ui, bool gilOpen, float scale)
    {
        ui.SectionHeading(Loc.T(L.Tables.SectionTable), Metrics.Space.Md);
        var card = GroupCard.Begin(ui, RowUnits + SegmentRowUnits);
        var nameRow = card.NextRow(RowUnits);
        Label(ui, nameRow, Loc.T(L.Tables.HostName), scale);
        Field(ui, FieldRect(nameRow, scale), "##hostName", Loc.T(L.Tables.HostNameHint), ref draft.Name,
            NameMaxLength, false);
        draft.Listing = Segment(ui, card.NextRow(SegmentRowUnits), "##hostListing", Loc.T(L.Tables.HostListing),
            listingOptions, draft.Listing, scale);
        card.End();
        Hint(ui, Loc.T(ListingHints[draft.Listing]), scale);
        ui.SectionHeading(Loc.T(L.Tables.SectionMoney), Metrics.Space.Md);
        venueOptions.DrawVenueCard(ui, gilOpen, scale);
        ui.SectionHeading(Loc.T(L.Venue.Location), Metrics.Space.Md);
        venueOptions.DrawLocation(ui, scale);
        DrawFooter(ui, scale);
    }

    private void DrawFooter(AppSkin ui, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        if (inlineReason.Length > 0)
        {
            DrawReason(ui, scale);
        }

        DrawOpenButton(ui, scale);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
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
        }
    }

    private void DrawTableCard(AppSkin ui, float scale)
    {
        var card = GroupCard.Begin(ui, RowUnits * 3 + SegmentRowUnits);
        var nameRow = card.NextRow(RowUnits);
        Label(ui, nameRow, Loc.T(L.Tables.HostName), scale);
        Field(ui, FieldRect(nameRow, scale), "##hostName", Loc.T(L.Tables.HostNameHint), ref draft.Name,
            NameMaxLength, false);
        var seatsRow = card.NextRow(RowUnits);
        Label(ui, seatsRow, Loc.T(L.Tables.HostSeats), scale);
        StepperField.Draw(ui, StepperRect(seatsRow, scale), texts.Count(L.Tables.SeatsValue, draft.Seats), scale,
            seatsDown, seatsUp);
        draft.Listing = Segment(ui, card.NextRow(SegmentRowUnits), "##hostListing", Loc.T(L.Tables.HostListing),
            listingOptions, draft.Listing, scale);
        draft.Spectators = ToggleRow(ui, card.NextRow(RowUnits), "host.spectators", Loc.T(L.Tables.HostSpectators),
            draft.Spectators, scale);
        card.End();
    }

    private void DrawMoneyCard(AppSkin ui, bool gilOpen, float scale)
    {
        var rows = draft.Currency switch
        {
            CasinoCurrencies.Practice => 5,
            CasinoCurrencies.Gil => 4,
            _ => 4,
        };
        var card = GroupCard.Begin(ui, SegmentRowUnits + rows * RowUnits);
        var options = gilOpen ? currencyOptions : chipCurrencyOptions;
        draft.Currency = Segment(ui, card.NextRow(SegmentRowUnits), "##hostCurrency", Loc.T(L.Tables.HostCurrency),
            options, Math.Min(draft.Currency, options.Length - 1), scale);
        switch (draft.Currency)
        {
            case CasinoCurrencies.Practice:
                FieldRow(ui, card.NextRow(RowUnits), "##hostStack", Loc.T(L.Tables.PracticeStack),
                    NumberText.Group(CasinoHostingRules.DefaultPracticeStack), ref draft.PracticeStack, scale);
                FieldRow(ui, card.NextRow(RowUnits), "##hostPracticeMin", Loc.T(L.Tables.MinBet),
                    Loc.T(L.Tables.DefaultHint), ref draft.PracticeMinBet, scale);
                FieldRow(ui, card.NextRow(RowUnits), "##hostPracticeMax", Loc.T(L.Tables.MaxBet),
                    Loc.T(L.Tables.DefaultHint), ref draft.PracticeMaxBet, scale);
                draft.PracticeRebuy = ToggleRow(ui, card.NextRow(RowUnits), "host.rebuy", Loc.T(L.Tables.Rebuys),
                    draft.PracticeRebuy, scale);
                draft.FaceUp = ToggleRow(ui, card.NextRow(RowUnits), "host.faceup", Loc.T(L.Tables.FaceUp),
                    draft.FaceUp, scale);
                break;
            case CasinoCurrencies.Gil:
                FieldRow(ui, card.NextRow(RowUnits), "##hostBank", Loc.T(L.Tables.Bank), Loc.T(L.Tables.GilHint),
                    ref draft.Bank, scale);
                FieldRow(ui, card.NextRow(RowUnits), "##hostGilMax", Loc.T(L.Tables.MaxBet), Loc.T(L.Tables.GilHint),
                    ref draft.GilMaxBet, scale);
                FieldRow(ui, card.NextRow(RowUnits), "##hostGilMin", Loc.T(L.Tables.MinBet),
                    Loc.T(L.Tables.DefaultHint), ref draft.GilMinBet, scale);
                FieldRow(ui, card.NextRow(RowUnits), "##hostPayout", Loc.T(L.Tables.MaxPayout),
                    Loc.T(L.Tables.MaxPayoutHint), ref draft.MaxPayout, scale);
                break;
            default:
                StepperRow(ui, card.NextRow(RowUnits), Loc.T(L.Tables.MinBet), NumberText.Compact(draft.ChipMinBet),
                    minBetDown, minBetUp, scale);
                StepperRow(ui, card.NextRow(RowUnits), Loc.T(L.Tables.MaxBet), NumberText.Compact(draft.ChipMaxBet),
                    maxBetDown, maxBetUp, scale);
                StepperRow(ui, card.NextRow(RowUnits), Loc.T(L.Tables.MinBuyIn),
                    texts.Number(L.Strip.CoinsShort, HostDraft.BuyInCoinSteps[draft.MinBuyInStep]), minBuyInDown,
                    minBuyInUp, scale);
                StepperRow(ui, card.NextRow(RowUnits), Loc.T(L.Tables.MaxBuyIn),
                    texts.Number(L.Strip.CoinsShort, HostDraft.BuyInCoinSteps[draft.MaxBuyInStep]), maxBuyInDown,
                    maxBuyInUp, scale);
                break;
        }

        card.End();
    }

    private void DrawClockCard(AppSkin ui)
    {
        var scale = UiScale.Current;
        var card = GroupCard.Begin(ui, SegmentRowUnits + RowUnits);
        var selected = Array.IndexOf(CasinoHostingRules.TurnSeconds, draft.TurnSeconds);
        var picked = Segment(ui, card.NextRow(SegmentRowUnits), "##hostTurn", Loc.T(L.Tables.TurnClock),
            turnOptions, selected < 0 ? 1 : selected, scale);
        draft.TurnSeconds = CasinoHostingRules.TurnSeconds[picked];
        draft.TimeBank = ToggleRow(ui, card.NextRow(RowUnits), "host.timebank", Loc.T(L.Tables.TimeBank),
            draft.TimeBank, scale);
        card.End();
    }

    private void DrawDealerCard(AppSkin ui)
    {
        var scale = UiScale.Current;
        var hostDeals = draft.DealerMode == CasinoDealerModes.Host;
        var card = GroupCard.Begin(ui, SegmentRowUnits + (hostDeals ? RowUnits : 0f));
        draft.DealerMode = Segment(ui, card.NextRow(SegmentRowUnits), "##hostDealer", Loc.T(L.Tables.DealerMode),
            dealerOptions, draft.DealerMode, scale);
        if (hostDeals)
        {
            draft.AutoDeal = ToggleRow(ui, card.NextRow(RowUnits), "host.autodeal", Loc.T(L.Tables.AutoDeal),
                draft.AutoDeal, scale);
        }

        card.End();
    }

    private void DrawRulesCard(AppSkin ui)
    {
        var scale = UiScale.Current;
        var card = GroupCard.Begin(ui, SegmentRowUnits * 4 + RowUnits * 3);
        draft.Pays = Segment(ui, card.NextRow(SegmentRowUnits), "##rulesPays", Loc.T(L.Tables.RulesPays), paysOptions,
            draft.Pays, scale);
        var deck = Array.IndexOf(CasinoRuleSheet.Decks, draft.Decks);
        var deckPicked = Segment(ui, card.NextRow(SegmentRowUnits), "##rulesDecks", Loc.T(L.Tables.RulesDecks),
            deckOptions, deck < 0 ? 3 : deck, scale);
        draft.Decks = CasinoRuleSheet.Decks[deckPicked];
        draft.Splits = Segment(ui, card.NextRow(SegmentRowUnits), "##rulesSplits", Loc.T(L.Tables.RulesSplits),
            splitOptions, draft.Splits, scale);
        draft.Doubles = Segment(ui, card.NextRow(SegmentRowUnits), "##rulesDoubles", Loc.T(L.Tables.RulesDoubles),
            doubleOptions, draft.Doubles, scale);
        draft.HitsSoft17 = ToggleRow(ui, card.NextRow(RowUnits), "rules.soft17", Loc.T(L.Tables.RulesSoft17),
            draft.HitsSoft17, scale);
        draft.Charlie = ToggleRow(ui, card.NextRow(RowUnits), "rules.charlie", Loc.T(L.Tables.RulesCharlie),
            draft.Charlie, scale);
        draft.Peek = ToggleRow(ui, card.NextRow(RowUnits), "rules.peek", Loc.T(L.Tables.RulesPeek), draft.Peek,
            scale);
        card.End();
    }

    private void DrawOpenButton(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + Button.LargeHeight * scale));
        if (Button.Draw(rect, Loc.T(L.Tables.OpenTable), ui.Ink, ButtonStyle.Prominent,
                enabled: !tables.IntentInFlight, id: "host.open"))
        {
            Submit();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Button.LargeHeight * scale));
    }

    private void Submit()
    {
        var config = venueOptions.Apply(draft.Build(chips.Rate));
        var reason = venueOptions.IsVenue ? VenueRules.Check(config) : CasinoHostingRules.Check(config);
        if (reason.Length > 0)
        {
            inlineReason = reason;
            return;
        }

        inlineReason = string.Empty;
        tables.HostTable(config);
    }

    private void DrawReason(AppSkin ui, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var max = new Vector2(origin.X + width, origin.Y + block.Y + pad * 2f);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, max.Y - origin.Y + Metrics.Space.Sm * scale));
    }

    private static void Hint(AppSkin ui, string text, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var top = origin.Y + Metrics.Space.Xs * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Lg * scale, top), text,
            ui.MutedInk, TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Xs * 2f * scale));
    }

    private static void Label(AppSkin ui, in Rect row, string label, float scale)
    {
        var width = row.Width * (1f - FieldWidthFraction) - Metrics.Space.Sm * scale;
        var height = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - height * 0.5f),
            Typography.FitText(label, width, TextStyles.Body), ui.TitleInk, TextStyles.Body);
    }

    private static Rect FieldRect(in Rect row, float scale)
    {
        var height = FieldHeight * scale;
        return new Rect(new Vector2(row.Max.X - row.Width * FieldWidthFraction, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + height * 0.5f));
    }

    private static Rect StepperRect(in Rect row, float scale)
    {
        var height = FieldHeight * scale;
        var width = MathF.Min(StepperWidth * scale, row.Width * 0.55f);
        return new Rect(new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + height * 0.5f));
    }

    private static void StepperRow(AppSkin ui, in Rect row, string label, string value, Action down, Action up,
        float scale)
    {
        Label(ui, row, label, scale);
        StepperField.Draw(ui, StepperRect(row, scale), value, scale, down, up);
    }

    private static void FieldRow(AppSkin ui, in Rect row, string id, string label, string hint, ref string buffer,
        float scale)
    {
        Label(ui, row, label, scale);
        Field(ui, FieldRect(row, scale), id, hint, ref buffer, AmountDigits, true);
    }

    private static bool ToggleRow(AppSkin ui, in Rect row, string id, string label, bool value, float scale)
    {
        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, row.Width - width - Metrics.Space.Md * scale, TextStyles.Body), ui.TitleInk,
            TextStyles.Body);
        var toggleMin = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(id, new Rect(toggleMin, toggleMin + new Vector2(width, height)), value, ui.Theme);
    }

    private static int Segment(AppSkin ui, in Rect row, string id, string label, string[] options, int selected,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = row.Min.Y + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(row.Min.X, top),
            Typography.FitText(label, row.Width, TextStyles.Body), ui.TitleInk, TextStyles.Body);
        var stripTop = top + Typography.LineHeight(TextStyles.Body) + Metrics.Space.Sm * scale;
        var strip = new Rect(new Vector2(row.Min.X, stripTop),
            new Vector2(row.Max.X, MathF.Max(stripTop + 30f * scale, row.Max.Y - Metrics.Space.Sm * scale)));
        return SegmentStrip.Draw(id, strip, options, Math.Clamp(selected, 0, options.Length - 1), ui.Palette);
    }

    private static void Field(AppSkin ui, Rect field, string id, string hint, ref string buffer, int maxLength,
        bool numeric)
    {
        var drawList = ImGui.GetWindowDrawList();
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        var flags = numeric ? ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll
            : ImGuiInputTextFlags.None;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint(id, hint, ref buffer, maxLength + 1, flags);
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    private void RefreshOptions()
    {
        if (ReferenceEquals(optionsLanguage, Loc.Current) && currencyOptions[0] is not null)
        {
            return;
        }

        optionsLanguage = Loc.Current;
        Fill(currencyOptions, CurrencyLabels);
        Fill(chipCurrencyOptions, CurrencyLabels);
        Fill(listingOptions, ListingLabels);
        Fill(dealerOptions, DealerLabels);
        Fill(paysOptions, PaysLabels);
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
    }

    private static void Fill(string[] target, LocString[] source)
    {
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = Loc.T(source[index]);
        }
    }
}
