using System;
using System.Collections.Generic;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Flap;

internal enum FlapState
{
    Ready,
    Playing,
    Over,
}

internal struct FlapPipe
{
    public float X;

    public float GapCenter;

    public float GapHalf;

    public bool Scored;
}

internal sealed class FlapBoard
{
    private const float BirdXFraction = 0.30f;

    private const float BirdRadiusFraction = 0.038f;

    private const float Gravity = 4.0f;

    private const float FlapVelocity = -1.05f;

    private const float MaxFall = 2.0f;

    private const float PipeSpeed = 0.44f;

    private const float PipeWidthFraction = 0.17f;

    private const float SpacingFraction = 0.66f;

    private const float GapHalfStart = 0.18f;

    private const float GapHalfMin = 0.135f;

    private readonly List<FlapPipe> pipes = new(8);

    private readonly Random random = new();

    public FlapState State { get; private set; }

    public int Score { get; private set; }

    public float BirdY { get; private set; }

    public float BirdVelocity { get; private set; }

    public int PipeCount => pipes.Count;

    public FlapPipe PipeAt(int index) => pipes[index];

    public static float BirdXOf(Rect area) => area.Min.X + BirdXFraction * area.Width;

    public static float RadiusOf(Rect area) => BirdRadiusFraction * area.Height;

    public static float PipeWidthOf(Rect area) => PipeWidthFraction * area.Width;

    public void Reset(Rect area)
    {
        State = FlapState.Ready;
        BirdY = area.Center.Y;
        BirdVelocity = 0f;
        Score = 0;
        pipes.Clear();
    }

    public void Flap(Rect area)
    {
        if (State == FlapState.Over)
        {
            return;
        }

        if (State == FlapState.Ready)
        {
            State = FlapState.Playing;
        }

        BirdVelocity = FlapVelocity * area.Height;
    }

    public bool Step(float deltaSeconds, Rect area)
    {
        if (State != FlapState.Playing)
        {
            return false;
        }

        var height = area.Height;
        var radius = RadiusOf(area);

        BirdVelocity = MathF.Min(BirdVelocity + Gravity * height * deltaSeconds, MaxFall * height);
        BirdY += BirdVelocity * deltaSeconds;

        var ceiling = area.Min.Y + radius;
        if (BirdY < ceiling)
        {
            BirdY = ceiling;
            BirdVelocity = 0f;
        }

        if (BirdY + radius >= area.Max.Y)
        {
            BirdY = area.Max.Y - radius;
            State = FlapState.Over;
            return true;
        }

        return StepPipes(deltaSeconds, area, radius);
    }

    private bool StepPipes(float deltaSeconds, Rect area, float radius)
    {
        var width = area.Width;
        var speed = MathF.Min(PipeSpeed + Score * 0.006f, 0.72f) * width;
        var pipeWidth = PipeWidthOf(area);
        var birdX = BirdXOf(area);

        for (var index = 0; index < pipes.Count; index++)
        {
            var pipe = pipes[index];
            pipe.X -= speed * deltaSeconds;
            if (!pipe.Scored && pipe.X + pipeWidth < birdX)
            {
                pipe.Scored = true;
                Score++;
            }

            pipes[index] = pipe;
        }

        if (pipes.Count > 0 && pipes[0].X + pipeWidth < area.Min.X)
        {
            pipes.RemoveAt(0);
        }

        MaybeSpawn(area, width);

        for (var index = 0; index < pipes.Count; index++)
        {
            var pipe = pipes[index];
            if (birdX + radius <= pipe.X || birdX - radius >= pipe.X + pipeWidth)
            {
                continue;
            }

            if (BirdY - radius < pipe.GapCenter - pipe.GapHalf || BirdY + radius > pipe.GapCenter + pipe.GapHalf)
            {
                State = FlapState.Over;
                return true;
            }
        }

        return false;
    }

    private void MaybeSpawn(Rect area, float width)
    {
        var spacing = SpacingFraction * width;
        if (pipes.Count == 0)
        {
            SpawnPipe(area, area.Max.X + 0.3f * width);
            return;
        }

        var last = pipes[pipes.Count - 1];
        if (last.X <= area.Max.X - spacing)
        {
            SpawnPipe(area, last.X + spacing);
        }
    }

    private void SpawnPipe(Rect area, float x)
    {
        var height = area.Height;
        var gapHalf = MathF.Max(GapHalfMin, GapHalfStart - Score * 0.004f) * height;
        var margin = 0.07f * height + gapHalf;
        var center = area.Min.Y + margin + (float)random.NextDouble() * (height - 2f * margin);

        pipes.Add(new FlapPipe
        {
            X = x,
            GapCenter = center,
            GapHalf = gapHalf,
            Scored = false,
        });
    }
}
