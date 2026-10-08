using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class OriginalsCabinet
{
    public static readonly string[] GameIds =
    {
        CasinoGames.Mines,
        CasinoGames.Dice,
        CasinoGames.Limbo,
        CasinoGames.Keno,
        CasinoGames.HiLo,
    };

    private const float SignHeight = 30f;
    private const float SectionGap = 8f;
    private const float AutoPauseSeconds = 0.35f;
    private const float SecondaryShare = 0.38f;
    private const float SpotlightAlpha = 0.05f;

    private readonly CasinoStore store;
    private readonly CasinoOriginalsStore originals;
    private readonly Action openCashier;
    private readonly IOriginalsSkin[] skins;
    private readonly BetComposer[] composers = new BetComposer[GameIds.Length];
    private readonly LadderStep[] ladder = new LadderStep[OriginalsLadder.Capacity];

    private IOriginalsSkin skin;
    private int skinIndex;
    private OriginalsLabel cashOutLabel;
    private LocString notice;
    private bool hasNotice;
    private float autoPause;

    public OriginalsCabinet(CasinoStore store, CasinoOriginalsStore originals, Action openCashier)
    {
        this.store = store;
        this.originals = originals;
        this.openCashier = openCashier;
        skins = new IOriginalsSkin[] { new MinesSkin(), new DiceSkin(), new LimboSkin(), new KenoSkin(), new HiLoSkin() };
        for (var index = 0; index < composers.Length; index++)
        {
            composers[index] = new BetComposer("##originalsBet." + GameIds[index]);
        }

        skin = skins[0];
    }

    private BetComposer Composer => composers[skinIndex];

    public static bool Owns(string gameId) => IndexOf(gameId) >= 0;

    public ICabinetIdle? IdleFor(string gameId)
    {
        var index = IndexOf(gameId);
        return index >= 0 ? skins[index] : null;
    }

    public CasinoStageSpec SpecFor(string gameId)
    {
        var index = IndexOf(gameId);
        var target = index >= 0 ? skins[index] : skin;
        return new CasinoStageSpec(gameId, target.Title, Backdrop.Strip,
            DeckHeight: BetComposer.DeckHeightFor(target.Knob, false), BetsRail: true, InstantAvailable: true,
            ReturnTenths: OriginalsRules.ReturnTenths);
    }

    public void Enter(string gameId)
    {
        var index = IndexOf(gameId);
        if (index < 0)
        {
            return;
        }

        skin = skins[index];
        skinIndex = index;
        hasNotice = false;
        autoPause = 0f;
        Composer.Prefill(OriginalsRules.MinBet);
        skin.Enter();
        originals.LoadOpen();
    }

    public void Reset()
    {
        for (var index = 0; index < skins.Length; index++)
        {
            skins[index].Reset();
            composers[index].Auto.Stop(AutoStop.Manual);
            composers[index].Auto.Acknowledge();
        }

        hasNotice = false;
        autoPause = 0f;
    }

    public void Gate()
    {
        for (var index = 0; index < composers.Length; index++)
        {
            composers[index].Gate();
        }
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        Composer.DrawOverlay(screen, ui, false);
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

        if (frame.SnapToTruth)
        {
            skin.Snap();
        }

        Absorb(frame.Instant);
        skin.Advance(frame.DeltaSeconds);
        Settle(stage, frame, state);
        var sitting = state.Sitting;
        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var safe = frame.Safe;
        var signBottom = safe.Min.Y + SignHeight * scale;
        DrawSign(drawList, frame, new Rect(safe.Min, new Vector2(safe.Max.X, signBottom)), scale);
        var ladderTop = signBottom + SectionGap * scale;
        var ladderBottom = ladderTop + OriginalsLadder.Height * scale;
        var count = skin.FillLadder(ladder, out var focus);
        OriginalsLadder.Draw(drawList, new Rect(new Vector2(safe.Min.X, ladderTop), new Vector2(safe.Max.X, ladderBottom)),
            ladder.AsSpan(0, count), focus, ui, scale);
        var statusHeight = Typography.LineHeight(TextStyles.Subheadline) * 2f + Metrics.Space.Md * scale;
        var world = new Rect(new Vector2(safe.Min.X, ladderBottom + SectionGap * scale),
            new Vector2(safe.Max.X, MathF.Max(ladderBottom + SectionGap * scale, safe.Max.Y - statusHeight)));
        var interactive = !blocked && !Composer.Auto.Running && sitting is not null;
        skin.DrawWorld(drawList, new OriginalsFrame(stage, originals, world, frame.DeltaSeconds, frame.Phase,
            frame.Instant, interactive, Composer.AutoTabSelected), ui);
        DrawStatus(drawList, ui, state, new Rect(new Vector2(safe.Min.X, world.Max.Y), safe.Max), scale);
        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, frame.Deck, scale);
            return;
        }

        if (skin.Live)
        {
            DrawLiveDeck(drawList, ui, frame.Deck, blocked, scale);
            skin.Step(originals, Composer.Auto.Running && !blocked);
            return;
        }

        DrawComposer(stage, frame, ui, sitting, blocked);
        skin.Step(originals, Composer.Auto.Running && !blocked);
    }

    private static int IndexOf(string gameId)
    {
        for (var index = 0; index < GameIds.Length; index++)
        {
            if (string.Equals(GameIds[index], gameId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void Absorb(bool instant)
    {
        var open = originals.TakeOpen();
        if (open is not null)
        {
            for (var index = 0; index < skins.Length; index++)
            {
                skins[index].Resume(open);
            }
        }

        for (var index = 0; index < skins.Length; index++)
        {
            skins[index].Consume(originals, instant);
        }

        if (!skin.TakeNotice(out var message))
        {
            return;
        }

        notice = message;
        hasNotice = true;
        if (Composer.Auto.Running && !string.Equals(message.Key, L.Originals.Resumed.Key, StringComparison.Ordinal))
        {
            Composer.Auto.Stop(AutoStop.Refused);
        }
    }

    private void Settle(CasinoStage stage, in CasinoStageFrame frame, CasinoStateDto state)
    {
        if (!skin.TakeSettled(out var outcome))
        {
            return;
        }

        hasNotice = false;
        stage.Settle(new CasinoBetRecord(skin.Title, outcome.Stake, outcome.Payout, outcome.RoundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        stage.Celebration.Celebrate(outcome.Stake, outcome.Payout, skin.Focus, frame.Instant);
        if (!Composer.Auto.Running)
        {
            return;
        }

        var stack = state.Sitting?.Stack ?? 0;
        Composer.Auto.Settle(outcome.Stake, outcome.Payout, false, OriginalsRules.MinBet, store.Ceiling.MaxBet, stack);
        autoPause = AutoPauseSeconds;
    }

    private void DrawSign(ImDrawListPtr drawList, in CasinoStageFrame frame, Rect band, float scale)
    {
        CasinoLights.Spotlight(drawList, new Vector2(band.Center.X, frame.Full.Min.Y), MathF.PI * 0.5f,
            frame.Safe.Max.Y - frame.Full.Min.Y, 0.36f, CasinoColors.MoneyHighlight, SpotlightAlpha);
        var height = CasinoSigns.HeightToFit(skin.Sign, band.Width * 0.6f, band.Height * 0.8f);
        var flicker = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        CasinoSigns.Draw(drawList, skin.Sign, band.Center, height, CasinoColors.LightA, flicker);
    }

    private void DrawStatus(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, Rect area, float scale)
    {
        var top = area.Min.Y + Metrics.Space.Xs * scale;
        if (hasNotice)
        {
            var kind = string.Equals(notice.Key, L.Originals.Resumed.Key, StringComparison.Ordinal)
                ? CasinoNoticeKind.Info
                : CasinoNoticeKind.Reason;
            CasinoNotice.Draw(drawList, ui, kind, string.Empty, Loc.T(notice), area.Min.X, top, area.Width, scale);
            return;
        }

        if (state.StakesPaused || state.Draining)
        {
            Typography.DrawWrappedCentered(drawList,
                Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle), TextStyles.Subheadline,
                ui.MutedInk, new Vector2(area.Center.X, top), area.Width);
            return;
        }

        var hint = skin.Hint;
        if (hint.Key is null)
        {
            return;
        }

        Typography.DrawWrappedCentered(drawList, Loc.T(hint), TextStyles.Subheadline, ui.MutedInk,
            new Vector2(area.Center.X, top), area.Width);
    }

    private void DrawLiveDeck(ImDrawListPtr drawList, AppSkin ui, Rect deck, bool blocked, float scale)
    {
        var pad = BetComposer.Pad * scale;
        var left = deck.Min.X + pad;
        var right = deck.Max.X - pad;
        if (skin.Knob)
        {
            var knobTop = deck.Min.Y + pad;
            skin.DrawKnob(drawList, new Rect(new Vector2(left, knobTop),
                new Vector2(right, knobTop + BetComposer.KnobHeight * scale)), ui, false);
        }

        var actionTop = deck.Max.Y - (BetComposer.Pad + BetComposer.ActionHeight) * scale;
        var actionBottom = actionTop + BetComposer.ActionHeight * scale;
        var idle = !blocked && !originals.InFlight;
        var cashLeft = left;
        var secondary = skin.LiveSecondary;
        if (secondary.Key is not null)
        {
            var secondaryRight = left + (right - left) * SecondaryShare;
            var secondaryRect = new Rect(new Vector2(left, actionTop), new Vector2(secondaryRight, actionBottom));
            if (Button.Draw(drawList, secondaryRect, Loc.T(secondary), ui.Ink, ButtonStyle.Gray,
                    enabled: idle && !skin.Busy && !Composer.Auto.Running))
            {
                skin.Secondary(originals);
            }

            cashLeft = secondaryRight + BetComposer.Gap * scale;
        }

        var label = cashOutLabel.Get(L.Originals.CashOutFor, NumberText.Compact(skin.CashOutValue));
        var cashRect = new Rect(new Vector2(cashLeft, actionTop), new Vector2(right, actionBottom));
        if (Button.Draw(drawList, cashRect, label, ui.Ink, ButtonStyle.Prominent, enabled: idle && skin.CanCashOut,
                id: "casino.originals.cashout"))
        {
            skin.CashOut(originals);
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private void DrawComposer(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoSittingDto sitting,
        bool blocked)
    {
        if (autoPause > 0f)
        {
            autoPause -= frame.DeltaSeconds;
        }

        var ceiling = store.Ceiling.MaxBet;
        var enabled = !blocked && sitting.Stack >= OriginalsRules.MinBet;
        var busy = originals.InFlight || skin.Busy;
        var model = new BetComposerModel(OriginalsRules.MinBet, ceiling, sitting.Stack, skin.Action, enabled,
            AutoAvailable: skin.AutoAvailable, Knob: skin.Knob, Repeat: stage.RepeatPressed(), Busy: busy);
        var action = Composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds);
        if (skin.Knob)
        {
            skin.DrawKnob(ImGui.GetWindowDrawList(), Composer.KnobRect, ui,
                enabled && !busy && !Composer.Auto.Running);
        }

        if (blocked && Composer.Auto.Running)
        {
            Composer.Auto.Stop(AutoStop.Manual);
        }

        if (action == BetComposerAction.Confirm)
        {
            Start(stage, Composer.Amount, false);
            return;
        }

        if (action == BetComposerAction.StartAuto)
        {
            Start(stage, Composer.Auto.Next, true);
            return;
        }

        if (!Composer.Auto.Running || busy || autoPause > 0f || stage.Celebration.Blocking || !enabled)
        {
            return;
        }

        if (sitting.Stack < Composer.Auto.Next)
        {
            Composer.Auto.Stop(AutoStop.Chips);
            return;
        }

        Start(stage, Composer.Auto.Next, true);
    }

    private void Start(CasinoStage stage, long stake, bool auto)
    {
        hasNotice = false;
        stage.Celebration.Clear();
        if (skin.Play(originals, stake, auto))
        {
            return;
        }

        if (Composer.Auto.Running)
        {
            Composer.Auto.Stop(AutoStop.Refused);
        }
    }

    private void DrawSeatMissing(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        var title = Loc.T(L.Casino.CabinetNoChipsTitle);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(deck.Min.X + inset, deck.Min.Y + inset),
            Typography.FitText(title, deck.Width - inset * 2f, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var top = deck.Min.Y + inset + titleHeight + Metrics.Space.Sm * scale;
        var rect = new Rect(new Vector2(deck.Min.X + inset, top),
            new Vector2(deck.Max.X - inset, top + Button.LargeHeight * scale));
        if (Button.Draw(drawList, rect, Loc.T(L.Casino.Cashier), ui.Ink))
        {
            openCashier();
        }
    }
}
