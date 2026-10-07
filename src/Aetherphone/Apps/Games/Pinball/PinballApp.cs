using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pinball;

internal enum PressMode : byte
{
    None,
    LeftFlipper,
    RightFlipper,
    Plunger,
}

internal sealed class PinballApp : IMiniGame
{
    private const string GameId = "pinball";
    private const string TableSurfaceId = "pinball.table";
    private const ulong IdleSeed = 0x50494E42414CUL;
    private const float BannerSeconds = 1.7f;
    private const float ResultDelaySeconds = 1.4f;
    private const float DisplayHeight = 40f;
    private const float DisplayGap = 6f;
    private const float MinDisplayWidth = 120f;
    private const float SideInset = 8f;
    private const float BottomInset = 8f;
    private const float PlungerDragUnits = 1.3f;
    private const float RibbonWidth = 0.2f;
    private const float PromptDelaySeconds = 2f;
    private const float KickDecay = 5f;
    private const int BannerLow = 1;
    private const int BannerMedium = 2;
    private const int BannerHigh = 3;
    private const int ConfettiCount = 46;
    private static readonly GameSpec StageSpec = new(GameId, L.Pinball.Title, GameGenre.Arcade, L.Pinball.Hook,
        Backdrop.Neon, HudStyle.Compact, ScoreKind.Score, clocked: true, keyboard: true);
    private static readonly Rect WorldRect = new(new Vector2(-0.1f, -0.06f), new Vector2(6.1f, 12.32f));
    private static readonly Vector2 BannerPoint = new(2.7f, 6.4f);
    private static readonly Vector2 PromptPoint = new(2.7f, 8.15f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 Ring = new(1f, 1f, 1f, 0.6f);
    private static readonly Vector4[] CelebrationPalette =
    {
        new(1f, 0.82f, 0.36f, 1f), new(1f, 0.36f, 0.74f, 1f), new(0.36f, 0.9f, 1f, 1f), new(0.7f, 0.52f, 1f, 1f),
        new(1f, 1f, 1f, 1f),
    };

    private static readonly ParticleSpec BumperSparks = new(White, PinballRenderer.Magenta, 0.05f, 5f, 0.35f, 0f, 3f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec SlingSparks = new(White, PinballRenderer.Violet, 0.045f, 4.5f, 0.3f, 0f, 3f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec SpinnerSparks = new(White, new Vector4(1f, 0.82f, 0.36f, 0f), 0.03f, 2.4f,
        0.3f, 4f, 2f, shape: ParticleShape.Spark);
    private static readonly ParticleSpec RampStars = new(White, PinballRenderer.Cyan, 0.08f, 3f, 0.8f, 1.5f, 2f, 6f,
        shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec JackpotGlow = new(White, new Vector4(1f, 0.6f, 0.2f, 0f), 0.11f, 7f, 1f, 3f,
        1.6f, shape: ParticleShape.GlowCircle, additive: true);
    private static readonly ParticleSpec LaunchStreaks = new(White, new Vector4(1f, 0.82f, 0.36f, 0f), 0.05f, 6f, 0.3f,
        0f, 2.5f, spread: 0.5f, direction: MathF.PI * 0.5f, shape: ParticleShape.Streak);
    private static readonly ParticleSpec DrainShards = new(Danger, Danger with { W = 0f }, 0.07f, 4f, 0.6f, 5f, 1.4f,
        8f, MathF.PI, -MathF.PI * 0.5f, ParticleShape.Shard);
    private static readonly ParticleSpec LockRings = new(PinballRenderer.Violet, PinballRenderer.Violet with { W = 0f },
        0.18f, 0.2f, 0.6f, 0f, 1f, shape: ParticleShape.Ring, additive: true);
    private static readonly ParticleSpec LaneStars = new(White, new Vector4(1f, 0.82f, 0.36f, 0f), 0.06f, 2.2f, 0.6f,
        1f, 2.2f, 6f, shape: ParticleShape.Star, additive: true);

    private readonly PinballBoard board = new();
    private readonly PinballDisplay display = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] ribbons = new Ribbon[PinballBoard.MaxBalls];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot ballLabel;
    private LabelSlot lockLabel;
    private LabelSlot multiplierLabel;
    private LabelSlot warningLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private float bannerProgress = 1f;
    private int bannerPriority;
    private PressMode pressMode;
    private Vector2 pressStart;
    private bool keyPulling;
    private bool leftWasHeld;
    private bool rightWasHeld;
    private float plungerKick;
    private float waitingSeconds;
    private float resultDelay;
    private float time;
    private bool finished;

    public PinballApp()
    {
        for (var slot = 0; slot < ribbons.Length; slot++)
        {
            ribbons[slot] = new Ribbon();
        }

        board.Reset(GameRandom.FromSeed(IdleSeed), true);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random, false);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        display.Reset();
        ClearRibbons();
        camera = Camera2D.Create();
        pressMode = PressMode.None;
        keyPulling = false;
        leftWasHeld = false;
        rightWasHeld = false;
        plungerKick = 0f;
        waitingSeconds = 0f;
        resultDelay = ResultDelaySeconds;
        finished = false;
        bannerPriority = 0;
        bannerProgress = 1f;
        ShowBanner(ballLabel.Get(L.Pinball.BallNumber, board.BallNumber), White, BannerLow);
    }

    public void Close()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed), true);
        particles.Clear();
        fx.Clear();
        display.Reset();
        ClearRibbons();
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || board.Attract || board.Score <= 0)
        {
            return;
        }

        finished = true;
        session.Finish(Outcome(session));
    }

    public void DrawIdle(in GameContext context)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        PlaceCamera(context);
        board.BeginFrame();
        board.Update(deltaSeconds);
        particles.Update(deltaSeconds);
        PushTrails(deltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        DrawWorld(drawList, UiScale.Current);
        display.DrawAttract(drawList, DisplayRect(context, UiScale.Current), Accent, UiScale.Current, board.Score, time);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        time += context.RawDeltaSeconds;
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        display.Update(context.RawDeltaSeconds);
        plungerKick = MathF.Max(0f, plungerKick - context.RawDeltaSeconds * KickDecay);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, BannerSeconds);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        display.Draw(drawList, DisplayRect(context, scale), Accent, scale, board, time);
        DrawPrompt(drawList, context, scale);
        GameBanner.Draw(drawList, camera.ToScreen(BannerPoint), bannerText, bannerColor, context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Lives(board.BallsLeft, Math.Max(PinballBoard.BallsPerGame, board.BallsLeft));
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var top = full.Min.Y + (StageLayout.SafeTopCompact + DisplayHeight + DisplayGap) * scale;
        var view = new Rect(new Vector2(full.Min.X + SideInset * scale, top),
            new Vector2(full.Max.X - SideInset * scale, MathF.Max(top + 1f, full.Max.Y - BottomInset * scale)));
        camera.Fit(view, WorldRect, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private Rect DisplayRect(in GameContext context, float scale)
    {
        var top = context.Full.Min.Y + (StageLayout.SafeTopCompact + 2f) * scale;
        var halfWidth = MathF.Max(camera.Zoom * WorldRect.Width, MinDisplayWidth * scale) * 0.5f;
        var centerX = camera.View.Center.X;
        return new Rect(new Vector2(centerX - halfWidth, top),
            new Vector2(centerX + halfWidth, top + (DisplayHeight - 2f) * scale));
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Update(deltaSeconds);
        waitingSeconds = board.BallWaiting ? waitingSeconds + deltaSeconds : 0f;
        PushTrails(deltaSeconds);
        React(context);
        if (!board.GameOver)
        {
            return;
        }

        resultDelay -= context.DeltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(Outcome(context.Session));
    }

    private GameOutcome Outcome(GameSession session) =>
        new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Pinball.Jackpots, GameNumber.Label(board.Jackpots))
            .WithStat(L.Pinball.Ramps, GameNumber.Label(board.Ramps))
            .WithStat(L.Pinball.Multiballs, GameNumber.Label(board.Multiballs))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)session.PlaySeconds));

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing || board.GameOver)
        {
            pressMode = PressMode.None;
            keyPulling = false;
            board.SetFlippers(false, false);
            return;
        }

        var left = GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow) || GameInput.Held(ImGuiKey.LeftShift);
        var right = GameInput.Held(ImGuiKey.D, ImGuiKey.RightArrow) || GameInput.Held(ImGuiKey.RightShift);
        HandlePointer(context, ref left, ref right);
        HandlePlungerKeys(context);
        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            Nudge();
        }

        if ((left && !leftWasHeld) || (right && !rightWasHeld))
        {
            UiFeedback.Play(UiSound.GamePiece);
        }

        leftWasHeld = left;
        rightWasHeld = right;
        board.SetFlippers(left, right);
    }

    private void HandlePointer(in GameContext context, ref bool left, ref bool right)
    {
        var view = camera.View;
        var mouse = ImGui.GetMousePos();
        PressSurface.Claim(TableSurfaceId, view, out var activated);
        if (activated && !context.ChromeHit(mouse))
        {
            pressStart = mouse;
            pressMode = PlungerZone(mouse) && board.BallWaiting
                ? PressMode.Plunger
                : camera.ToWorld(mouse).X < PinballTable.Width * 0.5f
                    ? PressMode.LeftFlipper
                    : PressMode.RightFlipper;
        }

        if (UiInteract.Hover(view.Min, view.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Right) &&
            !context.ChromeHit(mouse))
        {
            Nudge();
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (pressMode == PressMode.Plunger)
            {
                Release();
            }

            pressMode = PressMode.None;
            return;
        }

        switch (pressMode)
        {
            case PressMode.LeftFlipper:
                left = true;
                return;
            case PressMode.RightFlipper:
                right = true;
                return;
            case PressMode.Plunger:
                board.SetPlungerPull((mouse.Y - pressStart.Y) / MathF.Max(1f, camera.Px(PlungerDragUnits)));
                return;
        }
    }

    private bool PlungerZone(Vector2 mouse)
    {
        var world = camera.ToWorld(mouse);
        return world.X > PinballTable.LaneInnerX - 0.15f && world.Y > 8f;
    }

    private void HandlePlungerKeys(in GameContext context)
    {
        var held = GameInput.Held(ImGuiKey.Space, ImGuiKey.DownArrow);
        if (!board.BallWaiting)
        {
            if (keyPulling && !held)
            {
                keyPulling = false;
            }

            if (!keyPulling && GameInput.Pressed(ImGuiKey.Space))
            {
                Nudge();
            }

            return;
        }

        if (!keyPulling)
        {
            keyPulling = GameInput.Pressed(ImGuiKey.Space, ImGuiKey.DownArrow);
            return;
        }

        if (held)
        {
            board.PullPlunger(context.RawDeltaSeconds);
            return;
        }

        keyPulling = false;
        Release();
    }

    private void Release()
    {
        var pull = board.PlungerPull;
        if (!board.ReleasePlunger())
        {
            return;
        }

        plungerKick = 0.4f + pull;
    }

    private void Nudge()
    {
        if (!board.Nudge())
        {
            return;
        }

        camera.Shake(0.22f);
        UiFeedback.Play(UiSound.GameHitWood);
    }

    private void PushTrails(float deltaSeconds)
    {
        for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
        {
            var state = board.BallStateAt(slot);
            if (state is not BallState.Rolling)
            {
                ribbons[slot].Release();
                continue;
            }

            ribbons[slot].Claim(board.BallIdAt(slot));
            if (deltaSeconds > 0f)
            {
                ribbons[slot].Push(board.BallPositionAt(slot));
            }
        }
    }

    private void ClearRibbons()
    {
        for (var slot = 0; slot < ribbons.Length; slot++)
        {
            ribbons[slot].Release();
        }
    }

    private void React(in GameContext context)
    {
        for (var index = 0; index < board.EventCount; index++)
        {
            var happened = board.Event(index);
            switch (happened.Kind)
            {
                case PinballEventKind.Launch:
                    OnLaunch(happened);
                    break;
                case PinballEventKind.Bumper:
                    OnBumper(happened);
                    break;
                case PinballEventKind.Sling:
                    UiFeedback.Play(UiSound.GameHitWood);
                    particles.Emit(SlingSparks, happened.Position, 8);
                    camera.Shake(0.05f);
                    break;
                case PinballEventKind.Target:
                    OnTarget(happened);
                    break;
                case PinballEventKind.BankDown:
                    OnBankDown(happened, context);
                    break;
                case PinballEventKind.Spinner:
                    UiFeedback.Play(UiSound.GameTick);
                    particles.Emit(SpinnerSparks, happened.Position, 2);
                    break;
                case PinballEventKind.TopLane:
                    UiFeedback.Play(UiSound.GamePop);
                    particles.Emit(LaneStars, happened.Position, 6);
                    break;
                case PinballEventKind.LanesComplete:
                    OnLanesComplete(happened, context);
                    break;
                case PinballEventKind.Inlane:
                    UiFeedback.Play(UiSound.GameTick);
                    break;
                case PinballEventKind.Outlane:
                    UiFeedback.Play(UiSound.GameWrong);
                    context.Fx.Vignette(Danger, 0.18f, 0.5f);
                    break;
                case PinballEventKind.RampMade:
                    OnRampMade(happened, context);
                    break;
                case PinballEventKind.Jackpot:
                    OnJackpot(happened, context);
                    break;
                case PinballEventKind.JackpotLit:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    ShowBanner(Loc.T(L.Pinball.JackpotLit), PinballRenderer.Ember, BannerMedium);
                    context.Fx.Flash(PinballRenderer.Ember, 0.14f);
                    break;
                case PinballEventKind.JackpotRaised:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    ShowBanner(Loc.T(L.Pinball.JackpotRaised), PinballRenderer.Ember, BannerMedium);
                    display.Award(happened.Value, false);
                    break;
                case PinballEventKind.Locked:
                    OnLocked(happened, context);
                    break;
                case PinballEventKind.Multiball:
                    OnMultiball(happened, context);
                    break;
                case PinballEventKind.SaucerHold:
                    UiFeedback.Play(UiSound.GameCollect);
                    particles.Emit(LockRings, happened.Position, 2);
                    break;
                case PinballEventKind.SaucerKick:
                    UiFeedback.Play(UiSound.GameShoot);
                    camera.Shake(0.12f);
                    break;
                case PinballEventKind.SkillShot:
                    OnSkillShot(happened, context);
                    break;
                case PinballEventKind.BallSaved:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    ShowBanner(Loc.T(L.Pinball.BallSaved), PinballRenderer.Cyan, BannerHigh);
                    context.Fx.Flash(PinballRenderer.Cyan, 0.16f);
                    break;
                case PinballEventKind.ExtraBall:
                    OnExtraBall(context);
                    break;
                case PinballEventKind.Drain:
                    OnDrain(happened, context);
                    break;
                case PinballEventKind.Bonus:
                    OnBonus(happened);
                    break;
                case PinballEventKind.ShootAgain:
                    UiFeedback.Play(UiSound.GamePowerUp);
                    ShowBanner(Loc.T(L.Pinball.ShootAgain), PinballRenderer.Ember, BannerHigh);
                    break;
                case PinballEventKind.NewBall:
                    ShowBanner(ballLabel.Get(L.Pinball.BallNumber, happened.Value), White, BannerMedium);
                    break;
                case PinballEventKind.TiltWarning:
                    UiFeedback.Play(UiSound.GameWrong);
                    ShowBanner(warningLabel.Get(L.Pinball.TiltWarning, happened.Value), Danger, BannerHigh);
                    context.Fx.Vignette(Danger, 0.3f, 0.6f);
                    display.Alert();
                    break;
                case PinballEventKind.Tilt:
                    OnTilt(context);
                    break;
                case PinballEventKind.FlipperHit:
                    UiFeedback.Play(UiSound.GameHitSoft);
                    break;
            }
        }
    }

    private void OnLaunch(in PinballEvent happened)
    {
        UiFeedback.Play(UiSound.GameShoot);
        particles.Emit(LaunchStreaks, happened.Position + new Vector2(0f, PinballTable.BallRadius), 10);
        camera.Shake(0.08f + 0.12f * MathF.Min(1f, happened.Value / PinballBoard.MaxLaunchSpeed));
    }

    private void OnBumper(in PinballEvent happened)
    {
        UiFeedback.Play(UiSound.GamePop);
        particles.Emit(BumperSparks, happened.Position, 10);
        var center = camera.ToScreen(PinballTable.Bumpers[happened.Index]);
        fx.Shockwave(center, camera.Px(PinballTable.BumperRadius * 1.9f), Ring, 0.3f, 2.4f,
            camera.Px(PinballTable.BumperRadius));
        fx.AddText(GameNumber.Signed(happened.Value), center - new Vector2(0f, camera.Px(0.3f)),
            PinballRenderer.Magenta, 0.85f, 34f);
        camera.Shake(0.08f);
        camera.Punch(0.012f);
    }

    private void OnTarget(in PinballEvent happened)
    {
        if (happened.Value <= 0)
        {
            return;
        }

        UiFeedback.Play(UiSound.GameBreak);
        var color = PinballRenderer.BankColor(happened.Index / PinballTable.BankSize);
        particles.Burst(happened.Position, 10, color, 3.2f, 0.06f, 0.5f, 6f, MathF.PI * 2f, 0f, ParticleShape.Shard);
        fx.AddText(GameNumber.Signed(happened.Value), camera.ToScreen(happened.Position), color, 0.9f);
        fx.HitStop(0.03f);
        camera.Shake(0.12f);
    }

    private void OnBankDown(in PinballEvent happened, in GameContext context)
    {
        GameSfx.LevelClear();
        var color = PinballRenderer.BankColor(happened.Index);
        var screen = camera.ToScreen(happened.Position);
        fx.Shockwave(screen, camera.Px(1.2f), color, 0.5f, 3.2f);
        fx.AddText(GameNumber.Signed(happened.Value), screen - new Vector2(0f, camera.Px(0.4f)), color, 1.2f);
        particles.Burst(happened.Position, 22, color, 4f, 0.07f, 0.7f, 5f, MathF.PI * 2f, 0f, ParticleShape.Star);
        display.Award(happened.Value, false);
        context.Fx.Punch(0.04f);
    }

    private void OnLanesComplete(in PinballEvent happened, in GameContext context)
    {
        GameSfx.ComboTierUp();
        for (var lane = 0; lane < PinballTable.TopLaneCount; lane++)
        {
            particles.Emit(LaneStars, PinballTable.TopLanes[lane], 10);
        }

        ShowBanner(multiplierLabel.Get(L.Pinball.BonusMultiplier, happened.Value), PinballRenderer.Cyan, BannerMedium);
        context.Fx.Sweep();
        context.Fx.Punch(0.03f);
    }

    private void OnRampMade(in PinballEvent happened, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameCollect);
        var screen = camera.ToScreen(happened.Position);
        particles.Emit(RampStars, happened.Position, 16);
        fx.Shockwave(screen, camera.Px(0.9f), PinballRenderer.Cyan, 0.45f, 3f);
        fx.AddText(GameNumber.Signed(happened.Value), screen - new Vector2(0f, camera.Px(0.35f)), PinballRenderer.Cyan,
            1.1f);
        display.Award(happened.Value, false);
        context.Fx.Punch(0.035f);
        camera.Shake(0.1f);
    }

    private void OnJackpot(in PinballEvent happened, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameClear);
        GameSfx.ComboTierUp();
        var screen = camera.ToScreen(happened.Position);
        particles.Emit(JackpotGlow, happened.Position, 30);
        particles.Confetti(happened.Position, ConfettiCount, CelebrationPalette, 5f, 0.07f, 1.4f, 7f);
        fx.Shockwave(screen, camera.Px(2.6f), PinballRenderer.Ember, 0.7f, 4f);
        fx.Shockwave(screen, camera.Px(1.6f), White, 0.5f, 3f);
        fx.AddText(GameNumber.Label(happened.Value), screen - new Vector2(0f, camera.Px(0.5f)), PinballRenderer.Ember,
            1.5f);
        ShowBanner(Loc.T(L.Pinball.Jackpot), PinballRenderer.Ember, BannerHigh);
        display.Award(happened.Value, true);
        context.Fx.SlowMo(0.45f, 0.5f);
        context.Fx.Flash(PinballRenderer.Ember, 0.35f);
        context.Fx.Sweep();
        context.Fx.Punch(0.08f);
        camera.Shake(0.4f);
    }

    private void OnLocked(in PinballEvent happened, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        particles.Emit(LockRings, PinballTable.Saucer, 4);
        fx.Shockwave(camera.ToScreen(PinballTable.Saucer), camera.Px(0.8f), PinballRenderer.Violet, 0.5f, 3f);
        ShowBanner(lockLabel.Get(L.Pinball.LockNumber, happened.Index), PinballRenderer.Violet, BannerMedium);
        display.Award(happened.Value, false);
        context.Fx.Punch(0.04f);
    }

    private void OnMultiball(in PinballEvent happened, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameClear);
        GameSfx.ComboTierUp();
        particles.Confetti(PinballTable.Saucer, ConfettiCount, CelebrationPalette, 4.5f, 0.07f, 1.4f, 7f);
        fx.Shockwave(camera.ToScreen(PinballTable.Saucer), camera.Px(2.4f), PinballRenderer.Violet, 0.7f, 4f);
        ShowBanner(Loc.T(L.Pinball.Multiball), PinballRenderer.Violet, BannerHigh);
        display.Award(happened.Value, true);
        context.Fx.SlowMo(0.6f, 0.35f);
        context.Fx.Flash(PinballRenderer.Violet, 0.3f);
        context.Fx.Sweep();
        context.Fx.Punch(0.07f);
        camera.Shake(0.35f);
    }

    private void OnSkillShot(in PinballEvent happened, in GameContext context)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        particles.Emit(LaneStars, happened.Position, 24);
        fx.Shockwave(camera.ToScreen(happened.Position), camera.Px(1.4f), PinballRenderer.Cyan, 0.6f, 3.4f);
        ShowBanner(Loc.T(L.Pinball.SkillShot), PinballRenderer.Cyan, BannerHigh);
        display.Award(happened.Value, true);
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
    }

    private void OnExtraBall(in GameContext context)
    {
        GameSfx.LevelClear();
        particles.Confetti(new Vector2(PinballTable.Width * 0.5f, 6f), ConfettiCount, CelebrationPalette, 4f, 0.07f, 1.4f,
            7f);
        ShowBanner(Loc.T(L.Pinball.ExtraBall), PinballRenderer.Ember, BannerHigh);
        context.Fx.Flash(PinballRenderer.Ember, 0.25f);
        context.Fx.Sweep();
    }

    private void OnDrain(in PinballEvent happened, in GameContext context)
    {
        var position = new Vector2(happened.Position.X, PinballTable.DrainY);
        particles.Emit(DrainShards, position, 14);
        fx.Shockwave(camera.ToScreen(position), camera.Px(0.8f), Danger, 0.45f, 3f);
        if (board.LiveBalls > 0 || board.PendingLaunches > 0)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            return;
        }

        UiFeedback.Play(UiSound.GameWrong);
        context.Fx.Vignette(Danger, 0.4f, 0.8f);
        context.Fx.Flash(Danger, 0.2f);
        camera.Shake(0.3f);
    }

    private void OnBonus(in PinballEvent happened)
    {
        if (happened.Value <= 0)
        {
            return;
        }

        GameSfx.LevelClear();
        ShowBanner(Loc.T(L.Pinball.Bonus), White, BannerMedium);
        display.Award(happened.Value, true);
    }

    private void OnTilt(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameExplosion);
        ShowBanner(Loc.T(L.Pinball.Tilt), Danger, BannerHigh + 1);
        display.Alert();
        context.Fx.Flash(Danger, 0.4f);
        context.Fx.Vignette(Danger, 0.5f, 1.2f);
        camera.Shake(0.8f);
    }

    private void ShowBanner(string text, Vector4 color, int priority)
    {
        if (bannerProgress < 0.6f && priority < bannerPriority)
        {
            return;
        }

        bannerText = text;
        bannerColor = color;
        bannerPriority = priority;
        bannerProgress = 0f;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var table = PinballRenderer.TableRect(in camera);
        var accent = Accent;
        drawList.PushClipRect(table.Min - new Vector2(camera.Px(0.1f)), table.Max + new Vector2(camera.Px(0.1f)), true);
        PinballRenderer.DrawTable(drawList, in camera, board, accent, time, plungerKick);
        DrawBalls(drawList, accent, BallLayer.Playfield);
        PinballRenderer.DrawRamp(drawList, in camera, board, time);
        DrawBalls(drawList, accent, BallLayer.Ramp);
        drawList.PopClipRect();
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawBalls(ImDrawListPtr drawList, Vector4 accent, BallLayer layer)
    {
        var trail = GamePalette.Lighten(accent, 0.45f) with { W = 0.5f };
        for (var slot = 0; slot < PinballBoard.MaxBalls; slot++)
        {
            if (board.BallStateAt(slot) == BallState.None || board.BallLayerAt(slot) != layer)
            {
                continue;
            }

            ribbons[slot].Draw(drawList, in camera, trail, camera.Px(RibbonWidth), additive: true);
            var position = board.BallPositionAt(slot);
            if (board.BallStateAt(slot) == BallState.Lane && board.BallWaiting)
            {
                position.Y += board.PlungerPull * PinballRenderer.PlungerTravel;
            }

            PinballRenderer.DrawBall(drawList, in camera, position, accent, layer == BallLayer.Ramp);
        }
    }

    private void DrawPrompt(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        if (waitingSeconds < PromptDelaySeconds || context.Session.State != StageFlow.Playing)
        {
            return;
        }

        var text = Loc.T(L.Pinball.Launch);
        var style = TextStyles.FootnoteEmphasized;
        var size = Typography.Measure(text, style);
        var center = camera.ToScreen(PromptPoint);
        var half = size * 0.5f + new Vector2(14f, 7f) * scale;
        var pulse = 0.75f + 0.25f * MathF.Sin(time * 4f);
        Material.Frosted(drawList, center - half, center + half, half.Y, scale, 0.85f * pulse);
        Squircle.Stroke(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(Accent with { W = 0.6f }),
            MathF.Max(1f, 1.2f * scale));
        Typography.DrawCentered(drawList, center, text, context.Theme.TextStrong with { W = pulse }, style);
    }
}
