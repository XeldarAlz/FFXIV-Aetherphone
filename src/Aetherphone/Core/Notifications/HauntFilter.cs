namespace Aetherphone.Core.Notifications;

internal enum HauntDepth : byte
{
    None,
    Faint,
    Light,
    Full,
}

internal static class HauntFilter
{
    public const int SampleRate = 48000;

    private const int ChannelCount = 2;
    private const float FadeSeconds = 0.03f;
    private const float TritoneDown = 0.7071f;
    private const float ShadowCutoffHertz = 1800f;
    private const float LengthGrowth = 1.45f;
    private const float FirstMoanDelaySeconds = 0.11f;
    private const float FirstMoanStartRate = 0.95f;
    private const float FirstMoanEndRate = 0.72f;
    private const float FirstMoanCutoffHertz = 2600f;
    private const float SecondMoanDelaySeconds = 0.26f;
    private const float SecondMoanStartRate = 0.9f;
    private const float SecondMoanEndRate = 0.62f;
    private const float SecondMoanCutoffHertz = 1600f;
    private const float TrembleHertz = 5.5f;
    private const float TrembleDepth = 0.25f;
    private const float MoanFadeDepth = 0.6f;
    private const float PeakHeadroom = 1.12f;
    private const float Ceiling = 0.99f;
    private const float Silence = 1e-6f;

    public static float[] Apply(float[] clip, HauntDepth depth)
    {
        var frames = clip.Length / ChannelCount;
        if (depth == HauntDepth.None || frames < 2)
        {
            return clip;
        }

        var originalPeak = Peak(clip);
        if (originalPeak <= Silence)
        {
            return clip;
        }

        var recipe = RecipeFor(depth);
        var totalFrames = (int)(frames * LengthGrowth) + (int)(recipe.TailSeconds * SampleRate);
        var output = new float[totalFrames * ChannelCount];
        Array.Copy(clip, output, frames * ChannelCount);
        AddShadow(clip, output, recipe.Shadow);
        if (recipe.FirstMoan > 0f)
        {
            AddMoan(clip, output, new MoanVoice(FirstMoanDelaySeconds, FirstMoanStartRate, FirstMoanEndRate,
                FirstMoanCutoffHertz, recipe.FirstMoan));
        }

        if (recipe.SecondMoan > 0f)
        {
            AddMoan(clip, output, new MoanVoice(SecondMoanDelaySeconds, SecondMoanStartRate, SecondMoanEndRate,
                SecondMoanCutoffHertz, recipe.SecondMoan));
        }

        FadeEnd(output);
        Limit(output, MathF.Min(Ceiling, originalPeak * PeakHeadroom));
        return output;
    }

    private readonly record struct HauntRecipe(float TailSeconds, float Shadow, float FirstMoan, float SecondMoan);

    private readonly record struct MoanVoice(float DelaySeconds, float StartRate, float EndRate, float CutoffHertz,
        float Gain);

    private static HauntRecipe RecipeFor(HauntDepth depth) => depth switch
    {
        HauntDepth.Faint => new HauntRecipe(0.04f, 0.08f, 0f, 0f),
        HauntDepth.Light => new HauntRecipe(0.3f, 0.11f, 0.18f, 0.07f),
        _ => new HauntRecipe(0.3f, 0.13f, 0.22f, 0.1f),
    };

    private static void AddShadow(float[] clip, float[] output, float gain)
    {
        var frames = clip.Length / ChannelCount;
        var totalFrames = output.Length / ChannelCount;
        var smoothing = Smoothing(ShadowCutoffHertz);
        var left = 0f;
        var right = 0f;
        for (var frame = 0; frame < totalFrames; frame++)
        {
            var source = frame * TritoneDown;
            var sourceFrame = (int)source;
            if (sourceFrame + 1 >= frames)
            {
                break;
            }

            var fraction = source - sourceFrame;
            left += (Sample(clip, sourceFrame, 0, fraction) - left) * smoothing;
            right += (Sample(clip, sourceFrame, 1, fraction) - right) * smoothing;
            output[frame * ChannelCount] += left * gain;
            output[frame * ChannelCount + 1] += right * gain;
        }
    }

    private static void AddMoan(float[] clip, float[] output, in MoanVoice voice)
    {
        var frames = clip.Length / ChannelCount;
        var totalFrames = output.Length / ChannelCount;
        var smoothing = Smoothing(voice.CutoffHertz);
        var start = (int)(voice.DelaySeconds * SampleRate);
        var span = frames / ((voice.StartRate + voice.EndRate) * 0.5f);
        var trembleStep = 2f * MathF.PI * TrembleHertz / SampleRate;
        var left = 0f;
        var right = 0f;
        var source = 0.0;
        for (var frame = start; frame < totalFrames; frame++)
        {
            var sourceFrame = (int)source;
            if (sourceFrame + 1 >= frames)
            {
                break;
            }

            var progress = Math.Clamp((frame - start) / span, 0f, 1f);
            var fraction = (float)(source - sourceFrame);
            left += (Sample(clip, sourceFrame, 0, fraction) - left) * smoothing;
            right += (Sample(clip, sourceFrame, 1, fraction) - right) * smoothing;
            var tremble = 1f + TrembleDepth * MathF.Sin(frame * trembleStep);
            var level = voice.Gain * (1f - progress * MoanFadeDepth) * tremble;
            output[frame * ChannelCount] += left * level;
            output[frame * ChannelCount + 1] += right * level;
            source += voice.StartRate + (voice.EndRate - voice.StartRate) * progress;
        }
    }

    private static float Smoothing(float cutoffHertz) => 1f - MathF.Exp(-2f * MathF.PI * cutoffHertz / SampleRate);

    private static float Sample(float[] clip, int frame, int channel, float fraction)
    {
        var current = clip[frame * ChannelCount + channel];
        var next = clip[(frame + 1) * ChannelCount + channel];
        return current + (next - current) * fraction;
    }

    private static void FadeEnd(float[] output)
    {
        var totalFrames = output.Length / ChannelCount;
        var fadeFrames = Math.Min(totalFrames, (int)(FadeSeconds * SampleRate));
        var start = totalFrames - fadeFrames;
        for (var frame = start; frame < totalFrames; frame++)
        {
            var level = (float)(totalFrames - 1 - frame) / fadeFrames;
            output[frame * ChannelCount] *= level;
            output[frame * ChannelCount + 1] *= level;
        }
    }

    private static void Limit(float[] output, float ceiling)
    {
        var peak = Peak(output);
        if (peak <= ceiling)
        {
            return;
        }

        var scale = ceiling / peak;
        for (var index = 0; index < output.Length; index++)
        {
            output[index] *= scale;
        }
    }

    private static float Peak(float[] samples)
    {
        var peak = 0f;
        for (var index = 0; index < samples.Length; index++)
        {
            peak = MathF.Max(peak, MathF.Abs(samples[index]));
        }

        return peak;
    }
}
