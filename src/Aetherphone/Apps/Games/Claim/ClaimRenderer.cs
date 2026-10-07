using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Claim;

internal sealed class ClaimRenderer
{
    public static readonly Vector4 SlowColor = new(1f, 0.78f, 0.32f, 1f);
    public static readonly Vector4 FuseColor = new(1f, 0.56f, 0.22f, 1f);
    public static readonly Vector4 SparkColor = new(1f, 0.86f, 0.55f, 1f);
    public static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public const float RevealBand = 7f;
    private const int MaxRuns = ClaimBoard.Width / 2 + 2;
    private const int MaxCorners = 2048;
    private const int GridPitch = 16;
    private const float PlayerRadius = 2.6f;
    private const float SparkRadius = 1.8f;
    private const float BossWidth = 0.9f;
    private const float SheenPeriod = 7f;
    private static readonly Vector4 CoverTop = new(0.035f, 0.045f, 0.10f, 1f);
    private static readonly Vector4 CoverBottom = new(0.02f, 0.025f, 0.06f, 1f);
    private static readonly Vector4 Ash = new(0.25f, 0.22f, 0.24f, 0.9f);
    private static readonly Vector4 BossPink = new(1f, 0.36f, 0.74f, 1f);
    private static readonly Vector4 BossCyan = new(0.36f, 0.92f, 1f, 1f);

    private readonly int[] activeStart = new int[MaxRuns];
    private readonly int[] activeEnd = new int[MaxRuns];
    private readonly int[] currentStart = new int[MaxRuns];
    private readonly int[] currentEnd = new int[MaxRuns];
    private readonly Vector2[] corners = new Vector2[MaxCorners];
    private int activeCount;
    private int activeTop;

