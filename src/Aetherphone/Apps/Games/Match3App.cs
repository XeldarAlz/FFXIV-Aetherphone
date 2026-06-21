using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Games;

internal enum GsPhase
{
    Idle,
    SwapAnim,
    SwapBackAnim,
    MatchPop,
    FallAnim,
}

internal sealed class Match3App : IPhoneApp
{
    private const int C = 7;
    private const int R = 7;
    private const int N = C * R;
    private const int Colors = 6;
    private const float NoFall = -999f;

    private const float SwapDur = 0.14f;
    private const float SwapBackDur = 0.12f;
    private const float MatchPopDur = 0.25f;
    private const float FallBaseDur = 0.08f;
    private const float FallPerRowDur = 0.05f;
    private const float SpawnBaseDur = 0.10f;
    private const float SpawnPerRowDur = 0.04f;
    private const float ColumnStagger = 0.04f;
    private const float HintDelay = 6f;
    private const float ShakeSpeed = 14f;
    private const float ShakeAmplitude = 3f;

    private static readonly Vector4[] GemColors =
    {
        new(0.95f, 0.91f, 0.24f, 1f),
        new(0.12f, 0.53f, 0.90f, 1f),
        new(1.00f, 1.00f, 1.00f, 1f),
        new(1.00f, 0.43f, 0.00f, 1f),
        new(0.00f, 0.79f, 0.71f, 1f),
        new(0.85f, 0.11f, 0.38f, 1f),
    };

    private static readonly string[] GemSymbols = { "\u2605", "\u25C6", "\u25CF", "\u25B2", "\u25A0", "\u2665" };

    public string Id => "match3";
    public string DisplayName => "Gem Swap";
    public string Glyph => "G";
    public Vector4 Accent => new(0.70f, 0.40f, 0.95f, 1f);
    public int BadgeCount => 0;

    private readonly int[] b = new int[N];
    private readonly bool[] m = new bool[N];
    private readonly float[] fallSrc = new float[N];
    private readonly float[] fallTime = new float[N];
    private readonly float[] fallDur = new float[N];
    private readonly Random rng = new();

    private GsPhase phase;
    private int sel = -1;
    private int sA = -1, sB = -1;
    private float swapT;
    private int score;
    private int chain;
    private float timer;
    private float idleTime;
    private int hintA = -1, hintB = -1;
    private PhoneTheme th = PhoneTheme.Default;

    public void OnOpened() => Reset();
    public void OnClosed() { }

    public void Draw(in PhoneContext ctx)
    {
        th = ctx.Theme;
        var body = GameCommon.LayoutBelowHeader(ctx.Content);
        AppHeader.Draw(ctx, DisplayName);

        using (AppSurface.Begin(body))
        {
            var dt = ImGui.GetIO().DeltaTime;
            var area = new Rect(body.Min, body.Min + ImGui.GetContentRegionAvail());
            var sc = ImGuiHelpers.GlobalScale;

            Tick(dt);

            var sy = body.Min.Y + 24f * sc;
            GameCommon.DrawScorePill(new Vector2(area.Center.X, sy), "Score", score, th);

            var gt = sy + 42f * sc;
            if (chain > 1)
            {
                Typography.DrawCentered(new Vector2(area.Center.X, sy + 28f * sc), $"x{chain}", th.Accent, 1f);
            }

            var g = GameCommon.LayoutGameGrid(new Rect(new Vector2(body.Min.X, gt), new Vector2(body.Max.X, area.Max.Y)), C, R, 0.06f);
            var ch = (g.Max.Y - g.Min.Y) / R;

            DrawGrid(g, ch, sc);
        }
    }

    private void Reset()
    {
        for (var safety = 0; safety < 500; safety++)
        {
            for (var i = 0; i < N; i++) b[i] = rng.Next(Colors);
            FindMatches();
            if (!AnyMatch()) break;
            for (var i = 0; i < N; i++) if (m[i]) b[i] = rng.Next(Colors);
        }

        sel = -1; sA = -1; sB = -1; swapT = 0f;
        phase = GsPhase.Idle;
        score = 0; chain = 0; timer = 0f; idleTime = 0f;
        hintA = -1; hintB = -1;
        ClearFallData();
    }

