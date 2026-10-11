using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct WheelBoardLayout
{
    public const float CaptionHeight = 18f;
    public const float CaptionGap = 4f;
    public const float WheelGap = 6f;
    public const float CardGap = 6f;

    public readonly Rect History;
    public readonly Rect Wheel;
    public readonly Rect Caption;
    public readonly Rect Cards;

    private readonly float cardWidth;
    private readonly float cardGap;

    private WheelBoardLayout(Rect history, Rect wheel, Rect caption, Rect cards, float cardWidth, float cardGap)
    {
        History = history;
        Wheel = wheel;
        Caption = caption;
        Cards = cards;
        this.cardWidth = cardWidth;
        this.cardGap = cardGap;
    }

    public static WheelBoardLayout Compute(Rect safe, float scale)
    {
        var history = WheelHistoryLayout.StripOf(safe, scale);
        var cardsTop = MathF.Max(history.Max.Y, safe.Max.Y - WheelPodiumLayout.Height(scale));
        var cards = new Rect(new Vector2(safe.Min.X, cardsTop), safe.Max);
        var captionBottom = MathF.Max(history.Max.Y, cardsTop - CaptionGap * scale);
        var captionTop = MathF.Max(history.Max.Y, captionBottom - CaptionHeight * scale);
        var caption = new Rect(new Vector2(safe.Min.X, captionTop), new Vector2(safe.Max.X, captionBottom));
        var wheelTop = MathF.Min(captionTop, history.Max.Y + WheelHistoryLayout.BelowGap * scale);
        var wheelBottom = MathF.Max(wheelTop, captionTop - WheelGap * scale);
        var wheel = new Rect(new Vector2(safe.Min.X, wheelTop), new Vector2(safe.Max.X, wheelBottom));
        var gap = CardGap * scale;
        var width = MathF.Max(0f, (safe.Width - gap * (WheelRules.SpotCount - 1)) / WheelRules.SpotCount);
        return new WheelBoardLayout(history, wheel, caption, cards, width, gap);
    }

    public Rect Card(int spot)
    {
        var left = Cards.Min.X + spot * (cardWidth + cardGap);
        return new Rect(new Vector2(left, Cards.Min.Y), new Vector2(left + cardWidth, Cards.Max.Y));
    }
}
