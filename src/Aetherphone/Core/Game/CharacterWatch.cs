using Aetherphone.Core.Inventory;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Game;

internal sealed class CharacterWatch : IDisposable
{
    private readonly IFramework framework;
    private ulong current;
    private bool started;

    public CharacterWatch(IFramework framework)
    {
        this.framework = framework;
    }

    public ulong CurrentContentId => current;
    public event Action<ulong>? Changed;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        OnTick(framework);
        framework.Update += OnTick;
    }

    private void OnTick(IFramework _)
    {
        var id = InventoryReader.ReadLocalContentId();
        if (id == current)
        {
            return;
        }

        current = id;
        Changed?.Invoke(id);
    }

    public void Dispose()
    {
        if (started)
        {
            framework.Update -= OnTick;
        }
    }
}
