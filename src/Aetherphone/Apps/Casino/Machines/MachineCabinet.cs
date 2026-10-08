using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed partial class MachineCabinet
{
    public static readonly string[] MachineIds =
    {
        SlotsRules.BirdId,
        SlotsRules.CascadeId,
        SlotsRules.MoogleId,
    };

    private const float ChassisInset = 6f;
    private const float ChassisPad = 12f;
    private const float GlassShare = 0.24f;
    private const float GlassMin = 104f;
    private const float GlassMax = 140f;
    private const float StripHeight = 72f;
    private const float SceneFadeSpeed = 2.5f;
    private const float AutoPauseSeconds = 0.6f;
    private const float RollupTickSeconds = 0.09f;
    private const long ResultHoldMilliseconds = 10_000;
    private const string MalformedReason = "malformed";

    private readonly CasinoStore store;
    private readonly CasinoPlayStore play;
    private readonly ConfirmService confirm;
    private readonly Action openCashier;
    private readonly MachineRoundPlayback playback = new();
    private readonly MachineReels reels = new();
    private readonly MachinePaySheet paySheet = new();
    private readonly GambleLadder gamble = new();
    private readonly BetComposer[] composers = new BetComposer[3];
    private readonly MachineIdle[] idles = new MachineIdle[3];
    private readonly CasinoSlotsMeterDto?[] meters = new CasinoSlotsMeterDto?[2];

    private string machineId = SlotsRules.BirdId;
    private int machineIndex;
    private bool ante;
    private bool turbo;
    private volatile bool buyConfirmed;
    private string inlineReason = string.Empty;
    private MachineRollup rollup;
    private bool settled = true;
    private bool tumbleSounded;
    private MachineBeat soundedBeat;
    private int soundedStep = -1;
    private int soundedReels;
    private int soundedCoins;
    private float sceneAmount;
    private float autoPause;
    private float rollupTick;
    private long meterBet = -1;
    private long resultNet;
    private long resultTick;
    private Rect window;
    private Rect strip;
    private Rect glass;

    public MachineCabinet(CasinoStore store, CasinoPlayStore play, ConfirmService confirm, Action openCashier)
    {
        this.store = store;
        this.play = play;
        this.confirm = confirm;
        this.openCashier = openCashier;
        for (var index = 0; index < MachineIds.Length; index++)
        {
            composers[index] = new BetComposer("##machineBet." + MachineIds[index]);
            composers[index].Reset(SlotsRules.DefaultBet);
            idles[index] = new MachineIdle(MachineIds[index]);
        }
    }

    private BetComposer Composer => composers[machineIndex];

    private string Mode => ante && machineIndex == 1 ? SlotsRules.AnteMode : SlotsRules.BaseMode;

    public bool PayTableOpen => paySheet.IsOpen;

    public static bool Owns(string gameId) => IndexOf(MachineFor(gameId)) >= 0;

    public static string MachineFor(string gameId) =>
        string.Equals(gameId, CasinoGames.Slots, StringComparison.Ordinal) ? SlotsRules.BirdId : gameId;

    public static LocString TitleOf(string gameId) => MachineFor(gameId) switch
    {
        SlotsRules.CascadeId => L.Machines.GameCascade,
        SlotsRules.MoogleId => L.Machines.GameMoogle,
        _ => L.Machines.GameBird,
    };

    public ICabinetIdle? IdleFor(string gameId)
    {
        var index = IndexOf(MachineFor(gameId));
        return index >= 0 ? idles[index] : null;
    }

    public CasinoStageSpec SpecFor(string gameId)
    {
        var id = MachineFor(gameId);
        var info = SlotsMachines.For(id);
        var mode = string.Equals(id, machineId, StringComparison.Ordinal) ? Mode : SlotsRules.BaseMode;
        return new CasinoStageSpec(gameId, TitleOf(id), Backdrop.Strip,
            DeckHeight: BetComposer.DeckHeightFor(true, false), BetsRail: true, InstantAvailable: true,
            ReturnTenths: info.ReturnFor(mode) / 10, Extra: L.Machines.PaysTitle);
    }

    public void Enter(string gameId)
    {
        var id = MachineFor(gameId);
        var index = IndexOf(id);
        if (index < 0)
        {
            return;
        }

        if (!string.Equals(id, machineId, StringComparison.Ordinal))
        {
            ClearRound();
        }

        machineId = id;
        machineIndex = index;
        inlineReason = string.Empty;
        meterBet = -1;
        reels.Rest(id);
        play.RecoverPendingRound();
    }

    public void Reset()
    {
        ClearRound();
        paySheet.Close();
        for (var index = 0; index < composers.Length; index++)
        {
            composers[index].Auto.Stop(AutoStop.Manual);
            composers[index].Auto.Acknowledge();
        }

        inlineReason = string.Empty;
        autoPause = 0f;
    }

    public void OpenPayTable()
    {
        paySheet.Open();
    }

    public void ClosePayTable()
    {
        paySheet.Close();
    }

    public void Gate()
    {
        paySheet.Gate();
        for (var index = 0; index < composers.Length; index++)
        {
            composers[index].Gate();
        }
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        paySheet.Draw(screen, ui, machineId, Composer.Amount);
        Composer.DrawOverlay(screen, ui, true);
    }

    public bool TryRecentResult(out long net)
    {
        net = resultNet;
        return resultTick != 0 && Environment.TickCount64 - resultTick < ResultHoldMilliseconds;
    }

    public long DisplayStack(long stack)
    {
        var hidden = gamble.UnshownPayout;
        if (playback.HasRound && !settled)
        {
            hidden += Math.Max(0, playback.TotalWin + playback.Jackpot - rollup.Shown);
        }

        return Math.Max(0, stack - hidden);
    }

    public void Draw(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var state = store.State;
        if (state is null)
        {
            LoadingPulse.Draw(frame.Safe.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk,
                LoadingPulse.SafeLabel());
            return;
        }

        if (!store.HasFeature(CasinoFeatures.Machines))
        {
            DrawNotOpen(drawList, ui, frame.Safe, scale);
            return;
        }

        Consume(stage, frame);
        if (frame.SnapToTruth)
        {
            playback.Skip();
            rollup.Finish();
            gamble.Snap();
        }

        Advance(frame);
        Settle(stage, frame, state);
        RefreshMeters();
        Layout(frame, scale);
        sceneAmount = Approach(sceneAmount, playback.InFeature ? 1f : 0f, frame.DeltaSeconds * SceneFadeSpeed);
        MachineArt.SceneTint(drawList, frame.Full, machineId, sceneAmount, frame.Phase, scale);
        var chassis = new Rect(new Vector2(frame.Full.Min.X + ChassisInset * scale, glass.Min.Y - ChassisPad * scale),
            new Vector2(frame.Full.Max.X - ChassisInset * scale, strip.Max.Y + ChassisPad * 0.5f * scale));
        var lit = stage.Celebration.Active || playback.InFeature ? 1f : 0.55f;
        MachineArt.Chassis(drawList, chassis, machineId, frame.Phase, lit, playback.InFeature, scale);
        DrawGlass(drawList, ui, frame, scale);
        MachineArt.ReelWindow(drawList, window, machineId, scale);
        drawList.PushClipRect(window.Min, window.Max, true);
        reels.Draw(drawList, window, playback, machineId, frame.Phase, frame.DeltaSeconds, stage.Particles, scale);
        drawList.PopClipRect();
        DrawBanners(drawList, frame, scale);
        DrawGamble(drawList, ui, frame, scale);
        DrawStrip(drawList, ui, frame, scale);
        HandleWindowTap(frame);
        var sitting = state.Sitting;
        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, frame.Deck, scale);
            return;
        }

        DrawDeck(stage, frame, ui, state, sitting, scale);
    }

    private static int IndexOf(string id)
    {
        for (var index = 0; index < MachineIds.Length; index++)
        {
            if (string.Equals(MachineIds[index], id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void ClearRound()
    {
        playback.Reset();
        rollup.Snap(0);
        gamble.Reset();
        settled = true;
        soundedStep = -1;
        sceneAmount = 0f;
    }

    private void Consume(CasinoStage stage, in CasinoStageFrame frame)
    {
        var round = play.TakeSpinResult();
        if (round is not null)
        {
            Absorb(stage, frame, round);
        }

        var gambled = play.TakeGambleResult();
        if (gambled is not null)
        {
            if (gamble.Absorb(gambled))
            {
                stage.Settle(new CasinoBetRecord(L.Machines.GambleTitle, gambled.Stake, gambled.Payout,
                    gambled.RoundId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                CasinoSfx.Play(UiSound.CardSnap);
            }
            else if (!gambled.Granted)
            {
                inlineReason = gambled.Reason.Length > 0 ? gambled.Reason : CasinoReasons.Unreachable;
            }
        }

        var fetched = play.TakeMeters();
        if (fetched is not null && string.Equals(fetched.MachineId, SlotsRules.MoogleId, StringComparison.Ordinal)
            && fetched.Bet == Composer.Amount)
        {
            KeepMeters(fetched.Meters);
        }

        if (!play.TakeRoundFailure())
        {
            return;
        }

        inlineReason = CasinoReasons.Unreachable;
        gamble.Fail();
        Composer.Auto.Stop(AutoStop.Refused);
    }

    private void Absorb(CasinoStage stage, in CasinoStageFrame frame, CasinoSlotsSpinDto round)
    {
        if (!round.Granted)
        {
            inlineReason = round.Reason.Length > 0 ? round.Reason : CasinoReasons.Unreachable;
            Composer.Auto.Stop(AutoStop.Refused);
            return;
        }

        var index = IndexOf(round.MachineId);
        if (index < 0 || !playback.Begin(round))
        {
            inlineReason = MalformedReason;
            Composer.Auto.Stop(AutoStop.Refused);
            return;
        }

        if (index != machineIndex)
        {
            machineIndex = index;
            machineId = round.MachineId;
        }

        inlineReason = string.Empty;
        settled = false;
        rollup.Snap(0);
        rollupTick = 0f;
        soundedStep = -1;
        soundedReels = 0;
        stage.Celebration.Clear();
        if (string.Equals(round.MachineId, SlotsRules.MoogleId, StringComparison.Ordinal) && round.Bet == Composer.Amount)
        {
            KeepMeters(round.Meters);
        }

        if (frame.Instant)
        {
            playback.Skip();
            rollup.Snap(playback.TotalWin);
        }
    }

    private void KeepMeters(CasinoSlotsMeterDto[]? fresh)
    {
        Array.Clear(meters);
        if (fresh is null)
        {
            return;
        }

        for (var index = 0; index < fresh.Length; index++)
        {
            var meter = fresh[index];
            if (string.Equals(meter.Tier, SlotsRules.MeterMini, StringComparison.Ordinal))
            {
                meters[0] = meter;
            }
            else if (string.Equals(meter.Tier, SlotsRules.MeterMinor, StringComparison.Ordinal))
            {
                meters[1] = meter;
            }
        }
    }

    private void RefreshMeters()
    {
        if (machineIndex != 2 || Composer.Amount == meterBet || play.RoundInFlight)
        {
            return;
        }

        meterBet = Composer.Amount;
        Array.Clear(meters);
        play.RequestMeters(SlotsRules.MoogleId, meterBet);
    }

    private void Advance(in CasinoStageFrame frame)
    {
        var delta = frame.DeltaSeconds;
        playback.Turbo = turbo;
        playback.Update(delta);
        gamble.Update(delta);
        Sound();
        var target = playback.HasRound ? playback.Committed : 0;
        if (frame.Instant)
        {
            rollup.Snap(target);
            return;
        }

        rollup.Update(target, playback.Bet, delta, turbo);
        if (rollup.Done || playback.TotalWin + playback.Jackpot <= playback.Cost)
        {
            return;
        }

        rollupTick -= delta;
        if (rollupTick <= 0f)
        {
            rollupTick = RollupTickSeconds;
            CasinoSfx.Play(UiSound.ReelTick);
        }
    }

    private void Sound()
    {
        if (!playback.HasRound)
        {
            return;
        }

        var beat = playback.Beat;
        var stepChanged = soundedStep != playback.StepIndex || soundedBeat != beat;
        if (stepChanged)
        {
            soundedStep = playback.StepIndex;
            soundedBeat = beat;
            soundedReels = 0;
            soundedCoins = 0;
            tumbleSounded = false;
            switch (beat)
            {
                case MachineBeat.Intro:
                case MachineBeat.Buy:
                case MachineBeat.Hold:
                    CasinoSfx.Play(UiSound.LevelUp);
                    break;
                case MachineBeat.Meter:
                    CasinoSfx.Play(UiSound.TurnChime);
                    break;
                case MachineBeat.Expand:
                    CasinoSfx.Play(UiSound.GamePowerUp);
                    break;
            }
        }

        if (beat == MachineBeat.Spin)
        {
            var stopped = playback.StoppedReels();
            if (stopped > soundedReels)
            {
                soundedReels = stopped;
                CasinoSfx.Play(UiSound.ReelStop);
            }

            return;
        }

        if (beat == MachineBeat.Tumble && !tumbleSounded)
        {
            tumbleSounded = true;
            CasinoSfx.Pitched(UiSound.PegTick, playback.StepIndex);
            return;
        }

        if (beat == MachineBeat.Respin && !tumbleSounded
            && playback.BeatSeconds >= MachineTiming.Scaled(MachineTiming.Respin, turbo))
        {
            tumbleSounded = true;
            var before = playback.Previous?.Coins?.Length ?? 0;
            CasinoSfx.Play((playback.Current.Coins?.Length ?? 0) > before ? UiSound.TileSafe : UiSound.GameTick);
            return;
        }

        if (beat != MachineBeat.Collect)
        {
            return;
        }

        var coins = Math.Min(playback.CollectedCoins(), playback.Current.Coins?.Length ?? 0);
        if (coins > soundedCoins)
        {
            soundedCoins = coins;
            CasinoSfx.Pitched(UiSound.PegTick, coins);
        }
    }

    private void Settle(CasinoStage stage, in CasinoStageFrame frame, CasinoStateDto state)
    {
        if (settled || !playback.Finished || !rollup.Done)
        {
            return;
        }

        settled = true;
        var payout = playback.TotalWin + playback.Jackpot;
        stage.Settle(new CasinoBetRecord(TitleOf(machineId), playback.Cost, payout, playback.RoundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        stage.Celebration.Celebrate(playback.Cost, payout, window.Center, frame.Instant, playback.Jackpot > 0);
        resultNet = payout - playback.Cost;
        resultTick = Environment.TickCount64;
        if (playback.Current.Grid is { } grid)
        {
            reels.Remember(grid);
        }

        var auto = Composer.Auto;
        if (auto.Running)
        {
            auto.Settle(playback.Cost, payout, playback.BonusTriggered, SlotsRules.MinStake, store.Ceiling.MaxBet,
                state.Sitting?.Stack ?? 0);
            autoPause = AutoPauseSeconds;
            return;
        }

        if (playback.Jackpot == 0)
        {
            gamble.Offer(playback.RoundId, playback.Bet, playback.TotalWin);
        }
    }

    private void Layout(in CasinoStageFrame frame, float scale)
    {
        var safe = frame.Safe;
        var pad = ChassisPad * scale;
        var glassHeight = Math.Clamp(safe.Height * GlassShare, GlassMin * scale, GlassMax * scale);
        var left = frame.Full.Min.X + (ChassisInset + ChassisPad) * scale;
        var right = frame.Full.Max.X - (ChassisInset + ChassisPad) * scale;
        var top = safe.Min.Y + pad * 0.5f;
        glass = new Rect(new Vector2(left, top), new Vector2(right, top + glassHeight));
        var stripTop = MathF.Max(glass.Max.Y + pad, safe.Max.Y - StripHeight * scale);
        strip = new Rect(new Vector2(left, stripTop), new Vector2(right, stripTop + StripHeight * scale));
        window = new Rect(new Vector2(left, glass.Max.Y + pad * 0.75f),
            new Vector2(right, MathF.Max(glass.Max.Y + pad, stripTop - pad * 0.5f)));
    }

    private void HandleWindowTap(in CasinoStageFrame frame)
    {
        if (frame.Blocked || gamble.Open)
        {
            return;
        }

        var hovered = UiInteract.Hover(window.Min, window.Max);
        if (!UiInteract.Click(window.Min, window.Max, hovered))
        {
            return;
        }

        if (playback.Finished && !rollup.Done)
        {
            rollup.Finish();
        }
    }

    private static float Approach(float value, float target, float step)
    {
        return value < target ? MathF.Min(target, value + step) : MathF.Max(target, value - step);
    }

    private bool Busy => play.RoundInFlight || playback.Active || (playback.HasRound && !settled) || gamble.Open;

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state,
        CasinoSittingDto sitting, float scale)
    {
        if (autoPause > 0f)
        {
            autoPause -= frame.DeltaSeconds;
        }

        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var bet = Composer.Amount;
        var cost = SlotsRules.CostOf(Mode, bet);
        var busy = Busy;
        var enabled = !blocked && sitting.Stack >= SlotsRules.CostOf(Mode, SlotsRules.MinStake);
        var model = new BetComposerModel(SlotsRules.MinStake, store.Ceiling.MaxBet, sitting.Stack,
            L.Machines.SpinFor, enabled && cost <= sitting.Stack, AutoAvailable: true, Knob: true,
            Repeat: stage.RepeatPressed(), Busy: busy);
        var action = Composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds);
        DrawKnob(ui, Composer.KnobRect, sitting, !busy && !Composer.Auto.Running && !blocked, scale);
        if (blocked && Composer.Auto.Running)
        {
            Composer.Auto.Stop(AutoStop.Manual);
        }

        if (buyConfirmed)
        {
            buyConfirmed = false;
            if (!busy && !blocked)
            {
                Spin(stage, bet, SlotsRules.BuyMode);
            }
        }

        if (action == BetComposerAction.Confirm)
        {
            Spin(stage, bet, Mode);
            return;
        }

        if (action == BetComposerAction.StartAuto)
        {
            Spin(stage, Composer.Auto.Next, Mode);
            return;
        }

        if (!Composer.Auto.Running || busy || autoPause > 0f || stage.Celebration.Blocking || !enabled)
        {
            return;
        }

        var next = Composer.Auto.Next;
        if (sitting.Stack < SlotsRules.CostOf(Mode, next))
        {
            Composer.Auto.Stop(AutoStop.Chips);
            return;
        }

        Spin(stage, next, Mode);
    }

    private void Spin(CasinoStage stage, long bet, string mode)
    {
        inlineReason = string.Empty;
        gamble.Collect();
        stage.Celebration.Clear();
        if (play.SpinMachine(machineId, mode, bet))
        {
            CasinoSfx.Play(UiSound.ChipSlide);
            return;
        }

        if (Composer.Auto.Running)
        {
            Composer.Auto.Stop(AutoStop.Refused);
        }
    }

    private void AskBuy(long bet)
    {
        var cost = SlotsRules.CostOf(SlotsRules.BuyMode, bet);
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Machines.BuyConfirmTitle),
            Message = Loc.T(L.Machines.BuyConfirmBody, NumberText.Group(cost)),
            ConfirmLabel = Loc.T(L.Machines.BuyConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = () => buyConfirmed = true,
        });
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(deck.Min.X + inset, deck.Min.Y + inset),
            Typography.FitText(title, deck.Width - inset * 2f, TextStyles.SubheadlineEmphasized), StageInks.Strong,
            TextStyles.SubheadlineEmphasized);
        var top = deck.Min.Y + inset + titleHeight + Metrics.Space.Sm * scale;
        var rect = new Rect(new Vector2(deck.Min.X + inset, top),
            new Vector2(deck.Max.X - inset, top + Button.LargeHeight * scale));
        if (Button.Draw(drawList, rect, Loc.T(L.Casino.Cashier), ui.Ink))
        {
            openCashier();
        }
    }

    private static void DrawNotOpen(ImDrawListPtr drawList, AppSkin ui, Rect safe, float scale)
    {
        var title = Loc.T(L.Machines.NotOpenTitle);
        var body = Loc.T(L.Machines.NotOpenHint);
        var height = CasinoNotice.Height(CasinoNoticeKind.Card, title, body, safe.Width, scale);
        CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Card, title, body, safe.Min.X,
            safe.Center.Y - height * 0.5f, safe.Width, scale);
    }
}
