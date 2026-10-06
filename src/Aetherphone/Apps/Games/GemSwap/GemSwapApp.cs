using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapApp : IMiniGame
{
    internal const string BlitzStatId = "match3.blitz";
    private const string GameId = "match3";
    private const int BlitzMode = 1;
    private const float SwapDuration = 0.14f;
    private const float SwapBackDuration = 0.12f;
    private const float ClearDuration = 0.26f;
    private const float FallDuration = 0.30f;
    private const float HintDelay = 6f;
    private const float BannerSeconds = 1.5f;
    private const float FinaleDelay = 1.1f;
    private const float FinaleSweepPause = 0.35f;
    private const int MaxFinaleSweeps = 6;
    private const int BeamCapacity = 16;
    private const float BeamSeconds = 0.34f;
    private const float StormSeconds = 0.42f;
    private const float UrgentSeconds = 10f;
    private const int SlowMoChain = 4;
    private const float SlowMoFactor = 0.6f;
    private const float SlowMoSeconds = 0.25f;
    private const int MaxChainLabel = 32;
    private const float FrostFollow = 5f;
    private const int DustPerGem = 2;
    private static readonly LocString[] Modes = { L.Games.Classic, L.Games.Blitz };
    private static readonly string[] ModeStatIds = { GameId, BlitzStatId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.GemSwap, GameGenre.Puzzle, L.GemSwap.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true);
    private static readonly string?[] ChainLabels = new string?[MaxChainLabel + 1];
    private static readonly Dictionary<int, string> GainLabels = new();
    private static readonly Vector4 FireTint = new(1f, 0.52f, 0.2f, 1f);
    private static readonly Vector4 HotTint = new(1f, 0.38f, 0.42f, 1f);
    private static readonly Vector4 TimeUpTint = new(0.98f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Dust = new(0.85f, 0.85f, 0.92f, 0.7f);

    private struct Beam
    {
        public Vector2 From;
        public Vector2 To;
        public Vector4 Color;
        public float Life;
    }

    private readonly GemSwapBoard board = new();
    private readonly GemSwapBlitz blitz = new();
    private readonly GemSwapPowerDock dock = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Beam[] beams = new Beam[BeamCapacity];
    private ComboMeter combo = ComboMeter.Create();
    private LabelSlot bonusLabel;
    private GameGrid grid;
    private float entrance;
    private int previousScore;
    private GemPhase phase;
    private GemMode mode;
    private GemStage stage;
    private int swapA = -1;
    private int swapB = -1;
    private float swapTimer;
    private float clearTimer;
    private float fallTimer;
    private int selectedIndex = -1;
    private int chain;
    private int bestChain;
    private int swaps;
    private float idleTime;
    private int hintA = -1;
    private int hintB = -1;
    private float bannerProgress = 1f;
    private float finaleTimer;
    private int finaleSweeps;
    private float frostShown;
    private int lastSecond;
    private int beamCount;
    private int stormColumn;
    private float stormTimer;
    private int comboCell = -1;
    private ulong idleSeed;
    private bool idleSynced;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        mode = start.Mode == BlitzMode ? GemMode.Blitz : GemMode.Classic;
        board.Reset(start.Random);
        idleSeed = start.Seed;
        idleSynced = true;
        blitz.Reset();
        dock.Reset();
        particles.Clear();
        fx.Clear();
        combo.Reset();
        entrance = 0f;
        previousScore = 0;
        phase = GemPhase.Idle;
        stage = GemStage.Playing;
        swapA = -1;
        swapB = -1;
        swapTimer = 0f;
        clearTimer = 0f;
        fallTimer = 0f;
        selectedIndex = -1;
        chain = 0;
        bestChain = 0;
        swaps = 0;
        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        bannerProgress = 1f;
        finaleTimer = 0f;
        finaleSweeps = 0;
        frostShown = 0f;
        lastSecond = (int)MathF.Ceiling(GemSwapBlitz.StartSeconds);
        beamCount = 0;
        stormTimer = 0f;
        comboCell = -1;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        idleSynced = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        SyncIdle(context.Session.Seed);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var blitzMode = context.Session.Mode == BlitzMode;
        Layout(context.Safe, blitzMode, scale, out grid, out var plate);
        GemSwapRenderer.DrawPlate(drawList, plate, scale, Accent, context.Backdrop.Ink);
        GemSwapRenderer.DrawBoard(drawList, board, grid, GemAnim.Still, context.Theme, scale, 1f, 0f);
        if (blitzMode)
        {
            dock.DrawPowers(drawList, grid, blitz, scale, false);
        }
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var session = context.Session;
        var theme = context.Theme;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        combo.Update(context.DeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        var blitzMode = mode == GemMode.Blitz;
        if (blitzMode)
        {
            TickBlitz(context);
        }

        dock.Update(blitz, context.RawDeltaSeconds);
        UpdateEffects(context.RawDeltaSeconds);
        var area = Grow(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        Layout(area, blitzMode, scale, out grid, out var plate);
        AdvanceAnimation(simDelta, scale, context);
        if (stage == GemStage.Finale)
        {
            AdvanceFinale(simDelta, scale, context);
        }

        var interactive = stage == GemStage.Playing && phase == GemPhase.Idle && !finished &&
                          session.State == StageFlow.Playing;
        if (interactive)
        {
            HandleInput();
        }

        DrawHeat();
        GemSwapRenderer.DrawPlate(drawList, plate, scale, Accent, context.Backdrop.Ink);
        if (blitzMode)
        {
            dock.DrawCharge(drawList, GemSwapPowerDock.Band(grid, scale), Accent, scale);
        }

        var anim = new GemAnim(phase, swapA, swapB, MathF.Min(1f, swapTimer), MathF.Min(1f, clearTimer),
            MathF.Min(1f, fallTimer), selectedIndex, hintA, hintB, idleTime);
        GemSwapRenderer.DrawBoard(drawList, board, grid, anim, theme, scale, entrance, frostShown);
        DrawBeams(drawList, scale);
        DrawStorm(drawList, scale);
        if (blitzMode)
        {
            var clicked = dock.DrawPowers(drawList, grid, blitz, scale, interactive);
            if (clicked >= 0 && blitz.TryFire(clicked))
            {
                FirePower((GemPower)clicked, scale, context);
            }
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, grid.Center, Loc.T(L.Games.GemSwapTimeUp), TimeUpTint, theme, bannerProgress,
            TextStyles.LargeTitle);
        context.Hud.Score(board.Score);
        if (blitzMode)
        {
            var urgent = stage == GemStage.Playing && blitz.TimeLeft < UrgentSeconds && !blitz.Frozen;
            context.Hud.Timer(blitz.TimeLeft, GemSwapBlitz.StartSeconds, urgent);
            if (urgent)
            {
                context.Fx.Vignette(TimeUpTint, 0.10f + 0.12f * Pulse.Wave(Pulse.Fast), 0.3f);
            }
        }

        context.Hud.Combo(combo);
        context.Hud.Best(session.Best);
        session.Report(board.Score);
    }

    private void SyncIdle(ulong seed)
    {
        if (idleSynced && idleSeed == seed)
        {
            return;
        }

        board.Reset(GameRandom.FromSeed(seed));
        idleSeed = seed;
        idleSynced = true;
    }

    private static void Layout(Rect safe, bool blitzMode, float scale, out GameGrid boardGrid, out Rect plate)
    {
        var band = blitzMode ? GemSwapPowerDock.BandHeight * scale : 0f;
        var inset = BoardPlate.Padding * scale;
        var gridArea = new Rect(new Vector2(safe.Min.X + inset, safe.Min.Y + inset),
            new Vector2(safe.Max.X - inset, safe.Max.Y - inset - band));
        boardGrid = GameGrid.Centered(gridArea, GemSwapBoard.Columns, GemSwapBoard.Rows,
            GemSwapRenderer.GapFraction, band * 0.5f);
        var core = BoardPlate.Around(boardGrid.Bounds, scale);
        plate = blitzMode ? new Rect(core.Min, new Vector2(core.Max.X, core.Max.Y + band)) : core;
    }

    private void TickBlitz(in GameContext context)
    {
        var running = stage == GemStage.Playing;
        if (running && blitz.Tick(context.DeltaSeconds))
        {
            BeginFinale(context);
        }

        var frostTarget = running && blitz.Frozen ? 1f : 0f;
        frostShown += (frostTarget - frostShown) * MathF.Min(1f, context.RawDeltaSeconds * FrostFollow);
        var second = (int)MathF.Ceiling(blitz.TimeLeft);
        if (running && second < lastSecond && second > 0 && blitz.TimeLeft < UrgentSeconds && !blitz.Frozen)
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        lastSecond = second;
    }

    private void AdvanceAnimation(float deltaSeconds, float scale, in GameContext context)
    {
        switch (phase)
        {
            case GemPhase.Swapping:
                swapTimer += deltaSeconds / SwapDuration;
                if (swapTimer >= 1f)
                {
                    CompleteSwap(scale, context);
                }

                return;
            case GemPhase.SwapBack:
                swapTimer += deltaSeconds / SwapBackDuration;
                if (swapTimer >= 1f)
                {
                    board.Swap(swapA, swapB);
                    phase = GemPhase.Idle;
                    swapA = -1;
                    swapB = -1;
                    idleTime = 0f;
                }

                return;
            case GemPhase.Clearing:
                clearTimer += deltaSeconds / ClearDuration;
                if (clearTimer >= 1f)
                {
                    board.RemoveMatched();
                    board.ApplyGravity();
                    phase = GemPhase.Falling;
                    fallTimer = 0f;
                }

                return;
            case GemPhase.Falling:
                fallTimer += deltaSeconds / FallDuration;
                if (fallTimer >= 1f)
                {
                    OnLanded(scale);
                    board.ClearFall();
                    chain++;
                    if (board.ResolveMatches(chain) > 0)
                    {
                        OnCleared(scale, context);
                    }
                    else
                    {
                        EndCascade(context);
                    }
                }

                return;
            default:
                idleTime += deltaSeconds;
                if (stage == GemStage.Playing && idleTime >= HintDelay && hintA < 0)
                {
                    board.FindHint(out hintA, out hintB);
                }

                return;
        }
    }

    private void OnLanded(float scale)
    {
        var landed = false;
        var half = (grid.Pitch - grid.Gap) * 0.5f;
        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            if (board.FallFrom(index) == GemSwapBoard.NoFall)
            {
                continue;
            }

            landed = true;
            var center = grid.CellCenter(index % GemSwapBoard.Columns, index / GemSwapBoard.Columns);
            particles.Burst(new Vector2(center.X, center.Y + half), DustPerGem, Dust, 55f * scale, 2.4f, 0.32f, 140f,
                MathF.PI * 0.9f, -MathF.PI * 0.5f);
        }

        if (!landed)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameHitSoft);
        fx.AddTrauma(0.03f);
    }

    private void EndCascade(in GameContext context)
    {
        chain = 0;
        phase = GemPhase.Idle;
        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        if (stage != GemStage.Playing)
        {
            return;
        }

        if (mode == GemMode.Classic && !board.HasPossibleMoves())
        {
            FinishClassic(context);
            return;
        }

        board.ReshuffleIfStuck();
    }

    private void CompleteSwap(float scale, in GameContext context)
    {
        board.Swap(swapA, swapB);
        if (board.IsComboSwap(swapA, swapB))
        {
            swaps++;
            chain = 1;
            comboCell = swapB;
            board.ResolveCombo(swapB, swapA, chain);
            OnCleared(scale, context);
            return;
        }

        if (!board.HasAnyMatch())
        {
            phase = GemPhase.SwapBack;
            swapTimer = 0f;
            return;
        }

        swaps++;
        chain = 1;
        board.ResolveMatches(chain);
        OnCleared(scale, context);
    }

    private void BeginFinale(in GameContext context)
    {
        stage = GemStage.Finale;
        selectedIndex = -1;
        hintA = -1;
        hintB = -1;
        bannerProgress = 0f;
        finaleTimer = 0f;
        finaleSweeps = 0;
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Flash(TimeUpTint, 0.22f);
        fx.AddTrauma(0.3f);
    }

    private void AdvanceFinale(float deltaSeconds, float scale, in GameContext context)
    {
        finaleTimer += deltaSeconds;
        if (finaleTimer < FinaleDelay || phase != GemPhase.Idle)
        {
            return;
        }

        if (finaleSweeps < MaxFinaleSweeps && board.HasSpecials)
        {
            finaleSweeps++;
            finaleTimer = FinaleDelay - FinaleSweepPause;
            chain = 1;
            board.DetonateSpecials(chain);
            OnCleared(scale, context);
            return;
        }

        stage = GemStage.Over;
        FinishBlitz(context);
    }

    private void FinishBlitz(in GameContext context)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, BlitzStatId)
            .WithStat(L.Games.Combo, GameNumber.Label(bestChain))
            .WithStat(L.GemSwap.Bonuses, GameNumber.Label(blitz.BonusCount)));
    }

    private void FinishClassic(in GameContext context)
    {
        if (finished)
        {
            return;
        }

        finished = true;
        stage = GemStage.Over;
        selectedIndex = -1;
        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.Flash(TimeUpTint, 0.15f);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(bestChain))
            .WithStat(L.Games.Moves, GameNumber.Label(swaps)));
    }

    private void OnCleared(float scale, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameMatch);
        phase = GemPhase.Clearing;
        clearTimer = 0f;
        var cleared = board.LastClearCount;
        fx.AddTrauma(MathF.Min(0.55f, 0.05f + cleared * 0.03f));
        var multiplierBefore = combo.Multiplier;
        combo.Hit();
        if (chain > bestChain)
        {
            bestChain = chain;
        }

        for (var index = 0; index < GemSwapBoard.CellCount; index++)
        {
            if (!board.Matched(index) || board.Color(index) < 0)
            {
                continue;
            }

            var center = grid.CellCenter(index % GemSwapBoard.Columns, index / GemSwapBoard.Columns);
            var color = GemSwapRenderer.ColorOf(board.Color(index));
            particles.Burst(center, 7, color, 140f * scale, 2.8f, 0.5f, 260f);
            particles.Sparkle(center, 3, GamePalette.Lighten(color, 0.4f), 110f * scale, 2f, 0.6f);
        }

        SpawnActivationEffects(scale, context);
        SpawnComboEffects(scale, context);
        var scoreDelta = board.Score - previousScore;
        if (scoreDelta > 0)
        {
            fx.AddText(GainLabel(scoreDelta), new Vector2(grid.Center.X, grid.Bounds.Min.Y - 14f * scale), Accent,
                1.1f);
        }

        previousScore = board.Score;
        if (chain > 1)
        {
            fx.AddText(ChainLabel(chain), grid.Center, Accent, 1.5f);
            fx.Shockwave(grid.Center, grid.Pitch * (1.2f + 0.3f * chain), GamePalette.Lighten(Accent, 0.3f), 0.55f, 3f);
            if (mode == GemMode.Classic)
            {
                fx.HitStop(MathF.Min(0.08f, 0.03f + chain * 0.01f));
            }
        }

        if (chain >= SlowMoChain)
        {
            context.Fx.SlowMo(SlowMoFactor, SlowMoSeconds);
        }

        if (combo.Multiplier > multiplierBefore)
        {
            GameSfx.ComboTierUp();
        }

        if (mode == GemMode.Blitz && stage == GemStage.Playing)
        {
            FeedBlitz(scale, context);
        }
    }

    private void SpawnActivationEffects(float scale, in GameContext context)
    {
        if (board.ActivatedCount > 0)
        {
            UiFeedback.Play(UiSound.GameExplosion);
        }

        for (var slot = 0; slot < board.ActivatedCount; slot++)
        {
            var cell = board.ActivatedCell(slot);
            var column = cell % GemSwapBoard.Columns;
            var row = cell / GemSwapBoard.Columns;
            var center = grid.CellCenter(column, row);
            var color = GemSwapRenderer.ColorOf(board.Color(cell));
            var bright = GamePalette.Lighten(color, 0.35f);
            switch (board.ActivatedKind(slot))
            {
                case GemSpecial.LineHorizontal:
                    SweepRow(row, column, bright, scale);
                    fx.AddTrauma(0.12f);
                    break;
                case GemSpecial.LineVertical:
                    SweepColumn(column, row, bright, scale);
                    fx.AddTrauma(0.12f);
                    break;
                case GemSpecial.Burst:
                    fx.Shockwave(center, grid.Pitch * 1.9f, bright, 0.5f, 4f);
                    particles.Burst(center, 16, color, 260f * scale, 3.4f, 0.6f, 300f);
                    fx.AddTrauma(0.16f);
                    context.Fx.Punch(0.03f);
                    break;
                case GemSpecial.Prism:
                    fx.Shockwave(center, grid.Pitch * 3.5f, White with { W = 0.85f }, 0.65f, 4f);
                    context.Fx.Flash(White, 0.22f);
                    context.Fx.Sweep();
                    context.Fx.Punch(0.05f);
                    particles.Sparkle(center, 20, White, 220f * scale, 3f, 0.8f);
                    fx.AddTrauma(0.22f);
                    break;
            }
        }
    }

    private void SpawnComboEffects(float scale, in GameContext context)
    {
        if (board.LastCombo == GemCombo.None || comboCell < 0)
        {
            return;
        }

        var center = grid.CellCenter(comboCell % GemSwapBoard.Columns, comboCell / GemSwapBoard.Columns);
        var ring = GamePalette.Lighten(Accent, 0.4f);
        var prismBoard = board.LastCombo == GemCombo.PrismBoard;
        fx.Shockwave(center, grid.Pitch * 4.5f, ring, 0.7f, 5f);
        fx.Shockwave(center, grid.Pitch * 2.6f, White with { W = 0.8f }, 0.45f, 3f);
        context.Fx.Flash(ring, prismBoard ? 0.45f : 0.25f);
        context.Fx.Punch(prismBoard ? 0.08f : 0.05f);
        fx.AddTrauma(prismBoard ? 0.6f : 0.35f);
        particles.Streaks(center, 18, ring, 460f * scale, 2.8f, 0.5f);
        comboCell = -1;
    }

    private void SweepRow(int row, int originColumn, Vector4 color, float scale)
    {
        var y = grid.CellCenter(0, row).Y;
        AddBeam(new Vector2(grid.Bounds.Min.X, y), new Vector2(grid.Bounds.Max.X, y), color);
        for (var column = 0; column < GemSwapBoard.Columns; column++)
        {
            var direction = column < originColumn ? MathF.PI : 0f;
            particles.Streaks(grid.CellCenter(column, row), 2, color, 420f * scale, 2.4f, 0.35f, 0.35f, direction);
        }
    }

    private void SweepColumn(int column, int originRow, Vector4 color, float scale)
    {
        var x = grid.CellCenter(column, 0).X;
        AddBeam(new Vector2(x, grid.Bounds.Min.Y), new Vector2(x, grid.Bounds.Max.Y), color);
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            var direction = row < originRow ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            particles.Streaks(grid.CellCenter(column, row), 2, color, 420f * scale, 2.4f, 0.35f, 0.35f, direction);
        }
    }

    private void FeedBlitz(float scale, in GameContext context)
    {
        if (blitz.AddClear(board.LastClearCount, chain))
        {
            var band = GemSwapPowerDock.Band(grid, scale);
            dock.OnBonus();
            fx.AddText(bonusLabel.Get(L.Games.GemSwapBonusTime, (int)GemSwapBlitz.BonusSeconds),
                new Vector2(grid.Center.X, grid.Bounds.Min.Y + grid.Pitch * 0.5f), GamePalette.Lighten(Accent, 0.3f),
                1.4f);
            particles.Sparkle(band.Center, 16, GamePalette.Lighten(Accent, 0.45f), 200f * scale, 2.6f, 0.7f);
            context.Fx.Flash(Accent, 0.12f);
            UiFeedback.Play(UiSound.GamePowerUp);
        }

        for (var power = 0; power < GemSwapBlitz.PowerCount; power++)
        {
            var gems = board.ClearedOfColor(GemSwapBlitz.PowerColor(power));
            if (!blitz.AddCharge(power, gems))
            {
                continue;
            }

            var center = GemSwapPowerDock.ButtonCenter(grid, power, scale);
            var color = GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor(power));
            dock.OnReady(power);
            fx.Shockwave(center, GemSwapPowerDock.ButtonSize(scale) * 2.2f, GamePalette.Lighten(color, 0.4f), 0.5f, 3f);
            particles.Sparkle(center, 12, GamePalette.Lighten(color, 0.4f), 160f * scale, 2.4f, 0.6f);
            UiFeedback.Play(UiSound.GameCollect);
        }
    }

    private void FirePower(GemPower power, float scale, in GameContext context)
    {
        var color = GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor((int)power));
        var bright = GamePalette.Lighten(color, 0.35f);
        var button = GemSwapPowerDock.ButtonCenter(grid, (int)power, scale);
        fx.Shockwave(button, GemSwapPowerDock.ButtonSize(scale) * 2.6f, bright, 0.5f, 3.5f);
        fx.AddText(Loc.T(PowerName(power)), new Vector2(grid.Center.X, grid.Bounds.Min.Y + grid.Pitch), bright, 1.4f);
        UiFeedback.Play(UiSound.GamePowerUp);
        selectedIndex = -1;
        hintA = -1;
        hintB = -1;
        idleTime = 0f;
        int cleared;
        switch (power)
        {
            case GemPower.Fire:
            {
                cleared = GemSwapPowers.Fire(board, 1, out var origin);
                var originColumn = origin % GemSwapBoard.Columns;
                var originRow = origin / GemSwapBoard.Columns;
                var last = GemSwapPowers.FireSize - 1;
                var center = (grid.CellCenter(originColumn, originRow) +
                    grid.CellCenter(originColumn + last, originRow + last)) * 0.5f;
                fx.Shockwave(center, grid.Pitch * 3.2f, FireTint, 0.6f, 5f);
                context.Fx.Flash(FireTint, 0.22f);
                context.Fx.Punch(0.05f);
                fx.AddTrauma(0.35f);
                particles.Burst(center, 30, FireTint, 320f * scale, 4f, 0.8f, -160f);
                break;
            }
            case GemPower.Gale:
            {
                cleared = GemSwapPowers.Gale(board, 1, out var firstRow, out var secondRow);
                SweepRow(firstRow, -1, bright, scale);
                SweepRow(secondRow, -1, bright, scale);
                fx.AddTrauma(0.25f);
                break;
            }
            case GemPower.Storm:
            {
                cleared = GemSwapPowers.Storm(board, 1, out var column);
                stormColumn = column;
                stormTimer = StormSeconds;
                SweepColumn(column, -1, bright, scale);
                context.Fx.Flash(White, 0.3f);
                context.Fx.Punch(0.05f);
                fx.AddTrauma(0.4f);
                break;
            }
            default:
                context.Fx.Flash(GemSwapRenderer.FrostTint, 0.3f);
                fx.Shockwave(grid.Center, grid.Width * 0.6f, GemSwapRenderer.FrostTint, 0.6f, 3.5f);
                for (var index = 0; index < GemSwapBoard.CellCount; index += 4)
                {
                    particles.Sparkle(grid.CellCenter(index % GemSwapBoard.Columns, index / GemSwapBoard.Columns), 2,
                        GemSwapRenderer.FrostTint, 70f * scale, 2.4f, 0.9f);
                }

                return;
        }

        if (cleared <= 0)
        {
            return;
        }

        chain = 1;
        OnCleared(scale, context);
    }

    private static LocString PowerName(GemPower power) => power switch
    {
        GemPower.Fire => L.Games.GemSwapFire,
        GemPower.Frost => L.Games.GemSwapFrost,
        GemPower.Gale => L.Games.GemSwapGale,
        _ => L.Games.GemSwapStorm,
    };

    private void AddBeam(Vector2 from, Vector2 to, Vector4 color)
    {
        if (beamCount >= BeamCapacity)
        {
            return;
        }

        ref var beam = ref beams[beamCount];
        beam.From = from;
        beam.To = to;
        beam.Color = color;
        beam.Life = BeamSeconds;
        beamCount++;
    }

    private void UpdateEffects(float deltaSeconds)
    {
        stormTimer = MathF.Max(0f, stormTimer - deltaSeconds);
        for (var index = beamCount - 1; index >= 0; index--)
        {
            ref var beam = ref beams[index];
            beam.Life -= deltaSeconds;
            if (beam.Life > 0f)
            {
                continue;
            }

            beams[index] = beams[beamCount - 1];
            beamCount--;
        }
    }

    private void DrawHeat()
    {
        var heat = combo.Heat;
        if (heat <= 0.01f)
        {
            return;
        }

        var pulse = Pulse.Wave(Pulse.Fast);
        var hot = Vector4.Lerp(Accent, HotTint, heat);
        ProgressRing.Glow(grid.Center, grid.Width * (0.7f + 0.1f * pulse * heat), hot, heat * (1.2f + 0.6f * pulse));
    }

    private void DrawBeams(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < beamCount; index++)
        {
            ref readonly var beam = ref beams[index];
            var fade = beam.Life / BeamSeconds;
            var width = 6f * scale * fade;
            drawList.AddLine(beam.From, beam.To, ImGui.GetColorU32(beam.Color with { W = 0.35f * fade }), width * 3f);
            drawList.AddLine(beam.From, beam.To, ImGui.GetColorU32(White with { W = 0.9f * fade }), MathF.Max(1f, width));
        }
    }

    private void DrawStorm(ImDrawListPtr drawList, float scale)
    {
        if (stormTimer <= 0f)
        {
            return;
        }

        const int segments = 9;
        var fade = stormTimer / StormSeconds;
        var x = grid.CellCenter(stormColumn, 0).X;
        var top = grid.Bounds.Min.Y - grid.Pitch * 0.6f;
        var step = (grid.Bounds.Max.Y - top) / segments;
        var time = (float)ImGui.GetTime();
        var glow = ImGui.GetColorU32(GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor((int)GemPower.Storm)) with
        {
            W = 0.5f * fade,
        });
        var core = ImGui.GetColorU32(White with { W = fade });
        var previous = new Vector2(x, top);
        for (var segment = 1; segment <= segments; segment++)
        {
            var jitter = segment == segments ? 0f : MathF.Sin(segment * 2.3f + time * 40f) * grid.Pitch * 0.28f;
            var next = new Vector2(x + jitter, top + step * segment);
            drawList.AddLine(previous, next, glow, 9f * scale * fade);
            drawList.AddLine(previous, next, core, MathF.Max(1f, 2.6f * scale * fade));
            previous = next;
        }
    }

    private void HandleInput()
    {
        var bounds = grid.Bounds;
        if (!UiInteract.Hover(bounds.Min, bounds.Max))
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var local = ImGui.GetMousePos() - grid.Origin;
        var column = (int)(local.X / grid.Pitch);
        var row = (int)(local.Y / grid.Pitch);
        if (column < 0 || column >= GemSwapBoard.Columns || row < 0 || row >= GemSwapBoard.Rows)
        {
            return;
        }

        var index = row * GemSwapBoard.Columns + column;
        var cell = grid.Cell(column, row);
        if (!UiInteract.Hover(cell.Min, cell.Max))
        {
            return;
        }

        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        if (selectedIndex < 0)
        {
            selectedIndex = index;
            return;
        }

        if (selectedIndex == index)
        {
            selectedIndex = -1;
            return;
        }

        if (!GemSwapBoard.AreAdjacent(selectedIndex, index))
        {
            selectedIndex = index;
            return;
        }

        swapA = selectedIndex;
        swapB = index;
        selectedIndex = -1;
        phase = GemPhase.Swapping;
        swapTimer = 0f;
    }

    private static string ChainLabel(int value)
    {
        var index = Math.Clamp(value, 0, MaxChainLabel);
        return ChainLabels[index] ??= string.Concat("x", GameNumber.Label(index));
    }

    private static string GainLabel(int points)
    {
        if (GainLabels.TryGetValue(points, out var label))
        {
            return label;
        }

        label = string.Concat("+", GameNumber.Label(points));
        GainLabels[points] = label;
        return label;
    }

    private static Rect Grow(Rect rect, float factor)
    {
        var half = rect.Size * 0.5f * factor;
        return new Rect(rect.Center - half, rect.Center + half);
    }
}
