using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.MiniGolf;
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

internal enum OnlineGolfStage : byte
{
    Idle,
    Rolling,
    Sinking,
    Drowning,
    Done,
    Card,
}

// Every stroke here is the server's: the table replays the sampled path it was sent, sets every
// windmill from the room's mill clock, and only ever sends an angle and a power.
internal sealed class OnlineMiniGolfTable
{
    public const string AccentId = "minigolf";
    internal const int NotObserved = -1;
    private const float DoneSeconds = 1.7f;
    private const float CardSeconds = 3f;
    private const float BannerSeconds = 1.5f;
    private const float EntranceSpeed = 2.4f;
    private const float ScorecardSpeed = 2.8f;
    private const float MaxPullPixels = 150f;
    private const float MinPower = 0.04f;
    private const float EdgePadding = 10f;
    private const float SeatRowHeight = 40f;
    private const float HudRowHeight = 26f;
    private const float FooterHeight = 44f;
    private const float RowGap = 6f;
    private const float DotRadius = 6f;
    private const float BannerHeight = 0.3f;
    private const float MillCatchUp = 1.5f;
    private const double MillSnapSeconds = 2d;
    private const long SendGraceMilliseconds = 2_500;
    private const ulong PreviewSeed = 0x474F4C46UL;
    private const string Separator = " · ";
    private static readonly Vector4 Muted = new(0.62f, 0.66f, 0.70f, 1f);

    private readonly GameRoomsStore store;
    private readonly StageBackdrop backdrop = new();
    private readonly ScreenFx screen;
    private readonly MiniGolfJuice juice = new();
    private readonly MiniGolfBoard preview = new();
    private readonly MiniGolfRound card = new();
    private readonly OnlineMiniGolfReplay replay = new();
    private readonly Vector2[] aimPath = new Vector2[MiniGolfRenderer.MaxAimPoints];
    private readonly string[] rowNames = new string[MiniGolfRound.MaxPlayers];
    private readonly int[] rowSeats = new int[MiniGolfRound.MaxPlayers];
    private Camera2D camera = Camera2D.Create();
    private LabelPairSlot holeLabel;
    private LabelPairSlot holeOfLabel;
    private OnlineGolfStage stage;
    private MiniGolfRoomStateDto? cardSource;
    private MiniGolfPlayerDto[]? statusPlayers;
    private LanguageInfo? statusLanguage;
    private string statusText = string.Empty;
    private string bannerText = string.Empty;
    private string shotResult = string.Empty;
    private Vector4 bannerColor;
    private Vector2 ball;
    private Vector2 sinkFrom;
    private Vector2 shotRest;
    private Vector2 dragStart;
    private double millClock;
    private long seenRound = NotObserved;
    private long sentAtTick;
    private int seenShots;
    private int shownHole;
    private int previewHole = NotObserved;
    private int cardRows;
    private int ballRow;
    private int shotRow;
    private int shotHole;
    private int shotStrokes;
    private int doneHole;
    private int sentShotCount = NotObserved;
    private int statusKind = NotObserved;
    private int statusSeat = NotObserved;
    private int aimPathCount;
    private float stageSeconds;
    private float bannerProgress = 1f;
    private float entrance = 1f;
    private float cardAppear;
    private float power;
    private float time;
    private bool shotPicked;
    private bool ballVisible;
    private bool cardOpen;
    private bool dragging;
    private bool statusAttached;

    public OnlineMiniGolfTable(GameRoomsStore store)
    {
        this.store = store;
        screen = new ScreenFx(backdrop);
        backdrop.Set(Backdrop.Meadow);
    }

    public void Reset()
    {
        seenRound = NotObserved;
        seenShots = 0;
        stage = OnlineGolfStage.Idle;
        replay.Clear();
        juice.Clear();
        juice.Trail.Clear();
        screen.Clear();
        cardSource = null;
        statusPlayers = null;
        statusKind = NotObserved;
        sentShotCount = NotObserved;
        previewHole = NotObserved;
        cardOpen = false;
        dragging = false;
        aimPathCount = 0;
        bannerProgress = 1f;
    }

    internal static bool IsLive(MiniGolfRoomStateDto board) => board.EndKind.Length == 0 && board.TurnSeat >= 0;

