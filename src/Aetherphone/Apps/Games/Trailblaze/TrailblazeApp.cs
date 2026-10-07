using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trailblaze;

internal sealed class TrailblazeApp : IMiniGame
{
    private const string GameId = "trailblaze";
    private const string SwipeSurfaceId = "trailblaze.swipe";
    private const float SwipeThreshold = 24f;
    private const float BannerSeconds = 1.5f;
    private const float CameraFollow = 0.55f;
    private const float CameraSmoothSeconds = 0.14f;
    private const float LeanPerLane = 0.32f;
    private const float RollPerLane = 0.06f;
    private const float LeanSmoothSeconds = 0.07f;
    private const float BlendSmoothSeconds = 0.06f;
    private const float WingsSmoothSeconds = 0.18f;
    private const float BobStiffness = 260f;
    private const float BobDamping = 16f;
    private const float BobPerImpact = 10f;
    private const float StrideRate = 0.9f;
    private const float FlapRate = 9f;
    private const float CurveAmount = 0.42f;
    private const float SceneryRate = 0.9f;
    private const float DustInterval = 0.055f;
    private const float RibbonWidth = 0.42f;
    private const float RibbonHeight = 1.15f;
    private const float TrailNearFraction = 1.2f;
    private const float SkyStart = 0.14f;
    private const float SkyRange = 0.6f;
    private const float SkyDistance = 5200f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIcon = 14f;
    private const float CapsuleBar = 26f;
    private const float CapsuleGap = 6f;
    private const float CapsuleBarHeight = 4f;
    private const float UrgentPowerSeconds = 1.5f;
    private const ulong IdleSeed = 0x54524149UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Trailblaze.Title, GameGenre.Action, L.Trailblaze.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly TrailblazePower[] Powers = { TrailblazePower.Magnet, TrailblazePower.Double, TrailblazePower.Wings };
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.32f, 1f);
    private static readonly Vector4 Feather = new(1f, 0.86f, 0.36f, 1f);
    private static readonly Vector4 Dust = new(0.86f, 0.74f, 0.55f, 0.55f);
    private static readonly Vector4 Stunt = new(0.62f, 0.95f, 1f, 1f);
    private static readonly ParticleSpec DustPuff = new(Dust, Dust with { W = 0f }, 3.2f, 120f, 0.45f, 0f, 2.4f,
        spread: 1.3f, direction: MathF.PI * 0.5f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SlideSparks = new(Gold, Gold with { W = 0f }, 2f, 260f, 0.3f, 0f, 3f,
        spread: 1f, direction: MathF.PI * 0.5f, shape: ParticleShape.Spark);
    private static readonly ParticleSpec LandingRing = new(Dust, Dust with { W = 0f }, 16f, 0f, 0.35f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec CoinSparkle = new(Gold, White, 4f, 150f, 0.55f, 0f, 2.6f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec CoinRing = new(Gold, Gold with { W = 0f }, 9f, 0f, 0.3f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec FeatherBurst = new(Feather, White with { W = 0f }, 5f, 320f, 0.9f, 260f, 1.4f, 9f,
        shape: ParticleShape.Shard);
    private static readonly ParticleSpec WingFeathers = new(White, White with { W = 0f }, 3.4f, 70f, 0.8f, 60f, 1.2f, 5f,
        spread: 2.2f, direction: MathF.PI * 0.5f, shape: ParticleShape.Shard);
    private static readonly ParticleSpec StuntSparkle = new(Stunt, White, 3.5f, 180f, 0.5f, 0f, 2.6f, 6f,
        shape: ParticleShape.Star, additive: true);

    private readonly TrailblazeBoard board = new();
    private readonly TrailblazeBoard idleBoard = new();
    private readonly TrailblazeRenderer renderer = new();
    private readonly ParticleSystem particles = new(448);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly Vector3[] trail = new Vector3[Ribbon.Capacity];
    private Spring cameraLane;
    private Spring lean;
    private Spring roll;
    private Spring slideBlend;
    private Spring airBlend;
    private Spring wingsBlend;
    private Spring glowBlend;
    private LabelSlot milestoneLabel;
    private LabelSlot chainLabel;
    private LabelSlot distanceLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = Gold;
    private Vector2 swipeStart;
    private float bannerProgress = 1f;
    private float bob;
    private float bobVelocity;
    private float squash;
    private float runPhase;
    private float flapPhase;
    private float scenery;
    private float dustClock;
    private float time;
    private int trailHead;
    private int trailCount;
    private bool swipeActive;
    private bool finished;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ResetMotion();
        bannerText = string.Empty;
        bannerProgress = 1f;
        swipeActive = false;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        trailCount = 0;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.State == TrailblazeState.Over)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed));
            ResetMotion();
            idleReady = true;
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        TrailblazeAutopilot.Drive(idleBoard);
        idleBoard.Step(raw);
        UpdateMotion(idleBoard, raw);
        var view = BuildView(context, idleBoard, Vector2.Zero);
        DrawWorld(ImGui.GetWindowDrawList(), context, view, idleBoard, false);
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
        if (!finished)
        {
            HandleInput(context);
            board.Step(simDelta);
        }

        UpdateMotion(board, context.DeltaSeconds);
        var view = BuildView(context, board, fx.ShakeOffset(scale));
        if (!finished)
        {
            React(view, context, scale);
            EmitRunEffects(view, simDelta, scale);
            PushTrail(simDelta);
        }

        DrawWorld(drawList, context, view, board, true);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            bannerText, bannerColor, context.Theme, bannerProgress);
        DrawPowerCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Combo(board.Chain);
        context.Hud.Best(context.Session.Best);
        if (ActivePowers() > 0)
        {
            context.Hud.Custom(PowerCapsuleWidth(ActivePowers()));
        }

        context.Session.Report(board.Score);
        if (finished || board.State != TrailblazeState.Over)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Trailblaze.Distance, distanceLabel.Get(L.Trailblaze.Metres, board.Metres))
            .WithStat(L.Trailblaze.Gil, GameNumber.Label(board.Gil))
            .WithStat(L.Trailblaze.Stunts, GameNumber.Label(board.Stunts))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestChain)));
    }

    private void ResetMotion()
    {
        cameraLane.SnapTo(0f);
        lean.SnapTo(0f);
        roll.SnapTo(0f);
        slideBlend.SnapTo(0f);
        airBlend.SnapTo(0f);
        wingsBlend.SnapTo(0f);
        glowBlend.SnapTo(0f);
        bob = 0f;
        bobVelocity = 0f;
        squash = 0f;
        runPhase = 0f;
        flapPhase = 0f;
        scenery = 0f;
        dustClock = 0f;
        trailHead = 0;
        trailCount = 0;
        ribbon.Clear();
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State == StageFlow.Countdown)
        {
            PressSurface.Claim(SwipeSurfaceId, SwipeArea(context), out _);
            swipeActive = false;
            return;
        }

        if (context.Session.State != StageFlow.Playing || board.State != TrailblazeState.Running)
        {
            swipeActive = false;
            return;
        }

        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Move(TrailblazeMove.Left);
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Move(TrailblazeMove.Right);
        }

        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow) || GameInput.Pressed(ImGuiKey.Space))
        {
            board.Move(TrailblazeMove.Jump);
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Move(TrailblazeMove.Slide);
        }

        HandleSwipe(context);
    }

    private static Rect SwipeArea(in GameContext context)
    {
        var full = context.Full;
        return new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current), full.Max);
    }

    private void HandleSwipe(in GameContext context)
    {
        var scale = UiScale.Current;
        PressSurface.Claim(SwipeSurfaceId, SwipeArea(context), out var activated);
        var mouse = ImGui.GetMousePos();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            swipeActive = false;
            return;
        }

        if (!swipeActive)
        {
            if (!activated || context.ChromeHit(mouse))
            {
                return;
            }

            swipeActive = true;
            swipeStart = mouse;
            return;
        }

        var delta = mouse - swipeStart;
        var threshold = SwipeThreshold * scale;
        if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
        {
            return;
        }

        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
        {
            board.Move(delta.X > 0f ? TrailblazeMove.Right : TrailblazeMove.Left);
        }
        else
        {
            board.Move(delta.Y < 0f ? TrailblazeMove.Jump : TrailblazeMove.Slide);
        }

        swipeStart = mouse;
    }

    private void UpdateMotion(TrailblazeBoard target, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var steering = target.TargetLane - target.LaneX;
        cameraLane.Step((target.LaneX - 1f) * CameraFollow, CameraSmoothSeconds, deltaSeconds);
        lean.Step(target.State == TrailblazeState.Running ? steering * LeanPerLane : 0f, LeanSmoothSeconds, deltaSeconds);
        roll.Step(target.State == TrailblazeState.Running ? steering * RollPerLane : 0f, LeanSmoothSeconds * 2f, deltaSeconds);
        slideBlend.Step(target.Sliding ? 1f : 0f, BlendSmoothSeconds, deltaSeconds);
        airBlend.Step(target.Airborne && target.State == TrailblazeState.Running ? 1f : 0f, BlendSmoothSeconds, deltaSeconds);
        wingsBlend.Step(target.Flying ? 1f : 0f, WingsSmoothSeconds, deltaSeconds);
        glowBlend.Step(target.PowerLeft(TrailblazePower.Double) > 0f ? 1f : 0f, WingsSmoothSeconds, deltaSeconds);
        bobVelocity += (-BobStiffness * bob - BobDamping * bobVelocity) * deltaSeconds;
        bob += bobVelocity * deltaSeconds;
        squash *= MathF.Exp(-9f * deltaSeconds);
        runPhase += target.Speed * StrideRate * deltaSeconds;
        flapPhase += FlapRate * deltaSeconds;
        scenery += Curve(target.Distance) * target.Speed * SceneryRate * deltaSeconds;
    }

    private TrailblazeView BuildView(in GameContext context, TrailblazeBoard target, Vector2 shake)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var speedFraction = Math.Clamp((target.Speed - TrailblazeBoard.BaseSpeed) /
                                       (TrailblazeBoard.MaxSpeed - TrailblazeBoard.BaseSpeed), 0f, 1f);
        var view = new TrailblazeView(full, target.Distance, cameraLane.Value, Curve(target.Distance) * full.Width,
            roll.Value, context.Fx.PlateScale, shake + new Vector2(0f, bob * scale), speedFraction);
        var sky = SkyStart + SkyRange * Math.Clamp(target.Distance / SkyDistance, 0f, 1f);
        context.Backdrop.SetSky(sky);
        renderer.Prepare(context.Backdrop.Ground, sky);
        return view;
    }

    private static float Curve(float distance) =>
        CurveAmount * MathF.Sin(distance / 140f) * MathF.Sin(distance / 53f + 1.3f);

    private void React(in TrailblazeView view, in GameContext context, float scale)
    {
        var feet = view.At(board.LaneX - 1f, board.Height, board.Distance);
        var head = feet - new Vector2(0f, 2.2f * view.PlayerScale);
        if (board.CoinsThisStep > 0)
        {
            OnCoins(view);
        }

        if (board.ChainTierUpThisStep)
        {
            GameSfx.ComboTierUp();
            fx.AddText(chainLabel.Get(L.Stage.Times, board.Chain.Multiplier), head, Gold, 1.2f);
            if (board.Chain.Multiplier >= 3)
            {
                context.Fx.Punch(0.03f);
            }
        }

        if (board.JumpedThisStep)
        {
            UiFeedback.Play(UiSound.GameJump);
            squash = -0.9f;
            particles.Emit(DustPuff, feet, 6);
        }

        if (board.SlidThisStep)
        {
            UiFeedback.Play(UiSound.GameShuffle);
            squash = 0.5f;
            particles.Emit(SlideSparks, feet, 8);
        }

        if (board.SlammedThisStep)
        {
            particles.Emit(WingFeathers, head, 4);
        }

        if (board.LandedThisStep)
        {
            OnLanding(feet);
        }

        if (board.SteeredThisStep != 0)
        {
            particles.Streaks(feet + new Vector2(0f, -0.6f * view.PlayerScale), 4, White with { W = 0.6f }, 240f * scale, 2f, 0.25f,
                0.5f, board.SteeredThisStep > 0 ? MathF.PI : 0f, 0f);
        }

        if (board.BumpedThisStep != 0)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            fx.AddTrauma(0.22f);
            lean.Velocity -= board.BumpedThisStep * 5f;
            particles.Emit(DustPuff, feet + new Vector2(board.BumpedThisStep * 0.5f * view.PlayerScale, 0f), 5);
        }

        if (board.StuntThisStep != TrailblazeStunt.None)
        {
            OnStunt(head, context);
        }

        if (board.PowerThisStep != TrailblazePower.None)
        {
            OnPower(board.PowerThisStep, head, context, scale);
        }

        if (board.PowerEndedThisStep == TrailblazePower.Wings)
        {
            particles.Emit(WingFeathers, head, 14);
        }

        if (board.MilestoneThisStep > 0)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            ShowBanner(milestoneLabel.Get(L.Trailblaze.Milestone, board.MilestoneThisStep), Accent);
        }

        if (board.DiedThisStep)
        {
            OnDeath(feet, head, context, scale);
        }
    }

    private void OnCoins(in TrailblazeView view)
    {
        UiFeedback.Play(UiSound.GameCollect);
        for (var index = 0; index < board.CoinsThisStep; index++)
        {
            var coin = board.CoinEventAt(index);
            var position = view.At(coin.X - 1f, coin.Y, coin.Z);
            particles.Emit(CoinSparkle, position, board.PowerLeft(TrailblazePower.Double) > 0f ? 7 : 4);
            particles.Emit(CoinRing, position, 1);
        }
    }

    private void OnLanding(Vector2 feet)
    {
        var impact = board.LandingImpact;
        bobVelocity += impact * BobPerImpact;
        squash = MathF.Min(1f, 0.35f + impact / 30f);
        particles.Emit(LandingRing, feet, 1);
        particles.Emit(DustPuff, feet, 8);
        if (impact > TrailblazeBoard.SlamVelocity * 0.8f)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            fx.AddTrauma(0.12f);
        }
    }

    private void OnStunt(Vector2 head, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePop);
        var label = board.StuntThisStep == TrailblazeStunt.Leap ? L.Trailblaze.Leap : L.Trailblaze.Duck;
        fx.AddText(Loc.T(label), head - new Vector2(0f, 18f * UiScale.Current), Stunt, 1.05f);
        fx.AddText(GameNumber.Signed(TrailblazeBoard.StuntPoints), head, White, 0.9f, 60f);
        particles.Emit(StuntSparkle, head, 10);
        context.Fx.Punch(0.02f);
    }

    private void OnPower(TrailblazePower power, Vector2 head, in GameContext context, float scale)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        var color = TrailblazePowerArt.Color(power);
        var label = power switch
        {
            TrailblazePower.Magnet => L.Trailblaze.Magnet,
            TrailblazePower.Double => L.Trailblaze.Double,
            _ => L.Trailblaze.Wings,
        };
        ShowBanner(Loc.T(label), color);
        context.Fx.Flash(color, 0.22f);
        context.Fx.Punch(0.05f);
        context.Fx.Sweep();
        fx.Shockwave(head, 90f * scale, color, 0.5f, 3f);
        particles.Burst(head, 20, color, 260f * scale, 3.2f, 0.6f, 0f);
        if (power == TrailblazePower.Wings)
        {
            particles.Emit(WingFeathers, head, 16);
        }
    }

    private void OnDeath(Vector2 feet, Vector2 head, in GameContext context, float scale)
    {
        context.Fx.Flash(Danger, 0.45f);
        context.Fx.Vignette(Danger, 0.3f, 0.8f);
        fx.AddTrauma(board.Death == TrailblazeDeath.Crash ? 0.75f : 0.4f);
        if (board.Death == TrailblazeDeath.Crash)
        {
            UiFeedback.Play(UiSound.GameBreak);
            context.Fx.SlowMo(0.35f, 0.6f);
            context.Fx.Punch(0.07f);
            particles.Emit(FeatherBurst, (feet + head) * 0.5f, 28);
            fx.Shockwave((feet + head) * 0.5f, 120f * scale, Feather, 0.6f, 3.4f);
            return;
        }

        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.SlowMo(0.5f, 0.5f);
        particles.Emit(DustPuff, feet, 14);
        particles.Emit(WingFeathers, head, 8);
    }

    private void EmitRunEffects(in TrailblazeView view, float deltaSeconds, float scale)
    {
        if (deltaSeconds <= 0f || board.State != TrailblazeState.Running)
        {
            return;
        }

        dustClock += deltaSeconds;
        if (dustClock < DustInterval)
        {
            return;
        }

        dustClock = 0f;
        var feet = view.At(board.LaneX - 1f, board.Height, board.Distance);
        if (board.Flying)
        {
            particles.Emit(WingFeathers, feet - new Vector2(0f, 1.2f * view.PlayerScale), 1);
            return;
        }

        if (board.Airborne)
        {
            return;
        }

        var side = MathF.Sin(runPhase) > 0f ? -1f : 1f;
        particles.Emit(DustPuff, feet + new Vector2(side * 0.22f * view.PlayerScale, 0f), board.Sliding ? 3 : 1);
        if (board.Sliding)
        {
            particles.Emit(SlideSparks, feet, 2);
        }
    }

    private void PushTrail(float deltaSeconds)
    {
        if (deltaSeconds <= 0f || board.State != TrailblazeState.Running)
        {
            return;
        }

        trail[trailHead] = new Vector3(board.LaneX - 1f, board.Height + RibbonHeight, board.Distance);
        trailHead = (trailHead + 1) % trail.Length;
        trailCount = Math.Min(trail.Length, trailCount + 1);
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, in TrailblazeView view, TrailblazeBoard target,
        bool live)
    {
        var full = context.Full;
        drawList.PushClipRect(full.Min, full.Max, true);
        renderer.DrawBackdrop(drawList, view, scenery);
        renderer.DrawGround(drawList, view, target);
        renderer.DrawRoadside(drawList, view, time);
        renderer.DrawObjects(drawList, view, target, target.Distance, view.FarWorldZ, time);
        if (live)
        {
            DrawTrail(drawList, view);
        }

        DrawRunner(drawList, view, target, full);
        renderer.DrawObjects(drawList, view, target, view.NearWorldZ, target.Distance, time);
        renderer.DrawTrailingCarts(drawList, view, target, view.NearWorldZ);
        if (live)
        {
            particles.Draw(drawList, UiScale.Current);
            fx.DrawRings(drawList, UiScale.Current);
            var intensity = Math.Clamp((target.Speed - 17f) / 10f, 0f, 1f) + wingsBlend.Value * 0.5f;
            renderer.DrawSpeedLines(drawList, view, MathF.Min(1f, intensity), time, UiScale.Current);
        }

        drawList.PopClipRect();
        if (live)
        {
            fx.DrawText();
        }
    }

    private void DrawTrail(ImDrawListPtr drawList, in TrailblazeView view)
    {
        var strength = MathF.Max(wingsBlend.Value, glowBlend.Value * 0.6f);
        if (strength <= 0.02f || trailCount < 2)
        {
            return;
        }

        ribbon.Clear();
        for (var age = trailCount - 1; age >= 0; age--)
        {
            var point = trail[(trailHead - 1 - age + trail.Length * 2) % trail.Length];
            if (view.Depth(point.Z) < TrailblazeView.NearZ * TrailNearFraction)
            {
                continue;
            }

            ribbon.Push(view.At(point.X, point.Y, point.Z));
        }

        var color = wingsBlend.Value > glowBlend.Value ? new Vector4(0.8f, 0.94f, 1f, 0.7f * strength) : Gold with { W = 0.6f * strength };
        ribbon.Draw(drawList, color, RibbonWidth * view.PlayerScale, additive: true);
    }

    private void DrawRunner(ImDrawListPtr drawList, in TrailblazeView view, TrailblazeBoard target, Rect full)
    {
        var offset = target.LaneX - 1f;
        var pose = new TrailblazePose
        {
            Feet = view.At(offset, target.Height, target.Distance),
            Ground = view.At(offset, 0f, target.Distance),
            Scale = view.PlayerScale,
            Angle = view.Roll + lean.Value,
            RunPhase = runPhase,
            Slide = slideBlend.Value,
            Air = MathF.Max(airBlend.Value, wingsBlend.Value),
            Squash = squash,
            Wings = wingsBlend.Value,
            Flap = flapPhase,
            Alpha = RunnerAlpha(target),
            Glow = glowBlend.Value,
            Height = target.Height,
            Look = Math.Clamp((target.TargetLane - target.LaneX) * 2f + 0.4f, -1f, 1f),
            Shadow = target.Death == TrailblazeDeath.Fall ? 0f : 1f,
        };
        if (target.State != TrailblazeState.Running && target.Death == TrailblazeDeath.Crash)
        {
            pose.Angle += target.DyingProgress * MathF.PI * 2.5f;
        }

        if (target.Death == TrailblazeDeath.Fall)
        {
            var lip = view.At(offset, 0f, FallLip(target)).Y;
            drawList.PushClipRect(full.Min, new Vector2(full.Max.X, MathF.Max(full.Min.Y, lip)), true);
            TrailblazeChocobo.Draw(drawList, pose);
            drawList.PopClipRect();
            return;
        }

        TrailblazeChocobo.Draw(drawList, pose);
        if (target.PowerLeft(TrailblazePower.Magnet) <= 0f || target.State != TrailblazeState.Running)
        {
            return;
        }

        var center = pose.Feet - new Vector2(0f, 1.1f * pose.Scale);
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 8f);
        var magnet = TrailblazePowerArt.Color(TrailblazePower.Magnet);
        drawList.AddCircle(center, (1.3f + 0.15f * pulse) * pose.Scale, ImGui.GetColorU32(magnet with { W = 0.35f + 0.25f * pulse }),
            32, MathF.Max(1f, 0.06f * pose.Scale));
    }

    private float RunnerAlpha(TrailblazeBoard target)
    {
        if (target.GraceLeft <= 0f || target.Flying)
        {
            return 1f;
        }

        return MathF.Sin(time * 40f) > 0f ? 1f : 0.45f;
    }

    private static float FallLip(TrailblazeBoard target)
    {
        for (var index = 0; index < target.HazardCount; index++)
        {
            ref readonly var hazard = ref target.HazardAt(index);
            if (hazard.Kind == TrailblazeCell.Gap && target.Distance >= hazard.Z && target.Distance <= hazard.Z + hazard.Length + 2f &&
                MathF.Abs(hazard.Lane - target.LaneX) < 0.6f)
            {
                return hazard.Z;
            }
        }

        return target.Distance;
    }

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private int ActivePowers()
    {
        var count = 0;
        for (var index = 0; index < Powers.Length; index++)
        {
            if (board.PowerLeft(Powers[index]) > 0f)
            {
                count++;
            }
        }

        return count;
    }

    private static float PowerCapsuleWidth(int count) =>
        CapsulePadX * 2f + count * (CapsuleIcon + CapsuleGap + CapsuleBar) + (count - 1) * CapsuleGap;

    private void DrawPowerCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (ActivePowers() == 0 || !context.Hud.CustomPlaced(0))
        {
            return;
        }

        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var x = rect.Min.X + CapsulePadX * scale;
        var centerY = rect.Center.Y;
        for (var index = 0; index < Powers.Length; index++)
        {
            var power = Powers[index];
            var left = board.PowerLeft(power);
            if (left <= 0f)
            {
                continue;
            }

            var fraction = left / TrailblazeBoard.PowerSeconds(power);
            var blink = left < UrgentPowerSeconds && MathF.Sin(time * 18f) < 0f ? 0.4f : 1f;
            var iconSize = CapsuleIcon * scale;
            TrailblazePowerArt.Draw(drawList, power, new Vector2(x + iconSize * 0.5f, centerY), iconSize * 0.62f, blink);
            x += iconSize + CapsuleGap * scale;
            var bar = new Rect(new Vector2(x, centerY - CapsuleBarHeight * scale * 0.5f),
                new Vector2(x + CapsuleBar * scale, centerY + CapsuleBarHeight * scale * 0.5f));
            StageHud.Bar(drawList, bar, fraction, TrailblazePowerArt.Color(power) with { W = blink }, scale);
            x += CapsuleBar * scale + CapsuleGap * scale;
        }
    }
}
