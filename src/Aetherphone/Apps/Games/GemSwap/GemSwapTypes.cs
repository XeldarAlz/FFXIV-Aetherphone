namespace Aetherphone.Apps.Games.GemSwap;

internal enum GemSpecial : byte
{
    None,
    LineHorizontal,
    LineVertical,
    Burst,
    Prism,
}

internal enum GemCombo : byte
{
    None,
    PrismColor,
    PrismBoard,
    Cross,
    WideCross,
    BigBurst,
}

internal enum GemPhase : byte
{
    Idle,
    Swapping,
    SwapBack,
    Clearing,
    Falling,
}

internal enum GemMode : byte
{
    Classic,
    Blitz,
}

internal enum GemStage : byte
{
    Ready,
    Playing,
    Finale,
    Over,
}

internal enum GemPower : byte
{
    Fire,
    Frost,
    Gale,
    Storm,
}
