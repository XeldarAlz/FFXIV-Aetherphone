using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework.World;

internal readonly struct LaneProjection
{
    private const float MinimumDepth = 0.001f;

    public readonly float HorizonY;
    public readonly float VanishingX;
    public readonly float BottomY;
    public readonly float CameraHeight;
    public readonly float NearZ;
    public readonly float FarZ;
    public readonly float LaneWidth;
    public readonly int LaneCount;
    public readonly float Focal;

    public LaneProjection(float horizonY, float vanishingX, float bottomY, float cameraHeight, float nearZ, float farZ,
        float laneWidth, int laneCount)
    {
        HorizonY = horizonY;
        VanishingX = vanishingX;
        BottomY = bottomY;
        CameraHeight = MathF.Max(cameraHeight, MinimumDepth);
        NearZ = MathF.Max(nearZ, MinimumDepth);
        FarZ = MathF.Max(farZ, NearZ + MinimumDepth);
        LaneWidth = laneWidth;
        LaneCount = Math.Max(1, laneCount);
        Focal = (bottomY - horizonY) * NearZ / CameraHeight;
    }

    public static LaneProjection Fit(Rect view, int laneCount, float laneWidth, float horizonFraction,
        float roadWidthFraction, float nearZ, float farZ)
    {
        var horizonY = view.Min.Y + view.Height * horizonFraction;
        var roadPixels = MathF.Max(1f, view.Width * roadWidthFraction);
        var cameraHeight = (view.Max.Y - horizonY) * Math.Max(1, laneCount) * laneWidth / roadPixels;
        return new LaneProjection(horizonY, view.Center.X, view.Max.Y, cameraHeight, nearZ, farZ, laneWidth, laneCount);
    }

    public float Scale(float depth) => Focal / MathF.Max(depth, MinimumDepth);

    public float LaneOffset(int lane) => lane - (LaneCount - 1) * 0.5f;

    public Vector2 ToScreen(float laneX, float height, float depth)
    {
        var scale = Scale(depth);
        return new Vector2(VanishingX + laneX * LaneWidth * scale, HorizonY + (CameraHeight - height) * scale);
    }

    public Vector2 LaneEdge(int edge, float depth) => ToScreen(edge - LaneCount * 0.5f, 0f, depth);

    public float Fog(float depth)
    {
        var amount = Math.Clamp((depth - NearZ) / (FarZ - NearZ), 0f, 1f);
        return amount * amount;
    }
}
