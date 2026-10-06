using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal enum ParticleShape : byte
{
    Circle,
    GlowCircle,
    Square,
    Star,
    Streak,
    Ring,
    Shard,
    Spark,
    Glyph,
}

internal enum SizeCurve : byte
{
    Shrink,
    Grow,
    Pulse,
}

internal readonly struct ParticleSpec
{
    public readonly Vector4 StartColor;
    public readonly Vector4 EndColor;
    public readonly float Size;
    public readonly float Speed;
    public readonly float Life;
    public readonly float Gravity;
    public readonly float Drag;
    public readonly float Spin;
    public readonly float Spread;
    public readonly float Direction;
    public readonly ParticleShape Shape;
    public readonly SizeCurve Curve;
    public readonly bool Additive;
    public readonly char Glyph;

    public ParticleSpec(Vector4 startColor, Vector4 endColor, float size, float speed, float life,
        float gravity = 0f, float drag = 1.6f, float spin = 0f, float spread = MathF.PI * 2f, float direction = 0f,
        ParticleShape shape = ParticleShape.Circle, SizeCurve curve = SizeCurve.Shrink, bool additive = false,
        char glyph = ' ')
    {
        StartColor = startColor;
        EndColor = endColor;
        Size = size;
        Speed = speed;
        Life = life;
        Gravity = gravity;
        Drag = drag;
        Spin = spin;
        Spread = spread;
        Direction = direction;
        Shape = shape;
        Curve = curve;
        Additive = additive;
        Glyph = glyph;
    }

    public ParticleSpec WithGlyph(char glyph) =>
        new(StartColor, EndColor, Size, Speed, Life, Gravity, Drag, Spin, Spread, Direction, ParticleShape.Glyph,
            Curve, Additive, glyph);

    public ParticleSpec WithDirection(float direction, float spread) =>
        new(StartColor, EndColor, Size, Speed, Life, Gravity, Drag, Spin, spread, direction, Shape, Curve, Additive,
            Glyph);
}

internal struct Emitter
{
    public ParticleSpec Spec;
    public float Rate;
    private float accumulator;

    public Emitter(in ParticleSpec spec, float rate)
    {
        Spec = spec;
        Rate = rate;
        accumulator = 0f;
    }

    public void Advance(float deltaSeconds, Vector2 position, ParticleSystem particles)
    {
        if (deltaSeconds <= 0f || Rate <= 0f)
        {
            return;
        }

        accumulator += deltaSeconds * Rate;
        var count = (int)accumulator;
        if (count <= 0)
        {
            return;
        }

        accumulator -= count;
        particles.Emit(Spec, position, count);
    }

    public void Reset()
    {
        accumulator = 0f;
    }
}

internal sealed class ParticleSystem
{
    private const float HaloScale = 2.4f;
    private const float HaloAlpha = 0.22f;
    private const int FirstGlyph = 32;
    private const int LastGlyph = 126;

