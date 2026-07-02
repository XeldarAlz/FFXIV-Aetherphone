using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Device;

internal sealed class DeviceStatus : IDisposable
{
    private const int SampleIntervalMilliseconds = 2000;
    private const int PingTimeoutMilliseconds = 1500;
    private const int SampleWindow = 6;

    private static readonly IPAddress FallbackHost = IPAddress.Parse("1.1.1.1");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;

        public byte BatteryFlag;

        public byte BatteryLifePercent;

        public byte Reserved;

        public uint BatteryLifeTime;

        public uint BatteryFullLifeTime;
    }

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDataManager data;

    private readonly CancellationTokenSource cancellation = new();

    private readonly bool[] sampleSucceeded = new bool[SampleWindow];
    private readonly long[] sampleRoundtrip = new long[SampleWindow];
    private int sampleCursor;
    private int sampleCount;

    private volatile IPAddress target = FallbackHost;
    private uint resolvedWorldId;

    private volatile int batteryPercent = 100;
    private volatile bool batteryPresent;
    private volatile bool charging;
    private volatile int signalBars = 4;
    private volatile int latencyMilliseconds;
    private volatile int packetLossPercent;

    public DeviceStatus(IClientState clientState, IObjectTable objectTable, IDataManager data)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.data = data;
        _ = Task.Run(() => RunAsync(cancellation.Token));
    }

    public int BatteryPercent => batteryPercent;

    public bool BatteryPresent => batteryPresent;

    public bool Charging => charging;

    public int SignalBars => signalBars;

    public int LatencyMilliseconds => latencyMilliseconds;

    public int PacketLossPercent => packetLossPercent;

    public void SyncTarget()
    {
        var worldId = clientState.IsLoggedIn ? objectTable.LocalPlayer?.CurrentWorld.RowId ?? 0u : 0u;
        if (worldId == resolvedWorldId)
        {
            return;
        }

        resolvedWorldId = worldId;

        if (worldId != 0 && data.GetExcelSheet<World>().TryGetRow(worldId, out var world) && TryDataCenterHost(world.DataCenter.RowId, out var host))
        {
            target = host;
            return;
        }

        target = FallbackHost;
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var ping = new Ping();
        while (!token.IsCancellationRequested)
        {
            try
            {
                SampleBattery();
                await SampleNetworkAsync(ping).ConfigureAwait(false);
                await Task.Delay(SampleIntervalMilliseconds, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Transient probe failure; keep the loop alive for the next sample.
            }
        }
    }

    private void SampleBattery()
    {
        try
        {
            if (GetSystemPowerStatus(out var status))
            {
                var absent = (status.BatteryFlag & 0x80) != 0 || status.BatteryFlag == 0xFF || status.BatteryLifePercent == 0xFF;
                batteryPresent = !absent;
                batteryPercent = absent ? 100 : Math.Clamp((int)status.BatteryLifePercent, 0, 100);
                charging = !absent && ((status.BatteryFlag & 0x08) != 0 || status.AcLineStatus == 1);
                return;
            }
        }
        catch
        {
            // GetSystemPowerStatus unavailable; fall through to the desktop default.
        }

        batteryPresent = false;
        batteryPercent = 100;
        charging = false;
    }

    private async Task SampleNetworkAsync(Ping ping)
    {
        var endpoint = target;
        var succeeded = false;
        long roundtrip = 0;

        try
        {
            var reply = await ping.SendPingAsync(endpoint, PingTimeoutMilliseconds).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                succeeded = true;
                roundtrip = reply.RoundtripTime;
            }
        }
        catch
        {
            succeeded = false;
        }

        sampleSucceeded[sampleCursor] = succeeded;
        sampleRoundtrip[sampleCursor] = roundtrip;
        sampleCursor = (sampleCursor + 1) % SampleWindow;
        if (sampleCount < SampleWindow)
        {
            sampleCount++;
        }

        PublishSignal();
    }

    private void PublishSignal()
    {
        var failures = 0;
        var successes = 0;
        long total = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            if (sampleSucceeded[index])
            {
                successes++;
                total += sampleRoundtrip[index];
            }
            else
            {
                failures++;
            }
        }

        var loss = sampleCount == 0 ? 0 : failures * 100 / sampleCount;
        var average = successes == 0 ? 0 : (int)(total / successes);
        latencyMilliseconds = average;
        packetLossPercent = loss;
        signalBars = ComputeBars(average, loss, successes > 0);
    }

    private static int ComputeBars(int latencyMilliseconds, int packetLossPercent, bool reachable)
    {
        if (!reachable)
        {
            return 0;
        }

        var bars = latencyMilliseconds switch
        {
            <= 50 => 4,
            <= 110 => 3,
            <= 200 => 2,
            _ => 1,
        };

        if (packetLossPercent >= 50)
        {
            return Math.Min(bars, 1);
        }

        if (packetLossPercent >= 25)
        {
            return Math.Min(bars, 2);
        }

        if (packetLossPercent >= 10)
        {
            return Math.Min(bars, 3);
        }

        return bars;
    }

    private static bool TryDataCenterHost(uint dataCenterId, out IPAddress host)
    {
        host = dataCenterId switch
        {
            1 => IPAddress.Parse("119.252.36.6"),
            2 => IPAddress.Parse("119.252.36.7"),
            3 => IPAddress.Parse("119.252.36.8"),
            4 => IPAddress.Parse("204.2.29.6"),
            5 => IPAddress.Parse("204.2.29.7"),
            6 => IPAddress.Parse("80.239.145.6"),
            7 => IPAddress.Parse("80.239.145.7"),
            8 => IPAddress.Parse("204.2.29.8"),
            9 => IPAddress.Parse("153.254.80.103"),
            10 => IPAddress.Parse("119.252.36.9"),
            11 => IPAddress.Parse("204.2.29.9"),
            12 => IPAddress.Parse("80.239.145.8"),
            _ => FallbackHost,
        };

        return dataCenterId is >= 1 and <= 12;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
