namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct CellRegion
{
    public readonly int MinColumn;
    public readonly int MinRow;
    public readonly int MaxColumn;
    public readonly int MaxRow;

    public CellRegion(int minColumn, int minRow, int maxColumn, int maxRow)
    {
        MinColumn = minColumn;
        MinRow = minRow;
        MaxColumn = maxColumn;
        MaxRow = maxRow;
    }

    public int Columns => MaxColumn - MinColumn + 1;

    public int Rows => MaxRow - MinRow + 1;
}
