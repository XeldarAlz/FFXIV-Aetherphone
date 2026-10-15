namespace Aetherphone.Apps.Casino.Strip;

internal static class StripGrid
{
    public static float CellWidth(float rowWidth, int columns, float gap) =>
        columns <= 0 ? 0f : MathF.Max(0f, (rowWidth - gap * (columns - 1)) / columns);

    public static int Rows(int count, int columns) =>
        count <= 0 || columns <= 0 ? 0 : (count + columns - 1) / columns;

    public static float Height(int count, int columns, float cellHeight, float gap)
    {
        var rows = Rows(count, columns);
        return rows == 0 ? 0f : rows * cellHeight + (rows - 1) * gap;
    }

    public static Vector2 CellOrigin(Vector2 origin, int index, int columns, float cellWidth, float cellHeight,
        float gap) =>
        new(origin.X + index % columns * (cellWidth + gap), origin.Y + index / columns * (cellHeight + gap));
}
