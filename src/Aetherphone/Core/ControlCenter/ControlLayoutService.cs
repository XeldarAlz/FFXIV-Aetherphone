using Aetherphone.Core.Home;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlLayoutService
{
    public const int Columns = 4;
    public const int LayoutVersion = 1;
    private const int SolverRows = 16;

    private readonly IControlRegistry registry;
    private readonly IControlConfiguration configuration;
    private readonly List<ControlSlot> slots = new();
    private readonly List<ControlSlot> visible = new();
    private readonly List<GridCell> placements = new();
    private readonly HashSet<string> enabled = new();
    private bool placementsDirty = true;
    private int rowsUsed;

    public ControlLayoutService(IControlRegistry registry, IControlConfiguration configuration)
    {
        this.registry = registry;
        this.configuration = configuration;
        Load();
    }

    public IReadOnlyList<ControlSlot> Slots => SyncVisible();

    public int RowsUsed
    {
        get
        {
            SyncVisible();
            if (placementsDirty)
            {
                Solve();
            }

            return rowsUsed;
        }
    }

    public IReadOnlyList<GridCell> Placements
    {
        get
        {
            SyncVisible();
            if (placementsDirty)
            {
                Solve();
            }

            return placements;
        }
    }

    public IReadOnlyList<IControlModule> Hidden()
    {
        var hidden = new List<IControlModule>();
        var all = registry.Modules;
        for (var index = 0; index < all.Count; index++)
        {
            if (registry.IsAvailable(all[index].Id) && IndexOf(all[index].Id) < 0)
            {
                hidden.Add(all[index]);
            }
        }

        return hidden;
    }

    public int IndexOf(ControlSlot slot) => SyncVisible().IndexOf(slot);

    public void Move(ControlSlot slot, int insertIndex)
    {
        if (!slots.Remove(slot))
        {
            return;
        }

        slots.Insert(StoredIndex(insertIndex), slot);
        Commit();
    }

    public void Reorder(ControlSlot slot, int insertIndex)
    {
        var from = slots.IndexOf(slot);
        if (from < 0)
        {
            return;
        }

        slots.RemoveAt(from);
        var target = StoredIndex(insertIndex);
        if (target == from)
        {
            slots.Insert(from, slot);
            return;
        }

        slots.Insert(target, slot);
        placementsDirty = true;
    }

    public void Persist() => Save();

    public void Resize(ControlSlot slot)
    {
        var next = ControlSpans.Next(slot.Module.Sizes, slot.Span);
        if (next == slot.Span)
        {
            return;
        }

        slot.Span = next;
        Commit();
    }

    public void Remove(ControlSlot slot)
    {
        if (slots.Remove(slot))
        {
            enabled.Remove(slot.Id);
            Commit();
        }
    }

    public void Add(IControlModule module)
    {
        if (IndexOf(module.Id) >= 0)
        {
            return;
        }

        enabled.Add(module.Id);
        slots.Add(ControlSlot.For(module, module.DefaultSpan));
        Commit();
    }

    public void Reset()
    {
        configuration.ControlPanel = null;
        Load();
        Save();
    }

    private List<ControlSlot> SyncVisible()
    {
        var cursor = 0;
        var matches = true;
        for (var index = 0; index < slots.Count && matches; index++)
        {
            if (!registry.IsAvailable(slots[index].Id))
            {
                continue;
            }

            matches = cursor < visible.Count && ReferenceEquals(visible[cursor], slots[index]);
            cursor++;
        }

        if (matches && cursor == visible.Count)
        {
            return visible;
        }

        visible.Clear();
        for (var index = 0; index < slots.Count; index++)
        {
            if (registry.IsAvailable(slots[index].Id))
            {
                visible.Add(slots[index]);
            }
        }

        placementsDirty = true;
        return visible;
    }

    private int StoredIndex(int visibleIndex)
    {
        var seen = 0;
        for (var index = 0; index < slots.Count; index++)
        {
            if (!registry.IsAvailable(slots[index].Id))
            {
                continue;
            }

            if (seen >= visibleIndex)
            {
                return index;
            }

            seen++;
        }

        return slots.Count;
    }

    private int IndexOf(string moduleId)
    {
        for (var index = 0; index < slots.Count; index++)
        {
            if (string.Equals(slots[index].Id, moduleId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void Load()
    {
        slots.Clear();
        var saved = configuration.ControlPanel;
        if (saved is null)
        {
            LoadDefaults();
            placementsDirty = true;
            Save();
            return;
        }

        var migrated = Migrate(saved);
        var placed = new HashSet<string>();
        for (var index = 0; index < saved.Items.Count; index++)
        {
            var item = saved.Items[index];
            if (!registry.TryGet(item.ModuleId, out var module) || !placed.Add(module.Id))
            {
                continue;
            }

            slots.Add(ControlSlot.For(module, SpanFor(module, ControlSpans.Parse(item.Span))));
        }

        LoadEnabled(saved, placed);
        placementsDirty = true;
        if (migrated)
        {
            Save();
        }
    }

    private static bool Migrate(ControlLayout saved)
    {
        if (saved.Version >= LayoutVersion)
        {
            return false;
        }

        FoldClusterMembers(saved);
        saved.Version = LayoutVersion;
        return true;
    }

    private static void FoldClusterMembers(ControlLayout saved)
    {
        var insertAt = -1;
        for (var index = saved.Items.Count - 1; index >= 0; index--)
        {
            if (!IsClusterMember(saved.Items[index].ModuleId))
            {
                continue;
            }

            saved.Items.RemoveAt(index);
            insertAt = index;
        }

        if (insertAt < 0)
        {
            return;
        }

        saved.Items.Insert(insertAt, new ControlItem
        {
            ModuleId = ControlDefaults.ClusterId,
            Span = ControlSpans.Serialize(ControlSpan.Large),
        });
        for (var index = saved.Enabled.Count - 1; index >= 0; index--)
        {
            if (IsClusterMember(saved.Enabled[index]))
            {
                saved.Enabled.RemoveAt(index);
            }
        }

        if (!saved.Enabled.Contains(ControlDefaults.ClusterId))
        {
            saved.Enabled.Add(ControlDefaults.ClusterId);
        }
    }

    private static bool IsClusterMember(string moduleId)
    {
        var members = ControlDefaults.ClusterMembers;
        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index], moduleId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void LoadDefaults()
    {
        enabled.Clear();
        var defaults = ControlDefaults.Layout;
        for (var index = 0; index < defaults.Length; index++)
        {
            var entry = defaults[index];
            if (!registry.TryGet(entry.ModuleId, out var module) || !enabled.Add(module.Id))
            {
                continue;
            }

            slots.Add(ControlSlot.For(module, SpanFor(module, entry.Span)));
        }
    }

    private void LoadEnabled(ControlLayout saved, HashSet<string> placed)
    {
        enabled.Clear();
        if (saved.Enabled is { Count: > 0 } stored)
        {
            for (var index = 0; index < stored.Count; index++)
            {
                enabled.Add(stored[index]);
            }
        }
        else
        {
            SeedEnabled(saved);
        }

        enabled.UnionWith(placed);

        var all = registry.Modules;
        for (var index = 0; index < all.Count; index++)
        {
            if (enabled.Contains(all[index].Id) && placed.Add(all[index].Id))
            {
                slots.Add(ControlSlot.For(all[index], all[index].DefaultSpan));
            }
        }
    }

    private void SeedEnabled(ControlLayout saved)
    {
        for (var index = 0; index < saved.Items.Count; index++)
        {
            enabled.Add(saved.Items[index].ModuleId);
        }
    }

    private static ControlSpan SpanFor(IControlModule module, ControlSpan span) =>
        ControlSpans.Contains(module.Sizes, span) ? span : module.DefaultSpan;

    private void Commit()
    {
        placementsDirty = true;
        Save();
    }

    private void Solve()
    {
        HomeGridSolver.Solve(visible, Columns, SolverRows, placements);
        var used = 0;
        for (var index = 0; index < placements.Count && index < visible.Count; index++)
        {
            used = Math.Max(used, placements[index].Row + visible[index].RowSpan);
        }

        rowsUsed = used;
        placementsDirty = false;
    }

    private void Save()
    {
        var layout = new ControlLayout { Version = LayoutVersion };
        for (var index = 0; index < slots.Count; index++)
        {
            layout.Items.Add(new ControlItem
            {
                ModuleId = slots[index].Id,
                Span = ControlSpans.Serialize(slots[index].Span),
            });
        }

        layout.Enabled.AddRange(enabled);
        configuration.ControlPanel = layout;
        configuration.Save();
    }
}
