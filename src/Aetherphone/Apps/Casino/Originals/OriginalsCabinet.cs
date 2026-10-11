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

    private const float AutoPauseSeconds = 0.35f;
    private const float WrapLineSpacing = 1.25f;
    private const string LineProbe = "Ay";

    private static readonly TextStyle StakeStyle = TextStyles.Headline;
    private static readonly TextStyle StakeCaptionStyle = TextStyles.SubheadlineEmphasized;
    private const float SpotlightAlpha = 0.05f;

    private readonly CasinoStore store;
    private readonly CasinoOriginalsStore originals;
    private readonly IOriginalsSkin[] skins;
    private readonly BetComposer[] composers = new BetComposer[GameIds.Length];
    private readonly LadderStep[] ladder = new LadderStep[OriginalsLadder.Capacity];

    private IOriginalsSkin skin;
    private int skinIndex;
    private OriginalsLabel cashOutLabel;
    private LocString notice;
    private bool hasNotice;
    private float autoPause;

    public OriginalsCabinet(CasinoStore store, CasinoOriginalsStore originals)
    {
        this.store = store;
        this.originals = originals;
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
            skins[index].Gate();
        }
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        Composer.DrawOverlay(screen, ui, false);
        skin.DrawOverlay(screen, ui);
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
        var statusHeight = StatusHeight(state, frame.Safe.Width, scale);
        var hasStrip = skin.Strip.Key is not null;
        var layout = OriginalsLayout.Compute(frame.Safe, hasStrip, statusHeight, scale);
        DrawSign(drawList, frame, layout.Sign, scale);
        if (hasStrip)
        {
            var count = skin.FillLadder(ladder, out var focus);
            OriginalsLadder.Draw(drawList, layout.Ladder, Loc.T(skin.Strip), ladder.AsSpan(0, count), focus, ui, scale);
        }

        var interactive = !blocked && !Composer.Auto.Running && sitting is not null;
        skin.DrawWorld(drawList, new OriginalsFrame(stage, originals, layout.World, frame.DeltaSeconds, frame.Phase,
            frame.Instant, interactive, Composer.AutoArmed), ui);
        DrawStatus(drawList, ui, state, layout.Status, scale);
        if (skin.Live)
        {
            DrawLiveDeck(drawList, ui, frame.Deck, blocked, scale);
            skin.Step(originals, Composer.Auto.Running && !blocked);
            return;
        }

        DrawComposer(stage, frame, ui, sitting ?? CasinoWire.NoBankroll, blocked);
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
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), outcome.Capped));
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

    private CasinoNoticeKind NoticeKind() =>
        string.Equals(notice.Key, L.Originals.Resumed.Key, StringComparison.Ordinal)
            ? CasinoNoticeKind.Info
            : CasinoNoticeKind.Reason;

    private string PlainStatus(CasinoStateDto state)
    {
        if (state.StakesPaused || state.Draining)
        {
            return Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle);
        }

        var hint = skin.Hint;
        return hint.Key is null ? string.Empty : Loc.T(hint);
    }

    private float StatusHeight(CasinoStateDto state, float width, float scale)
    {
        var reserve = Typography.LineHeight(TextStyles.Subheadline) * 2f + Metrics.Space.Md * scale;
        var top = Metrics.Space.Xs * scale;
        if (hasNotice)
        {
            return MathF.Max(reserve,
                top + CasinoNotice.Height(NoticeKind(), string.Empty, Loc.T(notice), width, scale));
        }

        var text = PlainStatus(state);
        return text.Length == 0 ? reserve : MathF.Max(reserve, top + WrappedHeight(text, TextStyles.Subheadline, width));
    }

    private static float WrappedHeight(string text, in TextStyle style, float width)
    {
        var block = Typography.MeasureWrappedBlock(text, style, width).Y;
        var lines = MathF.Max(1f, MathF.Round(block / MathF.Max(1f, Typography.LineHeight(style))));
        return lines * Typography.Measure(LineProbe, style).Y * WrapLineSpacing;
    }

    private void DrawStatus(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, Rect area, float scale)
    {
        var top = area.Min.Y + Metrics.Space.Xs * scale;
        if (hasNotice)
        {
            CasinoNotice.Draw(drawList, ui, NoticeKind(), string.Empty, Loc.T(notice), area.Min.X, top, area.Width,
                scale);
            return;
        }

        var text = PlainStatus(state);
        if (text.Length == 0)
        {
            return;
        }

        var ink = state.StakesPaused || state.Draining ? CasinoColors.InkTitle : CasinoColors.InkBody;
        Typography.DrawWrappedCentered(drawList, text, TextStyles.Subheadline, ink, new Vector2(area.Center.X, top),
            area.Width, WrapLineSpacing);
    }

    private void DrawLiveDeck(ImDrawListPtr drawList, AppSkin ui, Rect deck, bool blocked, float scale)
    {
        var pad = BetComposer.Pad * scale;
        var left = deck.Min.X + pad;
        var right = deck.Max.X - pad;
        var bandTop = deck.Min.Y + pad;
        if (skin.Knob)
        {
            var knobBottom = bandTop + BetComposer.KnobHeight * scale;
            skin.DrawKnob(drawList, new Rect(new Vector2(left, bandTop), new Vector2(right, knobBottom)), ui, false);
            bandTop = knobBottom + BetComposer.Gap * scale;
        }

        var row = DeckActions.Row(deck, scale);
        var bandBottom = row.Min.Y - BetComposer.Gap * scale;
        if (bandBottom > bandTop && skin.LiveStake > 0)
        {
            DrawStakeLine(drawList, new Rect(new Vector2(left, bandTop), new Vector2(right, bandBottom)),
                skin.LiveStake, scale);
        }

        var cursor = row.Min.X;
        var secondaries = 0;
        var idle = !blocked && !originals.InFlight;
        var secondary = skin.LiveSecondary;
        if (secondary.Key is not null && DeckActions.DrawSecondary(row, ref cursor, ref secondaries,
                Loc.T(secondary), idle && !skin.Busy && !Composer.Auto.Running, ui.Ink, scale))
        {
            skin.Secondary(originals);
        }

        var label = cashOutLabel.Get(L.Originals.CashOutFor, NumberText.Compact(skin.CashOutValue));
        if (DeckActions.DrawPrimary(row, cursor, label, idle && skin.CanCashOut, ui.Ink,
                id: "casino.originals.cashout"))
        {
            skin.CashOut(originals);
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private static void DrawStakeLine(ImDrawListPtr drawList, Rect band, long stake, float scale)
    {
        var amount = NumberText.Compact(stake);
        var amountSize = CurrencyGlyph.MeasureAmount(amount, StakeStyle);
        if (amountSize.Y > band.Height || amountSize.X > band.Width)
        {
            return;
        }

        var caption = Loc.T(L.Casino.RoundStake);
        var gap = Metrics.Space.Sm * scale;
        var captionWidth = Typography.Measure(caption, StakeCaptionStyle).X;
        var showCaption = captionWidth + gap + amountSize.X <= band.Width;
        var total = showCaption ? captionWidth + gap + amountSize.X : amountSize.X;
        var left = band.Center.X - total * 0.5f;
        if (showCaption)
        {
            var captionHeight = Typography.Measure(caption, StakeCaptionStyle).Y;
            Typography.Draw(drawList, new Vector2(left, band.Center.Y - captionHeight * 0.5f), caption,
                CasinoColors.InkBody, StakeCaptionStyle);
            left += captionWidth + gap;
        }

        CurrencyGlyph.DrawAmount(drawList, new Vector2(left, band.Center.Y - amountSize.Y * 0.5f), amount,
            CurrencyKind.Chips, CasinoColors.Money, StakeStyle);
    }

    private void DrawComposer(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoSittingDto sitting,
        bool blocked)
    {
        if (autoPause > 0f)
        {
            autoPause -= frame.DeltaSeconds;
        }

        var ceiling = store.Ceiling.MaxBet;
        var enabled = !blocked;
        var busy = originals.InFlight || skin.Busy;
        var model = new BetComposerModel(OriginalsRules.MinBet, ceiling, sitting.Stack, skin.Action, enabled,
            AutoAvailable: skin.AutoAvailable, Knob: skin.Knob, Repeat: stage.RepeatPressed(), Busy: busy);
        var action = Composer.Draw(stage, ui, frame.Deck, model, frame.DeltaSeconds);
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

        if (!Composer.Covers(stage, Composer.Auto.Next, sitting.Stack))
        {
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
}
