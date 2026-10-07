namespace Aetherphone.Apps.Games.Tempo;

[Flags]
internal enum TempoSignal : ushort
{
    None = 0,
    Jumped = 1,
    Landed = 2,
    Died = 4,
    Coin = 8,
    Pad = 16,
    Portal = 32,
    Finished = 64,
    Bonked = 128,
    Restarted = 256,
    Beat = 512,
}

internal struct TempoRunner
{
    public float X;
    public float Y;
    public float PreviousX;
    public float PreviousY;
    public float VelocityY;
    public float Rotation;
    public float PreviousRotation;
    public int Tick;
    public int Jumps;
    public sbyte Gravity;
    public byte Buffer;
    public byte Coins;
    public bool Grounded;
    public bool PadFlight;
    public bool Alive;
    public bool Finished;
    public byte LastCoin;

    public readonly int CoinsCollected => (Coins & 1) + ((Coins >> 1) & 1) + ((Coins >> 2) & 1);
}

internal static class TempoPhysics
{
    public const float TickSeconds = 1f / 120f;
    public const float Half = 0.45f;
    public const float Gravity = 62f;
    public const float JumpVelocity = 16.5f;
    public const float PadVelocity = 21f;
    public const float PadHoldScale = 0.6f;
    public const float MaxFallSpeed = 26f;
    public const float SnapUp = 0.3f;
    public const float StartX = 2f;
    public const int BufferTicks = 12;
    public const float SpikeHalfWidth = 0.18f;
    public const float SpikeHeight = 0.5f;
    public const float SpikeShrink = 0.05f;
    public const float PadHeight = 0.25f;
    public const float PadInset = 0.1f;
    public const float CoinReach = 0.8f;
    public const float FloorLimit = -4f;
    public const float SkyLimit = 22f;
    private const float Infinity = 1000f;
    private const float Epsilon = 0.001f;
    private const float QuarterTurn = MathF.PI * 0.5f;
    private static readonly float AirTicks = 2f * JumpVelocity / Gravity / TickSeconds;

    public static TempoRunner Start(TempoLevel level)
    {
        var runner = default(TempoRunner);
        runner.X = StartX;
        runner.Gravity = 1;
        runner.Y = Support(level, StartX) + Half;
        runner.PreviousX = runner.X;
        runner.PreviousY = runner.Y;
        runner.Grounded = true;
        runner.Alive = true;
        return runner;
    }

    public static float Support(TempoLevel level, float x)
    {
        var first = (int)MathF.Floor(x - Half + Epsilon);
        var last = (int)MathF.Floor(x + Half - Epsilon);
        var support = -Infinity;
        for (var column = first; column <= last; column++)
        {
            var height = level.Ground(column);
            if (height != TempoLevel.Pit && height > support)
            {
                support = height;
            }
        }

        return support;
    }

    public static float Cap(TempoLevel level, float x)
    {
        var first = (int)MathF.Floor(x - Half + Epsilon);
        var last = (int)MathF.Floor(x + Half - Epsilon);
        var cap = Infinity;
        for (var column = first; column <= last; column++)
        {
            var height = level.Ceiling(column);
            if (height != TempoLevel.Open && height < cap)
            {
                cap = height;
            }
        }

        return cap;
    }

    public static TempoSignal Tick(ref TempoRunner runner, TempoLevel level, bool held, bool pressed)
    {
        if (!runner.Alive || runner.Finished)
        {
            return TempoSignal.None;
        }

        var signals = TempoSignal.None;
        runner.Tick++;
        runner.PreviousX = runner.X;
        runner.PreviousY = runner.Y;
        runner.PreviousRotation = runner.Rotation;
        if (pressed)
        {
            runner.Buffer = BufferTicks;
        }

        runner.X += level.Speed * TickSeconds;
        signals |= CrossPortals(ref runner, level);
        if (runner.Grounded && (held || runner.Buffer > 0))
        {
            runner.VelocityY = JumpVelocity * runner.Gravity;
            runner.Grounded = false;
            runner.PadFlight = false;
            runner.Buffer = 0;
            runner.Jumps++;
            signals |= TempoSignal.Jumped;
        }

        if (!runner.Grounded)
        {
            var rising = runner.VelocityY * runner.Gravity > 0f;
            var scale = runner.PadFlight && held && rising ? PadHoldScale : 1f;
            runner.VelocityY -= runner.Gravity * Gravity * scale * TickSeconds;
            runner.VelocityY = Math.Clamp(runner.VelocityY, -MaxFallSpeed, MaxFallSpeed);
            runner.Y += runner.VelocityY * TickSeconds;
        }

        signals |= Resolve(ref runner, level);
        if (!runner.Alive)
        {
            return signals | TempoSignal.Died;
        }

        signals |= Pads(ref runner, level);
        if (HitsSpike(runner, level) || runner.Y < FloorLimit || runner.Y > SkyLimit)
        {
            runner.Alive = false;
            return signals | TempoSignal.Died;
        }

        signals |= Collect(ref runner, level);
        if (runner.Buffer > 0)
        {
            runner.Buffer--;
        }

        Spin(ref runner);
        if (runner.X >= level.Length)
        {
            runner.Finished = true;
            signals |= TempoSignal.Finished;
        }

        return signals;
    }

    private static TempoSignal CrossPortals(ref TempoRunner runner, TempoLevel level)
    {
        var column = (int)MathF.Floor(runner.X - 0.5f);
        var middle = column + 0.5f;
        if (runner.PreviousX >= middle || runner.X < middle)
        {
            return TempoSignal.None;
        }

        var item = level.Item(column);
        var wanted = item switch
        {
            TempoItem.GravityUp => (sbyte)-1,
            TempoItem.GravityDown => (sbyte)1,
            _ => (sbyte)0,
        };
        if (wanted == 0)
        {
            return TempoSignal.None;
        }

        if (wanted == runner.Gravity)
        {
            return TempoSignal.Portal;
        }

        runner.Gravity = wanted;
        runner.Grounded = false;
        runner.PadFlight = false;
        runner.VelocityY *= 0.5f;
        return TempoSignal.Portal;
    }

