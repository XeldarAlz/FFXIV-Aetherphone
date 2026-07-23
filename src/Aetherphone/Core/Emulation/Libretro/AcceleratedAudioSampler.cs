namespace Aetherphone.Core.Emulation.Libretro;

internal static class AcceleratedAudioSampler
{
    public static int Copy(short[] source, int frameCount, byte[] destination, int speed, ref int sourcePhase)
    {
        speed = Math.Clamp(speed, 1, 8);
        var outputFrames = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            if (sourcePhase == 0)
            {
                var sample = frame * 2;
                var output = outputFrames * 4;
                var left = source[sample];
                var right = source[sample + 1];
                destination[output] = (byte)left;
                destination[output + 1] = (byte)(left >> 8);
                destination[output + 2] = (byte)right;
                destination[output + 3] = (byte)(right >> 8);
                outputFrames++;
            }

            sourcePhase = (sourcePhase + 1) % speed;
        }

        return outputFrames;
    }
}
