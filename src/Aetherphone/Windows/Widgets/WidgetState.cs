using System.Runtime.InteropServices;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Home;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal sealed class WidgetStates<T> where T : class, new()
{
    private readonly Dictionary<string, T> states = new(StringComparer.Ordinal);

    public T For(in WidgetContext context) => For(context.InstanceKey);

    public T For(string instanceKey)
    {
        if (states.TryGetValue(instanceKey, out var state))
        {
            return state;
        }

        state = new T();
        states[instanceKey] = state;
        return state;
    }
}

internal static class WidgetCaches
{
    public const int Limit = 128;

    public static ref TValue Slot<TKey, TValue>(Dictionary<TKey, TValue> cache, TKey key) where TKey : notnull
    {
        if (cache.Count >= Limit && !cache.ContainsKey(key))
        {
            cache.Clear();
        }

        return ref CollectionsMarshal.GetValueRefOrAddDefault(cache, key, out _)!;
    }
}

internal struct WidgetFrame
{
    private int frame;
    private bool started;

    public bool First()
    {
        var current = ImGui.GetFrameCount();
        if (started && frame == current)
        {
            return false;
        }

        started = true;
        frame = current;
        return true;
    }

    public float Delta(float delta) => First() ? MathF.Max(0f, delta) : 0f;
}

internal struct WidgetRefresh
{
    private long dueAt;

    public bool Due(int intervalMilliseconds)
    {
        var now = Environment.TickCount64;
        if (now < dueAt)
        {
            return false;
        }

        dueAt = now + intervalMilliseconds;
        return true;
    }

    public void Expire() => dueAt = 0;
}

internal struct WidgetEase
{
    private const float SmoothSeconds = 0.32f;
    private const float MaximumStep = 0.1f;

    private Spring spring;
    private WidgetFrame frame;
    private bool started;

    public float Value => spring.Value;

    public float Step(float target, float delta, bool animate)
    {
        if (!frame.First())
        {
            return spring.Value;
        }

        if (!animate)
        {
            started = true;
            spring.SnapTo(target);
            return target;
        }

        if (!started)
        {
            started = true;
            spring.SnapTo(0f);
        }

        return spring.Step(target, SmoothSeconds, MathF.Min(MathF.Max(0f, delta), MaximumStep));
    }

    public float Fraction(float target, float delta, bool animate) =>
        Math.Clamp(Step(Math.Clamp(target, 0f, 1f), delta, animate), 0f, 1f);
}
