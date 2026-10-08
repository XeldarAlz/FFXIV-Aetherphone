using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal enum BalanceTitle : byte
{
    None,
    Shark,
    HighRoller,
    Vip,
    Whale,
    Legend,
}

internal static class StatusTitle
{
    public const long Shark = 1_000_000;
    public const long HighRoller = 10_000_000;
    public const long Vip = 100_000_000;
    public const long Whale = 1_000_000_000;
    public const long Legend = 1_000_000_000_000;

    private const float PaddingX = 9f;
    private const float Height = 20f;

    private static readonly long[] Thresholds = { Legend, Whale, Vip, HighRoller, Shark };
    private static readonly BalanceTitle[] Titles =
    {
        BalanceTitle.Legend, BalanceTitle.Whale, BalanceTitle.Vip, BalanceTitle.HighRoller, BalanceTitle.Shark,
    };

    private static readonly Vector4[] Tints =
    {
        new(0f, 0f, 0f, 0f),
        new(0.18f, 0.90f, 1.00f, 1f),
        new(1.00f, 0.79f, 0.29f, 1f),
        new(1.00f, 0.24f, 0.60f, 1f),
        new(0.55f, 0.62f, 1.00f, 1f),
        new(1.00f, 0.88f, 0.54f, 1f),
    };

    public static BalanceTitle For(long balance)
    {
        for (var index = 0; index < Thresholds.Length; index++)
        {
            if (balance >= Thresholds[index])
            {
                return Titles[index];
            }
        }

        return BalanceTitle.None;
    }

    public static LocString Label(BalanceTitle title) => title switch
    {
        BalanceTitle.Shark => L.Strip.TitleShark,
        BalanceTitle.HighRoller => L.Strip.TitleHighRoller,
        BalanceTitle.Vip => L.Strip.TitleVip,
        BalanceTitle.Whale => L.Strip.TitleWhale,
        BalanceTitle.Legend => L.Strip.TitleLegend,
        _ => default,
    };

    public static Vector4 Tint(BalanceTitle title) => Tints[(int)title];

    public static float Width(BalanceTitle title, float maxWidth, float scale)
    {
        if (title == BalanceTitle.None)
        {
            return 0f;
        }

        var text = Loc.T(Label(title));
        var size = Typography.Measure(text, TextStyles.Caption2);
        return MathF.Min(maxWidth, size.X + PaddingX * 2f * scale);
    }

    public static float Draw(ImDrawListPtr drawList, Vector2 center, BalanceTitle title, float maxWidth, float scale)
    {
        if (title == BalanceTitle.None)
        {
            return 0f;
        }

        var tint = Tint(title);
        var width = Width(title, maxWidth, scale);
        var half = new Vector2(width, Height * scale) * 0.5f;
        var min = center - half;
        var max = center + half;
        Squircle.Fill(drawList, min, max, half.Y, ImGui.GetColorU32(tint with { W = 0.18f }));
        Squircle.Stroke(drawList, min, max, half.Y, ImGui.GetColorU32(tint with { W = 0.75f }), MathF.Max(1f, scale));
        var text = Typography.FitText(Loc.T(Label(title)), width - PaddingX * 2f * scale, TextStyles.Caption2);
        Typography.DrawCentered(drawList, center, text, tint, TextStyles.Caption2);
        return width;
    }
}
