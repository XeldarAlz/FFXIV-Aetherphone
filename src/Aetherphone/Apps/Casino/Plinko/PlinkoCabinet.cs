using Aetherphone.Apps.Casino.Originals;
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

namespace Aetherphone.Apps.Casino.Plinko;

internal sealed class PlinkoCabinet : ICabinetIdle
{
    private const float SignHeight = 30f;
    private const float RailHeight = 26f;
    private const float RailChipWidth = 52f;
    private const float RailGap = 4f;
    private const float SectionGap = 8f;
    private const float RowsShare = 0.42f;
    private const float AutoSpacingSeconds = 0.3f;
    private const float SpotlightAlpha = 0.07f;
    private const float SpotlightSpread = 0.42f;
    private const float EdgeSweep = 0.6f;
    private const float EdgeChaseInset = 6f;
    private const float RailFillAlpha = 0.85f;
    private const int EdgeShards = 24;
    private const int IdleRows = 8;
    private const int IdleMaxBalls = 4;
    private const float IdleDropSeconds = 0.55f;
    private const ulong IdleSeed = 0x91A7C0DEUL;

    private readonly CasinoStore store;
    private readonly CasinoPlinkoStore plinko;
    private readonly Action openCashier;
    private readonly BetComposer composer = new("##plinkoDrop");
    private readonly PlinkoFlight flight = new();
    private readonly PlinkoBoardFx fx = new();
    private readonly PlinkoResultRail rail = new();
    private readonly Ribbon[] trails = new Ribbon[PlinkoFlight.Capacity];
    private readonly PlinkoFlight idleFlight = new();
    private readonly PlinkoBoardFx idleFx = new();
    private readonly Ribbon[] idleTrails = new Ribbon[PlinkoFlight.Capacity];
    private readonly int[] idlePath = new int[IdleRows];
    private readonly string[] rowLabels = new string[PlinkoRules.RowCounts.Length];
    private readonly string[] riskLabels = new string[PlinkoRules.RiskCount];
    private readonly LabelSlot[] rowSlots = new LabelSlot[PlinkoRules.RowCounts.Length];

    private GameRandom idleRandom = GameRandom.FromSeed(IdleSeed);
    private OriginalsLabel edgeLabel;
    private PlinkoBoardLayout layout;
    private Rect boardArea;
    private int rows = PlinkoRules.DefaultRows;
    private int risk = PlinkoRules.DefaultRisk;
    private string noticeReason = string.Empty;
    private long noticeCeiling;
    private bool hasNotice;
    private float autoTimer;
    private int autoLaunched;
    private float idleTimer;

