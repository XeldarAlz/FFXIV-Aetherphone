using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal enum TournamentGate : byte
{
    Ready,
    NeedsPlayers,
    DealerOut,
    BadStack,
}

internal sealed class TournamentDoorCard
{
    private const int HandsDigits = 2;

    private readonly CasinoVenueStore venue;

    private string hands = string.Empty;
    private string stack = string.Empty;
    private string inlineReason = string.Empty;

    public TournamentDoorCard(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public void Reset()
    {
        hands = string.Empty;
        stack = string.Empty;
        inlineReason = string.Empty;
    }

    public static bool Applies(CasinoTableRowDto? card)
    {
        return card is not null && IsBlackjack(card) && CasinoCurrencies.Of(card) == CasinoCurrencies.Practice;
    }

    public static TournamentGate GateOf(bool hostDeals, int seated, long stack, long minBet)
    {
        if (seated < CasinoHostingRules.MinSeats)
        {
            return hostDeals ? TournamentGate.DealerOut : TournamentGate.NeedsPlayers;
        }

        return StackFits(stack, minBet) ? TournamentGate.Ready : TournamentGate.BadStack;
    }

    public static bool StackFits(long stack, long minBet)
    {
        return stack == 0 || (stack >= CasinoHostingRules.MinPracticeStack
            && stack <= CasinoHostingRules.MaxPracticeStack && stack >= minBet);
    }

    private static bool IsBlackjack(CasinoTableRowDto card)
    {
        return string.Equals(card.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal);
    }

    private static int SeatedOf(CasinoTableRowDto card, CasinoTableDoorDto? door)
    {
        return door is { Owner: true } ? door.Seated?.Length ?? 0 : card.SeatedCount;
    }

    private static LocString GateHint(TournamentGate gate) => gate switch
    {
        TournamentGate.NeedsPlayers => L.Venue.TournamentNeedsPlayers,
        TournamentGate.DealerOut => L.Venue.TournamentDealerOut,
        TournamentGate.BadStack => L.Tables.ReasonTournamentStack,
        _ => L.Venue.TournamentHint,
    };

    public static int HandsFrom(string text)
    {
        return Math.Clamp(VenueFields.ParseInt(text, 20), CasinoHostingRules.TournamentMinHands,
            CasinoHostingRules.TournamentMaxHands);
    }

    public void Draw(AppSkin ui, CasinoTableRowDto? card, CasinoTableDoorDto? door, string roomId)
    {
        if (card is null || !IsBlackjack(card))
        {
            return;
        }

        var scale = UiScale.Current;
        if (!Applies(card))
        {
            ui.SectionHeading(Loc.T(L.Venue.TournamentHeading), Metrics.Space.Md);
            VenueFields.Hint(ui, Loc.T(L.Venue.TournamentHint), scale);
            return;
        }

        var outcome = venue.TakeHostOutcome();
        if (outcome is not null)
        {
            inlineReason = outcome.Granted ? string.Empty : outcome.Reason;
        }

        ui.SectionHeading(Loc.T(L.Venue.TournamentHeading), Metrics.Space.Md);
        var group = GroupCard.Begin(ui, 2, VenueFields.RowUnits);
        VenueFields.TextRow(ui, group.NextRow(VenueFields.RowUnits), "##tournamentHands", Loc.T(L.Venue.TournamentHands),
            "20", ref hands, HandsDigits, true, scale);
        VenueFields.TextRow(ui, group.NextRow(VenueFields.RowUnits), "##tournamentStack", Loc.T(L.Venue.TournamentStack),
            Loc.T(L.Tables.DefaultHint), ref stack, VenueFields.AmountDigits, true, scale);
        group.End();
        var hostDeals = card.Config is { DealerMode: CasinoDealerModes.Host };
        var startingStack = VenueFields.Parse(stack);
        var gate = GateOf(hostDeals, SeatedOf(card, door), startingStack, card.MinBet);
        VenueFields.Hint(ui, Loc.T(inlineReason.Length > 0 ? CasinoReasons.MessageFor(inlineReason)
            : GateHint(gate)), scale);
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var height = Button.LargeHeight * scale;
        var start = new Rect(origin, new Vector2(origin.X + half, origin.Y + height));
        var stop = new Rect(new Vector2(origin.X + half + gap, origin.Y), new Vector2(origin.X + width, origin.Y + height));
        if (Button.Draw(start, Loc.T(L.Venue.TournamentStart), ui.Ink, ButtonStyle.Prominent,
                enabled: !venue.HostInFlight && gate == TournamentGate.Ready, id: "door.tournament.start"))
        {
            inlineReason = string.Empty;
            venue.StartTournament(roomId, HandsFrom(hands), startingStack);
        }

        if (Button.Draw(stop, Loc.T(L.Venue.TournamentStop), ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                !venue.HostInFlight, id: "door.tournament.stop"))
        {
            inlineReason = string.Empty;
            venue.StopTournament(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
