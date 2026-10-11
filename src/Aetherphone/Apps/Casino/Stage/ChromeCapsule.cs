using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal enum CapsuleCoins : byte
{
    None,
    Compact,
    Full,
}

internal readonly struct ChromeCapsule
{
    public readonly Rect Rect;
    public readonly CapsuleCoins Coins;
    public readonly float TextRoom;

    private ChromeCapsule(Rect rect, CapsuleCoins coins, float textRoom)
    {
        Rect = rect;
        Coins = coins;
        TextRoom = textRoom;
    }

    public static ChromeCapsule Fit(Vector2 center, float height, float maxWidth, float chrome, float text,
        float fullCoins, float compactCoins, float coinGap)
    {
        var core = chrome + text;
        var coins = CapsuleCoins.None;
        var width = core;
        if (fullCoins > 0f && core + coinGap + fullCoins <= maxWidth)
        {
            coins = CapsuleCoins.Full;
            width = core + coinGap + fullCoins;
        }
        else if (compactCoins > 0f && core + coinGap + compactCoins <= maxWidth)
        {
            coins = CapsuleCoins.Compact;
            width = core + coinGap + compactCoins;
        }

        width = MathF.Max(0f, MathF.Min(width, maxWidth));
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var trailing = coins switch
        {
            CapsuleCoins.Full => coinGap + fullCoins,
            CapsuleCoins.Compact => coinGap + compactCoins,
            _ => 0f,
        };
        return new ChromeCapsule(new Rect(center - half, center + half), coins,
            MathF.Max(0f, width - chrome - trailing));
    }
}
