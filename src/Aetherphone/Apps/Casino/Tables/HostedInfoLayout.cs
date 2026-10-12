using Aetherphone.Apps.Casino.Stage;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HostedInfoMode : byte
{
    Blocks,
    Beside,
    Line,
    Hidden,
}

internal static class HostedInfoLayout
{
    public const int MaxLines = 3;
    public const float MinimumBesideWidth = 96f;

    public static bool FitsLine(float textWidth, float free, float scale) =>
        free > 0f && StageText.Fits(textWidth + StageText.CapsulePadX * 2f * scale, free);

    public static float BlockHeight(float lineHeight, int lines, float scale) =>
        MathF.Max(StageText.MinimumCapsuleHeight * scale, lineHeight * lines + StageText.CapsulePadY * 2f * scale);

    public static HostedInfoMode Mode(float blocksHeight, float lineHeight, float available, float besideWidth,
        float scale)
    {
        if (blocksHeight <= available)
        {
            return HostedInfoMode.Blocks;
        }

        if (besideWidth >= MinimumBesideWidth * scale)
        {
            return HostedInfoMode.Beside;
        }

        return lineHeight <= available ? HostedInfoMode.Line : HostedInfoMode.Hidden;
    }

    public static float ButtonSqueeze(float naturalWidth, int buttons, float rowWidth, float gap)
    {
        var available = rowWidth - gap * MathF.Max(0, buttons - 1);
        if (naturalWidth <= 0f || naturalWidth <= available)
        {
            return 1f;
        }

        return MathF.Max(0f, available) / naturalWidth;
    }
}
