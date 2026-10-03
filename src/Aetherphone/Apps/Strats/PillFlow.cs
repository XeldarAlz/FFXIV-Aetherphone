namespace Aetherphone.Apps.Strats;

internal struct PillFlow
{
    private readonly float left;
    private readonly float available;
    private readonly float gap;
    private float cursorX;
    private int placed;

    public PillFlow(float left, float available, float gap)
    {
        this.left = left;
        this.available = available;
        this.gap = gap;
        cursorX = left;
        placed = 0;
        Row = 0;
    }

    public int Row { get; private set; }

    public readonly int Rows => placed == 0 ? 0 : Row + 1;

    public float Place(float width)
    {
        if (cursorX > left && cursorX + width > left + available)
        {
            Row++;
            cursorX = left;
        }

        var x = cursorX;
        cursorX += width + gap;
        placed++;
        return x;
    }

    public readonly float Height(float rowHeight, float rowGap) =>
        Rows == 0 ? 0f : Rows * rowHeight + (Rows - 1) * rowGap;
}
