using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Beat;

internal sealed class BeatApp : IMiniGame
{
    private const string GameId = "beat";
    private const float FlashDecay = 3.6f;
    private const float MaxLaneWidth = 0.22f;
    private const float BeatPunch = 0.02f;
    private const float PerfectPunch = 0.04f;
    private const float PerfectShake = 0.10f;
    private const float WrongShake = 0.12f;
    private const float MissShake = 0.35f;
    private const float OverShake = 0.6f;
    private const float CalloutRise = 30f;
    private const int PunchMultiplier = 3;
    private const ulong IdleSeed = 11;
    private const int IdleFrames = 96;
    private const float IdleFrameSeconds = 1f / 60f;
    private static readonly ImGuiKey[] LaneKeys = { ImGuiKey.Key1, ImGuiKey.Key2, ImGuiKey.Key3, ImGuiKey.Key4 };
    private static readonly ImGuiKey[] LaneAlternateKeys = { ImGuiKey.A, ImGuiKey.S, ImGuiKey.D, ImGuiKey.F };
    private static readonly string[] LaneKeyLabels = { "1", "2", "3", "4" };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Beat, GameGenre.Arcade, L.Beat.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, keyboard: true);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 Soft = new(0.92f, 0.94f, 0.98f, 0.9f);
    private static readonly ParticleSpec[] HitBursts = BuildBursts();
    private static readonly ParticleSpec[] HitSparkles = BuildSparkles();
    private static readonly ParticleSpec MissBurst = new(Danger, Danger with { W = 0f }, 0.012f, 1.1f, 0.5f, 2.2f);
    private readonly BeatBoard board = new();
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private readonly float[] laneFlash = new float[BeatBoard.Lanes];
    private Camera2D camera = Camera2D.Create();
    private float laneWidth = MaxLaneWidth;
    private bool finished;

    public BeatApp()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        for (var frame = 0; frame < IdleFrames; frame++)
        {
            board.Step(IdleFrameSeconds);
        }
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        fx.Clear();
        camera = Camera2D.Create();
        for (var lane = 0; lane < laneFlash.Length; lane++)
        {
            laneFlash[lane] = 0f;
        }

        finished = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        DrawWorld(ImGui.GetWindowDrawList(), context, UiScale.Current, false);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        for (var lane = 0; lane < laneFlash.Length; lane++)
        {
            laneFlash[lane] = MathF.Max(0f, laneFlash[lane] - context.RawDeltaSeconds * FlashDecay);
        }

        PlaceCamera(context);
        if (!finished)
        {
            Step(context, scale);
        }

        DrawWorld(drawList, context, scale, true);
        if (!finished && board.Lives == 1)
        {
            context.Fx.Vignette(Danger, 0.08f + 0.10f * Pulse.Wave(Pulse.Fast), 0.3f);
        }

        context.Hud.Score(board.Score);
        context.Hud.Lives(board.Lives, BeatBoard.StartLives);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Full, BeatBoard.Lanes * MaxLaneWidth, BeatBoard.WorldHeight, FitMode.CoverHeight);
        laneWidth = MathF.Min(MaxLaneWidth, context.Safe.Width / camera.Zoom / BeatBoard.Lanes);
        camera.Place(new Vector2(BeatBoard.Lanes * laneWidth * 0.5f, BeatBoard.WorldHeight * 0.5f));
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(in GameContext context, float scale)
    {
        var judgement = board.Step(context.DeltaSeconds);
        if (board.SpawnedThisStep)
        {
            context.Fx.Punch(BeatPunch);
        }

        if (judgement == BeatJudgement.Missed)
        {
            OnMissed(context, scale);
        }

        HandleInput(context, scale);
        if (board.State != BeatState.Over)
        {
            return;
        }

        finished = true;
        OnGameOver(context);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.Beat.PerfectHits, GameNumber.Label(board.Perfects))
            .WithStat(L.Games.Notes, GameNumber.Label(board.Hits))
            .WithStat(L.Games.Level, GameNumber.Label(board.Level)));
    }

    private void HandleInput(in GameContext context, float scale)
    {
        if (context.Session.State != StageFlow.Playing)
        {
            return;
        }

        for (var lane = 0; lane < BeatBoard.Lanes; lane++)
        {
            if (GameInput.Pressed(LaneKeys[lane], LaneAlternateKeys[lane]))
            {
                TapLane(lane, context, scale);
            }
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var lanes = BeatRenderer.LanesRect(in camera, laneWidth);
        var min = new Vector2(lanes.Min.X, MathF.Max(lanes.Min.Y, context.Full.Min.Y + StageLayout.ChromeBand * scale));
        if (!UiInteract.Hover(min, lanes.Max))
        {
            return;
        }

        var world = camera.ToWorld(ImGui.GetMousePos());
        TapLane(Math.Clamp((int)(world.X / laneWidth), 0, BeatBoard.Lanes - 1), context, scale);
    }

    private void TapLane(int lane, in GameContext context, float scale)
    {
        var multiplierBefore = board.Multiplier;
        var judgement = board.Tap(lane);
        switch (judgement)
        {
            case BeatJudgement.Wrong:
                OnWrong(lane, scale);
                return;
            case BeatJudgement.Perfect:
            case BeatJudgement.Good:
                OnHit(lane, judgement == BeatJudgement.Perfect, board.Multiplier > multiplierBefore, context, scale);
                return;
            default:
                return;
        }
    }

    private void OnHit(int lane, bool perfect, bool tierUp, in GameContext context, float scale)
    {
        UiFeedback.Play(perfect ? UiSound.GameMatch : UiSound.GameTick);
        laneFlash[lane] = 1f;
        var color = BeatRenderer.LaneColor(lane);
        var world = new Vector2(BeatBoard.LaneCenter(lane, laneWidth), BeatBoard.HitLine);
        var screen = camera.ToScreen(world);
        particles.Emit(HitBursts[lane], world, perfect ? 14 : 8);
        fx.Shockwave(screen, camera.Px(perfect ? 0.10f : 0.065f), GamePalette.Lighten(color, 0.4f), 0.4f, 2.6f);
        fx.AddText(Loc.T(perfect ? L.Games.Perfect : L.Games.Good), new Vector2(screen.X, screen.Y - CalloutRise * scale),
            perfect ? GamePalette.Lighten(color, 0.5f) : Soft, perfect ? 1.1f : 0.95f);
        if (tierUp)
        {
            GameSfx.ComboTierUp();
        }

        if (!perfect)
        {
            return;
        }

        particles.Emit(HitSparkles[lane], world, 9);
        camera.Shake(PerfectShake);
        if (board.Multiplier >= PunchMultiplier)
        {
            context.Fx.Punch(PerfectPunch);
        }
    }

    private void OnWrong(int lane, float scale)
    {
        UiFeedback.Play(UiSound.GameWrong);
        var screen = camera.ToScreen(new Vector2(BeatBoard.LaneCenter(lane, laneWidth), BeatBoard.HitLine));
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(screen.X, screen.Y - CalloutRise * scale), Danger, 0.95f);
        camera.Shake(WrongShake);
    }

    private void OnMissed(in GameContext context, float scale)
    {
        UiFeedback.Play(UiSound.GameHitSoft);
        var lane = board.MissedLane;
        if (lane < 0)
        {
            return;
        }

        var world = new Vector2(BeatBoard.LaneCenter(lane, laneWidth), BeatBoard.HitLine);
        var screen = camera.ToScreen(world);
        context.Fx.Flash(Danger, 0.25f);
        camera.Shake(MissShake);
        fx.AddText(Loc.T(L.Games.Miss), new Vector2(screen.X, screen.Y - CalloutRise * scale), Danger, 1.05f);
        particles.Emit(MissBurst, world with { Y = BeatBoard.MissLine }, 12);
    }

    private void OnGameOver(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameBreak);
        context.Fx.Flash(Danger, 0.4f);
        camera.Shake(OverShake);
        context.Fx.SlowMo(0.5f, 0.3f);
    }

    private void DrawWorld(ImDrawListPtr drawList, in GameContext context, float scale, bool interactive)
    {
        BeatRenderer.DrawLanes(drawList, in camera, laneWidth, laneFlash, board.Combo.Heat, Accent, scale);
        BeatRenderer.DrawTiles(drawList, in camera, board, laneWidth, scale);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        var band = StageLayout.PadBand(context.Full, StageLayout.ShooterBand, scale);
        BeatRenderer.DrawPads(drawList, in camera, band, laneWidth, laneFlash, LaneKeyLabels, interactive, Accent,
            context.Theme, scale);
    }

    private static ParticleSpec[] BuildBursts()
    {
        var specs = new ParticleSpec[BeatBoard.Lanes];
        for (var lane = 0; lane < specs.Length; lane++)
        {
            var color = BeatRenderer.LaneColor(lane);
            specs[lane] = new ParticleSpec(GamePalette.Lighten(color, 0.2f), color, 0.012f, 0.9f, 0.45f, 1.8f);
        }

        return specs;
    }

    private static ParticleSpec[] BuildSparkles()
    {
        var specs = new ParticleSpec[BeatBoard.Lanes];
        for (var lane = 0; lane < specs.Length; lane++)
        {
            var color = GamePalette.Lighten(BeatRenderer.LaneColor(lane), 0.5f);
            specs[lane] = new ParticleSpec(color, color, 0.010f, 0.7f, 0.6f, 0.3f, 2.4f, 6f,
                shape: ParticleShape.Star, additive: true);
        }

        return specs;
    }
}
