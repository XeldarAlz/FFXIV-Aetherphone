using System.Collections.Frozen;
using System.Globalization;
using System.IO.Compression;
using Aetherphone.Core;

namespace Aetherphone.Core.Emulation;

internal sealed class EmulatorCoreProvisioner : IDisposable
{
    private const string CoreBaseUrl = "https://buildbot.libretro.com/nightly/windows/x86_64/latest/";
    private const string CoreIndexUrl = CoreBaseUrl + ".index-extended";
    private const long UnknownCoreBytes = 4L * 1024L * 1024L;
    private const int CopyBufferBytes = 1 << 16;
    private const int DownloadAttempts = 3;
    private const int RetryDelayMilliseconds = 400;

    private static readonly FrozenDictionary<string, long> CoreBytes = new Dictionary<string, long>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["blastem_libretro.dll"] = 936839L,
        ["bsnes_libretro.dll"] = 1034253L,
        ["clownmdemu_libretro.dll"] = 384836L,
        ["geargrafx_libretro.dll"] = 914518L,
        ["mgba_libretro.dll"] = 529880L,
        ["mednafen_ngp_libretro.dll"] = 201805L,
        ["mednafen_wswan_libretro.dll"] = 114708L,
        ["melondsds_libretro.dll"] = 1902044L,
        ["mupen64plus_next_libretro.dll"] = 2892985L,
        ["nestopia_libretro.dll"] = 948102L,
        ["pcsx_rearmed_libretro.dll"] = 793594L,
        ["sameboy_libretro.dll"] = 127315L,
        ["smsplus_libretro.dll"] = 110730L,
        ["stella_libretro.dll"] = 1805707L,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient http;
    private readonly string coreDirectory;
    private readonly string systemDirectory;
    private FrozenDictionary<string, uint>? publishedChecksums;

    public EmulatorCoreProvisioner(string coreDirectory, string systemDirectory)
    {
        this.coreDirectory = coreDirectory;
        this.systemDirectory = systemDirectory;
        http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", $"{AepConstants.Name}/{AepConstants.Version}");
    }

    public string CoreDirectory => coreDirectory;

    public void Dispose() => http.Dispose();

    public string CorePath(EmulatorSystemDefinition system) => Path.Combine(coreDirectory, system.CoreFileName);

    public bool IsInstalled(EmulatorSystemDefinition system) => File.Exists(CorePath(system));

    public long PendingBytes(EmulatorSystemDefinition system)
    {
        if (File.Exists(CorePath(system)))
        {
            return 0L;
        }

        return CoreBytes.TryGetValue(system.CoreFileName, out var bytes) ? bytes : UnknownCoreBytes;
    }

    public async Task InstallAsync(EmulatorSystemDefinition system, Action<float>? onProgress,
        CancellationToken token)
    {
        if (File.Exists(CorePath(system)))
        {
            onProgress?.Invoke(1f);
            return;
        }

        var total = MathF.Max(1f, PendingBytes(system));
        var url = string.Concat(CoreBaseUrl, system.CoreFileName, ".zip");
        var expected = await PublishedChecksumAsync(system.CoreFileName, token).ConfigureAwait(false);
        await InstallArchiveAsync(url, coreDirectory, system.CoreFileName, expected,
                received => onProgress?.Invoke(received / total), token)
            .ConfigureAwait(false);
        onProgress?.Invoke(1f);
    }

    public async Task<string> InstallStarterGameAsync(EmulatorStarterGame game, string romDirectory,
        Action<float>? onProgress, CancellationToken token)
    {
        Directory.CreateDirectory(romDirectory);
        var destination = Path.Combine(romDirectory, game.FileName);
        var staged = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var total = MathF.Max(1f, game.Bytes);
        try
        {
            await DownloadAsync(game.Url, staged, received => onProgress?.Invoke(received / total), token)
                .ConfigureAwait(false);
            File.Copy(staged, destination, true);
            onProgress?.Invoke(1f);
            return destination;
        }
        finally
        {
            Discard(staged, string.Empty);
        }
    }

    private async Task InstallArchiveAsync(string url, string destination, string? singleEntry, uint? expected,
        Action<long> onReceived, CancellationToken token)
    {
        var archive = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.zip");
        var staging = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            await DownloadAsync(url, archive, onReceived, token).ConfigureAwait(false);
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(archive, staging, true);
            if (singleEntry is not null)
            {
                var extracted = Path.Combine(staging, singleEntry);
                if (!File.Exists(extracted))
                {
                    throw new InvalidDataException($"{url} did not contain {singleEntry}");
                }

                if (expected is { } checksum)
                {
                    Verify(extracted, checksum, singleEntry);
                }
            }

            Directory.CreateDirectory(destination);
            Publish(staging, destination);
        }
        finally
        {
            Discard(archive, staging);
        }
    }

    private async Task DownloadAsync(string url, string destination, Action<long> onReceived,
        CancellationToken token)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadOnceAsync(url, destination, onReceived, token).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (attempt < DownloadAttempts &&
                                              exception is HttpRequestException or IOException)
            {
                AepLog.Warning($"[Emulator] {url} failed on attempt {attempt} ({exception.Message}), retrying");
                await Task.Delay(RetryDelayMilliseconds * attempt, token).ConfigureAwait(false);
            }
        }
    }

    private async Task DownloadOnceAsync(string url, string destination, Action<long> onReceived,
        CancellationToken token)
    {
        using var response = await http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var target = File.Create(destination);
        var buffer = new byte[CopyBufferBytes];
        var received = 0L;
        while (true)
        {
            var read = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            received += read;
            onReceived(received);
        }
    }

    private static void Verify(string path, uint expected, string name)
    {
        var actual = Crc32.OfFile(path);
        if (actual == expected)
        {
            return;
        }

        throw new InvalidDataException(
            $"{name} failed its checksum (expected {expected:x8}, got {actual:x8})");
    }

    private static void Publish(string staging, string destination)
    {
        foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staging, source);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
        }
    }

    private static void Discard(string archive, string staging)
    {
        try
        {
            if (File.Exists(archive))
            {
                File.Delete(archive);
            }

            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AepLog.Warning($"[Emulator] could not clear the download staging area: {exception.Message}");
        }
    }

    private async Task<uint?> PublishedChecksumAsync(string coreFileName, CancellationToken token)
    {
        var checksums = publishedChecksums ?? await LoadChecksumsAsync(token).ConfigureAwait(false);
        if (checksums is null)
        {
            return null;
        }

        return checksums.TryGetValue(coreFileName, out var checksum) ? checksum : null;
    }

    private async Task<FrozenDictionary<string, uint>?> LoadChecksumsAsync(CancellationToken token)
    {
        try
        {
            var index = await http.GetStringAsync(CoreIndexUrl, token).ConfigureAwait(false);
            var checksums = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in index.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var columns = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 3 || !columns[2].EndsWith(".dll.zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (uint.TryParse(columns[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var checksum))
                {
                    checksums[columns[2][..^4]] = checksum;
                }
            }

            publishedChecksums = checksums.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            return publishedChecksums;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            AepLog.Warning($"[Emulator] could not read the core index, falling back to the archive's own " +
                           $"checksum: {exception.Message}");
            return null;
        }
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        public static uint OfFile(string path)
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[CopyBufferBytes];
            var checksum = 0xFFFFFFFFu;
            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                for (var index = 0; index < read; index++)
                {
                    checksum = Table[(checksum ^ buffer[index]) & 0xFF] ^ (checksum >> 8);
                }
            }

            return checksum ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (var index = 0u; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[index] = value;
            }

            return table;
        }
    }
}
