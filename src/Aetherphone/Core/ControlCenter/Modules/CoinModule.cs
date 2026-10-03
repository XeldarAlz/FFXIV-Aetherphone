using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Core.ControlCenter.Modules;

internal sealed class CoinModule : IControlModule
{
    private const string DetailId = "cc.detail.coin";
    private static readonly ControlSpan[] SpanOptions = { ControlSpan.Small, ControlSpan.Wide };

    private readonly CoinStore coins;
    private readonly AethernetSession session;
    private readonly Action open;

    public CoinModule(CoinStore coins, AethernetSession session, INavigator navigation, Action dismiss)
    {
        this.coins = coins;
        this.session = session;
        open = () =>
        {
            navigation.Open("coin");
            dismiss();
        };
    }

    public string Id => "coin";
    public string GalleryLabel => Loc.T(L.Apps.Coin);
    public FontAwesomeIcon GalleryIcon => FontAwesomeIcon.Coins;
    public IReadOnlyList<ControlSpan> Sizes => SpanOptions;
    public ControlSpan DefaultSpan => ControlSpan.Small;

    public void Draw(in ControlModuleContext context)
    {
        var balance = coins.Wallet?.Balance ?? session.CurrentUser?.Coins ?? 0;
        var label = NumberText.Group(balance);
        var expanded = context.Expanded;
        if (ControlTile.Toggle(context.DrawList, expanded ? DetailId : Id, context.Rect, FontAwesomeIcon.Coins, label,
                false, AppAccents.For("coin"), context.Theme, context.Opacity, context.Interactive,
                context.Span != ControlSpan.Small || expanded))
        {
            open();
        }
    }
}
