using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Spiral;

internal sealed class SpiralApp : IMiniGame
{
    private const string GameId = "spiral";
    private const string DragSurfaceId = "spiral.drag";
    private const ulong IdleSeed = 0x535049524CUL;
    private const float ViewWidth = 8.4f;
    private const float ViewHeight = 14f;
    private const float BallScreenFraction = 0.34f;
    private const float FollowSeconds = 0.12f;
    private const float FallLead = 0.08f;
    private const float KeyTurnSpeed = 3.4f;
    private const float IdleTurnSpeed = 5f;
    private const float IdleRestartSeconds = 1.2f;
    private const float ResultDelaySeconds = 0.8f;
    private const float SquashDecay = 7f;
    private const float BannerSeconds = 1.3f;
    private const float TrailWidth = 0.42f;
    private const float ShardGravity = 26f;
    private const float ShardLife = 0.8f;
    private const float FlameRate = 70f;
    private const int ShardCapacity = 96;
    private const int SplatCapacity = 24;
    private static readonly GameSpec StageSpec = new(GameId, L.Spiral.Title, GameGenre.Arcade, L.Spiral.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly ParticleSpec Flames = new(SpiralRenderer.FireCore, SpiralRenderer.Fire with { W = 0f },
        0.2f, 1.4f, 0.32f, -9f, 2f, 4f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Shrink, additive: true);
    private static readonly ParticleSpec PaintDrops = new(SpiralRenderer.Paint, SpiralRenderer.Paint with { W = 0f },
        0.07f, 3.4f, 0.45f, 22f, 1.2f, spread: 1.6f, direction: -MathF.PI * 0.5f);
    private static readonly ParticleSpec PassSparkle = new(White, White with { W = 0f }, 0.1f, 2.6f, 0.5f, 3f, 2.2f,
        6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec Embers = new(SpiralRenderer.FireCore, SpiralRenderer.Fire with { W = 0f },
        0.14f, 7f, 0.7f, 12f, 1.4f, 9f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec BallShards = new(SpiralRenderer.BallColor, SpiralRenderer.Paint with { W = 0f },
        0.12f, 5.5f, 0.7f, 16f, 1.3f, 10f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec DeathSparks = new(SpiralRenderer.Red, Danger with { W = 0f }, 0.08f, 6.5f,
        0.5f, 6f, 1.5f, shape: ParticleShape.Spark);
    private readonly SpiralBoard board = new();
    private readonly ParticleSystem particles = new(384);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly SpiralShard[] shards = new SpiralShard[ShardCapacity];
    private readonly SpiralSplat[] splats = new SpiralSplat[SplatCapacity];
    private Camera2D camera = Camera2D.Create();
    private GameRandom effects = GameRandom.Fresh();
    private Emitter flames = new(Flames, FlameRate);
    private LabelSlot levelLabel;
    private string bannerText = string.Empty;
    private int shardCursor;
    private int splatCursor;
    private float bannerProgress = 1f;
    private float focusY;
    private float squash;
    private float lastDragX;
    private float resultDelay;
    private float idleRestart;
    private float time;
    private bool cameraPlaced;
    private bool dragActive;
    private bool finished;

    public SpiralApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        effects = GameRandom.FromSeed(start.Seed ^ IdleSeed);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ClearEffects();
        flames.Reset();
        camera = Camera2D.Create();
        cameraPlaced = false;
        focusY = SpiralRenderer.BallPoint(board).Y;
        bannerProgress = 1f;
        squash = 0f;
        resultDelay = ResultDelaySeconds;
        dragActive = false;
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

    public void OnQuit(GameSession session)
    {
        if (finished || board.Score <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(Outcome());
    }

    public void DrawIdle(in GameContext context)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        Animate(deltaSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        SteerIdle(deltaSeconds);
        board.Step(deltaSeconds);
        Trail(deltaSeconds);
        React(context, false);
        if (board.State == SpiralState.Over)
        {
            idleRestart += deltaSeconds;
            if (idleRestart >= IdleRestartSeconds)
            {
                BuildIdle();
            }
        }

        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Animate(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        if (bannerText.Length > 0)
        {
            GameBanner.Draw(drawList, camera.ToScreen(SpiralRenderer.BallPoint(board)) - new Vector2(0f, 90f * scale),
                bannerText, Accent, context.Theme, bannerProgress);
        }

        context.Hud.Score(board.Score);
        context.Hud.Level(board.Level);
        context.Hud.Best(context.Session.Best);
        context.Hud.Custom(SpiralRenderer.PipsWidth);
        if (context.Hud.CustomPlaced(0))
        {
            SpiralRenderer.DrawPips(drawList, context.Hud.CustomRect(0), board.Streak, board.Fireball, Accent, scale);
        }

        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        ClearEffects();
        cameraPlaced = false;
        focusY = SpiralRenderer.BallPoint(board).Y;
        squash = 0f;
        idleRestart = 0f;
        dragActive = false;
    }

    private void Animate(float deltaSeconds)
    {
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        if (board.State == SpiralState.Playing)
        {
            squash = MathF.Max(0f, squash - deltaSeconds * SquashDecay);
        }

        for (var index = 0; index < ShardCapacity; index++)
        {
            ref var shard = ref shards[index];
            if (shard.Life <= 0f)
            {
                continue;
            }

            shard.Life -= deltaSeconds;
            shard.VerticalVelocity += ShardGravity * deltaSeconds;
            shard.Radial += shard.RadialVelocity * deltaSeconds;
            shard.Y += shard.VerticalVelocity * deltaSeconds;
            shard.Angle += shard.Spin * deltaSeconds;
        }

        for (var index = 0; index < SplatCapacity; index++)
        {
            splats[index].Age += deltaSeconds;
        }
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Full, ViewWidth, ViewHeight, FitMode.Contain);
        focusY = MathF.Max(focusY, SpiralRenderer.BallPoint(board).Y);
        var visibleHeight = camera.View.Height / MathF.Max(0.0001f, camera.Zoom);
        var lead = MathF.Max(0f, board.BallVelocity) * FallLead;
        var target = new Vector2(0f, focusY + lead + visibleHeight * (0.5f - BallScreenFraction));
        if (!cameraPlaced)
        {
            camera.Place(target);
            cameraPlaced = true;
        }
        else
        {
            camera.Follow(target, Vector2.Zero, FollowSeconds, context.RawDeltaSeconds);
        }

        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        Trail(deltaSeconds);
        React(context, true);
        if (board.State != SpiralState.Over)
        {
            return;
        }

        resultDelay -= context.DeltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(Outcome());
    }

    private GameOutcome Outcome() =>
        new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Spiral.Rings, GameNumber.Label(board.RingsPassed))
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Spiral.BestDrop, GameNumber.Label(board.BestStreak))
            .WithStat(L.Spiral.Smashes, GameNumber.Label(board.Smashes));

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State is not (StageFlow.Playing or StageFlow.Countdown))
        {
            dragActive = false;
            return;
        }

        var scale = UiScale.Current;
        var full = context.Full;
        var surface = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        PressSurface.Claim(DragSurfaceId, surface, out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated && !context.ChromeHit(mouse))
        {
            dragActive = true;
            lastDragX = mouse.X;
        }

        if (dragActive && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragActive = false;
        }

        if (dragActive)
        {
            var travel = mouse.X - lastDragX;
            lastDragX = mouse.X;
            board.Rotate(-travel / MathF.Max(1f, camera.Px(SpiralBoard.BallOrbit)));
        }

        var turn = 0f;
        if (GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            turn += 1f;
        }

        if (GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            turn -= 1f;
        }

        board.Rotate(turn * KeyTurnSpeed * context.RawDeltaSeconds);
    }

    private void SteerIdle(float deltaSeconds)
    {
        var ring = board.CurrentRing;
        var bestDelta = 0f;
        var bestDistance = float.MaxValue;
        for (var segment = 0; segment < SpiralBoard.Segments; segment++)
        {
            if (board.SegmentAt(ring, segment) != SpiralSegment.Gap)
            {
                continue;
            }

            var center = (segment + 0.5f) * SpiralBoard.SegmentArc + board.Angle;
            var delta = Shortest(SpiralBoard.BallAngle - center);
            if (MathF.Abs(delta) >= bestDistance)
            {
                continue;
            }

            bestDistance = MathF.Abs(delta);
            bestDelta = delta;
        }

        var limit = IdleTurnSpeed * deltaSeconds;
        board.Rotate(Math.Clamp(bestDelta, -limit, limit));
    }

    private static float Shortest(float angle)
    {
        var wrapped = SpiralBoard.Wrap(angle);
        return wrapped > MathF.PI ? wrapped - MathF.Tau : wrapped;
    }

    private void Trail(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var point = SpiralRenderer.BallPoint(board);
        ribbon.Push(point);
        if (board.Fireball && board.State == SpiralState.Playing)
        {
            flames.Advance(deltaSeconds, point, particles);
        }
    }

    private void React(in GameContext context, bool live)
    {
        for (var index = 0; index < board.PassCount; index++)
        {
            OnPass(board.Pass(index), live);
        }

        switch (board.Landing)
        {
            case SpiralLanding.Bounced:
                OnBounce(live);
                break;
            case SpiralLanding.Smashed:
                OnSmash(context, live);
                break;
            case SpiralLanding.Died:
                OnDeath(context, live);
                break;
            default:
                break;
        }

        if (board.FireballStarted)
        {
            OnFireball(context, live);
        }

        if (board.LevelUp)
        {
            OnLevelUp(context, live);
        }
    }

    private void OnPass(in SpiralPass pass, bool live)
    {
        BreakRing(pass.Ring, 1f);
        var front = SpiralRenderer.Project(SpiralBoard.BallAngle, SpiralBoard.OuterRadius, SpiralBoard.RingY(pass.Ring));
        particles.Emit(PassSparkle, front, 6 + pass.Streak * 2);
        if (!live)
        {
            return;
        }

        var scale = UiScale.Current;
        var ball = camera.ToScreen(SpiralRenderer.BallPoint(board));
        UiFeedback.Play(UiSound.GamePop);
        fx.AddText(GameNumber.Signed(pass.Points), ball + new Vector2(38f * scale, -10f * scale),
            GamePalette.Lighten(SpiralRenderer.RingColor(pass.Ring), 0.3f), 0.9f + 0.1f * MathF.Min(pass.Streak, 4));
        camera.Punch(MathF.Min(0.06f, 0.014f * pass.Streak));
        if (pass.Streak < 2)
        {
            return;
        }

        fx.Shockwave(camera.ToScreen(front), camera.Px(1.2f), GamePalette.Lighten(Accent, 0.35f), 0.35f, 2.4f);
    }

    private void OnBounce(bool live)
    {
        squash = 1f;
        AddSplat(board.LandingRing, SpiralBoard.BallAngle - board.Angle + effects.Range(-0.06f, 0.06f),
            SpiralRenderer.Paint);
        var contact = SpiralRenderer.Project(SpiralBoard.BallAngle, SpiralBoard.BallOrbit, SpiralBoard.RingY(board.LandingRing));
        particles.Emit(PaintDrops, contact, 6);
        if (live)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
        }
    }

