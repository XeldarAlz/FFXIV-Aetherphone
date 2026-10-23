namespace Aetherphone.Apps.Music.Radio.Live;

internal readonly record struct RadioReactionTrayFit(float Left, float Width, float Slot);

internal static class RadioReactionTrayLayout
{
    public static RadioReactionTrayFit Fit(float areaLeft, float areaRight, float margin, float padding, int slots,
        float preferredSlot)
    {
        if (slots <= 0)
        {
            return new RadioReactionTrayFit((areaLeft + areaRight) * 0.5f - padding, padding * 2f, 0f);
        }

        var available = MathF.Max(0f, areaRight - areaLeft - margin * 2f - padding * 2f);
        var slot = MathF.Min(preferredSlot, available / slots);
        var width = slot * slots + padding * 2f;
        return new RadioReactionTrayFit((areaLeft + areaRight - width) * 0.5f, width, slot);
    }
}
