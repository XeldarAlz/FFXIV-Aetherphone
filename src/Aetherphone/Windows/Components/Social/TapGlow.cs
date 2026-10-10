using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class TapGlow
{
    public const int Like = 0;
    public const int Comment = 1;
    public const int Share = 2;
    public const int Save = 3;
    public const int Repost = 4;
    public const int React = 5;

    private const int SlotCount = 8;
    private const int GlowCells = 8;
    private const float Settled = 0.97f;
    private const float HoverReach = 2.2f;
    private const float HoverAlpha = 0.16f;
    private const float BloomReach = 1.8f;
    private const float BloomAlpha = 0.55f;
    private const float RingReach = 1.5f;
    private const float RingAlpha = 0.5f;
    private const float SnuffReach = 1.2f;
    private const float SnuffAlpha = 0.3f;

    private struct Flare
    {
        public int Key;
        public double Start;
        public bool Snuff;
        public bool Active;
    }

    private static readonly Flare[] Flares = new Flare[SlotCount];
    private static bool enabled;
    private static Vector4 ink;

    public static Scope Use(bool on, Vector4 color)
    {
        var scope = new Scope(enabled, ink);
        enabled = on;
        ink = color;
        return scope;
    }

    public static int Key(string id, int action) => HashCode.Combine(id, action);

    public static void Bloom(int key) => Start(key, false);

    public static void Toggle(int key, bool wasOn) => Start(key, wasOn);

    public static void Draw(ImDrawListPtr drawList, int key, Vector2 center, float radius, bool hovered)
    {
        if (!enabled)
        {
            return;
        }

        if (hovered)
        {
            NightScene.Glow(drawList, center, radius * HoverReach, ink with { W = ink.W * HoverAlpha }, GlowCells);
        }

        var now = ImGui.GetTime();
        for (var slot = 0; slot < Flares.Length; slot++)
        {
            ref var flare = ref Flares[slot];
            if (!flare.Active || flare.Key != key)
            {
                continue;
            }

            var progress = Spring.Settle((float)(now - flare.Start), Motion.Release);
            if (progress >= Settled)
            {
                flare.Active = false;
                continue;
            }

            var fade = 1f - progress;
            if (flare.Snuff)
            {
                NightScene.Glow(drawList, center, radius * (1f + SnuffReach * fade),
                    ink with { W = ink.W * SnuffAlpha * fade }, GlowCells);
                continue;
            }

            NightScene.Glow(drawList, center, radius * (1f + BloomReach * progress),
                ink with { W = ink.W * BloomAlpha * fade }, GlowCells);
            drawList.AddCircle(center, radius * (1f + RingReach * progress),
                ImGui.GetColorU32(ink with { W = ink.W * RingAlpha * fade }), 32, MathF.Max(1f, UiScale.Current));
        }
    }

    private static void Start(int key, bool snuff)
    {
        if (!enabled)
        {
            return;
        }

        Flares[SlotFor(key)] = new Flare { Key = key, Start = ImGui.GetTime(), Snuff = snuff, Active = true };
    }

    private static int SlotFor(int key)
    {
        var free = -1;
        var oldest = 0;
        for (var slot = 0; slot < Flares.Length; slot++)
        {
            if (Flares[slot].Active && Flares[slot].Key == key)
            {
                return slot;
            }

            if (!Flares[slot].Active && free < 0)
            {
                free = slot;
            }

            if (Flares[slot].Start < Flares[oldest].Start)
            {
                oldest = slot;
            }
        }

        return free >= 0 ? free : oldest;
    }

    public readonly struct Scope(bool previousEnabled, Vector4 previousInk) : IDisposable
    {
        public void Dispose()
        {
            enabled = previousEnabled;
            ink = previousInk;
        }
    }
}
