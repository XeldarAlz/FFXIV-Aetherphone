using System.Collections.Concurrent;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;

namespace Aetherphone.Core.Shortcuts;

internal readonly record struct PluginCommand(string Command, string Help);

internal sealed class PluginEntry
{
    public required string InternalName { get; init; }
    public required string Name { get; init; }
    public required string Author { get; init; }
    public required string Punchline { get; init; }
    public required string IconUrl { get; init; }
    public required bool HasMainUi { get; init; }
    public required bool HasConfigUi { get; init; }
    public required bool Loaded { get; init; }
    public List<PluginCommand> Commands { get; } = new();
}

internal sealed class PluginCatalog
{
    private const string OwnInternalName = "Aetherphone";
    private const long RecheckIntervalMilliseconds = 1000;
    private const string DistRepoRoot = "https://raw.githubusercontent.com/goatcorp/PluginDistD17/main";
    private static readonly string[] DistChannels = { "stable", "testing/live" };
    private static readonly TimeSpan IconMaxAge = TimeSpan.FromDays(30);

    private readonly RemoteImageCache images;
    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly List<PluginEntry> entries = new();
    private readonly Dictionary<string, PluginEntry> byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> iconless = new(StringComparer.OrdinalIgnoreCase);

    private int lastPluginCount = -1;
    private int lastCommandCount = -1;
    private long nextRecheckTick;
    private bool dirty = true;

    public PluginCatalog(RemoteImageCache images, HttpService http, DiskCache disk)
    {
        this.images = images;
        this.http = http;
        this.disk = disk;
    }

    public IReadOnlyList<PluginEntry> Entries
    {
        get
        {
            EnsureFresh();
            return entries;
        }
    }

    public void Invalidate() => dirty = true;

    public PluginEntry? Find(string internalName)
    {
        EnsureFresh();
        return byName.TryGetValue(internalName, out var entry) ? entry : null;
    }

    public bool IsLoaded(string internalName) => Find(internalName)?.Loaded ?? false;

    public string DisplayName(string internalName) => Find(internalName)?.Name ?? internalName;

    public IDalamudTextureWrap? Icon(string internalName)
    {
        var entry = Find(internalName);
        if (entry is null)
        {
            return null;
        }

        if (entry.IconUrl.Length > 0)
        {
            return images.Get(entry.IconUrl);
        }

        if (iconless.ContainsKey(entry.InternalName))
        {
            return null;
        }

        var name = entry.InternalName;
        return images.GetKeyed(string.Concat("pluginicon:", name), token => FetchFallbackIconAsync(name, token));
    }

    private async Task<byte[]?> FetchFallbackIconAsync(string internalName, CancellationToken token)
    {
        var shipped = ReadShippedIcon(internalName);
        if (shipped is not null)
        {
            return shipped;
        }

        for (var index = 0; index < DistChannels.Length; index++)
        {
            var url = string.Concat(DistRepoRoot, "/", DistChannels[index], "/", internalName, "/images/icon.png");
            var cached = disk.Get(url, IconMaxAge);
            if (cached is not null && LooksLikeImage(cached))
            {
                return cached;
            }

            var bytes = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
            if (bytes is null || !LooksLikeImage(bytes))
            {
                continue;
            }

            disk.Set(url, bytes);
            return bytes;
        }

        iconless.TryAdd(internalName, 0);
        return null;
    }

    private static byte[]? ReadShippedIcon(string internalName)
    {
        try
        {
            var launcherRoot =
                Path.GetDirectoryName(Path.GetDirectoryName(Plugin.PluginInterface.ConfigDirectory.FullName));
            if (launcherRoot is null)
            {
                return null;
            }

            var pluginRoot = Path.Combine(launcherRoot, "installedPlugins", internalName);
            if (!Directory.Exists(pluginRoot))
            {
                return null;
            }

            var versions = Directory.GetDirectories(pluginRoot);
            Array.Sort(versions, static (first, second) =>
                Directory.GetLastWriteTimeUtc(second).CompareTo(Directory.GetLastWriteTimeUtc(first)));
            for (var index = 0; index < versions.Length; index++)
            {
                var files = Directory.GetFiles(versions[index], "icon.png", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(files[0]);
                if (LooksLikeImage(bytes))
                {
                    return bytes;
                }
            }

            return null;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Reading the shipped icon of {internalName} failed: {exception.Message}");
            return null;
        }
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return true;
        }

        return bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
               bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
    }

