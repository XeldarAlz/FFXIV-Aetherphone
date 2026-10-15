using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal struct GroupCard
{
    public const float DefaultRowHeight = Metrics.Size.Row;
    private const float CardStrokeAlpha = 0.06f;
    private readonly Vector4 separator;
    private readonly float scale;
    private readonly float rowHeight;
    private readonly float left;
    private readonly float right;
    private readonly float startY;
    private readonly float totalHeight;
    private float rowOffset;
    public float SeparatorInset;

    private GroupCard(Vector4 separator, float scale, float rowHeight, float left, float right, float startY,
        float totalHeight)
    {
        this.separator = separator;
        this.scale = scale;
        this.rowHeight = rowHeight;
        this.left = left;
        this.right = right;
        this.startY = startY;
        this.totalHeight = totalHeight;
        rowOffset = 0f;
        SeparatorInset = 0f;
    }

    public static GroupCard Begin(PhoneTheme theme, int rowCount, float rowHeight = DefaultRowHeight) =>
        Begin(theme, rowCount * rowHeight, rowHeight);

    public static GroupCard Begin(AppSkin ui, int rowCount, float rowHeight = DefaultRowHeight) =>
        Begin(ui, rowCount * rowHeight, rowHeight);

    public static GroupCard Begin(PhoneTheme theme, float totalHeight) => Begin(theme, totalHeight, 0f);

    public static GroupCard Begin(AppSkin ui, float totalHeight) => Begin(ui, totalHeight, 0f);

    private static GroupCard Begin(PhoneTheme theme, float totalHeight, float rowHeight)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = totalHeight * scale;
        DrawSurface(ImGui.GetWindowDrawList(), origin, new Vector2(right, origin.Y + height), theme, scale);
        return new GroupCard(theme.Separator, scale, rowHeight, origin.X, right, origin.Y, totalHeight);
    }

    public static void DrawSurface(ImDrawListPtr drawList, Vector2 min, Vector2 max, PhoneTheme theme, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(theme.GroupedCard));
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, CardStrokeAlpha)), Metrics.Stroke.Hairline);
    }

    private static GroupCard Begin(AppSkin ui, float totalHeight, float rowHeight)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var right = origin.X + ImGui.GetContentRegionAvail().X;
        var height = totalHeight * scale;
        var cardMax = new Vector2(right, origin.Y + height);
        ui.Card(ImGui.GetWindowDrawList(), origin, cardMax, Metrics.Radius.Grouped * scale);
        return new GroupCard(ui.Hairline, scale, rowHeight, origin.X, right, origin.Y, totalHeight);
    }

    public Rect Bounds => new(new Vector2(left, startY), new Vector2(right, startY + totalHeight * scale));

    public Rect NextRow(int rowSpan = 1) => NextRow(rowSpan * rowHeight);

    public Rect NextRow(float height)
    {
        var rowTop = startY + rowOffset * scale;
        if (rowOffset > 0f)
        {
            var separatorX = left + (Metrics.Space.Lg + SeparatorInset) * scale;
            ImGui.GetWindowDrawList().AddLine(new Vector2(separatorX, rowTop), new Vector2(right, rowTop),
                ImGui.GetColorU32(separator), Metrics.Stroke.Hairline);
        }

        rowOffset += height;
        var padding = Metrics.Space.Lg * scale;
        return new Rect(new Vector2(left + padding, rowTop),
            new Vector2(right - padding, rowTop + height * scale));
    }

    public void End()
    {
        ImGui.SetCursorScreenPos(new Vector2(left, startY));
        ImGui.Dummy(new Vector2(right - left, totalHeight * scale));
    }
}
