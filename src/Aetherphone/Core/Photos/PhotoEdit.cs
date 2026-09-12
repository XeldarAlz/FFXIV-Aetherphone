namespace Aetherphone.Core.Photos;

internal enum PhotoLook : byte
{
    None,
    Warm,
    Cool,
    Vivid,
    Film,
    Fade,
    Mono,
    Noir,
}

internal enum PhotoAdjustment : byte
{
    Brightness,
    Contrast,
    Saturation,
    Warmth,
    Vignette,
    Straighten,
}

internal readonly struct PhotoEdit
{
    public const float MaxStraightenDegrees = 15f;
    public const float MaxLookStrength = 1f;
    private const int QuarterTurnCount = 4;

    public readonly int QuarterTurns;
    public readonly bool Mirrored;
    public readonly float Straighten;
    public readonly float Brightness;
    public readonly float Contrast;
    public readonly float Saturation;
    public readonly float Warmth;
    public readonly float Vignette;
    public readonly PhotoLook Look;
    public readonly float LookStrength;

    public PhotoEdit(int quarterTurns, bool mirrored, float straighten, float brightness, float contrast,
        float saturation, float warmth, float vignette, PhotoLook look, float lookStrength)
    {
        QuarterTurns = ((quarterTurns % QuarterTurnCount) + QuarterTurnCount) % QuarterTurnCount;
        Mirrored = mirrored;
        Straighten = Math.Clamp(straighten, -MaxStraightenDegrees, MaxStraightenDegrees);
        Brightness = Math.Clamp(brightness, -1f, 1f);
        Contrast = Math.Clamp(contrast, -1f, 1f);
        Saturation = Math.Clamp(saturation, -1f, 1f);
        Warmth = Math.Clamp(warmth, -1f, 1f);
        Vignette = Math.Clamp(vignette, 0f, 1f);
        Look = look;
        LookStrength = Math.Clamp(lookStrength, 0f, MaxLookStrength);
    }

    public static PhotoEdit None => new(0, false, 0f, 0f, 0f, 0f, 0f, 0f, PhotoLook.None, MaxLookStrength);

    public bool ChangesOrientation => QuarterTurns != 0 || Mirrored;

    public bool ChangesGeometry => ChangesOrientation || Straighten != 0f;

    public bool AppliesLook => Look != PhotoLook.None && LookStrength > 0f;

    public bool ChangesColor => Brightness != 0f || Contrast != 0f || Saturation != 0f || Warmth != 0f
        || Vignette != 0f || AppliesLook;

    public bool IsIdentity => !ChangesGeometry && !ChangesColor;

    public bool SameAs(in PhotoEdit other)
    {
        return QuarterTurns == other.QuarterTurns
            && Mirrored == other.Mirrored
            && Straighten == other.Straighten
            && Brightness == other.Brightness
            && Contrast == other.Contrast
            && Saturation == other.Saturation
            && Warmth == other.Warmth
            && Vignette == other.Vignette
            && Look == other.Look
            && LookStrength == other.LookStrength;
    }

    public static (float Min, float Max) RangeOf(PhotoAdjustment adjustment)
    {
        switch (adjustment)
        {
            case PhotoAdjustment.Vignette:
                return (0f, 1f);
            case PhotoAdjustment.Straighten:
                return (-MaxStraightenDegrees, MaxStraightenDegrees);
            default:
                return (-1f, 1f);
        }
    }

    public float ValueOf(PhotoAdjustment adjustment)
    {
        switch (adjustment)
        {
            case PhotoAdjustment.Contrast:
                return Contrast;
            case PhotoAdjustment.Saturation:
                return Saturation;
            case PhotoAdjustment.Warmth:
                return Warmth;
            case PhotoAdjustment.Vignette:
                return Vignette;
            case PhotoAdjustment.Straighten:
                return Straighten;
            default:
                return Brightness;
        }
    }

    public PhotoEdit With(PhotoAdjustment adjustment, float value)
    {
        switch (adjustment)
        {
            case PhotoAdjustment.Contrast:
                return new PhotoEdit(QuarterTurns, Mirrored, Straighten, Brightness, value, Saturation, Warmth,
                    Vignette, Look, LookStrength);
            case PhotoAdjustment.Saturation:
                return new PhotoEdit(QuarterTurns, Mirrored, Straighten, Brightness, Contrast, value, Warmth,
                    Vignette, Look, LookStrength);
            case PhotoAdjustment.Warmth:
                return new PhotoEdit(QuarterTurns, Mirrored, Straighten, Brightness, Contrast, Saturation, value,
                    Vignette, Look, LookStrength);
            case PhotoAdjustment.Vignette:
                return new PhotoEdit(QuarterTurns, Mirrored, Straighten, Brightness, Contrast, Saturation, Warmth,
                    value, Look, LookStrength);
            case PhotoAdjustment.Straighten:
                return new PhotoEdit(QuarterTurns, Mirrored, value, Brightness, Contrast, Saturation, Warmth,
                    Vignette, Look, LookStrength);
            default:
                return new PhotoEdit(QuarterTurns, Mirrored, Straighten, value, Contrast, Saturation, Warmth,
                    Vignette, Look, LookStrength);
        }
    }

    public PhotoEdit WithLook(PhotoLook look, float strength)
    {
        return new PhotoEdit(QuarterTurns, Mirrored, Straighten, Brightness, Contrast, Saturation, Warmth, Vignette,
            look, strength);
    }

    // A mirrored view rotates the other way, because the mirror conjugates the rotation: what the
    // user sees turning clockwise is the source turning counterclockwise before the flip.
    public PhotoEdit RotatedClockwise()
    {
        var turns = Mirrored ? QuarterTurns - 1 : QuarterTurns + 1;
        return new PhotoEdit(turns, Mirrored, Straighten, Brightness, Contrast, Saturation, Warmth, Vignette, Look,
            LookStrength);
    }

    public PhotoEdit Flipped()
    {
        return new PhotoEdit(QuarterTurns, !Mirrored, Straighten, Brightness, Contrast, Saturation, Warmth, Vignette,
            Look, LookStrength);
    }

    public PhotoEdit WithoutGeometry()
    {
        return new PhotoEdit(0, false, 0f, Brightness, Contrast, Saturation, Warmth, Vignette, Look, LookStrength);
    }
}
