using Aetherphone.Core.Social;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class FeedVirtualizer
{
    public const int DefaultRowCap = 400;

    private const float ParkedAtTop = 1f;

    private readonly Dictionary<string, (float Height, int Revision)> rows = new();
    private readonly float cullMargin;
    private readonly int rowCap;
    private float rowWidth;
    private int fontGeneration;
    private float windowTop;
    private float windowBottom;
    private float rowStartY;

    public FeedVirtualizer(float cullMargin, int rowCap = DefaultRowCap)
    {
        this.cullMargin = cullMargin;
        this.rowCap = rowCap;
    }

    public void BeginFrame(ITrimmable? source = null)
    {
        var width = ImGui.GetContentRegionAvail().X;
        if (rowWidth != width || fontGeneration != Plugin.Fonts.Generation)
        {
            rows.Clear();
            rowWidth = width;
            fontGeneration = Plugin.Fonts.Generation;
        }

        if (source is not null && ImGui.GetScrollY() < ParkedAtTop)
        {
            source.Trim(rowCap);
        }

        windowTop = ImGui.GetWindowPos().Y;
        windowBottom = windowTop + ImGui.GetWindowSize().Y;
    }

    public bool Skip(string rowId, int revision = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        rowStartY = cursor.Y;
        var margin = cullMargin * UiScale.Current;
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
