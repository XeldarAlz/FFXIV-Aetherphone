using System.Text;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SoftWrapBufferTests
{
    private const float Width = 12f;

    private sealed class Field
    {
        private readonly SoftWrapBuffer buffer = new(WrapMonospace);
        private readonly float width;
        private string logical = string.Empty;
        private string display = string.Empty;
        private int cursor;

        public Field(float wrapWidth) => width = wrapWidth;

        public string Logical => logical;

        public string Display => display;

        public int Cursor => cursor;

        public void Type(string text)
        {
            display = display.Insert(cursor, text);
            cursor += text.Length;
            Apply();
        }

        public void Backspace()
        {
            if (cursor == 0)
            {
                return;
            }

            display = display.Remove(cursor - 1, 1);
            cursor--;
            Apply();
        }

        public void MoveTo(int logicalIndex)
        {
            cursor = buffer.DisplayIndexOf(logicalIndex);
        }

        private void Apply()
        {
            logical = buffer.Merge(logical, display, cursor, out var logicalCursor);
            buffer.Rewrap(logical, width);
            display = buffer.Display;
            cursor = buffer.DisplayIndexOf(logicalCursor);
        }
    }

    private static string WrapMonospace(string text, float wrapWidth)
    {
        var limit = (int)wrapWidth;
        if (text.Length == 0 || limit <= 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        var lineWidth = 0;
        var lineStart = 0;
        var wordStart = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var isSpace = text[index] == ' ';
            if (!isSpace && lineWidth > 0 && lineWidth + 1 > limit)
            {
                if (wordStart > lineStart)
                {
                    builder.Insert(wordStart, '\n');
                    lineStart = wordStart + 1;
                    lineWidth = builder.Length - lineStart;
                    wordStart = lineStart;
                }
                else
                {
                    builder.Append('\n');
                    lineStart = builder.Length;
                    lineWidth = 0;
                    wordStart = builder.Length;
                }
            }

            builder.Append(text[index]);
            lineWidth++;
            if (isSpace)
            {
                wordStart = builder.Length;
            }
        }

        return builder.ToString();
    }

    private static Field Typed(string text)
    {
        var field = new Field(Width);
        for (var index = 0; index < text.Length; index++)
        {
            field.Type(text[index].ToString());
            Assert.Equal(text[..(index + 1)], field.Logical);
        }

        return field;
    }

    [Fact]
    public void TypingPastTheLineEndWrapsWithoutTouchingTheMessage()
    {
        var field = Typed("hello world superb");

        Assert.Equal("hello world superb", field.Logical);
        Assert.Equal("hello world \nsuperb", field.Display);
    }

    [Fact]
    public void BackspacingThroughAWrappedWordNeverForcesABreak()
    {
        var field = Typed("hello world superb");

        for (var remaining = 17; remaining >= 0; remaining--)
        {
            field.Backspace();
            Assert.Equal("hello world superb"[..remaining], field.Logical);
            Assert.DoesNotContain('\n', field.Logical);
        }
    }

    [Fact]
    public void RetypingOnAClearedWrapLineNeverForcesABreak()
    {
        var field = Typed("hello world superb");
        for (var count = 0; count < 6; count++)
        {
            field.Backspace();
        }

        Assert.Equal("hello world ", field.Logical);
        Assert.Equal("hello world ", field.Display);

        field.Type("marvellous");

        Assert.Equal("hello world marvellous", field.Logical);
        Assert.Equal("hello world \nmarvellous", field.Display);
    }

    [Fact]
    public void CharacterBreaksInsideOneLongWordStaySoft()
    {
        var field = Typed("abcdefghijklmnopqrstuvwxyz");

        Assert.Equal("abcdefghijkl\nmnopqrstuvwx\nyz", field.Display);

        field.Backspace();
        field.Backspace();

        Assert.Equal("abcdefghijklmnopqrstuvwx", field.Logical);
    }

    [Fact]
    public void ATypedBreakSurvivesWrappingAndEditing()
    {
        var field = Typed("hi");
        field.Type("\n");
        field.Type("there is a long tail");

        Assert.Equal("hi\nthere is a long tail", field.Logical);
        Assert.Equal("hi\nthere is a \nlong tail", field.Display);

        field.Backspace();

        Assert.Equal("hi\nthere is a long tai", field.Logical);
    }

    [Fact]
    public void InsertingAtAWrapBoundaryLandsInTheLogicalText()
    {
        var field = Typed("hello world superb");
        field.MoveTo(12);
        field.Type("X");

        Assert.Equal("hello world Xsuperb", field.Logical);
        Assert.Equal("hello world \nXsuperb", field.Display);
        Assert.Equal(field.Display.IndexOf('X') + 1, field.Cursor);
    }

    [Fact]
    public void BackspacingAWrappedWordBackOntoOneLineKeepsOneMessage()
    {
        var field = Typed("aaa bbbbbbbbbbbb");

        Assert.Equal("aaa \nbbbbbbbbbbbb", field.Display);

        for (var count = 0; count < 8; count++)
        {
            field.Backspace();
        }

        Assert.Equal("aaa bbbb", field.Logical);
        Assert.Equal("aaa bbbb", field.Display);

        field.Type(" cc");

        Assert.Equal("aaa bbbb cc", field.Logical);
    }

    [Fact]
    public void EditingLongRunsOfWordsNeverGrowsABreak()
    {
        var field = Typed("aaa bbb ccc ddd eee fff");
        for (var count = 0; count < 10; count++)
        {
            field.Backspace();
        }

        Assert.Equal("aaa bbb ccc d", field.Logical);
        Assert.DoesNotContain('\n', field.Logical);
    }

    [Fact]
    public void DeletingASoftBreakLeavesTheMessageUnchanged()
    {
        var field = Typed("hello world superb");
        field.MoveTo(12);
        field.Backspace();

        Assert.Equal("hello world superb", field.Logical);
        Assert.Equal("hello world \nsuperb", field.Display);
    }
}
