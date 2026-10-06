using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flap;

internal sealed class FlapApp : IMiniGame
{
    private const string GameId = "flap";
    private const float BirdScreenFraction = 0.3f;
    private const float FollowSmoothSeconds = 0.08f;
    private const float TiltSmoothSeconds = 0.05f;
    private const float RibbonWidth = 0.35f;
    private const float MedalBannerSeconds = 1.6f;
    private const float ReadyBobUnits = 0.32f;
    private const float ReadyBobHertz = 0.6f;
    private const float WingHertz = 2.8f;
    private const float FlapPulseDecay = 5f;
    private const float SkyPipes = 100f;
    private const float DuskProgress = 0.5f;
    private const float CrashSlowFactor = 0.5f;
    private const float CrashSlowSeconds = 0.35f;
    private const float AutopilotLead = 0.9f;
    private const ulong IdleSeed = 0x464C4150UL;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Flap, GameGenre.Arcade, L.Flap.Hook,
        Backdrop.Sky, HudStyle.Standard, ScoreKind.Score, clocked: true, keyboard: true);
    private static readonly LocString[] MedalNames = { L.Flap.Bronze, L.Flap.Silver, L.Flap.Gold };
    private static readonly Vector4[] MedalColors =
    {
        new(0.86f, 0.56f, 0.28f, 1f), new(0.86f, 0.88f, 0.94f, 1f), new(1f, 0.84f, 0.36f, 1f),
    };

    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.6f, 1f);
    private static readonly Vector4 Ember = new(0.98f, 0.7f, 0.3f, 1f);
    private static readonly Vector4 Flame = new(1f, 0.92f, 0.6f, 1f);
    private static readonly Vector4 Ring = new(1f, 0.8f, 0.4f, 1f);
    private static readonly ParticleSpec FlapPuff = new(White with { W = 0.85f }, White with { W = 0f }, 0.05f, 1.9f,
        0.4f, 2.5f, 1.6f, 2f, 1.1f, 2.5f);
    private static readonly ParticleSpec ScoreSparkle = new(Spark, Spark, 0.05f, 2.7f, 0.7f, 0.8f, 2.4f, 6f,
        shape: ParticleShape.Star);
    private static readonly ParticleSpec CrashEmbers = new(Ember, Ember, 0.083f, 5.8f, 0.8f, 8.75f, 1.6f, 12f);
    private static readonly ParticleSpec CrashStreaks = new(Flame, Flame, 0.054f, 8.75f, 0.5f, 4.6f, 1.6f, 12f,
        shape: ParticleShape.Streak);
    private static readonly ParticleSpec CrashChips = new(White with { W = 0.9f }, White with { W = 0.9f }, 0.0625f,
        3.3f, 0.6f, 4.2f, 1.6f, 12f, shape: ParticleShape.Square);
    private static readonly ParticleSpec[] MedalSparkles = BuildMedalSparkles();

    private readonly FlapBoard board = new();
    private readonly FlapBoard idleBoard = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private Camera2D camera = Camera2D.Create();
    private Spring tilt = new(0f);
    private string medalBanner = string.Empty;
    private float bannerProgress = 1f;
    private float flapPulse;
    private float time;
    private int medal;
    private bool finished;
    private bool cameraPlaced;
    private bool idleReady;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        ribbon.Clear();
        tilt.SnapTo(0f);
        medalBanner = string.Empty;
        bannerProgress = 1f;
        flapPulse = 0f;
        medal = 0;
        finished = false;
        cameraPlaced = false;
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
        if (!idleReady || idleBoard.State == FlapState.Over)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed));
            idleBoard.Flap();
            idleReady = true;
            cameraPlaced = false;
        }

        var raw = context.RawDeltaSeconds;
        time += raw;
        Autopilot(idleBoard);
        idleBoard.Step(raw);
        PlaceCamera(context, idleBoard, raw);
        UpdateTilt(idleBoard, raw);
        context.Backdrop.SetSky(SkyProgress(idleBoard));
        DrawWorld(ImGui.GetWindowDrawList(), context.Full, idleBoard, idleBoard.BirdY, false, UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(raw);
        fx.Update(raw);
        time += raw;
        flapPulse = MathF.Max(0f, flapPulse - raw * FlapPulseDecay);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, MedalBannerSeconds);
        PlaceCamera(context, board, context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context);
        }

        UpdateTilt(board, context.DeltaSeconds);
        context.Backdrop.SetSky(SkyProgress(board));
        var displayY = board.BirdY;
        if (board.State == FlapState.Ready)
        {
            displayY += MathF.Sin(time * MathF.Tau * ReadyBobHertz) * ReadyBobUnits;
        }

        DrawWorld(drawList, context.Full, board, displayY, true, scale);
        GameBanner.Draw(drawList, new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f),
            medalBanner, MedalColor(medal), context.Theme, bannerProgress);
        context.Hud.Score(board.Score);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context, FlapBoard target, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var full = context.Full;
        var view = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        camera.Fit(view, FlapBoard.WorldWidth, FlapBoard.WorldHeight, FitMode.CoverHeight);
        var lead = new Vector2((0.5f - BirdScreenFraction) * camera.Units(view.Width), 0f);
        var focus = new Vector2(target.BirdX, FlapBoard.WorldHeight * 0.5f);
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

    private void UpdateTilt(FlapBoard target, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var velocityFraction = target.BirdVelocity / (2f * FlapBoard.WorldHeight);
        tilt.Step(Math.Clamp(velocityFraction * 1.3f, -0.5f, 1.15f), TiltSmoothSeconds, deltaSeconds);
    }

    private static float SkyProgress(FlapBoard target) =>
        DuskProgress * Math.Clamp(target.BirdX / (SkyPipes * FlapBoard.PipeSpacing), 0f, 1f);

    private static void Autopilot(FlapBoard target)
    {
        if (!target.TryNextGap(out var gapCenter))
        {
            return;
        }

        if (target.BirdY > gapCenter + AutopilotLead && target.BirdVelocity > -2f)
        {
            target.Flap();
        }
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        if (board.State == FlapState.Playing && deltaSeconds > 0f)
        {
            ribbon.Push(new Vector2(board.BirdX - FlapBoard.BirdRadius * 0.6f, board.BirdY));
        }

        if (board.ScoredThisStep)
        {
            OnScore(context);
        }

        if (board.DiedThisStep)
        {
            OnCrash(context);
        }

        if (board.State != FlapState.Over)
        {
            return;
        }

        finished = true;
        Finish(context);
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing || board.State is FlapState.Dying or FlapState.Over)
        {
            return;
        }

        var full = context.Full;
        var hitMin = new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * UiScale.Current);
        var tapped = ImGui.IsMouseClicked(ImGuiMouseButton.Left) && UiInteract.Hover(hitMin, full.Max);
        var pressed = GameInput.Pressed(ImGuiKey.Space) || GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow);
        if (!tapped && !pressed)
        {
            return;
        }

        board.Flap();
        UiFeedback.Play(UiSound.GameJump);
        flapPulse = 1f;
        particles.Emit(FlapPuff, new Vector2(board.BirdX - FlapBoard.BirdRadius * 0.6f, board.BirdY + FlapBoard.BirdRadius * 0.5f), 6);
    }

    private void OnScore(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameCollect);
        var bird = new Vector2(board.BirdX, board.BirdY);
        fx.Shockwave(camera.ToScreen(bird), camera.Px(1.1f), White with { W = 0.9f }, 0.42f, 2.6f);
        particles.Emit(ScoreSparkle, bird, 8);
        var tier = FlapBoard.MedalTier(board.Score);
        if (tier <= medal)
        {
            return;
        }

        medal = tier;
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Flash(MedalColor(tier), 0.18f);
        context.Fx.Punch(0.05f);
        particles.Emit(in MedalSparkles[tier - 1], bird, 18);
        medalBanner = Loc.T(L.Flap.MedalEarned, Loc.T(MedalNames[tier - 1]));
        bannerProgress = 0f;
    }

    private void OnCrash(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameBreak);
        var bird = new Vector2(board.BirdX, board.BirdY);
        context.Fx.Flash(Danger, 0.5f);
        camera.Shake(0.6f);
        fx.Shockwave(camera.ToScreen(bird), camera.Px(2.5f), Ring, 0.6f, 3.4f);
        particles.Emit(CrashEmbers, bird, 26);
        particles.Emit(CrashStreaks, bird, 12);
        particles.Emit(CrashChips, bird, 10);
        if (board.State == FlapState.Dying)
        {
            context.Fx.SlowMo(CrashSlowFactor, CrashSlowSeconds);
        }
    }

    private void Finish(in GameContext context)
    {
        var outcome = new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.PlaySeconds))
            .WithStat(L.Flap.Flaps, GameNumber.Label(board.Flaps));
        if (medal > 0)
        {
            outcome = outcome.WithStat(L.Flap.Medal, Loc.T(MedalNames[medal - 1]));
        }

        context.Session.Finish(outcome);
    }

    private void DrawWorld(ImDrawListPtr drawList, Rect full, FlapBoard target, float displayY, bool live, float scale)
    {
        drawList.PushClipRect(full.Min, full.Max, true);
        FlapRenderer.DrawGround(drawList, in camera);
        FlapRenderer.DrawPipes(drawList, in camera, target, scale);
        if (live)
        {
            ribbon.Draw(drawList, in camera, Flame with { W = 0.55f }, camera.Px(RibbonWidth), additive: true);
        }

        var center = camera.ToScreen(new Vector2(target.BirdX, displayY));
        var radius = camera.Px(FlapBoard.BirdRadius) * (1f + 0.14f * flapPulse);
        FlapRenderer.DrawBird(drawList, center, radius, tilt.Value, time * MathF.Tau * WingHertz, scale);
        if (live)
        {
            particles.Draw(drawList, in camera);
            fx.DrawRings(drawList, scale);
            fx.DrawText();
        }

        drawList.PopClipRect();
    }

    private static Vector4 MedalColor(int tier) => tier <= 0 ? White : MedalColors[tier - 1];

    private static ParticleSpec[] BuildMedalSparkles()
    {
        var specs = new ParticleSpec[MedalColors.Length];
        for (var index = 0; index < specs.Length; index++)
        {
            specs[index] = new ParticleSpec(MedalColors[index], White, 0.07f, 4.5f, 0.9f, 1.2f, 2.2f, 6f,
                shape: ParticleShape.Star, additive: true);
        }

        return specs;
    }
}