    private static readonly string[] GlyphLabels = BuildGlyphLabels();

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
        public Vector4 EndColor;
        public ParticleShape Shape;
        public SizeCurve Curve;
        public bool Additive;
        public char Glyph;
    }

    private readonly Particle[] particles;
    private GameRandom random = GameRandom.Fresh();
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

    public void Reseed(ulong seed)
    {
        random = GameRandom.FromSeed(seed);
    }

    public void Burst(Vector2 origin, int count, Vector4 color, float speed, float size, float life,
        float gravity = 360f, float spread = MathF.PI * 2f, float direction = 0f,
        ParticleShape shape = ParticleShape.Circle)
    {
        for (var index = 0; index < count; index++)
        {
            if (active >= particles.Length)
            {
                return;
            }

            var angle = direction + (random.NextFloat() - 0.5f) * spread;
            var velocityScale = 0.45f + random.NextFloat() * 0.55f;
            var lifeScale = 0.7f + random.NextFloat() * 0.6f;
            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * velocityScale;
            particle.MaxLife = life * lifeScale;
            particle.Life = particle.MaxLife;
            particle.Size = size * (0.7f + random.NextFloat() * 0.6f);
            particle.Gravity = gravity;
            particle.Drag = 1.6f;
            particle.Spin = (random.NextFloat() - 0.5f) * 12f;
            particle.Rotation = random.NextFloat() * MathF.PI * 2f;
            particle.Color = color;
            particle.EndColor = color;
            particle.Shape = shape;
            particle.Curve = SizeCurve.Shrink;
            particle.Additive = false;
            particle.Glyph = ' ';
            active++;
        }
    }

    public void Sparkle(Vector2 origin, int count, Vector4 color, float speed, float size, float life)
    {
        for (var index = 0; index < count; index++)
        {
            if (active >= particles.Length)
            {
                return;
            }

            var angle = random.NextFloat() * MathF.PI * 2f;
            var velocityScale = 0.2f + random.NextFloat() * 0.8f;
            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * velocityScale;
            particle.MaxLife = life * (0.6f + random.NextFloat() * 0.8f);
            particle.Life = particle.MaxLife;
            particle.Size = size * (0.6f + random.NextFloat() * 0.8f);
            particle.Gravity = 40f;
            particle.Drag = 2.4f;
            particle.Spin = (random.NextFloat() - 0.5f) * 6f;
            particle.Rotation = random.NextFloat() * MathF.PI * 2f;
            particle.Color = color;
            particle.EndColor = color;
            particle.Shape = ParticleShape.Star;
            particle.Curve = SizeCurve.Shrink;
            particle.Additive = false;
            particle.Glyph = ' ';
            active++;
        }
    }

    public void Streaks(Vector2 origin, int count, Vector4 color, float speed, float size, float life,
        float spread = MathF.PI * 2f, float direction = 0f)
    {
        Burst(origin, count, color, speed, size, life, 220f, spread, direction, ParticleShape.Streak);
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
            var angle = -MathF.PI * 0.5f + (random.NextFloat() - 0.5f) * 1.4f;
            var velocityScale = 0.5f + random.NextFloat() * 0.9f;
            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * velocityScale;
            particle.MaxLife = life * (0.7f + random.NextFloat() * 0.7f);
            particle.Life = particle.MaxLife;
            particle.Size = size * (0.7f + random.NextFloat() * 0.7f);
            particle.Gravity = 540f;
            particle.Drag = 0.7f;
            particle.Spin = (random.NextFloat() - 0.5f) * 16f;
            particle.Rotation = random.NextFloat() * MathF.PI * 2f;
            particle.Color = color;
            particle.EndColor = color;
            particle.Shape = ParticleShape.Square;
            particle.Curve = SizeCurve.Shrink;
            particle.Additive = false;
            particle.Glyph = ' ';
            active++;
        }
    }

    public void Emit(in ParticleSpec spec, Vector2 origin, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (active >= particles.Length)
            {
                return;
            }

            var angle = spec.Direction + (random.NextFloat() - 0.5f) * spec.Spread;
            var velocityScale = 0.6f + random.NextFloat() * 0.4f;
            ref var particle = ref particles[active];
            particle.Position = origin;
            particle.Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spec.Speed * velocityScale;
            particle.MaxLife = spec.Life * (0.75f + random.NextFloat() * 0.5f);
            particle.Life = particle.MaxLife;
            particle.Size = spec.Size * (0.8f + random.NextFloat() * 0.4f);
            particle.Gravity = spec.Gravity;
            particle.Drag = spec.Drag;
            particle.Spin = spec.Spin * (random.NextFloat() - 0.5f) * 2f;
            particle.Rotation = random.NextFloat() * MathF.PI * 2f;
            particle.Color = spec.StartColor;
            particle.EndColor = spec.EndColor;
            particle.Shape = spec.Shape;
            particle.Curve = spec.Curve;
            particle.Additive = spec.Additive;
            particle.Glyph = spec.Glyph;
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
            DrawOne(drawList, particle, particle.Position, particle.Velocity, scale);
        }
    }

    public void Draw(ImDrawListPtr drawList, in Camera2D camera, float baseZoom = 1f)
    {
        var sizeScale = camera.EffectiveZoom / MathF.Max(0.0001f, baseZoom);
        for (var index = 0; index < active; index++)
        {
            ref readonly var particle = ref particles[index];
            DrawOne(drawList, particle, camera.ToScreen(particle.Position), particle.Velocity * sizeScale, sizeScale);
        }
    }

    private static void DrawOne(ImDrawListPtr drawList, in Particle particle, Vector2 position, Vector2 velocity,
        float sizeScale)
    {
        var fade = particle.Life / particle.MaxLife;
        var alpha = fade > 0.7f ? 1f : fade / 0.7f;
        var tint = Vector4.Lerp(particle.EndColor, particle.Color, fade);
        var color = ImGui.GetColorU32(tint with { W = tint.W * alpha });
        var radius = particle.Size * sizeScale * SizeFactor(particle.Curve, fade);
        if (particle.Additive && particle.Shape != ParticleShape.GlowCircle)
        {
            drawList.AddCircleFilled(position, radius * HaloScale,
                ImGui.GetColorU32(tint with { W = tint.W * alpha * HaloAlpha }));
        }

        switch (particle.Shape)
        {
            case ParticleShape.Square:
                DrawSquare(drawList, particle.Rotation, position, radius, color);
                break;
            case ParticleShape.Star:
                DrawStar(drawList, particle.Rotation, position, radius, color, alpha);
                break;
            case ParticleShape.Streak:
                DrawStreak(drawList, position, velocity, radius, color);
                break;
            case ParticleShape.GlowCircle:
                DrawGlowCircle(drawList, position, radius, tint, color, alpha);
                break;
            case ParticleShape.Ring:
                DrawRing(drawList, position, radius, fade, color);
                break;
            case ParticleShape.Shard:
                DrawShard(drawList, particle.Rotation, position, radius, color);
                break;
            case ParticleShape.Spark:
                DrawSpark(drawList, position, velocity, radius, tint, alpha);
                break;
            case ParticleShape.Glyph:
                DrawGlyph(drawList, particle.Glyph, position, radius, tint with { W = tint.W * alpha });
                break;
            default:
                drawList.AddCircleFilled(position, radius, color);
                break;
        }
    }

    private static float SizeFactor(SizeCurve curve, float fade) => curve switch
    {
        SizeCurve.Grow => 0.3f + 0.7f * (1f - fade),
        SizeCurve.Pulse => 0.6f + 0.4f * MathF.Sin((1f - fade) * MathF.PI),
        _ => 0.4f + 0.6f * fade,
    };

    private static void DrawSquare(ImDrawListPtr drawList, float rotation, Vector2 position, float radius, uint color)
    {
        var right = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * radius;
        var up = new Vector2(-right.Y, right.X);
        drawList.AddQuadFilled(position - right - up, position + right - up, position + right + up,
            position - right + up, color);
    }

    private static void DrawStar(ImDrawListPtr drawList, float rotation, Vector2 position, float radius, uint color,
        float alpha)
    {
        var twinkle = 0.55f + 0.45f * MathF.Sin(rotation * 3f);
        var arm = radius * (1.6f + twinkle);
        var thickness = MathF.Max(1f, radius * 0.42f);
        var axisA = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * arm;
        var axisB = new Vector2(-axisA.Y, axisA.X);
        drawList.AddLine(position - axisA, position + axisA, color, thickness);
        drawList.AddLine(position - axisB, position + axisB, color, thickness);
        var core = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f * alpha));
        drawList.AddCircleFilled(position, thickness * 0.7f, core);
    }

    private static void DrawStreak(ImDrawListPtr drawList, Vector2 position, Vector2 velocity, float radius,
        uint color)
    {
        var speed = velocity.Length();
        if (speed < 1f)
        {
            drawList.AddCircleFilled(position, radius, color);
            return;
        }

        var stretch = MathF.Min(4.5f, 0.8f + speed * 0.012f);
        var tail = position - velocity / speed * radius * 2f * stretch;
        drawList.AddLine(tail, position, color, MathF.Max(1f, radius * 0.9f));
    }

    private static void DrawGlowCircle(ImDrawListPtr drawList, Vector2 position, float radius, Vector4 tint,
        uint color, float alpha)
    {
        var halo = ImGui.GetColorU32(tint with { W = tint.W * alpha * HaloAlpha });
        drawList.AddCircleFilled(position, radius * HaloScale, halo);
        drawList.AddCircleFilled(position, radius, color);
        var hot = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.6f * alpha));
        drawList.AddCircleFilled(position, radius * 0.45f, hot);
    }

    private static void DrawRing(ImDrawListPtr drawList, Vector2 position, float radius, float fade, uint color)
    {
        var grown = radius * (1f + 2.2f * (1f - fade));
        var thickness = MathF.Max(1f, radius * 0.35f * (0.3f + 0.7f * fade));
        drawList.AddCircle(position, grown, color, 0, thickness);
    }

    private static void DrawShard(ImDrawListPtr drawList, float rotation, Vector2 position, float radius, uint color)
    {
        var tip = position + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * radius * 1.6f;
        var baseAngle = rotation + MathF.PI;
        var baseDirection = new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle)) * radius * 0.6f;
        var side = new Vector2(-MathF.Sin(rotation), MathF.Cos(rotation)) * radius * 0.7f;
        drawList.AddTriangleFilled(tip, position + baseDirection + side, position + baseDirection - side, color);
    }

    private static void DrawSpark(ImDrawListPtr drawList, Vector2 position, Vector2 velocity, float radius,
        Vector4 tint, float alpha)
    {
        var speed = velocity.Length();
        var back = speed > 0.01f ? velocity / speed : Vector2.UnitY;
        var head = ImGui.GetColorU32(tint with { W = tint.W * alpha });
        var middle = ImGui.GetColorU32(tint with { W = tint.W * alpha * 0.55f });
        var tail = ImGui.GetColorU32(tint with { W = tint.W * alpha * 0.25f });
        drawList.AddCircleFilled(position, radius, head);
        drawList.AddCircleFilled(position - back * radius * 2.2f, radius * 0.7f, middle);
        drawList.AddCircleFilled(position - back * radius * 4.2f, radius * 0.45f, tail);
    }

    private static void DrawGlyph(ImDrawListPtr drawList, char glyph, Vector2 position, float radius, Vector4 color)
    {
        if (glyph < FirstGlyph || glyph > LastGlyph)
        {
            return;
        }

        var textScale = Math.Clamp(radius / 7f, 0.6f, 2.4f);
        Typography.DrawCentered(drawList, position, GlyphLabels[glyph - FirstGlyph], color, textScale, FontWeight.Bold);
    }

    private static string[] BuildGlyphLabels()
    {
        var labels = new string[LastGlyph - FirstGlyph + 1];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = ((char)(FirstGlyph + index)).ToString();
        }

        return labels;
    }
}
