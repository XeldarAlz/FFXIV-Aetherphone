using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Snake;

internal sealed class SnakeApp : IMiniGame
{
    internal const string WrapStatId = "snake.wrap";
    private const string GameId = "snake";
    private const string SwipeSurfaceId = "snake.swipe";
    private const int WrapMode = 1;
    private const float SwipeThreshold = 22f;
    private const float EatPulseDecay = 3.4f;
    private const float LeadFraction = 0.05f;
    private const float FollowSmoothSeconds = 0.4f;
    private const float RibbonWidth = 0.5f;
    private const ulong IdleSeed = 7;
    private static readonly LocString[] Modes = { L.Games.Classic, L.Snake.Wrap };
    private static readonly string[] ModeStatIds = { GameId, WrapStatId };
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Snake, GameGenre.Arcade, L.Snake.Hook,
        Backdrop.Meadow, HudStyle.Standard, ScoreKind.Score, Modes, ModeStatIds, clocked: true, countdown: true,
        keyboard: true);
    private static readonly string AppleLabel = string.Concat("+", GameNumber.Label(SnakeBoard.ApplePoints));
    private static readonly string GoldLabel = string.Concat("+", GameNumber.Label(SnakeBoard.GoldPoints));
    private static readonly string BombLabel = string.Concat("-", GameNumber.Label(SnakeBoard.BombShrink));
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.7f, 1f);
    private static readonly Vector4 Smoke = new(0.3f, 0.3f, 0.34f, 0.8f);
    private static readonly ParticleSpec AppleBurst = new(GamePalette.Lighten(SnakeRenderer.HeadColor, 0.2f),
        SnakeRenderer.AppleColor, 0.14f, 6f, 0.5f, 8f, shape: ParticleShape.Circle);
    private static readonly ParticleSpec GoldSparkle = new(SnakeRenderer.GoldColor, Spark, 0.12f, 4f, 0.8f, 1.5f,
        2.2f, 6f, shape: ParticleShape.Star, additive: true);
    private static readonly ParticleSpec BombSmoke = new(Smoke, Smoke with { W = 0f }, 0.3f, 3f, 0.7f, -1f, 1.5f,
        curve: SizeCurve.Grow);
    private static readonly ParticleSpec CrashShards = new(SnakeRenderer.HeadColor, SnakeRenderer.TailColor, 0.16f,
        9f, 0.8f, 10f, 1.2f, 8f, shape: ParticleShape.Shard);
    private readonly SnakeBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private Camera2D camera = Camera2D.Create();
    private Vector2 swipeStart;
    private bool swipeActive;
    private float eatPulse;
    private bool finished;

    public SnakeApp()
    {
        board.Reset(GameRandom.FromSeed(IdleSeed), false);
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random, start.Mode == WrapMode);
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        camera = Camera2D.Create();
        eatPulse = 0f;
        swipeActive = false;
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
        PlaceCamera(context);
        DrawWorld(ImGui.GetWindowDrawList(), UiScale.Current);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        eatPulse = MathF.Max(0f, eatPulse - context.RawDeltaSeconds * EatPulseDecay);
        PlaceCamera(context);
        if (!finished)
        {
            Step(simDelta, context);
        }

        DrawWorld(drawList, scale);
        context.Hud.Score(board.Score);
        context.Hud.Level(board.Level);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void PlaceCamera(in GameContext context)
    {
        camera.Fit(context.Safe, SnakeBoard.Columns, SnakeBoard.Rows, FitMode.Contain);
        var center = new Vector2(SnakeBoard.Columns * 0.5f, SnakeBoard.Rows * 0.5f);
        camera.Follow(center + (board.HeadWorld - center) * LeadFraction, Vector2.Zero, FollowSmoothSeconds,
            context.DeltaSeconds);
        context.Fx.ApplyTo(ref camera);
        camera.Update(context.RawDeltaSeconds, UiScale.Current);
        context.Backdrop.SetCamera(in camera);
    }

    private void Step(float deltaSeconds, in GameContext context)
    {
        HandleInput(context);
        board.Step(deltaSeconds);
        if (board.WrappedThisStep)
        {
            ribbon.Clear();
        }

        if (board.State == SnakeState.Playing && deltaSeconds > 0f)
        {
            ribbon.Push(board.HeadWorld);
        }

        if (board.AteThisStep != FruitKind.None)
        {
            OnEat(board.AteThisStep, context);
        }

        if (board.LevelledThisStep)
        {
            GameSfx.LevelClear();
            context.Fx.Sweep();
        }

        if (board.DiedThisStep)
        {
            OnDeath(context);
        }

        if (board.State != SnakeState.Over)
        {
            return;
        }

        finished = true;
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, context.Session.StatId)
            .WithStat(L.Games.Level, GameNumber.Label(board.Level))
            .WithStat(L.Games.Time, TimeText.MinutesSeconds((int)board.PlaySeconds)));
    }

    private void HandleInput(in GameContext context)
    {
        if (context.Session.State is not (StageFlow.Playing or StageFlow.Countdown))
        {
            swipeActive = false;
            return;
        }

        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            board.Steer(SnakeDirection.Up);
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            board.Steer(SnakeDirection.Down);
        }

        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            board.Steer(SnakeDirection.Left);
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            board.Steer(SnakeDirection.Right);
        }

        HandleSwipe(camera.View);
    }

    private void HandleSwipe(Rect area)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton(SwipeSurfaceId, area.Size);
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
                      UiInteract.Hover(area.Min, area.Max);
        var activated = hovered && ImGui.IsItemActivated();
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        ImGui.SetCursorScreenPos(cursor);
        var mouse = ImGui.GetMousePos();
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            swipeActive = false;
            return;
        }

        if (!swipeActive)
        {
            if (!activated)
            {
                return;
            }

            swipeActive = true;
            swipeStart = mouse;
            return;
        }

        var delta = mouse - swipeStart;
        var threshold = SwipeThreshold * UiScale.Current;
        if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
        {
            return;
        }

        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
        {
            board.Steer(delta.X > 0f ? SnakeDirection.Right : SnakeDirection.Left);
        }
        else
        {
            board.Steer(delta.Y > 0f ? SnakeDirection.Down : SnakeDirection.Up);
        }

        swipeStart = mouse;
    }

    private void OnEat(FruitKind kind, in GameContext context)
    {
        eatPulse = 1f;
        var headWorld = board.HeadWorld;
        var headScreen = camera.ToScreen(headWorld);
        switch (kind)
        {
            case FruitKind.Gold:
                UiFeedback.Play(UiSound.GamePowerUp);
                particles.Emit(GoldSparkle, headWorld, 18);
                fx.Shockwave(headScreen, camera.Px(2.2f), SnakeRenderer.GoldColor, 0.5f, 3f);
                fx.AddText(GoldLabel, headScreen, SnakeRenderer.GoldColor, 1.3f);
                context.Fx.Punch(0.08f);
                context.Fx.Sweep();
                return;
            case FruitKind.Bomb:
                UiFeedback.Play(UiSound.GameExplosion);
                particles.Emit(BombSmoke, headWorld, 14);
                fx.Shockwave(headScreen, camera.Px(2.6f), Danger, 0.5f, 3.4f);
                fx.AddText(BombLabel, headScreen, Danger, 1.2f);
                camera.Shake(0.35f);
                context.Fx.Flash(Danger, 0.25f);
                context.Fx.Punch(0.05f);
                return;
            default:
                UiFeedback.Play(UiSound.GameCollect);
                particles.Emit(AppleBurst, headWorld, 12);
                fx.Shockwave(headScreen, camera.Px(1.6f), GamePalette.Lighten(Accent, 0.35f), 0.4f, 2.6f);
                fx.AddText(AppleLabel, headScreen, Accent, 1.1f);
                fx.HitStop(0.04f);
                context.Fx.Punch(0.04f);
                return;
        }
    }

    private void OnDeath(in GameContext context)
    {
        UiFeedback.Play(UiSound.GameBreak);
        var headWorld = board.HeadWorld;
        var headScreen = camera.ToScreen(headWorld);
        particles.Emit(CrashShards, headWorld, 26);
        fx.Shockwave(headScreen, camera.Px(4f), Danger, 0.6f, 3.4f);
        camera.Shake(0.6f);
        context.Fx.Flash(Danger, 0.4f);
        context.Fx.SlowMo(0.5f, 0.3f);
    }

    private void DrawWorld(ImDrawListPtr drawList, float scale)
    {
        SnakeRenderer.DrawFloor(drawList, in camera, board.Wrap, scale);
        SnakeRenderer.DrawFruit(drawList, in camera, board, scale);
        ribbon.Draw(drawList, in camera, SnakeRenderer.HeadColor with { W = 0.55f }, camera.Px(RibbonWidth),
            additive: true);
        SnakeRenderer.DrawSnake(drawList, in camera, board, eatPulse);
        particles.Draw(drawList, in camera);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
    }
}
