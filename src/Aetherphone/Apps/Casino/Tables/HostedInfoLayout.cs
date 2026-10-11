using Aetherphone.Apps.Casino.Stage;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HostedInfoLayout
{
    public const int MaxLines = 3;

    public static bool FitsLine(float textWidth, float free, float scale) =>
        free > 0f && StageText.Fits(textWidth + StageText.CapsulePadX * 2f * scale, free);

    public static float BlockHeight(float lineHeight, int lines, float scale) =>
        MathF.Max(StageText.MinimumCapsuleHeight * scale, lineHeight * lines + StageText.CapsulePadY * 2f * scale);
}
