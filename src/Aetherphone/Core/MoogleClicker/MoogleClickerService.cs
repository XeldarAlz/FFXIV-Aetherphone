using Aetherphone.Core.Home;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.MoogleClicker;

internal sealed class MoogleClickerService : IDisposable
{
    private const long TickMilliseconds = 1000;
    private const int IdleSaveTicks = 300;
    private const int DirtySaveTicks = 5;

    private readonly Configuration configuration;
    private readonly KupoWorkshop workshop = new();
    private readonly object gate = new();
    private readonly FrameworkTicker ticker;
    private int ticksSinceSave;
    private bool changed;
    private bool dirty;
    private bool saveNow;

    public MoogleClickerService(Configuration configuration, IFramework framework, AppGate appGate)
    {
        this.configuration = configuration;
        workshop.Load(configuration.MoogleClicker);
        workshop.Advance(NowUnixMilliseconds);
        ticker = new FrameworkTicker(framework, TickMilliseconds, Tick, appGate);
    }

    public static long NowUnixMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public KupoWorkshop Workshop => workshop;

    public double Tap(double multiplier)
    {
        lock (gate)
        {
            changed = true;
            return workshop.Tap(NowUnixMilliseconds, multiplier);
        }
    }

    public int Buy(int building, int count)
    {
        lock (gate)
        {
            var bought = count > 0 ? workshop.Buy(building, count) : workshop.BuyMax(building);
            dirty |= bought > 0;
            return bought;
        }
    }

    public bool BuyUpgrade(int upgrade)
    {
        lock (gate)
        {
            var bought = workshop.BuyUpgrade(upgrade);
            dirty |= bought;
            return bought;
        }
    }

    public double Reward(KupoReward reward)
    {
        lock (gate)
        {
            dirty = true;
            return workshop.Reward(reward, NowUnixMilliseconds);
        }
    }

    public double CloseLedger()
    {
        lock (gate)
        {
            var gained = workshop.CloseLedger();
            saveNow |= gained > 0d;
            return gained;
        }
    }

    public void DismissAway()
    {
        lock (gate)
        {
            dirty |= workshop.HasAwayReport;
            workshop.DismissAway();
        }
    }

    public void Dispose()
    {
        ticker.Dispose();
        lock (gate)
        {
            workshop.Advance(NowUnixMilliseconds);
            workshop.Store(configuration.MoogleClicker);
        }
    }

    private void Tick()
    {
        lock (gate)
        {
            var advance = workshop.Advance(NowUnixMilliseconds);
            changed |= advance.Kupo > 0d;
            ticksSinceSave++;
            var due = saveNow || (dirty && ticksSinceSave >= DirtySaveTicks) ||
                      (changed && ticksSinceSave >= IdleSaveTicks);
            if (!due)
            {
                return;
            }

            workshop.Store(configuration.MoogleClicker);
            ticksSinceSave = 0;
            changed = false;
            dirty = false;
            saveNow = false;
        }

        configuration.Save();
    }
}
