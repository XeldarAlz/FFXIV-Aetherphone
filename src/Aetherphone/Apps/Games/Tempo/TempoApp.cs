using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Tempo;

internal sealed class TempoApp : IMiniGame
{
    private const string GameId = "tempo";
    private const string SurfaceId = "tempo.press";
    private const int PracticeMode = 1;
    private const float ViewTiles = 13f;
    private const float CubeScreenFraction = 0.3f;
    private const float CameraBaseHeight = 5f;
    private const float CameraFollow = 0.25f;
    private const float CameraSmoothSeconds = 0.35f;
    private const float ProgressTop = 88f;
    private const float ProgressHeight = 4f;
    private const float PracticeBand = 64f;
    private const float PracticeButtonRadius = 20f;
    private const float PracticeLabelWidth = 96f;
    private const float FinishDelaySeconds = 1.1f;
    private const float BannerSeconds = 1.4f;
    private const float RibbonWidth = 0.34f;
    private const float AttemptTextReach = 24f;
    private const float CoinCapsuleWidth = 62f;
    private const float CapsulePadding = 24f;
    private const float PopDecay = 3f;
    private const float BeatSharpness = 5f;
    private const int BeatsPerBar = 4;
    private const ulong IdleSeed = 0x54454D50UL;
    private static readonly LocString[] Modes = { L.Tempo.Normal, L.Tempo.Practice };
    private static readonly bool[] LevelModes = { true, false };
    private static readonly GameSpec StageSpec = new(GameId, L.Tempo.Title, GameGenre.Arcade, L.Tempo.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Level, Modes, clocked: true, countdown: true, keyboard: true,
        levelCount: TempoLevels.Count, levelModes: LevelModes);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.98f, 0.32f, 0.36f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.82f, 0.3f, 1f);
    private static readonly Vector4 Pad = new(1f, 0.86f, 0.3f, 1f);
    private static readonly Vector4 Mint = new(0.44f, 0.96f, 0.56f, 1f);
    private static readonly Vector4 Muted = new(1f, 1f, 1f, 0.7f);
    private static readonly Vector4[] ConfettiColors =
    {
        new(1f, 0.82f, 0.3f, 1f), new(0.36f, 0.86f, 1f, 1f), new(1f, 0.42f, 0.62f, 1f), new(0.44f, 0.96f, 0.56f, 1f),
    };
    private static readonly ParticleSpec Dust = new(White with { W = 0.7f }, White with { W = 0f }, 0.08f, 2.4f, 0.3f,
        6f, 2.2f, spread: 1.4f, direction: -MathF.PI * 0.5f);
    private static readonly ParticleSpec PadBurst = new(Pad, Pad with { W = 0f }, 0.1f, 6f, 0.5f, 6f, 2f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec CoinSparkle = new(Gold, White, 0.1f, 4f, 0.6f, 2f, 2.4f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec CoinRing = new(Gold, Gold with { W = 0f }, 0.5f, 0f, 0.4f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec WhiteShards = new(White, White with { W = 0f }, 0.14f, 8f, 0.8f, 14f, 1.2f, 10f,
        shape: ParticleShape.Shard);
    private static readonly ParticleSpec Flare = new(White, White with { W = 0f }, 0.5f, 0.5f, 0.35f,
        shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow, additive: true);

    private readonly TempoBoard board = new();
    private readonly TempoPlan plan = new(256);
    private readonly ParticleSystem particles = new(448);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private TempoAutopilot? autopilot;
    private TempoLevel? plannedLevel;
    private ParticleSpec shards;
    private ParticleSpec portalSparks;
    private Camera2D camera = Camera2D.Create();
    private Spring cameraHeight;
    private LabelSlot attemptLabel;
    private LabelSlot percentLabel;
    private LabelSlot widestPercent;
    private LabelSlot levelLabel;
    private LabelSlot practiceLabel;
    private LabelPairSlot coinsLabel;
    private Rect practiceBand;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private float bannerProgress = 1f;
    private float finishTimer;
    private float coinPop;
    private float time;
    private int levelNumber = 1;
    private int practiceLevel;
    private bool planReady;
    private bool pointerHeld;
    private bool finishing;
    private bool finished;
    private bool idleActive;
    private bool cameraPlaced;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        var practice = start.Mode == PracticeMode;
        levelNumber = practice ? Math.Max(1, practiceLevel) : Math.Clamp(start.Level, 1, TempoLevels.Count);
        if (!practice)
        {
            practiceLevel = levelNumber;
        }

        board.Load(TempoLevels.Get(levelNumber), start.Random, practice);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ribbon.Clear();
        camera = Camera2D.Create();
        cameraPlaced = false;
        BuildSpecs();
        ShowBanner(levelLabel.Get(L.Stage.LevelNumber, levelNumber), Accent);
        finishTimer = 0f;
        coinPop = 0f;
        pointerHeld = false;
        finishing = false;
        finished = false;
        idleActive = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        idleActive = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (practiceLevel == 0)
        {
            practiceLevel = Math.Max(1, context.Session.Stats.NextLevel(GameId, TempoLevels.Count));
        }

        var number = context.Session.Mode == PracticeMode ? practiceLevel : Math.Max(1, context.Session.Level);
        var level = TempoLevels.Get(number);
        EnsurePlan(level);
        if (!idleActive || !ReferenceEquals(board.Level, level) || board.Complete || board.Dead)
        {
            board.Load(level, GameRandom.FromSeed(IdleSeed), false);
            ribbon.Clear();
            cameraPlaced = false;
            idleActive = true;
            levelNumber = number;
            BuildSpecs();
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        particles.Update(raw);
        if (planReady)
        {
            board.StepPlan(raw, plan);
        }
        else
        {
            board.Step(raw, false, false);
        }

        PlaceCamera(context, raw);
        EmitTrail(raw);
        DrawWorld(ImGui.GetWindowDrawList(), context, UiScale.Current, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        time += raw;
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        coinPop = MathF.Max(0f, coinPop - raw * PopDecay);
        practiceBand = PracticeRect(context);
        if (!finished)
        {
            var held = ReadInput(context, out var pressed);
            board.Step(simDelta, held, pressed);
            React(context, scale);
            if (board.Complete)
            {
                if (!finishing)
                {
                    StartFinishing(context);
                }

                finishTimer += raw;
                if (finishTimer >= FinishDelaySeconds)
                {
                    FinishRun(context);
                }
            }
        }

        PlaceCamera(context, context.DeltaSeconds);
        EmitTrail(simDelta);
        DrawWorld(drawList, context, scale, true);
        DrawProgress(drawList, context, scale);
        DrawPracticeBar(drawList, context, scale);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Safe.Min.Y + context.Safe.Height * 0.22f),
            bannerText, bannerColor, context.Theme, bannerProgress);
        var percent = percentLabel.Get(L.Tempo.Percent, (int)MathF.Floor(board.Progress * 100f));
        context.Hud.Score(board.Attempts, L.Tempo.Attempt);
        if (!board.Practice)
        {
            context.Hud.Level(levelNumber);
        }

        context.Hud.Custom(CapsuleWidth(widestPercent.Get(L.Tempo.Percent, 100), scale));
        context.Hud.Custom(CoinCapsuleWidth);
        DrawPercentCapsule(drawList, context, percent, scale);
        DrawCoinCapsule(drawList, context, scale);
        context.Session.Report(board.Attempts);
    }

    private void BuildSpecs()
    {
        var accent = Accent;
        shards = new ParticleSpec(GamePalette.Lighten(accent, 0.2f), accent with { W = 0f }, 0.18f, 8f, 0.9f, 14f, 1.2f, 10f,
            shape: ParticleShape.Shard);
        portalSparks = new ParticleSpec(GamePalette.Lighten(accent, 0.4f), accent with { W = 0f }, 0.08f, 5f, 0.5f, 0f, 2f,
            shape: ParticleShape.Spark, additive: true);
    }

    private void EnsurePlan(TempoLevel level)
    {
        if (ReferenceEquals(plannedLevel, level))
        {
            return;
        }

        autopilot ??= new TempoAutopilot();
        plannedLevel = level;
        planReady = autopilot.Plan(level, plan);
    }

    private Rect PracticeRect(in GameContext context)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        return new Rect(new Vector2(full.Min.X + StageLayout.SafeSide * scale, full.Max.Y - PracticeBand * scale),
            new Vector2(full.Max.X - StageLayout.SafeSide * scale, full.Max.Y - StageLayout.SafeBottom * scale * 0.5f));
    }

    private Rect WorldView(in GameContext context)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var bottom = board.Practice ? practiceBand.Min.Y : full.Max.Y - StageLayout.SafeBottom * scale;
        return new Rect(new Vector2(full.Min.X, context.Safe.Min.Y), new Vector2(full.Max.X, MathF.Max(context.Safe.Min.Y + 1f, bottom)));
    }

    private void PlaceCamera(in GameContext context, float deltaSeconds)
    {
        var view = WorldView(context);
        camera.Fit(view, ViewTiles, ViewTiles, FitMode.CoverWidth);
        var height = CameraBaseHeight + (board.RenderY - CameraBaseHeight) * CameraFollow;
        if (!cameraPlaced)
        {
            cameraHeight.SnapTo(height);
            cameraPlaced = true;
        }
        else if (deltaSeconds > 0f)
        {
            cameraHeight.Step(height, CameraSmoothSeconds, deltaSeconds);
        }

        var lead = (0.5f - CubeScreenFraction) * ViewTiles;
        camera.Place(new Vector2(board.RenderX + lead, -cameraHeight.Value));
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private bool ReadInput(in GameContext context, out bool pressed)
    {
        pressed = false;
        var scale = UiScale.Current;
        var full = context.Full;
        var pressArea = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        if (context.Session.State == StageFlow.Countdown)
        {
            PressSurface.Claim(SurfaceId, pressArea, out _);
            pointerHeld = false;
            return false;
        }

        if (context.Session.State != StageFlow.Playing || board.Complete)
        {
            pointerHeld = false;
            return false;
        }

        PressSurface.Claim(SurfaceId, pressArea, out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated && !context.ChromeHit(mouse))
        {
            if (board.Practice && practiceBand.Contains(mouse))
            {
                PracticeTap(context, mouse);
            }
            else
            {
                pressed = true;
                pointerHeld = true;
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pointerHeld = false;
        }

        if (GameInput.Pressed(ImGuiKey.Space) || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            pressed = true;
        }

        if (board.Practice && GameInput.Pressed(ImGuiKey.Z))
        {
            PlaceCheckpoint();
        }

        if (board.Practice && GameInput.Pressed(ImGuiKey.X))
        {
            RemoveCheckpoint();
        }

        return pointerHeld || GameInput.Held(ImGuiKey.Space) || GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow);
    }

    private Vector2 PracticeButton(int index)
    {
        var scale = UiScale.Current;
        var radius = PracticeButtonRadius * scale;
        var y = practiceBand.Center.Y;
        return index switch
        {
            0 => new Vector2(practiceBand.Min.X + radius + 4f * scale, y),
            1 => new Vector2(practiceBand.Min.X + radius * 3f + PracticeLabelWidth * scale + 12f * scale, y),
            2 => new Vector2(practiceBand.Max.X - radius * 3f - 12f * scale, y),
            _ => new Vector2(practiceBand.Max.X - radius - 4f * scale, y),
        };
    }

    private void PracticeTap(in GameContext context, Vector2 mouse)
    {
        var radius = PracticeButtonRadius * UiScale.Current * 1.2f;
        for (var button = 0; button < 4; button++)
        {
            if (Vector2.DistanceSquared(mouse, PracticeButton(button)) > radius * radius)
            {
                continue;
            }

            switch (button)
            {
                case 0:
                    ChangePracticeLevel(context, -1);
                    break;
                case 1:
                    ChangePracticeLevel(context, 1);
                    break;
                case 2:
                    PlaceCheckpoint();
                    break;
                default:
                    RemoveCheckpoint();
                    break;
            }

            return;
        }
    }

    private void ChangePracticeLevel(in GameContext context, int step)
    {
        var next = levelNumber + step;
        if (next < 1 || next > TempoLevels.Count || !context.Session.Stats.IsUnlocked(GameId, next))
        {
            UiFeedback.Play(UiSound.GameWrong);
            return;
        }

        levelNumber = next;
        practiceLevel = next;
        board.Load(TempoLevels.Get(next), GameRandom.FromSeed(context.Session.Seed), true);
        ribbon.Clear();
        cameraPlaced = false;
        UiFeedback.Play(UiSound.GameCardFlip);
        ShowBanner(levelLabel.Get(L.Stage.LevelNumber, next), Accent);
    }

    private void PlaceCheckpoint()
    {
        if (!board.PlaceCheckpoint())
        {
            UiFeedback.Play(UiSound.GameWrong);
            return;
        }

        UiFeedback.Play(UiSound.GamePiece);
        var point = board.Checkpoint(board.CheckpointCount - 1);
        particles.Emit(CoinRing, TempoRenderer.World(point.X, point.Y), 1);
    }

    private void RemoveCheckpoint()
    {
        UiFeedback.Play(board.RemoveCheckpoint() ? UiSound.GamePop : UiSound.GameWrong);
    }

    private void React(in GameContext context, float scale)
    {
        var signals = board.Signals;
        if (signals == TempoSignal.None)
        {
            return;
        }

        ref readonly var runner = ref board.Runner;
        var center = TempoRenderer.World(runner.X, runner.Y);
        var feet = TempoRenderer.World(runner.X, runner.Y - runner.Gravity * TempoPhysics.Half);
        if ((signals & TempoSignal.Jumped) != 0)
        {
            particles.Emit(Dust, feet, 4);
        }

        if ((signals & TempoSignal.Landed) != 0)
        {
            particles.Emit(Dust, feet, 6);
        }

        if ((signals & TempoSignal.Pad) != 0)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Emit(PadBurst, feet, 12);
            fx.Shockwave(camera.ToScreen(feet), camera.Px(1.2f), Pad, 0.35f, 2.4f);
            camera.Punch(0.02f);
        }

        if ((signals & TempoSignal.Portal) != 0)
        {
            UiFeedback.Play(UiSound.GamePop);
            particles.Emit(portalSparks, center, 14);
            context.Fx.Flash(TempoRenderer.PortalColor(runner.Gravity < 0 ? TempoItem.GravityUp : TempoItem.GravityDown), 0.12f);
            camera.Punch(0.03f);
        }

        if ((signals & TempoSignal.Coin) != 0)
        {
            OnCoin(context);
        }

        if ((signals & TempoSignal.Beat) != 0 && board.Beat % BeatsPerBar == 0 && !board.Dead)
        {
            UiFeedback.Play(UiSound.GameTick);
            camera.Punch(0.006f);
        }

        if ((signals & TempoSignal.Died) != 0)
        {
            OnDeath(context);
        }

        if ((signals & TempoSignal.Restarted) == 0)
        {
            return;
        }

        ribbon.Clear();
        cameraPlaced = false;
        context.Fx.Flash(Accent, 0.08f);
    }

    private void OnCoin(in GameContext context)
    {
        var level = board.Level!;
        var column = level.CoinColumn(board.LastCoin);
        var world = TempoRenderer.World(column + 0.5f, level.Coin(column) + 0.5f);
        UiFeedback.Play(UiSound.GameCollect);
        particles.Emit(CoinSparkle, world, 12);
        particles.Emit(CoinRing, world, 1);
        fx.Shockwave(camera.ToScreen(world), camera.Px(1.4f), Gold, 0.4f, 2.6f);
        coinPop = 1f;
        context.Fx.Punch(0.02f);
    }

    private void OnDeath(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameBreak);
        var world = TempoRenderer.World(board.DeathPoint.X, board.DeathPoint.Y);
        particles.Reseed(board.ShatterSeed);
        particles.Emit(shards, world, 18);
        particles.Emit(WhiteShards, world, 8);
        particles.Emit(Flare, world, 1);
        fx.Shockwave(camera.ToScreen(world), camera.Px(2.2f), Accent, 0.45f, 3f);
        camera.Shake(0.55f);
        context.Fx.Flash(White, 0.22f);
        context.Fx.Vignette(Danger, 0.25f, 0.45f);
        ribbon.Clear();
    }

    private void StartFinishing(in GameContext context)
    {
        finishing = true;
        finishTimer = 0f;
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        var level = board.Level!;
        var gate = TempoRenderer.World(level.Length, board.Runner.Y + 2f);
        particles.Confetti(gate, 40, ConfettiColors, 9f, 0.12f, 1.2f, 12f);
        var flawless = !board.Practice && board.Attempts == 1;
        ShowBanner(Loc.T(flawless ? L.Tempo.Flawless : L.Tempo.Complete), flawless ? Gold : Mint);
    }

    private void FinishRun(in GameContext context)
    {
        finished = true;
        if (board.Practice)
        {
            context.Session.Finish(GameOutcome.Unranked()
                .WithStat(L.Tempo.Attempts, GameNumber.Label(board.Attempts))
                .WithStat(L.Tempo.Checkpoints, GameNumber.Label(board.CheckpointCount)));
            return;
        }

        ref readonly var runner = ref board.Runner;
        var coins = runner.CoinsCollected;
        var stars = Stars(board.Attempts, coins);
        context.Session.Finish(new GameOutcome(stars, ScoreKind.Level, GameId)
            .WithStars(stars)
            .WithStat(L.Tempo.Attempts, GameNumber.Label(board.Attempts))
            .WithStat(L.Tempo.Coins, coinsLabel.Get(L.Tempo.CoinsOf, coins, TempoLevel.CoinCount))
            .WithStat(L.Tempo.Jumps, GameNumber.Label(runner.Jumps)));
    }

    public static int Stars(int attempts, int coins) =>
        1 + (attempts == 1 ? 1 : 0) + (coins >= TempoLevel.CoinCount ? 1 : 0);

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void EmitTrail(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || board.Dead || board.Complete)
        {
            return;
        }

        ribbon.Push(TempoRenderer.World(board.RenderX, board.RenderY));
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, float scale, bool live)
    {
        var level = board.Level;
        if (level is null)
        {
            return;
        }

        var accent = Accent;
        var pulse = MathF.Exp(-board.BeatPhase * BeatSharpness);
        TempoRenderer.DrawBeatWash(drawList, context.Full, accent, pulse);
        drawList.PushClipRect(context.Full.Min, context.Full.Max, true);
        TempoRenderer.DrawLevel(drawList, in camera, level, accent, pulse, time);
        TempoRenderer.DrawCoins(drawList, in camera, level, board.Runner.Coins, time);
        if (board.Practice)
        {
            TempoRenderer.DrawCheckpoints(drawList, in camera, board, time);
        }

        DrawAttemptText(drawList, level);
        ribbon.Draw(drawList, in camera, accent with { W = 0.6f }, camera.Px(RibbonWidth), additive: true);
        if (!board.Dead)
        {
            var center = camera.ToScreen(TempoRenderer.World(board.RenderX, board.RenderY));
            TempoRenderer.DrawCube(drawList, center, camera.Px(TempoPhysics.Half * 2f), board.RenderRotation, accent, 0f);
        }

        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
        if (!live)
        {
            return;
        }

        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawAttemptText(ImDrawListPtr drawList, TempoLevel level)
    {
        if (board.Runner.X > TempoPhysics.StartX + AttemptTextReach)
        {
            return;
        }

        var height = level.Ground(0) + 4.5f;
        var anchor = camera.ToScreen(TempoRenderer.World(TempoPhysics.StartX + 6f, height));
        var text = attemptLabel.Get(L.Tempo.AttemptNumber, board.Attempts);
        Typography.DrawCentered(drawList, anchor, text, White with { W = 0.85f }, TextStyles.Title2);
    }

    private void DrawProgress(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var safe = context.Safe;
        var top = context.Full.Min.Y + ProgressTop * scale;
        var bar = new Rect(new Vector2(safe.Min.X, top), new Vector2(safe.Max.X, top + ProgressHeight * scale));
        TempoRenderer.DrawProgress(drawList, bar, board.Progress, board.BestProgress, Accent, scale);
    }

    private void DrawPracticeBar(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!board.Practice)
        {
            return;
        }

        var interactive = context.Session.State == StageFlow.Playing && !finished;
        var radius = PracticeButtonRadius * scale;
        var hintTop = practiceBand.Min.Y - Typography.LineHeight(TextStyles.Caption1) * 2.4f;
        Typography.DrawWrappedCentered(drawList, Loc.T(L.Tempo.PracticeHint), TextStyles.Caption1, Muted,
            new Vector2(practiceBand.Center.X, hintTop), practiceBand.Width - 24f * scale);
        for (var button = 0; button < 4; button++)
        {
            var center = PracticeButton(button);
            var corner = new Vector2(radius, radius);
            var hovered = interactive && UiInteract.Hover(center - corner, center + corner);
            Material.Frosted(drawList, center - corner, center + corner, radius, scale, hovered ? 1f : 0.85f);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var icon = button switch
            {
                0 => FontAwesomeIcon.ChevronLeft,
                1 => FontAwesomeIcon.ChevronRight,
                2 => FontAwesomeIcon.Flag,
                _ => FontAwesomeIcon.Times,
            };
            ProgressRing.CenterIcon(drawList, center, icon, button == 2 ? Mint : White, radius * 0.8f);
        }

        var labelLeft = PracticeButton(0).X + radius + 6f * scale;
        var labelRight = PracticeButton(1).X - radius - 6f * scale;
        var labelCenter = new Vector2((labelLeft + labelRight) * 0.5f, practiceBand.Center.Y);
        var label = practiceLabel.Get(L.Stage.LevelNumber, levelNumber);
        Typography.DrawCentered(drawList, labelCenter, Typography.FitText(label, labelRight - labelLeft, TextStyles.Headline),
            White, TextStyles.Headline);
        if (board.CheckpointCount <= 0)
        {
            return;
        }

        var badge = PracticeButton(2) + new Vector2(radius * 0.75f, -radius * 0.75f);
        drawList.AddCircleFilled(badge, 8f * scale, ImGui.GetColorU32(Mint), 14);
        Typography.DrawCentered(drawList, badge, GameNumber.Label(board.CheckpointCount), GamePalette.InkOn(Mint),
            TextStyles.Caption2);
    }

    private static float CapsuleWidth(string text, float scale) =>
        Typography.Measure(text, TextStyles.FootnoteEmphasized).X / scale + CapsulePadding;

    private void DrawPercentCapsule(ImDrawListPtr drawList, in GameContext context, string percent, float scale)
    {
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        var rect = context.Hud.CustomRect(0);
        StageHud.Capsule(drawList, rect, scale);
        Typography.DrawCentered(drawList, rect.Center, percent, board.Complete ? Mint : White, TextStyles.FootnoteEmphasized);
    }

    private void DrawCoinCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!context.Hud.CustomPlaced(1))
        {
            return;
        }

        var rect = context.Hud.CustomRect(1);
        StageHud.Capsule(drawList, rect, scale);
        var collected = board.Runner.Coins;
        var step = rect.Width / (TempoLevel.CoinCount + 1);
        var radius = rect.Height * 0.2f;
        for (var index = 0; index < TempoLevel.CoinCount; index++)
        {
            var center = new Vector2(rect.Min.X + step * (index + 1), rect.Center.Y);
            if ((collected & (1 << index)) == 0)
            {
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Gold with { W = 0.45f }), 14, MathF.Max(1f, scale));
                continue;
            }

            var pop = index == board.LastCoin ? 1f + 0.35f * coinPop : 1f;
            ProgressRing.Glow(center, radius * 2f, Gold, 0.3f);
            drawList.AddCircleFilled(center, radius * pop, ImGui.GetColorU32(Gold), 14);
        }
    }
}
