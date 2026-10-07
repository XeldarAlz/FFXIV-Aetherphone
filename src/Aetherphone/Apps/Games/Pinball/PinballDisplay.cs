using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pinball;

internal enum DisplayMood : byte
{
    Score,
    Award,
    Big,
    Danger,
}

internal sealed class PinballDisplay
{
    public const int Rows = 11;
    private const int MaxColumns = 192;
    private const int GlyphRows = 7;
    private const int GlyphTop = 1;
    private const int DigitWidth = 5;
    private const int CommaWidth = 2;
    private const int Gap = 1;
    private const int MaxDigits = 10;
    private const int StatusRow = 9;
    private const int MeterRow = 0;
    private const float RollPerSecond = 9f;
    private const int SparkleCount = 26;
    private const float AwardSeconds = 1.3f;
    private const float BigSeconds = 2f;
    private const float DangerSeconds = 1.4f;
    private const float LitRadius = 0.4f;
    private const float HaloRadius = 0.85f;
    private const float UnlitHalf = 0.16f;
    private const float PanelAlpha = 0.94f;
    private const float PaddingFraction = 0.1f;
    private static readonly Vector4 Panel = new(0.025f, 0.02f, 0.035f, 1f);
    private static readonly Vector4 Danger = new(1f, 0.32f, 0.28f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly byte[] DigitRows =
    {
        0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110,
        0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110,
        0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111,
        0b11111, 0b00010, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110,
        0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010,
        0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110,
        0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110,
        0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000,
        0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110,
        0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100,
    };

    private static readonly byte[] CommaRows = { 0, 0, 0, 0, 0, 0b01, 0b10 };

    private readonly bool[] lit = new bool[MaxColumns * Rows];
    private readonly int[] digits = new int[MaxDigits];
    private GameRandom sparkle = GameRandom.FromSeed(0x444D44UL);
    private DisplayMood mood;
    private int awardValue;
    private float moodSeconds;
    private float moodTotal = 1f;
    private int columns;
    private int shownScore;

    public DisplayMood Mood => moodSeconds > 0f ? mood : DisplayMood.Score;

    public void Reset()
    {
        moodSeconds = 0f;
        awardValue = 0;
        shownScore = 0;
        mood = DisplayMood.Score;
    }

    public void Award(int value, bool big)
    {
        if (moodSeconds > 0f && mood == DisplayMood.Big && !big)
        {
            return;
        }

        awardValue = value;
        mood = big ? DisplayMood.Big : DisplayMood.Award;
        moodTotal = big ? BigSeconds : AwardSeconds;
        moodSeconds = moodTotal;
    }

    public void Alert()
    {
        mood = DisplayMood.Danger;
        moodTotal = DangerSeconds;
        moodSeconds = DangerSeconds;
    }

    public void Update(float deltaSeconds, int score)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        moodSeconds = MathF.Max(0f, moodSeconds - deltaSeconds);
        if (score <= shownScore)
        {
            shownScore = score;
            return;
        }

        var gap = score - shownScore;
        shownScore += Math.Max(1, (int)(gap * MathF.Min(1f, deltaSeconds * RollPerSecond)));
    }

    public void Draw(ImDrawListPtr drawList, Rect rect, Vector4 color, float scale, PinballBoard board, float time)
    {
        var pitch = Frame(drawList, rect, color, scale, out var origin);
        Array.Clear(lit);
        Compose(board, time);
        var tone = board.FeverActive ? PinballRenderer.FeverInk(time, 0.3f) : color;
        var ink = Mood == DisplayMood.Danger && ((int)(time * 8f) & 1) == 0 ? Danger : tone;
        Paint(drawList, origin, pitch, ink);
        DrawTiltMeter(drawList, rect, color, scale, board);
    }

