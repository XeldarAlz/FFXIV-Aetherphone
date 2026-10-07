using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Aetherphone.Core.Audio;

internal sealed class RealtimeWaveProvider : IWaveProvider
{
    private const string ProAudioTask = "Pro Audio";
    private const long PauseGapMilliseconds = 2_000;
    private const long ReportIntervalMilliseconds = 60_000;

    [ThreadStatic]
    private static bool threadElevated;

    private readonly IWaveProvider source;
    private readonly long underrunGapTicks;
    private readonly long pauseGapTicks;
    private readonly long reportIntervalTicks;
    private long lastReadTimestamp;
    private long lastReportTimestamp;
    private long worstGapTicks;
    private int underrunCount;

    public RealtimeWaveProvider(IWaveProvider source, int latencyMilliseconds)
    {
        this.source = source;
        underrunGapTicks = Stopwatch.Frequency * latencyMilliseconds / 1000;
        pauseGapTicks = Stopwatch.Frequency * PauseGapMilliseconds / 1000;
        reportIntervalTicks = Stopwatch.Frequency * ReportIntervalMilliseconds / 1000;
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        var now = Stopwatch.GetTimestamp();
        if (!threadElevated)
        {
            threadElevated = true;
            ElevateCurrentThread();
            lastReadTimestamp = 0;
        }

        TrackStall(now);
        return source.Read(buffer, offset, count);
    }

    private void TrackStall(long now)
    {
        var gap = now - lastReadTimestamp;
        var tracked = lastReadTimestamp != 0;
        lastReadTimestamp = now;
        if (!tracked || gap <= underrunGapTicks || gap >= pauseGapTicks)
        {
            return;
        }

        underrunCount++;
        worstGapTicks = Math.Max(worstGapTicks, gap);
        if (now - lastReportTimestamp < reportIntervalTicks)
        {
            return;
        }

        AepLog.Info($"[Audio] {underrunCount} output underruns, longest stall {worstGapTicks * 1000 / Stopwatch.Frequency} ms");
        lastReportTimestamp = now;
        underrunCount = 0;
        worstGapTicks = 0;
    }

    private static void ElevateCurrentThread()
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        try
        {
            var taskIndex = 0u;
            if (AvSetMmThreadCharacteristics(ProAudioTask, ref taskIndex) == IntPtr.Zero)
            {
                AepLog.Debug($"[Audio] MMCSS registration refused (error {Marshal.GetLastWin32Error()})");
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            AepLog.Debug(exception, "[Audio] MMCSS unavailable; running the audio thread at highest priority only");
        }
    }

    [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);
}
