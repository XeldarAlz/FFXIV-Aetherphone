using System.Diagnostics;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Songs;

internal readonly record struct SongResolvedAudio(byte[] Bytes, bool IsOpus);

internal readonly record struct SongResolvedStream(string Url, bool IsOpus);

internal readonly record struct SongSearchEntry(
    string VideoId,
    string Title,
    string Author,
    string ThumbnailUrl,
    int DurationSeconds);

internal sealed class SongLinkResolver
{
    private const string FormatSelector = "bestaudio[acodec=opus]/bestaudio[ext=m4a]/bestaudio/best";
    private const string WatchUrlPrefix = "https://www.youtube.com/watch?v=";
    private const string ThumbnailUrlFormat = "https://i.ytimg.com/vi/{0}/hqdefault.jpg";
    private const string SearchPrintTemplate = "%(id)s\t%(duration)s\t%(channel,uploader)s\t%(title)s";
    private const int ErrorExcerptLength = 300;
    private const int ExitPollMilliseconds = 200;
    private const long UpdateCheckCooldownMilliseconds = 10 * 60 * 1000;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(45);
    private static readonly HttpClient StreamingClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly string stagingDirectory;
    private volatile MediaDependencies? dependencies;
    private long lastUpdateCheckAtTicks = long.MinValue;

    public SongLinkResolver(string stagingDirectory)
    {
        this.stagingDirectory = stagingDirectory;
    }

    public MediaDependencies? Media => dependencies;

    public bool IsInstalled => dependencies is { LinkResolverPath: not null, JsRuntimePath: not null };

    private bool HasResolver => dependencies?.LinkResolverPath is not null;

    public void Attach(MediaDependencies mediaDependencies)
    {
        dependencies = mediaDependencies;
    }

    public SongResolvedAudio? Fetch(string videoId, CancellationToken token)
    {
        if (!HasResolver || token.IsCancellationRequested)
        {
            return null;
        }

        Directory.CreateDirectory(stagingDirectory);
        DeleteStagedFiles(videoId);
        try
        {
            var outputTemplate = Path.Combine(stagingDirectory, videoId + ".%(ext)s");
            var output = Run(startInfo =>
            {
                startInfo.ArgumentList.Add("--no-playlist");
                startInfo.ArgumentList.Add("--force-overwrites");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add(FormatSelector);
                startInfo.ArgumentList.Add("-o");
                startInfo.ArgumentList.Add(outputTemplate);
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add(WatchUrlPrefix + videoId);
            }, FetchTimeout, token);
            if (output is null)
            {
                return null;
            }

            var downloadedPath = FindStagedFile(videoId);
            if (downloadedPath is null)
            {
                AepLog.Warning("Song link resolver finished without producing an audio file");
                return null;
            }

            var bytes = File.ReadAllBytes(downloadedPath);
            if (bytes.Length == 0)
            {
                return null;
            }

            return new SongResolvedAudio(bytes, IsWebm(bytes));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AepLog.Warning(exception, "Song link resolver fetch failed");
            return null;
        }
        finally
        {
            DeleteStagedFiles(videoId);
        }
    }