    private void ClearFallData()
    {
        for (var i = 0; i < N; i++) { fallSrc[i] = NoFall; fallTime[i] = 0f; fallDur[i] = 0f; }
    }

    private void Tick(float dt)
    {
        timer += dt;

        switch (phase)
        {
            case GsPhase.SwapAnim:
                TickSwap(dt, GsPhase.MatchPop, GsPhase.SwapBackAnim);
                break;
            case GsPhase.SwapBackAnim:
                TickSwapBack(dt);
                break;
            case GsPhase.MatchPop:
                TickMatchPop();
                break;
            case GsPhase.FallAnim:
                TickFall(dt);
                break;
            case GsPhase.Idle:
                idleTime += dt;
                if (idleTime >= HintDelay && hintA < 0)
                {
                    FindHint();
                }
                break;
        }
    }

    private void TickSwap(float dt, GsPhase onMatch, GsPhase onNoMatch)
    {
        swapT += dt / SwapDur;
        if (swapT < 1f) return;

        (b[sA], b[sB]) = (b[sB], b[sA]);
        FindMatches();
        if (AnyMatch())
        {
            chain = 1;
            phase = onMatch;
        }
        else
        {
            phase = onNoMatch;
            swapT = 0f;
        }
        timer = 0f;
    }

    private void TickSwapBack(float dt)
    {
        swapT += dt / SwapBackDur;
        if (swapT < 1f) return;

        (b[sA], b[sB]) = (b[sB], b[sA]);
        phase = GsPhase.Idle;
        sA = -1; sB = -1; swapT = 0f;
        timer = 0f; idleTime = 0f; hintA = -1; hintB = -1;
        EnsureMovesExist();
    }

    private void TickMatchPop()
    {
        if (timer < MatchPopDur) return;

        Remove();
        SetupFall();
        phase = GsPhase.FallAnim;
        timer = 0f;
    }

    private void TickFall(float dt)
    {
        var allDone = true;
        for (var i = 0; i < N; i++)
        {
            if (b[i] < 0) continue;
            var src = fallSrc[i];
            if (src == NoFall) continue;

            var dst = i / C;
            if (src >= dst)
            {
                fallSrc[i] = NoFall;
                continue;
            }

            fallTime[i] += dt;
            if (fallTime[i] < 0f)
            {
                allDone = false;
                continue;
            }

            if (fallTime[i] >= fallDur[i])
            {
                fallSrc[i] = NoFall;
            }
            else
            {
                allDone = false;
            }
        }

        if (!allDone) return;

        ClearFallData();
        FindMatches();
        if (AnyMatch())
        {
            chain++;
            phase = GsPhase.MatchPop;
        }
        else
        {
            chain = 0;
            sA = -1; sB = -1; swapT = 0f;
            phase = GsPhase.Idle;
            idleTime = 0f; hintA = -1; hintB = -1;
            EnsureMovesExist();
        }
        timer = 0f;
    }

    private void FindMatches()
    {
        for (var i = 0; i < N; i++) m[i] = false;

        for (var r = 0; r < R; r++)
        {
            var ci = 0;
            while (ci < C)
            {
                var col = b[r * C + ci];
                if (col < 0) { ci++; continue; }
                var end = ci;
                while (end + 1 < C && b[r * C + end + 1] == col) end++;
                if (end - ci + 1 >= 3)
                    for (var x = ci; x <= end; x++) m[r * C + x] = true;
                ci = end + 1;
            }
        }

        for (var c = 0; c < C; c++)
        {
            var ri = 0;
            while (ri < R)
            {
                var col = b[ri * C + c];
                if (col < 0) { ri++; continue; }
                var end = ri;
                while (end + 1 < R && b[(end + 1) * C + c] == col) end++;
                if (end - ri + 1 >= 3)
                    for (var y = ri; y <= end; y++) m[y * C + c] = true;
                ri = end + 1;
            }
        }
    }

    private bool AnyMatch()
    {
        for (var i = 0; i < N; i++) if (m[i]) return true;
        return false;
    }

    private void Remove()
    {
        var ct = 0;
        for (var i = 0; i < N; i++)
        {
            if (!m[i]) continue;
            b[i] = -1; ct++;
        }

        score += (ct >= 5 ? ct * 12 : ct >= 4 ? 60 : 30) * chain;

        for (var i = 0; i < N; i++) m[i] = false;
    }

