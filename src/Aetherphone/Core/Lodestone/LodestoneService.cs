using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Net;
using NetStone;
using NetStone.Model.Parseables.Search.Character;
using NetStone.Search.Character;

namespace Aetherphone.Core.Lodestone;

internal sealed class LodestoneService : IDisposable
{
    private readonly Configuration configuration;
    private readonly HttpService http;
    private readonly MediaCache media;
    private readonly RequestThrottle throttle;
    private readonly string idIndexPath;

    private readonly object idSync = new();
    private readonly Dictionary<string, string> resolvedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unresolved = new(StringComparer.OrdinalIgnoreCase);
    private bool idsLoaded;

    private readonly SemaphoreSlim clientGate = new(1, 1);
    private LodestoneClient? client;

    public LodestoneService(Configuration configuration, HttpService http, MediaCache media, DirectoryInfo cacheRoot)
    {
        this.configuration = configuration;
        this.http = http;
        this.media = media;
        throttle = new RequestThrottle(1, TimeSpan.FromMilliseconds(1200));
        idIndexPath = Path.Combine(cacheRoot.FullName, "lodestone-ids.tsv");
    }

    public AvatarHandle Avatar(string name, string world) => Image(name, world, false);

    public AvatarHandle Portrait(string name, string world) => Image(name, world, true);

    public bool TryGetCachedId(string name, string world, out string id)
    {
        id = string.Empty;
        if (name.Length == 0 || world.Length == 0)
        {
            return false;
        }

        EnsureIdsLoaded();

        var key = $"{name}@{world}";
        lock (idSync)
        {
            if (resolvedIds.TryGetValue(key, out var cached))
            {
                id = cached;
                return true;
            }
        }

        return false;
    }

    private AvatarHandle Image(string name, string world, bool fullBody)
    {
        if (!configuration.ShowLodestonePortraits || name.Length == 0 || world.Length == 0)
        {
            return AvatarHandle.Disabled;
        }

        var key = $"lodestone:{(fullBody ? "portrait" : "avatar")}:{name}@{world}".ToLowerInvariant();
        var result = media.GetOrRequest(key, token => FetchAsync(name, world, fullBody, token));
        var state = result.Texture is not null
            ? AvatarLoadState.Ready
            : result.Loading ? AvatarLoadState.Loading : AvatarLoadState.Failed;
        return new AvatarHandle(result.Texture, state, key);
    }

    private async Task<byte[]?> FetchAsync(string name, string world, bool fullBody, CancellationToken token)
    {
        using (await throttle.EnterAsync(token).ConfigureAwait(false))
        {
            var id = await ResolveIdAsync(name, world, token).ConfigureAwait(false);
            if (id is null)
            {
                return null;
            }

            var ready = await EnsureClientAsync(token).ConfigureAwait(false);
            if (ready is null)
            {
                return null;
            }

            var character = await ready.GetCharacter(id).ConfigureAwait(false);
            var uri = fullBody ? character?.Portrait : character?.Avatar;
            if (uri is null)
            {
                return null;
            }

            return await http.GetBytesAsync(uri, token).ConfigureAwait(false);
        }
    }

    private async Task<string?> ResolveIdAsync(string name, string world, CancellationToken token)
    {
        EnsureIdsLoaded();

        var key = $"{name}@{world}";
        lock (idSync)
        {
            if (resolvedIds.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (unresolved.Contains(key))
            {
                return null;
            }
        }

        var ready = await EnsureClientAsync(token).ConfigureAwait(false);
        if (ready is null)
        {
            return null;
        }

        var page = await ready.SearchCharacter(new CharacterSearchQuery { CharacterName = name, World = world }).ConfigureAwait(false);
        var id = SelectId(page, name);

        lock (idSync)
        {
            if (id is null)
            {
                unresolved.Add(key);
            }
            else
            {
                resolvedIds[key] = id;
            }
        }

        if (id is not null)
        {
            AppendIdIndex(key, id);
        }

        return id;
    }

    private static string? SelectId(CharacterSearchPage? page, string name)
    {
        if (page?.Results is null)
        {
            return null;
        }

        string? firstId = null;
        foreach (var entry in page.Results)
        {
            firstId ??= entry.Id;
            if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Id;
            }
        }

        return firstId;
    }

    private async Task<LodestoneClient?> EnsureClientAsync(CancellationToken token)
    {
        if (client is not null)
        {
            return client;
        }

        await clientGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            client ??= await LodestoneClient.GetClientAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Lodestone client init failed: {exception.Message}");
        }
        finally
        {
            clientGate.Release();
        }

        return client;
    }

    private void EnsureIdsLoaded()
    {
        if (idsLoaded)
        {
            return;
        }

        lock (idSync)
        {
            if (idsLoaded)
            {
                return;
            }

            idsLoaded = true;
            try
            {
                if (!File.Exists(idIndexPath))
                {
                    return;
                }

                var lines = File.ReadAllLines(idIndexPath);
                for (var index = 0; index < lines.Length; index++)
                {
                    var separator = lines[index].IndexOf('\t');
                    if (separator <= 0 || separator >= lines[index].Length - 1)
                    {
                        continue;
                    }

                    resolvedIds[lines[index].Substring(0, separator)] = lines[index].Substring(separator + 1);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Lodestone id index load failed: {exception.Message}");
            }
        }
    }

    private void AppendIdIndex(string key, string id)
    {
        try
        {
            File.AppendAllText(idIndexPath, $"{key}\t{id}{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Lodestone id index append failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        throttle.Dispose();
        clientGate.Dispose();
    }
}
