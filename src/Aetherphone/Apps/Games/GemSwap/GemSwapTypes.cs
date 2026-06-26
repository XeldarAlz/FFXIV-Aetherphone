namespace Aetherphone.Apps.Games.GemSwap;

internal enum GemSpecial : byte
{
    None,
    LineHorizontal,
    LineVertical,
    Bomb,
}

internal enum GemPhase : byte
{
    Idle,
    Swapping,
    SwapBack,
    Clearing,
    Falling,
}