    private static void DrawTiltMeter(ImDrawListPtr drawList, Rect rect, Vector4 color, float scale,
        PinballBoard board)
    {
        if (board.TiltMeter <= 0.01f && board.TiltWarnings == 0)
        {
            return;
        }

        var inset = rect.Height * 0.3f;
        var y = rect.Max.Y - rect.Height * PaddingFraction * 0.45f;
        var thickness = MathF.Max(1f, 1.5f * scale);
        var pip = 2f * scale;
        var left = rect.Min.X + inset;
        var right = rect.Max.X - inset - (PinballBoard.MaxTiltWarnings * 3f + 1f) * pip;
        var fill = Math.Clamp(board.TiltMeter, 0f, 1f);
        drawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(color with { W = 0.12f }),
            thickness);
        drawList.AddLine(new Vector2(left, y), new Vector2(left + (right - left) * fill, y),
            ImGui.GetColorU32(Vector4.Lerp(color, Danger, fill)), thickness);
        for (var warning = 0; warning < PinballBoard.MaxTiltWarnings; warning++)
        {
            var center = new Vector2(right + (warning * 3f + 2f) * pip, y);
            var used = warning < board.TiltWarnings;
            drawList.AddCircleFilled(center, pip, ImGui.GetColorU32(used ? Danger : color with { W = 0.2f }), 8);
        }
    }

    public void DrawAttract(ImDrawListPtr drawList, Rect rect, Vector4 color, float scale, int score, float time)
    {
        var pitch = Frame(drawList, rect, color, scale, out var origin);
        Array.Clear(lit);
        StampNumber(score, GlyphTop);
        ChaseBorder(time);
        Paint(drawList, origin, pitch, color);
    }

    private float Frame(ImDrawListPtr drawList, Rect rect, Vector4 color, float scale, out Vector2 origin)
    {
        var radius = MathF.Min(10f * scale, rect.Height * 0.3f);
        Elevation.Card(drawList, rect.Min, rect.Max, radius, scale, 0.8f);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Panel with { W = PanelAlpha }));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(color with { W = 0.35f }),
            MathF.Max(1f, 1.2f * scale));
        var paddingY = rect.Height * PaddingFraction;
        var paddingX = paddingY + radius * 0.5f;
        var pitch = (rect.Height - paddingY * 2f) / Rows;
        columns = Math.Clamp((int)((rect.Width - paddingX * 2f) / pitch), 1, MaxColumns);
        origin = new Vector2(rect.Center.X - columns * pitch * 0.5f, rect.Min.Y + paddingY);
        return pitch;
    }

    private void Compose(PinballBoard board, float time)
    {
        var current = Mood;
        var value = current is DisplayMood.Award or DisplayMood.Big ? awardValue : shownScore;
        var progress = 1f - moodSeconds / moodTotal;
        var blinkOff = current == DisplayMood.Big && progress < 0.35f && ((int)(time * 10f) & 1) == 0;
        if (!blinkOff)
        {
            StampNumber(value, GlyphTop);
        }

        if (current == DisplayMood.Big)
        {
            ChaseBorder(time);
            Sparkle(progress);
            return;
        }

        if (current == DisplayMood.Danger)
        {
            ChaseBorder(time);
            return;
        }

        StampStatus(board, time);
        StampMeter(board, time);
    }

    private void StampMeter(PinballBoard board, float time)
    {
        var fraction = board.FeverActive ? board.FeverLeft / PinballBoard.FeverSeconds : board.FeverCharge;
        var lit = (int)(columns * Math.Clamp(fraction, 0f, 1f));
        var march = (int)(time * 30f);
        for (var column = 0; column < lit; column++)
        {
            if (board.FeverActive && ((column + march) & 3) == 0)
            {
                continue;
            }

            Light(column, MeterRow);
        }
    }

    private void StampStatus(PinballBoard board, float time)
    {
        for (var ball = 0; ball < PinballBoard.BallsPerGame; ball++)
        {
            var on = ball + 1 <= board.BallNumber;
            var current = ball + 1 == board.BallNumber;
            if (!on || (current && board.BallWaiting && ((int)(time * 4f) & 1) == 0))
            {
                continue;
            }

            Block(1 + ball * 3, StatusRow);
        }

        var center = columns / 2;
        for (var lockIndex = 0; lockIndex < PinballBoard.LocksForMultiball; lockIndex++)
        {
            if (lockIndex < board.Locks)
            {
                Block(center - 4 + lockIndex * 3, StatusRow);
            }
        }

        for (var step = 1; step < board.Multiplier; step++)
        {
            Block(columns - 3 - (step - 1) * 3, StatusRow);
        }
    }

    private void StampNumber(int value, int top)
    {
        var count = 0;
        var remaining = Math.Max(0, value);
        do
        {
            digits[count++] = remaining % 10;
            remaining /= 10;
        }
        while (remaining > 0 && count < MaxDigits);

        var commas = (count - 1) / 3;
        var width = count * (DigitWidth + Gap) + commas * (CommaWidth + Gap) - Gap;
        var column = (columns - width) / 2;
        for (var index = count - 1; index >= 0; index--)
        {
            StampGlyph(DigitRows, digits[index] * GlyphRows, DigitWidth, column, top);
            column += DigitWidth + Gap;
            if (index > 0 && index % 3 == 0)
            {
                StampGlyph(CommaRows, 0, CommaWidth, column, top);
                column += CommaWidth + Gap;
            }
        }
    }

    private void StampGlyph(byte[] glyphs, int offset, int width, int left, int top)
    {
        for (var row = 0; row < GlyphRows; row++)
        {
            var bits = glyphs[offset + row];
            for (var column = 0; column < width; column++)
            {
                if ((bits & (1 << (width - 1 - column))) != 0)
                {
                    Light(left + column, top + row);
                }
            }
        }
    }

    private void ChaseBorder(float time)
    {
        var perimeter = 2 * (columns + Rows) - 4;
        var head = (int)(time * 60f);
        for (var step = 0; step < perimeter; step++)
        {
            if (((step + head) & 3) != 0)
            {
                continue;
            }

            if (step < columns)
            {
                Light(step, 0);
                continue;
            }

            var rest = step - columns;
            if (rest < Rows - 1)
            {
                Light(columns - 1, rest + 1);
                continue;
            }

            rest -= Rows - 1;
            if (rest < columns - 1)
            {
                Light(columns - 2 - rest, Rows - 1);
                continue;
            }

            rest -= columns - 1;
            Light(0, Rows - 2 - rest);
        }
    }

    private void Sparkle(float progress)
    {
        var count = (int)(SparkleCount * (1f - progress));
        for (var index = 0; index < count; index++)
        {
            Light(sparkle.Next(columns), sparkle.Next(Rows));
        }
    }

    private void Block(int column, int row)
    {
        Light(column, row);
        Light(column + 1, row);
        Light(column, row + 1);
        Light(column + 1, row + 1);
    }

    private void Light(int column, int row)
    {
        if (column < 0 || column >= columns || row < 0 || row >= Rows)
        {
            return;
        }

        lit[row * MaxColumns + column] = true;
    }

    private void Paint(ImDrawListPtr drawList, Vector2 origin, float pitch, Vector4 color)
    {
        var unlit = ImGui.GetColorU32(color with { W = 0.08f });
        var halo = ImGui.GetColorU32(color with { W = 0.16f });
        var core = ImGui.GetColorU32(GamePalette.Lighten(color, 0.35f));
        var hot = ImGui.GetColorU32(White with { W = 0.55f });
        var unlitHalf = new Vector2(pitch * UnlitHalf);
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var center = origin + new Vector2((column + 0.5f) * pitch, (row + 0.5f) * pitch);
                if (!lit[row * MaxColumns + column])
                {
                    drawList.AddRectFilled(center - unlitHalf, center + unlitHalf, unlit);
                    continue;
                }

                drawList.AddCircleFilled(center, pitch * HaloRadius, halo, 8);
                drawList.AddCircleFilled(center, pitch * LitRadius, core, 8);
                drawList.AddCircleFilled(center, pitch * LitRadius * 0.4f, hot, 6);
            }
        }
    }
}