    private void SetupFall()
    {
        var prev = new int[N];
        Array.Copy(b, prev, N);

        for (var c = 0; c < C; c++)
        {
            var writeRow = R - 1;
            for (var r = R - 1; r >= 0; r--)
            {
                var idx = r * C + c;
                if (b[idx] < 0) continue;
                if (r != writeRow)
                {
                    b[writeRow * C + c] = b[idx];
                    b[idx] = -1;
                }
                writeRow--;
            }
        }

        for (var i = 0; i < N; i++)
            if (b[i] < 0) b[i] = rng.Next(Colors);

        ClearFallData();

        for (var c = 0; c < C; c++)
        {
            var oldCount = 0;
            var oldRows = new int[R];
            for (var r = 0; r < R; r++)
                if (prev[r * C + c] >= 0) oldRows[oldCount++] = r;

            var newCount = 0;
            var newRows = new int[R];
            for (var r = 0; r < R; r++)
                if (b[r * C + c] >= 0) newRows[newCount++] = r;

            var spawnedCount = newCount - oldCount;

            for (var k = 0; k < oldCount; k++)
            {
                var newRow = newRows[spawnedCount + k];
                var oldRow = oldRows[k];
                if (oldRow >= newRow) continue;

                var idx = newRow * C + c;
                fallSrc[idx] = oldRow;
                fallDur[idx] = FallBaseDur + (newRow - oldRow) * FallPerRowDur;
                fallTime[idx] = -(c * ColumnStagger);
            }

            for (var k = 0; k < spawnedCount; k++)
            {
                var newRow = newRows[k];
                var idx = newRow * C + c;
                fallSrc[idx] = -(spawnedCount - k);
                fallDur[idx] = SpawnBaseDur + (newRow - fallSrc[idx]) * SpawnPerRowDur;
                fallTime[idx] = -(c * ColumnStagger + (spawnedCount - k) * 0.015f);
            }
        }
    }

