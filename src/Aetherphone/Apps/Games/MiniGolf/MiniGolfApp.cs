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
    private const float EntranceSpeed = 2.4f;
    private const float ScorecardSpeed = 2.8f;
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
    private readonly MiniGolfBoard board = new();
    private readonly MiniGolfRound round = new();
    private readonly MiniGolfJuice juice = new();
    private readonly Vector2[] aimPath = new Vector2[MiniGolfRenderer.MaxAimPoints];
    private Camera2D camera = Camera2D.Create();
    private LabelPairSlot holeLabel;
    private LabelPairSlot holeOfLabel;
    private string bannerText = string.Empty;
    private Vector4 bannerColor = White;
    private Vector2 dragStart;
    private ulong seed = IdleSeed;
    private GolfStage stage;
    private int aimPathCount;
    private int playedHole;
    private float power;
    private float bannerProgress = 1f;
    private float stageSeconds;
    private float entrance = 1f;
    private float scorecardAppear;
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
        juice.Clear();
        juice.Particles.Reseed(start.Seed);
        LoadHole();
    }

    public void Close()
    {
        BuildIdle();
        juice.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var deltaSeconds = context.RawDeltaSeconds;
        time += deltaSeconds;
        juice.Update(deltaSeconds);
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
        juice.Update(rawSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, rawSeconds, BannerSeconds);
        entrance = GameJuice.Advance(entrance, rawSeconds, EntranceSpeed);
        PlaceCamera(context);
        board.BeginFrame();
        var simDelta = juice.Fx.ScaleDelta(context.DeltaSeconds);
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

        juice.Celebration.Draw(drawList, scale);
        FillHud(drawList, context, scale);
        context.Session.Report(board.Strokes);
    }

    private void BuildIdle()
    {
        seed = IdleSeed;
        round.Reset(MiniGolfCourse.HoleCount, 1);
        board.Load(MiniGolfCourse.Get(IdleHole), GameRandom.FromSeed(IdleSeed));
        juice.Trail.Clear();
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
        juice.Trail.Clear();
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
        ShowBanner(MiniGolfScorecard.ResultText(result, strokes - par), MiniGolfScorecard.ResultColor(result));
        juice.Holed(result, board.Hole.Cup, context.Full, in camera, context.Fx);
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
        juice.React(new GolfImpact(board.Events, board.EventPoint, board.ImpactStrength, board.BallVelocity,
            board.TunnelFrom), ref camera, context.Fx, Accent, live);
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        var hole = board.Hole;
        var alpha = Easing.EaseOutCubic(entrance);
        MiniGolfRenderer.DrawCourse(drawList, in camera, hole, alpha, time);
        var ball = board.BallRenderPosition;
        MiniGolfRenderer.DrawCup(drawList, in camera, hole.Cup, ball, Accent, alpha, time);
        MiniGolfRenderer.DrawPosts(drawList, in camera, hole, juice.FlashPoint, juice.BumpFlash, alpha);
        juice.TrackBall(ball, board.Phase == GolfPhase.Rolling, board.BallSpeed);
        juice.DrawTrail(drawList, in camera);
        DrawBall(drawList, ball);
        MiniGolfRenderer.DrawMills(drawList, in camera, board, alpha);
        juice.DrawEffects(drawList, in camera, scale);
    }

    private void DrawBall(ImDrawListPtr drawList, Vector2 ball)
    {
        var band = round.Players > 1 ? GameSeats.Color(round.Player) : Accent with { W = 0f };
        switch (board.Phase)
        {
            case GolfPhase.Sinking:
                MiniGolfRenderer.DrawSinking(drawList, in camera, board.SinkFrom, board.Hole.Cup, board.PhaseProgress,
                    band);
                return;
            case GolfPhase.Holed:
                return;
            case GolfPhase.Drowning:
                MiniGolfRenderer.DrawDrowning(drawList, in camera, board.SinkFrom, board.PhaseProgress, band);
                return;
            default:
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(ball), camera.Px(MiniGolfBoard.BallRadius), band,
                    1f);
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
}
