namespace Aetherphone.Core.Theme;

internal readonly struct CaseFinish
{
    private const float LightCaseLuminance = 0.5f;

    public readonly Vector4 Frame;
    public readonly Vector4 Rail;
    public readonly Vector4 Rim;
    public readonly Vector4 EdgeBright;
    public readonly Vector4 EdgeDim;

    public CaseFinish(Vector4 frame)
    {
        Frame = frame;
        Rail = Palette.Lighten(frame, 0.035f);
        Rim = Palette.Luminance(frame) > LightCaseLuminance
            ? Palette.WithAlpha(Palette.Darken(frame, 0.34f), 0.55f)
            : Palette.WithAlpha(Palette.Lighten(frame, 0.45f), 0.55f);
        EdgeBright = Palette.WithAlpha(Palette.Lighten(frame, 0.84f), 0.85f);
        EdgeDim = Palette.WithAlpha(Palette.Darken(frame, 0.92f), 0.35f);
    }
}
