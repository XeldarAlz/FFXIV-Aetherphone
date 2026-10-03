namespace Aetherphone.Core.Video;

internal readonly record struct ScreenPose(Vector3 Position, float Yaw, float Pitch, float Roll, float Scale);

internal static class ScreenGeometry
{
    internal const float HalfWidth = 1.0f;
    internal const float HalfHeight = 0.6f;

    internal static Quaternion Rotation(in ScreenPose pose) =>
        Quaternion.CreateFromYawPitchRoll(pose.Yaw, pose.Pitch, pose.Roll);

    internal static Vector3 Right(in ScreenPose pose) => Vector3.Transform(Vector3.UnitX, Rotation(pose));

    internal static Vector3 Up(in ScreenPose pose) => Vector3.Transform(Vector3.UnitY, Rotation(pose));

    internal static Vector3 Facing(in ScreenPose pose) => Vector3.Transform(Vector3.UnitZ, Rotation(pose));

    internal static Vector3 Nudge(in ScreenPose pose, float right, float up, float toward)
    {
        var rotation = Rotation(pose);
        return pose.Position
            + Vector3.Transform(Vector3.UnitX, rotation) * right
            + Vector3.Transform(Vector3.UnitY, rotation) * up
            + Vector3.Transform(Vector3.UnitZ, rotation) * toward;
    }

    internal static Vector3 Corner(in ScreenPose pose, float horizontal, float vertical, float curveDepth = 0f)
    {
        var rotation = Rotation(pose);
        return pose.Position
            + Vector3.Transform(Vector3.UnitX, rotation) * (horizontal * HalfWidth * pose.Scale)
            + Vector3.Transform(Vector3.UnitY, rotation) * (vertical * HalfHeight * pose.Scale)
            + Vector3.Transform(Vector3.UnitZ, rotation) * (curveDepth * horizontal * horizontal * pose.Scale);
    }

    internal static float YawFacing(Vector3 screenPosition, Vector3 viewerPosition)
    {
        var toViewer = viewerPosition - screenPosition;
        return MathF.Atan2(toViewer.X, toViewer.Z);
    }

    internal static float AlongAxis(Vector2 pointerDelta, Vector2 axisStart, Vector2 axisEnd, float axisWorldLength)
    {
        var axis = axisEnd - axisStart;
        var lengthSquared = axis.LengthSquared();
        if (lengthSquared < 1f)
        {
            return 0f;
        }

        return Vector2.Dot(pointerDelta, axis) / lengthSquared * axisWorldLength;
    }
}

internal static class SpatialVolume
{
    internal const float DefaultRange = 30f;
    internal const float MinRange = 8f;
    internal const float MaxRange = 80f;

    internal const float FullVolumeShare = 0.25f;

    internal static float Gain(float distance, float range)
    {
        var reach = Math.Clamp(range, MinRange, MaxRange);
        var near = reach * FullVolumeShare;
        if (distance <= near)
        {
            return 1f;
        }

        if (distance >= reach)
        {
            return 0f;
        }

        var fade = 1f - (distance - near) / (reach - near);
        return fade * fade;
    }
}

internal static class ScreenPlaces
{
    internal const int MaxRemembered = 40;
    internal const float ReuseDistance = 60f;

    internal static string Key(uint territoryId, uint worldId, short ward, short plot, short room) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{territoryId}:{worldId}:{ward}:{plot}:{room}");
}
