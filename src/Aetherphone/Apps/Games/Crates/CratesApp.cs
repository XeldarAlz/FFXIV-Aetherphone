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

namespace Aetherphone.Apps.Games.Crates;

internal sealed class CratesApp : IMiniGame
{
    private const string GameId = "crates";
    private const string SurfaceId = "crates.board";
    private const float MaxPitch = 54f;
    private const float ToolbarHeight = 74f;
    private const float ToolRadius = 21f;
    private const float ToolSpacing = 92f;
    private const float StepSeconds = 0.11f;
    private const float AcceptFraction = 0.6f;
    private const float SwipeThreshold = 24f;
    private const float SquashDecay = 4.5f;
    private const float LandedDecay = 2.2f;
    private const float BumpDecay = 6f;
    private const float BumpReach = 0.14f;
    private const float CelebrateDelay = 0.16f;
    private const float FinishDelay = 1.6f;
    private const float WaveSeconds = 1.1f;
    private const float BannerSeconds = 1.6f;
    private const float HopHeight = 0.22f;
    private static readonly GameSpec StageSpec = new(GameId, L.Crates.Title, GameGenre.Puzzle, L.Crates.Hook,
        Backdrop.Slate, HudStyle.Standard, ScoreKind.Level, keyboard: true, levelCount: CratesLevels.Count);
    private static readonly Vector4 Silver = new(0.78f, 0.80f, 0.86f, 1f);
    private static readonly Vector4 Spark = new(1f, 0.95f, 0.70f, 1f);
    private static readonly Vector4[] ClearPalette =
    {
        CratesRenderer.Lit, CratesRenderer.Wood, CratesRenderer.Pompom, new(0.58f, 0.44f, 0.82f, 1f),
        new(0.42f, 0.74f, 0.96f, 1f),
    };

    private readonly CratesBoard board = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Vector2[] crateFrom = new Vector2[CratesBoard.MaxCrates];
    private readonly float[] crateTween = new float[CratesBoard.MaxCrates];
    private readonly float[] crateLanded = new float[CratesBoard.MaxCrates];
    private readonly bool[] landPending = new bool[CratesBoard.MaxCrates];
    private readonly CratesDirection[] path = new CratesDirection[CratesBoard.MaxCells];
    private LabelSlot levelLabel;
    private LabelPairSlot placedLabel;
    private CratesView view;
    private Vector2 playerFrom;
    private Vector2 swipeStart;
    private float playerTween = 1f;
    private float squash;
    private float bump;
    private float idleClock;
    private float wave;
    private float clearTimer;
    private float banner = 1f;
    private string bannerText = string.Empty;
    private Vector4 bannerTint;
    private CratesDirection bumpDirection;
    private int queued = -1;
    private int pathLength;
    private int pathIndex;
    private int level = 1;
    private int previewLevel;
    private int par;
    private bool squashHorizontal;
    private bool swipeActive;
    private bool swipeMoved;
    private bool cleared;
    private bool celebrated;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    private Vector2 PlayerPosition => new(board.PlayerColumn, board.PlayerRow);

    public void Start(in GameStart start)
    {
        Load(Math.Max(1, start.Level));
        ShowBanner(levelLabel.Get(L.Stage.LevelNumber, level), Accent);
    }

