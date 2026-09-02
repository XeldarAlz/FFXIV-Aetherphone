using System.Runtime.InteropServices;

namespace Aetherphone.Harness.Rendering;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct DrawVertex
{
    public readonly Vector2 Position;
    public readonly Vector2 Uv;
    public readonly uint Color;
}
