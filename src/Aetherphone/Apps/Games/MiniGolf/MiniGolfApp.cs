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

namespace Aetherphone.Apps.Games.MiniGolf;

internal enum GolfStage : byte
{
    Playing,
    HoleDone,
    Scorecard,
}

internal sealed class MiniGolfApp : IMiniGame
{
    internal const string NineStatId = "minigolf.nine";
    private const string GameId = "minigolf";
    private const string AimSurfaceId = "minigolf.aim";
    private const string PlaceSeparator = " · ";
    private const int NineMode = 1;
    private const int IdleHole = 6;
    private const ulong IdleSeed = 0x474F4C46UL;
    private const ulong HoleSeedStep = 7919UL;
    private const float MaxPullPixels = 150f;
    private const float MinPower = 0.04f;
    private const float HoleDoneSeconds = 1.7f;
    private const float BannerSeconds = 1.5f;
    private const float TrailSpeed = 2.2f;
    private const float TrailWidth = 0.13f;
    private const float EntranceSpeed = 2.4f;
    private const float ScorecardSpeed = 2.8f;
    private const float FlashDecay = 4f;
    private const float IdleShotSeconds = 1.6f;
    private const float IdleResetSeconds = 2.2f;
    private const float BannerHeight = 0.3f;
    private static readonly LocString[] Modes = { L.MiniGolf.EighteenHoles, L.MiniGolf.NineHoles };
    private static readonly string[] ModeStatIds = { GameId, NineStatId };
    private static readonly bool[] UnrankedModes = { false, true };
    private static readonly LocString[] Places = { L.MiniGolf.FirstPlace, L.MiniGolf.SecondPlace, L.MiniGolf.ThirdPlace, L.MiniGolf.FourthPlace };
    private static readonly GameSpec StageSpec = new(GameId, L.MiniGolf.Title, GameGenre.Arcade, L.MiniGolf.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Count, Modes, ModeStatIds, clocked: true,
        seats: MiniGolfRound.MaxPlayers, unit: L.MiniGolf.Strokes, unrankedModes: UnrankedModes);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Dust = new(0.86f, 0.80f, 0.62f, 0.9f);
    private static readonly Vector4[] AcePalette =
    {
        new(1f, 0.84f, 0.30f, 1f), new(0.98f, 0.72f, 0.18f, 1f), new(1f, 0.93f, 0.62f, 1f),
        new(0.42f, 0.86f, 0.52f, 1f), new(1f, 1f, 1f, 1f), new(0.40f, 0.78f, 1f, 1f),
    };
    private static readonly ParticleSpec GrassBits = new(MiniGolfRenderer.Grass, MiniGolfRenderer.Fairway with { W = 0f },
        0.05f, 2.2f, 0.45f, 0f, 3f, 8f, shape: ParticleShape.Square);
    private static readonly ParticleSpec WallDust = new(White with { W = 0.8f }, White with { W = 0f }, 0.06f, 1.6f,
        0.35f, 0f, 3f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);
    private static readonly ParticleSpec SandPuff = new(Dust, MiniGolfRenderer.SandTint with { W = 0f }, 0.1f, 1.4f,
        0.6f, 0f, 2.6f, shape: ParticleShape.GlowCircle, curve: SizeCurve.Grow);
    private static readonly ParticleSpec Droplets = new(White, MiniGolfRenderer.WaterTint with { W = 0f }, 0.06f, 3.2f,
        0.6f, 0f, 2.2f);
    private static readonly ParticleSpec CupSparkle = new(White, new Vector4(1f, 0.84f, 0.3f, 0f), 0.08f, 2.6f, 0.8f,
        0f, 2f, 6f, shape: ParticleShape.Star, additive: true);
    private readonly MiniGolfBoard board = new();
    private readonly MiniGolfRound round = new();
    private readonly ParticleSystem particles = new(320);
    private readonly ParticleSystem celebration = new(160);
    private readonly FeedbackFx fx = new();
    private readonly Ribbon trail = new();
    private readonly Vector2[] aimPath = new Vector2[MiniGolfRenderer.MaxAimPoints];
    private Camera2D camera = Camera2D.Create();
    private LabelPairSlot holeLabel;
    private LabelPairSlot holeOfLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private Vector2 dragStart;
    private Vector2 flashPoint;
    private ulong seed = IdleSeed;
    private GolfStage stage;
    private int aimPathCount;
    private int playedHole;
    private float power;
    private float bannerProgress = 1f;
    private float stageSeconds;
    private float entrance = 1f;
    private float scorecardAppear;
    private float bumpFlash;
    private float idleSeconds;
    private float time;
    private bool dragging;
    private bool finalCard;
    private bool finished;

