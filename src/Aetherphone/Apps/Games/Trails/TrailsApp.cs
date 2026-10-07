using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trails;

internal sealed class TrailsApp : IMiniGame
{
    private const string GameId = "trails";
    private const string FieldSurfaceId = "trails.field";
    private const float RoundBannerSeconds = 1.3f;
    private const float EndBannerSeconds = 1.8f;
    private const float HintSeconds = 4f;
    private const float SparkRate = 30f;
    private const float EdgeHeat = 0.55f;
    private const int IdleBots = 3;
    private const float IdleSpeed = 11f;
    private const ulong IdleSeed = 0x545241494CUL;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Trails.Duel, L.Games.Hard };
    private static readonly int[] ModeBots = { 2, 1, 3 };
    private static readonly BotSkill[] ModeSkills = { BotSkill.Easy, BotSkill.Hard, BotSkill.Hard };
    private static readonly float[] ModeSpeeds = { 9f, 10.5f, 11f };
    private static readonly GameSpec StageSpec = new(GameId, L.Trails.Title, GameGenre.Action, L.Trails.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Streak, Modes, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    private static readonly Vector4 AccentColor = AppAccents.For(GameId);
    private static readonly Vector4[] Celebration =
    {
        new(0.98f, 0.95f, 0.90f, 1f), AccentColor, new(0.36f, 0.86f, 1f, 1f), new(1f, 0.68f, 0.28f, 1f),
        new(0.64f, 1f, 0.38f, 1f), Gold,
    };

    private static readonly ParticleSpec Sparks = new(White, AccentColor with { W = 0f }, 0.16f, 9f, 0.4f, 0f, 2.2f,
        shape: ParticleShape.Spark, additive: true);
    private static readonly ParticleSpec Grind = new(White, Gold with { W = 0f }, 0.12f, 6f, 0.25f, 0f, 3f, 0f, 0.8f,
        shape: ParticleShape.Spark, additive: true);

    private readonly TrailsBoard board = new();
    private readonly TrailsBoard idleBoard = new();
    private readonly ParticleSystem particles = new(640);
    private readonly FeedbackFx fx = new();
    private readonly Emitter[] exhausts = new Emitter[TrailsBoard.MaxRiders];
    private readonly ParticleSpec[] exhaustSpecs = new ParticleSpec[TrailsBoard.MaxRiders];
    private Camera2D camera = Camera2D.Create();
    private LabelSlot roundLabel;
    private float bannerProgress = 1f;
    private float bannerLifetime = RoundBannerSeconds;
    private float hintTimer;
    private float time;
    private string bannerText = string.Empty;
    private bool turnedOnce;
    private bool finished;
    private bool idleReady;

    public TrailsApp()
    {
        for (var index = 0; index < TrailsBoard.MaxRiders; index++)
        {
            var color = TrailsRenderer.RiderColor(index, AccentColor);
            exhaustSpecs[index] = new ParticleSpec(NeonStroke.Core(color), color with { W = 0f }, 0.16f, 3f, 0.35f, 0f,
                2.4f, 0f, 0.9f, 0f, ParticleShape.Spark, SizeCurve.Shrink, true);
            exhausts[index] = new Emitter(in exhaustSpecs[index], SparkRate);
        }
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AccentColor;

    public void Start(in GameStart start)
    {
        var mode = StageSpec.ClampMode(start.Mode);
        board.Reset(start.Random, ModeBots[mode], ModeSkills[mode], ModeSpeeds[mode]);
        particles.Clear();
        particles.Reseed(start.Seed);
        fx.Clear();
        for (var index = 0; index < exhausts.Length; index++)
        {
            exhausts[index].Reset();
        }

        hintTimer = HintSeconds;
        turnedOnce = false;
        finished = false;
        bannerProgress = 1f;
    }

    public void Close()
    {
        particles.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        if (!idleReady || idleBoard.Phase == RoundPhase.MatchOver)
        {
            idleBoard.Reset(GameRandom.FromSeed(IdleSeed), IdleBots, BotSkill.Hard, IdleSpeed, true);
            idleReady = true;
        }

        var scale = UiScale.Current;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        idleBoard.Step(context.RawDeltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        var field = TrailsRenderer.FieldRect(in camera);
        TrailsRenderer.DrawField(drawList, field, in camera, Accent, scale);
        drawList.PushClipRect(field.Min, field.Max, true);
        TrailsRenderer.DrawTrails(drawList, idleBoard, in camera, Accent, scale, time);
        drawList.PopClipRect();
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var accent = Accent;
        time += context.RawDeltaSeconds;
        PlaceCamera(context, scale);
        var field = TrailsRenderer.FieldRect(in camera);
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        if (!finished)
        {
            HandleInput(context, field);
            board.Step(simDelta);
            ReactToEvents(context, accent);
            TrailSparks(simDelta);
        }

        if (board.Phase == RoundPhase.Racing && simDelta > 0f)
        {
            hintTimer = MathF.Max(0f, hintTimer - simDelta);
            context.Fx.EdgeGlow(board.SpeedFraction * EdgeHeat);
        }

        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        bannerProgress = GameBanner.Advance(bannerProgress, context.RawDeltaSeconds, bannerLifetime);
        TrailsRenderer.DrawField(drawList, field, in camera, accent, scale);
        drawList.PushClipRect(field.Min, field.Max, true);
        TrailsRenderer.DrawTrails(drawList, board, in camera, accent, scale, time);
        particles.Draw(drawList, in camera);
        drawList.PopClipRect();
        var hint = turnedOnce ? 0f : MathF.Min(1f, hintTimer);
        TrailsRenderer.DrawHints(drawList, field, accent, scale, hint);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, field.Center, bannerText, accent, context.Theme, bannerProgress);
        DrawHud(drawList, context, accent, scale);
        if (board.Phase != RoundPhase.MatchOver || finished)
        {
            return;
        }

        finished = true;
        Finish(context);
    }

    private void PlaceCamera(in GameContext context, float scale)
    {
        var full = context.Full;
        var view = new Rect(new Vector2(full.Min.X + StageLayout.SafeSide * scale, context.Safe.Min.Y),
            new Vector2(full.Max.X - StageLayout.SafeSide * scale, full.Max.Y - StageLayout.SafeBottom * scale));
        camera.Fit(view, TrailsBoard.Columns, TrailsBoard.Rows, FitMode.Contain);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, scale);
        context.Backdrop.SetCamera(in camera);
    }

    private void HandleInput(in GameContext context, Rect field)
    {
        if (context.Session.State is not (StageFlow.Playing or StageFlow.Countdown))
        {
            return;
        }

        if (GameInput.Pressed(ImGuiKey.LeftArrow, ImGuiKey.Q))
        {
            Turn(-1);
        }

        if (GameInput.Pressed(ImGuiKey.RightArrow, ImGuiKey.E))
        {
            Turn(1);
        }

        if (GameInput.Pressed(ImGuiKey.W))
        {
            Steer(Heading.Up);
        }

        if (GameInput.Pressed(ImGuiKey.D))
        {
            Steer(Heading.Right);
        }

        if (GameInput.Pressed(ImGuiKey.S))
        {
            Steer(Heading.Down);
        }

        if (GameInput.Pressed(ImGuiKey.A))
        {
            Steer(Heading.Left);
        }

        PressSurface.Claim(FieldSurfaceId, field, out var activated);
        var mouse = ImGui.GetMousePos();
        if (!activated || context.ChromeHit(mouse))
        {
            return;
        }

        Turn(mouse.X < field.Center.X ? -1 : 1);
    }

    private void Turn(int direction)
    {
        board.QueueTurn(direction);
        turnedOnce = true;
    }

    private void Steer(Heading heading)
    {
        board.QueueHeading(heading);
        turnedOnce = true;
    }

    private void TrailSparks(float deltaSeconds)
    {
        if (board.Phase != RoundPhase.Racing || deltaSeconds <= 0f)
        {
            return;
        }

        for (var index = 0; index < board.RiderCount; index++)
        {
            ref readonly var rider = ref board.RiderAt(index);
            if (!rider.Alive)
            {
                continue;
            }

            var back = MathF.Atan2(-TrailsBoard.DeltaY(rider.Heading), -TrailsBoard.DeltaX(rider.Heading));
            exhausts[index].Spec = exhaustSpecs[index].WithDirection(back, 0.9f);
            exhausts[index].Advance(deltaSeconds, board.HeadPosition(index), particles);
        }
    }

    private void ReactToEvents(in GameContext context, Vector4 accent)
    {
        if (board.TurnedThisFrame)
        {
            particles.Emit(Sparks, board.HeadPosition(TrailsBoard.Player), 4);
        }

        if (board.GrindLeftThisFrame || board.GrindRightThisFrame)
        {
            EmitGrind();
        }

        for (var index = 0; index < board.CrashCount; index++)
        {
            OnCrash(board.CrashAt(index), context, accent);
        }

        if (board.RoundEndedThisFrame)
        {
            OnRoundEnd(context, accent);
        }

        if (board.RoundStartedThisFrame && board.Round > 1)
        {
            UiFeedback.Play(UiSound.GameTick);
            ShowBanner(board.MatchPoint ? Loc.T(L.Trails.MatchPoint) : roundLabel.Get(L.Trails.RoundNumber, board.Round),
                RoundBannerSeconds);
        }

        if (!board.RacingStartedThisFrame)
        {
            return;
        }

        GameSfx.CountdownTick();
        fx.AddText(Loc.T(L.Trails.Go), camera.ToScreen(new Vector2(TrailsBoard.Columns * 0.5f, TrailsBoard.Rows * 0.5f)),
            accent, 1.5f);
    }

    private void EmitGrind()
    {
        ref readonly var player = ref board.RiderAt(TrailsBoard.Player);
        if (!player.Alive)
        {
            return;
        }

        var head = board.HeadPosition(TrailsBoard.Player);
        var side = new Vector2(-TrailsBoard.DeltaY(player.Heading), TrailsBoard.DeltaX(player.Heading));
        if (board.GrindRightThisFrame)
        {
            particles.Emit(Grind.WithDirection(MathF.Atan2(side.Y, side.X), 0.8f), head + side * 0.45f, 3);
        }

        if (board.GrindLeftThisFrame)
        {
            particles.Emit(Grind.WithDirection(MathF.Atan2(-side.Y, -side.X), 0.8f), head - side * 0.45f, 3);
        }
    }

    private void OnCrash(in Crash crash, in GameContext context, Vector4 accent)
    {
        var color = TrailsRenderer.RiderColor(crash.Rider, accent);
        var shards = new ParticleSpec(NeonStroke.Core(color), color with { W = 0f }, 0.32f, 11f, 0.9f, 0f, 1.4f, 12f,
            shape: ParticleShape.Shard, additive: true);
        particles.Emit(shards, crash.Position, 20);
        particles.Emit(Sparks, crash.Position, 16);
        particles.Emit(new ParticleSpec(color, color with { W = 0f }, 1.6f, 0f, 0.5f, shape: ParticleShape.Ring,
            curve: SizeCurve.Grow, additive: true), crash.Position, 1);
        var screen = camera.ToScreen(crash.Position);
        fx.Shockwave(screen, camera.Px(4f), color with { W = 0.7f }, 0.5f, 3f);
        UiFeedback.Play(UiSound.GameExplosion);
        if (crash.Rider == TrailsBoard.Player)
        {
            camera.Shake(0.8f);
            fx.HitStop(0.08f);
            context.Fx.Flash(Danger, 0.35f);
            context.Fx.Vignette(Danger, 0.45f, 1f);
            return;
        }

        camera.Shake(0.3f);
        if (!crash.Takedown)
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePowerUp);
        fx.AddText(Loc.T(L.Trails.Takedown), screen, Gold, 1.25f);
        context.Fx.Flash(Gold, 0.1f);
    }

    private void OnRoundEnd(in GameContext context, Vector4 accent)
    {
        context.Fx.SlowMo(0.3f, 0.9f);
        context.Fx.Punch(0.07f);
        switch (board.Verdict)
        {
            case MatchVerdict.Won:
                GameSfx.NewBest();
                context.Fx.Sweep();
                particles.Confetti(new Vector2(TrailsBoard.Columns * 0.5f, TrailsBoard.Rows * 0.3f), 70, Celebration,
                    18f, 0.3f, 1.4f, 26f);
                ShowBanner(Loc.T(L.Games.YouWin), EndBannerSeconds);
                return;
            case MatchVerdict.Lost:
                UiFeedback.Play(UiSound.GameWrong);
                ShowBanner(Loc.T(L.Games.Lose), EndBannerSeconds);
                return;
            case MatchVerdict.Drawn:
                ShowBanner(Loc.T(L.Games.Draw), EndBannerSeconds);
                return;
            default:
                break;
        }

        switch (board.LastResult)
        {
            case RoundResult.Won:
                GameSfx.LevelClear();
                context.Fx.Sweep();
                context.Fx.Flash(GamePalette.Lighten(accent, 0.4f), 0.12f);
                ShowBanner(Loc.T(L.Trails.RoundWon), EndBannerSeconds);
                return;
            case RoundResult.Lost:
                UiFeedback.Play(UiSound.GameWrong);
                ShowBanner(Loc.T(L.Trails.RoundLost), EndBannerSeconds);
                return;
            default:
                ShowBanner(Loc.T(L.Games.Draw), EndBannerSeconds);
                return;
        }
    }

    private void ShowBanner(string text, float lifetime)
    {
        bannerText = text;
        bannerLifetime = lifetime;
        bannerProgress = 0f;
    }

    private void DrawHud(ImDrawListPtr drawList, in GameContext context, Vector4 accent, float scale)
    {
        context.Hud.Score(board.PlayerWins, L.Trails.Wins);
        context.Hud.Custom(TrailsRenderer.ScoreboardWidth(board.RiderCount - 1));
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.PlayerWins);
        if (!context.Hud.CustomPlaced(0))
        {
            return;
        }

        TrailsRenderer.DrawScoreboard(drawList, context.Hud.CustomRect(0), board, accent, scale);
    }

    private void Finish(in GameContext context)
    {
        var outcome = board.Verdict == MatchVerdict.Drawn
            ? GameOutcome.Drawn(GameId)
            : new GameOutcome(0, ScoreKind.Streak, GameId, board.Verdict == MatchVerdict.Won);
        context.Session.Finish(outcome
            .WithStat(L.Trails.RoundsWon, GameNumber.Label(board.PlayerWins))
            .WithStat(L.Trails.Takedowns, GameNumber.Label(board.Takedowns))
            .WithStat(L.Trails.LongestRide, TimeText.MinutesSeconds((int)board.LongestRide)));
    }
}