    public PlinkoCabinet(CasinoStore store, CasinoPlinkoStore plinko, Action openCashier)
    {
        this.store = store;
        this.plinko = plinko;
        this.openCashier = openCashier;
        composer.Auto.StopOnBonus = false;
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index] = new Ribbon();
            idleTrails[index] = new Ribbon();
        }
    }

    public static float DeckHeight => BetComposer.DeckHeightFor(true, false);

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public int Rows => rows;

    public int Risk => risk;

    public CasinoStageSpec Spec => new(CasinoGames.Plinko, L.Plinko.Game, Backdrop.Strip, DeckHeight: DeckHeight,
        BetsRail: true, InstantAvailable: true, ReturnTenths: PlinkoRules.ReturnTenths(rows, risk));

    public long DisplayStack() => plinko.DisplayStack();

    public void Enter()
    {
        hasNotice = false;
        autoTimer = 0f;
        composer.Prefill(PlinkoRules.MinBet);
    }

    public void Reset()
    {
        flight.Clear();
        fx.Clear();
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Clear();
        }

        composer.Auto.Stop(AutoStop.Manual);
        composer.Auto.Acknowledge();
        plinko.Forget();
        hasNotice = false;
        autoTimer = 0f;
        autoLaunched = 0;
    }

    public void Gate()
    {
        composer.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        composer.DrawOverlay(screen, ui, false);
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

        var safe = frame.Safe;
        var signBottom = safe.Min.Y + SignHeight * scale;
        var railRect = new Rect(new Vector2(safe.Min.X, signBottom + SectionGap * 0.5f * scale),
            new Vector2(safe.Max.X, signBottom + (SectionGap * 0.5f + RailHeight) * scale));
        var statusHeight = OriginalsControls.StatHeight * scale + Typography.LineHeight(TextStyles.Footnote)
            + SectionGap * 2f * scale;
        var boardTop = railRect.Max.Y + SectionGap * scale;
        boardArea = new Rect(new Vector2(safe.Min.X, boardTop),
            new Vector2(safe.Max.X, MathF.Max(boardTop, safe.Max.Y - statusHeight)));
        layout = BoardLayout(boardArea, rows, risk, scale);
        if (frame.SnapToTruth)
        {
            flight.SnapAll();
            HandleEvents(stage, state, true, scale);
        }

        Consume(stage, state, frame.Instant, scale);
        if (!frame.SnapToTruth)
        {
            flight.Advance(frame.DeltaSeconds);
            HandleEvents(stage, state, false, scale);
        }

        fx.Advance(frame.DeltaSeconds);
        rail.Advance(frame.DeltaSeconds);
        DrawSign(drawList, frame, new Rect(safe.Min, new Vector2(safe.Max.X, signBottom)), scale);
        DrawRail(drawList, ui, railRect, scale);
        DrawBoard(drawList, frame, scale);
        DrawStatus(drawList, ui, state, new Rect(new Vector2(safe.Min.X, boardArea.Max.Y + SectionGap * scale),
            safe.Max), scale);
        DrawDeck(stage, frame, ui, state, scale);
    }

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var idleLayout = BoardLayout(rect, IdleRows, PlinkoRules.Medium, scale);
        idleTimer -= deltaSeconds;
        if (deltaSeconds > 0f && idleTimer <= 0f && idleFlight.ActiveCount < IdleMaxBalls)
        {
            LaunchIdle();
            idleTimer = IdleDropSeconds;
        }

        idleFlight.Advance(deltaSeconds);
        for (var index = 0; index < idleFlight.EventCount; index++)
        {
            var entry = idleFlight.Event(index);
            if (entry.Kind == PlinkoEventKind.Peg)
            {
                idleFx.Flash(entry.Row, entry.Column);
                continue;
            }

            idleTrails[entry.Ball].Clear();
            idleFx.Land(entry.Drop.Slot, entry.Drop.Won, false);
        }

        idleFx.Advance(deltaSeconds);
        var strip = PlinkoRules.Strip(IdleRows, PlinkoRules.Medium);
        PlinkoArt.Pegs(drawList, idleLayout, idleFx, scale);
        PlinkoArt.Slots(drawList, idleLayout, strip, idleFx, idleTimer, scale);
        DrawBalls(drawList, idleFlight, idleTrails, idleLayout);
    }

    private static PlinkoBoardLayout BoardLayout(Rect area, int boardRows, int boardRisk, float scale)
    {
        var plain = PlinkoBoardLayout.Compute(area, boardRows);
        if (PlinkoArt.LabelsFitInside(PlinkoRules.Strip(boardRows, boardRisk), plain.SlotWidth, scale))
        {
            return plain;
        }

        return PlinkoBoardLayout.Compute(area, boardRows, PlinkoArt.LegendHeight(scale));
    }

    private void LaunchIdle()
    {
        for (var row = 0; row < idlePath.Length; row++)
        {
            idlePath[row] = idleRandom.Next(2);
        }

        var slot = PlinkoRules.SlotOf(idlePath);
        var tenths = PlinkoRules.MultiplierTenths(IdleRows, PlinkoRules.Medium, slot);
        var drop = new PlinkoDrop(IdleRows, PlinkoRules.Medium, slot, tenths, PlinkoRules.MinBet,
            PlinkoRules.Payout(PlinkoRules.MinBet, tenths), string.Empty);
        if (idleFlight.Launch(drop, idlePath, idleRandom.NextUInt(), 0f, out var ball))
        {
            idleTrails[ball].Clear();
        }
    }

    private void Consume(CasinoStage stage, CasinoStateDto state, bool instant, float scale)
    {
        while (plinko.TryTake(out var result))
        {
            if (!result.Granted)
            {
                Raise(result.Reason, result.Ceiling);
                continue;
            }

            hasNotice = false;
            var path = result.Path ?? Array.Empty<int>();
            var drop = new PlinkoDrop(result.Rows, result.Risk, result.Slot, result.MultiplierTenths, result.Stake,
                result.Payout, result.RoundId);
            var valid = PlinkoRules.IsValidRisk(result.Risk) && PlinkoRules.IsPath(path, result.Rows, result.Slot);
            if (valid && flight.ActiveCount == 0 && (result.Rows != rows || result.Risk != risk))
            {
                rows = result.Rows;
                risk = result.Risk;
                fx.Clear();
                layout = BoardLayout(boardArea, rows, risk, scale);
            }

            var animate = valid && !instant && result.Rows == rows && result.Risk == risk;
            if (animate && flight.Launch(drop, path, PlinkoFlight.SeedOf(result.RoundId), 0f, out var ball))
            {
                trails[ball].Clear();
                continue;
            }

            Settle(stage, state, drop, true, scale);
        }

        if (plinko.TakeFailure())
        {
            Raise(CasinoReasons.Unreachable, 0);
        }
    }

    private void HandleEvents(CasinoStage stage, CasinoStateDto state, bool snapped, float scale)
    {
        for (var index = 0; index < flight.EventCount; index++)
        {
            var entry = flight.Event(index);
            if (entry.Kind == PlinkoEventKind.Peg)
            {
                fx.Flash(entry.Row, entry.Column);
                CasinoSfx.Pitched(UiSound.PegTick, entry.Row);
                continue;
            }

            trails[entry.Ball].Clear();
            Settle(stage, state, entry.Drop, snapped, scale);
        }
    }

    private void Settle(CasinoStage stage, CasinoStateDto state, in PlinkoDrop drop, bool instant, float scale)
    {
        plinko.Land(drop.Payout);
        var onBoard = drop.Rows == rows && drop.Risk == risk;
        if (onBoard)
        {
            fx.Land(drop.Slot, drop.Won, drop.Edge);
        }

        rail.Push(new PlinkoResult(drop.Tenths, drop.Rows, drop.Risk, drop.Edge));
        stage.Settle(new CasinoBetRecord(L.Plinko.Game, drop.Stake, drop.Payout, drop.RoundId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        var origin = onBoard ? layout.SlotCenter(drop.Slot) : layout.Bounds.Center;
        var tier = WinLadder.TierFor(drop.Stake, drop.Payout);
        var celebration = stage.Celebration;
        if (tier != WinTier.None && (!celebration.Active || tier >= celebration.Tier))
        {
            celebration.Celebrate(drop.Stake, drop.Payout, origin, instant);
        }

        if (drop.Edge && !instant)
        {
            CasinoSfx.Play(UiSound.Fanfare);
            stage.Particles.Emit(CasinoLights.Shard(scale), origin, EdgeShards);
            CasinoLights.LightSweep(stage.Backdrop, EdgeSweep);
        }

        if (!composer.Auto.Running)
        {
            return;
        }

        var stack = Math.Max(0, (state.Sitting?.Stack ?? 0) - plinko.QueuedStake);
        composer.Auto.Settle(drop.Stake, drop.Payout, false, PlinkoRules.MinBet, store.Ceiling.MaxBet, stack);
    }

    private void Raise(string reason, long ceiling)
    {
        noticeReason = reason.Length > 0 ? reason : CasinoReasons.Unreachable;
        noticeCeiling = ceiling;
        hasNotice = true;
        if (composer.Auto.Running)
        {
            composer.Auto.Stop(AutoStop.Refused);
        }
    }

    private void DrawSign(ImDrawListPtr drawList, in CasinoStageFrame frame, Rect band, float scale)
    {
        CasinoLights.Spotlight(drawList, new Vector2(band.Center.X, frame.Full.Min.Y), MathF.PI * 0.5f,
            MathF.Max(1f, layout.Bounds.Max.Y - frame.Full.Min.Y), SpotlightSpread, CasinoColors.MoneyHighlight,
            SpotlightAlpha);
        var height = CasinoSigns.HeightToFit(CasinoSign.Plinko, band.Width * 0.6f, band.Height * 0.8f);
        var flicker = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        CasinoSigns.Draw(drawList, CasinoSign.Plinko, band.Center, height, CasinoColors.LightA, flicker);
    }

    private void DrawRail(ImDrawListPtr drawList, AppSkin ui, Rect row, float scale)
    {
        if (rail.Count == 0)
        {
            var empty = Typography.FitText(Loc.T(L.Plinko.RailEmpty), row.Width, TextStyles.Footnote);
            Typography.DrawCentered(drawList, row.Center, empty, StageInks.Strong, TextStyles.Footnote);
            return;
        }

        var gap = RailGap * scale;
        var chipWidth = RailChipWidth * scale;
        var fit = Math.Max(1, (int)((row.Width + gap) / (chipWidth + gap)));
        var visible = Math.Min(rail.Count, fit);
        var style = TextStyles.FootnoteEmphasized;
        for (var age = 0; age < visible; age++)
        {
            var result = rail.Newest(age);
            var left = row.Min.X + age * (chipWidth + gap);
            var grow = age == 0 ? 0.6f + 0.4f * rail.Arrival : 1f;
            var center = new Vector2(left + chipWidth * 0.5f, row.Center.Y);
            var half = new Vector2(chipWidth, row.Height) * 0.5f * grow;
            var tint = PlinkoArt.SlotTint(result.Tenths, PlinkoRules.EdgeTenths(result.Rows, result.Risk));
            Squircle.Fill(drawList, center - half, center + half, half.Y,
                ImGui.GetColorU32(tint with { W = RailFillAlpha }));
            if (result.Edge)
            {
                Squircle.Stroke(drawList, center - half, center + half, half.Y,
                    ImGui.GetColorU32(CasinoColors.MoneyHighlight), MathF.Max(1f, 1.4f * scale));
            }

            var label = CasinoMultiples.Label(result.Tenths * PlinkoRules.TenthsPerMultiple);
            Typography.DrawCentered(drawList, center, label, GamePalette.InkOn(tint), style.Scale * grow,
                style.Weight);
        }
    }

    private void DrawBoard(ImDrawListPtr drawList, in CasinoStageFrame frame, float scale)
    {
        if (fx.Edge > 0f)
        {
            CasinoLights.BulbChase(drawList, layout.Bounds.Inset(-EdgeChaseInset * scale), Metrics.Radius.Grouped * scale,
                scale, frame.Phase, CasinoLights.BulbPitch, CasinoColors.Money, CasinoColors.LightA, fx.Edge);
        }

        var strip = PlinkoRules.Strip(rows, risk);
        PlinkoArt.Pegs(drawList, layout, fx, scale);
        PlinkoArt.Slots(drawList, layout, strip, fx, frame.Phase, scale);
        DrawBalls(drawList, flight, trails, layout);
        PlinkoArt.Pops(drawList, layout, strip, fx);
    }

    private static void DrawBalls(ImDrawListPtr drawList, PlinkoFlight balls, Ribbon[] ribbons,
        in PlinkoBoardLayout board)
    {
        var spacing = Ribbon.Spacing(board.Pitch / PlinkoFlight.SegmentSeconds);
        for (var ball = 0; ball < PlinkoFlight.Capacity; ball++)
        {
            if (!balls.IsActive(ball))
            {
                continue;
            }

            var position = board.ToScreen(balls.Position(ball));
            ribbons[ball].PushSpaced(position, spacing);
            PlinkoArt.Ball(drawList, position, board.BallRadius, ribbons[ball]);
        }
    }

    private void DrawStatus(ImDrawListPtr drawList, AppSkin ui, CasinoStateDto state, Rect area, float scale)
    {
        if (hasNotice)
        {
            CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty,
                CasinoReasons.Text(noticeReason, noticeCeiling), area.Min.X, area.Min.Y, area.Width, scale);
            return;
        }

        if (state.StakesPaused || state.Draining)
        {
            Typography.DrawWrappedCentered(drawList,
                Loc.T(state.StakesPaused ? L.Casino.PausedTitle : L.Casino.DrainingTitle), TextStyles.Subheadline,
                CasinoColors.InkBody, new Vector2(area.Center.X, area.Min.Y), area.Width);
            return;
        }

        var edgeTenths = PlinkoRules.EdgeTenths(rows, risk);
        var ceiling = store.Ceiling.MaxBet;
        var maxWin = ceiling > 0
            ? NumberText.Compact(PlinkoRules.Payout(ceiling, edgeTenths))
            : CasinoMultiples.Label(edgeTenths * PlinkoRules.TenthsPerMultiple);
        var odds = edgeLabel.Get(L.Plinko.EdgeOdds, NumberText.Group(PlinkoRules.EdgeOneIn(rows)));
        var statRow = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + OriginalsControls.StatHeight * scale));
        OriginalsControls.StatRow(drawList, statRow, Loc.T(L.Plinko.MaxWin), maxWin, CasinoColors.Money,
            Loc.T(L.Plinko.Edge), odds, CasinoColors.InkTitle, ui, scale);
        if (rail.Count > 0 || flight.ActiveCount > 0)
        {
            return;
        }

        var hint = Typography.FitText(Loc.T(L.Plinko.Hint), area.Width, TextStyles.Footnote);
        Typography.DrawCentered(drawList, new Vector2(area.Center.X,
                statRow.Max.Y + SectionGap * 0.5f * scale + Typography.LineHeight(TextStyles.Footnote) * 0.5f), hint,
            StageInks.Strong, TextStyles.Footnote);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoStateDto state, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (!store.HasFeature(PlinkoRules.Feature))
        {
            DrawClosed(drawList, ui, frame.Deck, scale);
            return;
        }

        var sitting = state.Sitting;
        if (sitting is null)
        {
            DrawSeatMissing(drawList, ui, frame.Deck, scale);
            return;
        }

        var blocked = state.StakesPaused || state.Draining || frame.Blocked;
        var stack = Math.Max(0, sitting.Stack - plinko.QueuedStake);
        var inFlight = flight.ActiveCount + plinko.Outstanding;
        var full = inFlight >= PlinkoRules.MaxInFlight;
        var enabled = !blocked && stack >= PlinkoRules.MinBet;
        var model = new BetComposerModel(PlinkoRules.MinBet, store.Ceiling.MaxBet, stack, L.Plinko.DropFor, enabled,
            AutoAvailable: true, Knob: true, Repeat: stage.RepeatPressed(), Busy: full);
        var action = composer.Draw(ui, frame.Deck, model, frame.DeltaSeconds);
        DrawKnobs(composer.KnobRect, ui, enabled && inFlight == 0 && !composer.Auto.Running);
        if (blocked && composer.Auto.Running)
        {
            composer.Auto.Stop(AutoStop.Manual);
        }

        if (action == BetComposerAction.Confirm)
        {
            Drop(composer.Amount);
            return;
        }

        if (action == BetComposerAction.StartAuto)
        {
            autoLaunched = 0;
            autoTimer = 0f;
        }

        StepAuto(stage, frame.DeltaSeconds, stack, inFlight, enabled);
    }

    private void StepAuto(CasinoStage stage, float deltaSeconds, long stack, int inFlight, bool enabled)
    {
        var auto = composer.Auto;
        if (!auto.Running)
        {
            return;
        }

        autoTimer -= deltaSeconds;
        if (autoTimer > 0f || inFlight >= PlinkoRules.MaxInFlight || stage.Celebration.Blocking || !enabled
            || (auto.Rounds > 0 && autoLaunched >= auto.Rounds))
        {
            return;
        }

        if (stack < auto.Next)
        {
            if (inFlight == 0)
            {
                auto.Stop(AutoStop.Chips);
            }

            return;
        }

        if (Drop(auto.Next))
        {
            autoLaunched++;
            autoTimer = AutoSpacingSeconds;
            return;
        }

        if (inFlight == 0)
        {
            auto.Stop(AutoStop.Refused);
        }
    }

    private bool Drop(long stake)
    {
        if (!plinko.Drop(rows, risk, stake))
        {
            return false;
        }

        hasNotice = false;
        CasinoSfx.Play(UiSound.ChipSlide);
        return true;
    }

    private void DrawKnobs(Rect rect, AppSkin ui, bool changeable)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var gap = BetComposer.Gap * UiScale.Current;
        var split = rect.Min.X + (rect.Width - gap) * RowsShare;
        var rowsRect = new Rect(rect.Min, new Vector2(split, rect.Max.Y));
        var riskRect = new Rect(new Vector2(split + gap, rect.Min.Y), rect.Max);
        for (var index = 0; index < rowLabels.Length; index++)
        {
            rowLabels[index] = rowSlots[index].Get(L.Plinko.RowsOption, PlinkoRules.RowCounts[index]);
        }

        riskLabels[PlinkoRules.Low] = Loc.T(L.Originals.RiskLow);
        riskLabels[PlinkoRules.Medium] = Loc.T(L.Originals.RiskMedium);
        riskLabels[PlinkoRules.High] = Loc.T(L.Originals.RiskHigh);
        var track = Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary);
        var pickedRows = SegmentStrip.Draw("casino.plinko.rows", rowsRect, rowLabels,
            PlinkoRules.RowsIndex(rows), track, ui.Accent, ui.MutedInk, CasinoColors.InkTitle);
        var pickedRisk = SegmentStrip.Draw("casino.plinko.risk", riskRect, riskLabels, risk, track, ui.Accent,
            ui.MutedInk, CasinoColors.InkTitle);
        if (!changeable)
        {
            return;
        }

        if (pickedRows >= 0 && pickedRows < PlinkoRules.RowCounts.Length && PlinkoRules.RowCounts[pickedRows] != rows)
        {
            rows = PlinkoRules.RowCounts[pickedRows];
            fx.Clear();
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (pickedRisk != risk && PlinkoRules.IsValidRisk(pickedRisk))
        {
            risk = pickedRisk;
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private static void DrawClosed(ImDrawListPtr drawList, AppSkin ui, Rect deck, float scale)
    {
        var inset = BetComposer.Pad * scale;
        CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Card, Loc.T(L.Plinko.NotOpenTitle),
            Loc.T(L.Plinko.NotOpenBody), deck.Min.X + inset, deck.Min.Y + inset, deck.Width - inset * 2f, scale);
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
