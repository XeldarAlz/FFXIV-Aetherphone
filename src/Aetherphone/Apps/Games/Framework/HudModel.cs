using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework;

internal enum HudSlot : byte
{
    None,
    Timer,
    Lives,
    Level,
    Combo,
    Best,
    Custom,
}

internal sealed class HudModel
{
    public const int MaxSecondary = 4;
    public const int MaxHearts = 5;

    private readonly HudSlot[] visible = new HudSlot[MaxSecondary];
    private int visibleCount;
    private HudStyle visibleStyle;
    private bool visibleDirty = true;

    public bool HasScore { get; private set; }

    public int ScoreValue { get; private set; }

    public bool HasTimer { get; private set; }

    public float TimerLeft { get; private set; }

    public float TimerTotal { get; private set; }

    public bool TimerUrgent { get; private set; }

    public bool HasLives { get; private set; }

    public int LivesLeft { get; private set; }

    public int LivesMax { get; private set; }

    public bool HasLevel { get; private set; }

    public int LevelValue { get; private set; }

    public bool HasCombo { get; private set; }

    public ComboMeter ComboValue { get; private set; }

    public bool HasBest { get; private set; }

    public int BestValue { get; private set; }

    public bool HasCustom { get; private set; }

    public float CustomWidth { get; private set; }

    public Rect CustomRect { get; private set; }

    public void Clear()
    {
        HasScore = false;
        HasTimer = false;
        HasLives = false;
        HasLevel = false;
        HasCombo = false;
        HasBest = false;
        HasCustom = false;
        visibleDirty = true;
    }

    public void Score(int value)
    {
        HasScore = true;
        ScoreValue = value;
    }

    public void Timer(float left, float total, bool urgent)
    {
        HasTimer = true;
        TimerLeft = MathF.Max(0f, left);
        TimerTotal = MathF.Max(0f, total);
        TimerUrgent = urgent;
        visibleDirty = true;
    }

    public void Lives(int left, int max)
    {
        HasLives = true;
        LivesLeft = Math.Max(0, left);
        LivesMax = Math.Max(1, max);
        visibleDirty = true;
    }

    public void Level(int level)
    {
        HasLevel = true;
        LevelValue = level;
        visibleDirty = true;
    }

    public void Combo(in ComboMeter meter)
    {
        HasCombo = meter.Count >= 2;
        ComboValue = meter;
        visibleDirty = true;
    }

    public void Best(int value)
    {
        HasBest = value > 0;
        BestValue = value;
        visibleDirty = true;
    }

    public void Custom(float width)
    {
        HasCustom = width > 0f;
        CustomWidth = width;
        visibleDirty = true;
    }

    public void PlaceCustom(Rect rect)
    {
        CustomRect = rect;
    }

    public ReadOnlySpan<HudSlot> Visible(HudStyle style)
    {
        if (visibleDirty || visibleStyle != style)
        {
            Layout(style);
        }

        return visible.AsSpan(0, visibleCount);
    }

    private void Layout(HudStyle style)
    {
        visibleStyle = style;
        visibleDirty = false;
        var limit = style == HudStyle.Compact ? 1 : MaxSecondary;
        var wanted = (HasTimer ? 1 : 0) + (HasLives ? 1 : 0) + (HasLevel ? 1 : 0) + (HasCombo ? 1 : 0) +
                     (HasBest ? 1 : 0) + (HasCustom ? 1 : 0);
        var dropBest = wanted > limit && HasBest;
        if (dropBest)
        {
            wanted--;
        }

        var dropLevel = wanted > limit && HasLevel;
        visibleCount = 0;
        Append(HudSlot.Timer, HasTimer, limit);
        Append(HudSlot.Lives, HasLives, limit);
        Append(HudSlot.Level, HasLevel && !dropLevel, limit);
        Append(HudSlot.Combo, HasCombo, limit);
        Append(HudSlot.Best, HasBest && !dropBest, limit);
        Append(HudSlot.Custom, HasCustom, limit);
    }

    private void Append(HudSlot slot, bool present, int limit)
    {
        if (!present || visibleCount >= limit)
        {
            return;
        }

        visible[visibleCount++] = slot;
    }
}
