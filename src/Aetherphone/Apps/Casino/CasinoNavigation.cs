using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Casino;

internal sealed class CasinoNavigation
{
    private readonly ViewRouter<CasinoRoute> router;

    public CasinoNavigation(ViewRouter<CasinoRoute> router)
    {
        this.router = router;
    }

    public CasinoTab Tab { get; private set; }

    public CasinoRoute Current => router.Current;

    public int Depth => router.Depth;

    public bool OnRoot => router.Depth <= 1;

    public void Reset()
    {
        router.Reset();
        Tab = CasinoTab.Floor;
    }

    public void Select(CasinoTab tab)
    {
        if (!OnRoot)
        {
            router.Reset();
        }

        Tab = tab;
    }

    public bool Push(CasinoRoute route)
    {
        if (Same(router.Current, route))
        {
            return false;
        }

        router.Push(route);
        return true;
    }

    public bool Back() => router.Pop();

    public void Launch(CasinoRoute target)
    {
        router.Reset();
        Tab = CasinoTab.Floor;
        if (target.Screen == CasinoScreen.Floor)
        {
            return;
        }

        router.Push(target, false);
    }

    public bool Holds(string tableId)
    {
        return tableId.Length > 0 && LeavesWith(router.Current, tableId);
    }

    public static bool LeavesWith(CasinoRoute route, string tableId)
    {
        return route.Screen is CasinoScreen.TableDoor or CasinoScreen.Table or CasinoScreen.TableLedger
                   or CasinoScreen.Broadcast or CasinoScreen.VenueRoom
               && string.Equals(route.TableId, tableId, StringComparison.Ordinal);
    }

    public static bool Same(CasinoRoute held, CasinoRoute next)
    {
        return held.Screen == next.Screen
               && string.Equals(held.GameId, next.GameId, StringComparison.Ordinal)
               && string.Equals(held.TableId, next.TableId, StringComparison.Ordinal)
               && string.Equals(held.RoundId, next.RoundId, StringComparison.Ordinal);
    }
}
