using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Trailblaze;

internal readonly struct TrailblazeView
{
    public const float LaneWidth = 2.2f;
    public const float NearZ = 2.5f;
    public const float FarZ = 75f;
    public const float PlayerDepth = 5f;
    public const float HorizonFraction = 0.4f;
    public const float RoadWidthFraction = 1.55f;
    public const float SpeedWidening = 0.18f;
    private const float FadeStart = 0.78f;

    public readonly LaneProjection Road;
    public readonly Rect Area;
    public readonly float Distance;
    public readonly float CameraOffset;
    public readonly float Bend;
    public readonly float Zoom;
    public readonly Vector2 Pivot;
    public readonly Vector2 Offset;
    private readonly float cosine;
    private readonly float sine;

    public TrailblazeView(Rect area, float distance, float cameraOffset, float bend, float roll, float zoom,
        Vector2 offset, float speedFraction)
    {
        Area = area;
        Road = LaneProjection.Fit(area, TrailblazeBoard.LaneCount, LaneWidth, HorizonFraction,
            RoadWidthFraction + SpeedWidening * speedFraction, NearZ, FarZ);
        Distance = distance;
        CameraOffset = cameraOffset;
        Bend = bend;
        Zoom = zoom;
        Pivot = new Vector2(area.Center.X, Road.BottomY - (Road.BottomY - Road.HorizonY) * 0.5f);
        Offset = offset;
        cosine = MathF.Cos(roll);
        sine = MathF.Sin(roll);
    }

    public float WorldZ(float depth) => Distance - PlayerDepth + depth;

    public float Depth(float worldZ) => worldZ - Distance + PlayerDepth;

    public float NearWorldZ => WorldZ(NearZ * 0.55f);

    public float FarWorldZ => WorldZ(FarZ);

    public float Scale(float depth) => Road.Scale(depth) * Zoom;

    public float PlayerScale => Scale(PlayerDepth);

    public float Fog(float depth) => Road.Fog(depth);

    public float Fade(float depth)
    {
        var far = Math.Clamp((depth / FarZ - FadeStart) / (1f - FadeStart), 0f, 1f);
        return 1f - far * far;
    }

    public bool Visible(float depth) => depth > NearZ * 0.35f && depth < FarZ;

    public Vector2 At(float laneOffset, float height, float worldZ) =>
        Transform(Raw(laneOffset, height, Depth(worldZ)));

    public Vector2 AtDepth(float laneOffset, float height, float depth) => Transform(Raw(laneOffset, height, depth));

    public Vector2 Horizon(float screenX) => Transform(new Vector2(screenX, Road.HorizonY));

    public Vector2 VanishingPoint => Transform(new Vector2(Road.VanishingX + Bend, Road.HorizonY));

    public float BendAt(float depth)
    {
        var progress = Math.Clamp((depth - PlayerDepth) / (FarZ - PlayerDepth), 0f, 1f);
        return Bend * progress * progress;
    }

    public Vector2 Transform(Vector2 point)
    {
        var local = (point - Pivot) * Zoom;
        return Pivot + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine) + Offset;
    }

    public float Roll => MathF.Atan2(sine, cosine);

    private Vector2 Raw(float laneOffset, float height, float depth)
    {
        var point = Road.ToScreen(laneOffset - CameraOffset, height, depth);
        return new Vector2(point.X + BendAt(depth), point.Y);
    }
}
