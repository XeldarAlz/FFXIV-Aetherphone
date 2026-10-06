namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct TerrainPlateau
{
    public readonly Vector2 Center;
    public readonly float HalfWidth;

    public TerrainPlateau(Vector2 center, float halfWidth)
    {
        Center = center;
        HalfWidth = halfWidth;
    }

    public float Left => Center.X - HalfWidth;

    public float Right => Center.X + HalfWidth;

    public bool Spans(float x) => x >= Left && x <= Right;
}
