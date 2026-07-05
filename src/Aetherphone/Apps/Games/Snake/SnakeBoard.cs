using System.Numerics;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Snake;

internal enum SnakeState
{
    Ready,
    Playing,
    Over,
}

internal sealed class SnakeBoard
{
    private const float SegRadiusFraction = 0.022f;
    private const float SpeedFraction = 0.52f;
    private const float MaxTurn = 5.5f;
    private const int StartLength = 7;
    private const int Growth = 4;
    private const float GraceSeconds = 0.6f;
    private readonly List<Vector2> samples = new(256);
    private readonly Random random = new();
    private float angle;
    private int length;
    private float playTime;
    private Vector2 lastReadyMouse;
    private bool readyMouseInit;
    public SnakeState State { get; private set; }
    public int Score { get; private set; }
    public Vector2 Head { get; private set; }
    public Vector2 Food { get; private set; }
    public float Angle => angle;
    public bool AteLastStep { get; private set; }
    public int SampleCount => samples.Count;
    public Vector2 Sample(int index) => samples[index];
    public static float SegRadiusOf(Rect area) => SegRadiusFraction * area.Height;
    public static float FoodRadiusOf(Rect area) => SegRadiusFraction * area.Height * 0.85f;

    public void Reset(Rect area)
    {
        State = SnakeState.Ready;
        Head = area.Center;
        angle = -MathF.PI / 2f;
        length = StartLength;
        Score = 0;
        playTime = 0f;
        readyMouseInit = false;
        AteLastStep = false;
        var step = SegRadiusOf(area) * 0.85f;
        var tailDirection = new Vector2(MathF.Cos(angle + MathF.PI), MathF.Sin(angle + MathF.PI));
        samples.Clear();
        for (var index = 0; index < StartLength; index++)
        {
            samples.Add(Head + tailDirection * ((StartLength - 1 - index) * step));
        }

        SpawnFood(area);
    }

    public void Begin(Vector2 mouse)
    {
        if (State != SnakeState.Ready)
        {
            return;
        }

        State = SnakeState.Playing;
        angle = MathF.Atan2(mouse.Y - Head.Y, mouse.X - Head.X);
    }

    public bool Step(float deltaSeconds, Rect area, Vector2 mouse)
    {
        AteLastStep = false;
        if (State == SnakeState.Ready)
        {
            if (!readyMouseInit)
            {
                lastReadyMouse = mouse;
                readyMouseInit = true;
                return false;
            }

            if (Vector2.Distance(mouse, lastReadyMouse) > area.Height * 0.01f)
            {
                Begin(mouse);
            }

            return false;
        }

        if (State != SnakeState.Playing)
        {
            return false;
        }

        playTime += deltaSeconds;
        var radius = SegRadiusOf(area);
        var step = radius * 0.85f;
        var desired = MathF.Atan2(mouse.Y - Head.Y, mouse.X - Head.X);
        var difference = WrapAngle(desired - angle);
        var maxStep = MaxTurn * deltaSeconds;
        angle += Math.Clamp(difference, -maxStep, maxStep);
        Head += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * SpeedFraction * area.Height * deltaSeconds;
        var margin = radius;
        if (Head.X < area.Min.X + margin || Head.X > area.Max.X - margin || Head.Y < area.Min.Y + margin ||
            Head.Y > area.Max.Y - margin)
        {
            State = SnakeState.Over;
            return true;
        }

        if (samples.Count == 0 || Vector2.Distance(Head, samples[samples.Count - 1]) >= step)
        {
            samples.Add(Head);
        }

        while (samples.Count > length)
        {
            samples.RemoveAt(0);
        }

        if (Vector2.Distance(Head, Food) < radius + FoodRadiusOf(area))
        {
            Score++;
            length += Growth;
            AteLastStep = true;
            SpawnFood(area);
        }

        if (playTime > GraceSeconds)
        {
            const int skipNearHead = 4;
            var collideDistance = radius * 1.25f;
            for (var index = 0; index < samples.Count - skipNearHead; index++)
            {
                if (Vector2.Distance(Head, samples[index]) < collideDistance)
                {
                    State = SnakeState.Over;
                    return true;
                }
            }
        }

        return false;
    }

    private void SpawnFood(Rect area)
    {
        var margin = SegRadiusOf(area) * 2.5f;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var candidate = new Vector2(area.Min.X + margin + (float)random.NextDouble() * (area.Width - 2f * margin),
                area.Min.Y + margin + (float)random.NextDouble() * (area.Height - 2f * margin));
            if (Vector2.Distance(candidate, Head) > area.Height * 0.18f)
            {
                Food = candidate;
                return;
            }
        }

        Food = area.Center + new Vector2(area.Width * 0.25f, 0f);
    }

    private static float WrapAngle(float value)
    {
        while (value > MathF.PI)
        {
            value -= MathF.PI * 2f;
        }

        while (value < -MathF.PI)
        {
            value += MathF.PI * 2f;
        }

        return value;
    }
}
