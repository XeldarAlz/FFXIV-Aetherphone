namespace Aetherphone.Core.Emulation;

[Flags]
internal enum EmulatorButtons : ushort
{
    None = 0,
    B = 1 << 0,
    Y = 1 << 1,
    Select = 1 << 2,
    Start = 1 << 3,
    Up = 1 << 4,
    Down = 1 << 5,
    Left = 1 << 6,
    Right = 1 << 7,
    A = 1 << 8,
    X = 1 << 9,
    L = 1 << 10,
    R = 1 << 11,
    L2 = 1 << 12,
    R2 = 1 << 13,
    L3 = 1 << 14,
    R3 = 1 << 15,
}

internal readonly record struct EmulatorInputState(
    EmulatorButtons Buttons,
    short LeftX = 0,
    short LeftY = 0,
    short RightX = 0,
    short RightY = 0);
