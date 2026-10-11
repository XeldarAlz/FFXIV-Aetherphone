using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Venue;

internal static class BroadcastLayout
{
    public const float TopPad = 8f;
    public const float LabelGap = 8f;
    public const float RowGap = 10f;
    public const float SideShare = 0.1f;
    public const float SpanShare = 0.8f;
    public const float MaxSag = 0.12f;
    public const float NameGap = 6f;
    public const float BetGap = 12f;
    public const float NameCardShare = 2.4f;

    public static float TopRowY(Rect safe, float cardHeight, float scale) =>
        safe.Min.Y + cardHeight * 0.5f + TopPad * scale;

    public static float TopLabelY(float topRowY, float cardHeight, float labelHeight, float scale) =>
        topRowY + cardHeight * 0.5f + LabelGap * scale + labelHeight * 0.5f;

    public static float TopBottom(float topRowY, float cardHeight, float labelHeight, float scale) =>
        TopLabelY(topRowY, cardHeight, labelHeight, scale) + labelHeight * 0.5f;

    public static float SeatAbove(float cardHeight, float betHeight, float scale) =>
        cardHeight * 0.5f + BetGap * scale + betHeight * 0.5f;

    public static float SeatBelow(float cardHeight, float nameHeight, float stackHeight, float scale) =>
        cardHeight * 0.5f + NameGap * scale + nameHeight + stackHeight;

    public static Vector2 Seat(Rect safe, int index, int count, float topBottom, float above, float below,
        float scale)
    {
        var share = count <= 1 ? 0.5f : index / (float)(count - 1);
        var baseY = topBottom + RowGap * scale + above;
        var sag = Math.Clamp(safe.Max.Y - below - baseY, 0f, safe.Height * MaxSag);
        return new Vector2(safe.Min.X + safe.Width * (SideShare + SpanShare * share),
            baseY + sag * MathF.Sin(MathF.PI * share));
    }

    public static float Pitch(Rect safe, int count) =>
        count <= 1 ? safe.Width * SpanShare : safe.Width * SpanShare / (count - 1);

    public static float NameWidth(Rect safe, int count, float cardWidth, float scale) =>
        MathF.Max(1f, MathF.Min(MathF.Max(cardWidth * NameCardShare, safe.Width / (count + 1)),
            MathF.Min(Pitch(safe, count) - NameGap * scale, safe.Width * SideShare * 2f)));
}
