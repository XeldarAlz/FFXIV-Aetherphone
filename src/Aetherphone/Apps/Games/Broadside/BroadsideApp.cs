using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Broadside;

internal enum BroadsideStage : byte
{
    Placing,
    Aiming,
    Flying,
    Resolving,
    Swapping,
    Thinking,
    Over,
}

internal sealed class BroadsideApp : IMiniGame
{
    private const string GameId = "broadside";
    private const string AimSurfaceId = "broadside.aim";
    private const int HardMode = 1;
    private const int CellCount = BroadsideFleet.CellCount;
    private const int ShipCount = BroadsideFleet.ShipCount;
    private const int StartCursor = 44;
    private const ulong IdleSeed = 21;
    private const ulong AiSalt = 0xB0A7B0A7UL;
    private const float HotSeatHold = 1.2f;
    private const float AimSeconds = 0.8f;
    private const float SwapSmooth = 0.13f;
    private const float BattleSmooth = 0.2f;
    private const float SwapSettle = 0.02f;
    private const float OverSeconds = 2.3f;
    private const float DemoGap = 0.32f;
    private const float DemoOverSeconds = 2.6f;
    private const float BannerSeconds = 1.7f;
    private static readonly LocString[] Modes = { L.Games.Easy, L.Games.Hard };
    private static readonly GameSpec StageSpec = new(GameId, L.Broadside.Title, GameGenre.Strategy, L.Broadside.Hook,
        Backdrop.Neon, HudStyle.Standard, ScoreKind.Streak, Modes, keyboard: true, seats: BroadsideBoard.Players,
        modesSoloOnly: true);

    private readonly BroadsideBoard board = new();
    private readonly BroadsideAi ai = new();
    private readonly BroadsideLayout layout = new();
    private readonly BroadsidePlacement placement = new();
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly float[] markAge = new float[CellCount * BroadsideBoard.Players];
    private readonly float[] sinkAge = new float[ShipCount * BroadsideBoard.Players];
    private readonly string[] shipDown = new string[ShipCount];
    private readonly string[] fireLines = new string[BroadsideBoard.Players];
    private readonly Vector4[] confetti;
    private GameRandom aiRandom = GameRandom.FromSeed(IdleSeed);
    private GameRandom placeRandom = GameRandom.FromSeed(IdleSeed);
    private LanguageInfo? textLanguage;
    private LabelSlot accuracyLabel;
    private Spring swap;
    private Spring battle;
    private BroadsideStage stage;
    private ulong demoSeed = IdleSeed;
    private int viewer;
    private int placingSeat;
    private int pendingHandoff = -1;
    private int shooter;
    private int shotCell = -1;
    private int shotFleet;
    private int aimCell = -1;
    private int aimFromCell = -1;
    private int hoverCell = -1;
    private int cursorCell = StartCursor;
    private float swapTarget;
    private float battleTarget;
    private float shotProgress;
    private float aimProgress;
    private float holdTimer;
    private float overTimer;
    private float clock;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private Vector2 bannerCenter;
    private bool cursorShown;
    private bool shotFromTop;
    private bool hotSeat;
    private bool hard;
    private bool demo;
    private bool finished;
    private bool revealed;

    public BroadsideApp()
    {
        confetti = new[] { AppAccents.For(GameId), BroadsideArt.Flame, BroadsideArt.Cloud, BroadsideArt.Brass };
        ResetDemo();
    }

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        demo = false;
        finished = false;
        hotSeat = start.HotSeat;
        hard = StageSpec.ClampMode(start.Mode) == HardMode;
        aiRandom = GameRandom.FromSeed(start.Seed ^ AiSalt);
        placeRandom = start.Random;
        board.Reset();
        if (!hotSeat)
        {
            board.Fleet(1).AutoPlace(ref placeRandom);
        }