    public SongResolvedStream? ResolveStreamUrl(string videoId, CancellationToken token)
    {
        if (!HasResolver || token.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            var output = Run(startInfo =>
            {
                startInfo.ArgumentList.Add("--no-playlist");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add(FormatSelector);
                startInfo.ArgumentList.Add("--print");
                startInfo.ArgumentList.Add("url");
                startInfo.ArgumentList.Add("--print");
                startInfo.ArgumentList.Add("acodec");
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add(WatchUrlPrefix + videoId);
            }, ResolveTimeout, token);
            if (output is null)
            {
                return null;
            }

            var lines = output.Split('\n');
            if (lines.Length < 2)
            {
                AepLog.Warning("Song link resolver returned no stream url");
                return null;
            }

            var url = lines[0].Trim();
            var audioCodec = lines[1].Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                AepLog.Warning("Song link resolver returned an unusable stream url");
                return null;
            }

            return new SongResolvedStream(url, audioCodec.StartsWith("opus", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AepLog.Warning(exception, "Song link resolver could not resolve a stream url");
            return null;
        }
    }

    public SongSearchEntry[]? Search(string query, int fetchCount, CancellationToken token)
    {
        if (!HasResolver || token.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            var output = Run(startInfo =>
            {
                startInfo.ArgumentList.Add("--flat-playlist");
                startInfo.ArgumentList.Add("--print");
                startInfo.ArgumentList.Add(SearchPrintTemplate);
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add($"ytsearch{fetchCount}:{query}");
            }, SearchTimeout, token);
            if (output is null)
            {
                return null;
            }

            return ParseSearchOutput(output);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AepLog.Warning(exception, "Song link resolver search failed");
            return null;
        }
    }

    internal static Stream OpenHttpStream(string url, CancellationToken token)
    {
        var response = StreamingClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStream(token);
    }

    private static SongSearchEntry[] ParseSearchOutput(string output)
    {
        var lines = output.Split('\n');
        var entries = new List<SongSearchEntry>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split('\t', 4);
            if (fields.Length < 4 || fields[0].Length == 0)
            {
                continue;
            }

            if (!double.TryParse(fields[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var durationSeconds)
                || durationSeconds <= 0)
            {
                continue;
            }

            var videoId = fields[0];
            entries.Add(new SongSearchEntry(videoId, fields[3], fields[2],
                string.Format(ThumbnailUrlFormat, videoId), (int)durationSeconds));
        }

        return entries.ToArray();
    }

    private string? Run(Action<ProcessStartInfo> configure, TimeSpan timeout, CancellationToken token)
    {
        var first = RunResolver(configure, timeout, token);
        if (first.Output is not null || !first.ExitedWithError || !TryUpdateResolver(token))
        {
            return first.Output;
        }

        return RunResolver(configure, timeout, token).Output;
    }

    private bool TryUpdateResolver(CancellationToken token)
    {
        var media = dependencies;
        if (media is null)
        {
            return false;
        }

        var now = Environment.TickCount64;
        var lastCheck = Interlocked.Read(ref lastUpdateCheckAtTicks);
        if (lastCheck != long.MinValue && now - lastCheck < UpdateCheckCooldownMilliseconds)
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref lastUpdateCheckAtTicks, now, lastCheck) != lastCheck)
        {
            return false;
        }

        try
        {
            var outcome = media.UpdateIfNewerAsync(media.LinkResolver, token).GetAwaiter().GetResult();
            if (outcome == ResolverUpdateOutcome.Updated)
            {
                AepLog.Warning(
                    $"Song link resolver failed; updated to {media.LinkResolver.RemoteVersion} and retrying");
                return true;
            }

            AepLog.Warning(
                $"Song link resolver failed with a current install ({media.InstalledVersion(media.LinkResolver) ?? "unknown version"})");
            return false;
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "Song link resolver update check did not complete");
            return false;
        }
    }

    private static ProcessStartInfo BuildStartInfo(string resolverPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resolverPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--no-warnings");
        startInfo.ArgumentList.Add("--no-progress");
        startInfo.ArgumentList.Add("--force-ipv4");
        return startInfo;
    }

    private ResolverRun RunResolver(Action<ProcessStartInfo> configure, TimeSpan timeout, CancellationToken token)
    {
        var resolverPath = dependencies?.LinkResolverPath;
        if (resolverPath is null)
        {
            return default;
        }

        var startInfo = BuildStartInfo(resolverPath);
        configure(startInfo);
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            AepLog.Warning("Song link resolver process could not be started");
            return default;
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var deadlineTicks = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (!process.WaitForExit(ExitPollMilliseconds))
        {
            if (!token.IsCancellationRequested && Environment.TickCount64 < deadlineTicks)
            {
                continue;
            }

            KillQuietly(process);
            return default;
        }

        var outputText = standardOutput.GetAwaiter().GetResult();
        var errorText = standardError.GetAwaiter().GetResult();
        if (process.ExitCode == 0)
        {
            return new ResolverRun(outputText, false);
        }

        AepLog.Warning($"Song link resolver exited with code {process.ExitCode}: {Excerpt(errorText)}");
        return new ResolverRun(null, true);
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "Song link resolver process could not be stopped");
        }
    }

    private string? FindStagedFile(string videoId)
    {
        var files = Directory.GetFiles(stagingDirectory, videoId + ".*");
        for (var index = 0; index < files.Length; index++)
        {
            if (!files[index].EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                && !files[index].EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase))
            {
                return files[index];
            }
        }

        return null;
    }

    private void DeleteStagedFiles(string videoId)
    {
        if (!Directory.Exists(stagingDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(stagingDirectory, videoId + ".*");
        for (var index = 0; index < files.Length; index++)
        {
            try
            {
                File.Delete(files[index]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AepLog.Debug(exception, $"Song link resolver staging cleanup skipped {files[index]}");
            }
        }
    }

    // 1A 45 DF A3 is the EBML header that opens every WebM file
    private static bool IsWebm(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3;

    private static string Excerpt(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= ErrorExcerptLength ? trimmed : trimmed[..ErrorExcerptLength];
    }

    private readonly record struct ResolverRun(string? Output, bool ExitedWithError);
}
