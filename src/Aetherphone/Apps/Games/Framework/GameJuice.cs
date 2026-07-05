using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameJuice
{
    public const float EntranceSpeed = 1.35f;

    public static float Stagger(float progress, int index, int count, float overlap = 0.55f)
    {
        if (progress >= 1f)
        {
            return 1f;
        }

        if (progress <= 0f)
        {
            return 0f;
        }

        var lastIndex = count > 1 ? count - 1 : 1;
        var start = (1f - overlap) * (index / (float)lastIndex);
        var local = (progress - start) / overlap;
        if (local <= 0f)
        {
            return 0f;
        }

        if (local >= 1f)
        {
            return 1f;
        }

        return local;
    }

    public static float PopIn(float progress)
    {
        return Easing.EaseOutBack(progress);
    }

    public static float Advance(float progress, float deltaSeconds, float speed = EntranceSpeed)
    {
        if (progress >= 1f)
        {
            return 1f;
        }

        return MathF.Min(1f, progress + deltaSeconds * speed);
    }
}
