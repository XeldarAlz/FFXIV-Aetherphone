using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class VenueHostOptions
{
    public const int GameBlackjack = 0;

    private const int SidesDigits = 7;
    private const int SecondsDigits = 3;

    private static readonly LocString[] GameLabels =
        { L.Casino.GameBlackjack, L.Venue.GameDiceTable, L.Venue.GameDeathroll, L.Venue.GameRaffle };

    private static readonly VenueRoomKind[] GameKinds =
        { VenueRoomKind.None, VenueRoomKind.Dice, VenueRoomKind.Deathroll, VenueRoomKind.Raffle };

    private static readonly LocString[] CurrencyLabels = { L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    private readonly CasinoVenueStore venue;
    private readonly string[] gameOptions = new string[GameLabels.Length];
    private readonly string[] currencyOptions = new string[CurrencyLabels.Length];
    private LanguageInfo? optionsLanguage;
    private string locationText = string.Empty;
    private CasinoHousingPosition locationOf;
    private LanguageInfo? locationLanguage;
    private string sidesHint = string.Empty;
    private string roundHint = string.Empty;
    private string startHint = string.Empty;
    private string stakeHint = string.Empty;
    private string stackHint = string.Empty;

    public int Game;
    public bool Gil;
    public bool PinLocation = true;
    public bool HighestWins = true;
    public string Sides = string.Empty;
    public string RoundSeconds = string.Empty;
    public string StartAt = string.Empty;
    public string Stake = string.Empty;
    public string PracticeStack = string.Empty;

    public VenueHostOptions(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public VenueRoomKind Kind => GameKinds[Math.Clamp(Game, 0, GameKinds.Length - 1)];

    public bool IsVenue => Kind != VenueRoomKind.None;

    public void Reset()
    {
        Game = GameBlackjack;
        Gil = false;
        PinLocation = true;
        HighestWins = true;
        Sides = string.Empty;
        RoundSeconds = string.Empty;
        StartAt = string.Empty;
        Stake = string.Empty;
        PracticeStack = string.Empty;
    }

    public void Select(VenueRoomKind kind)
    {
        Game = Math.Max(GameBlackjack, Array.IndexOf(GameKinds, kind));
    }

    public void DrawGameCard(AppSkin ui, float scale)
    {
        Refresh();
        var card = GroupCard.Begin(ui, 1, VenueFields.SegmentRowUnits);
        Game = VenueFields.Segment(ui, card.NextRow(VenueFields.SegmentRowUnits), "##hostGame",
            Loc.T(L.Venue.HostGame), gameOptions, Game, scale);
        card.End();
        if (IsVenue)
        {
            VenueFields.Hint(ui, Loc.T(HintFor(Kind)), scale);
        }
    }

    public void DrawVenueCard(AppSkin ui, bool gilOpen, float scale)
    {
        Refresh();
        if (!gilOpen)
        {
            Gil = false;
        }

        var rows = Kind switch
        {
            VenueRoomKind.Dice => 3,
            VenueRoomKind.Deathroll => Gil ? 2 : 3,
            _ => 0,
        };
        var card = GroupCard.Begin(ui, 1, VenueFields.SegmentRowUnits + rows * VenueFields.RowUnits);
        if (gilOpen)
        {
            Gil = VenueFields.Segment(ui, card.NextRow(VenueFields.SegmentRowUnits), "##venueCurrency",
                Loc.T(L.Tables.HostCurrency), currencyOptions, Gil ? 1 : 0, scale) == 1;
        }
        else
        {
            VenueFields.ValueRow(ui, card.NextRow(VenueFields.SegmentRowUnits), Loc.T(L.Tables.HostCurrency),
                currencyOptions[0], scale);
        }

        switch (Kind)
        {
            case VenueRoomKind.Dice:
                VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##venueSides", Loc.T(L.Venue.Sides),
                    sidesHint, ref Sides, SidesDigits, true, scale);
                HighestWins = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), "venue.highest",
                    Loc.T(L.Venue.HighestWins), HighestWins, scale);
                VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##venueRound", Loc.T(L.Venue.RoundSeconds),
                    roundHint, ref RoundSeconds, SecondsDigits, true, scale);
                break;
            case VenueRoomKind.Deathroll:
                VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##venueStart", Loc.T(L.Venue.StartNumber),
                    startHint, ref StartAt, SidesDigits, true, scale);
                VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##venueStake", Loc.T(L.Venue.Stake),
                    stakeHint, ref Stake, VenueFields.AmountDigits, true, scale);
                if (!Gil)
                {
                    VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##venueStack",
                        Loc.T(L.Tables.PracticeStack), stackHint, ref PracticeStack, VenueFields.AmountDigits, true, scale);
                }

                break;
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(Gil ? L.Venue.GilRoomHint : L.Venue.PracticeRoomHint), scale);
    }

    public void DrawLocation(AppSkin ui, float scale)
    {
        venue.EnsureNearby();
        var position = venue.Position;
        var card = GroupCard.Begin(ui, 2, VenueFields.RowUnits);
        VenueFields.ValueRow(ui, card.NextRow(VenueFields.RowUnits), Loc.T(L.Venue.Location),
            position.InWard ? LocationText(position) : Loc.T(L.Venue.LocationNone), scale);
        var pin = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), "venue.pin", Loc.T(L.Venue.PinLocation),
            PinLocation && position.InWard, scale);
        if (position.InWard)
        {
            PinLocation = pin;
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(L.Venue.LocationHint), scale);
    }

    public CasinoTableConfigDto Apply(CasinoTableConfigDto config)
    {
        var location = PinLocation ? venue.Position.ToLocation() : null;
        if (!IsVenue)
        {
            return config with { Location = location };
        }

        var practice = !Gil;
        var stack = VenueFields.Parse(PracticeStack);
        return config with
        {
            GameKind = VenueKinds.WireKind(Kind),
            Seats = 0,
            MinBet = 0,
            MaxBet = 0,
            MinBuyIn = 0,
            MaxBuyIn = 0,
            Practice = practice,
            PracticeStack = practice && stack > 0 ? stack : CasinoHostingRules.DefaultPracticeStack,
            PracticeRebuy = false,
            FaceUp = false,
            DealerMode = CasinoDealerModes.House,
            CoDealers = null,
            AutoDeal = true,
            HouseRules = null,
            Currency = Gil ? CasinoCurrencies.Gil : CasinoCurrencies.Practice,
            Bank = 0,
            MaxPayout = 0,
            Location = location,
            Dice = Kind == VenueRoomKind.Dice
                ? new CasinoDiceTableOptionsDto(VenueFields.ParseInt(Sides, VenueRules.DefaultSides), HighestWins,
                    VenueFields.ParseInt(RoundSeconds, VenueRules.DefaultRoundSeconds))
                : null,
            Deathroll = Kind == VenueRoomKind.Deathroll
                ? new CasinoDeathrollOptionsDto(VenueFields.ParseInt(StartAt, VenueRules.DefaultStartAt),
                    VenueFields.Parse(Stake) is > 0 and var stake ? stake : VenueRules.DefaultStake)
                : null,
        };
    }

    private static LocString HintFor(VenueRoomKind kind) => kind switch
    {
        VenueRoomKind.Deathroll => L.Venue.DeathrollHostHint,
        VenueRoomKind.Raffle => L.Venue.RaffleHostIntro,
        _ => L.Venue.DiceHostHint,
    };

    private string LocationText(CasinoHousingPosition position)
    {
        if (position == locationOf && ReferenceEquals(locationLanguage, Loc.Current) && locationText.Length > 0)
        {
            return locationText;
        }

        locationOf = position;
        locationLanguage = Loc.Current;
        var district = HousingDistricts.TryGet((uint)position.Territory, out var known) ? known.Name : string.Empty;
        locationText = position.Plot > 0
            ? Loc.T(L.Venue.LocationPlot, district, position.Ward.ToString(Loc.Culture),
                position.Plot.ToString(Loc.Culture))
            : Loc.T(L.Venue.LocationWard, district, position.Ward.ToString(Loc.Culture));
        return locationText;
    }

    private void Refresh()
    {
        if (ReferenceEquals(optionsLanguage, Loc.Current) && gameOptions[0] is not null)
        {
            return;
        }

        optionsLanguage = Loc.Current;
        for (var index = 0; index < gameOptions.Length; index++)
        {
            gameOptions[index] = Loc.T(GameLabels[index]);
        }

        for (var index = 0; index < currencyOptions.Length; index++)
        {
            currencyOptions[index] = Loc.T(CurrencyLabels[index]);
        }

        sidesHint = NumberText.Group(VenueRules.DefaultSides);
        roundHint = NumberText.Group(VenueRules.DefaultRoundSeconds);
        startHint = NumberText.Group(VenueRules.DefaultStartAt);
        stakeHint = NumberText.Group(VenueRules.DefaultStake);
        stackHint = NumberText.Group(CasinoHostingRules.DefaultPracticeStack);
    }
}
