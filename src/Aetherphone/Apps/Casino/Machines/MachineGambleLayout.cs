using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineGambleLayout
{
    public const float RowGap = 8f;
    public const float Inset = Metrics.Space.Sm;
    public const float LadderPad = 8f;
    public const float CardMinHeight = 36f;

    public readonly Rect Heading;
    public readonly Rect Amount;
    public readonly Rect Ladder;
    public readonly Rect Card;
    public readonly Rect Red;
    public readonly Rect Black;
    public readonly Rect Collect;
    public readonly bool HasCard;

    private MachineGambleLayout(Rect heading, Rect amount, Rect ladder, Rect card, Rect red, Rect black, Rect collect,
        bool hasCard)
    {
        Heading = heading;
        Amount = amount;
        Ladder = ladder;
        Card = card;
        Red = red;
        Black = black;
        Collect = collect;
        HasCard = hasCard;
    }

    public static Rect PanelOf(Rect glass, Rect window) => new(glass.Min, window.Max);

    public static MachineGambleLayout Compute(Rect panel, float headingHeight, float amountHeight,
        float ladderTextHeight, float buttonHeight, float scale)
    {
        var inner = panel.Inset(Inset * scale);
        var space = Metrics.Space.Xs * scale;
        var heading = Band(inner, inner.Min.Y, headingHeight);
        var amount = Band(inner, heading.Max.Y, amountHeight);
        var ladder = Band(inner, amount.Max.Y + space, ladderTextHeight + LadderPad * scale);
        var gap = RowGap * scale;
        var collectTop = MathF.Max(inner.Min.Y, inner.Max.Y - buttonHeight);
        var choiceTop = MathF.Max(inner.Min.Y, collectTop - gap - buttonHeight);
        var choiceBottom = MathF.Max(choiceTop, collectTop - gap);
        var half = MathF.Max(0f, (inner.Width - gap) * 0.5f);
        var red = new Rect(new Vector2(inner.Min.X, choiceTop), new Vector2(inner.Min.X + half, choiceBottom));
        var black = new Rect(new Vector2(MathF.Min(inner.Max.X, red.Max.X + gap), choiceTop),
            new Vector2(inner.Max.X, choiceBottom));
        var collect = new Rect(new Vector2(inner.Min.X, collectTop), inner.Max);
        var cardTop = ladder.Max.Y + space;
        var card = new Rect(new Vector2(inner.Min.X, cardTop),
            new Vector2(inner.Max.X, MathF.Max(cardTop, choiceTop - space)));
        return new MachineGambleLayout(heading, amount, ladder, card, red, black, collect,
            card.Height >= CardMinHeight * scale);
    }

    private static Rect Band(Rect inner, float top, float height) =>
        new(new Vector2(inner.Min.X, top), new Vector2(inner.Max.X, top + height));
}
