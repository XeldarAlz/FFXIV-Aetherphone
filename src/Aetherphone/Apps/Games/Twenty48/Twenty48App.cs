using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Twenty48;

internal sealed class Twenty48App : IMiniGame
{
    private const string GameId = "2048";
    private const string SwipeSurfaceId = "2048.swipe";
    private const float SlideDuration = 0.10f;
    private const float ResolveDuration = 0.16f;
    private const int MilestoneFloor = 256;
    private const int PunchFloor = 512;
    private const float SwipeThreshold = 18f;
    private const float CapsulePadX = 10f;
    private const float CapsuleIconSize = 11f;
    private const float CapsuleIconGap = 5f;
    private const float CapsuleSectionGap = 8f;
    private const float ChipPadX = 6f;
    private const float ChipMinWidth = 24f;
    private const float ChipInsetY = 5f;
    private const int MaxRank = 16;
    private static readonly GameSpec StageSpec = new(GameId, L.Twenty48.Title, GameGenre.Puzzle, L.Twenty48.Hook,
        Backdrop.Paper, HudStyle.Standard, ScoreKind.Score, keyboard: true);
    private static readonly string?[] MilestoneLabels = new string?[MaxRank + 1];
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.7f, 1f);
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

    private enum Phase : byte
    {
        Idle,
        Sliding,
        Resolving,
    }

    private readonly Twenty48Board board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon[] trails = new Ribbon[Twenty48Board.CellCount];
    private Phase phase;
    private float slideTimer;
    private float resolveTimer;
    private float entrance;
    private int previousScore;
    private ulong idleSeed;
    private bool idleSynced;
    private bool finished;
    private bool winCelebrated;
    private bool swipeActive;
    private bool swipeConsumed;
    private Vector2 swipeStart;

    public Twenty48App()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index] = new Ribbon();
        }
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        board.Reset(start.Random);
        idleSeed = start.Seed;
        idleSynced = true;
        particles.Clear();
        fx.Clear();
        ClearTrails();
        phase = Phase.Idle;
        slideTimer = 0f;
        resolveTimer = 0f;
        entrance = 0f;
        previousScore = 0;
        finished = false;
        winCelebrated = false;
        swipeActive = false;
        swipeConsumed = false;
    }

    public void Close()
    {
        particles.Clear();
        fx.Clear();
        ClearTrails();
        idleSynced = false;
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        SyncIdle(context.Session.Seed);
        var grid = GameGrid.Centered(context.Safe, Twenty48Board.Size, Twenty48Board.Size,
            Twenty48Renderer.GapFraction);
        Twenty48Renderer.DrawBoard(ImGui.GetWindowDrawList(), board, grid, TileAnim.Still, null, 0f, UiScale.Current,
            Accent, 1f, context.Backdrop.Ink);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        var grid = GameGrid.Centered(area, Twenty48Board.Size, Twenty48Board.Size, Twenty48Renderer.GapFraction);
        AdvanceAnimation(simDelta, grid, scale, context);
        HandleInput(grid, scale, context);
        var anim = BuildAnim();
        PushTrails(grid, anim);
        Twenty48Renderer.DrawBoard(drawList, board, grid, anim, trails, TrailFade(), scale, Accent, entrance,
            context.Backdrop.Ink);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        DrawCapsule(drawList, context, scale);
        context.Hud.Score(board.Score);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(board.Score);
    }

    private void SyncIdle(ulong seed)
    {
        if (idleSynced && idleSeed == seed)
        {
            return;
        }

        board.Reset(GameRandom.FromSeed(seed));
        idleSeed = seed;
        idleSynced = true;
    }

    private void AdvanceAnimation(float deltaSeconds, in GameGrid grid, float scale, in GameContext context)
    {
        switch (phase)
        {
            case Phase.Sliding:
                slideTimer += deltaSeconds;
                if (slideTimer >= SlideDuration)
                {
                    phase = Phase.Resolving;
                    resolveTimer = 0f;
                    OnSlideResolved(grid, scale, context);
                }

                return;
            case Phase.Resolving:
                resolveTimer += deltaSeconds;
                if (resolveTimer >= ResolveDuration)
                {
                    phase = Phase.Idle;
                    ClearTrails();
                    CheckEndState(context);
                }

                return;
            default:
                return;
        }
    }

    private void OnSlideResolved(in GameGrid grid, float scale, in GameContext context)
    {
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            if (!board.Merged(index))
            {
                continue;
            }

            var center = grid.CellCenter(index % Twenty48Board.Size, index / Twenty48Board.Size);
            var color = Twenty48Renderer.ColorFor(board.Value(index));
            particles.Burst(center, 9, color, 150f * scale, 3f, 0.5f, 280f);
            fx.Shockwave(center, grid.Pitch * 0.62f, GamePalette.Lighten(color, 0.25f), 0.36f, 2.4f);
        }

        var scoreDelta = board.Score - previousScore;
        if (scoreDelta > 0)
        {
            UiFeedback.Play(UiSound.GameMatch);
            fx.AddText(GameNumber.Signed(scoreDelta), new Vector2(grid.Center.X, grid.Bounds.Min.Y - 16f * scale),
                Accent, 1.15f);
        }

        previousScore = board.Score;
        var mergeMax = board.LastMergeMax;
        if (mergeMax >= MilestoneFloor)
        {
            fx.HitStop(0.05f);
            CelebrateMilestone(grid, mergeMax, scale);
        }

        if (mergeMax >= PunchFloor)
        {
            context.Fx.Punch(0.04f);
        }

        if (mergeMax < Twenty48Board.WinValue || winCelebrated)
        {
            return;
        }

        winCelebrated = true;
        UiFeedback.Play(UiSound.GameClear);
        context.Fx.Sweep();
        context.Fx.Flash(Twenty48Renderer.ColorFor(mergeMax), 0.22f);
        context.Fx.Punch(0.06f);
    }

    private void CelebrateMilestone(in GameGrid grid, int value, float scale)
    {
        UiFeedback.Play(UiSound.GamePowerUp);
        var color = Twenty48Renderer.ColorFor(value);
        var center = grid.Center;
        var win = value >= Twenty48Board.WinValue;
        fx.AddTrauma(win ? 0.7f : 0.4f);
        fx.AddText(MilestoneLabel(value), center, color, 1.6f);
        fx.Shockwave(center, grid.Pitch * 1.6f, GamePalette.Lighten(color, 0.3f), 0.6f, 3.4f);
        particles.Burst(center, win ? 60 : 34, color, 320f * scale, 4f, 0.9f, 360f);
        particles.Sparkle(center, 16, Spark, 220f * scale, 2.8f, 0.9f);
    }

    private void CheckEndState(in GameContext context)
    {
        if (finished || board.CanMove())
        {
            return;
        }

        finished = true;
        UiFeedback.Play(UiSound.GameWrong);
        context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, GameId)
            .WithStat(L.Twenty48.BestTile, GameNumber.Label(board.MaxTile))
            .WithStat(L.Games.Moves, GameNumber.Label(board.Moves)));
    }

    private void HandleInput(in GameGrid grid, float scale, in GameContext context)
    {
        var accepting = !finished && phase == Phase.Idle && context.Session.State == StageFlow.Playing;
        if (accepting && HandleKeys(grid, scale))
        {
            return;
        }

        HandleSwipe(grid, accepting);
    }

    private bool HandleKeys(in GameGrid grid, float scale)
    {
        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow))
        {
            Move(SwipeDirection.Up, grid);
            return true;
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow))
        {
            Move(SwipeDirection.Down, grid);
            return true;
        }

        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow))
        {
            Move(SwipeDirection.Left, grid);
            return true;
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow))
        {
            Move(SwipeDirection.Right, grid);
            return true;
        }

        if (GameInput.Pressed(ImGuiKey.Z) || GameInput.Pressed(ImGuiKey.Backspace))
        {
            TryUndo(grid, scale);
            return true;
        }

        return false;
    }

    private void HandleSwipe(in GameGrid grid, bool accepting)
    {
        PressSurface.Claim(SwipeSurfaceId, grid.Bounds, out var activated);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            swipeActive = false;
            swipeConsumed = false;
            return;
        }

        var mouse = ImGui.GetMousePos();
        if (!swipeActive)
        {
            if (!activated)
            {
                return;
            }

            swipeActive = true;
            swipeConsumed = false;
            swipeStart = mouse;
            return;
        }

        if (swipeConsumed || !accepting)
        {
            return;
        }

        var delta = mouse - swipeStart;
        var threshold = SwipeThreshold * UiScale.Current;
        if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
        {
            return;
        }

        swipeConsumed = true;
        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
        {
            Move(delta.X > 0f ? SwipeDirection.Right : SwipeDirection.Left, grid);
        }
        else
        {
            Move(delta.Y > 0f ? SwipeDirection.Down : SwipeDirection.Up, grid);
        }
    }

    private void Move(SwipeDirection direction, in GameGrid grid)
    {
        if (!board.TryMove(direction))
        {
            return;
        }

        ClearTrails();
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            var source = board.SlideFrom(index);
            if (source < 0)
            {
                continue;
            }

            trails[index].Push(grid.CellCenter(source % Twenty48Board.Size, source / Twenty48Board.Size));
        }

        phase = Phase.Sliding;
        slideTimer = 0f;
    }

    private void TryUndo(in GameGrid grid, float scale)
    {
        if (!board.Undo())
        {
            return;
        }

        UiFeedback.Play(UiSound.GamePop);
        previousScore = board.Score;
        ClearTrails();
        fx.AddTrauma(0.05f);
        fx.Shockwave(grid.Center, grid.Width * 0.5f, GamePalette.Lighten(Accent, 0.3f) with { W = 0.6f }, 0.4f,
            2f * scale);
    }

    private void PushTrails(in GameGrid grid, in TileAnim anim)
    {
        if (phase != Phase.Sliding)
        {
            return;
        }

        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            if (board.SlideFrom(index) < 0)
            {
                continue;
            }

            trails[index].Push(Twenty48Renderer.TileCenter(grid, board, anim, index));
        }
    }

    private float TrailFade()
    {
        return phase switch
        {
            Phase.Sliding => 1f,
            Phase.Resolving => 1f - MathF.Min(1f, resolveTimer / ResolveDuration),
            _ => 0f,
        };
    }

    private void ClearTrails()
    {
        for (var index = 0; index < trails.Length; index++)
        {
            trails[index].Clear();
        }
    }

    private TileAnim BuildAnim()
    {
        if (phase == Phase.Sliding)
        {
            return new TileAnim(MathF.Min(1f, slideTimer / SlideDuration), true, 0f, board.SpawnIndex, 0f);
        }

        if (phase == Phase.Resolving)
        {
            var resolve = MathF.Min(1f, resolveTimer / ResolveDuration);
            return new TileAnim(1f, false, resolve, board.SpawnIndex, resolve);
        }

        return TileAnim.Still;
    }

    private void DrawCapsule(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        var theme = context.Theme;
        var undoLabel = Loc.T(L.Games.Undo);
        var tileLabel = GameNumber.Label(board.MaxTile);
        var undoWidth = Typography.Measure(undoLabel, CapsuleStyle).X / scale;
        var chipWidth = MathF.Max(ChipMinWidth, Typography.Measure(tileLabel, CapsuleStyle).X / scale + ChipPadX * 2f);
        var width = CapsulePadX * 2f + CapsuleIconSize + CapsuleIconGap + undoWidth + CapsuleSectionGap + chipWidth;
        context.Hud.Custom(width);
        var rect = context.Hud.CustomRect(0);
        if (rect.Width <= 0f)
        {
            return;
        }

        StageHud.Capsule(drawList, rect, scale);
        var canUndo = board.CanUndo && !finished && phase == Phase.Idle &&
                      context.Session.State == StageFlow.Playing;
        var dim = StageInks.Muted with { W = 0.45f };
        var ink = canUndo ? StageInks.Strong : dim;
        var iconSize = CapsuleIconSize * scale;
        var centerY = rect.Center.Y;
        var left = rect.Min.X + CapsulePadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, centerY), FontAwesomeIcon.Undo,
            canUndo ? Accent : dim, iconSize);
        left += iconSize + CapsuleIconGap * scale;
        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f), undoLabel,
            ink, CapsuleStyle);
        left += undoWidth * scale;
        var undoMax = new Vector2(left, rect.Max.Y);
        left += CapsuleSectionGap * scale;
        var chipMin = new Vector2(left, rect.Min.Y + ChipInsetY * scale);
        var chipMax = new Vector2(left + chipWidth * scale, rect.Max.Y - ChipInsetY * scale);
        var color = Twenty48Renderer.ColorFor(board.MaxTile);
        Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.3f, ImGui.GetColorU32(color));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, tileLabel, GamePalette.InkOn(color),
            CapsuleStyle);
        if (!canUndo || !UiInteract.HoverClick(rect.Min, undoMax))
        {
            return;
        }

        var grid = GameGrid.Centered(context.Safe, Twenty48Board.Size, Twenty48Board.Size,
            Twenty48Renderer.GapFraction);
        TryUndo(grid, scale);
    }

    private static string MilestoneLabel(int value)
    {
        var rank = 0;
        var scan = value;
        while (scan > 1 && rank < MaxRank)
        {
            scan >>= 1;
            rank++;
        }

        return MilestoneLabels[rank] ??= string.Concat(GameNumber.Label(value), "!");
    }
}
