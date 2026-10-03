namespace Aetherphone.Core.Media;

internal static class IconLadder
{
    public static int Bit(int level) => 1 << (level - 1);

    public static bool IsResident(int residentLevels, int level) => (residentLevels & Bit(level)) != 0;

    public static int Nearest(int residentLevels, int level)
    {
        for (var above = level; above <= TextureSizes.LevelCount; above++)
        {
            if (IsResident(residentLevels, above))
            {
                return above;
            }
        }

        for (var below = level - 1; below > TextureSizes.Native; below--)
        {
            if (IsResident(residentLevels, below))
            {
                return below;
            }
        }

        return TextureSizes.Native;
    }
}
