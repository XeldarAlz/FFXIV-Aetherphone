using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class MinimizedPreview
{
    private MinimizedPhone? phone;

    public void Bind(MinimizedPhone demoPhone) => phone = demoPhone;

    public void Draw(ImDrawListPtr drawList, Rect body, float delta) => phone?.DrawPreview(drawList, body, delta);

    public void Show(MinimizedPart part) => phone?.ShowPart(part);
}
