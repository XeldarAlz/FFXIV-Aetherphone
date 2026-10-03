using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.GemSwap;

internal sealed class GemSwapApp : IMiniGame
{
    private const string GameId = "match3";
    internal const string BlitzStatId = "match3.blitz";
    private const float SwapDuration = 0.14f;
    private const float SwapBackDuration = 0.12f;
    private const float ClearDuration = 0.26f;
    private const float FallDuration = 0.30f;
    private const float HintDelay = 6f;
    private const float BannerSeconds = 1.5f;
    private const float FinaleDelay = 1.1f;
    private const float FinaleSweepPause = 0.35f;
    private const int MaxFinaleSweeps = 6;
    private const float FlyInSeconds = 0.6f;
    private const int BeamCapacity = 16;
    private const float BeamSeconds = 0.34f;
    private const float StormSeconds = 0.42f;
    private const float FeverDecay = 0.35f;
    private static readonly Vector4 FireTint = new(1f, 0.52f, 0.2f, 1f);
    private static readonly Vector4 HotTint = new(1f, 0.38f, 0.42f, 1f);
    private static readonly Vector4 TimeUpTint = new(0.98f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private struct Beam
    {
        public Vector2 From;
        public Vector2 To;
        public Vector4 Color;
        public float Life;
    }

    private readonly GemSwapBoard board = new();
    private readonly GemSwapRenderer renderer = new();
    private readonly GemSwapBlitz blitz = new();
    private readonly GemSwapBlitzHud blitzHud = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly string[] modeLabels = new string[2];
    private readonly Beam[] beams = new Beam[BeamCapacity];
    private RollingValue scoreRoll;
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
    private float idleTime;
    private int hintA = -1;
    private int hintB = -1;
    private int loadedBest;
    private int blitzBest;
    private bool bestsLoaded;
    private bool classicRunActive;
    private GameStatsStore? statsRef;
    private bool pendingSubmit;
    private bool newBest;
    private float resultAppear;
    private string? resultSecondary;
    private float bannerProgress = 1f;
    private float finaleTimer;
    private int finaleSweeps;
    private float fever;
    private int comboShown;
    private string comboLabel = string.Empty;
    private float flyProgress = 1f;
    private Vector2 flyFrom;
    private Vector2 clockCenter;
    private string bonusLabel = string.Empty;
    private int beamCount;
    private int stormColumn;
    private float stormTimer;
    private int comboCell = -1;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.GemSwap);
    public GameGenre Genre => GameGenre.Puzzle;
    public bool RunsOnAClock => mode == GemMode.Blitz && (stage == GemStage.Playing || stage == GemStage.Finale);

    public void Open()
    {
        bestsLoaded = false;
        ResetToReady();
    }

    public void Close()
    {
        PersistClassic();
    }

    public void Dispose()
    {
        PersistClassic();
    }

    private int ModeBest => mode == GemMode.Blitz ? blitzBest : loadedBest;

    private void PersistClassic()
    {
        if (!classicRunActive)
        {
            return;
        }

        classicRunActive = false;
        statsRef?.SubmitScore(GameId, board.Score);
        if (board.Score > loadedBest)
        {
            loadedBest = board.Score;
        }
    }

    private void ResetToReady()
    {
        PersistClassic();
        board.Reset();
        blitz.Reset();
        blitzHud.Reset();
        particles.Clear();
        fx.Clear();
        scoreRoll.Snap(0);
        entrance = 0f;
        previousScore = 0;
        phase = GemPhase.Idle;
        stage = GemStage.Ready;
        swapA = -1;
        swapB = -1;
        selectedIndex = -1;
        chain = 0;
        idleTime = 0f;
        hintA = -1;
        hintB = -1;
        pendingSubmit = false;
        newBest = false;
        resultAppear = 0f;
        resultSecondary = null;
        bannerProgress = 1f;
        finaleTimer = 0f;
        finaleSweeps = 0;
        fever = 0f;
        flyProgress = 1f;
        beamCount = 0;
        stormTimer = 0f;
        comboCell = -1;
    }

    private void BeginRun()
    {
        stage = GemStage.Playing;
        idleTime = 0f;
        if (mode == GemMode.Classic)
        {
            classicRunActive = true;
            return;
        }

        blitz.Reset();
        blitzHud.Reset();
    }