    public MiniGolfApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        seed = start.Seed;
        round.Reset(start.Mode == NineMode ? MiniGolfCourse.FrontNine : MiniGolfCourse.HoleCount, start.Seats);
        finished = false;
        finalCard = false;
        bannerProgress = 1f;
        camera = Camera2D.Create();
        particles.Clear();
        particles.Reseed(start.Seed);
        celebration.Clear();
        fx.Clear();
        LoadHole();
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        celebration.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        particles.Update(deltaSeconds);
        fx.Update(deltaSeconds);
        entrance = GameJuice.Advance(entrance, deltaSeconds, EntranceSpeed);
        PlaceCamera(context);
        board.BeginFrame();
        idleSeconds += deltaSeconds;
        if (board.CanShoot && idleSeconds >= IdleShotSeconds)
        {
            PuttTowardCup();
            idleSeconds = 0f;
        }

        board.Step(deltaSeconds);
        React(context, false);
        if (board.Phase == GolfPhase.Holed || board.Strokes >= MiniGolfBoard.MaxStrokes)
        {
            stageSeconds += deltaSeconds;
            if (stageSeconds >= IdleResetSeconds)
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
        var rawSeconds = context.RawDeltaSeconds;
        time += rawSeconds;
        particles.Update(rawSeconds);
        celebration.Update(rawSeconds);
        fx.Update(rawSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, rawSeconds, BannerSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds, EntranceSpeed);
        bumpFlash = MathF.Max(0f, bumpFlash - rawSeconds * FlashDecay);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            Step(simDelta, context, scale);
        }

        DrawWorld(drawList, scale);
        if (dragging && aimPathCount > 1)
        {
            MiniGolfRenderer.DrawAim(drawList, in camera, aimPath.AsSpan(0, aimPathCount), board.BallRenderPosition,
                ImGui.GetMousePos(), power, time);
        }

        GameBanner.Draw(drawList, BannerCenter(context.Safe), bannerText, bannerColor, context.Theme, bannerProgress);
        if (stage == GolfStage.Scorecard && !finished)
        {
            DrawScorecard(drawList, context, scale);
        }

