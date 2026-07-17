using System.Numerics;
using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Shell.Home;

internal sealed class TilePoseCache
{
    private const float ReflowSmoothTime = 0.16f;
    private const int MaxEntries = 256;

    private struct TilePose
    {
        public Spring X;
        public Spring Y;
        public Spring W;
        public Spring H;
        public int Page;
        public bool Init;
    }

    private readonly Dictionary<string, TilePose> poses = new();

    public void Forget(string key) => poses.Remove(key);

    public Rect Resolve(string key, int page, Rect localTarget, Vector2 origin, float delta, bool animate)
    {
        if (poses.Count > MaxEntries)
        {
            poses.Clear();
        }

        var center = localTarget.Center;
        var size = localTarget.Size;
        if (!poses.TryGetValue(key, out var pose) || !pose.Init || pose.Page != page || !animate)
        {
            pose.X = new Spring(center.X);
            pose.Y = new Spring(center.Y);
            pose.W = new Spring(size.X);
            pose.H = new Spring(size.Y);
            pose.Page = page;
            pose.Init = true;
        }
        else
        {
            pose.X.Step(center.X, ReflowSmoothTime, delta);
            pose.Y.Step(center.Y, ReflowSmoothTime, delta);
            pose.W.Step(size.X, ReflowSmoothTime, delta);
            pose.H.Step(size.Y, ReflowSmoothTime, delta);
        }

        poses[key] = pose;
        var posed = new Vector2(pose.X.Value, pose.Y.Value) + origin;
        var half = new Vector2(pose.W.Value, pose.H.Value) * 0.5f;
        return new Rect(posed - half, posed + half);
    }
}
