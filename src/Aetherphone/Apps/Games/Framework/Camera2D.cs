using Aetherphone.Core;
using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Framework;

internal enum FitMode : byte
{
    Contain,
    CoverWidth,
    CoverHeight,
}

internal struct Camera2D
{
    private const float PunchDecaySeconds = 0.25f;
    private const float MaxPunch = 0.25f;
    private const float TraumaDecay = 1.7f;
    private const float MaxShakePixels = 11f;

    public Vector2 Origin;
    public float Zoom;
    public Vector2 Anchor;
    public Rect View;
    private Spring leadX;
    private Spring leadY;
    private GameRandom random;
    private Vector2 shake;
    private float punch;
    private float trauma;
    private bool placed;

    public static Camera2D Create()
    {
        var camera = default(Camera2D);
        camera.Zoom = 1f;
        camera.random = GameRandom.Fresh();
        return camera;
    }

    public readonly float EffectiveZoom => Zoom * (1f + punch);

    public readonly Vector2 ShakeOffset => shake;

    public readonly float Trauma => trauma;

    public readonly bool Placed => placed;

    public void Fit(Rect view, float worldWidth, float worldHeight, FitMode mode)
    {
        Zoom = ZoomFor(view, worldWidth, worldHeight, mode);
        if (placed)
        {
            return;
        }

        Place(new Vector2(worldWidth * 0.5f, worldHeight * 0.5f));
    }

    public void Fit(Rect view, Rect world, FitMode mode)
    {
        Zoom = ZoomFor(view, world.Width, world.Height, mode);
        Place(world.Center);
    }

    private float ZoomFor(Rect view, float worldWidth, float worldHeight, FitMode mode)
    {
        View = view;
        Anchor = view.Center;
        var safeWidth = MathF.Max(0.0001f, worldWidth);
        var safeHeight = MathF.Max(0.0001f, worldHeight);
        return mode switch
        {
            FitMode.CoverWidth => view.Width / safeWidth,
            FitMode.CoverHeight => view.Height / safeHeight,
            _ => MathF.Min(view.Width / safeWidth, view.Height / safeHeight),
        };
    }

    public void Place(Vector2 origin)
    {
        Origin = origin;
        leadX.SnapTo(origin.X);
        leadY.SnapTo(origin.Y);
        placed = true;
    }

    public void Follow(Vector2 target, Vector2 lead, float smoothTime, float deltaSeconds)
    {
        var goal = target + lead;
        if (!placed)
        {
            Place(goal);
            return;
        }

        if (deltaSeconds <= 0f)
        {
            return;
        }

        Origin = new Vector2(leadX.Step(goal.X, smoothTime, deltaSeconds), leadY.Step(goal.Y, smoothTime, deltaSeconds));
    }

    public void Punch(float amount)
    {
        punch = MathF.Min(MaxPunch, punch + MathF.Max(0f, amount));
    }

    public void Shake(float amount)
    {
        trauma = MathF.Min(1f, trauma + MathF.Max(0f, amount));
    }

    public void Update(float deltaSeconds, float pixelScale)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        punch = MathF.Max(0f, punch - punch * MathF.Min(1f, deltaSeconds / PunchDecaySeconds) - 0.002f * deltaSeconds);
        trauma = MathF.Max(0f, trauma - TraumaDecay * deltaSeconds);
        if (trauma <= 0f)
        {
            shake = Vector2.Zero;
            return;
        }

        var magnitude = trauma * trauma * MaxShakePixels * pixelScale;
        shake = new Vector2((random.NextFloat() * 2f - 1f) * magnitude, (random.NextFloat() * 2f - 1f) * magnitude);
    }

    public readonly Vector2 ToScreen(Vector2 world) => Anchor + (world - Origin) * EffectiveZoom + shake;

    public readonly Vector2 ToWorld(Vector2 screen) => Origin + (screen - shake - Anchor) / EffectiveZoom;

    public readonly float Px(float worldLength) => worldLength * EffectiveZoom;

    public readonly float Units(float pixels) => pixels / EffectiveZoom;

    public readonly Rect VisibleWorld => new(ToWorld(View.Min), ToWorld(View.Max));
}
