using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntJsonCatalogLoader<T>
{
    private readonly FileInfo source;
    private readonly JsonTypeInfo<T> typeInfo;
    private readonly string label;
    private readonly Action<T> onLoaded;
    private readonly object gate = new();
    private volatile bool loaded;

    public HuntJsonCatalogLoader(FileInfo source, JsonTypeInfo<T> typeInfo, string label, Action<T> onLoaded)
    {
        this.source = source;
        this.typeInfo = typeInfo;
        this.label = label;
        this.onLoaded = onLoaded;
    }

    public void Preload() => _ = Task.Run(EnsureLoaded);

    public void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        lock (gate)
        {
            if (loaded)
            {
                return;
            }

            try
            {
                if (!source.Exists)
                {
                    AepLog.Warning($"Hunt {label} missing at {source.FullName}");
                    return;
                }

                var json = File.ReadAllText(source.FullName);
                var parsed = JsonSerializer.Deserialize(json, typeInfo);
                if (parsed is not null)
                {
                    onLoaded(parsed);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"Hunt {label} load failed");
            }
            finally
            {
                loaded = true;
            }
        }
    }
}
