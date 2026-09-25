using System.Globalization;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlinePoolTable
{
    private readonly struct TableGeometry
    {
        public readonly Vector2 Origin;
        public readonly float Unit;
        public readonly float Cushion;
        public readonly float Wood;

        public TableGeometry(Vector2 origin, float unit, float cushion, float wood)
        {
            Origin = origin;
            Unit = unit;
            Cushion = cushion;
            Wood = wood;
        }

        public float Width => GameRoomWire.PoolTableWidth * Unit;
        public float Height => GameRoomWire.PoolTableHeight * Unit;
        public float BallRadius => GameRoomWire.PoolBallRadius * Unit;
        public float PocketRadius => GameRoomWire.PoolPocketRadius * Unit;
        public Rect Felt => new(Origin, Origin + new Vector2(Width, Height));
        public Rect Outer => Felt.Inset(-(Cushion + Wood));
        public Vector2 ToScreen(float x, float y) => Origin + new Vector2(x * Unit, y * Unit);
        public Vector2 ToScreen(Vector2 point) => Origin + point * Unit;
        public Vector2 ToFelt(Vector2 screen) => (screen - Origin) / Unit;
    }

    private readonly struct Layout
    {
        public readonly bool Landscape;
        public readonly TableGeometry Table;
        public readonly Rect MyPanel;
        public readonly Rect TheirPanel;
        public readonly Rect Status;
        public readonly Vector2 BackCenter;
        public readonly Vector2 ResignCenter;
        public readonly Vector2 ResignSize;

        public Layout(bool landscape, TableGeometry table, Rect myPanel, Rect theirPanel, Rect status,
            Vector2 backCenter, Vector2 resignCenter, Vector2 resignSize)
        {
            Landscape = landscape;
            Table = table;
            MyPanel = myPanel;
            TheirPanel = theirPanel;
            Status = status;
            BackCenter = backCenter;
            ResignCenter = resignCenter;
            ResignSize = resignSize;
        }
    }

    private static readonly Vector4 Felt = new(0.17f, 0.50f, 0.31f, 1f);
    private static readonly Vector4 FeltEdge = new(0.10f, 0.35f, 0.21f, 1f);
    private static readonly Vector4 Cushion = new(0.09f, 0.31f, 0.19f, 1f);
    private static readonly Vector4 CushionNose = new(0.15f, 0.42f, 0.27f, 1f);
    private static readonly Vector4 Rail = new(0.30f, 0.17f, 0.09f, 1f);
    private static readonly Vector4 RailLight = new(0.44f, 0.27f, 0.14f, 1f);
    private static readonly Vector4 Sight = new(0.95f, 0.90f, 0.76f, 0.85f);
    private static readonly Vector4 Pocket = new(0.04f, 0.04f, 0.05f, 1f);
    private static readonly Vector4 PocketFloor = new(0.11f, 0.11f, 0.12f, 1f);
    private static readonly Vector4 PocketLeather = new(0.08f, 0.05f, 0.03f, 0.95f);
    private static readonly Vector4 Lamp = new(1f, 0.96f, 0.84f, 1f);
    private static readonly Vector4 CueWhite = new(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Vector4 EightBlack = new(0.10f, 0.10f, 0.12f, 1f);
    private static readonly Vector4 NumberInk = new(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Vector4 ResignTint = new(0.85f, 0.35f, 0.32f, 1f);
    private static readonly Vector4 AimInk = new(1f, 1f, 1f, 0.70f);
    private static readonly Vector4 PowerFill = new(0.98f, 0.62f, 0.20f, 0.95f);
    private static readonly Vector4 CueTip = new(0.30f, 0.50f, 0.78f, 1f);
    private static readonly Vector4 CueFerrule = new(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Vector4 CueShaft = new(0.87f, 0.71f, 0.45f, 1f);
    private static readonly Vector4 CueWrap = new(0.13f, 0.11f, 0.11f, 1f);
    private static readonly Vector4 CueButt = new(0.36f, 0.20f, 0.11f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly Vector4[] BallColors =
    {
        new(0.98f, 0.80f, 0.15f, 1f),
        new(0.16f, 0.36f, 0.86f, 1f),
        new(0.86f, 0.18f, 0.16f, 1f),
        new(0.42f, 0.22f, 0.62f, 1f),
        new(0.95f, 0.46f, 0.12f, 1f),
        new(0.14f, 0.55f, 0.32f, 1f),
        new(0.55f, 0.16f, 0.18f, 1f),
    };

    private static readonly string[] BallLabels = BuildBallLabels();

    private const float MaxDragUnits = 0.5f;
    private const int CueBall = 0;
    private const int EightBall = 8;
    private const int GroupSize = 7;
    private const float CushionDepth = 7f;
    private const float WoodDepth = 13f;
    private const float TableRounding = 10f;
    private const float PanelWidth = 108f;
    private const float PanelGap = 10f;
    private const float StripHeight = 28f;
    private const float RowHeight = 44f;
    private const float BackRadius = 14f;
    private const float NumberedBallRadius = 9f;
    private const float AimDotStep = 7f;
    private const float ObjectPathUnits = 0.22f;
    private const float DeflectPathUnits = 0.14f;
    private const float CueLengthUnits = 0.9f;
    private const float CuePullUnits = 0.3f;

    private readonly GameRoomsStore store;

    private int replayActionCount = -1;
    private long replayStartedTick;
    private float replayDurationMs;
    private bool dragging;
    private Vector2 dragStart;
    private float power;
    private int cachedSeconds = -1;
    private string cachedSecondsLabel = string.Empty;

    public OnlinePoolTable(GameRoomsStore store)
    {
        this.store = store;
    }

    public void Reset()
    {
        replayActionCount = -1;
        dragging = false;
        power = 0f;
    }

    public void Draw(Rect body, PhoneTheme theme, float scale, GameRoomSnapshotDto snapshot,
        PoolRoomStateDto board, string notice, Action? back, OnlineFinishHold hold)
    {
        using var surface = AppSurface.Begin(body, true);
        ImGui.Dummy(new Vector2(MathF.Max(1f, body.Width - 32f * scale), body.Height - 16f * scale));
        var drawList = ImGui.GetWindowDrawList();
        var accent = Core.Apps.AppAccents.For("games");
        GameScene.Ambient(drawList, body, Felt);

        var players = board.Players ?? Array.Empty<PoolPlayerDto>();
        var balls = board.Balls ?? Array.Empty<PoolBallDto>();
        var mySeat = SeatOf(players, store.AccountId);
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var myTurn = live && mySeat >= 0 && board.TurnSeat == mySeat;
        var mySide = mySeat >= 0 ? mySeat : 0;
        var theirSide = 1 - mySide;

        var layout = back is not null && body.IsLandscape()
            ? LandscapeLayout(body, scale)
            : PortraitLayout(body, scale);
        var table = layout.Table;
        var replayFraction = ReplayProgress(board);
        var replaying = replayFraction < 1f;

        DrawLampGlow(drawList, table);
        DrawTable(drawList, table, scale);
        DrawBalls(drawList, table, scale, board, balls, replayFraction);

        if (myTurn && !replaying)
        {
            if (board.BallInHand)
            {
                dragging = false;
                power = 0f;
                HandlePlacement(drawList, table, scale, balls);
            }
            else
            {
                HandleShot(drawList, table, scale, balls);
            }
        }
        else
        {
            dragging = false;
            power = 0f;
        }

        DrawSeatPanel(drawList, theme, scale, layout.MyPanel, layout.Landscape, board, players, mySide, snapshot,
            accent, myTurn);
        DrawSeatPanel(drawList, theme, scale, layout.TheirPanel, layout.Landscape, board, players, theirSide,
            snapshot, accent, false);
        if (hold.Holding)
        {
            var holdCenter = layout.Landscape
                ? new Vector2(layout.Status.Center.X, layout.Status.Min.Y + 32f * scale)
                : new Vector2(layout.Status.Center.X, (layout.Status.Min.Y + body.Max.Y) * 0.5f);
            hold.Draw(drawList, holdCenter, layout.Status.Width, theme, scale, !replaying);
        }
        else
        {
            DrawStatus(drawList, theme, layout, board, players, mySeat, myTurn, replaying, notice);
        }

        if (back is not null && layout.Landscape && GameHud.LandscapeBack(layout.BackCenter, BackRadius * scale, theme))
        {
            back();
        }

        if (mySeat >= 0 && live
            && GameHud.Button(layout.ResignCenter, layout.ResignSize, Loc.T(L.Games.OnlineResign), ResignTint, theme)
            && !store.ActInFlight)
        {
            store.SendResign();
        }
    }

    private static Layout LandscapeLayout(Rect body, float scale)
    {
        var stage = new Rect(body.Min + new Vector2(8f * scale, 4f * scale), body.Max - new Vector2(8f * scale, 4f * scale));
        var stripTop = stage.Min.Y;
        var stripHeight = StripHeight * scale;
        var panelWidth = PanelWidth * scale;
        var gap = PanelGap * scale;
        var tableArea = new Rect(
            new Vector2(stage.Min.X + panelWidth + gap, stripTop + stripHeight + 6f * scale),
            new Vector2(stage.Max.X - panelWidth - gap, stage.Max.Y - 4f * scale));
        var table = FitTable(tableArea, scale);
        var outer = table.Outer;
        var myPanel = new Rect(
            new Vector2(MathF.Max(stage.Min.X, outer.Min.X - gap - panelWidth), outer.Min.Y),
            new Vector2(outer.Min.X - gap, outer.Max.Y));
        var theirPanel = new Rect(
            new Vector2(outer.Max.X + gap, outer.Min.Y),
            new Vector2(MathF.Min(stage.Max.X, outer.Max.X + gap + panelWidth), outer.Max.Y));
        var resignSize = new Vector2(92f * scale, 28f * scale);
        var backCenter = new Vector2(stage.Min.X + BackRadius * scale + 2f * scale, stripTop + stripHeight * 0.5f);
        var resignCenter = new Vector2(stage.Max.X - resignSize.X * 0.5f - 2f * scale, stripTop + stripHeight * 0.5f);
        var status = new Rect(
            new Vector2(backCenter.X + BackRadius * scale + 10f * scale, stripTop),
            new Vector2(resignCenter.X - resignSize.X * 0.5f - 10f * scale, stripTop + stripHeight));
        return new Layout(true, table, myPanel, theirPanel, status, backCenter, resignCenter, resignSize);
    }

    private static Layout PortraitLayout(Rect body, float scale)
    {
        var stage = new Rect(body.Min + new Vector2(8f * scale, 4f * scale), body.Max - new Vector2(8f * scale, 4f * scale));
        var rowHeight = RowHeight * scale;
        var gap = 6f * scale;
        var statusHeight = 44f * scale;
        var resignSize = new Vector2(110f * scale, 30f * scale);
        var theirPanel = new Rect(stage.Min, new Vector2(stage.Max.X, stage.Min.Y + rowHeight));
        var tableArea = new Rect(
            new Vector2(stage.Min.X, theirPanel.Max.Y + gap),
            new Vector2(stage.Max.X, stage.Max.Y - (rowHeight + statusHeight + resignSize.Y + gap * 3f)));
        var table = FitTable(tableArea, scale);
        var outer = table.Outer;
        var myPanel = new Rect(
            new Vector2(stage.Min.X, outer.Max.Y + gap),
            new Vector2(stage.Max.X, outer.Max.Y + gap + rowHeight));
        var status = new Rect(
            new Vector2(stage.Min.X, myPanel.Max.Y + gap),
            new Vector2(stage.Max.X, myPanel.Max.Y + gap + statusHeight));
        var resignCenter = new Vector2(stage.Center.X, stage.Max.Y - resignSize.Y * 0.5f - 4f * scale);
        return new Layout(false, table, myPanel, theirPanel, status, Vector2.Zero, resignCenter, resignSize);
    }

    private static TableGeometry FitTable(Rect area, float scale)
    {
        var cushion = CushionDepth * scale;
        var wood = WoodDepth * scale;
        var frame = (cushion + wood) * 2f;
        var unit = MathF.Min(
            (area.Width - frame) / GameRoomWire.PoolTableWidth,
            (area.Height - frame) / GameRoomWire.PoolTableHeight);
        unit = MathF.Max(unit, 1f);
        var origin = area.Center - new Vector2(GameRoomWire.PoolTableWidth * unit, GameRoomWire.PoolTableHeight * unit) * 0.5f;
        return new TableGeometry(origin, unit, cushion, wood);
    }

    private float ReplayProgress(PoolRoomStateDto board)
    {
        var trace = board.LastShot ?? Array.Empty<PoolTraceDto>();
        if (trace.Length == 0)
        {
            replayActionCount = board.ActionCount;
            return 1f;
        }

        if (replayActionCount != board.ActionCount)
        {
            replayActionCount = board.ActionCount;
            replayStartedTick = Environment.TickCount64;
            replayDurationMs = ReplayLength(trace);
        }

        if (replayDurationMs <= 0f)
        {
            return 1f;
        }

        var elapsed = Environment.TickCount64 - replayStartedTick;
        return elapsed >= replayDurationMs ? 1f : elapsed / replayDurationMs;
    }

    private static void DrawLampGlow(ImDrawListPtr drawList, in TableGeometry table)
    {
        var outer = table.Outer;
        var center = new Vector2(outer.Center.X, outer.Min.Y);
        for (var layer = 4; layer >= 1; layer--)
        {
            var radius = outer.Width * (0.22f + layer * 0.09f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Lamp with { W = 0.03f }), 48);
        }
    }

    private static void DrawTable(ImDrawListPtr drawList, in TableGeometry table, float scale)
    {
        var felt = table.Felt;
        var outer = table.Outer;
        var rounding = TableRounding * scale;
        Elevation.Draw(drawList, outer.Min, outer.Max, rounding, scale, 14f, 6f, 0.35f);
        Squircle.FillVerticalGradient(drawList, outer.Min, outer.Max, rounding, ImGui.GetColorU32(RailLight),
            ImGui.GetColorU32(Rail));
        Squircle.Stroke(drawList, outer.Min, outer.Max, rounding, ImGui.GetColorU32(White with { W = 0.10f }),
            1f * scale);
        DrawWoodGrain(drawList, outer, table.Wood, rounding, scale);
        DrawSights(drawList, table, scale);

        DrawFelt(drawList, table, scale);
        var cushionRect = felt.Inset(-table.Cushion);
        Squircle.Stroke(drawList, cushionRect.Min, cushionRect.Max, 2f * scale,
            ImGui.GetColorU32(Shadow with { W = 0.45f }), 1.5f * scale);
        DrawCushions(drawList, table, scale);
        DrawPockets(drawList, table, scale);
    }

    private static void DrawWoodGrain(ImDrawListPtr drawList, in Rect outer, float wood, float rounding, float scale)
    {
        var left = outer.Min.X + rounding;
        var right = outer.Max.X - rounding;
        var highlight = ImGui.GetColorU32(White with { W = 0.07f });
        var shade = ImGui.GetColorU32(Shadow with { W = 0.14f });
        drawList.AddLine(new Vector2(left, outer.Min.Y + wood * 0.35f), new Vector2(right, outer.Min.Y + wood * 0.35f),
            highlight, 1f * scale);
        drawList.AddLine(new Vector2(left, outer.Max.Y - wood * 0.40f), new Vector2(right, outer.Max.Y - wood * 0.40f),
            shade, 1f * scale);
        var top = outer.Min.Y + rounding;
        var bottom = outer.Max.Y - rounding;
        drawList.AddLine(new Vector2(outer.Min.X + wood * 0.35f, top), new Vector2(outer.Min.X + wood * 0.35f, bottom),
            highlight, 1f * scale);
        drawList.AddLine(new Vector2(outer.Max.X - wood * 0.40f, top), new Vector2(outer.Max.X - wood * 0.40f, bottom),
            shade, 1f * scale);
    }

    private static void DrawSights(ImDrawListPtr drawList, in TableGeometry table, float scale)
    {
        var felt = table.Felt;
        var inset = table.Cushion + table.Wood * 0.5f;
        var size = 2.6f * scale;
        var color = ImGui.GetColorU32(Sight);
        for (var step = 1; step < 8; step++)
        {
            if (step == 4)
            {
                continue;
            }

            var x = felt.Min.X + felt.Width * step / 8f;
            DrawDiamond(drawList, new Vector2(x, felt.Min.Y - inset), size, color);
            DrawDiamond(drawList, new Vector2(x, felt.Max.Y + inset), size, color);
        }

        for (var step = 1; step < 4; step++)
        {
            var y = felt.Min.Y + felt.Height * step / 4f;
            DrawDiamond(drawList, new Vector2(felt.Min.X - inset, y), size, color);
            DrawDiamond(drawList, new Vector2(felt.Max.X + inset, y), size, color);
        }
    }

    private static void DrawDiamond(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        drawList.AddQuadFilled(center - new Vector2(0f, size), center + new Vector2(size, 0f),
            center + new Vector2(0f, size), center - new Vector2(size, 0f), color);
    }

    private static void DrawCushions(ImDrawListPtr drawList, in TableGeometry table, float scale)
    {
        var felt = table.Felt;
        var depth = table.Cushion;
        var cornerJaw = table.PocketRadius * 0.9f;
        var sideJaw = table.PocketRadius * 0.85f;
        var fill = ImGui.GetColorU32(Cushion);
        var nose = ImGui.GetColorU32(CushionNose);
        var noseThickness = MathF.Max(1f, 1.2f * scale);

        var topBack = felt.Min.Y - depth;
        var bottomBack = felt.Max.Y + depth;
        HorizontalCushion(drawList, felt.Min.X + cornerJaw, felt.Center.X - sideJaw, felt.Min.Y, topBack, depth, fill,
            nose, noseThickness);
        HorizontalCushion(drawList, felt.Center.X + sideJaw, felt.Max.X - cornerJaw, felt.Min.Y, topBack, depth, fill,
            nose, noseThickness);
        HorizontalCushion(drawList, felt.Min.X + cornerJaw, felt.Center.X - sideJaw, felt.Max.Y, bottomBack, depth,
            fill, nose, noseThickness);
        HorizontalCushion(drawList, felt.Center.X + sideJaw, felt.Max.X - cornerJaw, felt.Max.Y, bottomBack, depth,
            fill, nose, noseThickness);
        VerticalCushion(drawList, felt.Min.Y + cornerJaw, felt.Max.Y - cornerJaw, felt.Min.X, felt.Min.X - depth,
            depth, fill, nose, noseThickness);
        VerticalCushion(drawList, felt.Min.Y + cornerJaw, felt.Max.Y - cornerJaw, felt.Max.X, felt.Max.X + depth,
            depth, fill, nose, noseThickness);
    }

    private static void HorizontalCushion(ImDrawListPtr drawList, float noseStart, float noseEnd, float noseY,
        float backY, float chamfer, uint fill, uint nose, float noseThickness)
    {
        drawList.AddQuadFilled(new Vector2(noseStart - chamfer, backY), new Vector2(noseEnd + chamfer, backY),
            new Vector2(noseEnd, noseY), new Vector2(noseStart, noseY), fill);
        drawList.AddLine(new Vector2(noseStart, noseY), new Vector2(noseEnd, noseY), nose, noseThickness);
    }

    private static void VerticalCushion(ImDrawListPtr drawList, float noseStart, float noseEnd, float noseX,
        float backX, float chamfer, uint fill, uint nose, float noseThickness)
    {
        drawList.AddQuadFilled(new Vector2(backX, noseStart - chamfer), new Vector2(noseX, noseStart),
            new Vector2(noseX, noseEnd), new Vector2(backX, noseEnd + chamfer), fill);
        drawList.AddLine(new Vector2(noseX, noseStart), new Vector2(noseX, noseEnd), nose, noseThickness);
    }

    private static void DrawFelt(ImDrawListPtr drawList, in TableGeometry table, float scale)
    {
        var felt = table.Felt;
        var top = ImGui.GetColorU32(Felt);
        var bottom = ImGui.GetColorU32(FeltEdge);
        drawList.AddRectFilledMultiColor(felt.Min, felt.Max, top, top, bottom, bottom);

        drawList.PushClipRect(felt.Min, felt.Max, true);
        var span = felt.Width * 0.42f;
        for (var layer = 5; layer >= 1; layer--)
        {
            var radius = span * layer / 5f;
            var alpha = 0.045f * (6 - layer) / 5f;
            drawList.AddCircleFilled(felt.Center, radius, ImGui.GetColorU32(Lamp with { W = alpha }), 48);
        }

        drawList.PopClipRect();

        var dark = ImGui.GetColorU32(Shadow with { W = 0.22f });
        var clear = ImGui.GetColorU32(Shadow with { W = 0f });
        var sideBand = felt.Width * 0.10f;
        var endBand = felt.Height * 0.12f;
        drawList.AddRectFilledMultiColor(felt.Min, new Vector2(felt.Min.X + sideBand, felt.Max.Y), dark, clear, clear,
            dark);
        drawList.AddRectFilledMultiColor(new Vector2(felt.Max.X - sideBand, felt.Min.Y), felt.Max, clear, dark, dark,
            clear);
        drawList.AddRectFilledMultiColor(felt.Min, new Vector2(felt.Max.X, felt.Min.Y + endBand), dark, dark, clear,
            clear);
        drawList.AddRectFilledMultiColor(new Vector2(felt.Min.X, felt.Max.Y - endBand), felt.Max, clear, clear, dark,
            dark);

        var headX = felt.Min.X + felt.Width * 0.25f;
        drawList.AddLine(new Vector2(headX, felt.Min.Y), new Vector2(headX, felt.Max.Y),
            ImGui.GetColorU32(White with { W = 0.10f }), 1f * scale);
        drawList.AddCircleFilled(new Vector2(felt.Min.X + felt.Width * 0.75f, felt.Center.Y), 2f * scale,
            ImGui.GetColorU32(White with { W = 0.16f }), 10);
    }

    private static void DrawPockets(ImDrawListPtr drawList, in TableGeometry table, float scale)
    {
        var felt = table.Felt;
        var radius = table.PocketRadius;
        for (var pocket = 0; pocket < 6; pocket++)
        {
            var x = felt.Min.X + pocket % 3 * felt.Width * 0.5f;
            var y = pocket < 3 ? felt.Min.Y : felt.Max.Y;
            var center = new Vector2(x, y);
            drawList.AddCircleFilled(center, radius * 1.16f, ImGui.GetColorU32(PocketLeather), 32);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Pocket), 32);
            drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.22f), radius * 0.55f,
                ImGui.GetColorU32(PocketFloor), 24);
            drawList.AddCircle(center, radius * 1.16f, ImGui.GetColorU32(White with { W = 0.07f }), 32, 1f * scale);
        }
    }

    private static void DrawBalls(ImDrawListPtr drawList, in TableGeometry table, float scale,
        PoolRoomStateDto board, PoolBallDto[] balls, float replayFraction)
    {
        var radius = table.BallRadius;
        var numbered = radius >= NumberedBallRadius * scale;
        var trace = board.LastShot ?? Array.Empty<PoolTraceDto>();
        var replayMs = replayFraction * ReplayLength(trace);
        for (var index = 0; index < balls.Length; index++)
        {
            var ball = balls[index];
            if (!TryPositionAt(ball, trace, replayFraction, replayMs, out var x, out var y))
            {
                continue;
            }

            DrawBall(drawList, table.ToScreen(x, y), radius, ball.Number, scale, numbered);
        }
    }

    private static float ReplayLength(PoolTraceDto[] trace)
    {
        var length = 0f;
        for (var index = 0; index < trace.Length; index++)
        {
            var end = trace[index].AtMs + trace[index].DurationMs;
            if (end > length)
            {
                length = end;
            }
        }

        return length;
    }

    private static bool TryPositionAt(PoolBallDto ball, PoolTraceDto[] trace, float replayFraction,
        float replayMs, out float x, out float y)
    {
        x = ball.X;
        y = ball.Y;
        if (replayFraction >= 1f || trace.Length == 0)
        {
            return !ball.Pocketed;
        }

        PoolTraceDto? first = null;
        PoolTraceDto? last = null;
        for (var index = 0; index < trace.Length; index++)
        {
            var run = trace[index];
            if (run.Ball != ball.Number)
            {
                continue;
            }

            first ??= run;
            last = run;
            if (replayMs >= run.AtMs && replayMs <= run.AtMs + run.DurationMs)
            {
                var t = run.DurationMs <= 0f ? 1f : (replayMs - run.AtMs) / run.DurationMs;
                x = run.FromX + (run.ToX - run.FromX) * t;
                y = run.FromY + (run.ToY - run.FromY) * t;
                return true;
            }
        }

        if (first is null)
        {
            return !ball.Pocketed;
        }

        if (replayMs < first.AtMs)
        {
            x = first.FromX;
            y = first.FromY;
            return true;
        }

        if (last is not null && replayMs > last.AtMs + last.DurationMs)
        {
            x = last.ToX;
            y = last.ToY;
            return !ball.Pocketed;
        }

        for (var index = trace.Length - 1; index >= 0; index--)
        {
            var run = trace[index];
            if (run.Ball == ball.Number && run.AtMs + run.DurationMs <= replayMs)
            {
                x = run.ToX;
                y = run.ToY;
                return true;
            }
        }

        return true;
    }

    private static Vector4 BodyColor(int number)
    {
        if (number == CueBall)
        {
            return CueWhite;
        }

        if (number == EightBall)
        {
            return EightBlack;
        }

        return BallColors[(number - 1) % GroupSize];
    }

    private static void DrawBall(ImDrawListPtr drawList, Vector2 center, float radius, int number, float scale,
        bool numbered)
    {
        var shadowCenter = center + new Vector2(radius * 0.12f, radius * 0.24f);
        drawList.AddCircleFilled(shadowCenter + new Vector2(0f, radius * 0.1f), radius * 1.18f,
            ImGui.GetColorU32(Shadow with { W = 0.14f }), 24);
        drawList.AddCircleFilled(shadowCenter, radius, ImGui.GetColorU32(Shadow with { W = 0.32f }), 24);

        var body = BodyColor(number);
        var striped = number > EightBall;
        var baseColor = striped ? CueWhite : body;
        var lit = center - new Vector2(radius * 0.09f, radius * 0.11f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.Darken(baseColor, 0.42f)), 28);
        drawList.AddCircleFilled(lit, radius * 0.9f, ImGui.GetColorU32(baseColor), 28);
        if (striped)
        {
            drawList.PushClipRect(center - new Vector2(radius, radius * 0.48f), center + new Vector2(radius, radius * 0.48f),
                true);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.Darken(body, 0.42f)), 28);
            drawList.AddCircleFilled(lit, radius * 0.9f, ImGui.GetColorU32(body), 28);
            drawList.PopClipRect();
        }

        drawList.AddCircleFilled(center - new Vector2(radius * 0.22f, radius * 0.28f), radius * 0.55f,
            ImGui.GetColorU32(White with { W = 0.10f }), 20);
        drawList.AddCircle(center, radius * 0.91f, ImGui.GetColorU32(Shadow with { W = 0.18f }), 28, radius * 0.18f);

        if (number != CueBall && radius >= 5f * scale)
        {
            var badge = radius * 0.5f;
            drawList.AddCircleFilled(center, badge, ImGui.GetColorU32(CueWhite), 16);
            if (numbered)
            {
                Typography.DrawCentered(drawList, center, BallLabels[number], NumberInk,
                    Math.Clamp(badge / (8f * scale), 0.42f, 1.1f), FontWeight.SemiBold);
            }
        }

        drawList.AddCircleFilled(center - new Vector2(radius * 0.36f, radius * 0.42f), radius * 0.30f,
            ImGui.GetColorU32(White with { W = 0.22f }), 14);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.40f, radius * 0.46f), radius * 0.16f,
            ImGui.GetColorU32(White with { W = 0.55f }), 12);
    }

    private void HandleShot(ImDrawListPtr drawList, in TableGeometry table, float scale, PoolBallDto[] balls)
    {
        var cue = balls.Length > CueBall ? balls[CueBall] : null;
        if (cue is null || cue.Pocketed)
        {
            dragging = false;
            power = 0f;
            return;
        }

        var cueCenter = table.ToScreen(cue.X, cue.Y);
        var outer = table.Outer;
        var mouse = ImGui.GetMousePos();
        var overTable = UiInteract.Hover(outer.Min, outer.Max);

        if (!dragging && overTable && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            dragStart = mouse;
        }

        if (!dragging)
        {
            power = 0f;
            if (overTable)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                DrawAim(drawList, table, cueCenter, mouse - cueCenter, balls, scale, 0f);
            }

            return;
        }

        var pull = mouse - dragStart;
        var pullUnits = pull.Length() / table.Unit;
        power = MathF.Min(1f, pullUnits / MaxDragUnits);
        var shotDirection = -pull;
        DrawAim(drawList, table, cueCenter, shotDirection, balls, scale, power);

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            dragging = false;
            if (power >= 0.05f && shotDirection.LengthSquared() > 0f && !store.ActInFlight)
            {
                store.SendShoot(MathF.Atan2(shotDirection.Y, shotDirection.X), power);
            }

            power = 0f;
        }
    }

    private static void DrawAim(ImDrawListPtr drawList, in TableGeometry table, Vector2 cueCenter, Vector2 direction,
        PoolBallDto[] balls, float scale, float pull)
    {
        if (direction.LengthSquared() <= 0.0001f)
        {
            return;
        }

        var normalized = Vector2.Normalize(direction);
        DrawAimGuide(drawList, table, cueCenter, normalized, balls, scale);
        DrawCue(drawList, cueCenter, normalized, table.BallRadius, table.Unit, pull, scale);
    }

    private static void DrawAimGuide(ImDrawListPtr drawList, in TableGeometry table, Vector2 cueCenter,
        Vector2 direction, PoolBallDto[] balls, float scale)
    {
        var start = table.ToFelt(cueCenter);
        var radius = GameRoomWire.PoolBallRadius;
        var travel = CushionDistance(start, direction, radius);
        var hitIndex = -1;
        for (var index = 0; index < balls.Length; index++)
        {
            var ball = balls[index];
            if (ball.Number == CueBall || ball.Pocketed)
            {
                continue;
            }

            var toBall = new Vector2(ball.X, ball.Y) - start;
            var along = Vector2.Dot(toBall, direction);
            if (along <= 0f)
            {
                continue;
            }

            var perpendicularSquared = toBall.LengthSquared() - along * along;
            var reachSquared = radius * radius * 4f - perpendicularSquared;
            if (reachSquared < 0f)
            {
                continue;
            }

            var distance = along - MathF.Sqrt(reachSquared);
            if (distance < 0f)
            {
                distance = 0f;
            }

            if (distance < travel)
            {
                travel = distance;
                hitIndex = index;
            }
        }

        var ghost = start + direction * travel;
        var ghostScreen = table.ToScreen(ghost);
        DrawDottedLine(drawList, cueCenter, ghostScreen, table.BallRadius, scale);
        drawList.AddCircleFilled(ghostScreen, table.BallRadius, ImGui.GetColorU32(White with { W = 0.12f }), 28);
        drawList.AddCircle(ghostScreen, table.BallRadius, ImGui.GetColorU32(AimInk), 28, 1.5f * scale);
        if (hitIndex < 0)
        {
            return;
        }

        var target = new Vector2(balls[hitIndex].X, balls[hitIndex].Y);
        var contact = target - ghost;
        if (contact.LengthSquared() <= 0.000001f)
        {
            return;
        }

        var objectDirection = Vector2.Normalize(contact);
        drawList.AddLine(table.ToScreen(target), table.ToScreen(target + objectDirection * ObjectPathUnits),
            ImGui.GetColorU32(White with { W = 0.55f }), 2f * scale);
        var deflect = direction - objectDirection * Vector2.Dot(direction, objectDirection);
        if (deflect.LengthSquared() < 0.0025f)
        {
            return;
        }

        deflect = Vector2.Normalize(deflect);
        drawList.AddLine(ghostScreen, table.ToScreen(ghost + deflect * DeflectPathUnits),
            ImGui.GetColorU32(White with { W = 0.32f }), 1.5f * scale);
    }

    private static float CushionDistance(Vector2 start, Vector2 direction, float radius)
    {
        var travel = float.MaxValue;
        if (direction.X > 0f)
        {
            travel = MathF.Min(travel, (GameRoomWire.PoolTableWidth - radius - start.X) / direction.X);
        }
        else if (direction.X < 0f)
        {
            travel = MathF.Min(travel, (radius - start.X) / direction.X);
        }

        if (direction.Y > 0f)
        {
            travel = MathF.Min(travel, (GameRoomWire.PoolTableHeight - radius - start.Y) / direction.Y);
        }
        else if (direction.Y < 0f)
        {
            travel = MathF.Min(travel, (radius - start.Y) / direction.Y);
        }

        return MathF.Max(0f, travel);
    }

    private static void DrawDottedLine(ImDrawListPtr drawList, Vector2 from, Vector2 to, float skip, float scale)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length <= skip)
        {
            return;
        }

        var direction = delta / length;
        var step = AimDotStep * scale;
        var dotRadius = 1.3f * scale;
        var color = ImGui.GetColorU32(AimInk);
        for (var distance = skip + step; distance < length - skip; distance += step)
        {
            drawList.AddCircleFilled(from + direction * distance, dotRadius, color, 8);
        }
    }

    private static void DrawCue(ImDrawListPtr drawList, Vector2 cueCenter, Vector2 direction, float ballRadius,
        float unit, float pull, float scale)
    {
        var tip = cueCenter - direction * (ballRadius * 1.3f + pull * unit * CuePullUnits);
        var length = unit * CueLengthUnits;
        var normal = new Vector2(-direction.Y, direction.X);
        var shadowOffset = new Vector2(2f, 3f) * scale;
        CueSegment(drawList, tip, direction, normal, length, 0f, 1f, 3.2f * scale, 7.2f * scale,
            ImGui.GetColorU32(Shadow with { W = 0.28f }), shadowOffset);
        CueSegment(drawList, tip, direction, normal, length, 0f, 0.014f, 3f * scale, 3.2f * scale,
            ImGui.GetColorU32(CueTip), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.014f, 0.045f, 3.2f * scale, 3.4f * scale,
            ImGui.GetColorU32(CueFerrule), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.045f, 0.62f, 3.4f * scale, 5.4f * scale,
            ImGui.GetColorU32(CueShaft), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.62f, 0.635f, 5.4f * scale, 5.6f * scale,
            ImGui.GetColorU32(CueFerrule), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.635f, 0.85f, 5.6f * scale, 6.3f * scale,
            ImGui.GetColorU32(CueWrap), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.85f, 0.865f, 6.3f * scale, 6.4f * scale,
            ImGui.GetColorU32(CueFerrule), Vector2.Zero);
        CueSegment(drawList, tip, direction, normal, length, 0.865f, 1f, 6.4f * scale, 7.2f * scale,
            ImGui.GetColorU32(CueButt), Vector2.Zero);

        var highlightFrom = tip - direction * (length * 0.05f) - normal * (1.1f * scale);
        var highlightTo = tip - direction * (length * 0.6f) - normal * (1.6f * scale);
        drawList.AddLine(highlightFrom, highlightTo, ImGui.GetColorU32(White with { W = 0.28f }), 1f * scale);
    }

    private static void CueSegment(ImDrawListPtr drawList, Vector2 tip, Vector2 direction, Vector2 normal,
        float length, float fromFraction, float toFraction, float fromWidth, float toWidth, uint color, Vector2 offset)
    {
        var from = tip - direction * (length * fromFraction) + offset;
        var to = tip - direction * (length * toFraction) + offset;
        drawList.AddQuadFilled(from + normal * (fromWidth * 0.5f), to + normal * (toWidth * 0.5f),
            to - normal * (toWidth * 0.5f), from - normal * (fromWidth * 0.5f), color);
    }

    private void HandlePlacement(ImDrawListPtr drawList, in TableGeometry table, float scale, PoolBallDto[] balls)
    {
        var mouse = ImGui.GetMousePos();
        var radius = GameRoomWire.PoolBallRadius;
        var felt = table.ToFelt(mouse);
        var x = felt.X;
        var y = felt.Y;
        var inside = x >= 0f && x <= GameRoomWire.PoolTableWidth && y >= 0f && y <= GameRoomWire.PoolTableHeight
            && UiInteract.Hover(table.Felt.Min, table.Felt.Max);
        if (!inside)
        {
            return;
        }

        x = Math.Clamp(x, radius, GameRoomWire.PoolTableWidth - radius);
        y = Math.Clamp(y, radius, GameRoomWire.PoolTableHeight - radius);
        var clear = true;
        for (var index = 1; index < balls.Length; index++)
        {
            if (balls[index].Pocketed)
            {
                continue;
            }

            var deltaX = balls[index].X - x;
            var deltaY = balls[index].Y - y;
            if (deltaX * deltaX + deltaY * deltaY < radius * radius * 4.2f)
            {
                clear = false;
                break;
            }
        }

        var ghost = table.ToScreen(x, y);
        drawList.AddCircleFilled(ghost, table.BallRadius, ImGui.GetColorU32(CueWhite with { W = clear ? 0.6f : 0.25f }),
            24);
        drawList.AddCircle(ghost, table.BallRadius, ImGui.GetColorU32(clear ? White with { W = 0.9f } : ResignTint), 24,
            1.5f * scale);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (clear && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !store.ActInFlight)
        {
            store.SendPlace(MathF.Round(x, 4), MathF.Round(y, 4));
        }
    }

    private void DrawSeatPanel(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect panel, bool column,
        PoolRoomStateDto board, PoolPlayerDto[] players, int seat, GameRoomSnapshotDto snapshot, Vector4 accent,
        bool shooter)
    {
        if (seat < 0 || seat >= players.Length)
        {
            return;
        }

        var player = players[seat];
        var live = board.WinnerSeat < 0 && board.EndKind.Length == 0;
        var isMover = live && seat == board.TurnSeat;
        var rounding = Metrics.Radius.Card * scale;
        if (isMover)
        {
            Squircle.Fill(drawList, panel.Min - new Vector2(3f * scale, 3f * scale),
                panel.Max + new Vector2(3f * scale, 3f * scale), rounding + 3f * scale,
                ImGui.GetColorU32(accent with { W = 0.16f }));
        }

        Squircle.Fill(drawList, panel.Min, panel.Max, rounding, ImGui.GetColorU32(theme.GroupedCard with { W = 0.92f }));
        Squircle.Stroke(drawList, panel.Min, panel.Max, rounding,
            ImGui.GetColorU32(isMover ? accent with { W = 0.75f } : White with { W = 0.08f }), 1f * scale);

        var remaining = RemainingOfGroup(board, player.Group);
        var groupLabel = GroupLabel(board, player.Group, remaining);
        var nameColor = player.Away ? theme.TextMuted : theme.TextStrong;
        long timerLeft = 0;
        if (isMover)
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            timerLeft = store.Room.RemainingMilliseconds(snapshot.PhaseEndsAtUnixMs, nowMs);
        }

        if (column)
        {
            DrawSeatColumn(drawList, theme, scale, panel, board, player, groupLabel, nameColor, isMover, timerLeft,
                accent, shooter);
            return;
        }

        DrawSeatRow(drawList, theme, scale, panel, board, player, groupLabel, nameColor, isMover, timerLeft, accent);
    }

    private void DrawSeatColumn(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect panel,
        PoolRoomStateDto board, PoolPlayerDto player, string groupLabel, Vector4 nameColor, bool isMover,
        long timerLeft, Vector4 accent, bool shooter)
    {
        var textWidth = panel.Width - 16f * scale;
        var top = panel.Min.Y + 12f * scale;
        Typography.DrawCentered(drawList, new Vector2(panel.Center.X, top + 8f * scale),
            Typography.FitText(player.DisplayName, textWidth, TextStyles.SubheadlineEmphasized), nameColor,
            TextStyles.SubheadlineEmphasized);
        if (groupLabel.Length > 0)
        {
            Typography.DrawCentered(drawList, new Vector2(panel.Center.X, top + 28f * scale),
                Typography.FitText(groupLabel, textWidth, TextStyles.Footnote), theme.TextMuted, TextStyles.Footnote);
        }

        if (player.Away)
        {
            Typography.DrawCentered(drawList, new Vector2(panel.Center.X, top + 44f * scale),
                Loc.T(L.Games.OnlineAway), theme.TextMuted, TextStyles.Caption1);
        }

        if (!board.OpenTable && player.Group != 0)
        {
            var ballRadius = 8f * scale;
            var spacing = 21f * scale;
            var firstRowLeft = panel.Center.X - spacing * 1.5f;
            var rowTop = top + 66f * scale;
            for (var slot = 0; slot < GroupSize; slot++)
            {
                var row = slot / 4;
                var columnIndex = slot % 4;
                var rowLeft = row == 0 ? firstRowLeft : panel.Center.X - spacing;
                var center = new Vector2(rowLeft + columnIndex * spacing, rowTop + row * spacing);
                DrawGroupBall(drawList, theme, board, player.Group, slot, center, ballRadius, scale);
            }
        }

        if (isMover)
        {
            var ringCenter = new Vector2(panel.Center.X, panel.Max.Y - 26f * scale);
            DrawTurnRing(drawList, theme, scale, ringCenter, 15f * scale, timerLeft, board.TurnSeconds, accent);
        }

        if (!shooter || !isMover)
        {
            return;
        }

        var gaugeMin = new Vector2(panel.Min.X + 12f * scale, panel.Max.Y - 54f * scale);
        var gaugeMax = new Vector2(panel.Max.X - 12f * scale, gaugeMin.Y + 6f * scale);
        DrawPowerGauge(drawList, gaugeMin, gaugeMax, scale);
    }

    private void DrawSeatRow(ImDrawListPtr drawList, PhoneTheme theme, float scale, Rect row, PoolRoomStateDto board,
        PoolPlayerDto player, string groupLabel, Vector4 nameColor, bool isMover, long timerLeft, Vector4 accent)
    {
        var ringReserve = isMover ? 36f * scale : 0f;
        var ballRadius = 6f * scale;
        var ballSpacing = 15f * scale;
        var ballsReserve = !board.OpenTable && player.Group != 0 ? ballSpacing * GroupSize + 8f * scale : 0f;
        var textLeft = row.Min.X + 12f * scale;
        var textWidth = row.Width - ringReserve - ballsReserve - 24f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 16f * scale),
            Typography.FitText(player.DisplayName, textWidth, TextStyles.SubheadlineEmphasized), nameColor,
            TextStyles.SubheadlineEmphasized);
        var subLabel = player.Away ? Loc.T(L.Games.OnlineAway) : groupLabel;
        if (subLabel.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + 3f * scale),
                Typography.FitText(subLabel, textWidth, TextStyles.Footnote), theme.TextMuted, TextStyles.Footnote);
        }

        if (ballsReserve > 0f)
        {
            var firstCenterX = row.Max.X - ringReserve - 12f * scale - ballSpacing * (GroupSize - 1) - ballRadius;
            for (var slot = 0; slot < GroupSize; slot++)
            {
                DrawGroupBall(drawList, theme, board, player.Group, slot,
                    new Vector2(firstCenterX + slot * ballSpacing, row.Center.Y), ballRadius, scale);
            }
        }

        if (isMover)
        {
            DrawTurnRing(drawList, theme, scale, new Vector2(row.Max.X - 20f * scale, row.Center.Y), 12f * scale,
                timerLeft, board.TurnSeconds, accent);
        }
    }

    private static void DrawGroupBall(ImDrawListPtr drawList, PhoneTheme theme, PoolRoomStateDto board, int group,
        int slot, Vector2 center, float radius, float scale)
    {
        var number = group == GameRoomWire.PoolGroupSolids ? slot + 1 : EightBall + 1 + slot;
        DrawBall(drawList, center, radius, number, scale, false);
        if (OnTable(board, number))
        {
            return;
        }

        drawList.AddCircleFilled(center + new Vector2(radius * 0.06f, radius * 0.12f), radius * 1.3f,
            ImGui.GetColorU32(theme.GroupedCard with { W = 0.80f }), 20);
    }

    private static bool OnTable(PoolRoomStateDto board, int number)
    {
        var balls = board.Balls ?? Array.Empty<PoolBallDto>();
        for (var index = 0; index < balls.Length; index++)
        {
            if (balls[index].Number == number)
            {
                return !balls[index].Pocketed;
            }
        }

        return false;
    }

    private void DrawTurnRing(ImDrawListPtr drawList, PhoneTheme theme, float scale, Vector2 center, float radius,
        long remaining, int windowSeconds, Vector4 accent)
    {
        TurnTimerRing.Draw(drawList, center, radius, remaining, windowSeconds, accent, scale);
        if (radius < 12f * scale)
        {
            return;
        }

        var urgent = TurnTimerRing.IsUrgent(remaining, windowSeconds);
        Typography.DrawCentered(drawList, center, SecondsLabel(remaining), urgent ? theme.TextStrong : theme.TextMuted,
            0.78f, FontWeight.SemiBold);
    }

    private string SecondsLabel(long remainingMilliseconds)
    {
        var seconds = (int)((remainingMilliseconds + 999) / 1000);
        if (seconds < 0)
        {
            seconds = 0;
        }

        if (seconds != cachedSeconds)
        {
            cachedSeconds = seconds;
            cachedSecondsLabel = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return cachedSecondsLabel;
    }

    private void DrawPowerGauge(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale)
    {
        var radius = (max.Y - min.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(Shadow with { W = 0.40f }), radius);
        if (power <= 0f)
        {
            return;
        }

        var filled = MathF.Max(radius * 2f, (max.X - min.X) * power);
        drawList.AddRectFilled(min, new Vector2(min.X + filled, max.Y), ImGui.GetColorU32(PowerFill), radius);
        ProgressRing.Glow(new Vector2(min.X + filled, (min.Y + max.Y) * 0.5f), radius * 2.4f, PowerFill,
            0.35f * power);
    }

    private static string GroupLabel(PoolRoomStateDto board, int group, int remaining)
    {
        if (board.OpenTable)
        {
            return Loc.T(L.Games.OnlineGroupOpen);
        }

        if (group == 0)
        {
            return string.Empty;
        }

        if (remaining == 0)
        {
            return Loc.T(L.Games.OnlineOnTheEight);
        }

        return group == GameRoomWire.PoolGroupSolids
            ? Loc.T(L.Games.OnlineGroupSolids)
            : Loc.T(L.Games.OnlineGroupStripes);
    }

    private static int RemainingOfGroup(PoolRoomStateDto board, int group)
    {
        if (group == 0)
        {
            return 0;
        }

        var balls = board.Balls ?? Array.Empty<PoolBallDto>();
        var remaining = 0;
        for (var index = 0; index < balls.Length; index++)
        {
            var number = balls[index].Number;
            var ballGroup = number is >= 1 and <= 7 ? GameRoomWire.PoolGroupSolids
                : number is >= 9 and <= 15 ? GameRoomWire.PoolGroupStripes : 0;
            if (ballGroup == group && !balls[index].Pocketed)
            {
                remaining++;
            }
        }

        return remaining;
    }

    private void DrawStatus(ImDrawListPtr drawList, PhoneTheme theme, in Layout layout, PoolRoomStateDto board,
        PoolPlayerDto[] players, int mySeat, bool myTurn, bool replaying, string notice)
    {
        string status;
        if (notice.Length > 0)
        {
            status = notice;
        }
        else if (!replaying && board.LastFoul.Length > 0 && board.LastSeat != board.TurnSeat)
        {
            status = Loc.T(GamesOnlineText.FoulMessage(board.LastFoul));
            if (myTurn)
            {
                status = status + " · " + Loc.T(board.BallInHand ? L.Games.OnlineBallInHand : L.Games.OnlineYourTurn);
            }
        }
        else if (myTurn)
        {
            status = board.BallInHand
                ? Loc.T(L.Games.OnlineBallInHand)
                : board.BreakPending
                    ? Loc.T(L.Games.OnlineBreakShot) + " · " + Loc.T(L.Games.OnlineShootHint)
                    : Loc.T(L.Games.OnlineYourTurn) + " · " + Loc.T(L.Games.OnlineShootHint);
        }
        else if (board.TurnSeat >= 0 && board.TurnSeat < players.Length)
        {
            status = Loc.T(L.Games.OnlineTheirTurn, players[board.TurnSeat].DisplayName);
        }
        else
        {
            status = string.Empty;
        }

        if (!store.Room.Attached)
        {
            status = status.Length == 0
                ? Loc.T(L.Games.OnlineReconnecting)
                : status + " · " + Loc.T(L.Games.OnlineReconnecting);
        }

        if (status.Length == 0)
        {
            return;
        }

        var color = myTurn ? theme.TextStrong : theme.TextMuted;
        var area = layout.Status;
        if (layout.Landscape)
        {
            Typography.DrawCentered(drawList, area.Center, Typography.FitText(status, area.Width, TextStyles.Footnote),
                color, TextStyles.Footnote);
            return;
        }

        Typography.DrawWrappedCentered(drawList, area.Center, status, color, TextStyles.Footnote,
            area.Width - 12f * UiScale.Current);
    }

    private static int SeatOf(PoolPlayerDto[] players, string userId)
    {
        for (var index = 0; index < players.Length; index++)
        {
            if (string.Equals(players[index].UserId, userId, StringComparison.Ordinal))
            {
                return players[index].Seat;
            }
        }

        return -1;
    }

    private static string[] BuildBallLabels()
    {
        var labels = new string[16];
        for (var number = 0; number < labels.Length; number++)
        {
            labels[number] = number.ToString(CultureInfo.InvariantCulture);
        }

        return labels;
    }
}
