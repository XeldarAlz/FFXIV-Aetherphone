using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hop;

internal sealed class HopRenderer
{
    public static readonly Vector4 HopperColor = new(0.62f, 0.62f, 0.68f, 1f);
    public static readonly Vector4 HopperMask = new(0.18f, 0.18f, 0.24f, 1f);
    public static readonly Vector4 VehicleColor = new(1f, 0.62f, 0.30f, 1f);
    public static readonly Vector4 RiderColor = new(1f, 0.85f, 0.30f, 1f);
    public static readonly Vector4 PadColor = new(0.62f, 0.45f, 0.28f, 1f);
    public static readonly Vector4 WaterColor = new(0.18f, 0.42f, 0.82f, 1f);
    private static readonly Vector4 Water = new(0.18f, 0.42f, 0.82f, 0.55f);
    private static readonly Vector4 WaterDash = new(0.7f, 0.85f, 1f, 0.25f);
    private static readonly Vector4 Road = new(0.10f, 0.10f, 0.14f, 0.85f);
    private static readonly Vector4 RoadDash = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 Grass = new(0.22f, 0.52f, 0.30f, 0.6f);
    private static readonly Vector4 Safe = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 DenMouth = new(0.05f, 0.05f, 0.08f, 0.8f);
    private const int WaterDashesPerLane = 4;