        placingSeat = 0;
        viewer = 0;
        stage = BroadsideStage.Placing;
        battle.SnapTo(0f);
        battleTarget = 0f;
        swap.SnapTo(0f);
        swapTarget = 0f;
        pendingHandoff = hotSeat ? 0 : -1;
        ResetEffects();
        textLanguage = null;
    }

    public void Close()
    {
        ResetDemo();
    }

    public void Dispose()
    {
    }

    public void DrawIdle(in GameContext context)
    {
        Frame(context, context.RawDeltaSeconds, false);
    }

    public void Draw(in GameContext context)
    {
        if (!demo && pendingHandoff >= 0 && context.Session.Handoff(pendingHandoff))
        {
            pendingHandoff = -1;
        }

        Frame(context, demo ? context.RawDeltaSeconds : context.DeltaSeconds,
            !demo && context.Session.State == StageFlow.Playing);
        if (demo)
        {
            return;
        }

        context.Hud.Score(board.Hits(viewer), L.Broadside.Hits);
        if (!hotSeat)
        {
            context.Hud.Best(context.Session.Best);
        }

        context.Session.Report(board.Hits(0));
    }

    private void ResetDemo()
    {
        demo = true;
        hotSeat = false;
        hard = true;
        finished = false;
        aiRandom = GameRandom.FromSeed(demoSeed ^ AiSalt);
        placeRandom = GameRandom.FromSeed(demoSeed);
        board.Reset();
        board.Fleet(0).AutoPlace(ref placeRandom);
        board.Fleet(1).AutoPlace(ref placeRandom);
        viewer = 0;
        placingSeat = 0;
        pendingHandoff = -1;
        battle.SnapTo(1f);
        battleTarget = 1f;
        swap.SnapTo(0f);
        swapTarget = 0f;
        ResetEffects();
        stage = BroadsideStage.Aiming;
        holdTimer = DemoGap;
    }

    private void ResetEffects()
    {
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        Array.Fill(markAge, -1f);
        Array.Fill(sinkAge, -1f);
        placement.Reset();
        bannerProgress = 1f;
        shotCell = -1;
        aimCell = -1;
        aimFromCell = -1;
        hoverCell = -1;
        cursorCell = StartCursor;
        cursorShown = false;
        holdTimer = 0f;
        overTimer = 0f;
        revealed = false;
    }

    private void Frame(in GameContext context, float tick, bool interactive)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var raw = context.RawDeltaSeconds;
        SyncText();
        clock += raw;
        particles.Update(raw);
        fx.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        Age(raw);
        var area = demo ? context.Safe : StageLayout.Punched(context.Safe, context.Fx.PlateScale);
        layout.Build(area, scale);
        swap.Step(swapTarget, SwapSmooth, raw);
        battle.Step(battleTarget, BattleSmooth, raw);
        var shake = fx.ShakeOffset(scale);
        var blend = Easing.Clamp01(battle.Value);
        var home = BroadsideLayout.Lerp(layout.PlaceGrid,
            BroadsideLayout.Lerp(layout.Mini, layout.Big, swap.Value), blend).Translate(shake);
        var target = BroadsideLayout.Lerp(layout.Big, layout.Mini, swap.Value)
            .Translate(new Vector2((1f - blend) * (area.Width + 40f * scale), 0f) + shake);
        var placing = stage == BroadsideStage.Placing;
        if (!finished)
        {
            Step(context, tick, scale, interactive, home, target);
        }

        if (blend > 0.01f)
        {
            BroadsideArt.DrawGridLabel(drawList, target, Loc.T(L.Broadside.EnemySkies), scale, blend);
            DrawFleet(drawList, target, BroadsideBoard.Opponent(viewer), demo, revealed, context, scale, blend);
        }

        BroadsideArt.DrawGridLabel(drawList, home, HomeLabel(), scale, 1f);
        DrawFleet(drawList, home, viewer, true, false, context, scale, 1f);
        DrawReticles(drawList, target, home, scale, interactive);
        DrawShot(drawList, context, home, target, scale);
        if (blend > 0.5f)
        {
            DrawPanel(drawList, blend, scale);
        }

        if (placing && !demo)
        {
            var fleet = board.Fleet(placingSeat);
            var action = placement.DrawControls(drawList, fleet, layout, context.Theme, Accent, clock, scale,
                1f - blend, interactive, true);
            placement.DrawGhost(drawList, fleet, home, Accent, clock, scale);
            if (action == PlacementAction.Auto)
            {
                placement.AutoPlace(fleet, ref placeRandom);
            }
            else if (action == PlacementAction.Ready)
            {
                Ready(context);
            }
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, stage == BroadsideStage.Over ? area.Center : bannerCenter, bannerText, bannerColor,
            context.Theme, bannerProgress, TextStyles.Title1);
    }

    private void SyncText()
    {
        if (ReferenceEquals(textLanguage, Loc.Current))
        {
            return;
        }

        textLanguage = Loc.Current;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            shipDown[ship] = Loc.T(L.Broadside.ShipDown, Loc.T(BroadsideArt.ShipNames[ship]));
        }

        for (var seat = 0; seat < BroadsideBoard.Players; seat++)
        {
            fireLines[seat] = Loc.T(L.Broadside.ToFire, GameSeats.Name(seat));
        }
    }

    private void Age(float raw)
    {
        for (var index = 0; index < markAge.Length; index++)
        {
            if (markAge[index] >= 0f)
            {
                markAge[index] += raw;
            }
        }

        for (var index = 0; index < sinkAge.Length; index++)
        {
            if (sinkAge[index] >= 0f)
            {
                sinkAge[index] += raw;
            }
        }

        placement.Age(raw);
    }

    private void Step(in GameContext context, float tick, float scale, bool interactive, Rect home, Rect target)
    {
        switch (stage)
        {
            case BroadsideStage.Placing:
                if (interactive)
                {
                    placement.Handle(board.Fleet(placingSeat), layout, home, context.ChromeHit(ImGui.GetMousePos()),
                        scale, particles, fx);
                }

                return;
            case BroadsideStage.Aiming:
                if (demo)
                {
                    holdTimer -= tick;
                    if (holdTimer <= 0f)
                    {
                        var turn = board.Turn;
                        FireShot(turn, ai.ChooseShot(board.Fleet(BroadsideBoard.Opponent(turn)), true, ref aiRandom));
                    }

                    return;
                }

                hoverCell = -1;
                if (interactive && pendingHandoff < 0)
                {
                    HandleAim(context, target, scale);
                }

                return;
            case BroadsideStage.Flying:
                shotProgress += tick / BroadsideJuice.FlightSeconds;
                if (shotProgress >= 1f)
                {
                    Impact(context, scale, home, target);
                }

                return;
            case BroadsideStage.Resolving:
                holdTimer -= tick;
                if (holdTimer <= 0f)
                {
                    AfterShot(context, scale);
                }

                return;
            case BroadsideStage.Swapping:
                if (tick > 0f && MathF.Abs(swap.Value - swapTarget) < SwapSettle)
                {
                    if (swapTarget > 0.5f)
                    {
                        BeginAiAim();
                    }
                    else
                    {
                        stage = BroadsideStage.Aiming;
                        Sound(UiSound.GameTick);
                    }
                }

                return;
            case BroadsideStage.Thinking:
                aimProgress = MathF.Min(1f, aimProgress + tick / AimSeconds);
                if (aimProgress >= 1f)
                {
                    FireShot(1, aimCell);
                }

                return;
            case BroadsideStage.Over:
                overTimer += tick;
                if (demo && overTimer >= DemoOverSeconds)
                {
                    demoSeed++;
                    ResetDemo();
                }
                else if (!demo && overTimer >= OverSeconds)
                {
                    FinishGame(context);
                }

                return;
            default:
                return;
        }
    }

    private void HandleAim(in GameContext context, Rect target, float scale)
    {
        var fleet = board.Fleet(BroadsideBoard.Opponent(viewer));
        if (MoveCursor())
        {
            cursorShown = true;
        }

        if (cursorShown && GameInput.Pressed(ImGuiKey.Space, ImGuiKey.Enter))
        {
            TryFire(fleet, cursorCell);
            return;
        }

        PressSurface.Claim(AimSurfaceId, target, out _);
        var mouse = ImGui.GetMousePos();
        if (!UiInteract.Hover(target.Min, target.Max) || context.ChromeHit(mouse) ||
            !BroadsideLayout.CellAt(target, mouse, out var column, out var row))
        {
            return;
        }

        var cell = BroadsideFleet.CellOf(column, row);
        hoverCell = cell;
        if (ImGui.GetIO().MouseDelta.LengthSquared() > 0f)
        {
            cursorShown = false;
            cursorCell = cell;
        }

        var rect = BroadsideLayout.CellRect(target, column, row);
        if (fleet.MarkAt(cell) == CellMark.None)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, rect.Max, true, false))
        {
            TryFire(fleet, cell);
        }
    }

    private bool MoveCursor()
    {
        var column = BroadsideFleet.ColumnOf(cursorCell);
        var row = BroadsideFleet.RowOf(cursorCell);
        var moved = false;
        if (GameInput.Pressed(ImGuiKey.A, ImGuiKey.LeftArrow, true))
        {
            column--;
            moved = true;
        }

        if (GameInput.Pressed(ImGuiKey.D, ImGuiKey.RightArrow, true))
        {
            column++;
            moved = true;
        }

        if (GameInput.Pressed(ImGuiKey.W, ImGuiKey.UpArrow, true))
        {
            row--;
            moved = true;
        }

        if (GameInput.Pressed(ImGuiKey.S, ImGuiKey.DownArrow, true))
        {
            row++;
            moved = true;
        }

        if (!moved)
        {
            return false;
        }

        cursorCell = BroadsideFleet.CellOf(Math.Clamp(column, 0, BroadsideFleet.Size - 1),
            Math.Clamp(row, 0, BroadsideFleet.Size - 1));
        Sound(UiSound.GameTick);
        return true;
    }

    private void TryFire(BroadsideFleet fleet, int cell)
    {
        if (fleet.MarkAt(cell) != CellMark.None)
        {
            Sound(UiSound.GameWrong);
            fx.AddTrauma(0.06f);
            return;
        }

        FireShot(viewer, cell);
    }

    private void FireShot(int seat, int cell)
    {
        if (cell < 0)
        {
            return;
        }

        shooter = seat;
        shotCell = cell;
        shotFleet = BroadsideBoard.Opponent(seat);
        shotProgress = 0f;
        shotFromTop = demo ? seat == 1 : seat != viewer;
        ribbon.Clear();
        stage = BroadsideStage.Flying;
        Sound(UiSound.GameShoot);
    }

    private void Impact(in GameContext context, float scale, Rect home, Rect target)
    {
        var result = board.Fire(shotCell, out var ship);
        var rect = shotFleet == viewer ? home : target;
        var center = BroadsideLayout.CellCenter(rect, shotCell);
        var pitch = BroadsideLayout.Pitch(rect);
        var mine = shotFleet == viewer && !demo;
        markAge[shotFleet * CellCount + shotCell] = 0f;
        ribbon.Clear();
        switch (result)
        {
            case ShotResult.Miss:
                BroadsideJuice.Splash(particles, fx, center, pitch, scale);
                Sound(UiSound.GamePop);
                holdTimer = BroadsideJuice.MissHold;
                break;
            case ShotResult.Hit:
                BroadsideJuice.Explode(particles, fx, context.Fx, !demo, center, pitch, mine, false);
                BroadsideJuice.Struck(fx, center, pitch, scale);
                holdTimer = BroadsideJuice.HitHold;
                break;
            case ShotResult.Sunk:
                BroadsideJuice.Explode(particles, fx, context.Fx, !demo, center, pitch, mine, true);
                sinkAge[shotFleet * ShipCount + ship] = 0f;
                SinkBurst(rect, shotFleet, ship, pitch);
                Banner(shipDown[Math.Max(0, ship)], mine ? BroadsideArt.Danger : BroadsideArt.Flame, rect.Center);
                holdTimer = BroadsideJuice.SunkHold;
                if (!demo)
                {
                    context.Fx.SlowMo(0.5f, 0.4f);
                    if (!mine)
                    {
                        context.Fx.Sweep();
                    }
                }

                break;
            default:
                stage = BroadsideStage.Aiming;
                return;
        }

        if (hotSeat && !board.Over)
        {
            holdTimer = MathF.Max(holdTimer, HotSeatHold);
        }

        stage = BroadsideStage.Resolving;
    }

    private void SinkBurst(Rect grid, int fleetIndex, int ship, float pitch)
    {
        Span<int> cells = stackalloc int[BroadsideFleet.LongestShip];
        var count = board.Fleet(fleetIndex).ShipCellsOf(ship, cells);
        BroadsideJuice.SinkBurst(particles, fx, grid, cells[..count], pitch, !demo);
    }

    private void AfterShot(in GameContext context, float scale)
    {
        if (board.Over)
        {
            stage = BroadsideStage.Over;
            overTimer = 0f;
            EndBattle(context, scale);
            return;
        }

        if (demo)
        {
            stage = BroadsideStage.Aiming;
            holdTimer = DemoGap;
            return;
        }

        if (hotSeat)
        {
            viewer = board.Turn;
            RequestHandoff(context, viewer);
            swap.SnapTo(0f);
            swapTarget = 0f;
            cursorShown = false;
            stage = BroadsideStage.Aiming;
            return;
        }

        swapTarget = shooter == 0 ? 1f : 0f;
        stage = BroadsideStage.Swapping;
        Sound(UiSound.GameCardFlip);
    }

    private void BeginAiAim()
    {
        aimFromCell = aimCell;
        aimCell = ai.ChooseShot(board.Fleet(0), hard, ref aiRandom);
        aimProgress = 0f;
        stage = BroadsideStage.Thinking;
        Sound(UiSound.GameTick);
    }

    private void EndBattle(in GameContext context, float scale)
    {
        var winner = board.Winner;
        var won = hotSeat || demo || winner == 0;
        revealed = true;
        var line = hotSeat || demo
            ? GameSeats.WinLine(Math.Max(0, winner))
            : Loc.T(winner == 0 ? L.Broadside.Victory : L.Broadside.Defeat);
        Banner(line, won ? BroadsideArt.Flame : BroadsideArt.Danger, layout.Area.Center);
        if (demo)
        {
            return;
        }

        if (!won)
        {
            Sound(UiSound.GameWrong);
            context.Fx.Vignette(BroadsideArt.Danger, 0.35f, 1.2f);
            return;
        }

        Sound(UiSound.GameClear);
        particles.Confetti(new Vector2(layout.Area.Center.X, layout.Area.Min.Y), 90, confetti, 320f * scale, 4f, 1.6f);
        context.Fx.Sweep();
        context.Fx.Punch(0.05f);
    }

    private void FinishGame(in GameContext context)
    {
        finished = true;
        var winner = Math.Max(0, board.Winner);
        var focus = hotSeat ? winner : 0;
        var opponent = BroadsideBoard.Opponent(focus);
        var outcome = hotSeat
            ? GameOutcome.Unranked().WithWinner(winner)
            : new GameOutcome(0, ScoreKind.Streak, GameId, winner == 0);
        context.Session.Finish(outcome
            .WithStat(L.Broadside.Shots, GameNumber.Label(board.Shots(focus)))
            .WithStat(L.Broadside.Accuracy, accuracyLabel.Get(L.Broadside.Percent, board.Accuracy(focus)))
            .WithStat(L.Broadside.ShipsSunk, GameNumber.Label(ShipCount - board.Fleet(opponent).ShipsAfloat))
            .WithStat(L.Broadside.ShipsLost, GameNumber.Label(ShipCount - board.Fleet(focus).ShipsAfloat)));
    }

    private void Ready(in GameContext context)
    {
        if (!board.Fleet(placingSeat).AllPlaced)
        {
            return;
        }

        placement.Release();
        Sound(UiSound.GamePowerUp);
        if (hotSeat && placingSeat == 0)
        {
            placingSeat = 1;
            viewer = 1;
            RequestHandoff(context, 1);
            return;
        }

        viewer = hotSeat ? board.Turn : 0;
        if (hotSeat)
        {
            RequestHandoff(context, viewer);
        }

        battleTarget = 1f;
        stage = BroadsideStage.Aiming;
        Banner(hotSeat ? fireLines[viewer] : Loc.T(L.Games.YourTurn), Accent, layout.Big.Center);
        if (!demo)
        {
            context.Fx.Sweep();
        }
    }

    private void RequestHandoff(in GameContext context, int seat)
    {
        pendingHandoff = context.Session.Handoff(seat) ? -1 : seat;
    }

    private void Banner(string text, Vector4 color, Vector2 center)
    {
        bannerText = text;
        bannerColor = color;
        bannerCenter = center;
        bannerProgress = 0f;
    }

    private void Sound(UiSound sound)
    {
        if (!demo)
        {
            UiFeedback.Play(sound);
        }
    }

    private string HomeLabel()
    {
        if (stage == BroadsideStage.Placing && hotSeat)
        {
            return GameSeats.Name(placingSeat);
        }

        return Loc.T(L.Broadside.YourFleet);
    }

    private void DrawFleet(ImDrawListPtr drawList, Rect grid, int fleetIndex, bool showShips, bool ghostRemaining,
        in GameContext context, float scale, float alpha)
    {
        var fleet = board.Fleet(fleetIndex);
        var compact = grid.Width < layout.Big.Width * 0.7f;
        BroadsideArt.DrawSky(drawList, grid, Accent, context.Backdrop.Ink, scale, clock, compact, alpha);
        var pitch = BroadsideLayout.Pitch(grid);
        var own = fleetIndex == viewer;
        var placing = stage == BroadsideStage.Placing && fleetIndex == placingSeat;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            if (!fleet.IsPlaced(ship) || (placing && placement.Lifted(ship)))
            {
                continue;
            }

            var sunk = fleet.IsSunk(ship);
            if (!showShips && !sunk && !ghostRemaining)
            {
                continue;
            }

            var shipRect = BroadsideLayout.ShipRect(grid, fleet.Column(ship), fleet.Row(ship),
                BroadsideFleet.Length(ship), fleet.Horizontal(ship));
            var age = sinkAge[fleetIndex * ShipCount + ship];
            var sink = sunk ? (age < 0f ? 1f : Easing.Clamp01(age / BroadsideJuice.SinkSeconds)) : -1f;
            var pop = placing ? placement.Pop(ship) : 1f;
            if (pop < 0f)
            {
                continue;
            }

            if (pop < 1f)
            {
                shipRect = shipRect.Scaled(0.7f + 0.3f * GameJuice.PopIn(pop));
            }

            var lift = placing && ship == placement.DragShip ? 3f * scale : 0f;
            var shipAlpha = !showShips && !sunk ? 0.45f : alpha;
            BroadsideArt.DrawAirship(drawList, shipRect, fleet.Horizontal(ship), own ? BroadsideArt.Hull : BroadsideArt.EnemyHull,
                scale, clock, sink, shipAlpha, placing ? placement.Warn(ship) : 0f, lift);
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            var mark = fleet.MarkAt(cell);
            if (mark == CellMark.None)
            {
                continue;
            }

            var center = BroadsideLayout.CellCenter(grid, cell);
            var age = markAge[fleetIndex * CellCount + cell];
            if (mark == CellMark.Miss)
            {
                BroadsideArt.DrawPuff(drawList, center, pitch, clock, cell, age);
            }
            else
            {
                BroadsideArt.DrawFire(drawList, center, pitch, clock, cell, age);
            }
        }

        if (fleet.LastShot < 0 || stage == BroadsideStage.Placing)
        {
            return;
        }

        BroadsideArt.DrawLastShot(drawList, BroadsideLayout.CellCenter(grid, fleet.LastShot), pitch, scale);
    }

    private void DrawReticles(ImDrawListPtr drawList, Rect target, Rect home, float scale, bool interactive)
    {
        var pitch = BroadsideLayout.Pitch(target);
        if (stage == BroadsideStage.Aiming && !demo && interactive && pendingHandoff < 0)
        {
            var fleet = board.Fleet(BroadsideBoard.Opponent(viewer));
            var cell = cursorShown ? cursorCell : hoverCell;
            if (cell >= 0)
            {
                var open = fleet.MarkAt(cell) == CellMark.None;
                var color = open ? Accent : BroadsideArt.Disabled;
                StageCell.Draw(drawList, BroadsideLayout.CellRect(target, BroadsideFleet.ColumnOf(cell),
                        BroadsideFleet.RowOf(cell)).Inset(pitch * 0.06f), color with { W = open ? 0.35f : 0.12f },
                    CellDepth.Raised, pitch * 0.16f, scale);
                BroadsideArt.DrawReticle(drawList, BroadsideLayout.CellCenter(target, cell), pitch, color, clock, scale);
            }
        }

        if (stage != BroadsideStage.Thinking || aimCell < 0)
        {
            return;
        }

        var homePitch = BroadsideLayout.Pitch(home);
        var start = aimFromCell >= 0 ? BroadsideLayout.CellCenter(home, aimFromCell) : home.Center;
        var eased = Easing.EaseInOutCubic(aimProgress);
        var wander = new Vector2(MathF.Sin(clock * 5f), MathF.Cos(clock * 4f)) * homePitch * 0.4f * (1f - eased);
        var position = Vector2.Lerp(start, BroadsideLayout.CellCenter(home, aimCell), eased) + wander;
        BroadsideArt.DrawReticle(drawList, position, homePitch, BroadsideArt.Danger, clock, scale);
    }

    private void DrawShot(ImDrawListPtr drawList, in GameContext context, Rect home, Rect target, float scale)
    {
        if (stage != BroadsideStage.Flying || shotCell < 0)
        {
            BroadsideJuice.DrawTrail(drawList, ribbon, scale);
            return;
        }

        var rect = shotFleet == viewer ? home : target;
        var destination = BroadsideLayout.CellCenter(rect, shotCell);
        var origin = new Vector2(layout.Area.Center.X, shotFromTop ? context.Full.Min.Y : context.Full.Max.Y);
        BroadsideJuice.DrawFlight(drawList, ribbon, particles, origin, destination, shotProgress, scale);
    }

    private void DrawPanel(ImDrawListPtr drawList, float alpha, float scale)
    {
        var enemy = board.Fleet(BroadsideBoard.Opponent(viewer));
        var sunkMask = 0;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            if (enemy.IsSunk(ship))
            {
                sunkMask |= 1 << ship;
            }
        }

        BroadsideArt.DrawFleetPanel(drawList, layout.Panel, BroadsideLayout.Pitch(layout.Big), sunkMask, StatusText(),
            stage == BroadsideStage.Aiming ? Accent : BroadsideArt.Muted, alpha, scale);
    }

    private string StatusText()
    {
        switch (stage)
        {
            case BroadsideStage.Aiming:
                if (demo)
                {
                    return string.Empty;
                }

                return hotSeat ? fireLines[viewer] : Loc.T(L.Broadside.TapToFire);
            case BroadsideStage.Thinking:
                return Loc.T(L.Games.Thinking);
            case BroadsideStage.Swapping:
            case BroadsideStage.Flying:
            case BroadsideStage.Resolving:
                return demo ? string.Empty : Loc.T(shooter == viewer ? L.Games.YourTurn : L.Broadside.EnemyTurn);
            default:
                return string.Empty;
        }
    }
}
