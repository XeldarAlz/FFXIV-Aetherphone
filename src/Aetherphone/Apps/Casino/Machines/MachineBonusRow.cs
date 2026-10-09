using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineBonusRow
{
    public const float TurboShare = 0.28f;

    public readonly Rect Turbo;
    public readonly Rect Ante;
    public readonly Rect Buy;
    public readonly Rect Info;

    private MachineBonusRow(Rect turbo, Rect ante, Rect buy, Rect info)
    {
        Turbo = turbo;
        Ante = ante;
        Buy = buy;
        Info = info;
    }

    public static MachineBonusRow Compute(Rect row, float gap)
    {
        var turbo = new Rect(row.Min, new Vector2(row.Min.X + row.Width * TurboShare, row.Max.Y));
        var restLeft = MathF.Min(row.Max.X, turbo.Max.X + gap);
        var info = new Rect(new Vector2(restLeft, row.Min.Y), row.Max);
        var half = MathF.Max(0f, (row.Max.X - restLeft - gap) * 0.5f);
        var ante = new Rect(new Vector2(restLeft, row.Min.Y), new Vector2(restLeft + half, row.Max.Y));
        var buyLeft = MathF.Min(row.Max.X, ante.Max.X + gap);
        var buy = new Rect(new Vector2(buyLeft, row.Min.Y), row.Max);
        return new MachineBonusRow(turbo, ante, buy, info);
    }
}
