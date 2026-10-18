namespace Aetherphone.Apps.Casino;

internal readonly record struct CasinoLaunchWait(string TableId, bool Ledger, CasinoRoute From, int Depth, CasinoTab Tab)
{
    public bool Active => !string.IsNullOrEmpty(TableId);

    public bool Outlived(CasinoRoute current, int depth, CasinoTab tab) =>
        depth != Depth || tab != Tab || !CasinoNavigation.Same(current, From);

    public bool Answers(string tableId) => Active && string.Equals(TableId, tableId, StringComparison.Ordinal);
}
