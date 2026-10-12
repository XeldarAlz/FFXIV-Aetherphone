using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Venue;

internal readonly record struct VenueTableCardLayout(
    Rect Card,
    Rect Art,
    float TextLeft,
    float TextWidth,
    float NameTop,
    float SeatsTop,
    float CurrencyTop,
    float CellsLeft,
    float CodeTop,
    float CellWidth,
    float CellHeight,
    float CellGap,
    Rect Door,
    Rect Ledger)
{
    public Rect Cell(int index)
    {
        var x = CellsLeft + index * (CellWidth + CellGap);
        return new Rect(new Vector2(x, CodeTop), new Vector2(x + CellWidth, CodeTop + CellHeight));
    }
}

internal readonly record struct VenueHostCardLayout(
    Rect Card,
    Vector2 Title,
    float TextWidth,
    Vector2 Summary,
    Rect Primary,
    Rect Secondary);

internal static class VenueContextLayout
{
    public const float Pad = 14f;
    public const float Art = 52f;
    public const float TextGap = 12f;
    public const float LineGap = 3f;
    public const float Gap = 10f;
    public const float CellMax = 34f;
    public const float CellGapUnits = 6f;
    public const float CellAspect = 1.15f;
    public const float ButtonGap = 10f;
    public const float Touch = 44f;

    public static float Inner(float width, float scale) => MathF.Max(1f, width - Pad * 2f * scale);

    public static float CellWidth(float width, float scale)
    {
        var count = CasinoRoomCodes.Length;
        var fitted = (Inner(width, scale) - CellGapUnits * scale * (count - 1)) / count;
        return MathF.Max(1f, MathF.Min(CellMax * scale, fitted));
    }

    public static float TextWidth(float width, float scale) =>
        MathF.Max(1f, width - (Pad * 2f + Art + TextGap) * scale);

    public static float TextBlock(float nameHeight, float seatsHeight, float currencyHeight, float scale) =>
        nameHeight + seatsHeight + currencyHeight + LineGap * 2f * scale;

    public static VenueTableCardLayout Table(Vector2 origin, float width, float scale, float nameHeight,
        float seatsHeight, float currencyHeight, bool single)
    {
        var pad = Pad * scale;
        var art = Art * scale;
        var artRect = new Rect(origin + new Vector2(pad, pad), origin + new Vector2(pad + art, pad + art));
        var textLeft = artRect.Max.X + TextGap * scale;
        var textWidth = TextWidth(width, scale);
        var textBlock = TextBlock(nameHeight, seatsHeight, currencyHeight, scale);
        var band = MathF.Max(art, textBlock);
        var nameTop = origin.Y + pad + (band - textBlock) * 0.5f;
        var seatsTop = nameTop + nameHeight + LineGap * scale;
        var currencyTop = seatsTop + seatsHeight + LineGap * scale;
        var cellWidth = CellWidth(width, scale);
        var cellHeight = cellWidth * CellAspect;
        var codeTop = origin.Y + pad + band + Gap * scale;
        var buttonsTop = codeTop + cellHeight + Gap * scale;
        var touch = Touch * scale;
        var buttonWidth = single ? Inner(width, scale) : (Inner(width, scale) - ButtonGap * scale) * 0.5f;
        var door = new Rect(new Vector2(origin.X + pad, buttonsTop),
            new Vector2(origin.X + pad + buttonWidth, buttonsTop + touch));
        var ledger = single
            ? default
            : new Rect(new Vector2(origin.X + width - pad - buttonWidth, buttonsTop),
                new Vector2(origin.X + width - pad, buttonsTop + touch));
        var card = new Rect(origin, new Vector2(origin.X + width, buttonsTop + touch + pad));
        return new VenueTableCardLayout(card, artRect, textLeft, textWidth, nameTop, seatsTop, currencyTop,
            origin.X + pad, codeTop, cellWidth, cellHeight, CellGapUnits * scale, door, ledger);
    }

    public static VenueHostCardLayout Host(Vector2 origin, float width, float scale, float titleHeight,
        float summaryBlock, int buttons)
    {
        var pad = Pad * scale;
        var inner = Inner(width, scale);
        var title = new Vector2(origin.X + pad, origin.Y + pad);
        var summary = new Vector2(title.X, title.Y + titleHeight + LineGap * scale);
        var buttonsTop = summary.Y + summaryBlock + Gap * scale;
        var touch = Touch * scale;
        var count = Math.Max(1, buttons);
        var buttonWidth = (inner - ButtonGap * scale * (count - 1)) / count;
        var primary = new Rect(new Vector2(origin.X + pad, buttonsTop),
            new Vector2(origin.X + pad + buttonWidth, buttonsTop + touch));
        var secondary = count > 1
            ? new Rect(new Vector2(primary.Max.X + ButtonGap * scale, buttonsTop),
                new Vector2(origin.X + width - pad, buttonsTop + touch))
            : default;
        var card = new Rect(origin, new Vector2(origin.X + width, buttonsTop + touch + pad));
        return new VenueHostCardLayout(card, title, inner, summary, primary, secondary);
    }
}
