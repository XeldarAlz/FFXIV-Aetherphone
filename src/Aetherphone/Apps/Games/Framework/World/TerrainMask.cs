using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework.World;

internal sealed class TerrainMask
{
    public const int MaxPlateaus = 8;
    private const int WordShift = 6;
    private const int WordMask = 63;
    private const int NormalRadius = 3;
    private const float Epsilon = 1e-6f;
    private const int MinPlateauHalfWidth = 3;
    private const int PlateauHalfWidthDivisor = 64;
    private const int PlateauSpacing = 6;
    private const float PlateauHighest = 0.32f;
    private const float PlateauLowest = 0.74f;
    private const float SurfaceHighest = 0.18f;
    private const float SurfaceLowest = 0.9f;
    private const float HillFeatures = 6f;
    private const float RuggedFeatures = 34f;
    private const float RuggedFraction = 0.035f;
    private const float AmplitudeStretch = 3f;
    private const float ChannelChance = 0.55f;
    private const float ChannelShortest = 0.14f;
    private const float ChannelLongest = 0.26f;
    private const float SeaShortest = 0.3f;
    private const float SeaLongest = 0.6f;
    private const int ChannelMinimumSpace = 8;
    private const int CaveCrustDivisor = 30;
    private const int MinCaveCrust = 4;
    private const int FootingDivisor = 8;
    private const int MinFooting = 8;
    private const float ShelterReach = 2.5f;
    private const float ShelterStrength = 0.6f;
    private const float CaveFeaturesAcross = 8f;
    private const float CaveFeaturesDown = 4f;
    private const float CaveThreshold = 0.64f;
    private const float CaveDeepening = 0.08f;
    private const float RoofFraction = 0.15f;
    private const float RoofOpening = 0.46f;
    private const float RoofFeatures = 7f;
    private const float StalactiteFeatures = 70f;
    private const uint RuggedSalt = 0x27D4EB2Fu;
    private const uint StalactiteSalt = 0x5BD1E995u;

    private static readonly Vector2 Up = new(0f, -1f);

    private readonly ulong[] words;
    private readonly int wordsPerRow;
    private readonly float[] heights;
    private readonly int[] plateauColumns = new int[MaxPlateaus];
    private readonly int[] plateauRows = new int[MaxPlateaus];
    private readonly TerrainPlateau[] plateaus = new TerrainPlateau[MaxPlateaus];
    private readonly Vector2[] spawns = new Vector2[MaxPlateaus];
    private int plateauCount;
    private int plateauHalfWidth;
    private bool dirty;
    private int dirtyMinColumn;
    private int dirtyMinRow;
    private int dirtyMaxColumn;
    private int dirtyMaxRow;

