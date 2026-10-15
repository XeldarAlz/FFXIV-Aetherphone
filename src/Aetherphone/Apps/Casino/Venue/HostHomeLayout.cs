using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Venue;

internal readonly record struct HostTableLayout(
    Rect Card,
    Vector2 TileCenter,
    float TextLeft,
    float TextTop,
    Vector2 PillMin,
    Vector2 CodeOrigin,
    float CodeTileWidth,
    float CodeTileHeight,
    Vector2 CrowdLeftCenter,
    bool CrowdInline,
    Rect Go,
    Rect Manage);

internal static class HostHomeLayout
{
    public const int Columns = 3;
    public const float GridGap = 8f;
    public const float Pad = 14f;
    public const float Tile = 50f;
    public const float TextGap = 12f;
    public const float RowGap = 12f;
    public const float PillGap = 8f;
    public const float CodeTileMax = 30f;
    public const float CodeAspect = 1.2f;
    public const float CodeGap = 6f;
    public const float CrowdGap = 12f;
    public const float ButtonGap = 8f;
    public const float Touch = 44f;

    public static float GridTileWidth(float width, float scale) =>
        MathF.Max(1f, (width - GridGap * scale * (Columns - 1)) / Columns);

    public static Rect GridCell(Vector2 origin, float tileWidth, float tileHeight, int index, float scale)
    {
        var column = index % Columns;
        var row = index / Columns;
        var x = origin.X + column * (tileWidth + GridGap * scale);
        var y = origin.Y + row * (tileHeight + GridGap * scale);
        return new Rect(new Vector2(x, y), new Vector2(x + tileWidth, y + tileHeight));
    }

    public static float GridHeight(int count, float tileHeight, float scale)
    {
        if (count <= 0)
        {
            return 0f;
        }

        var rows = (count + Columns - 1) / Columns;
        return rows * tileHeight + (rows - 1) * GridGap * scale;
    }

    public static float TableTextWidth(float width, float pillWidth, float scale) =>
        MathF.Max(1f, width - (Pad * 2f + Tile + TextGap + PillGap) * scale - pillWidth);

    public static float CodeTileWidth(float width, float scale)
    {
        var inner = width - Pad * 2f * scale;
        var count = CasinoRoomCodes.Length;
        return MathF.Max(1f, MathF.Min(CodeTileMax * scale, (inner - CodeGap * scale * (count - 1)) / count));
    }

    public static bool CrowdFits(float width, float crowdWidth, float scale)
    {
        var tile = CodeTileWidth(width, scale);
        var tiles = tile * CasinoRoomCodes.Length + CodeGap * scale * (CasinoRoomCodes.Length - 1);
        return tiles + CrowdGap * scale + crowdWidth <= width - Pad * 2f * scale;
    }

    public static HostTableLayout Table(Vector2 origin, float width, float scale, float textBlock, Vector2 pillSize,
        float crowdWidth, float crowdHeight)
    {
        var pad = Pad * scale;
        var tile = Tile * scale;
        var header = MathF.Max(tile, MathF.Max(textBlock, pillSize.Y));
        var tileCenter = new Vector2(origin.X + pad + tile * 0.5f, origin.Y + pad + header * 0.5f);
        var textLeft = origin.X + pad + tile + TextGap * scale;
        var textTop = origin.Y + pad + (header - textBlock) * 0.5f;
        var pillMin = new Vector2(origin.X + width - pad - pillSize.X, origin.Y + pad + (header - pillSize.Y) * 0.5f);
        var codeWidth = CodeTileWidth(width, scale);
        var codeHeight = codeWidth * CodeAspect;
        var codeTop = origin.Y + pad + header + RowGap * scale;
        var inline = CrowdFits(width, crowdWidth, scale);
        var codeRow = inline ? MathF.Max(codeHeight, crowdHeight) : codeHeight;
        var codeOrigin = new Vector2(origin.X + pad, codeTop + (codeRow - codeHeight) * 0.5f);
        var crowdCenter = inline
            ? new Vector2(origin.X + width - pad - crowdWidth, codeTop + codeRow * 0.5f)
            : new Vector2(origin.X + pad, codeTop + codeRow + RowGap * scale * 0.5f + crowdHeight * 0.5f);
        var buttonsTop = inline
            ? codeTop + codeRow + RowGap * scale
            : codeTop + codeRow + RowGap * scale * 0.5f + crowdHeight + RowGap * scale;
        var touch = Touch * scale;
        var half = (width - pad * 2f - ButtonGap * scale) * 0.5f;
        var go = new Rect(new Vector2(origin.X + pad, buttonsTop), new Vector2(origin.X + pad + half, buttonsTop + touch));
        var manage = new Rect(new Vector2(origin.X + width - pad - half, buttonsTop),
            new Vector2(origin.X + width - pad, buttonsTop + touch));
        var card = new Rect(origin, new Vector2(origin.X + width, buttonsTop + touch + pad));
        return new HostTableLayout(card, tileCenter, textLeft, textTop, pillMin, codeOrigin, codeWidth, codeHeight,
            crowdCenter, inline, go, manage);
    }
}
