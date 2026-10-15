using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Honorific;

internal readonly struct NameplateGradient
{
    private const int Channels = 3;

    public readonly LocString Label;
    private readonly byte[] steps;

    public NameplateGradient(LocString label, string steps)
    {
        Label = label;
        this.steps = Convert.FromBase64String(steps);
    }

    public int Count => steps.Length / Channels;

    public Vector3 Step(int step)
    {
        var offset = step * Channels;
        return new Vector3(steps[offset] / 255f, steps[offset + 1] / 255f, steps[offset + 2] / 255f);
    }
}

internal static class NameplateGradients
{
    public const int TwoColors = -1;

    // Honorific's GradientSystem and GradientStyle: 64 two-color steps held at 0, 10000 and 23000 of 65535,
    // titles over 25 characters colored in pairs, one step every 15 ms times its throttle of 5.
    private const int TwoColorSteps = 64;
    private const int PairedLength = 25;
    private const long StepMilliseconds = 75;
    private const float TwoColorHoldEnd = 10000f / ushort.MaxValue;
    private const float TwoColorBlendEnd = 23000f / ushort.MaxValue;

    // Honorific's Gradient/GradientSystem.cs tables, kept in its order because GradientColourSet is the index.
    public static readonly NameplateGradient[] All =
    {
        new(L.Nameplate.GradientPride,
            "5AMD6RsC7TMC8ksB92MB/HsA/5EA/6IA/7IA/8MA/9QA/+UA5+MEutAKjr0RYaoYNZYeCIMlAHlFAG9rAGaRAF23AFTdAkv9FkXnKj/RPjm8UjOmZi2QcymCcymCcymCcymCcymCcymCZi2QUjOmPjm8Kj/RFkXnAkv9AFTdAF23AGaRAG9rAHlFCIMlNZYeYaoYjr0RutAK5+ME/+UA/9QA/8MA/7IA/6IA/5EA/HsA92MB8ksB7TMC6RsC5AMD"),
        new(L.Nameplate.GradientTransgender,
            "W876b8nygsXplsDhqbvYvbfQ0LLI5K2/9aq59rXC+MDL+cvU+tbd/OHm/ezv/vf4//z9/fH0/Obr+9zi+tHZ+MbQ97vH9rC+7qu72q/Ex7TMs7nUn77djMLleMftZcz2Zcz2eMftjMLln77ds7nUx7TM2q/E7qu79rC+97vH+MbQ+tHZ+9zi/Obr/fH0//z9/vf4/ezv/OHm+tbd+cvU+MDL9rXC9aq55K2/0LLIvbfQqbvYlsDhgsXpb8nyW876"),
        new(L.Nameplate.GradientLesbian,
            "1S0A2lQT33ol46E46MdL7e5d8Opg9Nhe98Zc+rVZ/aNX/6Rm/7eG/8qm/93H//Hn/fj79Nnp67vY4p3G2n+10WKkzGCgxl2cwVuZvFmVtleRskqJrzqBrCp4qBpvpQpmpQpmqBpvrCp4rzqBskqJtleRvFmVwVuZxl2czGCg0WKk2oC1457H67zY9Nrp/fj7//Hn/93H/8qm/7eG/6Rm/aNX+rVZ98dc9Nhe8Opg7exd6MZK46A43nkl2lMT1S0A"),
        new(L.Nameplate.GradientBisexual,
            "1gJwzgx1xxZ6vyB/uCmDsDOIqT2NoUeSm0+Wm0+Wm0+Wm0+WlU6XgUuZbUibWUWeRkKgMj+iHjylCjmnCjmnHjylMj+iRkKgWUWebUibgUuZlU6Xm0+Wm0+Wm0+Wm0+WoUeSqT2NsDOIuCmDvyB/xxZ6zgx11gJw"),
        new(L.Nameplate.GradientBlackWhite,
            "////9/f37+/v5+fn39/f19fXzs7OxsbGvr6+tra2rq6upqamnp6elpaWjo6OhoaGfX19dXV1bW1tZWVlXV1dVVVVTU1NRUVFPT09NTU1LS0tJCQkHBwcFBQUDAwMBAQEBAQEDAwMFBQUHBwcJCQkLS0tNTU1PT09RUVFTU1NVVVVXV1dZWVlbW1tdXV1fX19hoaGjo6OlpaWnp6epqamrq6utra2vr6+xsbGzs7O19fX39/f5+fn7+/v9/f3////"),
        new(L.Nameplate.GradientBlackRed,
            "/wAA9QAA6wAA4QAA1wAAzAAAwgAAuAAArgAApAAAmgAAkAAAhgAAewAAcQAAZwAAXQAAUwAASQAAPwAANQAAKwAAIAAAFgAADAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAADAAAFgAAIAAAKgAANQAAPwAASQAAUwAAXQAAZwAAcQAAewAAhgAAkAAAmgAApAAArgAAuAAAwgAAzAAA1wAA4QAA6wAA9QAA/wAA"),
        new(L.Nameplate.GradientBlackBlue,
            "AAD/AAD1AADrAADhAADXAADMAADCAAC4AACuAACkAACaAACQAACGAAB7AABxAABnAABdAABTAABJAAA/AAA1AAArAAAgAAAWAAAMAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAMAAAWAAAgAAAqAAA1AAA/AABJAABTAABdAABnAABxAAB7AACGAACQAACaAACkAACuAAC4AADCAADMAADXAADhAADrAAD1AAD/"),
        new(L.Nameplate.GradientBlackYellow,
            "//8A9fUA6+sA4eEA19cAzMwAwsIAuLgArq4ApKQAmpoAkJAAhoYAe3sAcXEAZ2cAXV0AU1MASUkAPz8ANTUAKysAICAAFhYADAwAAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgIADAwAFhYAICAAKioANTUAPz8ASUkAU1MAXV0AZ2cAcXEAe3sAhoYAkJAAmpoApKQArq4AuLgAwsIAzMwA19cA4eEA6+sA9fUA//8A"),
        new(L.Nameplate.GradientBlackGreen,
            "AP8AAPUAAOsAAOEAANcAAMwAAMIAALgAAK4AAKQAAJoAAJAAAIYAAHsAAHEAAGcAAF0AAFMAAEkAAD8AADUAACsAACAAABYAAAwAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAwAABYAACAAACoAADUAAD8AAEkAAFMAAF0AAGcAAHEAAHsAAIYAAJAAAJoAAKQAAK4AALgAAMIAAMwAANcAAOEAAOsAAPUAAP8A"),
        new(L.Nameplate.GradientBlackPink,
            "/wD/9QD16wDr4QDh1wDXzADMwgDCuAC4rgCupACkmgCakACQhgCGewB7cQBxZwBnXQBdUwBTSQBJPwA/NQA1KwArIAAgFgAWDAAMAgACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgACDAAMFgAWIAAgKgAqNQA1PwA/SQBJUwBTXQBdZwBncQBxewB7hgCGkACQmgCapACkrgCuuAC4wgDCzADM1wDX4QDh6wDr9QD1/wD/"),
        new(L.Nameplate.GradientBlackCyan,
            "AP//APX1AOvrAOHhANfXAMzMAMLCALi4AK6uAKSkAJqaAJCQAIaGAHt7AHFxAGdnAF1dAFNTAElJAD8/ADU1ACsrACAgABYWAAwMAAICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAICAAwMABYWACAgACoqADU1AD8/AElJAFNTAF1dAGdnAHFxAHt7AIaGAJCQAJqaAKSkAK6uALi4AMLCAMzMANfXAOHhAOvrAPX1AP//"),
        new(L.Nameplate.GradientCherryBlossom,
            "7s7/7Mj36sPv573n5bje47LW4azO3qbG3KC+2pq32pW13pG84YzD5YjK6ITR7H/Y8Hvg83bm93Lt+m30/Wn5/Wf3+Wbt9GXj72Ta6mPQ5mLH4mK+3mG02mCq1l+h0l6Yzl2Oyl2GymCEzmaI0WyM1HOP13mT2n6X34Sb4oqf5ZCi6Jam65up76Gt8qex9a60+bS4/Lq8/r/A/cHF+8LK+sPQ+cXV98bb9sfg9Mjm88rs8svy8Mz378387s7/7s7/"),
        new(L.Nameplate.GradientGolden,
            "/5IA/5QE/5YI/5kL/5sP/50T/58X/6Eb/6Mf/6Yj/6gn/6or/6wv/68z/7E2/7M6/7Y+/7hC/7pG/71J/79N/8FR/8NV/8VZ/8dd/8ph/8xl/85p/9Jz/9mJ/+Cl/+a1/+uu/+2c/++L/+2D/+p+/+Z5/+N0/+Bw/9xr/9lm/9Vh/9Jc/89X/8tS/8hN/8VI/8FE/74//7s6/7c1/7Qx/7As/60n/6oi/6Yd/6MY/58T/5wO/5kK/5UF/5IA/5IA"),
        new(L.Nameplate.GradientPastelRainbow,
            "/7y8/8K8/8i8/868/9S8/9q8/+G8/+e8/+28//O8//m8/v68+f+88/+87f+86P+84f+82/+81f+8z/+8yf+8w/+8vf+8vP/BvP/HvP/NvP/TvP/avP/gvP/mvP/svP/yvP/4vP//vPn/vPP/vOz/vOX/vN//vNj/vNL/vMz/vMX/vL//v7z/xrz/zLz/0rz/2rz/4Lz/5rz/7bz/87z/+rz//7z+/7z4/7zx/7zr/7zk/7ze/7zX/7zR/7zK/7y8"),
        new(L.Nameplate.GradientDarkRainbow,
            "MgAAMgUAMgkAMg4AMhIAMhcAMhsAMiAAMiUAMioAMi4AMTIALTIAKDIAJDIAHzIAGjIAFTIAETIADDIABzIAAzIAADICADIGADILADIQADIUADIZADIeADIiADInADIrADIwAC8yACsyACYyACEyABwyABgyABMyAA0yAAkyAAQyAQEyBQAyCgAyDwAyEwAyGQAyHgAyIgAyJwAyLAAyMQAyMgAvMgAqMgAlMgAgMgAbMgAWMgASMgANMgAAMgAA"),
        new(L.Nameplate.GradientNonBinary,
            "//Qz//VK//Zg//h3//mO//qk//u7//3S//7o////9O366dr13sjv07Xqx6PlvJDgsX7apmvVm1nQik+5eUWiZzuLVjF0RShcNB5FIhQuEQoXAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEQoXIhQuNB5FRShcVjF0ZzuLeUWiik+5m1nQpmvVsX7avJDgx6Pl07Xq3sjv6dr19O36//////7o//3S//u7//qk//mO//h3//Zg//VK//Qz"),
    };

