using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct GetChipsBlocks(
    float Row,
    float Need,
    float TileCaption,
    float TileAmount,
    float TileCoins,
    float AutoTitle,
    float AutoHint,
    float Note);

internal readonly struct GetChipsLayout
{
    public const float Gap = 8f;
    public const float SectionGap = 14f;
    public const float TilePad = 8f;
    public const float ControlHeight = Button.LargeHeight;
    public const float FieldShare = 0.5f;
    public const float NotePad = 12f;

    public readonly Rect WalletRow;
    public readonly Rect ChipsRow;
    public readonly Rect Need;
    public readonly Rect TileRow;
    public readonly int TileCount;
    public readonly float TileGap;
    public readonly Rect Field;
    public readonly Rect Buy;
    public readonly Rect AutoText;
    public readonly Rect AutoToggle;
    public readonly Rect AutoRow;
    public readonly Rect Note;
    public readonly float Bottom;

    private GetChipsLayout(Rect walletRow, Rect chipsRow, Rect need, Rect tileRow, int tileCount, float tileGap,
        Rect field, Rect buy, Rect autoText, Rect autoToggle, Rect autoRow, Rect note, float bottom)
    {
        WalletRow = walletRow;
        ChipsRow = chipsRow;
        Need = need;
        TileRow = tileRow;
        TileCount = tileCount;
        TileGap = tileGap;
        Field = field;
        Buy = buy;
        AutoText = autoText;
        AutoToggle = autoToggle;
        AutoRow = autoRow;
        Note = note;
        Bottom = bottom;
    }

    public bool HasNeed => Need.Height > 0f;

    public bool HasTiles => TileCount > 0;

    public bool HasNote => Note.Height > 0f;

    public Rect Tile(int index)
    {
        var count = Math.Max(1, TileCount);
        var width = MathF.Max(0f, (TileRow.Width - TileGap * (count - 1)) / count);
        var left = TileRow.Min.X + index * (width + TileGap);
        return new Rect(new Vector2(left, TileRow.Min.Y), new Vector2(left + width, TileRow.Max.Y));
    }

    public static float TileHeight(in GetChipsBlocks blocks, float scale) =>
        MathF.Max(ControlHeight * scale, TilePad * 2f * scale + blocks.TileCaption + blocks.TileAmount + blocks.TileCoins);

    public static float AutoTextWidth(float width, float scale) =>
        MathF.Max(1f, width - Metrics.Size.ToggleWidth * scale - Gap * 2f * scale);

    public static GetChipsLayout Compute(float left, float top, float width, in GetChipsBlocks blocks, int tileCount,
        float scale)
    {
        var right = left + width;
        var gap = Gap * scale;
        var sectionGap = SectionGap * scale;
        var walletRow = new Rect(new Vector2(left, top), new Vector2(right, top + blocks.Row));
        var chipsRow = new Rect(new Vector2(left, walletRow.Max.Y + gap), new Vector2(right, walletRow.Max.Y + gap
            + blocks.Row));
        var y = chipsRow.Max.Y;
        var need = new Rect(new Vector2(left, y), new Vector2(right, y));
        if (blocks.Need > 0f)
        {
            need = new Rect(new Vector2(left, y + gap), new Vector2(right, y + gap + blocks.Need));
            y = need.Max.Y;
        }

        var note = new Rect(new Vector2(left, y), new Vector2(left, y));
        if (blocks.Note > 0f)
        {
            note = new Rect(new Vector2(left, y + gap), new Vector2(right, y + gap + blocks.Note + NotePad * 2f * scale));
            y = note.Max.Y;
        }

        var tiles = Math.Max(0, tileCount);
        var tileRow = new Rect(new Vector2(left, y), new Vector2(right, y));
        if (tiles > 0)
        {
            var tileTop = y + sectionGap;
            tileRow = new Rect(new Vector2(left, tileTop), new Vector2(right, tileTop + TileHeight(blocks, scale)));
            y = tileRow.Max.Y;
        }

        var control = ControlHeight * scale;
        var rowTop = y + sectionGap;
        var fieldRight = left + MathF.Floor(width * FieldShare);
        var field = new Rect(new Vector2(left, rowTop), new Vector2(fieldRight, rowTop + control));
        var buy = new Rect(new Vector2(fieldRight + gap, rowTop), new Vector2(right, rowTop + control));
        y = field.Max.Y;

        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var autoTop = y + sectionGap;
        var textHeight = blocks.AutoTitle + blocks.AutoHint;
        var autoHeight = MathF.Max(MathF.Max(toggleHeight, textHeight), control);
        var autoRow = new Rect(new Vector2(left, autoTop), new Vector2(right, autoTop + autoHeight));
        var autoText = new Rect(new Vector2(left, autoTop + (autoHeight - textHeight) * 0.5f),
            new Vector2(left + AutoTextWidth(width, scale), autoTop + (autoHeight + textHeight) * 0.5f));
        var toggleTop = autoTop + (autoHeight - toggleHeight) * 0.5f;
        var autoToggle = new Rect(new Vector2(right - toggleWidth, toggleTop),
            new Vector2(right, toggleTop + toggleHeight));
        y = autoRow.Max.Y;

        return new GetChipsLayout(walletRow, chipsRow, need, tileRow, tiles, gap, field, buy, autoText, autoToggle,
            autoRow, note, y);
    }
}
