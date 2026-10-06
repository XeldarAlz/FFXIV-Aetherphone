namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct TerrainMaterial
{
    public static readonly TerrainMaterial Earth = new(new Vector4(0.56f, 0.40f, 0.27f, 1f),
        new Vector4(0.21f, 0.14f, 0.10f, 1f), new Vector4(0.52f, 0.80f, 0.36f, 1f));

    public static readonly TerrainMaterial Sand = new(new Vector4(0.83f, 0.70f, 0.48f, 1f),
        new Vector4(0.42f, 0.31f, 0.21f, 1f), new Vector4(0.96f, 0.88f, 0.64f, 1f));

    public static readonly TerrainMaterial Stone = new(new Vector4(0.43f, 0.41f, 0.48f, 1f),
        new Vector4(0.14f, 0.13f, 0.17f, 1f), new Vector4(0.72f, 0.70f, 0.82f, 1f));

    public static readonly TerrainMaterial Lunar = new(new Vector4(0.62f, 0.62f, 0.66f, 1f),
        new Vector4(0.23f, 0.23f, 0.28f, 1f), new Vector4(0.91f, 0.92f, 0.96f, 1f));

    public readonly Vector4 Surface;
    public readonly Vector4 Deep;
    public readonly Vector4 Edge;

    public TerrainMaterial(Vector4 surface, Vector4 deep, Vector4 edge)
    {
        Surface = surface;
        Deep = deep;
        Edge = edge;
    }
}
