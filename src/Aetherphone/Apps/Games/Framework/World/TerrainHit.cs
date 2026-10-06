namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct TerrainHit
{
    public readonly Vector2 Point;
    public readonly Vector2 Normal;
    public readonly float Distance;
    public readonly int Column;
    public readonly int Row;

    public TerrainHit(Vector2 point, Vector2 normal, float distance, int column, int row)
    {
        Point = point;
        Normal = normal;
        Distance = distance;
        Column = column;
        Row = row;
    }
}