    public TerrainMask(int width, int height, float metresPerCell)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metresPerCell);
        Width = width;
        Height = height;
        MetresPerCell = metresPerCell;
        wordsPerRow = (width + WordMask) >> WordShift;
        words = new ulong[wordsPerRow * height];
        heights = new float[width];
    }

    public int Width { get; }

    public int Height { get; }

    public float MetresPerCell { get; }

    public float WorldWidth => Width * MetresPerCell;

    public float WorldHeight => Height * MetresPerCell;

    public ReadOnlySpan<TerrainPlateau> Plateaus => new(plateaus, 0, plateauCount);

    public void Generate(ref GameRandom random, TerrainStyle style)
    {
        Array.Clear(words);
        var heightSeed = random.NextUInt();
        var ruggedSeed = random.NextUInt() ^ RuggedSalt;
        var caveSeed = random.NextUInt();
        var roofSeed = random.NextUInt();
        ShapeHeights(style, heightSeed, ruggedSeed);
        LayOutPlateaus(ref random);
        BlendPlateaus();
        if (style == TerrainStyle.Islands)
        {
            CutChannels(ref random);
        }

        FlattenPlateaus();
        FillBelowHeights();
        if (style == TerrainStyle.Caverns)
        {
            HollowCaves(caveSeed);
            HangRoof(roofSeed);
        }

        PublishPlateaus();
        MarkAllDirty();
    }

    public void Clear()
    {
        Array.Clear(words);
        plateauCount = 0;
        MarkAllDirty();
    }

    public ReadOnlySpan<Vector2> SpawnPoints(int count)
    {
        var take = Math.Clamp(count, 0, plateauCount);
        if (take == 0)
        {
            return ReadOnlySpan<Vector2>.Empty;
        }

        if (take == 1)
        {
            spawns[0] = plateaus[plateauCount / 2].Center;
            return new ReadOnlySpan<Vector2>(spawns, 0, 1);
        }

        for (var spawnIndex = 0; spawnIndex < take; spawnIndex++)
        {
            var plateauIndex = (int)MathF.Round(spawnIndex * (plateauCount - 1) / (float)(take - 1));
            spawns[spawnIndex] = plateaus[plateauIndex].Center;
        }

        return new ReadOnlySpan<Vector2>(spawns, 0, take);
    }

    public int ColumnOf(float x) => (int)MathF.Floor(x / MetresPerCell);

    public int RowOf(float y) => (int)MathF.Floor(y / MetresPerCell);

    public Vector2 CellCenter(int column, int row) =>
        new((column + 0.5f) * MetresPerCell, (row + 0.5f) * MetresPerCell);

    public bool InBounds(int column, int row) => (uint)column < (uint)Width && (uint)row < (uint)Height;

    public bool IsSolid(int column, int row) => InBounds(column, row) && IsSolidUnchecked(column, row);

    public bool IsSolid(Vector2 point) => IsSolid(ColumnOf(point.X), RowOf(point.Y));

    public int Carve(Vector2 center, float radius) => ApplyCircle(center, radius, false);

    public int Fill(Vector2 center, float radius) => ApplyCircle(center, radius, true);

    public int CarveRect(Rect area) => ApplyRect(area, false);

    public int FillRect(Rect area) => ApplyRect(area, true);

    public float SurfaceY(float x) => SurfaceY(x, 0f);

    public float SurfaceY(float x, float fromY)
    {
        var column = ColumnOf(x);
        if ((uint)column >= (uint)Width)
        {
            return WorldHeight;
        }

        for (var row = Math.Max(0, RowOf(fromY)); row < Height; row++)
        {
            if (IsSolidUnchecked(column, row))
            {
                return row * MetresPerCell;
            }
        }

        return WorldHeight;
    }

    public Vector2 Normal(Vector2 point) => TryNormal(point, out var normal) ? normal : Up;

    public bool Raycast(Vector2 origin, Vector2 direction, float maxDistance, out TerrainHit hit)
    {
        hit = default;
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared <= Epsilon || maxDistance <= 0f)
        {
            return false;
        }

        var heading = direction / MathF.Sqrt(lengthSquared);
        var start = origin / MetresPerCell;
        if (!ClipToGrid(start, heading, maxDistance / MetresPerCell, out var travelled, out var exit, out var axis))
        {
            return false;
        }

        var entry = start + heading * travelled;
        var column = Math.Clamp((int)MathF.Floor(entry.X), 0, Width - 1);
        var row = Math.Clamp((int)MathF.Floor(entry.Y), 0, Height - 1);
        var stepColumn = heading.X > 0f ? 1 : -1;
        var stepRow = heading.Y > 0f ? 1 : -1;
        var crossColumn = CrossingDistance(start.X, heading.X, column);
        var crossRow = CrossingDistance(start.Y, heading.Y, row);
        var spanColumn = heading.X == 0f ? float.PositiveInfinity : 1f / MathF.Abs(heading.X);
        var spanRow = heading.Y == 0f ? float.PositiveInfinity : 1f / MathF.Abs(heading.Y);
        while (true)
        {
            if (IsSolidUnchecked(column, row))
            {
                hit = MakeHit(start, heading, travelled, column, row, FaceNormal(axis, stepColumn, stepRow, heading));
                return true;
            }

            if (crossColumn < crossRow)
            {
                travelled = crossColumn;
                crossColumn += spanColumn;
                column += stepColumn;
                axis = CrossAxis.Column;
            }
            else
            {
                travelled = crossRow;
                crossRow += spanRow;
                row += stepRow;
                axis = CrossAxis.Row;
            }

            if (travelled > exit || !InBounds(column, row))
            {
                return false;
            }
        }
    }

    public bool CollideCircle(Vector2 center, float radius, out Vector2 normal, out float depth)
    {
        normal = Up;
        depth = 0f;
        if (radius <= 0f)
        {
            return false;
        }

        var reach = radius + MetresPerCell * 0.5f;
        var reachSquared = reach * reach;
        var firstColumn = Math.Max(0, ColumnOf(center.X - reach));
        var lastColumn = Math.Min(Width - 1, ColumnOf(center.X + reach));
        var firstRow = Math.Max(0, RowOf(center.Y - reach));
        var lastRow = Math.Min(Height - 1, RowOf(center.Y + reach));
        var push = Vector2.Zero;
        var touching = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (!IsSolidUnchecked(column, row))
                {
                    continue;
                }

                var offset = CellCenter(column, row) - center;
                var distanceSquared = offset.LengthSquared();
                if (distanceSquared >= reachSquared)
                {
                    continue;
                }

                touching++;
                var distance = MathF.Sqrt(distanceSquared);
                if (distance > Epsilon)
                {
                    push -= offset / distance * (reach - distance);
                }
            }
        }

        if (touching == 0)
        {
            return false;
        }

        var pushSquared = push.LengthSquared();
        if (pushSquared > Epsilon)
        {
            normal = push / MathF.Sqrt(pushSquared);
        }

        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (!IsSolidUnchecked(column, row))
                {
                    continue;
                }

                var offset = CellCenter(column, row) - center;
                var distanceSquared = offset.LengthSquared();
                if (distanceSquared >= reachSquared)
                {
                    continue;
                }

                var along = Vector2.Dot(offset, normal);
                var acrossSquared = MathF.Max(0f, distanceSquared - along * along);
                var required = along + MathF.Sqrt(MathF.Max(0f, reachSquared - acrossSquared));
                depth = MathF.Max(depth, required);
            }
        }

        return depth > 0f;
    }

    public bool TakeDirty(out CellRegion region)
    {
        if (!dirty)
        {
            region = default;
            return false;
        }

        region = new CellRegion(dirtyMinColumn, dirtyMinRow, dirtyMaxColumn, dirtyMaxRow);
        dirty = false;
        return true;
    }

    private bool IsSolidUnchecked(int column, int row) =>
        (words[row * wordsPerRow + (column >> WordShift)] & (1UL << (column & WordMask))) != 0UL;

    private void SetSolidUnchecked(int column, int row) =>
        words[row * wordsPerRow + (column >> WordShift)] |= 1UL << (column & WordMask);

    private void ClearSolidUnchecked(int column, int row) =>
        words[row * wordsPerRow + (column >> WordShift)] &= ~(1UL << (column & WordMask));

    private int ApplyCircle(Vector2 center, float radius, bool solid)
    {
        if (radius <= 0f)
        {
            return 0;
        }

        var firstRow = Math.Max(0, (int)MathF.Ceiling((center.Y - radius) / MetresPerCell - 0.5f));
        var lastRow = Math.Min(Height - 1, (int)MathF.Floor((center.Y + radius) / MetresPerCell - 0.5f));
        var radiusSquared = radius * radius;
        var changed = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var offsetY = (row + 0.5f) * MetresPerCell - center.Y;
            var spanSquared = radiusSquared - offsetY * offsetY;
            if (spanSquared < 0f)
            {
                continue;
            }

            var halfSpan = MathF.Sqrt(spanSquared);
            var firstColumn = Math.Max(0, (int)MathF.Ceiling((center.X - halfSpan) / MetresPerCell - 0.5f));
            var lastColumn = Math.Min(Width - 1, (int)MathF.Floor((center.X + halfSpan) / MetresPerCell - 0.5f));
            if (firstColumn > lastColumn)
            {
                continue;
            }

            changed += ApplySpan(row, firstColumn, lastColumn, solid);
        }

        return changed;
    }

    private int ApplyRect(Rect area, bool solid)
    {
        var firstColumn = Math.Max(0, (int)MathF.Ceiling(area.Min.X / MetresPerCell - 0.5f));
        var lastColumn = Math.Min(Width - 1, (int)MathF.Floor(area.Max.X / MetresPerCell - 0.5f));
        var firstRow = Math.Max(0, (int)MathF.Ceiling(area.Min.Y / MetresPerCell - 0.5f));
        var lastRow = Math.Min(Height - 1, (int)MathF.Floor(area.Max.Y / MetresPerCell - 0.5f));
        if (firstColumn > lastColumn)
        {
            return 0;
        }

        var changed = 0;
        for (var row = firstRow; row <= lastRow; row++)
        {
            changed += ApplySpan(row, firstColumn, lastColumn, solid);
        }

        return changed;
    }

    private int ApplySpan(int row, int firstColumn, int lastColumn, bool solid)
    {
        var rowStart = row * wordsPerRow;
        var firstWord = firstColumn >> WordShift;
        var lastWord = lastColumn >> WordShift;
        var changed = 0;
        for (var wordIndex = firstWord; wordIndex <= lastWord; wordIndex++)
        {
            var lowBit = wordIndex == firstWord ? firstColumn & WordMask : 0;
            var highBit = wordIndex == lastWord ? lastColumn & WordMask : WordMask;
            var bits = (ulong.MaxValue >> (WordMask - highBit)) & (ulong.MaxValue << lowBit);
            ref var word = ref words[rowStart + wordIndex];
            if (solid)
            {
                changed += BitOperations.PopCount(~word & bits);
                word |= bits;
            }
            else
            {
                changed += BitOperations.PopCount(word & bits);
                word &= ~bits;
            }
        }

        if (changed > 0)
        {
            MarkDirty(firstColumn, row, lastColumn, row);
        }

        return changed;
    }

    private void MarkDirty(int minColumn, int minRow, int maxColumn, int maxRow)
    {
        if (!dirty)
        {
            dirty = true;
            dirtyMinColumn = minColumn;
            dirtyMinRow = minRow;
            dirtyMaxColumn = maxColumn;
            dirtyMaxRow = maxRow;
            return;
        }

        dirtyMinColumn = Math.Min(dirtyMinColumn, minColumn);
        dirtyMinRow = Math.Min(dirtyMinRow, minRow);
        dirtyMaxColumn = Math.Max(dirtyMaxColumn, maxColumn);
        dirtyMaxRow = Math.Max(dirtyMaxRow, maxRow);
    }

    private void MarkAllDirty()
    {
        dirty = false;
        MarkDirty(0, 0, Width - 1, Height - 1);
    }

    private bool TryNormal(Vector2 point, out Vector2 normal)
    {
        var column = ColumnOf(point.X);
        var row = RowOf(point.Y);
        var sum = Vector2.Zero;
        for (var offsetY = -NormalRadius; offsetY <= NormalRadius; offsetY++)
        {
            for (var offsetX = -NormalRadius; offsetX <= NormalRadius; offsetX++)
            {
                if (offsetX * offsetX + offsetY * offsetY > NormalRadius * NormalRadius)
                {
                    continue;
                }

                if (IsSolid(column + offsetX, row + offsetY))
                {
                    sum -= new Vector2(offsetX, offsetY);
                }
            }
        }

        var lengthSquared = sum.LengthSquared();
        if (lengthSquared < Epsilon)
        {
            normal = Up;
            return false;
        }

        normal = sum / MathF.Sqrt(lengthSquared);
        return true;
    }

    private TerrainHit MakeHit(Vector2 start, Vector2 heading, float travelled, int column, int row, Vector2 faceNormal)
    {
        var point = (start + heading * travelled) * MetresPerCell;
        var normal = faceNormal;
        if (TryNormal(point, out var smooth) && Vector2.Dot(smooth, heading) < 0f)
        {
            normal = smooth;
        }

        return new TerrainHit(point, normal, travelled * MetresPerCell, column, row);
    }

    private bool ClipToGrid(Vector2 start, Vector2 heading, float limit, out float enter, out float exit,
        out CrossAxis axis)
    {
        enter = 0f;
        exit = limit;
        axis = CrossAxis.None;
        if (!ClipAxis(start.X, heading.X, Width, CrossAxis.Column, ref enter, ref exit, ref axis))
        {
            return false;
        }

        return ClipAxis(start.Y, heading.Y, Height, CrossAxis.Row, ref enter, ref exit, ref axis);
    }

    private static bool ClipAxis(float start, float heading, float size, CrossAxis slab, ref float enter,
        ref float exit, ref CrossAxis axis)
    {
        if (MathF.Abs(heading) < Epsilon)
        {
            return start >= 0f && start <= size;
        }

        var near = -start / heading;
        var far = (size - start) / heading;
        if (near > far)
        {
            (near, far) = (far, near);
        }

        if (near > enter)
        {
            enter = near;
            axis = slab;
        }

        exit = MathF.Min(exit, far);
        return enter <= exit;
    }

    private static float CrossingDistance(float start, float heading, int cell)
    {
        if (heading > 0f)
        {
            return (cell + 1 - start) / heading;
        }

        if (heading < 0f)
        {
            return (cell - start) / heading;
        }

        return float.PositiveInfinity;
    }

    private static Vector2 FaceNormal(CrossAxis axis, int stepColumn, int stepRow, Vector2 heading) => axis switch
    {
        CrossAxis.Column => new Vector2(-stepColumn, 0f),
        CrossAxis.Row => new Vector2(0f, -stepRow),
        _ => -heading,
    };

    private void ShapeHeights(TerrainStyle style, uint heightSeed, uint ruggedSeed)
    {
        var baseFraction = style switch
        {
            TerrainStyle.Islands => 0.6f,
            TerrainStyle.Caverns => 0.5f,
            _ => 0.56f,
        };
        var amplitudeFraction = style switch
        {
            TerrainStyle.Islands => 0.14f,
            TerrainStyle.Caverns => 0.12f,
            _ => 0.2f,
        };
        var featureWidth = Width / HillFeatures;
        var ruggedWidth = Width / RuggedFeatures;
        for (var column = 0; column < Width; column++)
        {
            var broad = TerrainNoise.Fractal1D(column / featureWidth, heightSeed, 4) - 0.5f;
            var rugged = TerrainNoise.Fractal1D(column / ruggedWidth, ruggedSeed, 2) - 0.5f;
            var fraction = baseFraction + broad * AmplitudeStretch * amplitudeFraction + rugged * 2f * RuggedFraction;
            heights[column] = Height * Math.Clamp(fraction, SurfaceHighest, SurfaceLowest);
        }
    }

    private void LayOutPlateaus(ref GameRandom random)
    {
        plateauHalfWidth = Math.Min(Math.Max(MinPlateauHalfWidth, Width / PlateauHalfWidthDivisor), (Width - 1) / 2);
        plateauCount = Math.Clamp(Width / Math.Max(1, plateauHalfWidth * PlateauSpacing), 1, MaxPlateaus);
        var slotWidth = Width / (float)plateauCount;
        var blend = plateauHalfWidth * 2;
        var jitter = MathF.Max(0f, (slotWidth - (plateauHalfWidth * 2 + 1) - blend * 2) * 0.5f);
        var highest = (int)(Height * PlateauHighest);
        var lowest = (int)(Height * PlateauLowest);
        for (var plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            var center = (int)MathF.Round((plateauIndex + 0.5f) * slotWidth + random.Range(-jitter, jitter));
            center = Math.Clamp(center, plateauHalfWidth, Width - 1 - plateauHalfWidth);
            plateauColumns[plateauIndex] = center;
            plateauRows[plateauIndex] = Math.Clamp((int)MathF.Round(heights[center]), highest, lowest);
        }
    }

    private void BlendPlateaus()
    {
        var blend = plateauHalfWidth * 2;
        for (var plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            var center = plateauColumns[plateauIndex];
            float level = plateauRows[plateauIndex];
            for (var offset = 1; offset <= blend; offset++)
            {
                var weight = TerrainNoise.Smooth(offset / (float)(blend + 1));
                var left = center - plateauHalfWidth - offset;
                var right = center + plateauHalfWidth + offset;
                if (left >= 0)
                {
                    heights[left] = TerrainNoise.Lerp(level, heights[left], weight);
                }

                if (right < Width)
                {
                    heights[right] = TerrainNoise.Lerp(level, heights[right], weight);
                }
            }
        }
    }

    private void CutChannels(ref GameRandom random)
    {
        var firstEdge = plateauColumns[0] - plateauHalfWidth;
        var leftSea = (int)(firstEdge * random.Range(SeaShortest, SeaLongest));
        if (leftSea > 0)
        {
            CutChannel(0, leftSea - 1, -1, firstEdge);
        }

        var lastEdge = plateauColumns[plateauCount - 1] + plateauHalfWidth;
        var rightSea = (int)((Width - 1 - lastEdge) * random.Range(SeaShortest, SeaLongest));
        if (rightSea > 0)
        {
            CutChannel(Width - rightSea, Width - 1, lastEdge, Width);
        }

        var cut = false;
        for (var plateauIndex = 0; plateauIndex < plateauCount - 1; plateauIndex++)
        {
            var wanted = random.Chance(ChannelChance);
            var last = plateauIndex == plateauCount - 2;
            if (!wanted && (cut || !last))
            {
                continue;
            }

            cut |= CutChannelBetween(plateauIndex, ref random);
        }
    }

    private bool CutChannelBetween(int plateauIndex, ref GameRandom random)
    {
        var leftEdge = plateauColumns[plateauIndex] + plateauHalfWidth;
        var rightEdge = plateauColumns[plateauIndex + 1] - plateauHalfWidth;
        var space = rightEdge - leftEdge - 1;
        if (space < ChannelMinimumSpace)
        {
            return false;
        }

        var halfWidth = Math.Max(1, (int)(space * random.Range(ChannelShortest, ChannelLongest) * 0.5f));
        var middle = (leftEdge + rightEdge) / 2;
        CutChannel(middle - halfWidth, middle + halfWidth, leftEdge, rightEdge);
        return true;
    }

    private void CutChannel(int first, int last, int leftLimit, int rightLimit)
    {
        for (var column = first; column <= last; column++)
        {
            heights[column] = Height;
        }

        var leftTaper = first - leftLimit - 1;
        for (var offset = 1; offset <= leftTaper; offset++)
        {
            var column = first - offset;
            var weight = TerrainNoise.Smooth(offset / (float)(leftTaper + 1));
            heights[column] = TerrainNoise.Lerp(Height, heights[column], weight);
        }

        var rightTaper = rightLimit - last - 1;
        for (var offset = 1; offset <= rightTaper; offset++)
        {
            var column = last + offset;
            var weight = TerrainNoise.Smooth(offset / (float)(rightTaper + 1));
            heights[column] = TerrainNoise.Lerp(Height, heights[column], weight);
        }
    }

    private void FlattenPlateaus()
    {
        for (var plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            var center = plateauColumns[plateauIndex];
            for (var column = center - plateauHalfWidth; column <= center + plateauHalfWidth; column++)
            {
                heights[column] = plateauRows[plateauIndex];
            }
        }
    }

    private void FillBelowHeights()
    {
        for (var column = 0; column < Width; column++)
        {
            for (var row = Math.Max(0, (int)MathF.Round(heights[column])); row < Height; row++)
            {
                SetSolidUnchecked(column, row);
            }
        }
    }

    private void HollowCaves(uint caveSeed)
    {
        var crust = Math.Max(MinCaveCrust, Height / CaveCrustDivisor);
        var footing = Math.Max(MinFooting, Height / FootingDivisor);
        var caveWidth = Width / CaveFeaturesAcross;
        var caveHeight = Height / CaveFeaturesDown;
        for (var column = 0; column < Width; column++)
        {
            var firstRow = (int)MathF.Round(heights[column]) + crust;
            for (var row = firstRow; row < Height - crust; row++)
            {
                var threshold = CaveThreshold - CaveDeepening * row / Height + Shelter(column, row, footing);
                if (TerrainNoise.Fractal2D(column / caveWidth, row / caveHeight, caveSeed, 4) > threshold)
                {
                    ClearSolidUnchecked(column, row);
                }
            }
        }
    }

    private float Shelter(int column, int row, int footing)
    {
        var reach = MathF.Max(1f, plateauHalfWidth * ShelterReach);
        var shelter = 0f;
        for (var plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            var across = (column - plateauColumns[plateauIndex]) / reach;
            var down = (row - plateauRows[plateauIndex]) / (float)footing;
            var distanceSquared = across * across + down * down;
            if (distanceSquared >= 1f)
            {
                continue;
            }

            shelter = MathF.Max(shelter, (1f - MathF.Sqrt(distanceSquared)) * ShelterStrength);
        }

        return shelter;
    }

    private void HangRoof(uint roofSeed)
    {
        var deepest = Height * RoofFraction;
        var roofWidth = Width / RoofFeatures;
        var stalactiteWidth = Width / StalactiteFeatures;
        var stalactiteSeed = roofSeed ^ StalactiteSalt;
        for (var column = 0; column < Width; column++)
        {
            var cover = (TerrainNoise.Fractal1D(column / roofWidth, roofSeed, 3) - RoofOpening) / (1f - RoofOpening);
            if (cover <= 0f)
            {
                continue;
            }

            var spike = TerrainNoise.Fractal1D(column / stalactiteWidth, stalactiteSeed, 2);
            var thickness = (int)MathF.Min(deepest, deepest * MathF.Sqrt(cover) * (0.55f + spike * 0.9f));
            for (var row = 0; row < thickness; row++)
            {
                SetSolidUnchecked(column, row);
            }
        }
    }

    private void PublishPlateaus()
    {
        for (var plateauIndex = 0; plateauIndex < plateauCount; plateauIndex++)
        {
            var center = new Vector2((plateauColumns[plateauIndex] + 0.5f) * MetresPerCell,
                plateauRows[plateauIndex] * MetresPerCell);
            plateaus[plateauIndex] = new TerrainPlateau(center, (plateauHalfWidth + 0.5f) * MetresPerCell);
        }
    }

    private enum CrossAxis : byte
    {
        None,
        Column,
        Row,
    }
}