        celebration.Draw(drawList, scale);
        FillHud(drawList, context, scale);
        context.Session.Report(board.Strokes);
    }

    private void BuildIdle()
    {
        seed = IdleSeed;
        round.Reset(MiniGolfCourse.HoleCount, 1);
        board.Load(MiniGolfCourse.Get(IdleHole), GameRandom.FromSeed(IdleSeed));
        trail.Clear();
        idleSeconds = 0f;
        stageSeconds = 0f;
        stage = GolfStage.Playing;
        dragging = false;
        aimPathCount = 0;
    }

    private void LoadHole()
    {
        board.Load(MiniGolfCourse.Get(round.Hole), GameRandom.FromSeed(seed + (ulong)round.Hole * HoleSeedStep));
        stage = GolfStage.Playing;
        stageSeconds = 0f;
        entrance = 0f;
        dragging = false;
        aimPathCount = 0;
        trail.Clear();
        ShowBanner(holeLabel.Get(L.MiniGolf.HolePar, round.Hole + 1, board.Hole.Par), Accent);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, board.Hole.Bounds, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context, float scale)
    {
        switch (stage)
        {
            case GolfStage.Playing:
                HandleAim(context, scale);
                board.Step(deltaSeconds);
                React(context, true);
                if (board.Phase == GolfPhase.Holed ||
                    (board.Phase == GolfPhase.Aiming && board.Strokes >= MiniGolfBoard.MaxStrokes))
                {
                    EndHole(context);
                }

                return;
            case GolfStage.HoleDone:
                board.Step(deltaSeconds);
                React(context, true);
                stageSeconds += context.DeltaSeconds;
                if (stageSeconds >= HoleDoneSeconds)
                {
                    AfterHole(context);
                }

                return;
            default:
                board.Step(deltaSeconds);
                return;
        }
    }

    private void HandleAim(in GameContext context, float scale)
    {
        var mouse = ImGui.GetMousePos();
        if (context.Session.State != StageFlow.Playing || context.Session.HandoffPending || !board.CanShoot)
        {
            dragging = false;
            aimPathCount = 0;
            return;
        }

        var full = context.Full;
        var surface = new Rect(new Vector2(full.Min.X, full.Min.Y + StageLayout.ChromeBand * scale), full.Max);
        PressSurface.Claim(AimSurfaceId, surface, out var activated);
        if (activated && !context.ChromeHit(mouse))
        {
            dragging = true;
            dragStart = mouse;
        }

        if (!dragging)
        {
            return;
        }

        var pull = mouse - dragStart;
        power = Math.Clamp(pull.Length() / (MaxPullPixels * scale), 0f, 1f);
        var direction = camera.ToWorld(dragStart) - camera.ToWorld(mouse);
        aimPathCount = power >= MinPower ? board.PredictAim(direction, power, aimPath) : 0;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        dragging = false;
        aimPathCount = 0;
        if (power >= MinPower)
        {
            board.Shoot(direction, power);
        }
    }

    private void PuttTowardCup()
    {
        var toCup = board.Hole.Cup - board.BallPosition;
        var distance = toCup.Length();
        var low = 0f;
        var high = 1f;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            var middle = (low + high) * 0.5f;
            if (MiniGolfBoard.RollDistance(middle * MiniGolfBoard.MaxShotSpeed) < distance * 1.05f)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        board.Shoot(toCup, high);
    }

    private void EndHole(in GameContext context)
    {
        var holed = board.Phase == GolfPhase.Holed;
        var strokes = holed ? board.Strokes : MiniGolfBoard.MaxStrokes;
        round.Record(strokes);
        playedHole = round.Hole;
        stage = GolfStage.HoleDone;
        stageSeconds = 0f;
        dragging = false;
        aimPathCount = 0;
        if (!holed)
        {
            ShowBanner(Loc.T(L.MiniGolf.PickedUp), MiniGolfScorecard.ResultColor(HoleResult.Worse));
            UiFeedback.Play(UiSound.GameWrong);
            return;
        }

        var par = board.Hole.Par;
        var result = MiniGolfRound.Classify(strokes, par);
        var color = MiniGolfScorecard.ResultColor(result);
        ShowBanner(ResultText(result, strokes - par), color);
        var cup = board.Hole.Cup;
        particles.Emit(CupSparkle, cup, 18 + (result <= HoleResult.Birdie ? 18 : 0));
        fx.Shockwave(camera.ToScreen(cup), camera.Px(1.4f), color, 0.55f, 3.2f);
        if (result == HoleResult.HoleInOne)
        {
            UiFeedback.Play(UiSound.GameWin);
            celebration.Confetti(new Vector2(context.Full.Center.X, context.Full.Min.Y + context.Full.Height * 0.3f), 110,
                AcePalette, 320f * UiScale.Current, 4.2f, 1.6f);
            context.Fx.Flash(new Vector4(1f, 0.86f, 0.4f, 1f), 0.35f);
            context.Fx.Punch(0.1f);
            context.Fx.Sweep();
            return;
        }

        GameSfx.LevelClear();
        if (result > HoleResult.Birdie)
        {
            return;
        }

        context.Fx.Punch(0.05f);
        context.Fx.Sweep();
    }

    private void AfterHole(in GameContext context)
    {
        var step = round.Advance();
        if (step == RoundStep.NextPlayer)
        {
            LoadHole();
            context.Session.Handoff(round.Player);
            return;
        }

        stage = GolfStage.Scorecard;
        scorecardAppear = 0f;
        finalCard = step == RoundStep.Finished;
        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private void DrawScorecard(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        scorecardAppear = MathF.Min(1f, scorecardAppear + context.RawDeltaSeconds * ScorecardSpeed);
        var subtitle = holeOfLabel.Get(L.MiniGolf.HoleOf, playedHole + 1, round.Holes);
        var pressed = MiniGolfScorecard.Draw(drawList, context.Safe, round, playedHole, subtitle, finalCard, Accent,
            context.Theme, scorecardAppear, scale);
        if (context.Session.State != StageFlow.Playing || context.Session.HandoffPending || scorecardAppear < 0.6f)
        {
            return;
        }

        if (!pressed && !GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            return;
        }

        if (finalCard)
        {
            FinishRound(context);
            return;
        }

        LoadHole();
        if (round.Players > 1)
        {
            context.Session.Handoff(0);
        }
    }

    private void FinishRound(in GameContext context)
    {
        finished = true;
        if (round.Players > 1)
        {
            Span<int> order = stackalloc int[MiniGolfRound.MaxPlayers];
            var count = round.Standings(order);
            var winner = round.Winner();
            var outcome = winner >= 0 ? GameOutcome.Unranked().WithWinner(winner) : GameOutcome.Drawn(GameId);
            for (var place = 0; place < count; place++)
            {
                var player = order[place];
                outcome = outcome.WithStat(Places[place],
                    string.Concat(GameSeats.Name(player), PlaceSeparator, GameNumber.Label(round.Total(player))));
            }

            context.Session.Finish(outcome);
            return;
        }

        var total = round.Total(0);
        var solo = round.Holes < MiniGolfCourse.HoleCount
            ? GameOutcome.Unranked()
            : new GameOutcome(total, ScoreKind.Count, GameId);
        context.Session.Finish(solo
            .WithStat(L.MiniGolf.Strokes, GameNumber.Label(total))
            .WithStat(L.MiniGolf.ToPar, MiniGolfScorecard.ToParLabel(round.ToPar(0)))
            .WithStat(L.MiniGolf.Birdies, GameNumber.Label(round.Count(0, HoleResult.Birdie)))
            .WithStat(L.MiniGolf.HolesInOne, GameNumber.Label(round.Count(0, HoleResult.HoleInOne))));
    }

    private void React(in GameContext context, bool live)
    {
        var events = board.Events;
        if (events == GolfEvents.None)
        {
            return;
        }

        var point = board.EventPoint;
        if ((events & GolfEvents.Shot) != 0)
        {
            var direction = Vector2.Normalize(board.BallVelocity + new Vector2(0.0001f, 0f));
            particles.Emit(GrassBits.WithDirection(MathF.Atan2(-direction.Y, -direction.X), 1.4f), point, 8);
            trail.Clear();
            if (live)
            {
                UiFeedback.Play(UiSound.GameHitWood);
                camera.Shake(0.03f + 0.06f * board.ImpactStrength);
                context.Fx.Punch(0.01f + 0.025f * board.ImpactStrength);
            }
        }

        if ((events & GolfEvents.Wall) != 0)
        {
            particles.Emit(WallDust, point, 5);
            if (live && board.ImpactStrength > 2f)
            {
                UiFeedback.Play(UiSound.GameHitSoft);
                camera.Shake(MathF.Min(0.2f, board.ImpactStrength * 0.025f));
            }
        }

        if ((events & GolfEvents.Post) != 0)
        {
            bumpFlash = 1f;
            flashPoint = point;
            fx.Shockwave(camera.ToScreen(point), camera.Px(0.9f), MiniGolfRenderer.BallWhite, 0.35f, 2.4f);
            if (live)
            {
                UiFeedback.Play(UiSound.GamePop);
                context.Fx.Punch(0.03f);
            }
        }

        if ((events & GolfEvents.Mill) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameHitWood);
            camera.Shake(0.15f);
        }

        if ((events & GolfEvents.Tunnel) != 0)
        {
            trail.Clear();
            fx.Shockwave(camera.ToScreen(board.TunnelFrom), camera.Px(0.8f), White, 0.3f, 2f);
            fx.Shockwave(camera.ToScreen(point), camera.Px(1f), Accent, 0.4f, 2.6f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameJump);
            }
        }

        if ((events & GolfEvents.Sand) != 0)
        {
            particles.Emit(SandPuff, point, 10);
        }

        if ((events & GolfEvents.Splash) != 0)
        {
            particles.Emit(Droplets, point, 22);
            fx.Shockwave(camera.ToScreen(point), camera.Px(1f), MiniGolfRenderer.WaterTint, 0.5f, 2.6f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameWrong);
                fx.AddText(Loc.T(L.MiniGolf.Penalty), camera.ToScreen(point), Danger, 1.1f);
                camera.Shake(0.12f);
            }
        }

        if ((events & GolfEvents.Reset) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameWrong);
        }

        if ((events & GolfEvents.LipOut) != 0 && live)
        {
            UiFeedback.Play(UiSound.GameHitSoft);
            fx.AddText(Loc.T(L.MiniGolf.LipOut), camera.ToScreen(point) - new Vector2(0f, camera.Px(0.4f)), White, 1f);
        }

        if ((events & GolfEvents.Drop) != 0)
        {
            fx.Shockwave(camera.ToScreen(point), camera.Px(0.6f), White, 0.3f, 2.2f);
            if (live)
            {
                UiFeedback.Play(UiSound.GameCollect);
                context.Fx.SlowMo(0.5f, 0.3f);
            }
        }

        if ((events & GolfEvents.Stopped) != 0)
        {
            trail.Clear();
        }
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var hole = board.Hole;
        var alpha = Easing.EaseOutCubic(entrance);
        MiniGolfRenderer.DrawCourse(drawList, in camera, hole, alpha, time);
        var ball = board.BallRenderPosition;
        MiniGolfRenderer.DrawCup(drawList, in camera, hole.Cup, ball, Accent, alpha, time);
        MiniGolfRenderer.DrawPosts(drawList, in camera, hole, flashPoint, bumpFlash, alpha);
        if (board.Phase == GolfPhase.Rolling && board.BallSpeed > TrailSpeed)
        {
            trail.Push(ball);
        }

        trail.Draw(drawList, in camera, White with { W = 0.45f }, camera.Px(TrailWidth), additive: true);
        DrawBall(drawList, ball);
        MiniGolfRenderer.DrawMills(drawList, in camera, board, alpha);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }

    private void DrawBall(ImDrawListPtr drawList, Vector2 ball)
    {
        var radius = camera.Px(MiniGolfBoard.BallRadius);
        var band = round.Players > 1 ? GameSeats.Color(round.Player) : Accent with { W = 0f };
        switch (board.Phase)
        {
            case GolfPhase.Sinking:
            {
                var progress = Easing.EaseInCubic(board.PhaseProgress);
                var position = Vector2.Lerp(board.SinkFrom, board.Hole.Cup, progress);
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(position), radius * (1f - 0.55f * progress), band,
                    1f - 0.6f * progress);
                return;
            }
            case GolfPhase.Holed:
                return;
            case GolfPhase.Drowning:
            {
                var progress = board.PhaseProgress;
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(board.SinkFrom), radius * (1f - progress), band,
                    1f - progress);
                return;
            }
            default:
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(ball), radius, band, 1f);
                return;
        }
    }

    private void FillHud(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Strokes, L.MiniGolf.Strokes);
        var holeText = holeLabel.Get(L.MiniGolf.HolePar, round.Hole + 1, board.Hole.Par);
        var hotSeat = round.Players > 1;
        var secondText = hotSeat
            ? GameSeats.Name(round.Player)
            : MiniGolfScorecard.ToParLabel(round.ToPar(0));
        hud.Custom(StatCapsule.Width(holeText, scale));
        hud.Custom(StatCapsule.Width(secondText, scale));
        if (hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(0), FontAwesomeIcon.Flag, holeText, Accent, scale);
        }

        if (!hud.CustomPlaced(1))
        {
            return;
        }

        StatCapsule.Draw(drawList, hud.CustomRect(1), hotSeat ? FontAwesomeIcon.User : FontAwesomeIcon.Bullseye,
            secondText, hotSeat ? GameSeats.Color(round.Player) : Accent, scale);
    }

    private static Vector2 BannerCenter(Rect safe) => new(safe.Center.X, safe.Min.Y + safe.Height * BannerHeight);

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private static string ResultText(HoleResult result, int overPar) => result switch
    {
        HoleResult.HoleInOne => Loc.T(L.MiniGolf.HoleInOne),
        HoleResult.Eagle => Loc.T(L.MiniGolf.Eagle),
        HoleResult.Birdie => Loc.T(L.MiniGolf.Birdie),
        HoleResult.Par => Loc.T(L.MiniGolf.Par),
        HoleResult.Bogey => Loc.T(L.MiniGolf.Bogey),
        HoleResult.DoubleBogey => Loc.T(L.MiniGolf.DoubleBogey),
        _ => GameNumber.Signed(overPar),
    };
}
