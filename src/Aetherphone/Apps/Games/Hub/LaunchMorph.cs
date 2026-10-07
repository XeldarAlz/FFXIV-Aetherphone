using Aetherphone.Core;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Hub;

internal enum LaunchStep : byte
{
    Idle,
    Moving,
    Presented,
    Dismissed,
}

internal struct LaunchMorph
{
    public const float VeilDim = 0.35f;
    private const float IconAspectTolerance = 0.12f;
    private const float IconFadeStart = 0.06f;
    private const float IconFadeEnd = 0.4f;
    private const float SurfaceFadeStart = 0.12f;
    private const float SurfaceFadeEnd = 0.5f;
    private const float CornerInsetFactor = 0.29289322f;
    private const float MinimumExtent = 1f;

    private Spring spring;
    private Rect source;
    private bool moving;
    private bool dismissing;

    public readonly bool HasSource => source.Width > 0f && source.Height > 0f;

    public readonly bool Active => moving;

    public readonly bool Dismissing => moving && dismissing;

    public readonly float Progress => spring.Value;

    public readonly Rect Source => source;

    public readonly bool FromIcon => IsIcon(source);

    public void Begin(Rect localSource)
    {
        source = localSource;
        moving = true;
        dismissing = false;
        spring.Launch(0f, TransitionTiming.LaunchVelocity(TransitionTiming.ZoomPresentSmoothTime));
    }

    public bool BeginDismiss()
    {
        if (!HasSource)
        {
            return false;
        }

        var value = spring.Value;
        var kick = -TransitionTiming.LaunchVelocity(TransitionTiming.ZoomDismissSmoothTime) * value;
        moving = true;
        dismissing = true;
        spring.Launch(value, MathF.Min(spring.Velocity, kick));
        return true;
    }

    public LaunchStep Advance(float deltaSeconds)
    {
        if (!moving)
        {
            return LaunchStep.Idle;
        }

        var target = dismissing ? 0f : 1f;
        var smoothTime = dismissing ? TransitionTiming.ZoomDismissSmoothTime : TransitionTiming.ZoomPresentSmoothTime;
        spring.Step(target, smoothTime, MathF.Min(deltaSeconds, TransitionTiming.MotionFrameSeconds));
        if (!spring.IsResting(target, TransitionTiming.RestPositionEpsilon, TransitionTiming.RestVelocityEpsilon))
        {
            return LaunchStep.Moving;
        }

        spring.SnapTo(target);
        moving = false;
        if (!dismissing)
        {
            return LaunchStep.Presented;
        }

        Clear();
        return LaunchStep.Dismissed;
    }

    public void Clear() => this = default;

    public static bool IsIcon(Rect source) =>
        source.Height > 0f && MathF.Abs(source.Width / source.Height - 1f) <= IconAspectTolerance;

    public static Rect Card(Rect source, Rect area, float progress) =>
        new(Vector2.Lerp(source.Min, area.Min, progress), Vector2.Lerp(source.Max, area.Max, progress));

    public static Rect Clip(Rect source, Rect area, float progress, bool icon)
    {
        var corner = icon
            ? MathF.Min(source.Width, source.Height) * HubMetrics.IconRadiusFactor * CornerInsetFactor
            : 0f;
        return Card(source.Inset(corner), area, progress);
    }

    public static LayerTransform Transform(Rect source, Rect area, float progress, bool icon)
    {
        var start = MathF.Max(source.Width / MathF.Max(area.Width, MinimumExtent),
            source.Height / MathF.Max(area.Height, MinimumExtent));
        var scale = Easing.Lerp(start, 1f, progress);
        var target = Vector2.Lerp(source.Center, area.Center, progress);
        return new LayerTransform(scale, area.Center, target, Clip(source, area, progress, icon),
            ContentAlpha(progress, icon));
    }

    public static float ContentAlpha(float progress, bool icon) =>
        icon ? 1f : Easing.Segment(progress, SurfaceFadeStart, SurfaceFadeEnd);

    public static float IconAlpha(float progress) => 1f - Easing.Segment(progress, IconFadeStart, IconFadeEnd);

    public static float Veil(float progress) => VeilDim * Easing.Clamp01(progress);
}
