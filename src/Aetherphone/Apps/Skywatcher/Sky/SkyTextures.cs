using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal enum SkyTexture : byte
{
    Mist,
    Billow,
    Glow,
    Rain,
}

internal static class SkyTextures
{
    public const int NoiseSize = 256;
    public const int GlowSize = 256;
    public const float GlowExtent = 0.25f;
    public const int RainWidth = 128;
    public const int RainHeight = 256;
    private const int TextureCount = 4;
    private const int RainStreakCount = 90;
    private const int BillowShadeOffset = 12;
    private const long RetryDelayMilliseconds = 10000;
    private static readonly IDalamudTextureWrap?[] Wraps = new IDalamudTextureWrap?[TextureCount];
    private static int loading;
    private static int generation;
    private static long retryAfter;

    public static bool TryGet(SkyTexture texture, out ImTextureID handle)
    {
        handle = default;
        var wrap = Volatile.Read(ref Wraps[(int)texture]);
        if (wrap is not null)
        {
            handle = wrap.Handle;
            return true;
        }

        if (Environment.TickCount64 >= Interlocked.Read(ref retryAfter) &&
            Interlocked.CompareExchange(ref loading, 1, 0) == 0)
        {
            var stamp = Volatile.Read(ref generation);
            _ = Task.Run(() => BuildAllAsync(stamp));
        }

        return false;
    }

    public static void Dispose()
    {
        Interlocked.Increment(ref generation);
        for (var index = 0; index < TextureCount; index++)
        {
            Interlocked.Exchange(ref Wraps[index], null)?.Dispose();
        }

        Interlocked.Exchange(ref loading, 0);
    }

    public static byte[] BakeMist()
    {
        var field = SkyNoise.Field(NoiseSize, NoiseSize, 3, 4, 0.22f, 11);
        var pixels = new byte[NoiseSize * NoiseSize * 4];
        for (var row = 0; row < NoiseSize; row++)
        {
            for (var column = 0; column < NoiseSize; column++)
            {
                var density = SkyNoise.SmoothStep(0.18f, 0.92f, field[row * NoiseSize + column]);
                Write(pixels, row * NoiseSize + column, 1f, MathF.Pow(density, 1.15f), column, row);
            }
        }

        return pixels;
    }

    public static byte[] BakeBillow()
    {
        var field = SkyNoise.Field(NoiseSize, NoiseSize, 3, 5, 0.12f, 23);
        var body = SkyNoise.Field(NoiseSize, NoiseSize, 3, 2, 0.12f, 23);
        var pixels = new byte[NoiseSize * NoiseSize * 4];
        for (var row = 0; row < NoiseSize; row++)
        {
            var aboveRow = (row - BillowShadeOffset + NoiseSize) % NoiseSize;
            for (var column = 0; column < NoiseSize; column++)
            {
                var value = field[row * NoiseSize + column];
                var lit = body[row * NoiseSize + column] - body[aboveRow * NoiseSize + column];
                var density = SkyNoise.SmoothStep(0.42f, 0.86f, value);
                var core = SkyNoise.SmoothStep(0.62f, 1f, value);
                var shade = Math.Clamp(0.97f - 0.24f * core + lit * 2.2f, 0.66f, 1f);
                Write(pixels, row * NoiseSize + column, shade, density, column, row);
            }
        }

        return pixels;
    }

    public static byte[] BakeGlow()
    {
        var pixels = new byte[GlowSize * GlowSize * 4];
        var half = GlowSize * 0.5f;
        var reach = GlowSize * GlowExtent;
        for (var row = 0; row < GlowSize; row++)
        {
            for (var column = 0; column < GlowSize; column++)
            {
                var offsetX = (column + 0.5f - half) / reach;
                var offsetY = (row + 0.5f - half) / reach;
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                var edge = MathF.Max(0f, 1f - distanceSquared);
                var strength = MathF.Exp(-4.5f * distanceSquared) * edge * edge;
                Write(pixels, row * GlowSize + column, 1f, strength, column, row);
            }
        }

        return pixels;
    }

