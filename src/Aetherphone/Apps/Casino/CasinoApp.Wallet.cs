using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private void DrawCashierTab(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.cashier"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var bottom = cashier.Panel.Draw(drawList, ui, origin.X, origin.Y, width, scale, !cashier.IsOpen, false);
            CoinArt.Reserve(origin, width, bottom + CoinArt.BottomPad * scale);
        }
    }
}
