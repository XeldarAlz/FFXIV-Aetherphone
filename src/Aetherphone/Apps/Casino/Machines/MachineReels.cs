using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed class MachineReels
{
    public const float RowsPerSecond = 16f;
    public const float SymbolShare = 0.36f;
    public const float TraceCycleSeconds = 0.8f;

    private static readonly Vector4 Gold = new(1f, 0.80f, 0.30f, 1f);
    private static readonly Vector4 SlotInk = new(0f, 0f, 0f, 0.42f);

    private readonly bool[] winning = new bool[MachineRoundPlayback.MaxCells];
    private readonly bool[] previousWinning = new bool[MachineRoundPlayback.MaxCells];
    private readonly int[] restingGrid = new int[MachineRoundPlayback.MaxCells];
    private readonly int[] traceOrder = new int[64];
    private readonly int[] survivorRows = new int[MachineRoundPlayback.MaxCells];

    private int traceCount;
    private int tracedStep = -1;
    private float traceSeconds;
    private int shatteredStep = -1;
    private string restingMachine = string.Empty;

    public void Rest(string machineId)
    {
        if (string.Equals(restingMachine, machineId, StringComparison.Ordinal))
        {
            return;
        }

        restingMachine = machineId;
        var info = SlotsMachines.For(machineId);
        for (var cell = 0; cell < info.CellCount; cell++)
        {
            restingGrid[cell] = MachineSymbols.BlurSymbol(machineId, cell * 3 + cell / info.Rows);
        }
    }

    public void Remember(int[] grid)
    {
        Array.Copy(grid, restingGrid, Math.Min(grid.Length, restingGrid.Length));
    }

    public void Draw(ImDrawListPtr drawList, Rect window, MachineRoundPlayback playback, string machineId,
        float phase, float deltaSeconds, ParticleSystem particles, float scale)
    {
        var info = SlotsMachines.For(machineId);
        traceSeconds += deltaSeconds;
        var cellWidth = window.Width / info.Reels;
        var cellHeight = window.Height / info.Rows;
        var extent = MathF.Min(cellWidth, cellHeight) * SymbolShare;
        var current = playback.HasRound ? playback.Current : null;
        var beat = playback.Beat;
        if (playback.HasRound && playback.InHold)
        {
            DrawHold(drawList, window, playback, info, extent, phase, scale);
            return;
        }

        var grid = GridFor(playback, current);
        PrepareWins(playback, current);
        for (var reel = 0; reel < info.Reels; reel++)
        {
            var columnMin = new Vector2(window.Min.X + reel * cellWidth, window.Min.Y);
            var columnMax = new Vector2(columnMin.X + cellWidth, window.Max.Y);
            if (reel > 0)
            {
                drawList.AddLine(columnMin, new Vector2(columnMin.X, window.Max.Y),
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), MathF.Max(1f, scale));
            }

            drawList.PushClipRect(columnMin, columnMax, true);
            if (beat == MachineBeat.Spin && !playback.ReelStopped(reel))
            {
                DrawSpinning(drawList, playback, info, grid, reel, columnMin, cellWidth, cellHeight, extent);
            }
            else if (beat == MachineBeat.Tumble && playback.Previous?.Grid is { } before)
            {
                DrawTumble(drawList, playback, info, before, grid, reel, columnMin, cellWidth, cellHeight, extent,
                    particles, scale);
            }
            else
            {
                DrawRested(drawList, playback, info, grid, current, reel, columnMin, cellWidth, cellHeight, extent,
                    phase);
            }

            drawList.PopClipRect();
            if (playback.Anticipating(reel))
            {
                var pulse = 0.45f + 0.35f * MathF.Sin(playback.BeatSeconds * 9f);
                Squircle.Stroke(drawList, columnMin, columnMax, 8f * scale, ImGui.GetColorU32(Gold with { W = pulse }),
                    3f * scale);
                drawList.AddRectFilled(columnMin, columnMax, ImGui.GetColorU32(Gold with { W = 0.08f * pulse }));
            }
        }

        if (beat == MachineBeat.Expand)
        {
            DrawExpand(drawList, window, playback, info, cellWidth, extent, scale);
        }

        if (Presenting(playback) && current is not null)
        {
            DrawTraces(drawList, window, current, info, cellWidth, cellHeight, scale);
            DrawGiant(drawList, window, playback, current, info, cellWidth, cellHeight, phase);
        }
        else if (current is not null && SlotsRules.MoogleId == info.Id && playback.Beat != MachineBeat.Spin)
        {
            DrawGiant(drawList, window, playback, current, info, cellWidth, cellHeight, phase);
        }
    }

    public static bool Presenting(MachineRoundPlayback playback)
    {
        return playback.HasRound && (playback.Beat == MachineBeat.Present || playback.Beat == MachineBeat.Done
            || playback.Beat == MachineBeat.Outro);
    }

    private int[] GridFor(MachineRoundPlayback playback, CasinoSlotsStepDto? current)
    {
        if (current?.Grid is { Length: > 0 } grid)
        {
            if (playback.Beat != MachineBeat.Spin || playback.StoppedReels() >= playback.Info.Reels)
            {
                Remember(grid);
            }

            return grid;
        }

        return restingGrid;
    }

    private void PrepareWins(MachineRoundPlayback playback, CasinoSlotsStepDto? current)
    {
        Array.Clear(winning);
        if (current is null || !Presenting(playback))
        {
            return;
        }

        MarkCells(current, winning);
        if (tracedStep == playback.StepIndex)
        {
            return;
        }

        tracedStep = playback.StepIndex;
        traceSeconds = 0f;
        traceCount = 0;
        if (current.Wins is not { } wins)
        {
            return;
        }

        for (var index = 0; index < wins.Length && traceCount < traceOrder.Length; index++)
        {
            var slot = traceCount;
            while (slot > 0 && wins[traceOrder[slot - 1]].Pay < wins[index].Pay)
            {
                traceOrder[slot] = traceOrder[slot - 1];
                slot--;
            }

            traceOrder[slot] = index;
            traceCount++;
        }
    }

    private static void MarkCells(CasinoSlotsStepDto step, bool[] mask)
    {
        if (step.Wins is not { } wins)
        {
            return;
        }

        for (var index = 0; index < wins.Length; index++)
        {
            if (wins[index].Cells is not { } cells)
            {
                continue;
            }

            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                if (cell >= 0 && cell < mask.Length)
                {
                    mask[cell] = true;
                }
            }
        }
    }

    private static void DrawSpinning(ImDrawListPtr drawList, MachineRoundPlayback playback, SlotsMachineInfo info,
        int[] grid, int reel, Vector2 columnMin, float cellWidth, float cellHeight, float extent)
    {
        var landing = playback.LandingProgress(reel);
        var centerX = columnMin.X + cellWidth * 0.5f;
        if (landing <= 0f)
        {
            var travel = playback.BeatSeconds * RowsPerSecond * MathF.Min(1f, playback.BeatSeconds /
                MathF.Max(0.01f, MachineTiming.Ramp));
            var whole = (int)travel;
            var fraction = travel - whole;
            for (var slot = -1; slot <= info.Rows; slot++)
            {
                var symbol = MachineSymbols.BlurSymbol(info.Id, playback.BlurOffset(reel) - whole - slot);
                var centerY = columnMin.Y + (slot + fraction + 0.5f) * cellHeight;
                MachineSymbols.Draw(drawList, info.Id, symbol, new Vector2(centerX, centerY - cellHeight * 0.2f),
                    extent, 0.18f, false);
                MachineSymbols.Draw(drawList, info.Id, symbol, new Vector2(centerX, centerY), extent, 0.6f, false);
            }

            return;
        }

        var eased = Easing.EaseOutCubic(landing);
        var offset = (eased - 1f) * info.Rows * cellHeight;
        for (var row = 0; row < info.Rows; row++)
        {
            var center = new Vector2(centerX, columnMin.Y + (row + 0.5f) * cellHeight + offset);
            MachineSymbols.Draw(drawList, info.Id, grid[reel * info.Rows + row], center, extent, 1f, false);
            var trailing = MachineSymbols.BlurSymbol(info.Id, playback.BlurOffset(reel) + row);
            MachineSymbols.Draw(drawList, info.Id, trailing,
                new Vector2(centerX, center.Y + info.Rows * cellHeight), extent, 0.6f, false);
        }
    }

    private void DrawRested(ImDrawListPtr drawList, MachineRoundPlayback playback, SlotsMachineInfo info, int[] grid,
        CasinoSlotsStepDto? current, int reel, Vector2 columnMin, float cellWidth, float cellHeight, float extent,
        float phase)
    {
        var anyWin = Presenting(playback) && HasAny(winning, info.CellCount);
        var settledLoss = playback.Finished && playback.TotalWin + playback.Jackpot <= playback.Cost;
        for (var row = 0; row < info.Rows; row++)
        {
            var cell = reel * info.Rows + row;
            var center = new Vector2(columnMin.X + cellWidth * 0.5f, columnMin.Y + (row + 0.5f) * cellHeight);
            var lit = anyWin && winning[cell];
            var alpha = anyWin && !lit ? 0.4f : 1f;
            var size = extent;
            if (lit)
            {
                var pulse = 0.5f + 0.5f * MathF.Sin(phase * 7f + cell * 0.4f);
                var ink = settledLoss ? CasinoColors.InkMuted : Gold;
                drawList.AddCircleFilled(center, MathF.Min(cellWidth, cellHeight) * 0.46f,
                    ImGui.GetColorU32(ink with { W = 0.10f + 0.10f * pulse }), 28);
                size = extent * (1f + (settledLoss ? 0f : 0.08f * pulse));
            }

            var symbol = grid[cell];
            if (SlotsRules.MoogleId == info.Id && GiantCell(playback, current, reel))
            {
                continue;
            }

            MachineSymbols.Draw(drawList, info.Id, symbol, center, size, alpha,
                !anyWin && MachineSymbols.ShimmerFrame(cell, phase));
            DrawCoinLabel(drawList, current, cell, center, extent, alpha, phase);
        }
    }

    private static bool GiantCell(MachineRoundPlayback playback, CasinoSlotsStepDto? current, int reel)
    {
        return current is not null && string.Equals(current.Kind, SlotsRules.StepGame, StringComparison.Ordinal)
            && MoogleMoneyRules.IsGiantReel(reel) && playback.ReelStopped(MoogleMoneyRules.GiantLastReel);
    }

    private static void DrawGiant(ImDrawListPtr drawList, Rect window, MachineRoundPlayback playback,
        CasinoSlotsStepDto current, SlotsMachineInfo info, float cellWidth, float cellHeight, float phase)
    {
        if (!GiantCell(playback, current, MoogleMoneyRules.GiantFirstReel) || current.Expander < 0)
        {
            return;
        }

        var min = new Vector2(window.Min.X + MoogleMoneyRules.GiantFirstReel * cellWidth, window.Min.Y);
        var max = new Vector2(window.Min.X + (MoogleMoneyRules.GiantLastReel + 1) * cellWidth, window.Max.Y);
        var center = (min + max) * 0.5f;
        var extent = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.4f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.4f, 0.08f)));
        MachineSymbols.Draw(drawList, info.Id, current.Expander, center, extent, 1f,
            MachineSymbols.ShimmerFrame(1, phase));
    }

    private void DrawTumble(ImDrawListPtr drawList, MachineRoundPlayback playback, SlotsMachineInfo info,
        int[] before, int[] after, int reel, Vector2 columnMin, float cellWidth, float cellHeight, float extent,
        ParticleSystem particles, float scale)
    {
        var shatter = MachineTiming.Scaled(MachineTiming.Shatter, playback.Turbo);
        var fall = MachineTiming.Scaled(MachineTiming.Fall, playback.Turbo);
        var seconds = playback.BeatSeconds;
        Array.Clear(previousWinning);
        if (playback.Previous is { } previousStep)
        {
            MarkCells(previousStep, previousWinning);
        }

        var centerX = columnMin.X + cellWidth * 0.5f;
        if (seconds < shatter)
        {
            var shrink = 1f - seconds / shatter;
            for (var row = 0; row < info.Rows; row++)
            {
                var cell = reel * info.Rows + row;
                var center = new Vector2(centerX, columnMin.Y + (row + 0.5f) * cellHeight);
                if (previousWinning[cell])
                {
                    drawList.AddCircleFilled(center, extent * (1.3f - shrink * 0.3f),
                        ImGui.GetColorU32(new Vector4(0.7f, 0.95f, 1f, 0.35f * shrink)), 24);
                    MachineSymbols.Draw(drawList, info.Id, before[cell], center, extent * shrink, shrink, false);
                    if (shatteredStep != playback.StepIndex * 8 + reel)
                    {
                        particles.Emit(CasinoLights.Shard(scale), center, 6);
                    }

                    continue;
                }

                MachineSymbols.Draw(drawList, info.Id, before[cell], center, extent, 1f, false);
            }

            shatteredStep = playback.StepIndex * 8 + reel;
            return;
        }

        var progress = Easing.EaseInCubic(Math.Clamp((seconds - shatter) / MathF.Max(0.01f, fall), 0f, 1f));
        var survivors = 0;
        for (var row = info.Rows - 1; row >= 0; row--)
        {
            if (!previousWinning[reel * info.Rows + row])
            {
                survivorRows[survivors++] = row;
            }
        }

        var missing = info.Rows - survivors;
        for (var row = 0; row < info.Rows; row++)
        {
            var targetY = columnMin.Y + (row + 0.5f) * cellHeight;
            float startY;
            if (row >= missing)
            {
                var survivorIndex = info.Rows - 1 - row;
                startY = columnMin.Y + (survivorRows[survivorIndex] + 0.5f) * cellHeight;
            }
            else
            {
                startY = targetY - missing * cellHeight;
            }

            var y = Easing.Lerp(startY, targetY, progress);
            MachineSymbols.Draw(drawList, info.Id, after[reel * info.Rows + row], new Vector2(centerX, y), extent, 1f,
                false);
        }
    }

    private static void DrawExpand(ImDrawListPtr drawList, Rect window, MachineRoundPlayback playback,
        SlotsMachineInfo info, float cellWidth, float extent, float scale)
    {
        var current = playback.Current;
        var grid = current.Grid;
        if (grid is null || current.Expander < 0)
        {
            return;
        }

        var grow = Easing.EaseOutCubic(playback.BeatProgress);
        for (var reel = 0; reel < info.Reels; reel++)
        {
            var filled = true;
            for (var row = 0; row < info.Rows; row++)
            {
                filled &= grid[reel * info.Rows + row] == current.Expander;
            }

            if (!filled)
            {
                continue;
            }

            var min = new Vector2(window.Min.X + reel * cellWidth, window.Min.Y);
            var max = new Vector2(min.X + cellWidth, window.Max.Y);
            var center = (min + max) * 0.5f;
            var half = (max.Y - min.Y) * 0.5f * grow;
            Squircle.FillVerticalGradient(drawList, new Vector2(min.X + 2f * scale, center.Y - half),
                new Vector2(max.X - 2f * scale, center.Y + half), 10f * scale,
                ImGui.GetColorU32(Gold with { W = 0.55f }), ImGui.GetColorU32(new Vector4(0.6f, 0.3f, 0.05f, 0.75f)));
            MachineSymbols.Draw(drawList, info.Id, current.Expander, center, extent * (1f + 0.4f * grow), 1f, false);
        }
    }

    private void DrawTraces(ImDrawListPtr drawList, Rect window, CasinoSlotsStepDto step, SlotsMachineInfo info,
        float cellWidth, float cellHeight, float scale)
    {
        if (traceCount == 0 || step.Wins is not { } wins || info.Layout == SlotsLayout.Cluster)
        {
            return;
        }

        var active = traceOrder[(int)(traceSeconds / TraceCycleSeconds) % traceCount];
        var win = wins[active];
        if (win.Line < 0 || win.Line >= SlotsRules.PaylineCount)
        {
            return;
        }

        var rows = SlotsRules.Paylines[win.Line];
        var count = Math.Clamp(win.Count, 2, SlotsRules.ReelCount);
        Span<Vector2> points = stackalloc Vector2[SlotsRules.ReelCount];
        for (var reel = 0; reel < count; reel++)
        {
            points[reel] = new Vector2(window.Min.X + (reel + 0.5f) * cellWidth,
                window.Min.Y + (rows[reel] + 0.5f) * cellHeight);
        }

        var glow = ImGui.GetColorU32(Gold with { W = 0.25f });
        var core = ImGui.GetColorU32(Gold);
        for (var segment = 0; segment < count - 1; segment++)
        {
            drawList.AddLine(points[segment], points[segment + 1], glow, 9f * scale);
            drawList.AddLine(points[segment], points[segment + 1], core, 3f * scale);
        }

        for (var reel = 0; reel < count; reel++)
        {
            drawList.AddCircleFilled(points[reel], 4f * scale, core, 12);
        }
    }

    private static void DrawCoinLabel(ImDrawListPtr drawList, CasinoSlotsStepDto? step, int cell, Vector2 center,
        float extent, float alpha, float phase)
    {
        if (step?.Coins is not { } coins)
        {
            return;
        }

        for (var index = 0; index < coins.Length; index++)
        {
            var coin = coins[index];
            if (coin.Cell != cell)
            {
                continue;
            }

            if (coin.Kind == SlotsRules.CoinOrb)
            {
                MachineSymbols.DrawOrb(drawList, center, extent, alpha, 0.5f + 0.5f * MathF.Sin(phase * 4f));
                DrawLabel(drawList, CasinoMultiples.Label((int)Math.Min(int.MaxValue / 100, coin.Multiple) * 100),
                    center, extent * 2f, alpha);
                return;
            }

            MachineSymbols.DrawCoin(drawList, center, extent, alpha, 0.6f);
            DrawLabel(drawList, CoinLabel(coin), center + new Vector2(0f, extent * 0.45f), extent * 2f, alpha);
            return;
        }
    }

    public static string CoinLabel(CasinoSlotsCoinDto coin) => coin.Kind switch
    {
        SlotsRules.CoinMini => Loc.T(L.Machines.Mini),
        SlotsRules.CoinMinor => Loc.T(L.Machines.Minor),
        SlotsRules.CoinMajor => Loc.T(L.Machines.Major),
        _ => NumberText.Compact(coin.Value),
    };

    private static void DrawLabel(ImDrawListPtr drawList, string text, Vector2 center, float width, float alpha)
    {
        var style = TextStyles.FootnoteEmphasized;
        var plate = new Vector2(4f, 1f) * UiScale.Current;
        var fitted = Typography.FitText(text, MathF.Max(1f, width - plate.X * 2f), style);
        var size = Typography.Measure(fitted, style);
        var min = center - size * 0.5f - plate;
        var max = center + size * 0.5f + plate;
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(0.05f, 0.03f, 0.08f, 0.82f * alpha)));
        Typography.DrawCentered(drawList, center, fitted, StageInks.Strong with { W = alpha }, style);
    }

    private static void DrawHold(ImDrawListPtr drawList, Rect window, MachineRoundPlayback playback,
        SlotsMachineInfo info, float extent, float phase, float scale)
    {
        var step = playback.Current;
        var cellWidth = window.Width / info.Reels;
        var cellHeight = window.Height / info.Rows;
        var respinning = playback.Beat == MachineBeat.Respin;
        var spinEnd = MachineTiming.Scaled(MachineTiming.Respin, playback.Turbo);
        var collected = playback.CollectedCoins();
        for (var cell = 0; cell < info.CellCount; cell++)
        {
            var reel = cell / info.Rows;
            var row = cell % info.Rows;
            var min = new Vector2(window.Min.X + reel * cellWidth, window.Min.Y + row * cellHeight);
            var max = min + new Vector2(cellWidth, cellHeight);
            var inset = 4f * scale;
            Squircle.Fill(drawList, min + new Vector2(inset, inset), max - new Vector2(inset, inset), 10f * scale,
                ImGui.GetColorU32(SlotInk));
            var center = (min + max) * 0.5f;
            var coinIndex = IndexOfCoin(step, cell);
            if (coinIndex < 0)
            {
                if (respinning && playback.BeatSeconds < spinEnd)
                {
                    var symbol = MachineSymbols.BlurSymbol(info.Id,
                        (int)(playback.BeatSeconds * RowsPerSecond) + cell * 3);
                    MachineSymbols.Draw(drawList, info.Id, symbol, center, extent * 0.8f, 0.3f, false);
                }

                continue;
            }

            var coin = step.Coins![coinIndex];
            var fresh = respinning && playback.IsNewCoin(cell);
            if (fresh && playback.BeatSeconds < spinEnd)
            {
                continue;
            }

            var pop = fresh
                ? GameJuice.PopIn(Math.Clamp((playback.BeatSeconds - spinEnd) /
                    MathF.Max(0.01f, MachineTiming.Scaled(MachineTiming.RespinLanded, playback.Turbo)), 0f, 1f))
                : 1f;
            var tallied = playback.Beat == MachineBeat.Collect && coinIndex < collected;
            var glow = 0.6f + 0.4f * MathF.Sin(phase * 5f + cell);
            if (tallied)
            {
                glow = 1.6f;
            }

            MachineSymbols.DrawCoin(drawList, center, extent * pop, 1f, glow);
            DrawLabel(drawList, CoinLabel(coin), center + new Vector2(0f, extent * 0.45f), cellWidth * 0.9f, 1f);
        }
    }

    private static int IndexOfCoin(CasinoSlotsStepDto step, int cell)
    {
        if (step.Coins is not { } coins)
        {
            return -1;
        }

        for (var index = 0; index < coins.Length; index++)
        {
            if (coins[index].Cell == cell)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasAny(bool[] mask, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (mask[index])
            {
                return true;
            }
        }

        return false;
    }
}
