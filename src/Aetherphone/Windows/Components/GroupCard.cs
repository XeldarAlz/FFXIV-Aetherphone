using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal struct GroupCard
{
    public const float DefaultRowHeight = Metrics.Size.Row;
    private readonly PhoneTheme theme;
    private readonly float scale;
    private readonly float rowHeight;
    private readonly float left;
    private readonly float right;
    private readonly float startY;
    private readonly int rowCount;
    private readonly bool showSeparators;
    private readonly float[] callTops;
    private readonly float[] callBottoms;
    private readonly bool[] callHovered;
    private int rowIndex;
    private int callCount;
    private float lastRowTop;
    private float lastRowBottom;
    private int lastRowIndex;
    private int lastRowSpan;
    private int lastCallIndex;

    private GroupCard(PhoneTheme theme, float scale, float rowHeight, float left, float right, float startY,
        int rowCount, bool showSeparators)
    {
        this.theme = theme;
        this.scale = scale;
        this.rowHeight = rowHeight;
        this.left = left;
        this.right = right;
        this.startY = startY;
        this.rowCount = rowCount;
        this.showSeparators = showSeparators;
        callTops = new float[rowCount];
        callBottoms = new float[rowCount];
        callHovered = new bool[rowCount];
        rowIndex = 0;
        callCount = 0;
        lastRowTop = startY;
        lastRowBottom = startY;
        lastRowIndex = 0;
        lastRowSpan = 0;
        lastCallIndex = 0;
    }

    public static GroupCard Begin(PhoneTheme theme, int rowCount, float rowHeight = DefaultRowHeight,
        bool showSeparators = true, Vector4? glowAccent = null, Vector4? fillOverride = null)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        origin.Y = MathF.Round(origin.Y);
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = MathF.Round(rowCount * rowHeight * scale);
        var cardMax = new Vector2(right, origin.Y + height);
        var dl = ImGui.GetWindowDrawList();
        Squircle.Fill(dl, origin, cardMax, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(fillOverride ?? theme.GroupedCard));
        if (glowAccent is not null)
        {
            Material.TopGlow(dl, origin, cardMax, Metrics.Radius.Md * scale, glowAccent.Value, 0.55f, 0.10f);
        }

        Material.EdgeSquircle(dl, origin, cardMax, Metrics.Radius.Md * scale, scale);
        return new GroupCard(theme, scale, rowHeight, origin.X, right, origin.Y, rowCount, showSeparators);
    }

    public Rect NextRow(int rowSpan = 1)
    {
        var rowTop = MathF.Round(startY + rowIndex * rowHeight * scale);
        var rowBottom = MathF.Round(rowTop + rowSpan * rowHeight * scale);

        callTops[callCount] = rowTop;
        callBottoms[callCount] = rowBottom;
        lastCallIndex = callCount;
        callCount++;

        lastRowIndex = rowIndex;
        lastRowSpan = rowSpan;
        lastRowTop = rowTop;
        lastRowBottom = rowBottom;
        rowIndex += rowSpan;
        var padding = Metrics.Space.Lg * scale;
        return new Rect(new Vector2(left + padding, rowTop), new Vector2(right - padding, rowBottom));
    }

    public void DrawHoverHighlight(Vector4 color)
    {
        callHovered[lastCallIndex] = true;

        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        var radius = Metrics.Radius.Md * scale;
        var cardMin = new Vector2(left, startY);
        var cardMax = new Vector2(right, startY + rowCount * rowHeight * scale);
        dl.PushClipRect(new Vector2(left, lastRowTop), new Vector2(right, lastRowBottom), true);
        Squircle.Fill(dl, cardMin, cardMax, radius, packed);
        dl.PopClipRect();
    }

    public void End()
    {
        if (showSeparators)
        {
            var dl = ImGui.GetWindowDrawList();
            var separatorX = left + Metrics.Space.Lg * scale;
            var separatorColor = ImGui.GetColorU32(theme.Separator);
            for (var i = 0; i < callCount - 1; i++)
            {
                if (callHovered[i] || callHovered[i + 1])
                {
                    continue;
                }

                var boundaryY = callBottoms[i];
                dl.AddLine(new Vector2(separatorX, boundaryY), new Vector2(right, boundaryY), separatorColor,
                    Metrics.Stroke.Hairline);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, rowCount * rowHeight * scale));
    }
}
