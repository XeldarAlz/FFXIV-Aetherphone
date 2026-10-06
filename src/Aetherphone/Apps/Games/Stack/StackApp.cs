using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Stack;

internal sealed class StackApp : IMiniGame
{
    private const string GameId = "stack";
    private const ulong IdleSeed = 5;
    private const int IdleLevels = 7;
    private const float ResultDelaySeconds = 0.5f;
    private const float FollowSmoothSeconds = 0.35f;
    private const float ViewAnchorFraction = 0.18f;
    private const float LeadLevels = 0.6f;
    private const float PerfectPunch = 0.05f;
    private const float WorldGravity = 6f;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Stack, GameGenre.Arcade, L.Stack.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true);
    private static readonly Vector4 Danger = new(0.95f, 0.32f, 0.32f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ember = new(1f, 0.72f, 0.4f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.92f, 0.66f, 1f);
    private static readonly ParticleSpec PerfectSparkle = new(White, White with { W = 0f }, 0.12f, 3f, 0.7f, 2f, 2.2f,
        6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec MissStreaks = new(Spark, Spark with { W = 0f }, 0.1f, 6f, 0.5f, 5f, 1.2f,
        shape: ParticleShape.Streak);
    private readonly StackBoard board = new();
    private readonly ParticleSystem particles = new(256);
    private readonly FeedbackFx fx = new();
    private Camera2D camera = Camera2D.Create();
    private bool cameraPlaced;
    private float resultDelay;
    private bool finished;

    public StackApp()
    {
        BuildIdle();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        particles.Clear();
        fx.Clear();
        camera = Camera2D.Create();
        cameraPlaced = false;
        resultDelay = 0f;
        finished = false;
    }

    public void Close()
    {
        BuildIdle();
        particles.Clear();
        fx.Clear();
        cameraPlaced = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        PlaceCamera(context);
        board.Step(context.RawDeltaSeconds);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        PlaceCamera(context);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        context.Hud.Score(board.Score);
        context.Hud.Combo(board.Combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void BuildIdle()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed));
        var idleRandom = GameRandom.FromSeed(IdleSeed);
        for (var level = 0; level < IdleLevels; level++)
        {
            board.DropAt(board.Block(board.Level - 1).CenterX + idleRandom.Range(-0.02f, 0.02f));
        }
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Full, StackRenderer.WorldWidth, StackBoard.VisibleLevels, FitMode.CoverWidth);
        var visibleLevels = camera.View.Height / camera.Zoom;
        var target = StackRenderer.World(0.5f, board.Level);
        var lead = new Vector2(0f, -(visibleLevels * ViewAnchorFraction + LeadLevels));
        if (!cameraPlaced)
        {
            camera.Place(target + lead);
            cameraPlaced = true;
        }
        else
        {
            camera.Follow(target, lead, FollowSmoothSeconds, context.DeltaSeconds);
        }

        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        if (board.State != StackState.Over)
        {
            return;
        }

        resultDelay -= deltaSeconds;
        if (resultDelay > 0f)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Stack.Height, GameNumber.Label(board.Height))
            .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
            .WithStat(L.Stack.Perfects, GameNumber.Label(board.Perfects)));
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State != StageFlow.Playing || board.State == StackState.Over)
        {
            return;
        }

        var safe = context.Safe;
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(safe.Min, safe.Max))
        {
            return;
        }

        var multiplierBefore = board.Combo.Multiplier;
        switch (board.Drop())
        {
            case StackDrop.Perfect:
                OnPerfect(multiplierBefore, context);
                return;
            case StackDrop.Placed:
                OnPlaced();
                return;
            case StackDrop.Missed:
                OnMissed(context);
                return;
            default:
                return;
        }
    }

    private void OnPlaced()
    {
        UiFeedback.Play(UiSound.GameHitWood);
        var level = board.Level - 1;
        var color = StackRenderer.ColorOf(level + board.ColorOffset);
        particles.Burst(StackRenderer.World(board.LastSliceCenterX, level + 0.5f), 10, color, 2.4f, 0.1f, 0.45f,
            WorldGravity);
        camera.Shake(0.10f);
    }

    private void OnPerfect(int multiplierBefore, in GameContext context)
    {
        UiFeedback.Play(UiSound.GameMatch);
        var scale = UiScale.Current;
        var level = board.Level - 1;
        var block = board.Block(level);
        var world = StackRenderer.BlockCenter(in block, level);
        var screen = camera.ToScreen(world);
        var color = StackRenderer.ColorOf(level + board.ColorOffset);
        fx.HitStop(0.045f);
        camera.Shake(0.2f);
        fx.Shockwave(screen, camera.Px(block.Width * StackRenderer.WorldWidth * 0.7f), GamePalette.Lighten(color, 0.4f),
            0.5f, 3.2f);
        fx.AddText(Loc.T(L.Games.Perfect), screen + new Vector2(0f, -26f * scale), GamePalette.Lighten(color, 0.45f),
            1.15f);
        particles.Emit(PerfectSparkle, world, 14);
        particles.Burst(world, 8, GamePalette.Lighten(color, 0.5f), 2f, 0.08f, 0.4f, 2f);
        context.Fx.Punch(PerfectPunch);
        if (board.Combo.Multiplier > multiplierBefore)
        {
            GameSfx.ComboTierUp();
        }

        if (!board.WidenedThisDrop)
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePowerUp);
        context.Fx.Sweep();
        context.Fx.Flash(GamePalette.Lighten(color, 0.5f), 0.18f);
    }

    private void OnMissed(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameBreak);
        var world = StackRenderer.World(board.MovingCenterX, board.Level + 0.5f);
        var screen = camera.ToScreen(world);
        context.Fx.Flash(Danger, 0.42f);
        context.Fx.SlowMo(0.5f, 0.35f);
        camera.Shake(0.65f);
        fx.Shockwave(screen, camera.Px(StackRenderer.WorldWidth * 0.3f), Ember, 0.6f, 3.4f);
        particles.Burst(world, 22, StackRenderer.ColorOf(board.Level + board.ColorOffset), 4f, 0.12f, 0.7f,
            WorldGravity, shape: ParticleShape.Shard);
        particles.Emit(MissStreaks, world, 10);
        resultDelay = ResultDelaySeconds;
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        StackRenderer.Draw(drawList, in camera, board, board.Combo.Heat, scale);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }
}
