using Aetherphone.Core;

namespace Aetherphone.Apps.Casino;

internal readonly record struct TableCardLayout(
    Rect Art,
    Vector2 Avatar,
    float AvatarRadius,
    float TextLeft,
    float NameRight,
    float LineRight,
    float NameTop,
    float LineTop,
    float RowTop,
    float ReputationTop,
    Rect Phase,
    Vector2 Chevron)
{
    public const float Pad = 14f;
    public const float ArtSize = 52f;
    public const float AvatarUnits = 11f;
    public const float AvatarOverhang = 4f;
    public const float TextGap = 12f;
    public const float LineGap = 3f;
    public const float ChevronUnits = 12f;
    public const float PhaseGap = 8f;
    public const float MinHeight = 84f;

    public static float Height(float scale, float headline, float footnote, float rowHeight, float reputation) =>
        MathF.Max(MinHeight * scale,
            Pad * 2f * scale + headline + footnote + rowHeight + LineGap * 2f * scale + reputation);

    public static TableCardLayout Compute(in Rect card, float scale, float headline, float footnote,
        float rowHeight, float phaseWidth, float phaseHeight)
    {
        var pad = Pad * scale;
        var art = ArtSize * scale;
        var artMin = new Vector2(card.Min.X + pad, card.Min.Y + pad);
        var artRect = new Rect(artMin, artMin + new Vector2(art, art));
        var avatarRadius = AvatarUnits * scale;
        var overhang = AvatarOverhang * scale;
        var avatar = new Vector2(artRect.Max.X + overhang - avatarRadius, artRect.Max.Y + overhang - avatarRadius);
        var chevron = new Vector2(card.Max.X - pad - ChevronUnits * 0.5f * scale, card.Center.Y);
        var lineRight = chevron.X - ChevronUnits * 0.5f * scale - PhaseGap * scale;
        var phaseMax = new Vector2(lineRight, card.Min.Y + pad + phaseHeight);
        var phase = new Rect(new Vector2(phaseMax.X - phaseWidth, card.Min.Y + pad), phaseMax);
        var textLeft = artRect.Max.X + overhang + TextGap * scale;
        var nameTop = card.Min.Y + pad;
        var lineTop = nameTop + headline + LineGap * scale;
        var rowTop = lineTop + footnote + LineGap * scale;
        return new TableCardLayout(artRect, avatar, avatarRadius, textLeft, phase.Min.X - PhaseGap * scale,
            lineRight, nameTop, lineTop, rowTop, rowTop + rowHeight, phase, chevron);
    }
}
