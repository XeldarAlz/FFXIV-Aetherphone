using Aetherphone.Core.Notifications;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameSfx
{
    public static void CountdownTick() => UiFeedback.Play(UiSound.GameTick);

    public static void ComboTierUp() => UiFeedback.Play(UiSound.GamePowerUp);

    public static void NewBest() => UiFeedback.Play(UiSound.GameWin);

    public static void LevelClear() => UiFeedback.Play(UiSound.GameClear);
}