    internal static bool FollowsSeen(int seenShots, MiniGolfRoomStateDto board) =>
        board.ShotCount == seenShots + 1 && board.LastShot is { Seat: >= 0 };

    internal static int Rows(MiniGolfPlayerDto[] players, Span<int> seats)
    {
        var rows = 0;
        for (var seat = 0; seat < players.Length && rows < seats.Length; seat++)
        {
            if (players[seat].InRound)
            {
                seats[rows++] = seat;
            }
        }

        return rows;
    }

    internal static float MillAngle(in GolfMill mill, double clockSeconds) =>
        (float)(mill.Speed * clockSeconds % (Math.PI * 2d));

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot, MiniGolfRoomStateDto board,
        string notice, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var raw = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var accent = AppAccents.For(AccentId);
        backdrop.Update(raw, body, ImGui.GetMousePos(), UiInteract.Hover(body.Min, body.Max));
        screen.Update(raw);
        var tick = raw * screen.TimeScale;
        time += raw;
        juice.Update(raw);
        bannerProgress = GameBanner.Advance(bannerProgress, raw, BannerSeconds);
        entrance = GameJuice.Advance(entrance, raw, EntranceSpeed);

        var players = board.Players ?? Array.Empty<MiniGolfPlayerDto>();
        var live = IsLive(board);
        var mySeat = SeatOf(players, store.AccountId);
        SyncCard(board, players);
        var seatRow = new Rect(new Vector2(body.Min.X + EdgePadding * scale, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X - EdgePadding * scale, body.Min.Y + (4f + SeatRowHeight) * scale));
        var hudRow = new Rect(new Vector2(seatRow.Min.X, seatRow.Max.Y + RowGap * scale),
            new Vector2(seatRow.Max.X, seatRow.Max.Y + (RowGap + HudRowHeight) * scale));
        var footer = new Rect(new Vector2(seatRow.Min.X, body.Max.Y - FooterHeight * scale),
            new Vector2(seatRow.Max.X, body.Max.Y - 4f * scale));
        var course = new Rect(new Vector2(body.Min.X + 4f * scale, hudRow.Max.Y + RowGap * scale),
            new Vector2(body.Max.X - 4f * scale, footer.Min.Y - RowGap * scale));

        Observe(board, live, accent);
        var serverNow = store.Room.ServerNowUnixMs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Advance(tick, raw, board, live, serverNow, body, accent);
        var hole = MiniGolfCourse.Get(shownHole);
        camera.Fit(course, hole.Bounds, FitMode.Contain);
        screen.ApplyTo(ref camera);
        camera.Update(raw, scale);
        backdrop.SetCamera(in camera);
        backdrop.Draw(drawList, body, accent, scale);

        DrawWorld(drawList, hole, accent, scale);
        var myTurn = live && mySeat >= 0 && board.TurnSeat == mySeat;
        if (myTurn && stage == OnlineGolfStage.Idle && !cardOpen && !hold.Holding && !SendPending(board))
        {
            HandleAim(board, hole, course, scale);
        }
        else
        {
            dragging = false;
            aimPathCount = 0;
        }

        if (dragging && aimPathCount > 1)
        {
            MiniGolfRenderer.DrawAim(drawList, in camera, aimPath.AsSpan(0, aimPathCount), ball, ImGui.GetMousePos(),
                power, time);
        }

        GameBanner.Draw(drawList, new Vector2(course.Center.X, course.Min.Y + course.Height * BannerHeight), bannerText,
            bannerColor, theme, bannerProgress);
        screen.Draw(drawList, body, accent);
        DrawSeats(drawList, scale, seatRow, board, players, snapshot);
        DrawHud(drawList, scale, hudRow, board, hole, accent);
        var settled = stage == OnlineGolfStage.Idle;
        DrawCard(drawList, theme, scale, course, board, accent, raw, settled && !live && hold.Holding);
        if (hold.Holding)
        {
            hold.Draw(drawList, footer.Center, footer.Width, theme, scale, settled);
        }
        else
        {
            DrawStatus(drawList, theme, scale, footer, board, players, mySeat, myTurn && settled, notice);
        }

