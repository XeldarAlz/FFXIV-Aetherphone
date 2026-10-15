using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct HostPillPlacement(Rect Pill, bool Collapsed, Vector2 CapsuleCenter, float CapsuleWidth);

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
    public const float HostGap = 8f;
    public const float CapsuleMinimum = 120f;

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
    public readonly Rect HostPill;
    public readonly bool HostCollapsed;

    private CasinoStageLayout(Rect full, Rect band, Vector2 backCenter, Vector2 infoCenter, Vector2 capsuleCenter,
        float capsuleMaxWidth, float chipRadius, float touchRadius, Rect ribbon, Rect practice, Rect deck, Rect safe,
        Rect hostPill = default, bool hostCollapsed = false)
    {
        HostPill = hostPill;
        HostCollapsed = hostCollapsed;
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

    public bool Landscape => Full.Width > Full.Height;

    public bool HasHost => HostPill.Width > 0f;

    public CasinoStageLayout WithHost(float pillWidth, float scale)
    {
        if (Landscape)
        {
            return this;
        }

        var placement = PlaceHost(BackCenter, InfoCenter, ChipRadiusPixels, pillWidth, scale);
        return new CasinoStageLayout(Full, Band, BackCenter, InfoCenter, placement.CapsuleCenter, placement.CapsuleWidth,
            ChipRadiusPixels, TouchRadiusPixels, Ribbon, Practice, Deck, Safe, placement.Pill, placement.Collapsed);
    }

    public static HostPillPlacement PlaceHost(Vector2 backCenter, Vector2 infoCenter, float chipRadius,
        float pillWidth, float scale)
    {
        var gap = HostGap * scale;
        var right = infoCenter.X - chipRadius - gap;
        var laneLeft = backCenter.X + chipRadius + gap;
        var diameter = chipRadius * 2f;
        var collapsed = right - MathF.Max(pillWidth, diameter) - gap - laneLeft < CapsuleMinimum * scale;
        var width = collapsed ? diameter : MathF.Max(pillWidth, diameter);
        var pill = new Rect(new Vector2(right - width, infoCenter.Y - chipRadius),
            new Vector2(right, infoCenter.Y + chipRadius));
        var laneRight = pill.Min.X - gap;
        return new HostPillPlacement(pill, collapsed, new Vector2((laneLeft + laneRight) * 0.5f, infoCenter.Y),
            MathF.Max(0f, laneRight - laneLeft));
    }

    public static CasinoStageLayout Compute(Rect full, bool ribbon, bool practice, float deckHeight, float scale) =>
        Compute(full, full, ribbon, practice, deckHeight, scale);

    public static CasinoStageLayout Compute(Rect full, Rect content, bool ribbon, bool practice, float deckHeight,
        float scale)
    {
        var bandBottom = content.Min.Y + ChromeBand * scale;
        var band = new Rect(full.Min, new Vector2(full.Max.X, MathF.Min(full.Max.Y, bandBottom)));
        var backCenter = new Vector2(content.Min.X + ChipInsetX * scale, content.Min.Y + ChipCenterY * scale);
        var infoCenter = new Vector2(content.Max.X - ChipInsetX * scale, content.Min.Y + ChipCenterY * scale);
        var capsuleCenter = new Vector2(content.Center.X, content.Min.Y + ChipCenterY * scale);
        var capsuleMaxWidth = MathF.Max(0f, content.Width - CapsuleReserve * 2f * scale);
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

        var bottom = MathF.Max(top, content.Max.Y);
        var deckTop = MathF.Max(top, bottom - MathF.Max(0f, deckHeight) * scale);
        var deck = new Rect(new Vector2(full.Min.X, deckTop), new Vector2(full.Max.X, bottom));
        var inset = SafeInset * scale;
        var safeMin = new Vector2(full.Min.X + inset, top + inset);
        var safeMax = new Vector2(full.Max.X - inset, deckTop - inset);
        var safe = new Rect(safeMin, new Vector2(MathF.Max(safeMin.X, safeMax.X), MathF.Max(safeMin.Y, safeMax.Y)));
        return new CasinoStageLayout(full, band, backCenter, infoCenter, capsuleCenter, capsuleMaxWidth,
            ChipRadius * scale, MathF.Max(ChipRadius, TouchTarget * 0.5f) * scale, ribbonRect, practiceRect, deck,
            safe);
    }

    public Rect DeckPanel => new(new Vector2(Full.Min.X, Deck.Min.Y), Full.Max);

    public bool ChromeContains(Vector2 point)
    {
        var radiusSquared = TouchRadiusPixels * TouchRadiusPixels;
        if (Vector2.DistanceSquared(point, BackCenter) <= radiusSquared)
        {
            return true;
        }

        if (Landscape)
        {
            return false;
        }

        if (Vector2.DistanceSquared(point, InfoCenter) <= radiusSquared)
        {
            return true;
        }

        if (HasHost && point.X >= HostPill.Min.X && point.X <= HostPill.Max.X && point.Y >= HostPill.Min.Y
            && point.Y <= HostPill.Max.Y)
        {
            return true;
        }

        return point.Y >= Band.Min.Y && point.Y <= Band.Max.Y
            && MathF.Abs(point.X - CapsuleCenter.X) <= CapsuleMaxWidth * 0.5f;
    }

    public Rect Notice(float width, float height, float scale)
    {
        var clearance = (BetsRail.HandleHeight * 0.5f + DeckActions.Gap) * scale;
        var bottom = HasDeck ? Deck.Min.Y - clearance : Safe.Max.Y;
        var fitted = MathF.Min(width, Safe.Width);
        var center = Safe.Center.X;
        return new Rect(new Vector2(center - fitted * 0.5f, bottom - height),
            new Vector2(center + fitted * 0.5f, bottom));
    }

    public Rect Body => new(new Vector2(Full.Min.X, Practice.Max.Y), Full.Max);
}
