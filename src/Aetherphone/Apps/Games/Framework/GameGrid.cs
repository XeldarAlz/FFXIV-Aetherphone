using System.Numerics;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework;

internal readonly struct GameGrid
{
    public readonly int Columns;

    public readonly int Rows;

    public readonly float Pitch;

    public readonly float Gap;

    public readonly Vector2 Origin;

    private GameGrid(int columns, int rows, float pitch, float gap, Vector2 origin)
    {
        Columns = columns;
        Rows = rows;
        Pitch = pitch;
        Gap = gap;
        Origin = origin;
    }

    public float Width => Columns * Pitch;

    public float Height => Rows * Pitch;

    public Vector2 Center => Origin + new Vector2(Width, Height) * 0.5f;

    public Rect Bounds => new(Origin, Origin + new Vector2(Width, Height));

    public static GameGrid Centered(Rect area, int columns, int rows, float gapFraction, float verticalBias = 0f)
    {
        var pitchFromWidth = area.Width / columns;
        var pitchFromHeight = area.Height / rows;
        var pitch = MathF.Min(pitchFromWidth, pitchFromHeight);
        var gap = pitch * gapFraction;

        var width = columns * pitch;
        var height = rows * pitch;
        var origin = new Vector2(
            area.Center.X - width * 0.5f,
            area.Center.Y - height * 0.5f + verticalBias);

        return new GameGrid(columns, rows, pitch, gap, origin);
    }

    public Rect Cell(int column, int row)
    {
        var halfGap = Gap * 0.5f;
        var min = new Vector2(Origin.X + column * Pitch + halfGap, Origin.Y + row * Pitch + halfGap);
        var max = new Vector2(min.X + Pitch - Gap, min.Y + Pitch - Gap);
        return new Rect(min, max);
    }

    public Vector2 CellCenter(int column, int row)
    {
        return new Vector2(
            Origin.X + column * Pitch + Pitch * 0.5f,
            Origin.Y + row * Pitch + Pitch * 0.5f);
    }
}
