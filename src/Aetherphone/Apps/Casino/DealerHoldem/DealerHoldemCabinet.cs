using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed partial class DealerHoldemCabinet : ICabinetIdle
{
    public const int HeroTag = 10;
    public const int DealerTag = 20;
    public const int BoardTag = 30;
    public const int WinningsTag = 9;

    private const float NoticeSeconds = 4f;
    private const float BannerSeconds = 2.2f;
    private const float FlightSeconds = 0.34f;
    private const float RevealSeconds = 0.42f;

    private readonly CasinoStore store;
    private readonly CasinoDealerHoldemStore dealerStore;
    private readonly Action openCashier;
    private readonly BetComposer composer = new("##dealerHoldemAnte");
    private readonly DealerHoldemPlayback playback = new();
    private readonly CardFlight cards = new(12);
    private readonly DealerHoldemChipFlights chips = new();
    private readonly DealerHoldemTexts texts = new();
    private readonly DealerHoldemPaySheet paySheet = new();
    private readonly BlackjackDealer dealer = new();
    private readonly string[] tripsOptions = new string[2];
    private readonly string[] spotLines = new string[DealerHoldemRules.SpotCount];
    private readonly string[] spotShort = new string[DealerHoldemRules.SpotCount];
    private readonly DealerHoldemResult[] spotResults = new DealerHoldemResult[DealerHoldemRules.SpotCount];
    private readonly long[] spotShown = new long[DealerHoldemRules.SpotCount];

    private DealerHoldemLayout layout;
    private OriginalsLabel tripsLabel;
    private OriginalsLabel outcomeLabel;
    private OriginalsLabel dealerLine;
    private readonly OriginalsLabel[] betLabels = new OriginalsLabel[3];
    private bool trips;
    private bool dealing;
    private string noticeText = string.Empty;
    private float noticeClock;
    private float bannerClock;
    private int resolvingSpot = -1;
    private float idleTime;

    public DealerHoldemCabinet(CasinoStore store, CasinoDealerHoldemStore dealerStore, Action openCashier)
    {
        this.store = store;
        this.dealerStore = dealerStore;
        this.openCashier = openCashier;
    }

    public static float DeckHeight => BetComposer.DeckHeightFor(true, false);

    public Backdrop IdleBackdrop => Backdrop.Felt;

    public CasinoStageSpec Spec => new(CasinoGames.DealerHoldem, L.DealerHoldem.Game, Backdrop.Felt,
        DeckHeight: DeckHeight, BetsRail: true, InstantAvailable: true, ReturnTenths: DealerHoldemRules.ReturnTenths,
        Extra: L.DealerHoldem.PayTables, LampPool: 1f);

    public void Enter()
    {
        noticeText = string.Empty;
        composer.Prefill(DealerHoldemRules.MinAnte);
        dealerStore.LoadOpen();
    }

    public void Reset()
    {
        playback.Clear();
        cards.Clear();
        chips.Clear();
        dealer.Clear();
        paySheet.Close();
        ClearResults();
        dealing = false;
        noticeText = string.Empty;
        bannerClock = 0f;
    }

    public void Gate()
    {
        composer.Gate();
        paySheet.Gate();
    }

    public void OpenPayTables()
    {
        paySheet.Open();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        composer.DrawOverlay(screen, ui, false);
        paySheet.Draw(screen, ui, texts);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(frame.Safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        layout = DealerHoldemLayout.Compute(frame.Safe, scale,
            DealerHoldemArt.TableHeight(DealerHoldemTexts.TableRows, scale));
        var instant = frame.Instant || frame.SnapToTruth;
        if (frame.SnapToTruth)
        {
            playback.Snap();
            cards.Clear();
            chips.Snap();
        }

        Consume(frame.Instant);
        playback.Advance(frame.DeltaSeconds);
        cards.Advance(frame.DeltaSeconds);
        chips.Advance(frame.DeltaSeconds);
        dealer.Update(frame.DeltaSeconds);
        TickClocks(frame.DeltaSeconds);
        while (playback.TryTake(out var cue))
        {
            HandleCue(stage, cue, instant || cue.Instant, scale);
        }

        Land();
        DrawFelt(drawList, frame, scale);
        DrawStateLine(drawList, scale);
        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var sitting = state.Sitting;
        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, frame.Deck, scale);
            return;
        }

        if (playback.Open || dealing)
        {
            DrawDecisions(ui, frame.Deck, sitting.Stack, blocked, scale);
            return;
        }

        DrawComposer(stage, ui, frame, sitting.Stack, blocked);
    }

    private void TickClocks(float deltaSeconds)
    {
        if (noticeClock > 0f)
        {
            noticeClock -= deltaSeconds;
            if (noticeClock <= 0f)
            {
                noticeText = string.Empty;
            }
        }

        if (bannerClock > 0f)
        {
            bannerClock -= deltaSeconds;
        }

        idleTime += deltaSeconds;
    }

    private void Consume(bool instant)
    {
        var open = dealerStore.TakeOpen();
        if (open?.Round is { } resumed && resumed.RoundId.Length > 0
            && !string.Equals(resumed.RoundId, playback.RoundId, StringComparison.Ordinal)
            && !DealerHoldemRules.IsOver(resumed.Phase))
        {
            BeginRound();
            playback.Apply(resumed, true);
            Raise(Loc.T(L.Originals.Resumed));
        }

        var answer = dealerStore.TakeResult();
        if (answer is not null)
        {
            Absorb(answer, instant);
        }

        if (!dealerStore.TakeFailure())
        {
            return;
        }

        dealing = false;
        Raise(Loc.T(L.Casino.ReasonUnreachable));
    }

    private void Absorb(CasinoDealerHoldemDto answer, bool instant)
    {
        dealing = false;
        if (!answer.Granted)
        {
            Raise(CasinoReasons.Text(answer.Reason, answer.Ceiling));
            if (string.Equals(answer.Reason, CasinoReasons.InvalidMove, StringComparison.Ordinal))
            {
                dealerStore.LoadOpen();
            }

            if (answer.RoundId.Length > 0 && string.Equals(answer.RoundId, playback.RoundId, StringComparison.Ordinal))
            {
                playback.Apply(answer, instant);
            }

            return;
        }

        if (!string.Equals(answer.RoundId, playback.RoundId, StringComparison.Ordinal))
        {
            BeginRound();
        }

        noticeText = string.Empty;
        playback.Apply(answer, instant);
    }

    private void BeginRound()
    {
        cards.Clear();
        chips.Clear();
        dealer.Clear();
        ClearResults();
        bannerClock = 0f;
    }

    private void ClearResults()
    {
        resolvingSpot = -1;
        for (var index = 0; index < DealerHoldemRules.SpotCount; index++)
        {
            spotLines[index] = string.Empty;
            spotShort[index] = string.Empty;
            spotResults[index] = DealerHoldemResult.None;
            spotShown[index] = 0;
        }
    }

    private void Raise(string text)
    {
        noticeText = text;
        noticeClock = NoticeSeconds;
    }

    private void HandleCue(CasinoStage stage, in DealerHoldemCue cue, bool instant, float scale)
    {
        switch (cue.Kind)
        {
            case DealerHoldemCueKind.Hero:
                Deal(DealerHoldemCueKind.Hero, cue.Index, HeroPose(cue.Index, true), instant);
                break;
            case DealerHoldemCueKind.Dealer:
                Deal(DealerHoldemCueKind.Dealer, cue.Index, DealerPose(cue.Index, false), instant);
                break;
            case DealerHoldemCueKind.Board:
                Deal(DealerHoldemCueKind.Board, cue.Index, BoardPose(cue.Index, true), instant);
                break;
            case DealerHoldemCueKind.Reveal:
                Reveal(instant);
                break;
            case DealerHoldemCueKind.Verdict:
                Verdict(instant);
                break;
            case DealerHoldemCueKind.Resolve:
                Resolve((DealerHoldemSpot)cue.Index, instant);
                break;
            default:
                Settle(stage, instant, scale);
                break;
        }
    }

    private void Deal(DealerHoldemCueKind kind, int index, in CardPose to, bool instant)
    {
        if (instant)
        {
            return;
        }

        var card = playback.Card(kind, index);
        var from = new CardPose(layout.Shoe, layout.DealerCardWidth * 0.8f, 0.2f, false);
        cards.Launch(card, from, to, flip: to.FaceUp, tag: TagOf(kind, index), seconds: FlightSeconds);
    }

    private void Reveal(bool instant)
    {
        if (instant)
        {
            return;
        }

        for (var index = 0; index < DealerHoldemRules.HoleCards; index++)
        {
            var card = playback.Card(DealerHoldemCueKind.Dealer, index);
            cards.Launch(card, DealerPose(index, false), DealerPose(index, true), delay: index * 0.12f, flip: true,
                tag: DealerTag + index, seconds: RevealSeconds, arc: 0f);
        }
    }

    private void Verdict(bool instant)
    {
        var round = playback.Round;
        if (round is null || round.Phase == DealerHoldemRules.PhaseVoided)
        {
            return;
        }

        var hand = HoldemHandNames.Describe(round.DealerHand);
        if (!round.DealerQualifies)
        {
            bannerClock = instant ? 0f : BannerSeconds;
            dealer.Say(Loc.T(L.DealerHoldem.NoQualify));
            return;
        }

        dealer.Say(dealerLine.Get(L.DealerHoldem.DealerHas, hand));
    }

    private void Resolve(DealerHoldemSpot spot, bool instant)
    {
        var round = playback.Round;
        if (round is null)
        {
            return;
        }

        var index = (int)spot;
        var stake = DealerHoldemPlayback.StakeOf(round, spot);
        var returned = DealerHoldemPlayback.ReturnOf(round, spot);
        var result = DealerHoldemPlayback.ResultOf(round, spot);
        spotResults[index] = result;
        resolvingSpot = index;
        var name = texts.Spot(spot);
        switch (result)
        {
            case DealerHoldemResult.Win:
                spotLines[index] = Loc.T(L.DealerHoldem.SpotPays, name, OddsFor(round, spot));
                spotShort[index] = NumberText.Signed(returned - stake);
                if (!instant)
                {
                    chips.Launch(layout.Puck, layout.Circle(spot), returned - stake, index, false);
                    CasinoSfx.Play(UiSound.ChipSlide);
                }
                else
                {
                    spotShown[index] = returned;
                }

                break;
            case DealerHoldemResult.Push:
                spotLines[index] = Loc.T(L.DealerHoldem.SpotPush, name);
                spotShort[index] = Loc.T(L.DealerHoldem.Push);
                spotShown[index] = stake;
                break;
            default:
                spotLines[index] = Loc.T(L.DealerHoldem.SpotLoses, name);
                spotShort[index] = name;
                spotShown[index] = 0;
                if (!instant)
                {
                    chips.Launch(layout.Circle(spot), layout.Puck, stake, -1, true);
                }

                break;
        }
    }

    private string OddsFor(CasinoDealerHoldemDto round, DealerHoldemSpot spot)
    {
        var category = round.PlayerHand >= 0 ? HoldemHands.DisplayCategoryOf(round.PlayerHand) : 0;
        return spot switch
        {
            DealerHoldemSpot.Blind when DealerHoldemRules.BlindPaysOn(category) =>
                OddsText(DealerHoldemRules.BlindNumerators[category], DealerHoldemRules.BlindDenominators[category]),
            DealerHoldemSpot.Trips when DealerHoldemRules.TripsPaysOn(category) =>
                OddsText(DealerHoldemRules.TripsPays[category], 1),
            _ => OddsText(1, 1),
        };
    }

    private static string OddsText(int numerator, int denominator) =>
        Loc.T(L.DealerHoldem.Odds, GameNumber.Label(numerator), GameNumber.Label(denominator));

    private void Settle(CasinoStage stage, bool instant, float scale)
    {
        var round = playback.Round;
        resolvingSpot = -1;
        if (round is null)
        {
            return;
        }

        for (var index = 0; index < DealerHoldemRules.SpotCount; index++)
        {
            if (spotResults[index] == DealerHoldemResult.Win)
            {
                spotShown[index] = DealerHoldemPlayback.ReturnOf(round, (DealerHoldemSpot)index);
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        stage.Settle(new CasinoBetRecord(L.DealerHoldem.Game, round.Stake, round.Payout, round.RoundId, now));
        if (round.Payout > round.Stake)
        {
            stage.Celebration.Celebrate(round.Stake, round.Payout, layout.Hero.Center, instant);
        }
    }

    private void Land()
    {
        while (cards.TryTakeLanded(out _))
        {
            CasinoSfx.Play(UiSound.CardSnap);
        }

        var round = playback.Round;
        while (chips.TryTakeLanded(out var landing))
        {
            if (round is null || landing.Tag < 0 || landing.Tag >= DealerHoldemRules.SpotCount)
            {
                continue;
            }

            spotShown[landing.Tag] = DealerHoldemPlayback.ReturnOf(round, (DealerHoldemSpot)landing.Tag);
        }
    }

    private static int TagOf(DealerHoldemCueKind kind, int index) => kind switch
    {
        DealerHoldemCueKind.Hero => HeroTag + index,
        DealerHoldemCueKind.Board => BoardTag + index,
        _ => DealerTag + index,
    };

    private bool Flying(int tag)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            if (cards.Tag(index) == tag)
            {
                return true;
            }
        }

        return false;
    }

    private CardPose HeroPose(int index, bool faceUp) =>
        new(layout.HeroCard(index), layout.HeroCardWidth, DealerHoldemLayout.HeroCardAngle(index), faceUp);

    private CardPose DealerPose(int index, bool faceUp) =>
        new(layout.DealerCard(index), layout.DealerCardWidth, 0f, faceUp);

    private CardPose BoardPose(int index, bool faceUp) => new(layout.BoardCard(index), layout.BoardCardWidth, 0f, faceUp);
}