    public void Close()
    {
        previewLevel = 0;
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        var levelNumber = Math.Max(1, context.Session.Level);
        if (levelNumber != previewLevel)
        {
            Load(levelNumber);
        }

        var scale = UiScale.Current;
        idleClock += context.RawDeltaSeconds;
        Layout(context.Safe, scale, out var boardArea, out _);
        view = CratesView.Fit(boardArea, board.Columns, board.Rows, MaxPitch * scale);
        DrawScene(ImGui.GetWindowDrawList(), context, scale);
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        particles.Update(raw);
        fx.Update(raw);
        banner = GameBanner.Advance(banner, raw, BannerSeconds);
        var area = StageLayout.Punched(context.Safe, context.Fx.PlateScale).Translate(fx.ShakeOffset(scale));
        Layout(area, scale, out var boardArea, out var toolbar);
        view = CratesView.Fit(boardArea, board.Columns, board.Rows, MaxPitch * scale);
        Animate(raw, scale);
        var playing = context.Session.State == StageFlow.Playing && !cleared;
        if (playing)
        {
            HandleInput(context, scale);
            Advance(context, scale);
        }

        if (cleared && !finished)
        {
            AdvanceClear(context, scale);
        }

        DrawScene(drawList, context, scale);
        DrawToolbar(drawList, toolbar, context, playing, scale);
        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, view.Bounds.Center, bannerText, bannerTint, context.Theme, banner);
        DrawHud(context, drawList, scale);
    }

    private void Load(int levelNumber)
    {
        level = levelNumber;
        previewLevel = levelNumber;
        var data = CratesLevels.Get(levelNumber);
        board.Load(data);
        par = data.Par;
        particles.Clear();
        fx.Clear();
        ResetMotion();
        wave = 0f;
        clearTimer = 0f;
        banner = 1f;
        cleared = false;
        celebrated = false;
        finished = false;
    }

    private void ResetMotion()
    {
        playerFrom = PlayerPosition;
        playerTween = 1f;
        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            crateFrom[crate] = CratePosition(crate);
            crateTween[crate] = 1f;
            crateLanded[crate] = 0f;
            landPending[crate] = false;
        }

        squash = 0f;
        bump = 0f;
        queued = -1;
        pathLength = 0;
        pathIndex = 0;
        swipeActive = false;
    }

    private static void Layout(Rect area, float scale, out Rect boardArea, out Rect toolbar)
    {
        var band = ToolbarHeight * scale;
        var inset = BoardPlate.Padding * scale;
        boardArea = new Rect(area.Min + new Vector2(inset, inset),
            new Vector2(area.Max.X - inset, MathF.Max(area.Min.Y + inset, area.Max.Y - band - inset)));
        toolbar = new Rect(new Vector2(area.Min.X, area.Max.Y - band), area.Max);
    }

    private Vector2 CratePosition(int crate)
    {
        var cell = board.CrateCell(crate);
        return new Vector2(cell % board.Columns, cell / board.Columns);
    }

    private Vector2 DisplayPlayer() => Vector2.Lerp(playerFrom, PlayerPosition, Easing.EaseOutCubic(playerTween));

    private Vector2 DisplayCrate(int crate) =>
        Vector2.Lerp(crateFrom[crate], CratePosition(crate), Easing.EaseOutCubic(crateTween[crate]));

    private void Animate(float deltaSeconds, float scale)
    {
        idleClock += deltaSeconds;
        playerTween = MathF.Min(1f, playerTween + deltaSeconds / StepSeconds);
        squash = MathF.Max(0f, squash - deltaSeconds * SquashDecay);
        bump = MathF.Max(0f, bump - deltaSeconds * BumpDecay);
        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            crateLanded[crate] = MathF.Max(0f, crateLanded[crate] - deltaSeconds * LandedDecay);
            if (crateTween[crate] >= 1f)
            {
                continue;
            }

            crateTween[crate] = MathF.Min(1f, crateTween[crate] + deltaSeconds / StepSeconds);
            if (crateTween[crate] >= 1f && landPending[crate])
            {
                landPending[crate] = false;
                OnLanded(crate, scale);
            }
        }
    }

    private void HandleInput(in GameContext context, float scale)
    {
        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow, true))
        {
            Queue(CratesDirection.Up);
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow, true))
        {
            Queue(CratesDirection.Down);
        }

        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow, true))
        {
            Queue(CratesDirection.Left);
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow, true))
        {
            Queue(CratesDirection.Right);
        }

        if (GameInput.Pressed(ImGuiKey.Z, ImGuiKey.U, true) || GameInput.Pressed(ImGuiKey.Backspace, true))
        {
            Undo();
        }

        if (GameInput.Pressed(ImGuiKey.R))
        {
            Restart(context);
        }

        HandlePointer(context, scale);
    }

    private void HandlePointer(in GameContext context, float scale)
    {
        PressSurface.Claim(SurfaceId, view.Bounds, out var activated);
        var mouse = ImGui.GetMousePos();
        if (activated && !context.ChromeHit(mouse))
        {
            swipeActive = true;
            swipeMoved = false;
            swipeStart = mouse;
            return;
        }

        if (!swipeActive)
        {
            return;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var delta = mouse - swipeStart;
            var threshold = SwipeThreshold * scale;
            if (MathF.Abs(delta.X) < threshold && MathF.Abs(delta.Y) < threshold)
            {
                return;
            }

            if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
            {
                Queue(delta.X > 0f ? CratesDirection.Right : CratesDirection.Left);
            }
            else
            {
                Queue(delta.Y > 0f ? CratesDirection.Down : CratesDirection.Up);
            }

            swipeMoved = true;
            swipeStart = mouse;
            return;
        }

        swipeActive = false;
        if (!swipeMoved)
        {
            Tap(mouse);
        }
    }

    private void Tap(Vector2 mouse)
    {
        var local = (mouse - view.Origin) / MathF.Max(1f, view.Pitch);
        var column = (int)MathF.Floor(local.X);
        var row = (int)MathF.Floor(local.Y);
        var deltaColumn = column - board.PlayerColumn;
        var deltaRow = row - board.PlayerRow;
        if (Math.Abs(deltaColumn) + Math.Abs(deltaRow) == 1)
        {
            Queue(deltaColumn switch
            {
                1 => CratesDirection.Right,
                -1 => CratesDirection.Left,
                _ => deltaRow > 0 ? CratesDirection.Down : CratesDirection.Up,
            });
            return;
        }

        var length = board.PathTo(column, row, path);
        if (length > 0)
        {
            queued = -1;
            pathLength = length;
            pathIndex = 0;
            fx.Shockwave(view.Center(new Vector2(column, row)), view.Pitch * 0.45f, Accent, 0.35f, 2f);
            return;
        }

        if (board.TileAt(column, row) == CratesTile.Floor && length < 0)
        {
            fx.Shockwave(view.Center(new Vector2(column, row)), view.Pitch * 0.35f,
                StageInks.Strong with { W = 0.4f }, 0.3f, 1.6f);
        }
    }

    private void Queue(CratesDirection direction)
    {
        queued = (int)direction;
        pathLength = 0;
        pathIndex = 0;
    }

    private void Advance(in GameContext context, float scale)
    {
        if (playerTween < AcceptFraction)
        {
            return;
        }

        CratesDirection direction;
        var fromPath = false;
        if (queued >= 0)
        {
            direction = (CratesDirection)queued;
            queued = -1;
        }
        else if (pathIndex < pathLength)
        {
            direction = path[pathIndex++];
            fromPath = true;
        }
        else
        {
            return;
        }

        Perform(direction, fromPath, context, scale);
    }

    private void Perform(CratesDirection direction, bool fromPath, in GameContext context, float scale)
    {
        var before = DisplayPlayer();
        var result = board.Move(direction);
        if (result == CratesStep.Blocked)
        {
            bump = 1f;
            bumpDirection = direction;
            pathLength = 0;
            var ahead = CratesBoard.Step(direction);
            if (!fromPath && board.CrateAt(board.PlayerColumn + (int)ahead.X, board.PlayerRow + (int)ahead.Y) !=
                CratesBoard.NoCrate)
            {
                fx.AddTrauma(0.05f);
                UiFeedback.Play(UiSound.GameHitSoft);
            }

            return;
        }

        playerFrom = before;
        playerTween = 0f;
        if (result == CratesStep.Pushed)
        {
            OnPush(direction, scale);
        }

        if (board.Solved)
        {
            OnSolved(context);
        }
    }

    private void OnPush(CratesDirection direction, float scale)
    {
        var crate = board.LastCrate;
        var step = CratesBoard.Step(direction);
        crateFrom[crate] = CratePosition(crate) - step;
        crateTween[crate] = 0f;
        landPending[crate] = board.LastCrateLanded;
        squash = 1f;
        squashHorizontal = direction is CratesDirection.Left or CratesDirection.Right;
        UiFeedback.Play(UiSound.GamePiece);
        var trailing = view.Center(crateFrom[crate]) - step * view.Pitch * 0.42f + new Vector2(0f, view.Pitch * 0.3f);
        var backwards = MathF.Atan2(-step.Y, -step.X);
        particles.Emit(new ParticleSpec(CratesRenderer.Dust, CratesRenderer.Dust with { W = 0f }, view.Pitch * 0.09f,
            view.Pitch * 1.4f, 0.45f, -view.Pitch * 0.6f, 3.2f, curve: SizeCurve.Grow,
            direction: backwards, spread: 1.6f), trailing, 7);
        fx.AddTrauma(0.03f);
        if (board.LastCrateLeft)
        {
            UiFeedback.Play(UiSound.GameTick);
        }
    }

    private void OnLanded(int crate, float scale)
    {
        crateLanded[crate] = 1f;
        var center = view.Center(CratePosition(crate));
        UiFeedback.Play(UiSound.GameHitWood);
        fx.Shockwave(center, view.Pitch * 0.95f, CratesRenderer.Lit, 0.42f, 2.6f);
        particles.Sparkle(center, 10, Spark, view.Pitch * 2.6f, 2.4f * scale, 0.6f);
        if (board.Solved)
        {
            return;
        }

        fx.AddText(placedLabel.Get(L.Stage.StarsOf, board.CratesOnTargets, board.CrateCount),
            center - new Vector2(0f, view.Pitch * 0.55f), GamePalette.Lighten(Accent, 0.1f), 0.95f, 34f * scale);
    }

    private void Undo()
    {
        if (cleared)
        {
            return;
        }

        var before = DisplayPlayer();
        if (!board.Undo(out var direction))
        {
            return;
        }

        queued = -1;
        pathLength = 0;
        playerFrom = before;
        playerTween = 0f;
        var crate = board.LastCrate;
        if (crate != CratesBoard.NoCrate)
        {
            crateFrom[crate] = CratePosition(crate) + CratesBoard.Step(direction);
            crateTween[crate] = 0f;
            landPending[crate] = board.LastCrateLanded;
        }

        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private void Restart(in GameContext context)
    {
        if (cleared || board.Moves == 0)
        {
            return;
        }

        board.Restart();
        ResetMotion();
        UiFeedback.Play(UiSound.GameShuffle);
        fx.AddTrauma(0.12f);
        context.Fx.Punch(0.03f);
        ShowBanner(levelLabel.Get(L.Stage.LevelNumber, level), Accent);
    }

    private void OnSolved(in GameContext context)
    {
        cleared = true;
        celebrated = false;
        clearTimer = 0f;
        queued = -1;
        pathLength = 0;
        context.Fx.SlowMo(0.6f, 0.25f);
    }

    private void AdvanceClear(in GameContext context, float scale)
    {
        clearTimer += context.DeltaSeconds;
        if (celebrated)
        {
            wave = MathF.Min(1f, wave + context.DeltaSeconds / WaveSeconds);
        }

        if (!celebrated && clearTimer >= CelebrateDelay)
        {
            Celebrate(context, scale);
        }

        if (clearTimer < FinishDelay || finished)
        {
            return;
        }

        finished = true;
        var stars = CratesBoard.Stars(board.Moves, par);
        context.Session.Finish(new GameOutcome(board.Moves, ScoreKind.Level, GameId).WithStars(stars)
            .WithStat(L.Crates.Moves, GameNumber.Label(board.Moves))
            .WithStat(L.Crates.Pushes, GameNumber.Label(board.Pushes))
            .WithStat(L.Crates.Par, GameNumber.Label(par))
            .WithStat(L.Crates.Undos, GameNumber.Label(board.Undos)));
    }

    private void Celebrate(in GameContext context, float scale)
    {
        celebrated = true;
        wave = 0.0001f;
        GameSfx.LevelClear();
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
        context.Fx.Flash(CratesRenderer.Lit, 0.18f);
        fx.AddTrauma(0.15f);
        var center = view.Bounds.Center;
        particles.Confetti(new Vector2(center.X, view.Bounds.Min.Y), 90, ClearPalette, 320f * scale, 4.2f * scale, 1.5f);
        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            crateLanded[crate] = 1f;
            particles.Sparkle(view.Center(CratePosition(crate)), 8, Spark, view.Pitch * 2.2f, 2.6f * scale, 0.7f);
        }

        fx.Shockwave(view.Center(PlayerPosition), view.Pitch * 3f, Accent, 0.6f, 3f);
        ShowBanner(Loc.T(board.Moves <= par ? L.Crates.Perfect : L.Crates.Solved), CratesRenderer.Lit);
    }

    private void ShowBanner(string text, Vector4 tint)
    {
        bannerText = text;
        bannerTint = tint;
        banner = 0f;
    }

    private void DrawScene(ImDrawListPtr drawList, in GameContext context, float scale)
    {
        CratesRenderer.DrawBoard(drawList, board, view, wave, Accent, context.Backdrop.Ink, scale);
        var player = DisplayPlayer();
        if (bump > 0f)
        {
            player += CratesBoard.Step(bumpDirection) * BumpReach * MathF.Sin(bump * MathF.PI);
        }

        if (cleared && celebrated)
        {
            player.Y -= HopHeight * MathF.Abs(MathF.Sin(clearTimer * 9f)) * (1f - wave * 0.5f);
        }

        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            var position = DisplayCrate(crate);
            if (position.Y <= player.Y)
            {
                DrawCrate(drawList, crate, position, scale);
            }
        }

        var bob = idleClock * 2.2f;
        CratesRenderer.DrawMoogle(drawList, view.Center(player), view.Pitch, board.Facing, squash, squashHorizontal,
            MathF.Sin(bob));
        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            var position = DisplayCrate(crate);
            if (position.Y > player.Y)
            {
                DrawCrate(drawList, crate, position, scale);
            }
        }
    }

    private void DrawCrate(ImDrawListPtr drawList, int crate, Vector2 position, float scale)
    {
        var onTarget = board.CrateOnTarget(crate) && crateTween[crate] >= 1f;
        CratesRenderer.DrawCrate(drawList, view.Center(position), view.Pitch, onTarget, crateLanded[crate], scale);
    }

    private void DrawToolbar(ImDrawListPtr drawList, Rect toolbar, in GameContext context, bool interactive,
        float scale)
    {
        var center = new Vector2(toolbar.Center.X, toolbar.Min.Y + (ToolRadius + 8f) * scale);
        var spacing = ToolSpacing * scale * 0.5f;
        var radius = ToolRadius * scale;
        if (CratesRenderer.ToolButton(drawList, center - new Vector2(spacing, 0f), radius, FontAwesomeIcon.Undo,
                Loc.T(L.Games.Undo), Accent, interactive && board.CanUndo, scale))
        {
            Undo();
        }

        if (CratesRenderer.ToolButton(drawList, center + new Vector2(spacing, 0f), radius, FontAwesomeIcon.RedoAlt,
                Loc.T(L.Stage.Restart), Accent, interactive && board.Moves > 0, scale))
        {
            Restart(context);
        }
    }

    private void DrawHud(in GameContext context, ImDrawListPtr drawList, float scale)
    {
        var hud = context.Hud;
        hud.Score(board.Moves, L.Crates.Moves);
        hud.Level(level);
        var pushesLabel = GameNumber.Label(board.Pushes);
        var parLabel = GameNumber.Label(par);
        hud.Custom(StatCapsule.Width(pushesLabel, scale));
        hud.Custom(StatCapsule.Width(parLabel, scale));
        if (hud.CustomPlaced(0))
        {
            StatCapsule.Draw(drawList, hud.CustomRect(0), FontAwesomeIcon.BoxOpen, pushesLabel, CratesRenderer.Wood,
                scale);
        }

        if (hud.CustomPlaced(1))
        {
            var stars = CratesBoard.Stars(board.Moves, par);
            var ink = stars == 3 ? GamePalette.Star : stars == 2 ? Silver : Silver with { W = 0.4f };
            StatCapsule.Draw(drawList, hud.CustomRect(1), FontAwesomeIcon.Star, parLabel, ink, scale);
        }

        context.Session.Report(board.Moves);
    }
}
