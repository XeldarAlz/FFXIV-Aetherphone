using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct CasinoStageLayout
{
    public const float ChromeBand = 52f;
    public const float ChipRadius = 18f;
    public const float TouchTarget = 44f;
    public const float ChipInsetX = 28f;
    public const float ChipCenterY = 26f;
    public const float CapsuleHeight = 34f;
    public const float CapsuleReserve = 56f;
    public const float RibbonHeight = 40f;
    public const float PracticeRibbonHeight = 22f;
    public const float DeckHeight = 118f;
    public const float SafeInset = 12f;

    public readonly Rect Full;
    public readonly Rect Band;
    public readonly Vector2 BackCenter;
    public readonly Vector2 InfoCenter;
    public readonly Vector2 CapsuleCenter;
    public readonly float CapsuleMaxWidth;
    public readonly float ChipRadiusPixels;
    public readonly float TouchRadiusPixels;
    public readonly Rect Ribbon;
    public readonly Rect Practice;
    public readonly Rect Deck;
    public readonly Rect Safe;

    private CasinoStageLayout(Rect full, Rect band, Vector2 backCenter, Vector2 infoCenter, Vector2 capsuleCenter,
        float capsuleMaxWidth, float chipRadius, float touchRadius, Rect ribbon, Rect practice, Rect deck, Rect safe)
    {
        Full = full;
        Band = band;
        BackCenter = backCenter;
        InfoCenter = infoCenter;
        CapsuleCenter = capsuleCenter;
        CapsuleMaxWidth = capsuleMaxWidth;
        ChipRadiusPixels = chipRadius;
        TouchRadiusPixels = touchRadius;
        Ribbon = ribbon;
        Practice = practice;
        Deck = deck;
        Safe = safe;
    }

    public bool HasRibbon => Ribbon.Height > 0f;

    public bool HasDeck => Deck.Height > 0f;

    public bool HasPractice => Practice.Height > 0f;

    public static CasinoStageLayout Compute(Rect full, bool ribbon, bool practice, float deckHeight, float scale)
    {
        var bandBottom = full.Min.Y + ChromeBand * scale;
        var band = new Rect(full.Min, new Vector2(full.Max.X, MathF.Min(full.Max.Y, bandBottom)));
        var backCenter = new Vector2(full.Min.X + ChipInsetX * scale, full.Min.Y + ChipCenterY * scale);
        var infoCenter = new Vector2(full.Max.X - ChipInsetX * scale, full.Min.Y + ChipCenterY * scale);
        var capsuleCenter = new Vector2(full.Center.X, full.Min.Y + ChipCenterY * scale);
        var capsuleMaxWidth = MathF.Max(0f, full.Width - CapsuleReserve * 2f * scale);
        var top = band.Max.Y;
        var ribbonRect = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, top));
        if (ribbon)
        {
            ribbonRect = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, top + RibbonHeight * scale));
            top = ribbonRect.Max.Y;
        }

        var practiceRect = new Rect(new Vector2(full.Min.X, top), new Vector2(full.Max.X, top));
        if (practice)
        {
            practiceRect = new Rect(new Vector2(full.Min.X, top),
                new Vector2(full.Max.X, top + PracticeRibbonHeight * scale));
            top = practiceRect.Max.Y;
        }

        var deckTop = MathF.Max(top, full.Max.Y - MathF.Max(0f, deckHeight) * scale);
        var deck = new Rect(new Vector2(full.Min.X, deckTop), full.Max);
        var inset = SafeInset * scale;
        var safeMin = new Vector2(full.Min.X + inset, top + inset);
        var safeMax = new Vector2(full.Max.X - inset, deckTop - inset);
        var safe = new Rect(safeMin, new Vector2(MathF.Max(safeMin.X, safeMax.X), MathF.Max(safeMin.Y, safeMax.Y)));
        return new CasinoStageLayout(full, band, backCenter, infoCenter, capsuleCenter, capsuleMaxWidth,
            ChipRadius * scale, MathF.Max(ChipRadius, TouchTarget * 0.5f) * scale, ribbonRect, practiceRect, deck,
            safe);
    }

    public bool ChromeContains(Vector2 point)
    {
        var radiusSquared = TouchRadiusPixels * TouchRadiusPixels;
        if (Vector2.DistanceSquared(point, BackCenter) <= radiusSquared
            || Vector2.DistanceSquared(point, InfoCenter) <= radiusSquared)
        {
            return true;
        }

        return point.Y >= Band.Min.Y && point.Y <= Band.Max.Y
            && MathF.Abs(point.X - CapsuleCenter.X) <= CapsuleMaxWidth * 0.5f;
    }

    public Rect Body => new(new Vector2(Full.Min.X, Practice.Max.Y), Full.Max);
}