    private void OnSmash(in GameContext context, bool live)
    {
        BreakRing(board.LandingRing, 1.8f);
        var ball = SpiralRenderer.BallPoint(board);
        particles.Emit(Embers, ball, 22);
        particles.Emit(PassSparkle, ball, 12);
        if (!live)
        {
            return;
        }

        var scale = UiScale.Current;
        var screen = camera.ToScreen(ball);
        UiFeedback.Play(UiSound.GameExplosion);
        fx.Shockwave(screen, camera.Px(SpiralBoard.OuterRadius * 1.3f), SpiralRenderer.Fire, 0.5f, 4f);
        fx.AddText(Loc.T(L.Spiral.Smash), screen - new Vector2(0f, 34f * scale), SpiralRenderer.FireCore, 1.35f);
        fx.AddText(GameNumber.Signed(SpiralBoard.SmashPoints * board.Level), screen + new Vector2(42f * scale, -6f * scale),
            SpiralRenderer.FireCore, 1.05f);
        fx.HitStop(0.05f);
        camera.Shake(0.45f);
        context.Fx.Punch(0.07f);
        context.Fx.Flash(SpiralRenderer.Fire, 0.3f);
    }

    private void OnDeath(in GameContext context, bool live)
    {
        squash = 1f;
        var ball = SpiralRenderer.BallPoint(board);
        particles.Emit(BallShards, ball, 18);
        particles.Emit(DeathSparks, ball, 14);
        AddSplat(board.LandingRing, SpiralBoard.BallAngle - board.Angle, SpiralRenderer.BallColor);
        if (!live)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameBreak);
        fx.Shockwave(camera.ToScreen(ball), camera.Px(2.2f), Danger, 0.55f, 3.4f);
        camera.Shake(0.6f);
        context.Fx.Flash(Danger, 0.45f);
        context.Fx.SlowMo(0.35f, 0.6f);
        context.Fx.Vignette(Danger, 0.4f, 0.8f);
    }

    private void OnFireball(in GameContext context, bool live)
    {
        particles.Emit(Embers, SpiralRenderer.BallPoint(board), 12);
        if (!live)
        {
            return;
        }

        var scale = UiScale.Current;
        UiFeedback.Play(UiSound.GamePowerUp);
        fx.AddText(Loc.T(L.Spiral.Fireball), camera.ToScreen(SpiralRenderer.BallPoint(board)) - new Vector2(0f, 40f * scale),
            SpiralRenderer.FireCore, 1.3f);
        context.Fx.Flash(SpiralRenderer.Fire, 0.22f);
        context.Fx.Punch(0.05f);
        context.Fx.Sweep();
    }

    private void OnLevelUp(in GameContext context, bool live)
    {
        if (!live)
        {
            return;
        }

        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(SpiralRenderer.RingColor(board.CurrentRing), 0.3f), 0.14f);
        bannerText = levelLabel.Get(L.Spiral.LevelBanner, board.Level);
        bannerProgress = 0f;
    }

    private void BreakRing(int ring, float power)
    {
        var y = SpiralBoard.RingY(ring);
        var color = SpiralRenderer.RingColor(ring);
        for (var segment = 0; segment < SpiralBoard.Segments; segment++)
        {
            var kind = board.SegmentAt(ring, segment);
            if (kind == SpiralSegment.Gap)
            {
                continue;
            }

            ref var shard = ref shards[shardCursor];
            shardCursor = (shardCursor + 1) % ShardCapacity;
            shard.Angle = segment * SpiralBoard.SegmentArc + board.Angle;
            shard.Arc = SpiralBoard.SegmentArc * 0.92f;
            shard.Radial = 0f;
            shard.Y = y;
            shard.RadialVelocity = effects.Range(2.4f, 4.2f) * power;
            shard.VerticalVelocity = effects.Range(-3f, 0.5f) * power;
            shard.Spin = effects.Range(-1.2f, 1.2f);
            shard.MaxLife = ShardLife * effects.Range(0.85f, 1.15f);
            shard.Life = shard.MaxLife;
            shard.Color = kind == SpiralSegment.Red ? SpiralRenderer.Red : color;
        }
    }

    private void AddSplat(int ring, float localAngle, Vector4 color)
    {
        ref var splat = ref splats[splatCursor];
        splatCursor = (splatCursor + 1) % SplatCapacity;
        splat.Ring = ring;
        splat.LocalAngle = SpiralBoard.Wrap(localAngle);
        splat.Size = effects.Range(0.3f, 0.42f);
        splat.Age = 0f;
        splat.Color = color;
    }

    private void ClearEffects()
    {
        for (var index = 0; index < ShardCapacity; index++)
        {
            shards[index].Life = 0f;
        }

        for (var index = 0; index < SplatCapacity; index++)
        {
            splats[index].Size = 0f;
        }

        shardCursor = 0;
        splatCursor = 0;
        ribbon.Clear();
        bannerText = string.Empty;
        bannerProgress = 1f;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        SpiralRenderer.DrawTower(drawList, in camera, board, splats, scale);
        SpiralRenderer.DrawShards(drawList, in camera, shards);
        SpiralRenderer.DrawShadow(drawList, in camera, board);
        particles.Draw(drawList, in camera);
        DrawTrail(drawList);
        SpiralRenderer.DrawBall(drawList, in camera, board, squash, time);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawTrail(ImDrawListPtr drawList)
    {
        var speed = Math.Clamp((board.BallVelocity - 8f) / 12f, 0f, 1f);
        var strength = board.Fireball ? 1f : speed;
        if (strength <= 0.01f || board.State != SpiralState.Playing)
        {
            return;
        }

        var color = board.Fireball ? SpiralRenderer.Fire : SpiralRenderer.BallColor;
        ribbon.Draw(drawList, in camera, color with { W = 0.55f * strength }, camera.Px(TrailWidth), additive: true);
    }
}
