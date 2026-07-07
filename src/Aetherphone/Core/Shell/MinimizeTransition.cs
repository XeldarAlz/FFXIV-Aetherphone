using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal enum MinimizePhase : byte
{
    None,
    Collapsing,
    Minimized,
    Expanding,
}

internal sealed class MinimizeTransition
{
    public static readonly Vector2 MinimizedSize = new(78f, 152f);

    private const float CollapseSmoothTime = 0.26f;
    private const float ExpandSmoothTime = 0.23f;

    private Spring progress;
    private MinimizePhase phase = MinimizePhase.None;

    public MinimizePhase Phase => phase;
    public bool MorphActive => phase is MinimizePhase.Collapsing or MinimizePhase.Expanding;
    public bool MinimizedResting => phase == MinimizePhase.Minimized;

    public void BeginCollapse()
    {
        if (phase is MinimizePhase.None or MinimizePhase.Expanding)
        {
            phase = MinimizePhase.Collapsing;
        }
    }

    public void BeginExpand()
    {
        if (phase is MinimizePhase.Minimized or MinimizePhase.Collapsing)
        {
            phase = MinimizePhase.Expanding;
        }
    }

    public void SnapFull()
    {
        phase = MinimizePhase.None;
        progress.SnapTo(0f);
    }

    public void SnapMinimized()
    {
        phase = MinimizePhase.Minimized;
        progress.SnapTo(1f);
    }

    public void Advance(float delta)
    {
        switch (phase)
        {
            case MinimizePhase.None:
                progress.SnapTo(0f);
                break;
            case MinimizePhase.Minimized:
                progress.SnapTo(1f);
                break;
            case MinimizePhase.Collapsing:
                progress.Step(1f, CollapseSmoothTime, delta);
                if (progress.IsResting(1f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
                {
                    progress.SnapTo(1f);
                    phase = MinimizePhase.Minimized;
                }

                break;
            case MinimizePhase.Expanding:
                progress.Step(0f, ExpandSmoothTime, delta);
                if (progress.IsResting(0f, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
                {
                    progress.SnapTo(0f);
                    phase = MinimizePhase.None;
                }

                break;
        }
    }

    public void DrawMorph(ImDrawListPtr dl, Rect startBody, Rect endBody, PhoneTheme theme, float scale, int unread)
    {
        var eased = Easing.EaseInOutCubic(Math.Clamp(progress.Value, 0f, 1f));
        var body = new Rect(Vector2.Lerp(startBody.Min, endBody.Min, eased),
            Vector2.Lerp(startBody.Max, endBody.Max, eased));
        var bezel = Lerp(theme.BezelThickness * scale, endBody.Width * 0.09f, eased);
        var rounding = Lerp(theme.DeviceRounding * scale, endBody.Width * 0.30f, eased);
        var geometry = MinimizedPhone.Geometry.Lerp(body, bezel, rounding);
        Elevation.Floating(dl, body.Min, body.Max, rounding, scale, eased);
        MinimizedPhone.DrawShell(dl, geometry, theme);
        var raw = Math.Clamp((eased - 0.5f) / 0.4f, 0f, 1f);
        var glyphAlpha = raw * raw * (3f - 2f * raw);
        MinimizedPhone.DrawFace(dl, geometry, theme, scale, glyphAlpha, unread);
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}
