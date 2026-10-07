namespace Aetherphone.Apps.Games.Framework.Cards;

internal static class PileLayout
{
    public const int MaxLayers = 6;
    public const int CardsPerLayer = 5;
    public const float LayerStep = 1.5f;
    private const float ScatterAngle = 0.18f;
    private const float ScatterOffset = 0.07f;
    private const uint HashMultiplier = 2654435761u;
    private const float ByteRange = 255f;

    public static int Layers(int count) => count <= 0 ? 0 : Math.Min(MaxLayers, 1 + (count - 1) / CardsPerLayer);

    public static Vector2 LayerOffset(int layer, float scale) => new(0f, -layer * LayerStep * scale);

    public static CardPose Top(Vector2 center, float width, int count, float scale, bool faceUp = false)
    {
        var layers = Layers(count);
        return new CardPose(center + LayerOffset(Math.Max(0, layers - 1), scale), width, 0f, faceUp);
    }

    public static CardPose Scatter(Vector2 center, float width, int sequence)
    {
        var hash = unchecked((uint)sequence * HashMultiplier);
        var angle = (Unit(hash) * 2f - 1f) * ScatterAngle;
        var offset = new Vector2(Unit(hash >> 8) * 2f - 1f, Unit(hash >> 16) * 2f - 1f) * (width * ScatterOffset);
        return new CardPose(center + offset, width, angle);
    }

    private static float Unit(uint bits) => (bits & 0xFFu) / ByteRange;
}
