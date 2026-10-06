using System.Security.Cryptography;
using Aetherphone.Core;
using Aetherphone.Core.Video;
using SharpCompress.Readers;

namespace Aetherphone.Apps.Games.Doom;

internal readonly struct DoomIwad
{
    public readonly string FileName;
    public readonly string? Title;

    public DoomIwad(string fileName, string? title)
    {
        FileName = fileName;
        Title = title;
    }
}

internal readonly struct DoomPayload
{
    public readonly string FileName;
    public readonly string Sha256;

    public DoomPayload(string fileName, string sha256)
    {
        FileName = fileName;
        Sha256 = sha256;
    }
}

internal sealed class DoomAssets : IDisposable
{
    public const string FolderName = "doom";
    public const string SharewareFileName = "doom1.wad";
    public const string Freedoom1FileName = "freedoom1.wad";
    public const string Freedoom2FileName = "freedoom2.wad";
    public const string SoundfontFileName = "TimGM6mb.sf2";
    public const long SharewareDownloadBytes = 1756095;
    public const long FreedoomDownloadBytes = 24143781;
    public const long SoundfontDownloadBytes = 5560953;
    public static readonly DoomIwad[] Catalog =
    {
        new("doom2.wad", "Doom II"), new("plutonia.wad", "The Plutonia Experiment"), new("tnt.wad", "TNT: Evilution"),
        new("doom.wad", "Doom"), new(Freedoom2FileName, "Freedoom: Phase 2"), new(Freedoom1FileName, "Freedoom: Phase 1"),
        new(SharewareFileName, null),
    };
    private const string SharewareUrl =
        "https://deb.debian.org/debian/pool/non-free/d/doom-wad-shareware/doom-wad-shareware_1.9.fixed.orig.tar.gz";
    private const string FreedoomUrl = "https://github.com/freedoom/freedoom/releases/download/v0.13.0/freedoom-0.13.0.zip";
    private const string SoundfontUrl =
        "https://deb.debian.org/debian/pool/main/t/timgm6mb-soundfont/timgm6mb-soundfont_1.3.orig.tar.gz";
    private static readonly DoomPayload[] SharewarePayloads =
    {
        new(SharewareFileName, "1d7d43be501e67d927e415e0b8f3e29c3bf33075e859721816f652a526cac771"),
    };
    private static readonly DoomPayload[] FreedoomPayloads =
    {
        new(Freedoom1FileName, "7323bcc168c5a45ff10749b339960e98314740a734c30d4b9f3337001f9e703d"),
        new(Freedoom2FileName, "a8772e088847032510d97ba2312406a6998f21cbab44d4ff10696faa9c0ecd4b"),
    };
    private static readonly DoomPayload[] SoundfontPayloads =
    {
        new(SoundfontFileName, "c5378b62028c920cb11e4803327983fee2f2cdff5dc89c708e39da417e51c854"),
    };
    private const long SharewareMinimumBytes = 4_000_000;
    private const long FreedoomMinimumBytes = 20_000_000;
    private const long SoundfontMinimumBytes = 5_000_000;
    private const string StagingSuffix = ".download";
    private const string FreshSuffix = ".fresh";
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);
    private readonly HttpClient httpClient;
    private readonly string folder;
    private readonly List<int> availableCatalogIndices = new();
    private int installing;

    public DoomAssets()
    {
        httpClient = new HttpClient { Timeout = DownloadTimeout };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Aetherphone-Doom");
        folder = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, FolderName);
        Shareware = new MediaDependency("doom-shareware", SharewareUrl, string.Empty, ".tar.gz", SharewareFileName,
            SharewareMinimumBytes);
        Freedoom = new MediaDependency("doom-freedoom", FreedoomUrl, string.Empty, ".zip", Freedoom1FileName,
            FreedoomMinimumBytes);
        Soundfont = new MediaDependency("doom-soundfont", SoundfontUrl, string.Empty, ".tar.gz", SoundfontFileName,
            SoundfontMinimumBytes);
        Shareware.SetTotalBytes(SharewareDownloadBytes);
        Freedoom.SetTotalBytes(FreedoomDownloadBytes);
        Soundfont.SetTotalBytes(SoundfontDownloadBytes);
        RefreshStates();
    }

    public MediaDependency Shareware { get; }
    public MediaDependency Freedoom { get; }
    public MediaDependency Soundfont { get; }
    public string Folder => folder;
    public bool Installing => installing != 0;
    public int AvailableIwadCount => availableCatalogIndices.Count;
    public DoomIwad AvailableIwad(int index) => Catalog[availableCatalogIndices[index]];
    public string PathFor(in DoomIwad iwad) => Path.Combine(folder, iwad.FileName);
    public bool HasShareware { get; private set; }
    public bool HasFreedoom { get; private set; }
    public bool HasSoundfont { get; private set; }
    public bool SharewareReady => File.Exists(Path.Combine(folder, SharewareFileName));

    public bool FreedoomReady =>
        File.Exists(Path.Combine(folder, Freedoom1FileName)) && File.Exists(Path.Combine(folder, Freedoom2FileName));

    public string? SoundfontPath()
    {
        var path = Path.Combine(folder, SoundfontFileName);
        return File.Exists(path) ? path : null;
    }

    public static string? PreferredIwad(ReadOnlySpan<string> fileNames)
    {
        for (var catalogIndex = 0; catalogIndex < Catalog.Length; catalogIndex++)
        {
            for (var fileIndex = 0; fileIndex < fileNames.Length; fileIndex++)
            {
                if (string.Equals(fileNames[fileIndex], Catalog[catalogIndex].FileName, StringComparison.OrdinalIgnoreCase))
                {
                    return fileNames[fileIndex];
                }
            }
        }

        return null;
    }

    public void RefreshStates()
    {
        availableCatalogIndices.Clear();
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (File.Exists(Path.Combine(folder, Catalog[index].FileName)))
            {
                availableCatalogIndices.Add(index);
            }
        }

        HasShareware = SharewareReady;
        HasFreedoom = FreedoomReady;
        HasSoundfont = SoundfontPath() is not null;
        if (Installing)
        {
            return;
        }

        SettleState(Shareware, HasShareware);
        SettleState(Freedoom, HasFreedoom);
        SettleState(Soundfont, HasSoundfont);
    }

    private static void SettleState(MediaDependency dependency, bool ready)
    {
        if (ready)
        {
            dependency.SetState(DependencyState.Ready);
            return;
        }

        if (dependency.Snapshot().State != DependencyState.Failed)
        {
            dependency.SetState(DependencyState.Missing);
        }
    }

    public void Install(bool shareware, bool freedoom, bool soundfont)
    {
        if (Interlocked.CompareExchange(ref installing, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(() => InstallAsync(shareware, freedoom, soundfont));
    }

    private async Task InstallAsync(bool shareware, bool freedoom, bool soundfont)
    {
        try
        {
            if (shareware && !SharewareReady)
            {
                await DownloadAsync(Shareware, SharewareUrl, SharewarePayloads, SharewareDownloadBytes).ConfigureAwait(false);
            }

            if (freedoom && !FreedoomReady)
            {
                await DownloadAsync(Freedoom, FreedoomUrl, FreedoomPayloads, FreedoomDownloadBytes).ConfigureAwait(false);
            }

            if (soundfont && SoundfontPath() is null)
            {
                await DownloadAsync(Soundfont, SoundfontUrl, SoundfontPayloads, SoundfontDownloadBytes).ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref installing, 0);
        }
    }

    private async Task DownloadAsync(MediaDependency dependency, string url, DoomPayload[] payloads, long expectedBytes)
    {
        var staging = Path.Combine(folder, dependency.Id + StagingSuffix);
        dependency.ResetTransfer();
        dependency.SetTotalBytes(expectedBytes);
        dependency.SetState(DependencyState.Downloading);
        try
        {
            Directory.CreateDirectory(folder);
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is { } length && length > 0)
                {
                    dependency.SetTotalBytes(length);
                }

                await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using var destination = new FileStream(staging, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                var received = 0L;
                while (true)
                {
                    var read = await source.ReadAsync(buffer).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    received += read;
                    dependency.ReportReceived(received);
                }
            }

            dependency.SetState(DependencyState.Installing);
            for (var index = 0; index < payloads.Length; index++)
            {
                InstallPayload(staging, payloads[index]);
            }

            dependency.SetState(DependencyState.Ready);
            AepLog.Debug($"[Doom] {dependency.Id} is ready");
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Doom] Installing {dependency.Id} failed: {exception.Message}");
            dependency.Fail(exception.Message);
        }
        finally
        {
            QuietDelete(staging);
        }
    }

    private void InstallPayload(string archivePath, in DoomPayload payload)
    {
        var target = Path.Combine(folder, payload.FileName);
        var fresh = target + FreshSuffix;
        try
        {
            ExtractEntry(archivePath, payload.FileName, fresh);
            var actual = HashFile(fresh);
            if (!actual.Equals(payload.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{payload.FileName} did not match its expected checksum.");
            }

            File.Move(fresh, target, true);
        }
        finally
        {
            QuietDelete(fresh);
        }
    }

    private static void ExtractEntry(string archivePath, string entryName, string destination)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            var entry = reader.Entry;
            if (entry.IsDirectory || entry.Key is null)
            {
                continue;
            }

            if (!Path.GetFileName(entry.Key).Equals(entryName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            reader.WriteEntryTo(target);
            return;
        }

        throw new InvalidOperationException($"{entryName} was not in the downloaded archive.");
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static void QuietDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
