using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal sealed class ParticleSystem
{
    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Gravity;
        public float Drag;
        public float Spin;
        public float Rotation;
        public Vector4 Color;
        public bool Square;
    }

    private readonly Particle[] particles;
    private readonly Random random = new();
    private int active;

    public ParticleSystem(int capacity = 512)
    {
        particles = new Particle[capacity];
    }

    public int ActiveCount => active;

    public void Clear()
    {
        active = 0;
    }

    public void Burst(Vector2 origin, int count, Vector4 color, float speed, float size, float life, float gravity = 360f, float spread = MathF.PI * 2f, float direction = 0f, bool square = false)
    {
        for (var index = 0; index < count; index++)
        {
            if (active >= particles.Length)
            {
                return;
            }

            var angle = direction + ((float)random.NextDouble() - 0.5f) * spread;
            var velocityScale = 0.45f + (float)random.NextDouble() * 0.55f;
            var lifeScale = 0.7f + (float)random.NextDouble() * 0.6f;

            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * velocityScale;
            particle.MaxLife = life * lifeScale;
            particle.Life = particle.MaxLife;
            particle.Size = size * (0.7f + (float)random.NextDouble() * 0.6f);
            particle.Gravity = gravity;
            particle.Drag = 1.6f;
            particle.Spin = ((float)random.NextDouble() - 0.5f) * 12f;
            particle.Rotation = (float)random.NextDouble() * MathF.PI * 2f;
            particle.Color = color;
            particle.Square = square;
            active++;
        }
    }

    public void Confetti(Vector2 origin, int count, ReadOnlySpan<Vector4> palette, float speed, float size, float life)
    {
        for (var index = 0; index < count; index++)
        {
            if (active >= particles.Length)
            {
                return;
            }

            var color = palette.Length > 0 ? palette[random.Next(palette.Length)] : new Vector4(1f, 1f, 1f, 1f);
            var angle = -MathF.PI * 0.5f + ((float)random.NextDouble() - 0.5f) * 1.4f;
            var velocityScale = 0.5f + (float)random.NextDouble() * 0.9f;

            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * velocityScale;
            particle.MaxLife = life * (0.7f + (float)random.NextDouble() * 0.7f);
            particle.Life = particle.MaxLife;
            particle.Size = size * (0.7f + (float)random.NextDouble() * 0.7f);
            particle.Gravity = 540f;
            particle.Drag = 0.7f;
            particle.Spin = ((float)random.NextDouble() - 0.5f) * 16f;
            particle.Rotation = (float)random.NextDouble() * MathF.PI * 2f;
            particle.Color = color;
            particle.Square = true;
            active++;
        }
    }

    public void Update(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        for (var index = active - 1; index >= 0; index--)
        {
            ref var particle = ref particles[index];
            particle.Life -= deltaSeconds;
            if (particle.Life <= 0f)
            {
                particles[index] = particles[active - 1];
                active--;
                continue;
            }

            particle.Velocity.Y += particle.Gravity * deltaSeconds;
            particle.Velocity *= MathF.Max(0f, 1f - particle.Drag * deltaSeconds);
            particle.Position += particle.Velocity * deltaSeconds;
            particle.Rotation += particle.Spin * deltaSeconds;
        }
    }

    public void Draw(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < active; index++)
        {
            ref readonly var particle = ref particles[index];
            var fade = particle.Life / particle.MaxLife;
            var alpha = fade > 0.7f ? 1f : fade / 0.7f;
            var color = ImGui.GetColorU32(particle.Color with { W = particle.Color.W * alpha });
            var radius = particle.Size * scale * (0.4f + 0.6f * fade);

            if (particle.Square)
            {
                var right = new Vector2(MathF.Cos(particle.Rotation), MathF.Sin(particle.Rotation)) * radius;
                var up = new Vector2(-right.Y, right.X);
                drawList.AddQuadFilled(
                    particle.Position - right - up,
                    particle.Position + right - up,
                    particle.Position + right + up,
                    particle.Position - right + up,
                    color);
            }
            else
            {
                drawList.AddCircleFilled(particle.Position, radius, color);
            }
        }
    }
}
