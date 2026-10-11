using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Tables;

internal readonly record struct RoomCodeLayout(
    Rect Card,
    Vector2 Label,
    Rect Copy,
    float CellsLeft,
    float CellsTop,
    float CellWidth,
    float CellHeight,
    float CellGap,
    Vector2 Hint,
    float HintWidth)
{
    public const float Pad = 16f;
    public const float Gap = 12f;
    public const float CellMax = 50f;
    public const float CellGapUnits = 8f;
    public const float CellAspect = 1.25f;
    public const float Touch = 44f;

    public Rect Cell(int index)
    {
        var x = CellsLeft + index * (CellWidth + CellGap);
        return new Rect(new Vector2(x, CellsTop), new Vector2(x + CellWidth, CellsTop + CellHeight));
    }

    public static float HintWidthFor(float width, float scale) => MathF.Max(1f, width - Pad * 2f * scale);

    public static RoomCodeLayout Compute(Vector2 origin, float width, float scale, float labelHeight,
        float hintHeight, float copyWidth)
    {
        var pad = Pad * scale;
        var inner = width - pad * 2f;
        var touch = Touch * scale;
        var labelRow = MathF.Max(labelHeight, touch);
        var copy = new Rect(new Vector2(origin.X + width - pad - copyWidth, origin.Y + pad),
            new Vector2(origin.X + width - pad, origin.Y + pad + touch));
        var label = new Vector2(origin.X + pad, origin.Y + pad + (labelRow - labelHeight) * 0.5f);
        var cellGap = CellGapUnits * scale;
        var count = CasinoRoomCodes.Length;
        var cellWidth = MathF.Min(CellMax * scale, (inner - cellGap * (count - 1)) / count);
        var cellHeight = cellWidth * CellAspect;
        var cellsTop = origin.Y + pad + labelRow + Gap * scale;
        var hintTop = cellsTop + cellHeight + Gap * scale;
        var bottom = hintTop + hintHeight + pad;
        return new RoomCodeLayout(new Rect(origin, new Vector2(origin.X + width, bottom)), label, copy,
            origin.X + pad, cellsTop, cellWidth, cellHeight, cellGap, new Vector2(origin.X + pad, hintTop), inner);
    }
}