    public static bool IsValid(int gradient) => gradient == TwoColors || (uint)gradient < (uint)All.Length;

    public static LocString Label(int gradient) =>
        gradient == TwoColors ? L.Nameplate.GradientTwoColors : All[gradient].Label;

    public static long Tick(long milliseconds) => milliseconds / StepMilliseconds;

    public static int StepCount(int gradient) => gradient == TwoColors ? TwoColorSteps : All[gradient].Count;

    public static Vector3 Step(int gradient, Vector3 first, Vector3 second, int step) =>
        gradient == TwoColors
            ? TwoColorAt(first, second, step / (TwoColorSteps - 1f))
            : All[gradient].Step(step);

    public static Vector3 Blend(int gradient, Vector3 first, Vector3 second, float position)
    {
        var count = StepCount(gradient);
        var wrapped = position % count;
        if (wrapped < 0f)
        {
            wrapped += count;
        }

        var lower = (int)wrapped;
        var upper = (lower + 1) % count;
        return Vector3.Lerp(Step(gradient, first, second, lower % count), Step(gradient, first, second, upper),
            wrapped - lower);
    }

    public static bool TryGlyphGlow(in TitleLook look, int glyph, int length, long tick, out Vector3 glow)
    {
        glow = default;
        if (look.GradientColourSet is not { } gradient || !IsValid(gradient) ||
            (gradient == TwoColors && (look.Glow is null || look.Color3 is null)))
        {
            return false;
        }

        var count = StepCount(gradient);
        var character = length > PairedLength ? glyph & ~1 : glyph;
        var step = (NameplateGradientMotion?)look.GradientAnimationStyle switch
        {
            NameplateGradientMotion.Pulse => tick,
            NameplateGradientMotion.Wave => tick + character,
            _ => (long)MathF.Round(character / (float)Math.Max(length, 1) * count),
        };
        glow = Step(gradient, look.Glow ?? default, look.Color3 ?? default, (int)(step % count));
        return true;
    }

    private static Vector3 TwoColorAt(Vector3 first, Vector3 second, float position)
    {
        var folded = position > 0.5f ? 1f - position : position;
        if (folded <= TwoColorHoldEnd)
        {
            return first;
        }

        if (folded >= TwoColorBlendEnd)
        {
            return second;
        }

        return Vector3.Lerp(first, second, (folded - TwoColorHoldEnd) / (TwoColorBlendEnd - TwoColorHoldEnd));
    }
}
