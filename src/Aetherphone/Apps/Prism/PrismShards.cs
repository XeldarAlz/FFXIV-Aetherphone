using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed class PrismShards
{
    private const int Capacity = 12;
    private const float Drag = 4f;
    private const float Stagger = 0.012f;
    private const float LengthUnits = 11f;
    private const float WidthUnits = 2.6f;
    private const float Shrink = 0.4f;
    private const float Peak = 0.85f;

    private readonly Shard[] shards = new Shard[Capacity];
    private int next;

    private struct Shard
    {
        public Vector2 Origin;
        public Vector2 Direction;
        public float Speed;
        public float Age;
        public float Life;
        public int Hue;
    }

    public bool Active
    {
        get
        {
            for (var index = 0; index < Capacity; index++)
            {
                if (shards[index].Life > 0f && shards[index].Age < shards[index].Life)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void Burst(Vector2 origin, float angle, float spread, float speed, float life)
    {
        var count = PrismLight.Hues.Length;
        for (var index = 0; index < count; index++)
        {
            var turn = angle + spread * (index / (float)(count - 1) - 0.5f);
            shards[next] = new Shard
            {
                Origin = origin,
                Direction = new Vector2(MathF.Cos(turn), MathF.Sin(turn)),
                Speed = speed,
                Age = -index * Stagger,
                Life = life,
                Hue = index,
            };
            next = (next + 1) % Capacity;
        }
    }

    public void Draw(ImDrawListPtr drawList, Rect clip, float delta, float scale)
    {
        var clipped = false;
        for (var index = 0; index < Capacity; index++)
        {
            ref var shard = ref shards[index];
            if (shard.Life <= 0f || shard.Age >= shard.Life)
            {
                continue;
            }

            shard.Age += delta;
            if (shard.Age <= 0f || shard.Age >= shard.Life)
            {
                continue;
            }

            if (!clipped)
            {
                drawList.PushClipRect(clip.Min, clip.Max, true);
                clipped = true;
            }

            var travel = shard.Speed * scale * (1f - MathF.Exp(-Drag * shard.Age)) / Drag;
            var center = shard.Origin + shard.Direction * travel;
            var share = shard.Age / shard.Life;
            var along = shard.Direction * (LengthUnits * scale * (1f - Shrink * share) * 0.5f);
            var across = new Vector2(-shard.Direction.Y, shard.Direction.X) * (WidthUnits * scale * 0.5f);
            var fade = (1f - share) * (1f - share);
            drawList.AddQuadFilled(center + along, center + across, center - along, center - across,
                Palette.Pack(PrismLight.Hues[shard.Hue] with { W = Peak * fade }));
        }

        if (clipped)
        {
            drawList.PopClipRect();
        }
    }
}