    private enum RunMode : byte
    {
        Cover,
        Wave,
    }

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(ClaimBoard.Width, ClaimBoard.Height)));

    public static void DrawPicture(ImDrawListPtr drawList, ClaimMosaic mosaic, in Camera2D camera, Vector4 accent,
        float time, float scale)
    {
        var points = mosaic.Points;
        var facets = mosaic.Facets;
        var dark = GamePalette.Darken(accent, 0.62f);
        var light = GamePalette.Lighten(accent, 0.42f);
        var deep = GamePalette.Darken(accent, 0.35f);
        var pale = GamePalette.Lighten(accent, 0.7f);
        var outline = ImGui.GetColorU32(White with { W = 0.16f });
        var outlineWidth = MathF.Max(1f, 0.8f * scale);
        for (var index = 0; index < facets.Length; index++)
        {
            ref readonly var facet = ref facets[index];
            var first = camera.ToScreen(points[facet.First]);
            var second = camera.ToScreen(points[facet.Second]);
            var third = camera.ToScreen(points[facet.Third]);
            var color = Vector4.Lerp(dark, light, facet.Shade);
            color = facet.Tint < 0.5f
                ? Vector4.Lerp(color, deep, (0.5f - facet.Tint) * 0.6f)
                : Vector4.Lerp(color, pale, (facet.Tint - 0.5f) * 0.45f);
            drawList.AddTriangleFilled(first, second, third, ImGui.GetColorU32(color with { W = 1f }));
            drawList.AddTriangle(first, second, third, outline, outlineWidth);
        }

        DrawSheen(drawList, in camera, time);
        var glints = mosaic.Glints;
        var phases = mosaic.GlintPhases;
        for (var index = 0; index < glints.Length; index++)
        {
            var twinkle = 0.5f + 0.5f * MathF.Sin(time * 2.6f + phases[index]);
            if (twinkle < 0.35f)
            {
                continue;
            }

            var center = camera.ToScreen(points[glints[index]]);
            var arm = camera.Px(2.4f) * twinkle;
            var star = ImGui.GetColorU32(White with { W = 0.85f * twinkle });
            drawList.AddLine(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f), star, MathF.Max(1f, scale));
            drawList.AddLine(center - new Vector2(0f, arm), center + new Vector2(0f, arm), star, MathF.Max(1f, scale));
            drawList.AddCircleFilled(center, MathF.Max(1f, arm * 0.25f), star, 10);
        }
    }

    private static void DrawSheen(ImDrawListPtr drawList, in Camera2D camera, float time)
    {
        var field = FieldRect(in camera);
        var progress = time % SheenPeriod / SheenPeriod;
        var travel = field.Width + field.Height;
        var sheenLeft = field.Min.X - field.Height + travel * progress * 1.4f;
        var width = field.Width * 0.12f;
        drawList.PushClipRect(field.Min, field.Max, true);
        for (var band = 0; band < 3; band++)
        {
            var alpha = band == 1 ? 0.07f : 0.03f;
            var left = sheenLeft + band * width;
            var color = ImGui.GetColorU32(White with { W = alpha });
            drawList.AddQuadFilled(new Vector2(left, field.Max.Y), new Vector2(left + width, field.Max.Y),
                new Vector2(left + width + field.Height, field.Min.Y), new Vector2(left + field.Height, field.Min.Y), color);
        }

        drawList.PopClipRect();
    }

    public void DrawCover(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent, float front,
        float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        DrawRuns(drawList, board, in camera, RunMode.Cover, front, accent, alpha);
    }

    public void DrawWave(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent, float front)
    {
        if (front <= 0f || front > board.RevealDepth + RevealBand)
        {
            return;
        }

        DrawRuns(drawList, board, in camera, RunMode.Wave, front, accent, 1f);
    }

    private void DrawRuns(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, RunMode mode, float front,
        Vector4 accent, float alpha)
    {
        var cells = board.Cells;
        activeCount = 0;
        activeTop = 1;
        for (var row = 1; row < ClaimBoard.Height - 1; row++)
        {
            var count = 0;
            var runStart = -1;
            var rowStart = row * ClaimBoard.Width;
            for (var column = 1; column < ClaimBoard.Width - 1; column++)
            {
                var cell = rowStart + column;
                var inside = mode == RunMode.Cover
                    ? cells[cell] != ClaimCell.Claimed || (board.IsFresh(cell) && board.RevealAt(cell) > front)
                    : board.IsFresh(cell) && board.RevealAt(cell) <= front && board.RevealAt(cell) > front - RevealBand;
                if (inside && runStart < 0)
                {
                    runStart = column;
                }
                else if (!inside && runStart >= 0)
                {
                    count = AddRun(count, runStart, column - 1);
                    runStart = -1;
                }
            }

            if (runStart >= 0)
            {
                count = AddRun(count, runStart, ClaimBoard.Width - 2);
            }

            if (SameRuns(count))
            {
                continue;
            }

            Flush(drawList, in camera, mode, row, accent, alpha);
            Array.Copy(currentStart, activeStart, count);
            Array.Copy(currentEnd, activeEnd, count);
            activeCount = count;
            activeTop = row;
        }

        Flush(drawList, in camera, mode, ClaimBoard.Height - 1, accent, alpha);
    }

    private int AddRun(int count, int start, int end)
    {
        if (count >= MaxRuns)
        {
            return count;
        }

        currentStart[count] = start;
        currentEnd[count] = end;
        return count + 1;
    }

    private bool SameRuns(int count)
    {
        if (count != activeCount)
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            if (currentStart[index] != activeStart[index] || currentEnd[index] != activeEnd[index])
            {
                return false;
            }
        }

        return true;
    }

    private void Flush(ImDrawListPtr drawList, in Camera2D camera, RunMode mode, int bottomRow, Vector4 accent,
        float alpha)
    {
        if (activeCount == 0 || bottomRow <= activeTop)
        {
            return;
        }

        var topShade = activeTop / (float)ClaimBoard.Height;
        var bottomShade = bottomRow / (float)ClaimBoard.Height;
        uint top;
        uint bottom;
        if (mode == RunMode.Cover)
        {
            top = ImGui.GetColorU32(Vector4.Lerp(CoverTop, CoverBottom, topShade) with { W = alpha });
            bottom = ImGui.GetColorU32(Vector4.Lerp(CoverTop, CoverBottom, bottomShade) with { W = alpha });
        }
        else
        {
            top = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.6f) with { W = 0.75f });
            bottom = top;
        }

        var grid = ImGui.GetColorU32(accent with { W = 0.07f * alpha });
        for (var index = 0; index < activeCount; index++)
        {
            var min = camera.ToScreen(new Vector2(activeStart[index], activeTop));
            var max = camera.ToScreen(new Vector2(activeEnd[index] + 1, bottomRow));
            drawList.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
            if (mode != RunMode.Cover)
            {
                continue;
            }

            DrawGrid(drawList, in camera, activeStart[index], activeEnd[index] + 1, activeTop, bottomRow, grid);
        }
    }

    private static void DrawGrid(ImDrawListPtr drawList, in Camera2D camera, int left, int right, int top, int bottom,
        uint color)
    {
        var firstColumn = (left + GridPitch - 1) / GridPitch * GridPitch;
        for (var column = firstColumn; column < right; column += GridPitch)
        {
            drawList.AddLine(camera.ToScreen(new Vector2(column, top)), camera.ToScreen(new Vector2(column, bottom)),
                color);
        }

        var firstRow = (top + GridPitch - 1) / GridPitch * GridPitch;
        for (var row = firstRow; row < bottom; row += GridPitch)
        {
            drawList.AddLine(camera.ToScreen(new Vector2(left, row)), camera.ToScreen(new Vector2(right, row)), color);
        }
    }

    public static void DrawEdges(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent,
        float alpha, float scale)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var glow = ImGui.GetColorU32(accent with { W = 0.3f * alpha });
        var core = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.55f) with { W = alpha });
        var glowWidth = MathF.Max(2f, camera.Px(1.6f));
        var coreWidth = MathF.Max(1f, MathF.Min(camera.Px(0.6f), 1.6f * scale));
        var cells = board.Cells;
        for (var row = 1; row < ClaimBoard.Height; row++)
        {
            var runStart = -1;
            for (var column = 0; column <= ClaimBoard.Width; column++)
            {
                var edge = column < ClaimBoard.Width &&
                           (cells[(row - 1) * ClaimBoard.Width + column] == ClaimCell.Claimed) !=
                           (cells[row * ClaimBoard.Width + column] == ClaimCell.Claimed);
                if (edge && runStart < 0)
                {
                    runStart = column;
                }
                else if (!edge && runStart >= 0)
                {
                    Edge(drawList, camera.ToScreen(new Vector2(runStart, row)), camera.ToScreen(new Vector2(column, row)),
                        glow, core, glowWidth, coreWidth);
                    runStart = -1;
                }
            }
        }

        for (var column = 1; column < ClaimBoard.Width; column++)
        {
            var runStart = -1;
            for (var row = 0; row <= ClaimBoard.Height; row++)
            {
                var edge = row < ClaimBoard.Height &&
                           (cells[row * ClaimBoard.Width + column - 1] == ClaimCell.Claimed) !=
                           (cells[row * ClaimBoard.Width + column] == ClaimCell.Claimed);
                if (edge && runStart < 0)
                {
                    runStart = row;
                }
                else if (!edge && runStart >= 0)
                {
                    Edge(drawList, camera.ToScreen(new Vector2(column, runStart)), camera.ToScreen(new Vector2(column, row)),
                        glow, core, glowWidth, coreWidth);
                    runStart = -1;
                }
            }
        }

        var field = FieldRect(in camera);
        drawList.AddRect(field.Min, field.Max, ImGui.GetColorU32(accent with { W = 0.5f * alpha }), 0f,
            ImDrawFlags.None, MathF.Max(1f, scale));
    }

    private static void Edge(ImDrawListPtr drawList, Vector2 from, Vector2 to, uint glow, uint core, float glowWidth,
        float coreWidth)
    {
        drawList.AddLine(from, to, glow, glowWidth);
        drawList.AddLine(from, to, core, coreWidth);
    }

    public void DrawTrail(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent, bool dying,
        float scale)
    {
        if (!board.Drawing)
        {
            return;
        }

        var color = dying ? Danger : board.LineSlow ? SlowColor : GamePalette.Lighten(accent, 0.35f);
        var count = Corners(board, in camera, float.MaxValue);
        Stroke(drawList, count, ImGui.GetColorU32(color with { W = 0.3f }), MathF.Max(3f, camera.Px(2.2f)));
        Stroke(drawList, count, ImGui.GetColorU32(color), MathF.Max(1.5f, camera.Px(0.9f)));
        if (!board.FuseLit)
        {
            return;
        }

        var burnt = Corners(board, in camera, board.FuseTravel);
        Stroke(drawList, burnt, ImGui.GetColorU32(Ash), MathF.Max(2f, camera.Px(1.2f)));
        var fuse = camera.ToScreen(board.FusePosition);
        var flicker = 0.7f + 0.3f * MathF.Sin((float)ImGui.GetTime() * 40f);
        ProgressRing.Glow(fuse, camera.Px(3.4f) * flicker, FuseColor, 1f);
        drawList.AddCircleFilled(fuse, MathF.Max(2f, camera.Px(1.1f)) * flicker, ImGui.GetColorU32(White), 12);
    }

    private int Corners(ClaimBoard board, in Camera2D camera, float limit)
    {
        var trail = board.Trail;
        var count = 0;
        corners[count++] = camera.ToScreen(ClaimBoard.CellCenter(board.TrailStart));
        var previous = board.TrailStart;
        var lastStepX = 0;
        var lastStepY = 0;
        var last = Math.Min(trail.Length, (int)MathF.Min(limit, trail.Length));
        for (var index = 0; index < last; index++)
        {
            var cell = trail[index];
            var stepX = ClaimBoard.ColumnOf(cell) - ClaimBoard.ColumnOf(previous);
            var stepY = ClaimBoard.RowOf(cell) - ClaimBoard.RowOf(previous);
            if (index > 0 && (stepX != lastStepX || stepY != lastStepY) && count < MaxCorners - 2)
            {
                corners[count++] = camera.ToScreen(ClaimBoard.CellCenter(previous));
            }

            lastStepX = stepX;
            lastStepY = stepY;
            previous = cell;
        }

        corners[count++] = limit < trail.Length
            ? camera.ToScreen(board.FusePosition)
            : camera.ToScreen(ClaimBoard.CellCenter(previous));
        return count;
    }

    private void Stroke(ImDrawListPtr drawList, int count, uint color, float width)
    {
        if (count < 2)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            drawList.PathLineTo(corners[index]);
        }

        drawList.PathStroke(color, ImDrawFlags.None, width);
    }

    public static void DrawBoss(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent,
        float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var history = board.BossHistoryCount;
        var phase = board.BossPhase;
        var width = MathF.Max(1f, camera.Px(BossWidth));
        for (var age = history - 1; age >= 1; age--)
        {
            board.BossHistoryAt(age, out var head, out var tail);
            var fade = 1f - age / (float)history;
            var color = BossColor(accent, phase + age * 0.35f) with { W = alpha * fade * fade * 0.8f };
            drawList.AddLine(camera.ToScreen(head), camera.ToScreen(tail), ImGui.GetColorU32(color), width);
        }

        var headScreen = camera.ToScreen(board.BossHead);
        var tailScreen = camera.ToScreen(board.BossTail);
        var live = BossColor(accent, phase);
        drawList.AddLine(headScreen, tailScreen, ImGui.GetColorU32(live with { W = 0.35f * alpha }), width * 4f);
        var span = tailScreen - headScreen;
        var length = span.Length();
        var normal = length > 0.01f ? new Vector2(-span.Y, span.X) / length : Vector2.UnitY;
        for (var strand = -1; strand <= 1; strand += 2)
        {
            var wobble = normal * MathF.Sin(phase * 3f + strand) * camera.Px(1.4f);
            drawList.AddBezierCubic(headScreen, headScreen + span * 0.33f + wobble * strand,
                headScreen + span * 0.66f - wobble * strand, tailScreen,
                ImGui.GetColorU32(BossColor(accent, phase + strand) with { W = 0.7f * alpha }), width * 0.8f, 16);
        }

        drawList.AddLine(headScreen, tailScreen, ImGui.GetColorU32(White with { W = 0.9f * alpha }), width * 0.9f);
        ProgressRing.Glow(headScreen, camera.Px(3.2f), live, alpha);
        ProgressRing.Glow(tailScreen, camera.Px(3.2f), live, alpha);
        drawList.AddCircleFilled(headScreen, MathF.Max(1.5f, camera.Px(1f)), ImGui.GetColorU32(White with { W = alpha }), 10);
        drawList.AddCircleFilled(tailScreen, MathF.Max(1.5f, camera.Px(1f)), ImGui.GetColorU32(White with { W = alpha }), 10);
    }

    public static Vector4 BossColor(Vector4 accent, float phase)
    {
        var cycle = (phase * 0.25f % 3f + 3f) % 3f;
        if (cycle < 1f)
        {
            return Vector4.Lerp(BossPink, BossCyan, cycle);
        }

        if (cycle < 2f)
        {
            return Vector4.Lerp(BossCyan, GamePalette.Lighten(accent, 0.3f), cycle - 1f);
        }

        return Vector4.Lerp(GamePalette.Lighten(accent, 0.3f), BossPink, cycle - 2f);
    }

    public static void DrawSparks(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, float time,
        float scale)
    {
        for (var index = 0; index < board.SparkCount; index++)
        {
            var center = camera.ToScreen(board.SparkPosition(index));
            var spin = time * 7f + index * 1.3f;
            var radius = camera.Px(SparkRadius) * (0.85f + 0.15f * MathF.Sin(time * 18f + index));
            ProgressRing.Glow(center, radius * 2f, FuseColor, 0.9f);
            var axis = new Vector2(MathF.Cos(spin), MathF.Sin(spin)) * radius * 1.6f;
            var cross = new Vector2(-axis.Y, axis.X);
            var line = ImGui.GetColorU32(SparkColor);
            var thickness = MathF.Max(1f, 1.4f * scale);
            drawList.AddLine(center - axis, center + axis, line, thickness);
            drawList.AddLine(center - cross, center + cross, line, thickness);
            drawList.AddCircleFilled(center, MathF.Max(1.5f, radius * 0.45f), ImGui.GetColorU32(White), 10);
        }
    }

    public static void DrawPlayer(ImDrawListPtr drawList, ClaimBoard board, in Camera2D camera, Vector4 accent,
        float time, float scale)
    {
        if (board.State == ClaimState.Dying || board.State == ClaimState.Over)
        {
            return;
        }

        if (board.Shielded && MathF.Sin(time * 26f) < 0f)
        {
            return;
        }

        var center = camera.ToScreen(board.PlayerPosition);
        var radius = MathF.Max(4f * scale, camera.Px(PlayerRadius));
        var color = board.Drawing ? board.LineSlow ? SlowColor : GamePalette.Lighten(accent, 0.35f) : accent;
        ProgressRing.Glow(center, radius * 1.8f, color, 0.9f);
        if (board.Drawing)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 10f);
            drawList.AddCircle(center, radius * (1.4f + 0.3f * pulse), ImGui.GetColorU32(color with { W = 0.6f }), 20,
                MathF.Max(1f, 1.2f * scale));
        }

        var top = center + new Vector2(0f, -radius);
        var right = center + new Vector2(radius, 0f);
        var bottom = center + new Vector2(0f, radius);
        var left = center + new Vector2(-radius, 0f);
        drawList.AddQuadFilled(top, right, bottom, left, ImGui.GetColorU32(color));
        drawList.AddTriangleFilled(top, right, center, ImGui.GetColorU32(White with { W = 0.45f }));
        drawList.AddQuad(top, right, bottom, left, ImGui.GetColorU32(White with { W = 0.9f }), MathF.Max(1f, scale));
    }
}