    private static TempoSignal Resolve(ref TempoRunner runner, TempoLevel level)
    {
        var signals = TempoSignal.None;
        var support = Support(level, runner.X);
        var cap = Cap(level, runner.X);
        var bottom = runner.Y - Half;
        var top = runner.Y + Half;
        if (bottom < support)
        {
            if (runner.PreviousY - Half < support - SnapUp)
            {
                runner.Alive = false;
                return signals;
            }

            runner.Y = support + Half;
            if (runner.Gravity > 0)
            {
                signals |= Land(ref runner);
            }
            else if (runner.VelocityY < 0f)
            {
                runner.VelocityY = 0f;
                signals |= TempoSignal.Bonked;
            }
        }

        if (top > cap)
        {
            if (runner.PreviousY + Half > cap + SnapUp)
            {
                runner.Alive = false;
                return signals;
            }

            runner.Y = cap - Half;
            if (runner.Gravity < 0)
            {
                signals |= Land(ref runner);
            }
            else if (runner.VelocityY > 0f)
            {
                runner.VelocityY = 0f;
                signals |= TempoSignal.Bonked;
            }
        }

        if (!runner.Grounded)
        {
            return signals;
        }

        var footing = runner.Gravity > 0 ? runner.Y - Half - support : cap - (runner.Y + Half);
        if (footing > Epsilon)
        {
            runner.Grounded = false;
            runner.VelocityY = 0f;
        }

        return signals;
    }

    private static TempoSignal Land(ref TempoRunner runner)
    {
        var wasAirborne = !runner.Grounded;
        runner.VelocityY = 0f;
        runner.Grounded = true;
        runner.PadFlight = false;
        return wasAirborne ? TempoSignal.Landed : TempoSignal.None;
    }

    private static TempoSignal Pads(ref TempoRunner runner, TempoLevel level)
    {
        var first = (int)MathF.Floor(runner.X - Half);
        var last = (int)MathF.Floor(runner.X + Half);
        for (var column = first; column <= last; column++)
        {
            var item = level.Item(column);
            if (item == TempoItem.Pad && runner.Gravity > 0)
            {
                var surface = level.Ground(column);
                if (!Overlaps(runner.X, column + PadInset, column + 1f - PadInset) || runner.VelocityY > 0f ||
                    runner.Y - Half > surface + PadHeight)
                {
                    continue;
                }

                Launch(ref runner);
                return TempoSignal.Pad;
            }

            if (item == TempoItem.CeilingPad && runner.Gravity < 0)
            {
                var surface = level.Ceiling(column);
                if (!Overlaps(runner.X, column + PadInset, column + 1f - PadInset) || runner.VelocityY < 0f ||
                    runner.Y + Half < surface - PadHeight)
                {
                    continue;
                }

                Launch(ref runner);
                return TempoSignal.Pad;
            }
        }

        return TempoSignal.None;
    }

    private static void Launch(ref TempoRunner runner)
    {
        runner.VelocityY = PadVelocity * runner.Gravity;
        runner.Grounded = false;
        runner.PadFlight = true;
        runner.Buffer = 0;
    }

    private static bool Overlaps(float x, float left, float right) => x + Half > left && x - Half < right;

    private static bool HitsSpike(in TempoRunner runner, TempoLevel level)
    {
        var first = (int)MathF.Floor(runner.X - Half);
        var last = (int)MathF.Floor(runner.X + Half);
        var left = runner.X - Half + SpikeShrink;
        var right = runner.X + Half - SpikeShrink;
        var bottom = runner.Y - Half + SpikeShrink;
        var top = runner.Y + Half - SpikeShrink;
        for (var column = first; column <= last; column++)
        {
            var item = level.Item(column);
            if (item is not (TempoItem.Spike or TempoItem.CeilingSpike))
            {
                continue;
            }

            if (right <= column + 0.5f - SpikeHalfWidth || left >= column + 0.5f + SpikeHalfWidth)
            {
                continue;
            }

            if (item == TempoItem.Spike)
            {
                var surface = level.Ground(column);
                if (bottom < surface + SpikeHeight && top > surface)
                {
                    return true;
                }

                continue;
            }

            var ceiling = level.Ceiling(column);
            if (top > ceiling - SpikeHeight && bottom < ceiling)
            {
                return true;
            }
        }

        return false;
    }

    private static TempoSignal Collect(ref TempoRunner runner, TempoLevel level)
    {
        for (var index = 0; index < TempoLevel.CoinCount && index < level.CoinTotal; index++)
        {
            var bit = (byte)(1 << index);
            if ((runner.Coins & bit) != 0)
            {
                continue;
            }

            var column = level.CoinColumn(index);
            var center = new Vector2(column + 0.5f, level.Coin(column) + 0.5f);
            if (Vector2.DistanceSquared(center, new Vector2(runner.X, runner.Y)) > CoinReach * CoinReach)
            {
                continue;
            }

            runner.Coins |= bit;
            runner.LastCoin = (byte)index;
            return TempoSignal.Coin;
        }

        return TempoSignal.None;
    }

    private static void Spin(ref TempoRunner runner)
    {
        if (runner.Grounded)
        {
            var target = MathF.Round(runner.Rotation / QuarterTurn) * QuarterTurn;
            runner.Rotation += (target - runner.Rotation) * 0.35f;
            return;
        }

        runner.Rotation += runner.Gravity * QuarterTurn / AirTicks;
    }
}