    public static Rect BoardRect(in Camera2D camera, out float cell)
    {
        cell = camera.Px(1f);
        return new Rect(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(HopBoard.Columns, HopBoard.Rows)));
    }

    public static Vector2 CellCenterWorld(float x, int row) => new(x + 0.5f, HopBoard.Rows - 1 - row + 0.5f);

    private static float RowTop(Rect board, float cell, int row) => board.Min.Y + (HopBoard.Rows - 1 - row) * cell;

    private static Vector2 CellCenter(Rect board, float cell, float x, int row) =>
        new(board.Min.X + (x + 0.5f) * cell, RowTop(board, cell, row) + cell * 0.5f);

    public void Draw(ImDrawListPtr drawList, HopBoard board, in Camera2D camera, Vector4 accent, float scale)
    {
        var boardRect = BoardRect(in camera, out var cell);
        drawList.PushClipRect(boardRect.Min, boardRect.Max, true);
        var time = (float)ImGui.GetTime();
        DrawTerrain(drawList, board, boardRect, cell, time);
        DrawDens(drawList, board, boardRect, cell, accent, time);
        DrawPads(drawList, board, boardRect, cell);
        DrawVehicles(drawList, board, boardRect, cell);
        DrawHopper(drawList, board, boardRect, cell, time);
        drawList.PopClipRect();
    }

    private static void DrawTerrain(ImDrawListPtr drawList, HopBoard board, Rect boardRect, float cell, float time)
    {
        var grass = ImGui.GetColorU32(Grass);
        var grassTop = RowTop(boardRect, cell, HopBoard.Rows - 1);
        drawList.AddRectFilled(new Vector2(boardRect.Min.X, grassTop), new Vector2(boardRect.Max.X, grassTop + cell * 2f), grass);
        var tuft = ImGui.GetColorU32(new Vector4(0.5f, 0.85f, 0.5f, 0.45f));
        for (var column = 0; column < HopBoard.Columns; column++)
        {
            for (var band = 0; band < 2; band++)
            {
                if ((column + band) % 3 != 1)
                {
                    continue;
                }

                var min = new Vector2(boardRect.Min.X + column * cell + cell * 0.4f, grassTop + band * cell + cell * 0.55f);
                drawList.AddRectFilled(min, min + new Vector2(cell * 0.2f, cell * 0.35f), tuft, cell * 0.08f);
            }
        }

        var safe = ImGui.GetColorU32(Safe);
        DrawBand(drawList, boardRect, cell, HopBoard.MedianRow, safe);
        DrawBand(drawList, boardRect, cell, HopBoard.StartRow, safe);
        var water = ImGui.GetColorU32(Water);
        var dash = ImGui.GetColorU32(WaterDash);
        for (var row = HopBoard.StreamFirstRow; row <= HopBoard.StreamLastRow; row++)
        {
            DrawBand(drawList, boardRect, cell, row, water);
            var top = RowTop(boardRect, cell, row);
            var drift = time * board.StreamSpeed(row - HopBoard.StreamFirstRow);
            for (var index = 0; index < WaterDashesPerLane; index++)
            {
                var x = HopBoard.Wrap(index * HopBoard.Columns / (float)WaterDashesPerLane + drift);
                var min = new Vector2(boardRect.Min.X + x * cell, top + cell * 0.45f);
                drawList.AddRectFilled(min, min + new Vector2(cell * 0.7f, MathF.Max(1f, cell * 0.1f)), dash);
            }
        }

        var road = ImGui.GetColorU32(Road);
        var roadDash = ImGui.GetColorU32(RoadDash);
        for (var row = HopBoard.RoadFirstRow; row <= HopBoard.RoadLastRow; row++)
        {
            DrawBand(drawList, boardRect, cell, row, road);
            if (row == HopBoard.RoadLastRow)
            {
                continue;
            }

            var top = RowTop(boardRect, cell, row);
            var x = boardRect.Min.X;
            while (x < boardRect.Max.X)
            {
                drawList.AddRectFilled(new Vector2(x, top - 1f), new Vector2(x + cell * 0.55f, top + 1f), roadDash);
                x += cell * 1.3f;
            }
        }
    }

    private static void DrawBand(ImDrawListPtr drawList, Rect boardRect, float cell, int row, uint color)
    {
        var top = RowTop(boardRect, cell, row);
        drawList.AddRectFilled(new Vector2(boardRect.Min.X, top), new Vector2(boardRect.Max.X, top + cell), color);
    }

    private static void DrawDens(ImDrawListPtr drawList, HopBoard board, Rect boardRect, float cell, Vector4 accent, float time)
    {
        var top = RowTop(boardRect, cell, HopBoard.BankRow);
        var wall = ImGui.GetColorU32(GamePalette.Darken(accent, 0.45f) with { W = 0.9f });
        var hatch = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f));
        var mouth = ImGui.GetColorU32(DenMouth);
        var aligned = board.AlignedBay;
        var pulse = 0.65f + MathF.Abs(MathF.Sin(time * 7f)) * 0.35f;
        var bay = 0;
        for (var column = 0; column < HopBoard.Columns; column++)
        {
            var min = new Vector2(boardRect.Min.X + column * cell, top);
            var max = min + new Vector2(cell, cell);
            var isDen = bay < HopBoard.BayCount && HopBoard.BayColumns[bay] == column;
            var bumped = board.BumpFlash > 0f && board.BumpColumn == column;
            if (!isDen)
            {
                drawList.AddRectFilled(min + new Vector2(1f, 1f), max - new Vector2(1f, 1f), wall, cell * 0.15f);
                for (var stripe = 0; stripe < 3; stripe++)
                {
                    var offset = cell * (0.15f + stripe * 0.3f);
                    drawList.AddLine(new Vector2(min.X + offset, max.Y - 1f), new Vector2(min.X + offset + cell * 0.3f, min.Y + 1f),
                        hatch, MathF.Max(1f, cell * 0.06f));
                }

                if (bumped)
                {
                    drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 0.4f, 0.4f, 1f)), cell * 0.15f, ImDrawFlags.None,
                        MathF.Max(1.5f, cell * 0.12f));
                }

                continue;
            }

            var filled = board.BayFilled(bay);
            var flashing = (board.BankFlash > 0f && board.LastBankedBay == bay) || (board.ClearingLevel && filled);
            drawList.AddRectFilled(min + new Vector2(1.5f, 1.5f), max - new Vector2(1.5f, 1.5f), mouth, cell * 0.2f);
            var rimAlpha = bumped ? 1f : filled ? 0.55f : 0.85f;
            drawList.AddRect(min + new Vector2(1.5f, 1.5f), max - new Vector2(1.5f, 1.5f),
                ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = rimAlpha }), cell * 0.2f, ImDrawFlags.None,
                MathF.Max(1f, cell * 0.07f));
            if (filled)
            {
                DrawHopperSprite(drawList, min + new Vector2(cell * 0.5f, cell * 0.5f), cell * 0.9f, false, 1f);
            }
            else
            {
                var chevronAlpha = bay == aligned ? pulse : 0.5f;
                var apexY = min.Y + cell * (bay == aligned ? 0.22f : 0.28f);
                var halfWidth = cell * (bay == aligned ? 0.24f : 0.18f);
                var centerX = min.X + cell * 0.5f;
                drawList.AddTriangleFilled(new Vector2(centerX - halfWidth, apexY), new Vector2(centerX + halfWidth, apexY),
                    new Vector2(centerX, apexY + halfWidth * 1.2f),
                    ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = chevronAlpha }));
                if (bay == aligned)
                {
                    drawList.AddRect(min, max, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f) with { W = pulse }), cell * 0.2f,
                        ImDrawFlags.None, MathF.Max(1.5f, cell * 0.1f));
                }
            }

            if (flashing)
            {
                var flashAlpha = 0.1f + MathF.Abs(MathF.Sin(time * 8f)) * 0.3f;
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, flashAlpha)), cell * 0.2f);
            }

            bay++;
        }
    }

    private static void DrawPads(ImDrawListPtr drawList, HopBoard board, Rect boardRect, float cell)
    {
        var fill = ImGui.GetColorU32(PadColor);
        var seam = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f));
        for (var lane = 0; lane < HopBoard.LaneCount; lane++)
        {
            var row = HopBoard.StreamFirstRow + lane;
            var top = RowTop(boardRect, cell, row);
            var inset = MathF.Max(1f, cell * 0.14f);
            for (var index = 0; index < board.PadCount(lane); index++)
            {
                var pad = board.Pad(lane, index);
                DrawPad(drawList, boardRect, cell, top, inset, pad, fill, seam);
                if (pad.X + pad.Length > HopBoard.Columns)
                {
                    DrawPad(drawList, boardRect, cell, top, inset, new LaneEntity { X = pad.X - HopBoard.Columns, Length = pad.Length },
                        fill, seam);
                }
            }
        }
    }

    private static void DrawPad(ImDrawListPtr drawList, Rect boardRect, float cell, float top, float inset, LaneEntity pad, uint fill,
        uint seam)
    {
        var min = new Vector2(boardRect.Min.X + pad.X * cell, top + inset);
        var max = new Vector2(min.X + pad.Length * cell, top + cell - inset);
        drawList.AddRectFilled(min, max, fill, cell * 0.3f);
        for (var plank = 1; plank < pad.Length; plank++)
        {
            var x = min.X + plank * cell;
            drawList.AddLine(new Vector2(x, min.Y), new Vector2(x, max.Y), seam, MathF.Max(1f, cell * 0.08f));
        }
    }

    private static void DrawVehicles(ImDrawListPtr drawList, HopBoard board, Rect boardRect, float cell)
    {
        for (var lane = 0; lane < HopBoard.LaneCount; lane++)
        {
            var row = HopBoard.RoadFirstRow + lane;
            var top = RowTop(boardRect, cell, row);
            var direction = board.RoadSpeed(lane) >= 0f ? 1f : -1f;
            for (var index = 0; index < board.RoadCount(lane); index++)
            {
                var vehicle = board.RoadEntity(lane, index);
                DrawVehicle(drawList, boardRect, cell, top, vehicle, direction);
                if (vehicle.X + vehicle.Length > HopBoard.Columns)
                {
                    DrawVehicle(drawList, boardRect, cell, top,
                        new LaneEntity { X = vehicle.X - HopBoard.Columns, Length = vehicle.Length }, direction);
                }
            }
        }
    }

    private static void DrawVehicle(ImDrawListPtr drawList, Rect boardRect, float cell, float top, LaneEntity vehicle, float direction)
    {
        var left = boardRect.Min.X + vehicle.X * cell;
        if (vehicle.Length == 1)
        {
            var color = ImGui.GetColorU32(RiderColor);
            var centerX = left + cell * 0.5f;
            var body = new Vector2(centerX - direction * cell * 0.06f, top + cell * 0.58f);
            var head = new Vector2(centerX + direction * cell * 0.26f, top + cell * 0.26f);
            ProgressRing.Glow(body, cell * 0.4f, RiderColor, 0.3f);
            drawList.AddLine(body, head, color, MathF.Max(1.5f, cell * 0.14f));
            drawList.AddCircleFilled(body, cell * 0.28f, color, 14);
            drawList.AddCircleFilled(head, cell * 0.14f, color, 10);
            drawList.AddTriangleFilled(new Vector2(head.X + direction * cell * 0.12f, head.Y - cell * 0.06f),
                new Vector2(head.X + direction * cell * 0.3f, head.Y), new Vector2(head.X + direction * cell * 0.12f, head.Y + cell * 0.06f),
                color);
            var legWidth = MathF.Max(1f, cell * 0.08f);
            drawList.AddRectFilled(new Vector2(centerX - cell * 0.16f, top + cell * 0.76f), new Vector2(centerX - cell * 0.16f + legWidth, top + cell * 0.95f), color);
            drawList.AddRectFilled(new Vector2(centerX + cell * 0.08f, top + cell * 0.76f), new Vector2(centerX + cell * 0.08f + legWidth, top + cell * 0.95f), color);
            return;
        }

        var cart = ImGui.GetColorU32(VehicleColor);
        var bodyMin = new Vector2(left + cell * 0.08f, top + cell * 0.3f);
        var bodyMax = new Vector2(left + cell * 1.92f, top + cell * 0.68f);
        ProgressRing.Glow((bodyMin + bodyMax) * 0.5f, cell * 0.7f, VehicleColor, 0.25f);
        drawList.AddRectFilled(bodyMin, bodyMax, cart, cell * 0.12f);
        var canopyLeft = left + (direction > 0f ? cell * 0.15f : cell * 1.0f);
        drawList.AddRectFilled(new Vector2(canopyLeft, top + cell * 0.12f), new Vector2(canopyLeft + cell * 0.85f, top + cell * 0.34f), cart,
            cell * 0.1f);
        var hub = ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.08f, 1f));
        drawList.AddCircleFilled(new Vector2(left + cell * 0.45f, top + cell * 0.76f), cell * 0.17f, cart, 12);
        drawList.AddCircleFilled(new Vector2(left + cell * 1.55f, top + cell * 0.76f), cell * 0.17f, cart, 12);
        drawList.AddCircleFilled(new Vector2(left + cell * 0.45f, top + cell * 0.76f), cell * 0.06f, hub, 8);
        drawList.AddCircleFilled(new Vector2(left + cell * 1.55f, top + cell * 0.76f), cell * 0.06f, hub, 8);
    }

    private static void DrawHopper(ImDrawListPtr drawList, HopBoard board, Rect boardRect, float cell, float time)
    {
        var center = CellCenter(boardRect, cell, board.X, board.Row);
        if (board.BumpFlash > 0f)
        {
            center.Y += cell * 0.3f * MathF.Min(1f, board.BumpFlash * 3f);
        }

        var alpha = board.Dying ? (MathF.Sin(time * 12f) > 0f ? 0.9f : 0.15f) : 1f;
        DrawHopperSprite(drawList, center, cell, board.HopFlash > 0f, alpha);
    }

    private static void DrawHopperSprite(ImDrawListPtr drawList, Vector2 center, float size, bool hopFrame, float alpha)
    {
        var body = ImGui.GetColorU32(HopperColor with { W = alpha });
        var mask = ImGui.GetColorU32(HopperMask with { W = alpha });
        var eye = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        var min = center - new Vector2(size * 0.5f, size * 0.5f);
        drawList.AddTriangleFilled(min + new Vector2(size * 0.18f, size * 0.3f), min + new Vector2(size * 0.24f, size * 0.04f),
            min + new Vector2(size * 0.42f, size * 0.26f), body);
        drawList.AddTriangleFilled(min + new Vector2(size * 0.58f, size * 0.26f), min + new Vector2(size * 0.76f, size * 0.04f),
            min + new Vector2(size * 0.82f, size * 0.3f), body);
        drawList.AddRectFilled(min + new Vector2(size * 0.14f, size * 0.22f), min + new Vector2(size * 0.86f, size * 0.92f), body,
            size * 0.26f);
        drawList.AddRectFilled(min + new Vector2(size * 0.18f, size * 0.36f), min + new Vector2(size * 0.82f, size * 0.5f), mask,
            size * 0.06f);
        drawList.AddCircleFilled(min + new Vector2(size * 0.34f, size * 0.43f), size * 0.055f, eye, 8);
        drawList.AddCircleFilled(min + new Vector2(size * 0.66f, size * 0.43f), size * 0.055f, eye, 8);
        if (hopFrame)
        {
            drawList.AddRectFilled(min + new Vector2(size * 0.02f, size * 0.66f), min + new Vector2(size * 0.16f, size * 0.78f), body, size * 0.05f);
            drawList.AddRectFilled(min + new Vector2(size * 0.84f, size * 0.66f), min + new Vector2(size * 0.98f, size * 0.78f), body, size * 0.05f);
            return;
        }

        drawList.AddRectFilled(min + new Vector2(size * 0.30f, size * 0.84f), min + new Vector2(size * 0.42f, size * 0.92f), mask, size * 0.03f);
        drawList.AddRectFilled(min + new Vector2(size * 0.58f, size * 0.84f), min + new Vector2(size * 0.70f, size * 0.92f), mask, size * 0.03f);
    }
}
