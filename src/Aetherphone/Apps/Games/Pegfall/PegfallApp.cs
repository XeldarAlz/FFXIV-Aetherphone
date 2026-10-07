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

namespace Aetherphone.Apps.Games.Pegfall;

internal sealed class PegfallApp : IMiniGame
{
    private const string GameId = "pegfall";
    private const string AimSurfaceId = "pegfall.aim";
    private const int PathPoints = 72;
    private const int BouncePoints = 12;
    private const float ResultDelaySeconds = 1.6f;
    private const float FeverZoom = 1.45f;
    private const float ZoomRate = 4f;
    private const float FollowSeconds = 0.16f;
    private const float FeverSlowFactor = 0.3f;
    private const float FeverSlowSeconds = 1.4f;
    private const float KeyboardTurnSpeed = 1.4f;
    private const float WheelTurnStep = 0.006f;
    private const float KickDecay = 5f;
    private const float BannerSeconds = 1.3f;
    private const float IdleFireSeconds = 2.4f;
    private const float PointerMoveEpsilon = 0.25f;
    private const ulong IdleSeed = 0x50454746414CUL;
    private static readonly GameSpec StageSpec = new(GameId, L.Pegfall.Title, GameGenre.Arcade, L.Pegfall.Hook,
        Backdrop.Cavern, HudStyle.Standard, ScoreKind.Score, keyboard: true, levelCount: PegfallLevels.Count);
    private static readonly Vector2 FieldCenter = new(PegfallBoard.Width * 0.5f, PegfallBoard.Height * 0.5f);
    private static readonly float[] HitScale = BuildHitScale(-5, -3, -1, 0, 2, 4, 5, 7, 9, 11, 12);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Dust = new(0.75f, 0.72f, 0.80f, 0.6f);
    private static readonly Vector4[] Confetti =
    {
        PegfallRenderer.Orange, PegfallRenderer.Blue, PegfallRenderer.Green, PegfallRenderer.Gold,
        new(0.95f, 0.45f, 0.85f, 1f),
    };
    private static readonly ParticleSpec BlueSpark = HitSpark(PegfallRenderer.BlueLit);
    private static readonly ParticleSpec OrangeSpark = HitSpark(PegfallRenderer.OrangeLit);
    private static readonly ParticleSpec GreenSpark = HitSpark(PegfallRenderer.GreenLit);
    private static readonly ParticleSpec BlueShards = PopShards(PegfallRenderer.Blue);
    private static readonly ParticleSpec OrangeShards = PopShards(PegfallRenderer.Orange);
    private static readonly ParticleSpec GreenShards = PopShards(PegfallRenderer.Green);
    private static readonly ParticleSpec MuzzleFlash = new(White, PegfallRenderer.Gold with { W = 0f }, 0.07f, 4f,
        0.3f, 0f, 3f, shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec ReboundSpark = new(White, White with { W = 0f }, 0.04f, 2.2f, 0.2f, 0f, 3f,
        shape: ParticleShape.Spark);
    private static readonly ParticleSpec DrainPuff = new(Dust, Dust with { W = 0f }, 0.18f, 1.2f, 0.5f, -1f, 2f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec GoldStars = new(PegfallRenderer.Gold, White with { W = 0f }, 0.1f, 4.5f, 0.9f,
        3f, 1.6f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec FeverRing = new(PegfallRenderer.Gold, PegfallRenderer.Gold with { W = 0f },
        0.5f, 0f, 0.6f, shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Streaks = new(White, PegfallRenderer.Gold with { W = 0f }, 0.08f, 9f, 0.6f,
        6f, 1.2f, shape: ParticleShape.Streak);
    private static readonly ParticleSpec MagnetSpark = new(PegfallRenderer.Magnet, PegfallRenderer.Magnet with { W = 0f },
        0.05f, 1.6f, 0.35f, 0f, 2f, shape: ParticleShape.Spark, additive: true);
    private readonly PegfallBoard board = new();
    private readonly ParticleSystem particles = new(720);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] trails = { new(), new(), new() };
    private readonly Vector2[] path = new Vector2[PathPoints];
    private readonly Vector2[] bounce = new Vector2[BouncePoints];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot multiplierLabel;
    private LabelPairSlot orangeLabel;
    private LocString banner = L.Pegfall.Fever;
    private Vector4 bannerTint = PegfallRenderer.Gold;
    private Vector2 aim = Vector2.UnitY;
    private Vector2 lastPointer;
    private float bannerProgress = 1f;
    private float zoom = 1f;
    private float kick;
    private float resultDelay;
    private float idleTimer;
    private float rainbowPhase;
    private int level = 1;
    private int trailedBalls;
    private int shotHits;
    private bool finished;

    public PegfallApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        level = Math.Clamp(start.Level, 1, PegfallLevels.Count);
        board.Load(PegfallLevels.Get(level), start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ClearTrails();
        camera = Camera2D.Create();
        aim = Vector2.UnitY;
        zoom = 1f;
        kick = 0f;
        shotHits = 0;
        resultDelay = ResultDelaySeconds;
        bannerProgress = 1f;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        idleTimer += context.RawDeltaSeconds;
        if (board.Phase == PegfallPhase.Over)
        {
            BuildIdle();
        }

        if (board.CanFire && idleTimer >= IdleFireSeconds)
        {
            idleTimer = 0f;
            var angle = 0.5f + board.Shots * 0.73f % 2.1f;
            aim = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            board.Fire(aim);
        }

        board.Step(context.RawDeltaSeconds);
        board.ClearEvents();
        PushTrails(context.RawDeltaSeconds);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        kick = MathF.Max(0f, kick - context.RawDeltaSeconds * KickDecay);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        rainbowPhase += context.RawDeltaSeconds * 0.9f;
        PlaceCamera(context);
        if (!finished)
        {
            Step(simDelta, context);
        }

        var aiming = !finished && context.Session.State == StageFlow.Playing && board.CanFire;
        DrawWorld(drawList, scale, aiming);
        GameBanner.Draw(drawList, camera.ToScreen(new Vector2(FieldCenter.X, PegfallBoard.Height * 0.38f)),
            Loc.T(banner), bannerTint, context.Theme, bannerProgress);
        DrawHud(drawList, context, scale);
    }

    private void BuildIdle()
    {
        board.Load(PegfallLevels.Get(1), GameRandom.FromSeed(IdleSeed));
        ClearTrails();
        idleTimer = IdleFireSeconds * 0.5f;
        aim = Vector2.UnitY;
    }

    private void PlaceCamera(in GameContext context)
    {
        var feverFocus = board.Fever && board.BallCount > 0;
        var targetZoom = feverFocus ? FeverZoom : 1f;
        zoom += (targetZoom - zoom) * MathF.Min(1f, context.RawDeltaSeconds * ZoomRate);
        camera.Fit(context.Safe, PegfallBoard.Width / zoom, PegfallBoard.Height / zoom, FitMode.Contain);
        var blend = Math.Clamp((zoom - 1f) / (FeverZoom - 1f), 0f, 1f);
        var focus = feverFocus ? board.RenderBallPosition(0) : FieldCenter;
        camera.Follow(Vector2.Lerp(FieldCenter, focus, blend), Vector2.Zero, FollowSeconds, context.RawDeltaSeconds);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        for (var index = 0; index < board.EventCount; index++)
        {
            React(board.Event(index), context);
        }

        board.ClearEvents();
        PushTrails(deltaSeconds);
        EmitBallEffects(deltaSeconds);
        if (board.Phase == PegfallPhase.Flying && board.BallsLeft == 0 && board.OrangeLeft > 0 && !board.Fever)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.08f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        if (board.Phase != PegfallPhase.Over)
        {
            return;
        }

        resultDelay -= context.RawDeltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        var cleared = board.OrangeTotal - board.OrangeLeft;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId, board.Won)
            .WithStars(board.Stars)
            .WithStat(L.Pegfall.Oranges, orangeLabel.Get(L.Pegfall.OrangesCount, cleared, board.OrangeTotal))
            .WithStat(L.Pegfall.BestShot, GameNumber.Label(board.BestShot))
            .WithStat(L.Pegfall.BallsLeft, GameNumber.Label(board.BallsLeft))
            .WithStat(L.Pegfall.PegsHit, GameNumber.Label(board.PegsHit)));
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            return;
        }

        var full = context.Full;
        var pointer = ImGui.GetMousePos();
        var moved = Vector2.DistanceSquared(pointer, lastPointer) > PointerMoveEpsilon;
        lastPointer = pointer;
        if (moved && UiInteract.Hover(full.Min, full.Max) && !context.ChromeHit(pointer))
        {
            aim = PegfallBoard.ClampAim(camera.ToWorld(pointer) - PegfallBoard.Launcher);
        }

        var turn = (GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow) ? 1f : 0f) -
                   (GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow) ? 1f : 0f);
        if (turn != 0f)
        {
            var angle = MathF.Atan2(aim.Y, aim.X) - turn * KeyboardTurnSpeed * context.RawDeltaSeconds;
            aim = PegfallBoard.ClampAim(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
        }

        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel != 0f && UiInteract.Hover(full.Min, full.Max))
        {
            var angle = MathF.Atan2(aim.Y, aim.X) - wheel * WheelTurnStep;
            aim = PegfallBoard.ClampAim(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
        }

        PressSurface.Claim(AimSurfaceId, full, out var activated);
        var fire = activated && !context.ChromeHit(pointer);
        if (GameInput.Pressed(ImGuiKey.Space) || GameInput.Pressed(ImGuiKey.Enter))
        {
            fire = true;
        }

        if (fire)
        {
            board.Fire(aim);
        }
    }

    private void React(in PegfallEvent entry, in GameContext context)
    {
        switch (entry.Kind)
        {
            case PegfallEventKind.Fire:
                OnFire(entry);
                return;
            case PegfallEventKind.PegHit:
                OnPegHit(entry, context);
                return;
            case PegfallEventKind.PegRebound:
                particles.Emit(ReboundSpark, entry.Position, 3);
                return;
            case PegfallEventKind.PegCleared:
                OnPegCleared(entry);
                return;
            case PegfallEventKind.Multiball:
                UiFeedback.Play(UiSound.GamePowerUp);
                ShowBanner(L.Pegfall.Multiball, PegfallRenderer.GreenLit);
                particles.Emit(GreenSpark, entry.Position, 18);
                context.Fx.Flash(PegfallRenderer.Green, 0.16f);
                camera.Shake(0.18f);
                return;
            case PegfallEventKind.Magnet:
                UiFeedback.Play(UiSound.GamePowerUp);
                ShowBanner(L.Pegfall.Magnet, PegfallRenderer.Magnet);
                fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(2.2f), PegfallRenderer.Magnet, 0.5f, 3f);
                context.Fx.Flash(PegfallRenderer.Magnet, 0.14f);
                return;
            case PegfallEventKind.FreeBall:
                OnFreeBall(entry, context);
                return;
            case PegfallEventKind.Drain:
                particles.Emit(DrainPuff, entry.Position, 6);
                return;
            case PegfallEventKind.LastOrange:
                UiFeedback.Play(UiSound.GameTick);
                ShowBanner(L.Pegfall.LastOrange, PegfallRenderer.OrangeLit);
                fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(1.4f), PegfallRenderer.Orange, 0.6f, 3f);
                return;
            case PegfallEventKind.Fever:
                OnFever(entry, context);
                return;
            case PegfallEventKind.FeverBucket:
                OnFeverBucket(entry, context);
                return;
            case PegfallEventKind.ShotEnd:
                OnShotEnd(entry);
                return;
            case PegfallEventKind.BallBonus:
                UiFeedback.Play(UiSound.GameCollect);
                fx.AddText(GameNumber.Signed(entry.Value), camera.ToScreen(PegfallBoard.Launcher + new Vector2(0f, 1.2f)),
                    PegfallRenderer.Gold, 1.4f);
                particles.Emit(GoldStars, PegfallBoard.Launcher, 16);
                return;
            case PegfallEventKind.Won:
                GameSfx.LevelClear();
                context.Fx.Sweep();
                particles.Confetti(new Vector2(FieldCenter.X, 1.5f), 70, Confetti, 9f, 0.12f, 1.6f, 9f);
                return;
            case PegfallEventKind.Lost:
                UiFeedback.Play(UiSound.GameWrong);
                context.Fx.Vignette(Danger, 0.35f, 0.9f);
                camera.Shake(0.3f);
                return;
            case PegfallEventKind.RimBounce:
                UiFeedback.Play(UiSound.GameHitWood);
                particles.Emit(ReboundSpark, entry.Position, 6);
                camera.Shake(0.05f);
                return;
            case PegfallEventKind.Unstuck:
                UiFeedback.Play(UiSound.GameHitSoft);
                particles.Emit(DrainPuff, entry.Position, 5);
                camera.Shake(0.1f);
                return;
            default:
                return;
        }
    }

    private void OnFire(in PegfallEvent entry)
    {
        UiFeedback.Play(UiSound.GameShoot);
        shotHits = 0;
        kick = 1f;
        particles.Emit(MuzzleFlash.WithDirection(MathF.Atan2(aim.Y, aim.X), 0.9f), entry.Position, 10);
        camera.Shake(0.06f);
        if (board.BallsLeft == 0)
        {
            ShowBanner(L.Pegfall.LastBall, Danger);
        }
    }

    private void OnPegHit(in PegfallEvent entry, in GameContext context)
    {
        var kind = board.KindOf(entry.Peg);
        var color = PegfallRenderer.LitColor(kind);
        var screen = camera.ToScreen(entry.Position);
        particles.Emit(SparkFor(kind), entry.Position, kind == PegKind.Orange ? 10 : 6);
        fx.Shockwave(screen, camera.Px(0.65f), color, 0.3f, 2f);
        var textScale = 0.8f + 0.08f * MathF.Min(board.Multiplier, 8);
        var rate = HitScale[Math.Min(shotHits, HitScale.Length - 1)];
        shotHits++;
        fx.AddText(GameNumber.Signed(entry.Value), screen - new Vector2(0f, camera.Px(0.35f)), color, textScale);
        switch (kind)
        {
            case PegKind.Orange:
                UiFeedback.PlayPitched(UiSound.GamePop, rate);
                camera.Shake(0.05f);
                break;
            case PegKind.Green:
                break;
            default:
                UiFeedback.PlayPitched(UiSound.GameHitSoft, rate);
                break;
        }

        if (!entry.TierUp)
        {
            return;
        }

        GameSfx.ComboTierUp();
        fx.AddText(multiplierLabel.Get(L.Stage.Times, board.Multiplier), screen - new Vector2(0f, camera.Px(1.1f)),
            PegfallRenderer.Gold, 1.35f);
        context.Fx.Punch(0.04f);
    }

    private static float[] BuildHitScale(params int[] semitones)
    {
        var rates = new float[semitones.Length];
        for (var index = 0; index < semitones.Length; index++)
        {
            rates[index] = MathF.Pow(2f, semitones[index] / 12f);
        }

        return rates;
    }

    private void OnPegCleared(in PegfallEvent entry)
    {
        var kind = board.KindOf(entry.Peg);
        UiFeedback.Play(UiSound.GamePop);
        particles.Emit(ShardsFor(kind), entry.Position, 5);
        fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(0.5f), PegfallRenderer.LitColor(kind), 0.25f, 1.6f);
    }

    private void OnFreeBall(in PegfallEvent entry, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameCollect);
        ShowBanner(L.Pegfall.FreeBall, PegfallRenderer.Gold);
        particles.Emit(GoldStars, entry.Position, 20);
        fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(1.8f), PegfallRenderer.Gold, 0.5f, 3f);
        context.Fx.Sweep();
        context.Fx.Punch(0.03f);
    }

    private void OnFever(in PegfallEvent entry, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameClear);
        GameSfx.ComboTierUp();
        ShowBanner(L.Pegfall.Fever, PegfallRenderer.Gold);
        context.Fx.SlowMo(FeverSlowFactor, FeverSlowSeconds);
        context.Fx.Punch(0.08f);
        context.Fx.Flash(PegfallRenderer.Gold, 0.3f);
        context.Fx.Sweep();
        fx.Shockwave(camera.ToScreen(entry.Position), camera.Px(4f), PegfallRenderer.Gold, 0.8f, 4f);
        particles.Emit(FeverRing, entry.Position, 3);
        particles.Emit(GoldStars, entry.Position, 24);
        fx.HitStop(0.08f);
    }

    private void OnFeverBucket(in PegfallEvent entry, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        GameSfx.LevelClear();
        var screen = camera.ToScreen(entry.Position);
        particles.Confetti(entry.Position, 60, Confetti, 11f, 0.12f, 1.4f, 9f);
        particles.Emit(Streaks.WithDirection(-MathF.PI * 0.5f, 1.6f), entry.Position, 24);
        particles.Emit(GoldStars, entry.Position, 30);
        fx.Shockwave(screen, camera.Px(5f), PegfallRenderer.Gold, 0.7f, 4.5f);
        fx.AddText(GameNumber.Signed(entry.Value), screen - new Vector2(0f, camera.Px(1.6f)), PegfallRenderer.Gold, 1.9f);
        camera.Shake(0.55f);
        context.Fx.Flash(White, 0.35f);
        context.Fx.Sweep();
        fx.HitStop(0.1f);
    }

    private void OnShotEnd(in PegfallEvent entry)
    {
        if (entry.Value <= 0)
        {
            return;
        }

        fx.AddText(GameNumber.Signed(entry.Value), camera.ToScreen(new Vector2(FieldCenter.X, PegfallBoard.Height * 0.62f)),
            GamePalette.Lighten(Accent, 0.4f), 1.3f);
    }

    private void ShowBanner(LocString text, Vector4 tint)
    {
        banner = text;
        bannerTint = tint;
        bannerProgress = 0f;
    }

    private void PushTrails(float deltaSeconds)
    {
        if (board.BallCount != trailedBalls)
        {
            ClearTrails();
            trailedBalls = board.BallCount;
        }

        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var ball = 0; ball < board.BallCount; ball++)
        {
            trails[ball].Push(board.RenderBallPosition(ball));
        }
    }

    private void ClearTrails()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Clear();
        }

        trailedBalls = 0;
    }

    private void EmitBallEffects(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var ball = 0; ball < board.BallCount; ball++)
        {
            var position = board.RenderBallPosition(ball);
            if (board.Fever)
            {
                var color = PegfallRenderer.Rainbow(rainbowPhase + ball * 0.33f);
                var spec = new ParticleSpec(color, color with { W = 0f }, 0.1f, 0.6f, 0.5f, 0f, 2f,
                    shape: ParticleShape.GlowCircle, curve: SizeCurve.Shrink);
                particles.Emit(spec, position, 2);
            }

            if (board.MagnetActive)
            {
                particles.Emit(MagnetSpark, position, 1);
            }
        }
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale, bool aiming)
    {
        var accent = Accent;
        PegfallRenderer.DrawField(drawList, in camera, accent, scale);
        PegfallRenderer.DrawRingGuides(drawList, in camera, board, accent, scale);
        PegfallRenderer.DrawBucket(drawList, in camera, board, accent, scale);
        if (aiming)
        {
            var count = board.PreviewPath(aim, path, out var bounced, out var bounceStart, out var bounceVelocity);
            if (bounced)
            {
                board.PreviewBounce(bounceStart, bounceVelocity, bounce);
            }

            PegfallRenderer.DrawGuide(drawList, in camera, path.AsSpan(0, count), bounced, bounce, accent);
        }

        PegfallRenderer.DrawPegs(drawList, in camera, board);
        PegfallRenderer.DrawLauncher(drawList, in camera, aim, kick, board.CanFire, accent);
        DrawBalls(drawList);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawBalls(ImDrawListPtr drawList)
    {
        var radius = camera.Px(PegfallBoard.BallRadius);
        var trailWidth = camera.Px(PegfallRenderer.TrailWidth);
        for (var ball = 0; ball < board.BallCount; ball++)
        {
            var halo = board.Fever
                ? PegfallRenderer.Rainbow(rainbowPhase + ball * 0.33f)
                : board.MagnetActive ? PegfallRenderer.Magnet : PegfallRenderer.Steel;
            trails[ball].Draw(drawList, in camera, halo with { W = 0.5f }, trailWidth, additive: true);
            var strength = board.Fever ? 0.9f : board.MagnetActive ? 0.5f * (0.5f + board.MagnetFraction) : 0.25f;
            PegfallRenderer.DrawBall(drawList, camera.ToScreen(board.RenderBallPosition(ball)), radius, halo, strength);
        }
    }

    private void DrawHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var ballsLabel = GameNumber.Label(board.BallsLeft);
        var orangesLabel = GameNumber.Label(board.OrangeLeft);
        context.Hud.Score(board.Score);
        context.Hud.Level(level);
        if (board.Multiplier > 1)
        {
            context.Hud.Combo(board.Combo);
        }

        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(StatCapsule.Width(ballsLabel, scale));
        context.Hud.Custom(StatCapsule.Width(orangesLabel, scale));
        if (context.Hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, context.Hud.CustomRect(0), FontAwesomeIcon.DotCircle, ballsLabel,
                board.BallsLeft == 0 ? Danger : PegfallRenderer.Steel, scale);
        }

        if (context.Hud.CustomPlaced(1))
        {
            StatCapsule.Draw(drawList, context.Hud.CustomRect(1), FontAwesomeIcon.Bullseye, orangesLabel,
                PegfallRenderer.Orange, scale);
        }

        context.Session.Report(board.Score);
    }

    private static ParticleSpec SparkFor(PegKind kind) => kind switch
    {
        PegKind.Orange => OrangeSpark,
        PegKind.Green => GreenSpark,
        _ => BlueSpark,
    };

    private static ParticleSpec ShardsFor(PegKind kind) => kind switch
    {
        PegKind.Orange => OrangeShards,
        PegKind.Green => GreenShards,
        _ => BlueShards,
    };

    private static ParticleSpec HitSpark(Vector4 color) =>
        new(White, color with { W = 0f }, 0.05f, 3.2f, 0.32f, 0f, 2.4f, shape: ParticleShape.Spark, additive: true);

    private static ParticleSpec PopShards(Vector4 color) =>
        new(GamePalette.Lighten(color, 0.3f), color with { W = 0f }, 0.09f, 2.6f, 0.45f, 9f, 1.4f, 9f,
            shape: ParticleShape.Shard);
}
