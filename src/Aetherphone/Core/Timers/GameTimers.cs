using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Runtime;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using RetainerTask = Lumina.Excel.Sheets.RetainerTask;

namespace Aetherphone.Core.Timers;

internal sealed class GameTimers : IDisposable
{
    private const long TickIntervalMilliseconds = 1000;
    private const int CaptureEveryTicks = 5;
    private const int MaxRetainers = 10;
    private const int MaxVessels = 8;
    private const long StampSaveSeconds = 600;

    private readonly Configuration configuration;
    private readonly CharacterWatch characterWatch;
    private readonly GameData gameData;
    private readonly IDataManager data;
    private readonly FrameworkTicker ticker;
    private readonly AppGate gate;
    private readonly CapturedRetainer[] retainerBuffer = new CapturedRetainer[MaxRetainers];
    private readonly CapturedVessel[] vesselBuffer = new CapturedVessel[MaxVessels];
    private ExcelSheet<RetainerTask>? tasks;
    private int ticksUntilCapture;
    private long lastSaveUnix;
    private bool stampsMoved;

    public GameTimers(Configuration configuration, IFramework framework, CharacterWatch characterWatch,
        GameData gameData, IDataManager data, AppGate gate)
    {
        this.configuration = configuration;
        this.characterWatch = characterWatch;
        this.gameData = gameData;
        this.data = data;
        this.gate = gate;
        ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
    }

    public List<TimerCharacterRecord> Characters => configuration.TimerCharacters;
    public List<TimerWorkshopRecord> Workshops => configuration.TimerWorkshops;
    public string RegionCode => configuration.TimerRegionCode;
    public ulong CurrentContentId { get; private set; }
    public bool Enabled => gate.Open;

    public void Dispose()
    {
        ticker.Dispose();
    }

    private void OnTick()
    {
        CurrentContentId = characterWatch.CurrentContentId;
        if (CurrentContentId == 0)
        {
            return;
        }

        if (ticksUntilCapture > 0)
        {
            ticksUntilCapture--;
            return;
        }

        ticksUntilCapture = CaptureEveryTicks - 1;
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var name = player.Name.TextValue;
        var world = player.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var changed = CaptureRegion();
        changed |= CaptureRetainers(name, world, nowUnix);
        changed |= CaptureVessels(player.CompanyTag.TextValue, world, nowUnix);
        changed |= CaptureMapAllowance(name, world, nowUnix);
        if (!changed && (!stampsMoved || nowUnix - lastSaveUnix < StampSaveSeconds))
        {
            return;
        }

        lastSaveUnix = nowUnix;
        stampsMoved = false;
        configuration.Save();
    }

    private bool CaptureMapAllowance(string name, string world, long nowUnix)
    {
        var allowance = GameTimerCapture.ReadMapAllowance();
        stampsMoved |= allowance > 0;
        return TimerLedger.ApplyMapAllowance(Characters, CurrentContentId, name, world, allowance, nowUnix);
    }

    private bool CaptureRegion()
    {
        var region = gameData.LocalRegionCode();
        if (region.Length == 0 || string.Equals(region, configuration.TimerRegionCode, StringComparison.Ordinal))
        {
            return false;
        }

        configuration.TimerRegionCode = region;
        return true;
    }

    private bool CaptureRetainers(string name, string world, long nowUnix)
    {
        tasks ??= data.GetExcelSheet<RetainerTask>();
        var count = GameTimerCapture.ReadRetainers(retainerBuffer, tasks);
        stampsMoved |= count > 0;
        return count > 0 && TimerLedger.ApplyRetainers(Characters, CurrentContentId, name, world,
            retainerBuffer.AsSpan(0, count), nowUnix);
    }

    private bool CaptureVessels(string company, string world, long nowUnix)
    {
        var count = GameTimerCapture.ReadVessels(vesselBuffer);
        if (count <= 0)
        {
            return false;
        }

        stampsMoved = true;
        var key = TimerLedger.WorkshopKey(company, world, CurrentContentId);
        return TimerLedger.ApplyVessels(Workshops, key, company, world, vesselBuffer.AsSpan(0, count), nowUnix);
    }
}
