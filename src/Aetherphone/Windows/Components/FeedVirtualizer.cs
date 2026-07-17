using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal sealed class FeedVirtualizer
{
    private readonly Dictionary<string, (float Height, int Revision)> rows = new();
    private readonly float cullMargin;
    private float rowWidth;
    private int fontGeneration;
    private float windowTop;
    private float windowBottom;
    private float rowStartY;

    public FeedVirtualizer(float cullMargin)
    {
        this.cullMargin = cullMargin;
    }

    public void BeginFrame()
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (rowWidth != width || fontGeneration != Plugin.Fonts.Generation)
        {
            rows.Clear();
            rowWidth = width;
            fontGeneration = Plugin.Fonts.Generation;
        }

        windowTop = ImGui.GetWindowPos().Y;
        windowBottom = windowTop + ImGui.GetWindowSize().Y;
    }

    public bool Skip(string rowId, int revision = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        rowStartY = cursor.Y;
        var margin = cullMargin * ImGuiHelpers.GlobalScale;
        if (rows.TryGetValue(rowId, out var row) && row.Revision == revision
            && (cursor.Y > windowBottom + margin || cursor.Y + row.Height < windowTop - margin))
        {
            ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + row.Height));
            return true;
        }

        return false;
    }

    public void Record(string rowId, int revision = 0)
    {
        rows[rowId] = (ImGui.GetCursorScreenPos().Y - rowStartY, revision);
    }
}
