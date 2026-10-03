namespace Aetherphone.Apps.Games.Coil;

internal sealed class CoilTrack
{
    public const int RawCapacity = 2400;
    public const int SampleCapacity = 4096;

    private readonly Vector2[] raw = new Vector2[RawCapacity];
    private readonly float[] rawArc = new float[RawCapacity];
    private readonly Vector2[] samples = new Vector2[SampleCapacity];
    private int rawCount;
    private int sampleCount;

    public float Length { get; private set; }
    public float SampleSpacing { get; private set; }
    public Vector2 Launcher { get; private set; }
    public int Version { get; private set; }
    public int SampleCount => sampleCount;
    public ReadOnlySpan<Vector2> Samples => samples.AsSpan(0, sampleCount);
    public ReadOnlySpan<float> RawArc => rawArc.AsSpan(0, rawCount);
    public Vector2 Start => samples[0];
    public Vector2 End => samples[sampleCount - 1];

    public void Build(int shapeIndex, float fieldWidth, float fieldHeight, float margin, float spacing)
    {
        rawCount = CoilShapes.Sample(shapeIndex, raw, out var launcher);
        Launcher = Fit(launcher, fieldWidth, fieldHeight, margin);
        rawArc[0] = 0f;
        for (var index = 1; index < rawCount; index++)
        {
            rawArc[index] = rawArc[index - 1] + Vector2.Distance(raw[index - 1], raw[index]);
        }

        Length = rawArc[rawCount - 1];
        Resample(spacing);
        Version++;
    }

    private Vector2 Fit(Vector2 launcher, float fieldWidth, float fieldHeight, float margin)
    {
        var min = raw[0];
        var max = raw[0];
        for (var index = 1; index < rawCount; index++)
        {
            min = Vector2.Min(min, raw[index]);
            max = Vector2.Max(max, raw[index]);
        }

        var size = Vector2.Max(max - min, new Vector2(0.0001f, 0.0001f));
        var fit = MathF.Min((fieldWidth - margin * 2f) / size.X, (fieldHeight - margin * 2f) / size.Y);
        var center = (min + max) * 0.5f;
        var fieldCenter = new Vector2(fieldWidth * 0.5f, fieldHeight * 0.5f);
        for (var index = 0; index < rawCount; index++)
        {
            raw[index] = fieldCenter + (raw[index] - center) * fit;
        }

        return fieldCenter + (launcher - center) * fit;
    }

    private void Resample(float spacing)
    {
        var count = (int)MathF.Ceiling(Length / spacing) + 1;
        count = Math.Clamp(count, 2, SampleCapacity);
        SampleSpacing = Length / (count - 1);
        var cursor = 1;
        for (var index = 0; index < count; index++)
        {
            var target = MathF.Min(index * SampleSpacing, Length);
            while (cursor < rawCount - 1 && rawArc[cursor] < target)
            {
                cursor++;
            }

            var spanStart = rawArc[cursor - 1];
            var spanLength = rawArc[cursor] - spanStart;
            var blend = spanLength <= 0f ? 0f : Math.Clamp((target - spanStart) / spanLength, 0f, 1f);
            samples[index] = Vector2.Lerp(raw[cursor - 1], raw[cursor], blend);
        }

        sampleCount = count;
    }

    public Vector2 PositionAt(float arc)
    {
        if (arc <= 0f)
        {
            return samples[0] - TangentAt(0f) * -arc;
        }

        if (arc >= Length)
        {
            return samples[sampleCount - 1] + TangentAt(Length) * (arc - Length);
        }

        var scaled = arc / SampleSpacing;
        var index = Math.Min((int)scaled, sampleCount - 2);
        return Vector2.Lerp(samples[index], samples[index + 1], scaled - index);
    }

    public Vector2 TangentAt(float arc)
    {
        var scaled = Math.Clamp(arc, 0f, Length) / SampleSpacing;
        var index = Math.Clamp((int)scaled, 0, sampleCount - 2);
        var direction = samples[index + 1] - samples[index];
        var length = direction.Length();
        return length <= 0f ? new Vector2(1f, 0f) : direction / length;
    }

    public float NearestArc(Vector2 point, out float distanceSquared)
    {
        var bestIndex = 0;
        var best = float.MaxValue;
        for (var index = 0; index < sampleCount; index++)
        {
            var distance = Vector2.DistanceSquared(samples[index], point);
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            bestIndex = index;
        }

        distanceSquared = best;
        return bestIndex * SampleSpacing;
    }
}
