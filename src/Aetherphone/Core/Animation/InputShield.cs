using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Animation;

internal readonly ref struct InputShield
{
    private static readonly Vector2 OffScreen = new(-100000f, -100000f);
    private readonly Vector2 saved;
    private readonly bool active;

    private InputShield(Vector2 saved, bool active)
    {
        this.saved = saved;
        this.active = active;
    }

    public static InputShield Engage(bool active = true)
    {
        if (!active)
        {
            return new InputShield(default, false);
        }

        var io = ImGui.GetIO();
        var shield = new InputShield(io.MousePos, true);
        io.MousePos = OffScreen;
        return shield;
    }

    public void Dispose()
    {
        if (active)
        {
            ImGui.GetIO().MousePos = saved;
        }
    }
}
