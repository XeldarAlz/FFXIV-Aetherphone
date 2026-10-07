using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Claim;

internal readonly struct ClaimFacet
{
    public readonly int First;
    public readonly int Second;
    public readonly int Third;
    public readonly float Shade;
    public readonly float Tint;

    public ClaimFacet(int first, int second, int third, float shade, float tint)
    {
        First = first;
        Second = second;
        Third = third;
        Shade = shade;
        Tint = tint;
    }
}

internal sealed class ClaimMosaic
{
    public const int Columns = 7;
    public const int Rows = 11;
    public const int PointCount = (Columns + 1) * (Rows + 1);
    public const int FacetCount = Columns * Rows * 2;
    public const int GlintCount = 14;
    private const float Jitter = 0.38f;
    private const float Relief = 26f;
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.55f, -0.75f, 0.9f));

    private readonly Vector2[] points = new Vector2[PointCount];
    private readonly float[] heights = new float[PointCount];
    private readonly ClaimFacet[] facets = new ClaimFacet[FacetCount];
    private readonly int[] glints = new int[GlintCount];
    private readonly float[] glintPhases = new float[GlintCount];

    public ReadOnlySpan<Vector2> Points => points;

    public ReadOnlySpan<ClaimFacet> Facets => facets;

    public ReadOnlySpan<int> Glints => glints;

    public ReadOnlySpan<float> GlintPhases => glintPhases;

    public uint Seed { get; private set; }

    public void Build(uint seed, float width, float height)
    {
        Seed = seed;
        var random = GameRandom.FromSeed(seed);
        var cellWidth = width / Columns;
        var cellHeight = height / Rows;
        for (var row = 0; row <= Rows; row++)
        {
            for (var column = 0; column <= Columns; column++)
            {
                var index = row * (Columns + 1) + column;
                var pointX = column * cellWidth;
                var pointY = row * cellHeight;
                if (column > 0 && column < Columns)
                {
                    pointX += random.Range(-Jitter, Jitter) * cellWidth;
                }

                if (row > 0 && row < Rows)
                {
                    pointY += random.Range(-Jitter, Jitter) * cellHeight;
                }

                points[index] = new Vector2(pointX, pointY);
                heights[index] = random.NextFloat();
            }
        }

        var facet = 0;
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var topLeft = row * (Columns + 1) + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + Columns + 1;
                var bottomRight = bottomLeft + 1;
                if (random.Chance(0.5f))
                {
                    facets[facet++] = Facet(topLeft, topRight, bottomRight, random.NextFloat());
                    facets[facet++] = Facet(topLeft, bottomRight, bottomLeft, random.NextFloat());
                }
                else
                {
                    facets[facet++] = Facet(topLeft, topRight, bottomLeft, random.NextFloat());
                    facets[facet++] = Facet(topRight, bottomRight, bottomLeft, random.NextFloat());
                }
            }
        }

        for (var glint = 0; glint < GlintCount; glint++)
        {
            glints[glint] = random.Next(PointCount);
            glintPhases[glint] = random.NextFloat() * MathF.Tau;
        }
    }

    private ClaimFacet Facet(int first, int second, int third, float tint)
    {
        var firstPoint = new Vector3(points[first], heights[first] * Relief);
        var secondPoint = new Vector3(points[second], heights[second] * Relief);
        var thirdPoint = new Vector3(points[third], heights[third] * Relief);
        var normal = Vector3.Cross(secondPoint - firstPoint, thirdPoint - firstPoint);
        var length = normal.Length();
        var shade = 0.5f;
        if (length > 1e-5f)
        {
            normal /= length;
            if (normal.Z < 0f)
            {
                normal = -normal;
            }

            shade = Math.Clamp(Vector3.Dot(normal, Light) * 1.35f - 0.35f, 0f, 1f);
        }

        return new ClaimFacet(first, second, third, shade, tint);
    }
}
