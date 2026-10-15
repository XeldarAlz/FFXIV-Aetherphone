namespace Aetherphone.Core.Honorific;

internal static class NameplatePalette
{
    public static readonly Vector3 DarkGlow = new(0.04f, 0.10f, 0.16f);

    public static readonly Vector3[] Colors =
    {
        new(1.00f, 1.00f, 1.00f),
        new(1.00f, 0.86f, 0.55f),
        new(1.00f, 0.62f, 0.38f),
        new(0.98f, 0.42f, 0.52f),
        new(0.86f, 0.55f, 1.00f),
        new(0.55f, 0.66f, 1.00f),
        new(0.42f, 0.84f, 1.00f),
        new(0.45f, 0.92f, 0.62f),
        new(0.80f, 0.80f, 0.84f),
        DarkGlow,
    };

    private static readonly uint[] Packed = PackAll(Colors);

    public static readonly uint White = Packed[0];
    public static readonly uint DarkGlowPacked = Pack(DarkGlow);
    public static readonly uint Lilac = Packed[4];
    public static readonly uint Sky = Packed[6];

    public static int IndexOf(uint packed) => Array.IndexOf(Packed, packed);

    public static uint PackedAt(int index) => Packed[Math.Clamp(index, 0, Packed.Length - 1)];

    public static uint Pack(Vector3 color) => (Channel(color.X) << 16) | (Channel(color.Y) << 8) | Channel(color.Z);

    public static uint Pack(Vector4 color) => Pack(new Vector3(color.X, color.Y, color.Z));

    public static Vector3 Unpack(uint packed) =>
        new(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f);

    private static uint Channel(float value) => (uint)Math.Clamp(MathF.Round(value * 255f), 0f, 255f);

    private static uint[] PackAll(Vector3[] colors)
    {
        var packed = new uint[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            packed[index] = Pack(colors[index]);
        }

        return packed;
    }
}
