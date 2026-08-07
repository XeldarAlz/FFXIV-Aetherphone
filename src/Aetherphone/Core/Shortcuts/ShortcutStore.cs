using Aetherphone.Core.Home;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Shortcuts;

internal sealed class ShortcutStore : IShortcutSource
{
    public const int MaxShortcuts = 60;
    public const int MaxSteps = 30;
    public const int NameMaxLength = 24;
    public const int CommandMaxLength = 400;

    private readonly Configuration configuration;
    private readonly PluginCatalog catalog;
    private HomeLayoutService? layout;

    public event Action? Changed;

    public ShortcutStore(Configuration configuration, PluginCatalog catalog)
    {
        this.configuration = configuration;
        this.catalog = catalog;
    }

    public PluginCatalog Catalog => catalog;

    public IReadOnlyList<ShortcutEntry> All => configuration.Shortcuts;

    public bool AtCapacity => configuration.Shortcuts.Count >= MaxShortcuts;

    public void Bind(HomeLayoutService service) => layout = service;

    public ShortcutEntry? Find(Guid id)
    {
        var shortcuts = configuration.Shortcuts;
        for (var index = 0; index < shortcuts.Count; index++)
        {
            if (shortcuts[index].Id == id)
            {
                return shortcuts[index];
            }
        }

        return null;
    }

    public ShortcutEntry? FindByName(string name)
    {
        var wanted = name.Trim();
        if (wanted.Length == 0)
        {
            return null;
        }

        var shortcuts = configuration.Shortcuts;
        for (var index = 0; index < shortcuts.Count; index++)
        {
            if (string.Equals(shortcuts[index].Name, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return shortcuts[index];
            }
        }

        return null;
    }

    public void Add(ShortcutEntry entry)
    {
        if (AtCapacity)
        {
            return;
        }

        configuration.Shortcuts.Insert(0, entry);
        Save();
    }

    public void Remove(Guid id)
    {
        layout?.RemoveShortcut(id);
        configuration.Shortcuts.RemoveAll(entry => entry.Id == id);
        Save();
    }

    public void Save()
    {
        configuration.Save();
        Changed?.Invoke();
    }

    public bool IsPinned(Guid id) => layout?.HasShortcut(id) ?? false;

    public void Pin(Guid id) => layout?.AddShortcut(id);

    public void Unpin(Guid id) => layout?.RemoveShortcut(id);

    public void SetPinned(Guid id, bool pinned)
    {
        if (pinned)
        {
            Pin(id);
            return;
        }

        Unpin(id);
    }

    public IDalamudTextureWrap? Icon(ShortcutEntry entry) =>
        entry.IconPlugin.Length == 0 ? null : catalog.Icon(entry.IconPlugin);

    public static string Monogram(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return "?";
        }

        var separator = trimmed.IndexOf(' ');
        if (separator <= 0 || separator + 1 >= trimmed.Length)
        {
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        return string.Concat(trimmed.Substring(0, 1), trimmed.Substring(separator + 1, 1)).ToUpperInvariant();
    }
}
