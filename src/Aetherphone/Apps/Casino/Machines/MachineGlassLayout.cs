using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineGlassLayout
{
    public const float Pad = 8f;
    public const float TopShare = 0.52f;
    public const float CaptionTopShare = 0.46f;
    public const float HeroShare = 0.86f;
    public const float Gap = 8f;
    public const float JackpotPadX = 10f;
    public const float JackpotPadY = 4f;
    public const float JackpotMaxShare = 0.46f;
    public const float CaptionGap = 3f;
    public const float PillMaxHeight = 28f;

    public readonly Rect Top;
    public readonly Rect Hero;
    public readonly Rect Sign;
    public readonly Rect Jackpot;
    public readonly Rect Caption;
    public readonly Rect Content;

    private MachineGlassLayout(Rect top, Rect hero, Rect sign, Rect jackpot, Rect caption, Rect content)
    {
        Top = top;
        Hero = hero;
        Sign = sign;
        Jackpot = jackpot;
        Caption = caption;
        Content = content;
    }

    public static MachineGlassLayout Compute(Rect glass, Vector2 jackpotContent, float captionHeight, float scale)
    {
        var pad = MathF.Min(Pad * scale, MathF.Min(glass.Width, glass.Height) * 0.1f);
        var gap = Gap * scale;
        var inner = new Rect(glass.Min + new Vector2(pad, pad),
            new Vector2(MathF.Max(glass.Min.X + pad, glass.Max.X - pad), MathF.Max(glass.Min.Y + pad, glass.Max.Y - pad)));
        var split = inner.Min.Y + inner.Height * (captionHeight > 0f ? CaptionTopShare : TopShare);
        var top = new Rect(inner.Min, new Vector2(inner.Max.X, split));
        var bottom = new Rect(new Vector2(inner.Min.X, split), inner.Max);

        var heroSize = MathF.Min(top.Height * HeroShare, top.Width * 0.2f);
        var hero = new Rect(new Vector2(top.Min.X, top.Center.Y - heroSize * 0.5f),
            new Vector2(top.Min.X + heroSize, top.Center.Y + heroSize * 0.5f));
        var jackpotWidth = MathF.Min(jackpotContent.X + JackpotPadX * 2f * scale, top.Width * JackpotMaxShare);
        var jackpotHeight = MathF.Min(top.Height, jackpotContent.Y + JackpotPadY * 2f * scale);
        var jackpot = new Rect(new Vector2(top.Max.X - jackpotWidth, top.Center.Y - jackpotHeight * 0.5f),
            new Vector2(top.Max.X, top.Center.Y + jackpotHeight * 0.5f));
        var signLeft = hero.Max.X + gap;
        var signRight = MathF.Max(signLeft, jackpot.Min.X - gap);
        var sign = new Rect(new Vector2(signLeft, top.Min.Y), new Vector2(signRight, top.Max.Y));

        var caption = new Rect(bottom.Min, new Vector2(bottom.Max.X, bottom.Min.Y));
        var contentTop = bottom.Min.Y + gap * 0.5f;
        if (captionHeight > 0f)
        {
            caption = new Rect(new Vector2(bottom.Min.X, contentTop),
                new Vector2(bottom.Max.X, MathF.Min(bottom.Max.Y, contentTop + captionHeight)));
            contentTop = MathF.Min(bottom.Max.Y, caption.Max.Y + CaptionGap * scale);
        }

        var content = new Rect(new Vector2(bottom.Min.X, contentTop), bottom.Max);
        return new MachineGlassLayout(top, hero, sign, jackpot, caption, content);
    }

    public static Rect Pill(Rect content, int index, int count, float scale)
    {
        var gap = Gap * scale * 0.75f;
        var width = MathF.Max(0f, (content.Width - gap * (count - 1)) / count);
        var height = MathF.Min(content.Height, PillMaxHeight * scale);
        var left = content.Min.X + index * (width + gap);
        return new Rect(new Vector2(left, content.Center.Y - height * 0.5f),
            new Vector2(left + width, content.Center.Y + height * 0.5f));
    }

    public static int ActiveRung(int[] ladder, int multiplier)
    {
        var active = 0;
        for (var rung = 0; rung < ladder.Length; rung++)
        {
            if (ladder[rung] <= multiplier)
            {
                active = rung;
            }
        }

        return active;
    }
}
