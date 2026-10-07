using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Thrust;

internal sealed class ThrustApp : IMiniGame
{
    private const string GameId = "thrust";
    private const string PressSurfaceId = "thrust.press";
    private const float ViewTop = 92f;
    private const float ViewBottom = 8f;
    private const float ViewWidth = 10f;
    private const float PilotScreenFraction = 0.24f;
    private const float FollowSmoothSeconds = 0.06f;
    private const float TiltPerVelocity = 0.03f;
    private const float TiltSmoothSeconds = 0.08f;
    private const float ThrustSmoothSeconds = 0.04f;
    private const float StrideRate = 3f;
    private const float FlapRate = 16f;
    private const float BannerSeconds = 1.4f;
    private const float RibbonWidth = 0.22f;
    private const float ExhaustRate = 70f;
    private const float SmokeRate = 30f;
    private const float DangerReach = 3f;
    private const float CapsuleWidth = 38f;
    private const float HardLanding = 7f;
    private const ulong IdleSeed = 0x54485255UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Thrust.Title, GameGenre.Action, L.Thrust.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.98f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Smoke = new(0.55f, 0.56f, 0.62f, 0.55f);
    private static readonly Vector4 Ember = new(1f, 0.45f, 0.2f, 1f);
    private static readonly ParticleSpec ExhaustFlame = new(ThrustArt.Flame, Ember with { W = 0f }, 0.13f, 3.6f, 0.32f, 5f,
        2f, spread: 0.7f, direction: MathF.PI * 0.5f + 0.5f, shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec MissileSmoke = new(Smoke, Smoke with { W = 0f }, 0.2f, 0.8f, 0.6f, -0.5f, 1.4f,
        spread: 0.8f, direction: 0f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec CoinSparkle = new(ThrustArt.Coin, White, 0.1f, 3.4f, 0.5f, 0f, 2.6f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec CoinRing = new(ThrustArt.Coin, ThrustArt.Coin with { W = 0f }, 0.34f, 0f, 0.3f,
        shape: ParticleShape.Ring, curve: SizeCurve.Grow);
    private static readonly ParticleSpec ZapSparks = new(ThrustArt.Electric, White with { W = 0f }, 0.08f, 7f, 0.5f, 8f, 1.8f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec Blast = new(ThrustArt.Flame, Ember with { W = 0f }, 0.22f, 6.5f, 0.7f, 4f, 1.8f,
        shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec BlastSmoke = new(Smoke, Smoke with { W = 0f }, 0.4f, 2.2f, 0.9f, -1f, 1.6f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec Feathers = new(ThrustArt.Plumage, White with { W = 0f }, 0.13f, 5f, 0.9f, 7f, 1.5f, 9f,
        shape: ParticleShape.Shard);
    private static readonly ParticleSpec Dust = new(Smoke, Smoke with { W = 0f }, 0.16f, 2.4f, 0.4f, 0f, 2.4f,
        spread: 1.2f, direction: -MathF.PI * 0.5f, curve: SizeCurve.Grow);
    private static readonly ParticleSpec CloseSparkle = new(ThrustArt.Electric, White, 0.1f, 3f, 0.45f, 0f, 2.6f, 6f,
        shape: ParticleShape.Star, additive: true);

    private readonly ThrustBoard board = new();
    private readonly ThrustBoard idleBoard = new();
    private readonly ParticleSystem particles = new(448);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private Camera2D camera = Camera2D.Create();
    private Emitter exhaust = new(ExhaustFlame, ExhaustRate);
    private Emitter smoke = new(MissileSmoke, SmokeRate);
    private Spring tilt;
    private Spring thrust;
    private LabelSlot milestoneLabel;
    private LabelSlot chainLabel;
    private LabelSlot distanceLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private float bannerProgress = 1f;
    private float run;
    private float time;
    private bool pressHeld;
    private bool cameraPlaced;
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
        pressHeld = false;
        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.State == ThrustState.Over)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed));
            ResetMotion();
            idleReady = true;
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        var hold = ThrustAutopilot.Hold(idleBoard, out var tap);
        if (tap)
        {
            idleBoard.Tap();
        }

        idleBoard.Step(raw, hold);
        PlaceCamera(context, idleBoard, raw);
        UpdateMotion(idleBoard, raw);
        DrawWorld(ImGui.GetWindowDrawList(), context, idleBoard, false);
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
        PlaceCamera(context, board, context.DeltaSeconds);
        if (!finished)
        {
            var holding = ReadInput(context);
            board.Step(simDelta, holding);
            React(context, scale);
            EmitTrails(simDelta);
        }

        UpdateMotion(board, context.DeltaSeconds);
        DrawWorld(drawList, context, board, true);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            bannerText, bannerColor, context.Theme, bannerProgress);
        DrawMountCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Combo(board.Chain);
        context.Hud.Best(context.Session.Best);
        if (board.Mounted)
        {
            context.Hud.Custom(CapsuleWidth);
        }

        context.Session.Report(board.Score);
        if (finished || board.State != ThrustState.Over)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Thrust.Distance, distanceLabel.Get(L.Thrust.Metres, board.Metres))
            .WithStat(L.Thrust.Coins, GameNumber.Label(board.Coins))
            .WithStat(L.Thrust.CloseCalls, GameNumber.Label(board.NearMisses))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestChain)));
    }

    private void ResetMotion()
    {
        tilt.SnapTo(0f);
        thrust.SnapTo(0f);
        run = 0f;
        cameraPlaced = false;
        exhaust.Reset();
        smoke.Reset();
        ribbon.Clear();
    }

    private void PlaceCamera(in GameContext context, ThrustBoard target, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var view = new Rect(new Vector2(full.Min.X, full.Min.Y + ViewTop * scale),
            new Vector2(full.Max.X, MathF.Max(full.Min.Y + ViewTop * scale + 1f, full.Max.Y - ViewBottom * scale)));
        camera.Fit(view, ViewWidth, ThrustBoard.Height + ThrustRenderer.WallThickness * 2f, FitMode.Contain);
        var lead = new Vector2((0.5f - PilotScreenFraction) * camera.Units(view.Width), 0f);
        var focus = new Vector2(target.X, ThrustBoard.Height * 0.5f);
        if (!cameraPlaced)
        {
            camera.Place(focus + lead);
            cameraPlaced = true;
        }
        else
        {
            camera.Follow(focus, lead, FollowSmoothSeconds, deltaSeconds);
        }

        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private bool ReadInput(in GameContext context)
    {
        if (context.Session.State == StageFlow.Countdown)
        {
            PressSurface.Claim(PressSurfaceId, PressArea(context), out _);
            pressHeld = false;
            return false;
        }

        if (context.Session.State != StageFlow.Playing || board.State != ThrustState.Running)
        {
            pressHeld = false;
            return false;
        }

        PressSurface.Claim(PressSurfaceId, PressArea(context), out var activated);
        if (activated && !context.ChromeHit(ImGui.GetMousePos()))
        {
            pressHeld = true;
            board.Tap();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pressHeld = false;
        }

        if (GameInput.Pressed(ImGuiKey.Space) || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Tap();
        }

        return pressHeld || GameInput.Held(ImGuiKey.Space) || GameInput.Held(ImGuiKey.W, ImGuiKey.UpArrow);
    }

    private static Rect PressArea(in GameContext context)
    {
        var full = context.Full;
        return new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current), full.Max);
    }

    private void UpdateMotion(ThrustBoard target, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        tilt.Step(Math.Clamp(target.VelocityY * TiltPerVelocity, -0.35f, 0.3f), TiltSmoothSeconds, deltaSeconds);
        thrust.Step(target.Holding && !target.Mounted ? 1f : 0f, ThrustSmoothSeconds, deltaSeconds);
        if (target.Grounded)
        {
            run += target.Speed * StrideRate * deltaSeconds;
        }
    }

    private void React(in GameContext context, float scale)
    {
        var pilot = new Vector2(board.X, board.Y);
        var screen = camera.ToScreen(pilot);
        if (board.ThrustStartedThisStep)
        {
            particles.Emit(ExhaustFlame, Nozzle(), 6);
        }

        if (board.CoinsThisStep > 0)
        {
            UiFeedback.Play(UiSound.GameCollect);
            for (var index = 0; index < board.CoinsThisStep; index++)
            {
                var coin = board.CoinEventAt(index);
                particles.Emit(CoinSparkle, coin, 5);
                particles.Emit(CoinRing, coin, 1);
            }
        }

        if (board.ChainTierUpThisStep)
        {
            GameSfx.ComboTierUp();
            fx.AddText(chainLabel.Get(L.Stage.Times, board.Chain.Multiplier), screen - new Vector2(0f, 34f * scale),
                ThrustArt.Coin, 1.15f);
            if (board.Chain.Multiplier >= 3)
            {
                context.Fx.Punch(0.03f);
            }
        }

        if (board.NearMissThisStep)
        {
            UiFeedback.Play(UiSound.GamePop);
            var position = camera.ToScreen(board.NearMissPosition);
            fx.AddText(Loc.T(L.Thrust.Close), position - new Vector2(0f, 30f * scale), ThrustArt.Electric, 1.05f);
            fx.AddText(GameNumber.Signed(ThrustBoard.NearMissPoints), position - new Vector2(0f, 10f * scale), White, 0.85f, 60f);
            particles.Emit(CloseSparkle, board.NearMissPosition, 8);
            context.Fx.EdgeGlow(0.5f);
        }

        if (board.BonkedThisStep)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            camera.Shake(0.12f);
        }

        if (board.LandedThisStep && board.LandingImpact > HardLanding)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            camera.Shake(0.1f);
            particles.Emit(Dust, new Vector2(board.X, ThrustBoard.Height), 8);
        }

        if (board.JumpedThisStep)
        {
            UiFeedback.Play(UiSound.GameJump);
            particles.Emit(Dust, new Vector2(board.X, board.Y + board.Radius), 5);
        }

        if (board.MissileWarnedThisStep)
        {
            UiFeedback.Play(UiSound.GameTick);
        }

        if (board.MissileLockedThisStep)
        {
            UiFeedback.Play(UiSound.GameWrong);
            context.Fx.Vignette(Danger, 0.18f, 0.5f);
        }

        if (board.MissileLaunchedThisStep)
        {
            UiFeedback.Play(UiSound.GameShoot);
        }

        if (board.MountedThisStep)
        {
            UiFeedback.Play(UiSound.GamePowerUp);
            ShowBanner(Loc.T(L.Thrust.Chocobo), ThrustArt.Plumage);
            context.Fx.Flash(ThrustArt.Plumage, 0.2f);
            context.Fx.Sweep();
            context.Fx.Punch(0.05f);
            fx.Shockwave(screen, 80f * scale, ThrustArt.Plumage, 0.5f, 3f);
            particles.Emit(Feathers, pilot, 14);
        }

        if (board.DismountedThisStep)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            fx.AddText(Loc.T(L.Thrust.Saved), screen - new Vector2(0f, 36f * scale), ThrustArt.Plumage, 1.15f);
            particles.Emit(Feathers, pilot, 24);
            camera.Shake(0.35f);
            context.Fx.Flash(White, 0.2f);
        }

        if (board.MilestoneThisStep > 0)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
            ShowBanner(milestoneLabel.Get(L.Thrust.Milestone, board.MilestoneThisStep), Accent);
        }

        if (board.DiedThisStep)
        {
            OnDeath(context, pilot, screen, scale);
        }
    }

    private void OnDeath(in GameContext context, Vector2 pilot, Vector2 screen, float scale)
    {
        camera.Shake(0.7f);
        context.Fx.SlowMo(0.4f, 0.55f);
        context.Fx.Punch(0.06f);
        if (board.Death == ThrustDeath.Zapped)
        {
            UiFeedback.Play(UiSound.GameBreak);
            context.Fx.Flash(ThrustArt.Electric, 0.5f);
            particles.Emit(ZapSparks, pilot, 30);
            fx.Shockwave(screen, 110f * scale, ThrustArt.Electric, 0.55f, 3.4f);
            return;
        }

        UiFeedback.Play(UiSound.GameExplosion);
        context.Fx.Flash(Danger, 0.45f);
        particles.Emit(Blast, pilot, 26);
        particles.Emit(BlastSmoke, pilot, 12);
        fx.Shockwave(screen, 130f * scale, ThrustArt.Flame, 0.6f, 3.6f);
    }

    private void EmitTrails(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (board.State == ThrustState.Running)
        {
            ribbon.Push(Nozzle());
        }

        if (board.Holding && !board.Mounted && board.State == ThrustState.Running)
        {
            exhaust.Advance(deltaSeconds, Nozzle(), particles);
        }

        for (var index = 0; index < board.MissileCount; index++)
        {
            ref readonly var missile = ref board.MissileAt(index);
            if (missile.Phase == MissilePhase.Flying)
            {
                smoke.Advance(deltaSeconds, new Vector2(missile.X + 1.05f, missile.Y), particles);
            }
        }
    }

    private Vector2 Nozzle()
    {
        var radius = ThrustBoard.PlayerRadius;
        var angle = tilt.Value;
        var local = new Vector2(-0.72f * radius, 0.85f * radius);
        return new Vector2(board.X + local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle),
            board.Y + local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle));
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, ThrustBoard target, bool live)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var accent = Accent;
        drawList.PushClipRect(full.Min, full.Max, true);
        ThrustRenderer.DrawBackWall(drawList, camera, accent);
        var visible = camera.VisibleWorld;
        for (var index = 0; index < target.CoinCount; index++)
        {
            var coin = target.CoinAt(index);
            if (coin.X > visible.Min.X - 1f && coin.X < visible.Max.X + 1f)
            {
                ThrustRenderer.DrawCoin(drawList, camera, coin, time);
            }
        }

        if (target.VehicleActive)
        {
            ThrustRenderer.DrawVehicle(drawList, camera, target.VehiclePosition, time);
        }

        for (var index = 0; index < target.ZapperCount; index++)
        {
            ref readonly var zapper = ref target.ZapperAt(index);
            if (zapper.Right < visible.Min.X - 1f || zapper.Left > visible.Max.X + 1f)
            {
                continue;
            }

            var danger = 1f - Math.Clamp((zapper.Left - target.X) / DangerReach, 0f, 1f);
            ThrustRenderer.DrawZapper(drawList, camera, zapper, index, target.Time, danger);
        }

        for (var index = 0; index < target.MissileCount; index++)
        {
            ref readonly var missile = ref target.MissileAt(index);
            if (missile.Phase == MissilePhase.Flying)
            {
                ThrustRenderer.DrawMissile(drawList, camera, missile, time);
            }
        }

        if (live)
        {
            ribbon.Draw(drawList, in camera, Accent with { W = 0.5f * MathF.Max(0.25f, thrust.Value) },
                camera.Px(RibbonWidth), additive: true);
            particles.Draw(drawList, in camera);
        }

        DrawPilot(drawList, target);
        ThrustRenderer.DrawWalls(drawList, camera, full, accent, time, scale);
        for (var index = 0; index < target.MissileCount; index++)
        {
            ref readonly var missile = ref target.MissileAt(index);
            if (missile.Phase != MissilePhase.Flying)
            {
                ThrustRenderer.DrawWarning(drawList, camera, missile, time, scale);
            }
        }

        if (live)
        {
            var intensity = Math.Clamp((target.Speed - 6f) / 2.5f, 0f, 1f);
            ThrustRenderer.DrawSpeedLines(drawList, camera, intensity, time, scale);
            fx.DrawRings(drawList, scale);
        }

        drawList.PopClipRect();
        if (live)
        {
            fx.DrawText();
        }
    }

    private void DrawPilot(ImDrawListPtr drawList, ThrustBoard target)
    {
        var center = camera.ToScreen(new Vector2(target.X, target.Y));
        var alpha = target.Invulnerable && MathF.Sin(time * 36f) < 0f ? 0.4f : 1f;
        var angle = tilt.Value;
        if (target.State != ThrustState.Running)
        {
            angle += target.DyingProgress * 7f;
        }

        if (target.Mounted)
        {
            ThrustArt.DrawRider(drawList, center, camera.Px(ThrustBoard.MountedRadius), angle * 0.5f, run, target.Grounded,
                alpha, time);
            return;
        }

        var radius = camera.Px(ThrustBoard.PlayerRadius) * 1.08f;
        ThrustArt.DrawPilot(drawList, center, radius, angle, time * FlapRate, thrust.Value, run, target.Grounded, alpha, time);
        if (target.State == ThrustState.Running || target.Death != ThrustDeath.Zapped)
        {
            return;
        }

        var flicker = MathF.Sin(time * 50f) > 0f ? 0.55f : 0.15f;
        drawList.AddCircleFilled(center, radius * 1.3f, ImGui.GetColorU32(ThrustArt.Electric with { W = flicker * (1f - target.DyingProgress) }), 20);
    }

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private void DrawMountCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (!board.Mounted || !context.Hud.CustomPlaced(0))
        {
            return;
        }

        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var pulse = 0.85f + 0.15f * MathF.Sin(time * 4f);
        ThrustArt.DrawFeather(drawList, rect.Center, rect.Height * 0.3f * pulse, -0.75f, 1f);
    }
}
