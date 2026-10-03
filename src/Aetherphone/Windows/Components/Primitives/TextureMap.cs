namespace Aetherphone.Windows.Components;

internal readonly record struct TextureMap(Vector2 Anchor, Vector2 Origin, Vector2 PerPixelX, Vector2 PerPixelY)
{
    public Vector2 At(Vector2 position)
    {
        var offset = position - Anchor;
        return Origin + PerPixelX * offset.X + PerPixelY * offset.Y;
    }

    public static TextureMap Tiled(Vector2 anchor, Vector2 tileSize, Vector2 scroll, float shear = 0f)
    {
        var perX = new Vector2(1f / MathF.Max(1f, tileSize.X), 0f);
        var perY = new Vector2(-shear / MathF.Max(1f, tileSize.X), 1f / MathF.Max(1f, tileSize.Y));
        return new TextureMap(anchor, scroll, perX, perY);
    }
}
