using Aetherphone.Apps.Games.Framework.Cards;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.LuckyDraw;

internal sealed class LuckyDrawLayout
{
    public const float PlateWidth = 128f;
    public const float PlateHeight = 42f;
    private const float ButtonHeight = 50f;
    private const float ButtonMaxWidth = 168f;
    private const float Gap = 10f;
    private const float CaptionBand = 20f;
    private const float RowGap = 8f;
    private const float HumanCardFraction = 0.125f;
    private const float HumanCardMin = 34f;
    private const float HumanCardMax = 50f;
    private const float PileFraction = 0.13f;
    private const float PileMin = 36f;
    private const float PileMax = 54f;
    private const float PileSpacing = 0.78f;
    private const float RowCardsWide = 3.9f;
    private const float FanAngle = 0.16f;
    private const float FanStepFraction = 0.6f;
    private const float LowerHalfFraction = 0.25f;
    private static readonly float[] OpponentCards = { 40f, 40f, 40f, 32f, 32f, 26f, 26f };

    private readonly Vector2[] plates = new Vector2[LuckyDrawBoard.MaxSeats];
    private readonly Vector2[] rows = new Vector2[LuckyDrawBoard.MaxSeats];
    private readonly float[] cardWidths = new float[LuckyDrawBoard.MaxSeats];
    private readonly float[] rowWidths = new float[LuckyDrawBoard.MaxSeats];
    private readonly bool[] rowsAbove = new bool[LuckyDrawBoard.MaxSeats];

    public Rect Ring { get; private set; }

    public Rect Controls { get; private set; }

    public Vector2 HitCenter { get; private set; }

    public Vector2 StayCenter { get; private set; }

    public Vector2 ButtonSize { get; private set; }

    public float CaptionY { get; private set; }

    public Vector2 Deck { get; private set; }

    public Vector2 DiscardPile { get; private set; }

    public float PileWidth { get; private set; }

    public Vector2 Center => Ring.Center;

    public float Scale { get; private set; } = 1f;

    public Vector2 Plate(int seat) => plates[seat];

    public Vector2 Row(int seat) => rows[seat];

    public float CardWidth(int seat) => cardWidths[seat];

    public bool RowAbove(int seat) => rowsAbove[seat];

    public Rect PlateRect(int seat)
    {
        var half = new Vector2(PlateWidth, PlateHeight) * (Scale * 0.5f);
        return new Rect(plates[seat] - half, plates[seat] + half);
    }

    public Rect Zone(int seat)
    {
        var plate = PlateRect(seat);
        var halfRow = new Vector2(rowWidths[seat] * 0.5f + cardWidths[seat] * 0.5f,
            cardWidths[seat] * CardPose.Aspect * 0.5f);
        var row = new Rect(rows[seat] - halfRow, rows[seat] + halfRow);
        return new Rect(Vector2.Min(plate.Min, row.Min), Vector2.Max(plate.Max, row.Max));
    }

    public void Build(Rect area, int seats, float scale)
    {
        Scale = scale;
        var gap = Gap * scale;
        var buttonHeight = ButtonHeight * scale;
        Controls = new Rect(new Vector2(area.Min.X, area.Max.Y - buttonHeight), area.Max);
        var buttonWidth = MathF.Min((area.Width - gap) * 0.5f, ButtonMaxWidth * scale);
        ButtonSize = new Vector2(buttonWidth, buttonHeight);
        HitCenter = new Vector2(area.Center.X - (buttonWidth + gap) * 0.5f, Controls.Center.Y);
        StayCenter = new Vector2(area.Center.X + (buttonWidth + gap) * 0.5f, Controls.Center.Y);
        var plateHalf = new Vector2(PlateWidth, PlateHeight) * (scale * 0.5f);
        var captionBand = CaptionBand * scale;
        CaptionY = Controls.Min.Y - captionBand * 0.5f;
        var humanCard = Math.Clamp(area.Width * HumanCardFraction, HumanCardMin * scale, HumanCardMax * scale);
        var humanPlateY = Controls.Min.Y - captionBand - gap * 0.5f - plateHalf.Y;
        var humanRowY = humanPlateY - (plateHalf.Y + humanCard * CardPose.Aspect * 0.5f + RowGap * scale);
        var ringBottom = humanRowY - humanCard * CardPose.Aspect * 0.5f - gap;
        var ring = new Rect(new Vector2(area.Min.X + plateHalf.X, area.Min.Y + plateHalf.Y),
            new Vector2(area.Max.X - plateHalf.X, MathF.Max(area.Min.Y + plateHalf.Y * 2f, ringBottom)));
        Ring = ring;
        SeatLayout.Ring(seats, ring, plates);
        plates[0] = new Vector2(area.Center.X, humanPlateY);
        PileWidth = Math.Clamp(area.Width * PileFraction, PileMin * scale, PileMax * scale);
        var center = ring.Center;
        Deck = new Vector2(center.X - PileWidth * PileSpacing, center.Y);
        DiscardPile = new Vector2(center.X + PileWidth * PileSpacing, center.Y);
        var opponentCard = MathF.Min(OpponentCards[Math.Clamp(seats, 0, OpponentCards.Length - 1)] * scale,
            humanCard);
        var pileReach = PileWidth * (PileSpacing + 0.5f) + gap;
        var pileHalfHeight = PileWidth * CardPose.Aspect * 0.5f + gap;
        var sideRoom = MathF.Max(opponentCard * 2f, area.Width * 0.5f - pileReach - gap);
        for (var seat = 0; seat < seats; seat++)
        {
            var human = seat == 0;
            var card = human ? humanCard : opponentCard;
            var rowWidth = human
                ? area.Width - card - gap * 2f
                : MathF.Min(card * RowCardsWide, sideRoom - card);
            cardWidths[seat] = card;
            rowWidths[seat] = MathF.Max(0f, rowWidth);
            var plate = plates[seat];
            var above = human || plate.Y > center.Y + ring.Height * 0.5f * LowerHalfFraction;
            rowsAbove[seat] = above;
            var cardHalfHeight = card * CardPose.Aspect * 0.5f;
            var offset = plateHalf.Y + cardHalfHeight + RowGap * scale;
            var rowY = above ? plate.Y - offset : plate.Y + offset;
            var halfSpan = rowWidths[seat] * 0.5f + card * 0.5f;
            var rowX = plate.X;
            if (!human && MathF.Abs(rowY - center.Y) < pileHalfHeight + cardHalfHeight)
            {
                rowX = plate.X < center.X
                    ? MathF.Min(rowX, center.X - pileReach - halfSpan)
                    : MathF.Max(rowX, center.X + pileReach + halfSpan);
            }

            rowX = Math.Clamp(rowX, area.Min.X + halfSpan, MathF.Max(area.Min.X + halfSpan, area.Max.X - halfSpan));
            rows[seat] = new Vector2(rowX, rowY);
        }
    }

    public CardPose RowPose(int seat, int index, int count)
    {
        var width = cardWidths[seat];
        return HandLayout.Fan(index, count, rows[seat], rowWidths[seat], FanAngle, width * FanStepFraction).Pose(width);
    }
}
