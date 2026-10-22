using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Harness.Fonts;

internal sealed class FakeLockedFont : ILockedImFont
{
    public FakeLockedFont(ImFontPtr font)
    {
        ImFont = font;
    }

    public ImFontPtr ImFont { get; }

    public ILockedImFont NewRef() => new FakeLockedFont(ImFont);

    public void Dispose()
    {
    }
}