    public static byte[] BakeRain()
    {
        var alpha = new float[RainWidth * RainHeight];
        for (var streakIndex = 0; streakIndex < RainStreakCount; streakIndex++)
        {
            var column = (int)(SkyNoise.Unit(streakIndex, 1, 41) * RainWidth);
            var start = SkyNoise.Unit(streakIndex, 2, 41) * RainHeight;
            var length = 14f + SkyNoise.Unit(streakIndex, 3, 41) * 46f;
            var strength = 0.30f + SkyNoise.Unit(streakIndex, 4, 41) * 0.70f;
            var steps = (int)MathF.Ceiling(length);
            for (var step = 0; step <= steps; step++)
            {
                var along = step / (float)steps;
                var body = strength * MathF.Pow(MathF.Sin(along * MathF.PI), 0.6f) * (0.35f + 0.65f * along);
                var row = ((int)(start + step) % RainHeight + RainHeight) % RainHeight;
                Accumulate(alpha, column, row, body);
                Accumulate(alpha, (column + 1) % RainWidth, row, body * 0.30f);
                Accumulate(alpha, (column - 1 + RainWidth) % RainWidth, row, body * 0.30f);
            }
        }

        var pixels = new byte[RainWidth * RainHeight * 4];
        for (var row = 0; row < RainHeight; row++)
        {
            for (var column = 0; column < RainWidth; column++)
            {
                Write(pixels, row * RainWidth + column, 1f, alpha[row * RainWidth + column], column, row);
            }
        }

        return pixels;
    }

    private static void Accumulate(float[] alpha, int column, int row, float value)
    {
        ref var cell = ref alpha[row * RainWidth + column];
        cell = MathF.Max(cell, value);
    }

    private static void Write(byte[] pixels, int pixelIndex, float shade, float alpha, int column, int row)
    {
        var offset = pixelIndex * 4;
        var channel = SkyNoise.Quantize(shade, column, row);
        pixels[offset] = channel;
        pixels[offset + 1] = channel;
        pixels[offset + 2] = channel;
        pixels[offset + 3] = SkyNoise.Quantize(alpha, column, row);
    }

    private static async Task BuildAllAsync(int stamp)
    {
        try
        {
            await UploadAsync(SkyTexture.Mist, BakeMist(), NoiseSize, NoiseSize, stamp).ConfigureAwait(false);
            await UploadAsync(SkyTexture.Billow, BakeBillow(), NoiseSize, NoiseSize, stamp).ConfigureAwait(false);
            await UploadAsync(SkyTexture.Glow, BakeGlow(), GlowSize, GlowSize, stamp).ConfigureAwait(false);
            await UploadAsync(SkyTexture.Rain, BakeRain(), RainWidth, RainHeight, stamp).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Skywatcher] failed to bake the sky textures");
            Interlocked.Exchange(ref retryAfter, Environment.TickCount64 + RetryDelayMilliseconds);
            Interlocked.Exchange(ref loading, 0);
        }
    }

    private static async Task UploadAsync(SkyTexture texture, byte[] pixels, int width, int height, int stamp)
    {
        if (Volatile.Read(ref Wraps[(int)texture]) is not null)
        {
            return;
        }

        var wrap = await Plugin.TextureProvider.CreateFromRawAsync(RawImageSpecification.Rgba32(width, height),
            pixels, $"Aetherphone.Sky.{texture}", CancellationToken.None).ConfigureAwait(false);
        if (stamp != Volatile.Read(ref generation) ||
            Interlocked.CompareExchange(ref Wraps[(int)texture], wrap, null) is not null)
        {
            wrap.Dispose();
            return;
        }

        if (stamp != Volatile.Read(ref generation))
        {
            Interlocked.CompareExchange(ref Wraps[(int)texture], null, wrap)?.Dispose();
        }
    }
}
