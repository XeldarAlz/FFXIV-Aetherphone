namespace Aetherphone.Core.Shell.Spotlight;

internal readonly struct SpotlightLayout
{
    public const float FieldHeightUnits = 44f;
    public const float FieldRadiusUnits = 22f;
    public const float ListGapUnits = 12f;
    public const float PanelRadiusUnits = 22f;
    public const float PanelPadUnits = 8f;
    public const float RowHeightUnits = 56f;
    public const float RowTileUnits = 36f;
    public const float RowInsetUnits = 10f;
    public const float TextGapUnits = 12f;
    public const float SectionHeaderUnits = 24f;
    public const float HeaderTextOffsetUnits = 6f;
    public const float RecentTileUnits = 56f;
    public const float RecentLabelBandUnits = 22f;
    public const float RecentGutterUnits = 14f;
    public const float PillHeightUnits = 28f;
    public const float PillPadUnits = 12f;
    public const float PillGapUnits = 6f;
    public const int RecentColumns = RecentLaunches.Capacity;

    public readonly float Scale;
    public readonly float RowHeight;
    public readonly float TileSize;
    public readonly float RowInset;
    public readonly float RowRadius;
    public readonly float TextGap;
    public readonly float HeaderHeight;
    public readonly float HeaderTextOffset;
    public readonly float PanelPad;
    public readonly float PanelRadius;
    public readonly float RecentTile;
    public readonly float RecentLabelBand;
    public readonly float RecentGutter;
    public readonly float PillHeight;
    public readonly float PillPad;
    public readonly float PillGap;

    public SpotlightLayout(float scale)
    {
        Scale = scale;
        RowHeight = RowHeightUnits * scale;
        TileSize = RowTileUnits * scale;
        RowInset = RowInsetUnits * scale;
        RowRadius = (PanelRadiusUnits - PanelPadUnits) * scale;
        TextGap = TextGapUnits * scale;
        HeaderHeight = SectionHeaderUnits * scale;
        HeaderTextOffset = HeaderTextOffsetUnits * scale;
        PanelPad = PanelPadUnits * scale;
        PanelRadius = PanelRadiusUnits * scale;
        RecentTile = RecentTileUnits * scale;
        RecentLabelBand = RecentLabelBandUnits * scale;
        RecentGutter = RecentGutterUnits * scale;
        PillHeight = PillHeightUnits * scale;
        PillPad = PillPadUnits * scale;
        PillGap = PillGapUnits * scale;
    }

    public float RecentsPanelHeight(float innerWidth) =>
        PanelPad * 2f + HeaderHeight + RecentTileSize(innerWidth) + RecentLabelBand;

    public static Rect RestRect(Rect content, float scale) =>
        new(content.Min, new Vector2(content.Max.X, content.Min.Y + FieldHeightUnits * scale));

    public static Rect FieldRect(Rect origin, Rect rest, float progress) =>
        new(Vector2.Lerp(origin.Min, rest.Min, progress), Vector2.Lerp(origin.Max, rest.Max, progress));

    public static float FieldRadius(Rect origin, float restRadius, float progress)
    {
        var originRadius = origin.Height * 0.5f;
        return originRadius + (restRadius - originRadius) * progress;
    }

    public static float ScrollToReveal(float rowTop, float rowBottom, float scrollY, float viewHeight)
    {
        if (rowTop < scrollY)
        {
            return rowTop;
        }

        if (rowBottom > scrollY + viewHeight)
        {
            return rowBottom - viewHeight;
        }

        return scrollY;
    }

    public float Measure(IReadOnlyList<SpotlightResult> results)
    {
        var total = 0f;
        var lastKind = (SpotlightKind)255;
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var kind = results[resultIndex].Kind;
            if (kind != lastKind)
            {
                lastKind = kind;
                total += HeaderHeight;
            }

            total += RowHeight;
        }

        return total;
    }

    public float RowTop(IReadOnlyList<SpotlightResult> results, int index)
    {
        var total = 0f;
        var lastKind = (SpotlightKind)255;
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var kind = results[resultIndex].Kind;
            if (kind != lastKind)
            {
                lastKind = kind;
                total += HeaderHeight;
            }

            if (resultIndex == index)
            {
                return total;
            }

            total += RowHeight;
        }

        return total;
    }

    public Rect TileRect(Rect row)
    {
        var half = TileSize * 0.5f;
        var centerY = row.Center.Y;
        var left = row.Min.X + RowInset;
        return new Rect(new Vector2(left, centerY - half), new Vector2(left + TileSize, centerY + half));
    }

    public float TextLeft(Rect row) => row.Min.X + RowInset + TileSize + TextGap;

    public Rect PillRect(Rect row, float right, float labelWidth)
    {
        var width = labelWidth + PillPad * 2f;
        var half = PillHeight * 0.5f;
        var centerY = row.Center.Y;
        return new Rect(new Vector2(right - width, centerY - half), new Vector2(right, centerY + half));
    }

    public float RecentCellWidth(float innerWidth) => innerWidth / RecentColumns;

    public float RecentTileSize(float innerWidth) =>
        MathF.Max(0f, MathF.Min(RecentTile, RecentCellWidth(innerWidth) - RecentGutter));
}
