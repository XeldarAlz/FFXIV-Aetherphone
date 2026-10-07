using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Broadside;

internal sealed class BroadsideLayout
{
    public const float LabelHeight = 20f;
    private const float Gap = 10f;
    private const float ButtonHeight = 46f;
    private const float ButtonMaxWidth = 160f;
    private const float HintHeight = 18f;
    private const float BattleBigShare = 0.62f;
    private const float MiniShare = 0.46f;
    private const float DockGapCells = 0.35f;
    private static readonly int[] DockRows = { 0, 0, 1, 1, 1 };
    private static readonly int[] DockColumns = { 0, 5, 0, 3, 6 };

    private readonly Rect[] dock = new Rect[BroadsideFleet.ShipCount];

    public Rect Area { get; private set; }

    public Rect PlaceGrid { get; private set; }

    public Rect Dock { get; private set; }

    public Rect Hint { get; private set; }

    public Vector2 AutoCenter { get; private set; }

    public Vector2 ReadyCenter { get; private set; }

    public Vector2 ButtonSize { get; private set; }

    public Rect Big { get; private set; }

    public Rect Mini { get; private set; }

    public Rect Panel { get; private set; }

    public Rect DockSlot(int ship) => dock[ship];

    public float DockPitch { get; private set; }

    public static float Pitch(Rect grid) => grid.Width / BroadsideFleet.Size;

    public static Vector2 CellCenter(Rect grid, int cell)
    {
        var pitch = Pitch(grid);
        return grid.Min + new Vector2((BroadsideFleet.ColumnOf(cell) + 0.5f) * pitch,
            (BroadsideFleet.RowOf(cell) + 0.5f) * pitch);
    }

    public static Rect CellRect(Rect grid, int column, int row)
    {
        var pitch = Pitch(grid);
        var min = grid.Min + new Vector2(column * pitch, row * pitch);
        return new Rect(min, min + new Vector2(pitch, pitch));
    }

    public static Rect ShipRect(Rect grid, int column, int row, int length, bool across)
    {
        var pitch = Pitch(grid);
        var min = grid.Min + new Vector2(column * pitch, row * pitch);
        var size = across ? new Vector2(length * pitch, pitch) : new Vector2(pitch, length * pitch);
        return new Rect(min, min + size);
    }

    public static bool CellAt(Rect grid, Vector2 point, out int column, out int row)
    {
        var pitch = Pitch(grid);
        column = (int)MathF.Floor((point.X - grid.Min.X) / pitch);
        row = (int)MathF.Floor((point.Y - grid.Min.Y) / pitch);
        return BroadsideFleet.InBounds(column, row);
    }

    public static Rect Lerp(Rect from, Rect to, float amount) =>
        new(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));

    public void Build(Rect area, float scale)
    {
        Area = area;
        var gap = Gap * scale;
        var label = LabelHeight * scale;
        var buttonHeight = ButtonHeight * scale;
        var hint = HintHeight * scale;
        var below = buttonHeight + hint + gap * 3f;
        var placeSide = MathF.Min(area.Width, (area.Height - label - below) / (1f + 0.2f + DockGapCells * 0.1f));
        placeSide = MathF.Max(placeSide, 40f * scale);
        var placeMin = new Vector2(area.Center.X - placeSide * 0.5f, area.Min.Y + label);
        PlaceGrid = new Rect(placeMin, placeMin + new Vector2(placeSide, placeSide));
        var pitch = placeSide / BroadsideFleet.Size;
        DockPitch = pitch;
        var dockTop = PlaceGrid.Max.Y + gap;
        var dockGap = pitch * DockGapCells;
        Dock = new Rect(new Vector2(PlaceGrid.Min.X, dockTop),
            new Vector2(PlaceGrid.Max.X, dockTop + pitch * 2f + dockGap));
        for (var ship = 0; ship < BroadsideFleet.ShipCount; ship++)
        {
            var row = DockRows[ship];
            var columnOffset = DockColumns[ship];
            var gapsBefore = ship == 1 || ship == 3 ? 1 : ship == 4 ? 2 : 0;
            var rowWidth = row == 0 ? 9f * pitch + dockGap * 0.5f : 8f * pitch + dockGap;
            var x = PlaceGrid.Min.X + (placeSide - rowWidth) * 0.5f + columnOffset * pitch + gapsBefore * dockGap * 0.5f;
            var y = dockTop + row * (pitch + dockGap);
            var length = BroadsideFleet.Length(ship);
            dock[ship] = new Rect(new Vector2(x, y), new Vector2(x + length * pitch, y + pitch));
        }

        Hint = new Rect(new Vector2(area.Min.X, Dock.Max.Y + gap * 0.5f), new Vector2(area.Max.X, Dock.Max.Y + gap * 0.5f + hint));
        var buttonWidth = MathF.Min((area.Width - gap) * 0.5f, ButtonMaxWidth * scale);
        ButtonSize = new Vector2(buttonWidth, buttonHeight);
        var buttonY = MathF.Min(area.Max.Y - buttonHeight * 0.5f, Hint.Max.Y + gap + buttonHeight * 0.5f);
        AutoCenter = new Vector2(area.Center.X - (buttonWidth + gap) * 0.5f, buttonY);
        ReadyCenter = new Vector2(area.Center.X + (buttonWidth + gap) * 0.5f, buttonY);

        var bigSide = MathF.Max(40f * scale, MathF.Min(area.Width, (area.Height - label * 2f - gap) * BattleBigShare));
        var bigMin = new Vector2(area.Center.X - bigSide * 0.5f, area.Min.Y + label);
        Big = new Rect(bigMin, bigMin + new Vector2(bigSide, bigSide));
        var lowerTop = Big.Max.Y + gap + label;
        var miniSide = MathF.Max(20f * scale, MathF.Min(bigSide * MiniShare, area.Max.Y - lowerTop));
        var miniMin = new Vector2(Big.Min.X, lowerTop);
        Mini = new Rect(miniMin, miniMin + new Vector2(miniSide, miniSide));
        Panel = new Rect(new Vector2(Mini.Max.X + gap * 1.5f, Big.Max.Y + gap), new Vector2(Big.Max.X, area.Max.Y));
    }
}