    public void Draw(in GameContext context)
    {
        var deltaSeconds = context.DeltaSeconds;
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        statsRef = context.Stats;
        LoadBests(context.Stats);
        SubmitBlitzIfPending(context.Stats);
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        entrance = GameJuice.Advance(entrance, deltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, deltaSeconds, BannerSeconds);
        var blitzMode = mode == GemMode.Blitz;
        if (blitzMode && stage == GemStage.Playing && blitz.Tick(deltaSeconds))
        {
            BeginFinale();
        }

        if (blitzHud.Update(blitz, deltaSeconds, blitzMode && stage == GemStage.Playing))
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        UpdateEffects(deltaSeconds);
        var rowY = body.Min.Y + 30f * scale;
        var shake = fx.ShakeOffset(scale);
        var bottomReserve = blitzMode ? GemSwapBlitzHud.DockHeight : 6f;
        var gridArea = new Rect(new Vector2(body.Min.X, body.Min.Y + 64f * scale) + shake,
            new Vector2(body.Max.X, body.Max.Y - bottomReserve * scale) + shake);
        var grid = GameGrid.Centered(gridArea, GemSwapBoard.Columns, GemSwapBoard.Rows, 0.06f);
        AdvanceAnimation(fx.ScaleDelta(deltaSeconds), grid);
        if (stage == GemStage.Finale)
        {
            AdvanceFinale(deltaSeconds, grid);
        }

        if (stage == GemStage.Playing && phase == GemPhase.Idle)
        {
            HandleInput(grid);
        }

        RefreshComboLabel();
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        DrawFever(drawList, body, grid);
        if (DrawHud(body, rowY, theme, deltaSeconds, scale))
        {
            return;
        }

        if (chain > 1)
        {
            var comboPulse = 1f + 0.08f * Pulse.Wave(Pulse.Fast);
            Typography.DrawCentered(new Vector2(body.Center.X, rowY + 28f * scale), comboLabel, Accent,
                TextStyles.Headline.Scale * comboPulse, TextStyles.Headline.Weight);
        }

        var anim = new GemAnim(phase, swapA, swapB, MathF.Min(1f, swapTimer), MathF.Min(1f, clearTimer),
            MathF.Min(1f, fallTimer), selectedIndex, hintA, hintB, idleTime);
        renderer.Draw(board, grid, anim, theme, scale, Accent, entrance, blitzMode ? blitzHud.FrostShown : 0f);
        DrawBeams(drawList, scale);
        DrawStorm(drawList, grid, scale);
        if (blitzMode)
        {
            DrawDock(drawList, grid, scale);
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawFlash(drawList, body, 0f);
        fx.DrawText();
        DrawFlyIn(drawList, scale);
        GameBanner.Draw(drawList, grid.Center, Loc.T(L.Games.GemSwapTimeUp), TimeUpTint, theme, bannerProgress,
            TextStyles.LargeTitle);
        if (stage == GemStage.Ready)
        {
            DrawStartCard(drawList, grid, theme, scale);
        }
        else if (stage == GemStage.Over)
        {
            DrawResult(body, theme, deltaSeconds);
        }
    }

    private void LoadBests(GameStatsStore stats)
    {
        if (bestsLoaded)
        {
            return;
        }

        bestsLoaded = true;
        loadedBest = Math.Max(loadedBest, stats.Get(GameId).BestScore);
        blitzBest = Math.Max(blitzBest, stats.Get(BlitzStatId).BestScore);
    }

    private void SubmitBlitzIfPending(GameStatsStore stats)
    {
        if (!pendingSubmit)
        {
            return;
        }

        pendingSubmit = false;
        var previousBest = blitzBest;
        newBest = stats.SubmitScore(BlitzStatId, board.Score);
        if (newBest)
        {
            blitzBest = board.Score;
        }

        resultSecondary = !newBest && previousBest > 0
            ? $"{Loc.T(L.Games.Best)} {GameNumber.Label(previousBest)}"
            : null;
    }

    private void RefreshComboLabel()
    {
        if (chain == comboShown)
        {
            return;
        }

        comboShown = chain;
        comboLabel = chain > 1 ? "x" + GameNumber.Label(chain) : string.Empty;
    }

    private bool DrawHud(Rect body, float rowY, PhoneTheme theme, float deltaSeconds, float scale)
    {
        var best = ModeBest;
        var beatingBest = board.Score > 0 && board.Score > best;
        GameHud.ScorePill(new Vector2(body.Center.X - 68f * scale, rowY), Loc.T(L.Games.Score), ref scoreRoll,
            board.Score, Accent, theme, deltaSeconds, beatingBest);
        var rightCenter = new Vector2(body.Center.X + 20f * scale, rowY);
        if (mode == GemMode.Blitz && stage != GemStage.Ready)
        {
            clockCenter = rightCenter;
            blitzHud.DrawClock(ImGui.GetWindowDrawList(), clockCenter, blitz, theme, Accent, scale);
        }
        else
        {
            var displayBest = Math.Max(best, board.Score);
            GameHud.Pill(rightCenter, Loc.T(L.Games.Best), GameNumber.Label(displayBest), Accent, theme,
                displayBest > best);
        }

        if (!GameHud.RestartButton(new Vector2(body.Max.X - 20f * scale, rowY), 16f * scale, theme))
        {
            return false;
        }

        ResetToReady();
        return true;
    }

    private void DrawStartCard(ImDrawListPtr drawList, GameGrid grid, PhoneTheme theme, float scale)
    {
        modeLabels[0] = Loc.T(L.Games.Classic);
        modeLabels[1] = Loc.T(L.Games.Blitz);
        var hint = Loc.T(mode == GemMode.Blitz ? L.Games.GemSwapBlitzHint : L.Games.GemSwapClassicHint);
        var tapText = Loc.T(L.Games.TapToStart);
        var padding = 16f * scale;
        var halfWidth = MathF.Min(grid.Width * 0.46f, 150f * scale);
        var contentWidth = halfWidth * 2f - padding * 2f;
        var tapScale = Typography.FitScale(tapText, contentWidth, TextStyles.Title2.Scale,
            TextStyles.Title2.Scale * 0.6f, TextStyles.Title2.Weight);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, contentWidth).Y;
        var tapHeight = Typography.Measure(tapText, tapScale, TextStyles.Title2.Weight).Y;
        var stripHeight = 30f * scale;
        var gap = 12f * scale;
        var cardHeight = padding * 2f + stripHeight + gap + hintHeight + gap * 1.5f + tapHeight;
        var min = new Vector2(grid.Center.X - halfWidth, grid.Center.Y - cardHeight * 0.5f);
        var max = new Vector2(grid.Center.X + halfWidth, grid.Center.Y + cardHeight * 0.5f);
        var radius = 22f * scale;
        Material.Veil(drawList, grid.Bounds.Min, grid.Bounds.Max, 0.32f, 10f * scale);
        ProgressRing.Glow(grid.Center, halfWidth * 1.1f, Accent, 0.4f);
        Material.Frosted(drawList, min, max, radius, scale);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.35f }), 1f * scale);
        var stripRow = new Rect(new Vector2(min.X + padding, min.Y + padding),
            new Vector2(max.X - padding, min.Y + padding + stripHeight));
        var selection = SegmentStrip.Draw("match3.mode", stripRow, modeLabels, (int)mode, theme);
        var hintCenter = new Vector2(grid.Center.X, stripRow.Max.Y + gap + hintHeight * 0.5f);
        Typography.DrawWrappedCentered(drawList, hintCenter, hint, theme.TextMuted, TextStyles.Footnote, contentWidth);
        var pulse = 1f + 0.05f * Pulse.Wave(Pulse.Calm);
        var tapCenter = new Vector2(grid.Center.X, hintCenter.Y + hintHeight * 0.5f + gap * 1.5f + tapHeight * 0.5f);
        Typography.DrawCentered(drawList, tapCenter, tapText, theme.TextStrong, tapScale * pulse,
            TextStyles.Title2.Weight);
        if (selection != (int)mode)
        {
            mode = (GemMode)selection;
            entrance = 0f;
            return;
        }

        var overStrip = UiInteract.Hover(stripRow.Min, stripRow.Max);
        var overGrid = UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max);
        if (overGrid && !overStrip)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!overGrid || overStrip || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePop);
        BeginRun();
    }

    private void DrawResult(Rect body, PhoneTheme theme, float deltaSeconds)
    {
        resultAppear = MathF.Min(1f, resultAppear + deltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GemSwapTimeUp), Accent, Loc.T(L.Games.Score),
            GameNumber.Label(board.Score), resultSecondary, newBest);
        if (!GameOverlay.Draw(body, theme, Accent, resultAppear, result))
        {
            return;
        }

        ResetToReady();
        BeginRun();
    }

    private void DrawDock(ImDrawListPtr drawList, GameGrid grid, float scale)
    {
        blitzHud.DrawBar(drawList, GemSwapBlitzHud.BarRect(grid, scale), Accent, scale);
        var interactive = stage == GemStage.Playing && phase == GemPhase.Idle;
        var clicked = blitzHud.DrawPowers(drawList, grid, blitz, scale, interactive);
        if (clicked < 0 || !blitz.TryFire(clicked))
        {
            return;
        }

        FirePower((GemPower)clicked, grid, scale);
    }

    private void AdvanceAnimation(float deltaSeconds, GameGrid grid)
    {
        switch (phase)
        {
            case GemPhase.Swapping:
                swapTimer += deltaSeconds / SwapDuration;
                if (swapTimer >= 1f)
                {
                    CompleteSwap(grid);
                }

                break;
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

                break;
            case GemPhase.Clearing:
                clearTimer += deltaSeconds / ClearDuration;
                if (clearTimer >= 1f)
                {
                    board.RemoveMatched();
                    board.ApplyGravity();
                    phase = GemPhase.Falling;
                    fallTimer = 0f;
                }

                break;
            case GemPhase.Falling:
                fallTimer += deltaSeconds / FallDuration;
                if (fallTimer >= 1f)
                {
                    board.ClearFall();
                    chain++;
                    if (board.ResolveMatches(chain) > 0)
                    {
                        OnCleared(grid);
                    }
                    else
                    {
                        chain = 0;
                        board.ReshuffleIfStuck();
                        phase = GemPhase.Idle;
                        idleTime = 0f;
                        hintA = -1;
                        hintB = -1;
                    }
                }

                break;
            case GemPhase.Idle:
                idleTime += deltaSeconds;
                if (stage == GemStage.Playing && idleTime >= HintDelay && hintA < 0)
                {
                    board.FindHint(out hintA, out hintB);
                }

                break;
        }
    }

    private void CompleteSwap(GameGrid grid)
    {
        board.Swap(swapA, swapB);
        if (board.IsComboSwap(swapA, swapB))
        {
            chain = 1;
            comboCell = swapB;
            board.ResolveCombo(swapB, swapA, chain);
            OnCleared(grid);
            return;
        }

        if (board.HasAnyMatch())
        {
            chain = 1;
            board.ResolveMatches(chain);
            OnCleared(grid);
        }
        else
        {
            phase = GemPhase.SwapBack;
            swapTimer = 0f;
        }
    }

    private void BeginFinale()
    {
        stage = GemStage.Finale;
        selectedIndex = -1;
        hintA = -1;
        hintB = -1;
        bannerProgress = 0f;
        finaleTimer = 0f;
        finaleSweeps = 0;
        UiFeedback.Play(UiSound.GameClear);
        fx.Flash(TimeUpTint, 0.22f);
        fx.AddTrauma(0.3f);
    }

    private void AdvanceFinale(float deltaSeconds, GameGrid grid)
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
            OnCleared(grid);
            return;
        }

        stage = GemStage.Over;
        pendingSubmit = true;
        resultAppear = 0f;
    }

    private void OnCleared(GameGrid grid)
    {
        UiFeedback.Play(UiSound.GameMatch);
        phase = GemPhase.Clearing;
        clearTimer = 0f;
        var scale = UiScale.Current;
        var cleared = board.LastClearCount;
        fx.AddTrauma(MathF.Min(0.55f, 0.05f + cleared * 0.03f));
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

        SpawnActivationEffects(grid, scale);
        SpawnComboEffects(grid, scale);
        var scoreDelta = board.Score - previousScore;
        if (scoreDelta > 0)
        {
            fx.AddText($"+{GameNumber.Label(scoreDelta)}",
                new Vector2(grid.Center.X, grid.Bounds.Min.Y - 14f * scale), Accent, 1.1f);
        }

        previousScore = board.Score;
        fever = MathF.Max(fever, chain > 1 ? MathF.Min(1f, (chain - 1) * 0.3f) : 0.12f);
        if (chain > 1)
        {
            fx.AddText($"x{chain}", grid.Center, Accent, 1.5f);
            fx.Shockwave(grid.Center, grid.Pitch * (1.2f + 0.3f * chain), GamePalette.Lighten(Accent, 0.3f), 0.55f, 3f);
            if (mode == GemMode.Classic)
            {
                fx.HitStop(MathF.Min(0.08f, 0.03f + chain * 0.01f));
            }
        }

        if (mode == GemMode.Blitz && stage == GemStage.Playing)
        {
            FeedBlitz(grid, scale);
        }
    }

    private void SpawnActivationEffects(GameGrid grid, float scale)
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
                    SweepRow(grid, row, column, bright, scale);
                    fx.AddTrauma(0.12f);
                    break;
                case GemSpecial.LineVertical:
                    SweepColumn(grid, column, row, bright, scale);
                    fx.AddTrauma(0.12f);
                    break;
                case GemSpecial.Burst:
                    fx.Shockwave(center, grid.Pitch * 1.9f, bright, 0.5f, 4f);
                    particles.Burst(center, 16, color, 260f * scale, 3.4f, 0.6f, 300f);
                    fx.AddTrauma(0.16f);
                    break;
                case GemSpecial.Prism:
                    fx.Shockwave(center, grid.Pitch * 3.5f, White with { W = 0.85f }, 0.65f, 4f);
                    fx.Flash(White, 0.22f);
                    particles.Sparkle(center, 20, White, 220f * scale, 3f, 0.8f);
                    fx.AddTrauma(0.22f);
                    break;
            }
        }
    }

    private void SpawnComboEffects(GameGrid grid, float scale)
    {
        if (board.LastCombo == GemCombo.None || comboCell < 0)
        {
            return;
        }

        var center = grid.CellCenter(comboCell % GemSwapBoard.Columns, comboCell / GemSwapBoard.Columns);
        var ring = GamePalette.Lighten(Accent, 0.4f);
        fx.Shockwave(center, grid.Pitch * 4.5f, ring, 0.7f, 5f);
        fx.Shockwave(center, grid.Pitch * 2.6f, White with { W = 0.8f }, 0.45f, 3f);
        fx.Flash(ring, board.LastCombo == GemCombo.PrismBoard ? 0.45f : 0.25f);
        fx.AddTrauma(board.LastCombo == GemCombo.PrismBoard ? 0.6f : 0.35f);
        particles.Streaks(center, 18, ring, 460f * scale, 2.8f, 0.5f);
        comboCell = -1;
    }

    private void SweepRow(GameGrid grid, int row, int originColumn, Vector4 color, float scale)
    {
        var y = grid.CellCenter(0, row).Y;
        AddBeam(new Vector2(grid.Bounds.Min.X, y), new Vector2(grid.Bounds.Max.X, y), color);
        for (var column = 0; column < GemSwapBoard.Columns; column++)
        {
            var direction = column < originColumn ? MathF.PI : 0f;
            particles.Streaks(grid.CellCenter(column, row), 2, color, 420f * scale, 2.4f, 0.35f, 0.35f, direction);
        }
    }

    private void SweepColumn(GameGrid grid, int column, int originRow, Vector4 color, float scale)
    {
        var x = grid.CellCenter(column, 0).X;
        AddBeam(new Vector2(x, grid.Bounds.Min.Y), new Vector2(x, grid.Bounds.Max.Y), color);
        for (var row = 0; row < GemSwapBoard.Rows; row++)
        {
            var direction = row < originRow ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
            particles.Streaks(grid.CellCenter(column, row), 2, color, 420f * scale, 2.4f, 0.35f, 0.35f, direction);
        }
    }

    private void FeedBlitz(GameGrid grid, float scale)
    {
        if (blitz.AddClear(board.LastClearCount, chain))
        {
            var bar = GemSwapBlitzHud.BarRect(grid, scale);
            blitzHud.OnBonus();
            flyFrom = new Vector2(bar.Max.X, bar.Center.Y);
            flyProgress = 0f;
            bonusLabel = Loc.T(L.Games.GemSwapBonusTime, (int)GemSwapBlitz.BonusSeconds);
            particles.Sparkle(bar.Center, 16, GamePalette.Lighten(Accent, 0.45f), 200f * scale, 2.6f, 0.7f);
            UiFeedback.Play(UiSound.GamePowerUp);
        }

        for (var power = 0; power < GemSwapBlitz.PowerCount; power++)
        {
            var gems = board.ClearedOfColor(GemSwapBlitz.PowerColor(power));
            if (!blitz.AddCharge(power, gems))
            {
                continue;
            }

            var center = GemSwapBlitzHud.ButtonCenter(grid, power, scale);
            var color = GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor(power));
            blitzHud.OnReady(power);
            fx.Shockwave(center, GemSwapBlitzHud.ButtonSize(scale) * 2.2f, GamePalette.Lighten(color, 0.4f), 0.5f, 3f);
            particles.Sparkle(center, 12, GamePalette.Lighten(color, 0.4f), 160f * scale, 2.4f, 0.6f);
            UiFeedback.Play(UiSound.GameCollect);
        }
    }

    private void FirePower(GemPower power, GameGrid grid, float scale)
    {
        var color = GemSwapRenderer.ColorOf(GemSwapBlitz.PowerColor((int)power));
        var bright = GamePalette.Lighten(color, 0.35f);
        var button = GemSwapBlitzHud.ButtonCenter(grid, (int)power, scale);
        fx.Shockwave(button, GemSwapBlitzHud.ButtonSize(scale) * 2.6f, bright, 0.5f, 3.5f);
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
                fx.Flash(FireTint, 0.22f);
                fx.AddTrauma(0.35f);
                particles.Burst(center, 30, FireTint, 320f * scale, 4f, 0.8f, -160f);
                break;
            }
            case GemPower.Gale:
            {
                cleared = GemSwapPowers.Gale(board, 1, out var firstRow, out var secondRow);
                SweepRow(grid, firstRow, -1, bright, scale);
                SweepRow(grid, secondRow, -1, bright, scale);
                fx.AddTrauma(0.25f);
                break;
            }
            case GemPower.Storm:
            {
                cleared = GemSwapPowers.Storm(board, 1, out var column);
                stormColumn = column;
                stormTimer = StormSeconds;
                SweepColumn(grid, column, -1, bright, scale);
                fx.Flash(White, 0.3f);
                fx.AddTrauma(0.4f);
                break;
            }
            default:
                fx.Flash(GemSwapRenderer.FrostTint, 0.3f);
                fx.Shockwave(clockCenter, GemSwapBlitzHud.ClockRadius * scale * 2.6f, GemSwapRenderer.FrostTint, 0.6f,
                    3.5f);
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
        OnCleared(grid);
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
        fever = MathF.Max(0f, fever - FeverDecay * deltaSeconds);
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

        if (flyProgress >= 1f)
        {
            return;
        }

        flyProgress = MathF.Min(1f, flyProgress + deltaSeconds / FlyInSeconds);
        if (flyProgress < 1f)
        {
            return;
        }

        var scale = UiScale.Current;
        blitzHud.OnBonusArrived();
        fx.Shockwave(clockCenter, GemSwapBlitzHud.ClockRadius * scale * 2.2f, GamePalette.Lighten(Accent, 0.4f),
            0.5f, 3f);
        particles.Sparkle(clockCenter, 14, GamePalette.Lighten(Accent, 0.45f), 160f * scale, 2.6f, 0.6f);
    }

    private void DrawFever(ImDrawListPtr drawList, Rect body, GameGrid grid)
    {
        if (fever <= 0.01f)
        {
            return;
        }

        var pulse = Pulse.Wave(Pulse.Fast);
        var hot = Vector4.Lerp(Accent, HotTint, fever);
        drawList.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(hot with { W = 0.07f * fever * (0.7f + 0.3f * pulse) }));
        ProgressRing.Glow(grid.Center, grid.Width * (0.72f + 0.12f * pulse * fever), hot, fever * (1.3f + 0.7f * pulse));
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

    private void DrawStorm(ImDrawListPtr drawList, GameGrid grid, float scale)
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

    private void DrawFlyIn(ImDrawListPtr drawList, float scale)
    {
        if (flyProgress >= 1f)
        {
            return;
        }

        var eased = Easing.EaseInOutCubic(flyProgress);
        var control = new Vector2(flyFrom.X + 20f * scale, clockCenter.Y + (flyFrom.Y - clockCenter.Y) * 0.35f);
        var inverse = 1f - eased;
        var position = inverse * inverse * flyFrom + 2f * inverse * eased * control + eased * eased * clockCenter;
        var pop = 1f + 0.25f * MathF.Sin(flyProgress * MathF.PI);
        ProgressRing.Glow(position, 20f * scale * pop, Accent, 1.1f);
        particles.Sparkle(position, 1, GamePalette.Lighten(Accent, 0.5f), 40f * scale, 2f, 0.4f);
        Typography.DrawCentered(drawList, position, bonusLabel, White, TextStyles.Headline.Scale * pop,
            FontWeight.Bold);
    }

    private void HandleInput(GameGrid grid)
    {
        var mouse = ImGui.GetMousePos();
        if (!UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max) || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            UpdateHoverCursor(grid);
            return;
        }

        var local = mouse - grid.Origin;
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

    private static void UpdateHoverCursor(GameGrid grid)
    {
        if (UiInteract.Hover(grid.Bounds.Min, grid.Bounds.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
