using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Machines;

internal enum MachineExtra : byte
{
    Turbo,
    Ante,
    Buy,
    Skip,
    Gamble,
}

internal static class MachineExtrasLayout
{
    public const int MaxPills = 3;
    public const float Gap = DeckActions.Gap;
    public const float PadX = 12f;
    public const float IconGap = DeckActions.IconGap;
    public const float MinimumWidth = DeckActions.PillMinWidth;
    public const float FitSlack = 0.5f;
    public const float IconShare = 0.9f;

    public static readonly TextStyle LabelStyle = TextStyles.SubheadlineEmphasized;

    public static bool HasIcon(MachineExtra kind) => kind is MachineExtra.Turbo or MachineExtra.Skip
        or MachineExtra.Gamble;

    public static int Pick(Span<MachineExtra> kinds, bool cascade, bool skippable, bool gambleOffered)
    {
        var count = 0;
        kinds[count++] = MachineExtra.Turbo;
        if (skippable)
        {
            kinds[count++] = MachineExtra.Skip;
            return count;
        }

        if (gambleOffered)
        {
            kinds[count++] = MachineExtra.Gamble;
            return count;
        }

        if (cascade)
        {
            kinds[count++] = MachineExtra.Ante;
            kinds[count++] = MachineExtra.Buy;
        }

        return count;
    }

    public static bool Skippable(MachineRoundPlayback playback)
    {
        return playback.HasRound && playback.Active && (playback.InFeature || playback.InHold);
    }

    public static float PillWidth(float labelWidth, float iconWidth, float scale)
    {
        var content = labelWidth + (iconWidth > 0f ? iconWidth + IconGap * scale : 0f);
        return MathF.Max(MinimumWidth * scale, MathF.Ceiling(content + PadX * 2f * scale));
    }

    public static float ContentWidth(ReadOnlySpan<float> widths, float scale)
    {
        var total = 0f;
        for (var index = 0; index < widths.Length; index++)
        {
            total += widths[index] + (index > 0 ? Gap * scale : 0f);
        }

        return total;
    }

    public static bool Fits(Rect row, ReadOnlySpan<float> widths, float scale)
    {
        return ContentWidth(widths, scale) <= row.Width + FitSlack;
    }

    public static void Place(Rect row, ReadOnlySpan<float> widths, float offset, float scale, Span<Rect> rects)
    {
        var left = row.Min.X - offset;
        for (var index = 0; index < widths.Length && index < rects.Length; index++)
        {
            rects[index] = new Rect(new Vector2(left, row.Min.Y), new Vector2(left + widths[index], row.Max.Y));
            left += widths[index] + Gap * scale;
        }
    }
}
