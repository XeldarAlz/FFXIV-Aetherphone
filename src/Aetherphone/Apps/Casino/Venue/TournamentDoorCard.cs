using System.Globalization;
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
    BadHands,
    BadStack,
    Pending,
}

internal sealed class TournamentDoorCard
{
    public const int DefaultHands = 20;

    private const int HandsDigits = 2;

    private readonly CasinoVenueStore venue;

    private string hands = string.Empty;
    private string stack = string.Empty;
    private string inlineReason = string.Empty;
    private int requestedHands;

    public TournamentDoorCard(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public void Reset()
    {
        hands = string.Empty;
        stack = string.Empty;
        inlineReason = string.Empty;
        requestedHands = 0;
    }

    public static bool Applies(CasinoTableRowDto? card)
    {
        return card is not null && IsBlackjack(card) && CasinoCurrencies.Of(card) == CasinoCurrencies.Practice;
    }

    public static TournamentGate GateOf(bool hostDeals, int seated, int hands, long stack, long minBet)
    {
        if (seated < CasinoHostingRules.MinSeats)
        {
            return hostDeals ? TournamentGate.DealerOut : TournamentGate.NeedsPlayers;
        }

        if (!HandsFit(hands))
        {
            return TournamentGate.BadHands;
        }

        return StackFits(stack, minBet) ? TournamentGate.Ready : TournamentGate.BadStack;
    }

    public static bool HandsFit(int hands)
    {
        return hands >= CasinoHostingRules.TournamentMinHands && hands <= CasinoHostingRules.TournamentMaxHands;
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
        TournamentGate.BadHands => L.Tables.ReasonTournamentHands,
        TournamentGate.BadStack => L.Tables.ReasonTournamentStack,
        TournamentGate.Pending => L.Venue.TournamentPending,
        _ => L.Venue.TournamentHint,
    };

    public static int HandsFrom(string text)
    {
        return VenueFields.ParseInt(text, DefaultHands);
    }

    private void Absorb(CasinoStakeOutcome outcome)
    {
        var pending = string.Equals(outcome.Reason, CasinoReasons.TournamentPending, StringComparison.Ordinal);
        inlineReason = outcome.Granted && !pending ? string.Empty : outcome.Reason;
        if (outcome.Granted && requestedHands > 0)
        {
            hands = requestedHands.ToString(CultureInfo.InvariantCulture);
        }

        requestedHands = 0;
    }

    public void Draw(AppSkin ui, CasinoTableRowDto? card, CasinoTableDoorDto? door, string roomId)
    {
        if (card is null || !Applies(card))
        {
            return;
        }

        var scale = UiScale.Current;
        var outcome = venue.TakeHostOutcome();
        if (outcome is not null)
        {
            Absorb(outcome);
        }

        var pendingOnDoor = door is { TournamentPending: true };
        if (pendingOnDoor && string.Equals(inlineReason, CasinoReasons.TournamentPending, StringComparison.Ordinal))
        {
            inlineReason = string.Empty;
        }

        ui.SectionHeading(Loc.T(L.Venue.TournamentHeading), Metrics.Space.Md);
        var group = GroupCard.Begin(ui, 2, VenueFields.RowUnits);
        VenueFields.TextRow(ui, group.NextRow(VenueFields.RowUnits), "##tournamentHands", Loc.T(L.Venue.TournamentHands),
            Loc.T(L.Venue.TournamentHandsPlaceholder), ref hands, HandsDigits, true, scale);
        VenueFields.TextRow(ui, group.NextRow(VenueFields.RowUnits), "##tournamentStack", Loc.T(L.Venue.TournamentStack),
            Loc.T(L.Tables.DefaultHint), ref stack, VenueFields.AmountDigits, true, scale);
        group.End();
        var hostDeals = card.Config is { DealerMode: CasinoDealerModes.Host };
        var handCount = HandsFrom(hands);
        var startingStack = VenueFields.Parse(stack);
        var gate = pendingOnDoor
            ? TournamentGate.Pending
            : GateOf(hostDeals, SeatedOf(card, door), handCount, startingStack, card.MinBet);
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
            requestedHands = handCount;
            venue.StartTournament(roomId, handCount, startingStack);
        }

        if (Button.Draw(stop, Loc.T(L.Venue.TournamentStop), ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                !venue.HostInFlight, id: "door.tournament.stop"))
        {
            inlineReason = string.Empty;
            requestedHands = 0;
            venue.StopTournament(roomId);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Sm * scale));
    }
}
