using Aetherphone.Apps.Games.Broadside;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Online;

// Every shot is replayed from LastCell and LastResult, facts the server already settled: the table
// flies the shell, holds the mark back until it lands, and never decides a hit itself.
internal sealed class OnlineBroadsideTable
{
    internal const long NotObserved = -1;
    private const string AccentId = "broadside";
    private const string AimSurfaceId = "broadside.online.aim";
    private const int Seats = 2;
    private const int CellCount = GameRoomWire.BroadsideCellCount;
    private const int ShipCount = GameRoomWire.BroadsideShipCount;
    private const int NoCell = -1;
    private const int NoShip = -1;
    private const float SwapSmooth = 0.13f;
    private const float BattleSmooth = 0.2f;
    private const float BannerSeconds = 1.7f;
    private const float SeatRowHeight = 30f;
    private const float FooterHeight = 40f;
    private const float EdgePadding = 12f;
    private const float SeatIconRadius = 10f;
    private const float ResignWidth = 110f;
    private const float ResignHeight = 30f;
    private const long SendGraceMilliseconds = 1_500;
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);
    private static readonly Vector4 ReadyInk = new(0.45f, 0.86f, 0.55f, 1f);

    private readonly GameRoomsStore store;
    private readonly BroadsideLayout layout = new();
    private readonly BroadsidePlacement placement = new();
    private readonly BroadsideFleet draft = new();
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx screen;
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private readonly Ribbon ribbon = new();
    private readonly float[] markAge = new float[Seats * CellCount];
    private readonly float[] sinkAge = new float[Seats * ShipCount];
    private readonly int[] shipCell = new int[Seats * ShipCount];
    private readonly bool[] shipAcross = new bool[Seats * ShipCount];
    private readonly bool[] shipSunk = new bool[Seats * ShipCount];
    private readonly int[] lastShotOn = new int[Seats];
    private readonly string[] shipDown = new string[ShipCount];
    private readonly string[] seatNames = new string[Seats];
    private readonly Vector4[] confetti;
    private GameRandom placeRandom = GameRandom.Fresh();
    private LanguageInfo? textLanguage;
    private BroadsidePlayerDto[]? labeledPlayers;
    private int labeledViewer = -1;
    private string theirTurn = string.Empty;
    private string waitingFleet = string.Empty;
    private Spring swap;
    private Spring battle;
    private float swapTarget;
    private float battleTarget;
    private long seenRound = NotObserved;
    private int seenShots;
    private bool seenPlacing;
    private bool endShown;
    private bool flying;
    private bool holding;
    private int flightSeat;
    private int flightCell = NoCell;
    private int flightShip = NoShip;
    private string flightResult = string.Empty;
    private bool flightFromTop;
    private float flightProgress;
    private float holdTimer;
    private int hoverCell = NoCell;
    private int firedCell = NoCell;
    private long sentRound = NotObserved;
    private long sentAtTick;
    private float clock;
    private float bannerProgress = 1f;
    private string bannerText = string.Empty;
    private Vector4 bannerColor;
    private Vector2 bannerCenter;

    public OnlineBroadsideTable(GameRoomsStore store)
    {
        this.store = store;
        screen = new ScreenFx(backdrop);
        backdrop.Set(Backdrop.Neon);
        confetti = new[] { AppAccents.For(AccentId), BroadsideArt.Flame, BroadsideArt.Cloud, BroadsideArt.Brass };
        Array.Fill(lastShotOn, NoCell);
    }

    public void Reset()
    {
        seenRound = NotObserved;
        flying = false;
        holding = false;
        firedCell = NoCell;
        sentRound = NotObserved;
        draft.Clear();
        placement.Reset();
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        screen.Clear();
        bannerProgress = 1f;
    }

    internal static bool IsLive(BroadsideRoomStateDto board) =>
        board.EndKind.Length == 0 && (board.Placing || board.TurnSeat >= 0);

    internal static bool IsNextShot(long seenRound, int seenShots, BroadsideRoomStateDto board) =>
        seenRound == board.RoundIndex && board.ShotCount == seenShots + 1 && board.LastCell is >= 0 and < CellCount
        && board.LastSeat is >= 0 and < Seats;

    internal static int MarkOf(BroadsidePlayerDto[] players, int seat, int cell)
    {
        if (seat < 0 || seat >= players.Length || cell < 0 || cell >= CellCount)
        {
            return 0;
        }

        var marks = players[seat].Marks;
        return marks is not null && marks.Length == CellCount ? marks[cell] : 0;
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        BroadsideRoomStateDto board, BroadsideYouDto? mine, string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var raw = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var accent = AppAccents.For(AccentId);
        backdrop.Update(raw, body, ImGui.GetMousePos(), UiInteract.Hover(body.Min, body.Max));
        backdrop.Draw(drawList, body, accent, scale);
        screen.Update(raw);
        var tick = raw * screen.TimeScale;
        clock += raw;
        particles.Update(raw);
        fx.Update(raw);
        placement.Age(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        Age(raw);

        var players = board.Players ?? Array.Empty<BroadsidePlayerDto>();
        var mySeat = SeatOf(players, store.AccountId);
        var viewer = mySeat >= 0 ? mySeat : 0;
        var opponent = Seats - 1 - viewer;
        SyncText(players, viewer, opponent);

        var seatRow = new Rect(new Vector2(body.Min.X + EdgePadding * scale, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X - EdgePadding * scale, body.Min.Y + (4f + SeatRowHeight) * scale));
        var footer = new Rect(new Vector2(body.Min.X + EdgePadding * scale, body.Max.Y - FooterHeight * scale),
            new Vector2(body.Max.X - EdgePadding * scale, body.Max.Y - 4f * scale));
        var area = new Rect(new Vector2(seatRow.Min.X, seatRow.Max.Y + 6f * scale),
            new Vector2(seatRow.Max.X, footer.Min.Y - 4f * scale));
        layout.Build(StageLayout.Punched(area, screen.PlateScale), scale);
        Observe(board, viewer, mySeat, accent, scale);
        SyncShips(board, players, mine, viewer, mySeat);

        swap.Step(swapTarget, SwapSmooth, raw);
        battle.Step(battleTarget, BattleSmooth, raw);
        var shake = fx.ShakeOffset(scale);
        var blend = Easing.Clamp01(battle.Value);
        var home = BroadsideLayout.Lerp(layout.PlaceGrid,
            BroadsideLayout.Lerp(layout.Mini, layout.Big, swap.Value), blend).Translate(shake);
        var target = BroadsideLayout.Lerp(layout.Big, layout.Mini, swap.Value)
            .Translate(new Vector2((1f - blend) * (layout.Area.Width + 40f * scale), 0f) + shake);
        Advance(tick, board, viewer, mySeat, home, target, accent, scale);

        var live = IsLive(board);
        var animating = flying || holding;
        var myReady = mySeat >= 0 && players[mySeat].Ready;
        var editing = board.Placing && live && mySeat >= 0 && !myReady && !Sending(board) && !hold.Holding;
        var myTurn = live && !board.Placing && mySeat >= 0 && board.TurnSeat == mySeat;
        if (editing)
        {
            placement.Handle(draft, layout, home, false, scale, particles, fx);
        }
        else
        {
            placement.Release();
        }

        if (blend > 0.01f)
        {
            BroadsideArt.DrawGridLabel(drawList, target, Loc.T(L.Broadside.EnemySkies), scale, blend);
            DrawBoard(drawList, target, players, opponent, false, !live, accent, scale, blend);
        }

        BroadsideArt.DrawGridLabel(drawList, home, Loc.T(L.Broadside.YourFleet), scale, 1f);
        DrawBoard(drawList, home, players, viewer, true, !live, accent, scale, 1f);
        if (editing && board.Placing)
        {
            DrawDraft(drawList, home, scale);
        }

        hoverCell = NoCell;
        var aiming = myTurn && !animating && !hold.Holding;
        if (aiming)
        {
            HandleAim(players, opponent, target);
        }

        if (!store.ActInFlight)
        {
            firedCell = NoCell;
        }

        DrawReticles(drawList, players, opponent, target, accent, scale, aiming);
        DrawFlight(drawList, body, home, target, viewer, scale);
        if (blend > 0.5f)
        {
            BroadsideArt.DrawFleetPanel(drawList, layout.Panel, BroadsideLayout.Pitch(layout.Big), SunkMask(opponent),
                StatusText(board, viewer, myTurn, live), aiming ? accent : BroadsideArt.Muted, blend, scale);
        }

        if (board.Placing && live && mySeat >= 0)
        {
            DrawPlacement(drawList, theme, board, players, opponent, home, editing, accent, scale, 1f - blend);
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        GameBanner.Draw(drawList, bannerCenter, bannerText, bannerColor, theme, bannerProgress, TextStyles.Title1);
        screen.Draw(drawList, body, accent);
        DrawSeatRow(drawList, theme, scale, seatRow, board, players, viewer, opponent, snapshot);
        DrawFooter(drawList, theme, scale, footer, mySeat, live, notice, hold, animating);
    }

    private void Observe(BroadsideRoomStateDto board, int viewer, int mySeat, Vector4 accent, float scale)
    {
        if (board.RoundIndex != seenRound)
        {
            ResetRound(board, viewer);
            return;
        }

        if (seenPlacing && !board.Placing && IsLive(board))
        {
            BattleBegins(board, viewer, mySeat, accent);
        }

        seenPlacing = board.Placing;
        if (board.ShotCount != seenShots)
        {
            if (IsNextShot(seenRound, seenShots, board))
            {
                BeginFlight(board, viewer);
            }
            else
            {
                Settle(board);
            }

            seenShots = board.ShotCount;
        }

        if (flying || holding)
        {
            return;
        }

        if (!IsLive(board) && !endShown)
        {
            EndBattle(board, mySeat, scale);
        }

        swapTarget = DesiredSwap(board, viewer);
    }

    private void ResetRound(BroadsideRoomStateDto board, int viewer)
    {
        seenRound = board.RoundIndex;
        seenShots = board.ShotCount;
        seenPlacing = board.Placing;
        endShown = !IsLive(board);
        flying = false;
        holding = false;
        flightCell = NoCell;
        firedCell = NoCell;
        sentRound = NotObserved;
        particles.Clear();
        fx.Clear();
        ribbon.Clear();
        screen.Clear();
        draft.Clear();
        placement.Reset();
        Array.Fill(markAge, -1f);
        Array.Fill(sinkAge, -1f);
        Array.Fill(lastShotOn, NoCell);
        if (board.ShotCount > 0 && board.LastSeat is >= 0 and < Seats)
        {
            lastShotOn[Seats - 1 - board.LastSeat] = board.LastCell;
        }

        battleTarget = board.Placing ? 0f : 1f;
        battle.SnapTo(battleTarget);
        swapTarget = DesiredSwap(board, viewer);
        swap.SnapTo(swapTarget);
        bannerProgress = 1f;
    }

    private void BattleBegins(BroadsideRoomStateDto board, int viewer, int mySeat, Vector4 accent)
    {
        battleTarget = 1f;
        placement.Release();
        swapTarget = DesiredSwap(board, viewer);
        swap.SnapTo(swapTarget);
        screen.Sweep();
        if (mySeat >= 0 && board.TurnSeat == mySeat)
        {
            Banner(Loc.T(L.Games.OnlineYourTurn), accent, layout.Big.Center);
        }
    }

    private void BeginFlight(BroadsideRoomStateDto board, int viewer)
    {
        if (flying && flightCell >= 0)
        {
            lastShotOn[flightSeat] = flightCell;
        }

        flightSeat = Seats - 1 - board.LastSeat;
        flightCell = board.LastCell;
        flightShip = board.LastShip;
        flightResult = board.LastResult;
        flightFromTop = board.LastSeat != viewer;
        flightProgress = 0f;
        flying = true;
        holding = false;
        firedCell = NoCell;
        ribbon.Clear();
        UiFeedback.Play(UiSound.GameShoot);
    }

    private void Settle(BroadsideRoomStateDto board)
    {
        flying = false;
        holding = false;
        flightCell = NoCell;
        ribbon.Clear();
        if (board.LastSeat is >= 0 and < Seats)
        {
            lastShotOn[Seats - 1 - board.LastSeat] = board.LastCell;
        }
    }

    private void Advance(float tick, BroadsideRoomStateDto board, int viewer, int mySeat, Rect home, Rect target,
        Vector4 accent, float scale)
    {
        if (flying)
        {
            flightProgress += tick / BroadsideJuice.FlightSeconds;
            if (flightProgress >= 1f)
            {
                Impact(viewer, home, target, scale);
            }

            return;
        }

        if (!holding)
        {
            return;
        }

        holdTimer -= tick;
        if (holdTimer > 0f)
        {
            return;
        }

        holding = false;
        flightCell = NoCell;
        if (!IsLive(board))
        {
            EndBattle(board, mySeat, scale);
            return;
        }

        var desired = DesiredSwap(board, viewer);
        if (MathF.Abs(desired - swapTarget) > 0.5f)
        {
            UiFeedback.Play(UiSound.GameCardFlip);
        }

        swapTarget = desired;
        if (mySeat >= 0 && board.TurnSeat == mySeat)
        {
            Banner(Loc.T(L.Games.OnlineYourTurn), accent, layout.Big.Center);
        }
    }

    private void Impact(int viewer, Rect home, Rect target, float scale)
    {
        flying = false;
        holding = true;
        var rect = flightSeat == viewer ? home : target;
        var center = BroadsideLayout.CellCenter(rect, flightCell);
        var pitch = BroadsideLayout.Pitch(rect);
        var mine = flightSeat == viewer;
        markAge[flightSeat * CellCount + flightCell] = 0f;
        lastShotOn[flightSeat] = flightCell;
        ribbon.Clear();
        switch (flightResult)
        {
            case GameRoomWire.BroadsideResultHit:
                BroadsideJuice.Explode(particles, fx, screen, true, center, pitch, mine, false);
                BroadsideJuice.Struck(fx, center, pitch, scale);
                holdTimer = BroadsideJuice.HitHold;
                return;
            case GameRoomWire.BroadsideResultSunk:
                BroadsideJuice.Explode(particles, fx, screen, true, center, pitch, mine, true);
                Sink(rect, pitch, mine);
                holdTimer = BroadsideJuice.SunkHold;
                return;
            default:
                BroadsideJuice.Splash(particles, fx, center, pitch, scale);
                UiFeedback.Play(UiSound.GamePop);
                holdTimer = BroadsideJuice.MissHold;
                return;
        }
    }

    private void Sink(Rect rect, float pitch, bool mine)
    {
        screen.SlowMo(0.5f, 0.4f);
        if (!mine)
        {
            screen.Sweep();
        }

        if (flightShip is < 0 or >= ShipCount)
        {
            return;
        }

        sinkAge[flightSeat * ShipCount + flightShip] = 0f;
        Span<int> cells = stackalloc int[BroadsideFleet.LongestShip];
        var count = ShipCells(flightSeat, flightShip, cells);
        BroadsideJuice.SinkBurst(particles, fx, rect, cells[..count], pitch, true);
        Banner(shipDown[flightShip], mine ? BroadsideArt.Danger : BroadsideArt.Flame, rect.Center);
    }

    private void EndBattle(BroadsideRoomStateDto board, int mySeat, float scale)
    {
        endShown = true;
        if (mySeat < 0)
        {
            return;
        }

        var won = board.WinnerSeat == mySeat;
        Banner(Loc.T(won ? L.Broadside.Victory : L.Broadside.Defeat), won ? BroadsideArt.Flame : BroadsideArt.Danger,
            layout.Area.Center);
        if (!won)
        {
            UiFeedback.Play(UiSound.GameWrong);
            screen.Vignette(BroadsideArt.Danger, 0.35f, 1.2f);
            return;
        }

        UiFeedback.Play(UiSound.GameClear);
        particles.Confetti(new Vector2(layout.Area.Center.X, layout.Area.Min.Y), 90, confetti, 320f * scale, 4f, 1.6f);
        screen.Sweep();
        screen.Punch(0.05f);
    }

    private static float DesiredSwap(BroadsideRoomStateDto board, int viewer) =>
        IsLive(board) && !board.Placing && board.TurnSeat >= 0 && board.TurnSeat != viewer ? 1f : 0f;

    private bool Sending(BroadsideRoomStateDto board) =>
        sentRound == board.RoundIndex
        && (store.ActInFlight || Environment.TickCount64 - sentAtTick < SendGraceMilliseconds);

    private void SyncShips(BroadsideRoomStateDto board, BroadsidePlayerDto[] players, BroadsideYouDto? mine,
        int viewer, int mySeat)
    {
        Array.Fill(shipCell, NoCell);
        Array.Clear(shipSunk);
        for (var seat = 0; seat < Seats && seat < players.Length; seat++)
        {
            var sunk = players[seat].Sunk ?? Array.Empty<BroadsideShipDto>();
            for (var index = 0; index < sunk.Length; index++)
            {
                Record(seat, sunk[index].Ship, sunk[index].Cell, sunk[index].Across);
            }
        }

        if (mySeat >= 0 && mine is { Ships: { } ships } && mine.Seat == mySeat)
        {
            for (var index = 0; index < ships.Length; index++)
            {
                Record(viewer, ships[index].Ship, ships[index].Cell, ships[index].Across);
            }
        }
        else if (mySeat >= 0 && draft.AllPlaced && (players[mySeat].Ready || Sending(board)))
        {
            for (var ship = 0; ship < ShipCount; ship++)
            {
                Record(viewer, ship, BroadsideFleet.CellOf(draft.Column(ship), draft.Row(ship)), draft.Horizontal(ship));
            }
        }

        for (var seat = 0; seat < Seats && seat < players.Length; seat++)
        {
            for (var ship = 0; ship < ShipCount; ship++)
            {
                shipSunk[seat * ShipCount + ship] = IsSunk(players, seat, ship);
            }
        }
    }

    private void Record(int seat, int ship, int cell, bool across)
    {
        if (ship is < 0 or >= ShipCount || cell is < 0 or >= CellCount)
        {
            return;
        }

        shipCell[seat * ShipCount + ship] = cell;
        shipAcross[seat * ShipCount + ship] = across;
    }

    private bool IsSunk(BroadsidePlayerDto[] players, int seat, int ship)
    {
        Span<int> cells = stackalloc int[BroadsideFleet.LongestShip];
        var count = ShipCells(seat, ship, cells);
        if (count == 0)
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            if (MarkOf(players, seat, cells[index]) != GameRoomWire.BroadsideMarkHit)
            {
                return false;
            }
        }

        return true;
    }

    private int ShipCells(int seat, int ship, Span<int> cells)
    {
        var bow = shipCell[seat * ShipCount + ship];
        if (bow < 0)
        {
            return 0;
        }

        var across = shipAcross[seat * ShipCount + ship];
        var length = Math.Min(BroadsideFleet.Length(ship), cells.Length);
        var column = BroadsideFleet.ColumnOf(bow);
        var row = BroadsideFleet.RowOf(bow);
        var count = 0;
        for (var step = 0; step < length; step++)
        {
            var stepColumn = across ? column + step : column;
            var stepRow = across ? row : row + step;
            if (!BroadsideFleet.InBounds(stepColumn, stepRow))
            {
                break;
            }

            cells[count++] = BroadsideFleet.CellOf(stepColumn, stepRow);
        }

        return count;
    }

    private int SunkMask(int seat)
    {
        var mask = 0;
        for (var ship = 0; ship < ShipCount; ship++)
        {
            if (ShownSunk(seat, ship))
            {
                mask |= 1 << ship;
            }
        }

        return mask;
    }

    private bool Pending(int seat, int ship) => flying && seat == flightSeat && ship == flightShip;

    private bool ShownSunk(int seat, int ship) => shipSunk[seat * ShipCount + ship] && !Pending(seat, ship);

    private void DrawBoard(ImDrawListPtr drawList, Rect grid, BroadsidePlayerDto[] players, int seat, bool own,
        bool revealed, Vector4 accent, float scale, float alpha)
    {
        var compact = grid.Width < layout.Big.Width * 0.7f;
        BroadsideArt.DrawSky(drawList, grid, accent, backdrop.Ink, scale, clock, compact, alpha);
        var pitch = BroadsideLayout.Pitch(grid);
        for (var ship = 0; ship < ShipCount; ship++)
        {
            var index = seat * ShipCount + ship;
            var bow = shipCell[index];
            if (bow < 0 || (!own && Pending(seat, ship)))
            {
                continue;
            }

            var sunk = ShownSunk(seat, ship);
            if (!own && !sunk && !revealed)
            {
                continue;
            }

            var across = shipAcross[index];
            var shipRect = BroadsideLayout.ShipRect(grid, BroadsideFleet.ColumnOf(bow), BroadsideFleet.RowOf(bow),
                BroadsideFleet.Length(ship), across);
            var age = sinkAge[index];
            var sink = sunk ? (age < 0f ? 1f : Easing.Clamp01(age / BroadsideJuice.SinkSeconds)) : -1f;
            var shipAlpha = !own && !sunk ? 0.45f : alpha;
            BroadsideArt.DrawAirship(drawList, shipRect, across, own ? BroadsideArt.Hull : BroadsideArt.EnemyHull, scale,
                clock, sink, shipAlpha);
        }

        for (var cell = 0; cell < CellCount; cell++)
        {
            var mark = MarkOf(players, seat, cell);
            if (mark == 0 || (flying && seat == flightSeat && cell == flightCell))
            {
                continue;
            }

            var center = BroadsideLayout.CellCenter(grid, cell);
            var age = markAge[seat * CellCount + cell];
            if (mark == GameRoomWire.BroadsideMarkHit)
            {
                BroadsideArt.DrawFire(drawList, center, pitch, clock, cell, age);
            }
            else
            {
                BroadsideArt.DrawPuff(drawList, center, pitch, clock, cell, age);
            }
        }

        var last = lastShotOn[seat];
        if (last < 0 || (flying && seat == flightSeat))
        {
            return;
        }

        BroadsideArt.DrawLastShot(drawList, BroadsideLayout.CellCenter(grid, last), pitch, scale);
    }

    private void DrawDraft(ImDrawListPtr drawList, Rect grid, float scale)
    {
        for (var ship = 0; ship < ShipCount; ship++)
        {
            if (!draft.IsPlaced(ship) || placement.Lifted(ship))
            {
                continue;
            }

            var pop = placement.Pop(ship);
            if (pop < 0f)
            {
                continue;
            }

            var shipRect = BroadsideLayout.ShipRect(grid, draft.Column(ship), draft.Row(ship),
                BroadsideFleet.Length(ship), draft.Horizontal(ship));
            if (pop < 1f)
            {
                shipRect = shipRect.Scaled(0.7f + 0.3f * GameJuice.PopIn(pop));
            }

            var lift = ship == placement.DragShip ? 3f * scale : 0f;
            BroadsideArt.DrawAirship(drawList, shipRect, draft.Horizontal(ship), BroadsideArt.Hull, scale, clock, -1f,
                1f, placement.Warn(ship), lift);
        }
    }

    private void DrawPlacement(ImDrawListPtr drawList, PhoneTheme theme, BroadsideRoomStateDto board,
        BroadsidePlayerDto[] players, int opponent, Rect home, bool editing, Vector4 accent, float scale, float fade)
    {
        if (editing)
        {
            var action = placement.DrawControls(drawList, draft, layout, theme, accent, clock, scale, fade,
                !store.ActInFlight, false);
            placement.DrawGhost(drawList, draft, home, accent, clock, scale);
            if (action == PlacementAction.Auto)
            {
                placement.AutoPlace(draft, ref placeRandom);
            }
            else if (action == PlacementAction.Ready)
            {
                SendFleet(board);
            }

            return;
        }

        if (fade <= 0.01f || opponent >= players.Length || players[opponent].Ready)
        {
            return;
        }

        var spinner = 7f * scale;
        var center = new Vector2(layout.Hint.Center.X, layout.Dock.Center.Y);
        LoadingPulse.Spinner(new Vector2(center.X, center.Y - spinner * 2f), spinner, accent, fade);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + spinner),
            Typography.FitText(waitingFleet, layout.Area.Width, TextStyles.Subheadline),
            BroadsideArt.Muted with { W = BroadsideArt.Muted.W * fade }, TextStyles.Subheadline);
    }

    private void SendFleet(BroadsideRoomStateDto board)
    {
        if (!draft.AllPlaced || store.ActInFlight)
        {
            return;
        }

        var fleet = new BroadsideShipDto[ShipCount];
        for (var ship = 0; ship < ShipCount; ship++)
        {
            fleet[ship] = new BroadsideShipDto(ship, BroadsideFleet.CellOf(draft.Column(ship), draft.Row(ship)),
                draft.Horizontal(ship));
        }

        store.SendFleet(fleet);
        sentRound = board.RoundIndex;
        sentAtTick = Environment.TickCount64;
        placement.Release();
        UiFeedback.Play(UiSound.GamePowerUp);
    }

    private void HandleAim(BroadsidePlayerDto[] players, int opponent, Rect target)
    {
        PressSurface.Claim(AimSurfaceId, target, out _);
        var mouse = ImGui.GetMousePos();
        if (!UiInteract.Hover(target.Min, target.Max) || !BroadsideLayout.CellAt(target, mouse, out var column, out var row))
        {
            return;
        }

        var cell = BroadsideFleet.CellOf(column, row);
        hoverCell = cell;
        var open = MarkOf(players, opponent, cell) == 0;
        if (open)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var rect = BroadsideLayout.CellRect(target, column, row);
        if (!UiInteract.Click(rect.Min, rect.Max, true, false))
        {
            return;
        }

        if (!open || store.ActInFlight)
        {
            UiFeedback.Play(UiSound.GameWrong);
            fx.AddTrauma(0.06f);
            return;
        }

        firedCell = cell;
        store.SendFire(cell);
    }

    private void DrawReticles(ImDrawListPtr drawList, BroadsidePlayerDto[] players, int opponent, Rect target,
        Vector4 accent, float scale, bool aiming)
    {
        var cell = firedCell >= 0 ? firedCell : aiming ? hoverCell : NoCell;
        if (cell < 0)
        {
            return;
        }

        var pitch = BroadsideLayout.Pitch(target);
        var open = firedCell >= 0 || MarkOf(players, opponent, cell) == 0;
        var color = open ? accent : BroadsideArt.Disabled;
        StageCell.Draw(drawList, BroadsideLayout.CellRect(target, BroadsideFleet.ColumnOf(cell),
                BroadsideFleet.RowOf(cell)).Inset(pitch * 0.06f), color with { W = open ? 0.35f : 0.12f },
            CellDepth.Raised, pitch * 0.16f, scale);
        BroadsideArt.DrawReticle(drawList, BroadsideLayout.CellCenter(target, cell), pitch, color, clock, scale);
    }

    private void DrawFlight(ImDrawListPtr drawList, Rect body, Rect home, Rect target, int viewer, float scale)
    {
        if (!flying || flightCell < 0)
        {
            BroadsideJuice.DrawTrail(drawList, ribbon, scale);
            return;
        }

        var rect = flightSeat == viewer ? home : target;
        var destination = BroadsideLayout.CellCenter(rect, flightCell);
        var origin = new Vector2(layout.Area.Center.X, flightFromTop ? body.Min.Y : body.Max.Y);
        BroadsideJuice.DrawFlight(drawList, ribbon, particles, origin, destination, flightProgress, scale);
    }

    private string StatusText(BroadsideRoomStateDto board, int viewer, bool myTurn, bool live)
    {
        if (!live || board.Placing)
        {
            return string.Empty;
        }

        if (flying || holding)
        {
            return flightSeat == viewer ? theirTurn : Loc.T(L.Games.OnlineYourTurn);
        }

        return myTurn ? Loc.T(L.Broadside.TapToFire) : theirTurn;
    }

    private void DrawSeatRow(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect row,
        BroadsideRoomStateDto board, BroadsidePlayerDto[] players, int viewer, int opponent,
        GameRoomSnapshotDto snapshot)
    {
        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var half = row.Width * 0.5f;
        DrawSeat(drawList, theme, scale, new Rect(row.Min, new Vector2(row.Min.X + half - 4f * scale, row.Max.Y)),
            board, players, viewer, true, remaining);
        DrawSeat(drawList, theme, scale, new Rect(new Vector2(row.Min.X + half + 4f * scale, row.Min.Y), row.Max),
            board, players, opponent, false, remaining);
    }

    private void DrawSeat(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect slot, BroadsideRoomStateDto board,
        BroadsidePlayerDto[] players, int seat, bool own, long remaining)
    {
        if (seat >= players.Length)
        {
            return;
        }

        var player = players[seat];
        var live = IsLive(board);
        var waiting = live && board.Placing && !player.Ready;
        var moving = live && !board.Placing && board.TurnSeat == seat;
        var radius = SeatIconRadius * scale;
        var iconCenter = new Vector2(slot.Min.X + radius + 2f * scale, slot.Center.Y);
        drawList.AddCircleFilled(iconCenter, radius, ImGui.GetColorU32(BroadsideArt.SkyBottom), 24);
        var hull = new Rect(iconCenter - new Vector2(radius * 0.8f, radius * 0.34f),
            iconCenter + new Vector2(radius * 0.8f, radius * 0.34f));
        BroadsideArt.DrawAirship(drawList, hull, true, own ? BroadsideArt.Hull : BroadsideArt.EnemyHull, scale, clock,
            -1f, 1f);
        if (waiting || moving)
        {
            TurnTimerRing.Draw(drawList, iconCenter, radius + 4f * scale, remaining, board.TurnSeconds,
                own ? AppAccents.For(AccentId) : BroadsideArt.EnemyHull, scale);
        }
        else if (live && board.Placing)
        {
            ProgressRing.CenterIcon(drawList, iconCenter + new Vector2(radius * 0.8f, -radius * 0.8f),
                FontAwesomeIcon.Check, ReadyInk, radius * 0.8f);
        }

        var textLeft = iconCenter.X + radius + 10f * scale;
        var style = TextStyles.SubheadlineEmphasized;
        Typography.Draw(drawList, new Vector2(textLeft, slot.Center.Y - Typography.LineHeight(style) * 0.5f),
            Typography.FitText(seatNames[seat], MathF.Max(1f, slot.Max.X - textLeft), style),
            waiting || moving ? theme.TextStrong : theme.TextMuted, style);
    }

    private void DrawFooter(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect footer, int mySeat, bool live,
        string notice, OnlineFinishHold hold, bool animating)
    {
        if (hold.Holding)
        {
            hold.Draw(drawList, new Vector2(footer.Center.X, footer.Center.Y - 8f * scale), footer.Width, theme, scale,
                !animating);
            return;
        }

        var resignWidth = 0f;
        if (mySeat >= 0 && live)
        {
            resignWidth = ResignWidth * scale;
            var size = new Vector2(resignWidth, ResignHeight * scale);
            if (GameHud.Button(new Vector2(footer.Max.X - size.X * 0.5f, footer.Center.Y), size,
                    Loc.T(L.Games.OnlineResign), ResignTint, theme) && !store.ActInFlight)
            {
                store.SendResign();
            }
        }

        var status = notice.Length > 0
            ? notice
            : store.Room.Attached ? string.Empty : Loc.T(L.Games.OnlineReconnecting);
        if (status.Length == 0)
        {
            return;
        }

        var style = TextStyles.Subheadline;
        var width = MathF.Max(1f, footer.Width - resignWidth - 8f * scale);
        Typography.Draw(drawList, new Vector2(footer.Min.X, footer.Center.Y - Typography.LineHeight(style) * 0.5f),
            Typography.FitText(status, width, style), notice.Length > 0 ? theme.Danger : theme.TextMuted, style);
    }

    private void SyncText(BroadsidePlayerDto[] players, int viewer, int opponent)
    {
        var languageChanged = !ReferenceEquals(textLanguage, Loc.Current);
        if (languageChanged)
        {
            textLanguage = Loc.Current;
            for (var ship = 0; ship < ShipCount; ship++)
            {
                shipDown[ship] = Loc.T(L.Broadside.ShipDown, Loc.T(BroadsideArt.ShipNames[ship]));
            }
        }

        if (!languageChanged && ReferenceEquals(players, labeledPlayers) && viewer == labeledViewer)
        {
            return;
        }

        labeledPlayers = players;
        labeledViewer = viewer;
        for (var seat = 0; seat < Seats; seat++)
        {
            if (seat >= players.Length)
            {
                seatNames[seat] = string.Empty;
                continue;
            }

            var player = players[seat];
            seatNames[seat] = player.Away
                ? string.Concat(player.DisplayName, " · ", Loc.T(L.Games.OnlineAway))
                : player.DisplayName;
        }

        var opponentName = opponent < players.Length ? players[opponent].DisplayName : string.Empty;
        theirTurn = Loc.T(L.Games.OnlineTheirTurn, opponentName);
        waitingFleet = Loc.T(L.Games.OnlineBroadsideWaiting, opponentName);
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
    }

    private void Banner(string text, Vector4 color, Vector2 center)
    {
        bannerText = text;
        bannerColor = color;
        bannerCenter = center;
        bannerProgress = 0f;
    }

    private static int SeatOf(BroadsidePlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
