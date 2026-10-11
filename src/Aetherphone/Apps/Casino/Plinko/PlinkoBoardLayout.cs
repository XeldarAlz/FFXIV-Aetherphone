using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Plinko;

internal readonly struct PlinkoBoardLayout
{
    public const float RowRatio = 0.86f;
    public const float PegRadiusPitch = 0.12f;
    public const float BallRadiusPitch = 0.22f;
    public const float StartRow = -1.1f;
    public const float SlotRow = 0.95f;
    public const float SlotHeightPitch = 0.86f;
    public const float SlotWidthPitch = 0.92f;
    public const float TopReserveRows = 1.4f;
    public const float SideMarginPitch = 0.3f;

    public readonly int Rows;
    public readonly float Pitch;
    public readonly float RowGap;
    public readonly Vector2 Origin;
    public readonly Rect Bounds;
    public readonly float Legend;

    private PlinkoBoardLayout(int rows, float pitch, Vector2 origin, Rect bounds, float legend)
    {
        Rows = rows;
        Pitch = pitch;
        RowGap = pitch * RowRatio;
        Origin = origin;
        Bounds = bounds;
        Legend = legend;
    }

    public static float ContactLift => (PegRadiusPitch + BallRadiusPitch) / RowRatio;

    public float PegRadius => Pitch * PegRadiusPitch;

    public float BallRadius => Pitch * BallRadiusPitch;

    public float SlotWidth => Pitch * SlotWidthPitch;

    public float SlotHeight => Pitch * SlotHeightPitch;

    public bool HasLegend => Legend > 0f;

    public static int PegsInRow(int row) => row + 3;

    public static int PegCount(int rows) => rows <= 0 ? 0 : rows * (rows + 5) / 2;

    public static int PegIndex(int row, int column) => row * (row + 5) / 2 + column;

    public static float HeightInRows(int rows) =>
        TopReserveRows + (rows - 1) + SlotRow + SlotHeightPitch / RowRatio * 0.5f;

    public static float WidthInPitches(int rows) => rows + 1 + SideMarginPitch * 2f;

    public static PlinkoBoardLayout Compute(Rect area, int rows, float legend = 0f)
    {
        var reserve = MathF.Max(0f, legend);
        var height = area.Height - reserve;
        if (rows <= 0 || area.Width <= 0f || height <= 0f)
        {
            return new PlinkoBoardLayout(Math.Max(rows, 0), 0f, area.Center, new Rect(area.Center, area.Center),
                reserve);
        }

        var widthPitch = area.Width / WidthInPitches(rows);
        var heightPitch = height / (HeightInRows(rows) * RowRatio);
        var pitch = MathF.Min(widthPitch, heightPitch);
        var rowGap = pitch * RowRatio;
        var boardHeight = HeightInRows(rows) * rowGap + reserve;
        var width = WidthInPitches(rows) * pitch;
        var top = area.Center.Y - boardHeight * 0.5f;
        var origin = new Vector2(area.Center.X, top + TopReserveRows * rowGap);
        var bounds = new Rect(new Vector2(area.Center.X - width * 0.5f, top),
            new Vector2(area.Center.X + width * 0.5f, top + boardHeight));
        return new PlinkoBoardLayout(rows, pitch, origin, bounds, reserve);
    }

    public Vector2 ToScreen(Vector2 unit) => new(Origin.X + unit.X * Pitch, Origin.Y + unit.Y * RowGap);

    public Vector2 PegCenter(int row, int column) =>
        ToScreen(new Vector2(column - (row + 2) * 0.5f, row));

    public Vector2 SlotCenter(int slot) => ToScreen(SlotUnit(Rows, slot));

    public Rect SlotRect(int slot)
    {
        var center = SlotCenter(slot);
        var half = new Vector2(SlotWidth * 0.5f, SlotHeight * 0.5f);
        return new Rect(center - half, center + half);
    }

    public float SlotsBottom => SlotCenter(0).Y + SlotHeight * 0.5f;

    public static Vector2 SlotUnit(int rows, int slot) => new(slot - rows * 0.5f, rows - 1 + SlotRow);

    public static Vector2 ContactUnit(int row, int rights) => new(rights - row * 0.5f, row - ContactLift);

    public static Vector2 StartUnit => new(0f, StartRow);
}
