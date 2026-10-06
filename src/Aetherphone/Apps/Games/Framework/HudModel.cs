using Aetherphone.Core;
using Aetherphone.Core.Localization;

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
    SecondCustom,
}

internal sealed class HudModel
{
    public const int MaxSecondary = 4;
    public const int MaxHearts = 5;
    public const int CustomSlots = 2;
    private const int SlotCount = (int)HudSlot.SecondCustom + 1;

    private readonly HudSlot[] visible = new HudSlot[MaxSecondary];
    private readonly Rect[] slotRects = new Rect[SlotCount];
    private readonly float[] customWidths = new float[CustomSlots];
    private readonly int[] customPlacedGeneration = new int[CustomSlots];
    private int visibleCount;
    private int customCount;
    private int generation = 1;
    private HudStyle visibleStyle;
    private bool visibleDirty = true;

    public bool HasScore { get; private set; }

    public int ScoreValue { get; private set; }

    public LocString? ScoreLabel { get; private set; }

    public bool HasTimer { get; private set; }

    public float TimerLeft { get; private set; }

    public float TimerTotal { get; private set; }

    public bool TimerUrgent { get; private set; }

    public bool TimerElapsed { get; private set; }

    public bool HasLives { get; private set; }

    public int LivesLeft { get; private set; }

    public int LivesMax { get; private set; }

    public bool HasLevel { get; private set; }

    public int LevelValue { get; private set; }

    public bool HasCombo { get; private set; }

    public ComboMeter ComboValue { get; private set; }

    public bool HasBest { get; private set; }

    public int BestValue { get; private set; }

    public bool HasCustom => customCount > 0;

    public int CustomCount => customCount;

    public void Clear()
    {
        HasScore = false;
        ScoreLabel = null;
        HasTimer = false;
        HasLives = false;
        HasLevel = false;
        HasCombo = false;
        HasBest = false;
        customCount = 0;
        generation++;
        visibleDirty = true;
    }

    public void Score(int value)
    {
        HasScore = true;
        ScoreValue = value;
        ScoreLabel = null;
    }

    public void Score(int value, LocString label)
    {
        HasScore = true;
        ScoreValue = value;
        ScoreLabel = label;
    }

    public void Timer(float left, float total, bool urgent)
    {
        HasTimer = true;
        TimerLeft = MathF.Max(0f, left);
        TimerTotal = MathF.Max(0f, total);
        TimerUrgent = urgent;
        TimerElapsed = false;
        visibleDirty = true;
    }

    public void Clock(float seconds)
    {
        HasTimer = true;
        TimerLeft = MathF.Max(0f, seconds);
        TimerTotal = 0f;
        TimerUrgent = false;
        TimerElapsed = true;
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
        if (width <= 0f || customCount >= CustomSlots)
        {
            return;
        }

        customWidths[customCount++] = width;
        visibleDirty = true;
    }

    public float CustomWidth(int index) => index >= 0 && index < customCount ? customWidths[index] : 0f;

    public Rect CustomRect(int index) => SlotRect(CustomSlot(index));

    public bool CustomPlaced(int index) =>
        index >= 0 && index < CustomSlots && customPlacedGeneration[index] == generation - 1;

    public Rect SlotRect(HudSlot slot) => slotRects[(int)slot];

    public void PlaceSlot(HudSlot slot, Rect rect)
    {
        slotRects[(int)slot] = rect;
        if (slot == HudSlot.Custom)
        {
            customPlacedGeneration[0] = generation;
        }
        else if (slot == HudSlot.SecondCustom)
        {
            customPlacedGeneration[1] = generation;
        }
    }

    public static HudSlot CustomSlot(int index) => index == 0 ? HudSlot.Custom : HudSlot.SecondCustom;

    public static int CustomIndex(HudSlot slot) => slot == HudSlot.SecondCustom ? 1 : 0;

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
                     (HasBest ? 1 : 0) + customCount;
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
        Append(HudSlot.Custom, customCount > 0, limit);
        Append(HudSlot.SecondCustom, customCount > 1, limit);
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