    private void DrawGrid(Rect g, float ch, float sc)
    {
        var clickable = phase == GsPhase.Idle;
        var isSwapPhase = phase == GsPhase.SwapAnim || phase == GsPhase.SwapBackAnim;
        var isMatchPop = phase == GsPhase.MatchPop;
        var isFall = phase == GsPhase.FallAnim;

        if (isMatchPop)
        {
            DrawMatchPopGlow(g, sc);
        }

        for (var r = 0; r < R; r++)
        {
            for (var c = 0; c < C; c++)
            {
                var idx = r * C + c;
                if (b[idx] < 0) continue;

                var (cellMin, cellMax) = Cell(g, c, r);
                var cx = (cellMin.X + cellMax.X) * 0.5f;
                var cy = (cellMin.Y + cellMax.Y) * 0.5f;
                var hw = (cellMax.X - cellMin.X) * 0.5f;
                var hh = (cellMax.Y - cellMin.Y) * 0.5f;
                var alpha = 1f;
                var drawCx = cx;
                var drawCy = cy;
                var drawHw = hw;
                var drawHh = hh;

                if (isSwapPhase)
                {
                    ApplySwapTransform(idx, g, ref drawCx, ref drawCy);
                }

                if (isFall && fallSrc[idx] != NoFall)
                {
                    ApplyFallTransform(idx, ch, ref drawCy, ref drawHh);
                }

                if (isMatchPop && m[idx])
                {
                    ApplyMatchPopTransform(ref drawHw, ref drawHh, ref alpha);
                }

                if (clickable && (idx == hintA || idx == hintB))
                {
                    drawCx += MathF.Sin(idleTime * ShakeSpeed) * ShakeAmplitude * sc;
                    drawCy += MathF.Cos(idleTime * ShakeSpeed * 1.3f) * ShakeAmplitude * sc;
                }

                if (alpha < 0.01f) continue;

                var dmin = new Vector2(drawCx - drawHw, drawCy - drawHh);
                var dmax = new Vector2(drawCx + drawHw, drawCy + drawHh);

                var col = b[idx];
                var color = GemColors[col];
                GameCommon.FillRect(dmin, dmax, Styling.WithAlpha(color, alpha), 5f * sc);

                if (!isMatchPop || !m[idx])
                {
                    GameCommon.FillRect(dmin, dmax, new Vector4(1f, 1f, 1f, 0.08f * alpha), 5f * sc);
                    GameCommon.DrawRect(dmin, dmax, Styling.WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.12f * alpha), 5f * sc, 1f);
                }

                var symbolScale = (drawHw * 2f) / 40f;
                var luminance = color.X * 0.299f + color.Y * 0.587f + color.Z * 0.114f;
                var symbolColor = luminance > 0.7f
                    ? Styling.WithAlpha(new Vector4(0f, 0f, 0f, 1f), alpha)
                    : Styling.WithAlpha(new Vector4(1f, 1f, 1f, 1f), alpha);
                Typography.DrawCentered(new Vector2(drawCx, drawCy), GemSymbols[col], symbolColor, symbolScale);

                if (clickable)
                {
                    DrawSelectionHighlight(idx, cellMin, cellMax, dmin, dmax, sc);
                }
            }
        }
    }

    private void DrawMatchPopGlow(Rect g, float sc)
    {
        for (var r = 0; r < R; r++)
        {
            for (var c = 0; c < C; c++)
            {
                var idx = r * C + c;
                if (!m[idx] || b[idx] < 0) continue;

                var (min, max) = Cell(g, c, r);
                var t = timer / MatchPopDur;
                if (t >= 1f) continue;

                var s = 1f + t * 0.4f;
                var cx = (min.X + max.X) * 0.5f;
                var cy = (min.Y + max.Y) * 0.5f;
                var hw = (max.X - min.X) * 0.5f * s;
                var hh = (max.Y - min.Y) * 0.5f * s;
                GameCommon.FillRect(
                    new Vector2(cx - hw, cy - hh),
                    new Vector2(cx + hw, cy + hh),
                    Styling.WithAlpha(GemColors[b[idx]], (1f - t) * 0.6f), 5f * sc);
            }
        }
    }

    private void ApplySwapTransform(int idx, Rect g, ref float drawCx, ref float drawCy)
    {
        if (idx != sA && idx != sB) return;

        var other = idx == sA ? sB : sA;
        var otherCol = other % C;
        var otherRow = other / C;
        var (otherMin, otherMax) = Cell(g, otherCol, otherRow);
        var otherCx = (otherMin.X + otherMax.X) * 0.5f;
        var otherCy = (otherMin.Y + otherMax.Y) * 0.5f;

        var t = swapT < 1f ? EaseOutBack(swapT) : 1f;
        drawCx = drawCx + (otherCx - drawCx) * t;
        drawCy = drawCy + (otherCy - drawCy) * t;
    }

    private void ApplyFallTransform(int idx, float ch, ref float drawCy, ref float drawHh)
    {
        var src = fallSrc[idx];
        var dst = idx / C;
        var totalDist = dst - src;
        if (totalDist <= 0f) return;

        var elapsed = fallTime[idx];
        if (elapsed < 0f) elapsed = 0f;

        var t = elapsed / fallDur[idx];
        if (t >= 1f)
        {
            var excess = elapsed - fallDur[idx];
            var bt = Clamp01(excess / 0.08f);
            var bounce = MathF.Sin(bt * MathF.PI * 2f) * MathF.Exp(-bt * 5f);
            drawCy += bounce * ch * 0.10f;
            drawHh = drawHh * (1f - MathF.Abs(bounce) * 0.3f);
        }
        else
        {
            var offset = totalDist * ch * (1f - EaseOutQuad(t));
            drawCy -= offset;
        }
    }

    private void ApplyMatchPopTransform(ref float drawHw, ref float drawHh, ref float alpha)
    {
        var t = timer / MatchPopDur;
        if (t >= 1f)
        {
            alpha = 0f;
            return;
        }

        var s = 1f + t * 0.35f;
        drawHw *= s;
        drawHh *= s;
        alpha = 1f - t * t;
    }

    private void DrawSelectionHighlight(int idx, Vector2 cellMin, Vector2 cellMax, Vector2 dmin, Vector2 dmax, float sc)
    {
        var hovered = GameCommon.HitTest(cellMin, cellMax);
        if (hovered)
        {
            GameCommon.FillRect(dmin, dmax, new Vector4(1f, 1f, 1f, 0.2f), 5f * sc);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            if (sel == idx)
                GameCommon.DrawRect(dmin, dmax, th.Accent, 5f * sc, 2.5f * sc);

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                Click(idx);
        }
        else if (sel == idx)
        {
            GameCommon.DrawRect(dmin, dmax, th.Accent, 5f * sc, 2.5f * sc);
        }
    }

    private static (Vector2, Vector2) Cell(Rect g, int c, int r)
    {
        var cs = g.Width / C;
        var gap = cs * 0.06f;
        var hg = gap * 0.5f;
        var min = new Vector2(g.Min.X + c * cs + hg, g.Min.Y + r * cs + hg);
        var max = new Vector2(min.X + cs - gap, min.Y + cs - gap);
        return (min, max);
    }

    private void Click(int idx)
    {
        idleTime = 0f; hintA = -1; hintB = -1;

        if (sel < 0) { sel = idx; return; }
        if (sel == idx) { sel = -1; return; }

        var ac = sel % C; var ar = sel / C;
        var bc = idx % C; var br = idx / C;
        if (Math.Abs(ac - bc) + Math.Abs(ar - br) != 1) { sel = idx; return; }

        sA = sel; sB = idx; sel = -1;
        phase = GsPhase.SwapAnim; swapT = 0f; timer = 0f;
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var t1 = t - 1f;
        return 1f + c3 * (t1 * t1 * t1) + c1 * (t1 * t1);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private void EnsureMovesExist()
    {
        if (HasPossibleMoves()) return;

        for (var safety = 0; safety < 200; safety++)
        {
            for (var i = 0; i < N; i++) b[i] = rng.Next(Colors);
            FindMatches();
            while (AnyMatch())
            {
                for (var i = 0; i < N; i++) if (m[i]) b[i] = rng.Next(Colors);
                FindMatches();
            }
            if (HasPossibleMoves()) return;
        }
    }

    private bool HasPossibleMoves()
    {
        for (var r = 0; r < R; r++)
        {
            for (var c = 0; c < C - 1; c++)
            {
                var cellA = r * C + c;
                var cellB = r * C + c + 1;
                if (b[cellA] != b[cellB] && SwapCreatesMatch(cellA, cellB)) return true;
            }
        }

        for (var r = 0; r < R - 1; r++)
        {
            for (var c = 0; c < C; c++)
            {
                var cellA = r * C + c;
                var cellB = (r + 1) * C + c;
                if (b[cellA] != b[cellB] && SwapCreatesMatch(cellA, cellB)) return true;
            }
        }

        return false;
    }

    private bool SwapCreatesMatch(int idxA, int idxB)
    {
        var colA = b[idxA];
        var colB = b[idxB];

        b[idxA] = colB;
        b[idxB] = colA;

        var result = CheckMatchAt(idxA) || CheckMatchAt(idxB);

        b[idxA] = colA;
        b[idxB] = colB;

        return result;
    }

    private bool CheckMatchAt(int idx)
    {
        var r = idx / C;
        var c = idx % C;
        var col = b[idx];

        var left = c;
        while (left > 0 && b[r * C + left - 1] == col) left--;
        var right = c;
        while (right + 1 < C && b[r * C + right + 1] == col) right++;
        if (right - left + 1 >= 3) return true;

        var top = r;
        while (top > 0 && b[(top - 1) * C + c] == col) top--;
        var bottom = r;
        while (bottom + 1 < R && b[(bottom + 1) * C + c] == col) bottom++;
        if (bottom - top + 1 >= 3) return true;

        return false;
    }

    private void FindHint()
    {
        for (var r = 0; r < R; r++)
        {
            for (var c = 0; c < C - 1; c++)
            {
                var cellA = r * C + c;
                var cellB = r * C + c + 1;
                if (b[cellA] != b[cellB] && SwapCreatesMatch(cellA, cellB))
                {
                    hintA = cellA; hintB = cellB; return;
                }
            }
        }

        for (var r = 0; r < R - 1; r++)
        {
            for (var c = 0; c < C; c++)
            {
                var cellA = r * C + c;
                var cellB = (r + 1) * C + c;
                if (b[cellA] != b[cellB] && SwapCreatesMatch(cellA, cellB))
                {
                    hintA = cellA; hintB = cellB; return;
                }
            }
        }
    }

    public void Dispose() { }
}
