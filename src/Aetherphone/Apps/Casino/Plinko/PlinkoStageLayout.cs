using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Plinko;

internal readonly struct PlinkoStageLayout
{
    public const float SignHeight = 36f;
    public const float SectionGap = 8f;
    public const float PlatePad = 8f;
    public const float PlateGap = 6f;
    public const float RailHeight = 26f;
    public const float PlateRadius = 18f;

    public readonly Rect Sign;
    public readonly Rect Board;
    public readonly Rect Plate;
    public readonly Rect Rail;
    public readonly Rect Stats;

    private PlinkoStageLayout(Rect sign, Rect board, Rect plate, Rect rail, Rect stats)
    {
        Sign = sign;
        Board = board;
        Plate = plate;
        Rail = rail;
        Stats = stats;
    }

    public static float PlateHeight(float scale) =>
        (PlatePad * 2f + RailHeight + PlateGap + OriginalsControls.StatHeight) * scale;

    public static PlinkoStageLayout Compute(Rect safe, float scale)
    {
        var signBottom = MathF.Min(safe.Max.Y, safe.Min.Y + SignHeight * scale);
        var sign = new Rect(safe.Min, new Vector2(safe.Max.X, signBottom));
        var plateTop = MathF.Max(signBottom, safe.Max.Y - PlateHeight(scale));
        var plate = new Rect(new Vector2(safe.Min.X, plateTop), safe.Max);
        var pad = PlatePad * scale;
        var innerLeft = plate.Min.X + pad;
        var innerRight = MathF.Max(innerLeft, plate.Max.X - pad);
        var railTop = plate.Min.Y + pad;
        var rail = new Rect(new Vector2(innerLeft, railTop), new Vector2(innerRight, railTop + RailHeight * scale));
        var statsTop = rail.Max.Y + PlateGap * scale;
        var stats = new Rect(new Vector2(innerLeft, statsTop),
            new Vector2(innerRight, statsTop + OriginalsControls.StatHeight * scale));
        var gap = SectionGap * scale;
        var boardTop = signBottom + gap;
        var board = new Rect(new Vector2(safe.Min.X, boardTop),
            new Vector2(safe.Max.X, MathF.Max(boardTop, plate.Min.Y - gap)));
        return new PlinkoStageLayout(sign, board, plate, rail, stats);
    }
}