    public static bool TryOpenMainUi(string internalName)
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (!string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal) || !plugin.IsLoaded)
            {
                continue;
            }

            try
            {
                if (plugin.HasMainUi)
                {
                    plugin.OpenMainUi();
                    return true;
                }

                if (plugin.HasConfigUi)
                {
                    plugin.OpenConfigUi();
                    return true;
                }
            }
            catch (Exception ex)
            {
                AepLog.Warning($"Opening {internalName} failed: {ex.Message}");
            }

            return false;
        }

        return false;
    }

    public static bool TryOpenConfigUi(string internalName)
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (!string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal) || !plugin.IsLoaded ||
                !plugin.HasConfigUi)
            {
                continue;
            }

            try
            {
                plugin.OpenConfigUi();
                return true;
            }
            catch (Exception ex)
            {
                AepLog.Warning($"Opening the settings of {internalName} failed: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private void EnsureFresh()
    {
        var now = Environment.TickCount64;
        if (!dirty && now < nextRecheckTick)
        {
            return;
        }

        nextRecheckTick = now + RecheckIntervalMilliseconds;
        var pluginCount = CountPlugins();
        var commandCount = Plugin.CommandManager.Commands.Count;
        if (!dirty && pluginCount == lastPluginCount && commandCount == lastCommandCount)
        {
            return;
        }

        dirty = false;
        lastPluginCount = pluginCount;
        lastCommandCount = commandCount;
        Rebuild();
    }

    private static int CountPlugins()
    {
        var installed = 0;
        var loaded = 0;
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            installed++;
            if (plugin.IsLoaded)
            {
                loaded++;
            }
        }

        return installed * 1024 + loaded;
    }

    private void Rebuild()
    {
        entries.Clear();
        byName.Clear();
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, OwnInternalName, StringComparison.Ordinal) ||
                byName.ContainsKey(plugin.InternalName))
            {
                continue;
            }

            var entry = Describe(plugin);
            entries.Add(entry);
            byName[entry.InternalName] = entry;
        }

        AttachCommands();
        entries.Sort(CompareByName);
    }

    private static PluginEntry Describe(IExposedPlugin plugin)
    {
        var author = string.Empty;
        var punchline = string.Empty;
        var iconUrl = string.Empty;
        try
        {
            var manifest = plugin.Manifest;
            author = manifest.Author ?? string.Empty;
            punchline = manifest.Punchline ?? string.Empty;
            iconUrl = NormalizeIconUrl(manifest.IconUrl);
        }
        catch (Exception)
        {
            author = string.Empty;
        }

        return new PluginEntry
        {
            InternalName = plugin.InternalName,
            Name = plugin.Name.Length > 0 ? plugin.Name : plugin.InternalName,
            Author = author,
            Punchline = punchline,
            IconUrl = iconUrl,
            HasMainUi = plugin.HasMainUi,
            HasConfigUi = plugin.HasConfigUi,
            Loaded = plugin.IsLoaded,
        };
    }

    private static string NormalizeIconUrl(string? url)
    {
        if (url is null || url.Length == 0 || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return url;
    }

    private void AttachCommands()
    {
        var commands = Plugin.CommandManager.Commands;
        foreach (var pair in commands)
        {
            if (!pair.Value.ShowInHelp)
            {
                continue;
            }

            var owner = OwnerAssembly(pair.Value);
            if (owner.Length == 0 || ResolveOwner(owner) is not { } entry)
            {
                continue;
            }

            entry.Commands.Add(new PluginCommand(pair.Key, pair.Value.HelpMessage ?? string.Empty));
        }

        for (var index = 0; index < entries.Count; index++)
        {
            entries[index].Commands.Sort(CompareCommands);
        }
    }

    private PluginEntry? ResolveOwner(string assemblyName)
    {
        if (byName.TryGetValue(assemblyName, out var byInternalName))
        {
            return byInternalName;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return entries[index];
            }
        }

        return null;
    }

    private static string OwnerAssembly(Dalamud.Game.Command.IReadOnlyCommandInfo info)
    {
        try
        {
            var handler = info.Handler;
            var declaring = handler.Target?.GetType() ?? handler.Method.DeclaringType;
            return declaring?.Assembly.GetName().Name ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static int CompareByName(PluginEntry first, PluginEntry second) =>
        string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);

    private static int CompareCommands(PluginCommand first, PluginCommand second) =>
        string.Compare(first.Command, second.Command, StringComparison.OrdinalIgnoreCase);
}
