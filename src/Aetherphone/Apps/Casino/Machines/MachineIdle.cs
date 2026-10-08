using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed class MachineIdle : ICabinetIdle
{
    private readonly string machineId;
    private float seconds;

    public MachineIdle(string machineId)
    {
        this.machineId = machineId;
    }

    public Backdrop IdleBackdrop => Backdrop.Strip;

    public void DrawIdle(ImDrawListPtr drawList, Rect rect, float deltaSeconds)
    {
        var scale = UiScale.Current;
        seconds += deltaSeconds;
        var info = SlotsMachines.For(machineId);
        MachineArt.Chassis(drawList, rect, machineId, seconds, 0.8f, false, scale);
        var window = rect.Inset(10f * scale);
        MachineArt.ReelWindow(drawList, window, machineId, scale);
        var cellWidth = window.Width / info.Reels;
        var cellHeight = window.Height / info.Rows;
        var extent = MathF.Min(cellWidth, cellHeight) * MachineReels.SymbolShare;
        for (var cell = 0; cell < info.CellCount; cell++)
        {
            var reel = cell / info.Rows;
            var row = cell % info.Rows;
            var drift = MathF.Sin(seconds * 0.8f + reel) * cellHeight * 0.04f;
            var center = new Vector2(window.Min.X + (reel + 0.5f) * cellWidth,
                window.Min.Y + (row + 0.5f) * cellHeight + drift);
            var symbol = MachineSymbols.BlurSymbol(machineId, cell * 5 + (int)(seconds * 0.25f) + reel);
            MachineSymbols.Draw(drawList, machineId, symbol, center, extent, 1f,
                MachineSymbols.ShimmerFrame(cell, seconds));
        }
    }
}