        juice.Celebration.Draw(drawList, scale);
    }

    private void Observe(MiniGolfRoomStateDto board, bool live, Vector4 accent)
    {
        if (board.RoundIndex != seenRound)
        {
            seenRound = board.RoundIndex;
            seenShots = board.ShotCount;
            stage = OnlineGolfStage.Idle;
            replay.Clear();
            juice.Clear();
            juice.Trail.Clear();
            cardOpen = false;
            LoadHole(board.Hole, live && board.ShotCount == 0, accent);
            return;
        }

        if (board.ShotCount == seenShots)
        {
            return;
        }

        var follows = FollowsSeen(seenShots, board);
        seenShots = board.ShotCount;
        if (!follows)
        {
            replay.Clear();
            stage = OnlineGolfStage.Idle;
            return;
        }

        BeginShot(board.LastShot!, accent);
    }

    private void BeginShot(MiniGolfShotDto shot, Vector4 accent)
    {
        if (shot.Hole != shownHole)
        {
            LoadHole(shot.Hole, false, accent);
        }

        shotRow = RowOf(shot.Seat);
        shotHole = shot.Hole;
        shotStrokes = shot.Strokes;
        shotResult = shot.Result;
        shotPicked = shot.PickedUp;
        shotRest = new Vector2(shot.RestX, shot.RestY);
        dragging = false;
        aimPathCount = 0;
        cardOpen = false;
        replay.Begin(shot);
        if (!replay.Active)
        {
            Lift();
            return;
        }

        ball = replay.Sample(0);
        ballRow = shotRow;
        stage = OnlineGolfStage.Rolling;
        millClock = replay.ClockSeconds;
        var heading = replay.Heading();
        juice.React(new GolfImpact(GolfEvents.Shot, ball, Math.Clamp(heading.Length() / MiniGolfBoard.MaxShotSpeed, 0f, 1f),
            heading, Vector2.Zero), ref camera, screen, accent, true);
        if (shot.Timed)
        {
            juice.Fx.AddText(Loc.T(L.Games.OnlineTimedOut), camera.ToScreen(ball), Muted, 1f);
        }
    }

    private void Advance(float tick, float raw, MiniGolfRoomStateDto board, bool live, long serverNow, Rect full,
        Vector4 accent)
    {
        var hole = MiniGolfCourse.Get(shownHole);
        if (stage is not OnlineGolfStage.Rolling and not OnlineGolfStage.Idle)
        {
            millClock += raw;
        }

        switch (stage)
        {
            case OnlineGolfStage.Rolling:
                Roll(tick, hole, accent);
                return;
            case OnlineGolfStage.Sinking:
                stageSeconds += tick;
                if (stageSeconds >= MiniGolfBoard.SinkSeconds)
                {
                    Holed(hole, full);
                }

                return;
            case OnlineGolfStage.Drowning:
                stageSeconds += tick;
                if (stageSeconds < MiniGolfBoard.DrownSeconds)
                {
                    return;
                }

                ball = shotRest;
                if (shotPicked)
                {
                    Lift();
                    return;
                }

                Settle();
                return;
            case OnlineGolfStage.Done:
                stageSeconds += tick;
                if (stageSeconds >= DoneSeconds)
                {
                    AfterHole(board);
                }

                return;
            case OnlineGolfStage.Card:
                stageSeconds += raw;
                if (stageSeconds >= CardSeconds)
                {
                    stage = OnlineGolfStage.Idle;
                }

                return;
            default:
                Rest(board, live, serverNow, raw, accent);
                return;
        }
    }

    private void Roll(float tick, MiniGolfHole hole, Vector4 accent)
    {
        replay.Advance(tick);
        millClock = replay.ClockSeconds;
        ball = replay.Position(hole);
        while (replay.TakeMark(out var kind, out var value, out var sample))
        {
            React(hole, kind, value, sample, accent);
        }

        juice.TrackBall(ball, true, replay.Speed());
        if (!replay.Finished)
        {
            return;
        }

        sinkFrom = replay.Last;
        stageSeconds = 0f;
        if (string.Equals(shotResult, GameRoomWire.MiniGolfResultHoled, StringComparison.Ordinal))
        {
            stage = OnlineGolfStage.Sinking;
            return;
        }

        if (string.Equals(shotResult, GameRoomWire.MiniGolfResultWater, StringComparison.Ordinal)
            || string.Equals(shotResult, GameRoomWire.MiniGolfResultOut, StringComparison.Ordinal))
        {
            stage = OnlineGolfStage.Drowning;
            return;
        }

        ball = shotRest;
        if (shotPicked)
        {
            Lift();
            return;
        }

        Settle();
    }

    private void React(MiniGolfHole hole, int kind, int value, int sample, Vector4 accent)
    {
        var events = OnlineMiniGolfReplay.EventOf(kind);
        var point = replay.Sample(sample);
        var from = Vector2.Zero;
        if (events == GolfEvents.Tunnel && value >= 0 && value < hole.Tunnels.Length)
        {
            from = hole.Tunnels[value].Entry;
            point = hole.Tunnels[value].Exit;
        }
        else if (events is GolfEvents.Drop or GolfEvents.LipOut)
        {
            point = hole.Cup;
        }

        juice.React(new GolfImpact(events, point, value / 10f, Vector2.Zero, from), ref camera, screen, accent, true);
    }

    private void Rest(MiniGolfRoomStateDto board, bool live, long serverNow, float raw, Vector4 accent)
    {
        ballVisible = live && board.TurnSeat >= 0;
        if (!live)
        {
            millClock += raw;
            return;
        }

        if (board.Hole != shownHole)
        {
            LoadHole(board.Hole, true, accent);
        }

        ball = new Vector2(board.BallX, board.BallY);
        ballRow = RowOf(board.TurnSeat);
        var target = (serverNow - board.MillEpochUnixMs) / 1000d;
        var drift = target - millClock;
        millClock = Math.Abs(drift) > MillSnapSeconds
            ? target
            : millClock + raw + drift * Math.Min(1d, raw * MillCatchUp);
    }

    private void Settle()
    {
        stage = OnlineGolfStage.Idle;
        juice.Trail.Clear();
    }

    private void Holed(MiniGolfHole hole, Rect full)
    {
        stage = OnlineGolfStage.Done;
        stageSeconds = 0f;
        doneHole = shotHole;
        ballVisible = false;
        var result = MiniGolfRound.Classify(shotStrokes, hole.Par);
        ShowBanner(MiniGolfScorecard.ResultText(result, shotStrokes - hole.Par), MiniGolfScorecard.ResultColor(result));
        juice.Holed(result, hole.Cup, full, in camera, screen);
    }

    private void Lift()
    {
        stage = OnlineGolfStage.Done;
        stageSeconds = 0f;
        doneHole = shotHole;
        ballVisible = false;
        juice.Trail.Clear();
        ShowBanner(Loc.T(L.MiniGolf.PickedUp), MiniGolfScorecard.ResultColor(HoleResult.Worse));
        UiFeedback.Play(UiSound.GameWrong);
    }

    private void AfterHole(MiniGolfRoomStateDto board)
    {
        juice.Trail.Clear();
        stage = OnlineGolfStage.Idle;
        if (!IsLive(board) || board.Hole == doneHole)
        {
            return;
        }

        stage = OnlineGolfStage.Card;
        stageSeconds = 0f;
        cardAppear = 0f;
        UiFeedback.Play(UiSound.GameCardFlip);
    }

    private void LoadHole(int hole, bool banner, Vector4 accent)
    {
        shownHole = Math.Clamp(hole, 0, MiniGolfCourse.HoleCount - 1);
        entrance = 0f;
        dragging = false;
        aimPathCount = 0;
        juice.Trail.Clear();
        if (!banner)
        {
            return;
        }

        ShowBanner(holeLabel.Get(L.MiniGolf.HolePar, shownHole + 1, MiniGolfCourse.Get(shownHole).Par), accent);
    }

    private void HandleAim(MiniGolfRoomStateDto board, MiniGolfHole hole, Rect course, float scale)
    {
        var mouse = ImGui.GetMousePos();
        var overCourse = UiInteract.Hover(course.Min, course.Max);
        if (!dragging && overCourse && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragStart = mouse;
        }

        if (!dragging)
        {
            aimPathCount = 0;
            if (overCourse)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            return;
        }

        var pull = mouse - dragStart;
        power = Math.Clamp(pull.Length() / (MaxPullPixels * scale), 0f, 1f);
        var direction = camera.ToWorld(dragStart) - camera.ToWorld(mouse);
        aimPathCount = power >= MinPower ? PredictAim(hole, direction) : 0;
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        dragging = false;
        aimPathCount = 0;
        if (power < MinPower || direction.LengthSquared() <= 0.000001f || store.ActInFlight)
        {
            return;
        }

        store.SendShoot(MathF.Atan2(direction.Y, direction.X), power);
        sentShotCount = board.ShotCount;
        sentAtTick = Environment.TickCount64;
    }

    private int PredictAim(MiniGolfHole hole, Vector2 direction)
    {
        if (previewHole != shownHole)
        {
            preview.Load(hole, GameRandom.FromSeed(PreviewSeed));
            previewHole = shownHole;
        }

        preview.PlaceBall(ball);
        for (var index = 0; index < preview.MillCount; index++)
        {
            var mill = hole.Mills[index];
            preview.World.SetTransform(preview.MillBody(index), mill.Hub, MillAngle(mill, millClock));
        }

        return preview.PredictAim(direction, power, aimPath);
    }

    private bool SendPending(MiniGolfRoomStateDto board) =>
        sentShotCount == board.ShotCount
        && (store.ActInFlight || Environment.TickCount64 - sentAtTick < SendGraceMilliseconds);

    private void DrawWorld(ImDrawListPtr drawList, MiniGolfHole hole, Vector4 accent, float scale)
    {
        var alpha = Easing.EaseOutCubic(entrance);
        MiniGolfRenderer.DrawCourse(drawList, in camera, hole, alpha, time);
        MiniGolfRenderer.DrawCup(drawList, in camera, hole.Cup, ball, accent, alpha, time);
        MiniGolfRenderer.DrawPosts(drawList, in camera, hole, juice.FlashPoint, juice.BumpFlash, alpha);
        juice.DrawTrail(drawList, in camera);
        DrawBall(drawList, hole, accent);
        for (var index = 0; index < hole.Mills.Length; index++)
        {
            MiniGolfRenderer.DrawMill(drawList, in camera, hole.Mills[index], MillAngle(hole.Mills[index], millClock),
                alpha);
        }

        juice.DrawEffects(drawList, in camera, scale);
    }

    private void DrawBall(ImDrawListPtr drawList, MiniGolfHole hole, Vector4 accent)
    {
        var band = RowColor(ballRow, accent);
        switch (stage)
        {
            case OnlineGolfStage.Rolling:
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(ball), camera.Px(MiniGolfBoard.BallRadius), band, 1f);
                return;
            case OnlineGolfStage.Sinking:
                MiniGolfRenderer.DrawSinking(drawList, in camera, sinkFrom, hole.Cup,
                    stageSeconds / MiniGolfBoard.SinkSeconds, band);
                return;
            case OnlineGolfStage.Drowning:
                MiniGolfRenderer.DrawDrowning(drawList, in camera, sinkFrom,
                    MathF.Min(1f, stageSeconds / MiniGolfBoard.DrownSeconds), band);
                return;
            case OnlineGolfStage.Idle when ballVisible:
                MiniGolfRenderer.DrawBall(drawList, camera.ToScreen(ball), camera.Px(MiniGolfBoard.BallRadius), band, 1f);
                return;
        }
    }

    private void DrawSeats(ImDrawListPtr drawList, float scale, Rect row, MiniGolfRoomStateDto board,
        MiniGolfPlayerDto[] players, GameRoomSnapshotDto snapshot)
    {
        var count = Math.Min(players.Length, MiniGolfRound.MaxPlayers);
        if (count == 0)
        {
            return;
        }

        var remaining = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var gap = 6f * scale;
        var width = (row.Width - gap * (count - 1)) / count;
        var live = IsLive(board);
        for (var seat = 0; seat < count; seat++)
        {
            var player = players[seat];
            var chip = new Rect(new Vector2(row.Min.X + seat * (width + gap), row.Min.Y),
                new Vector2(row.Min.X + seat * (width + gap) + width, row.Max.Y));
            StageHud.Capsule(drawList, chip, scale, 0.75f);
            var cardRow = RowOf(seat);
            var color = cardRow >= 0 ? GameSeats.Color(cardRow) : Muted;
            var dot = new Vector2(chip.Min.X + 12f * scale, chip.Center.Y);
            drawList.AddCircleFilled(dot, DotRadius * scale, ImGui.GetColorU32(player.Away ? Muted : color), 16);
            var onTurn = live && board.TurnSeat == seat;
            if (onTurn)
            {
                TurnTimerRing.Draw(drawList, dot, (DotRadius + 3.5f) * scale, remaining, board.TurnSeconds, color, scale);
            }

            var textLeft = dot.X + (DotRadius + 6f) * scale;
            var textWidth = MathF.Max(1f, chip.Max.X - textLeft - 6f * scale);
            var nameStyle = TextStyles.FootnoteEmphasized;
            var lineStyle = TextStyles.Caption1;
            var top = chip.Center.Y - (Typography.LineHeight(nameStyle) + Typography.LineHeight(lineStyle)) * 0.5f;
            Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(player.DisplayName, textWidth, nameStyle),
                onTurn ? GamePalette.InkLight : GamePalette.InkLight with { W = 0.75f }, nameStyle);
            var line = !player.InRound ? Loc.T(L.Games.OnlineLuckyDrawNextDeal)
                : player.Away ? Loc.T(L.Games.OnlineAway)
                : GameNumber.Label(player.Total);
            Typography.Draw(drawList, new Vector2(textLeft, top + Typography.LineHeight(nameStyle)),
                Typography.FitText(line, textWidth, lineStyle), Muted, lineStyle);
        }
    }

    private void DrawHud(ImDrawListPtr drawList, float scale, Rect row, MiniGolfRoomStateDto board, MiniGolfHole hole,
        Vector4 accent)
    {
        var holeText = holeLabel.Get(L.MiniGolf.HolePar, shownHole + 1, hole.Par);
        var strokes = stage == OnlineGolfStage.Idle ? board.HoleStrokes : shotStrokes;
        var strokesText = GameNumber.Label(strokes);
        var cardText = Loc.T(L.MiniGolf.Scorecard);
        var gap = 6f * scale;
        var holeWidth = StatCapsule.Width(holeText, scale) * scale;
        var strokesWidth = StatCapsule.Width(strokesText, scale) * scale;
        var cardWidth = MathF.Min(StatCapsule.Width(cardText, scale) * scale,
            MathF.Max(0f, row.Width - holeWidth - strokesWidth - gap * 2f));
        var holeRect = new Rect(row.Min, new Vector2(row.Min.X + holeWidth, row.Max.Y));
        var strokesRect = new Rect(new Vector2(holeRect.Max.X + gap, row.Min.Y),
            new Vector2(holeRect.Max.X + gap + strokesWidth, row.Max.Y));
        var cardRect = new Rect(new Vector2(row.Max.X - cardWidth, row.Min.Y), row.Max);
        StatCapsule.Draw(drawList, holeRect, FontAwesomeIcon.Flag, holeText, accent, scale);
        StatCapsule.Draw(drawList, strokesRect, FontAwesomeIcon.Bullseye, strokesText, RowColor(ballRow, accent), scale);
        if (cardRows == 0 || cardWidth <= 0f)
        {
            return;
        }

        var hovered = UiInteract.Hover(cardRect.Min, cardRect.Max);
        StatCapsule.Draw(drawList, cardRect, FontAwesomeIcon.ListUl, Typography.FitText(cardText,
            MathF.Max(1f, cardWidth - 36f * scale), TextStyles.FootnoteEmphasized), cardOpen ? accent : Muted, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(cardRect.Min, cardRect.Max, hovered))
        {
            return;
        }

        cardOpen = !cardOpen;
        cardAppear = 0f;
        dragging = false;
        if (stage == OnlineGolfStage.Card)
        {
            stage = OnlineGolfStage.Idle;
            cardOpen = false;
        }
    }

    private void DrawCard(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect course, MiniGolfRoomStateDto board,
        Vector4 accent, float raw, bool final)
    {
        var between = stage == OnlineGolfStage.Card;
        if (cardRows == 0 || (!between && !cardOpen && !final))
        {
            return;
        }

        cardAppear = MathF.Min(1f, cardAppear + raw * ScorecardSpeed);
        var shown = final ? card.Holes - 1 : between ? doneHole : shownHole;
        var subtitle = holeOfLabel.Get(L.MiniGolf.HoleOf, shown + 1, card.Holes);
        MiniGolfScorecard.Draw(drawList, course, card, shown, subtitle, final, accent, theme, cardAppear, scale,
            rowNames.AsSpan(0, cardRows), false);
        if (final || cardAppear < 0.6f || !UiInteract.Click(course.Min, course.Max))
        {
            return;
        }

        cardOpen = false;
        if (between)
        {
            stage = OnlineGolfStage.Idle;
        }
    }

    private void DrawStatus(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect footer, MiniGolfRoomStateDto board,
        MiniGolfPlayerDto[] players, int mySeat, bool aiming, string notice)
    {
        var status = notice.Length > 0 ? notice : StatusText(board, players, mySeat, aiming);
        if (status.Length == 0)
        {
            return;
        }

        var style = TextStyles.Subheadline;
        Typography.DrawCentered(drawList, footer.Center, Typography.FitText(status, footer.Width, style),
            notice.Length > 0 ? theme.Danger : aiming ? GamePalette.InkLight : Muted, style);
    }

    private string StatusText(MiniGolfRoomStateDto board, MiniGolfPlayerDto[] players, int mySeat, bool aiming)
    {
        var live = IsLive(board);
        var kind = aiming ? 0
            : mySeat >= 0 && !players[mySeat].InRound && live ? 1
            : live && board.TurnSeat >= 0 && board.TurnSeat < players.Length && board.TurnSeat != mySeat ? 2
            : 3;
        var attached = store.Room.Attached;
        if (kind == statusKind && board.TurnSeat == statusSeat && attached == statusAttached
            && ReferenceEquals(players, statusPlayers) && ReferenceEquals(statusLanguage, Loc.Current))
        {
            return statusText;
        }

        statusKind = kind;
        statusSeat = board.TurnSeat;
        statusAttached = attached;
        statusPlayers = players;
        statusLanguage = Loc.Current;
        var text = kind switch
        {
            0 => string.Concat(Loc.T(L.Games.OnlineYourTurn), Separator, Loc.T(L.Games.OnlineMiniGolfAimHint)),
            1 => Loc.T(L.Games.OnlineLuckyDrawNextDeal),
            2 => Loc.T(L.Games.OnlineTheirTurn, players[board.TurnSeat].DisplayName),
            _ => string.Empty,
        };
        if (!attached)
        {
            text = text.Length == 0
                ? Loc.T(L.Games.OnlineReconnecting)
                : string.Concat(text, Separator, Loc.T(L.Games.OnlineReconnecting));
        }

        statusText = text;
        return statusText;
    }

    private void SyncCard(MiniGolfRoomStateDto board, MiniGolfPlayerDto[] players)
    {
        if (ReferenceEquals(board, cardSource))
        {
            return;
        }

        cardSource = board;
        cardRows = Rows(players, rowSeats);
        card.Reset(Math.Max(1, board.Holes), Math.Max(1, cardRows));
        for (var row = 0; row < cardRows; row++)
        {
            var player = players[rowSeats[row]];
            rowNames[row] = player.DisplayName;
            var strokes = player.Strokes ?? Array.Empty<int>();
            for (var hole = 0; hole < strokes.Length && hole < card.Holes; hole++)
            {
                card.Set(row, hole, strokes[hole]);
            }
        }
    }

    private int RowOf(int seat)
    {
        for (var row = 0; row < cardRows; row++)
        {
            if (rowSeats[row] == seat)
            {
                return row;
            }
        }

        return NotObserved;
    }

    private static Vector4 RowColor(int row, Vector4 accent) => row >= 0 ? GameSeats.Color(row) : accent with { W = 0f };

    private void ShowBanner(string text, Vector4 color)
    {
        bannerText = text;
        bannerColor = color;
        bannerProgress = 0f;
    }

    private static int SeatOf(MiniGolfPlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return NotObserved;
    }
}
