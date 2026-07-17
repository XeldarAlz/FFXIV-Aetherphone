using Aetherphone.Core.Home;

namespace Aetherphone.Core.ControlCenter;

internal sealed class ControlLayoutService
{
    public const int Columns = 4;
    private const int SolverRows = 16;

    private readonly ControlRegistry registry;
    private readonly Configuration configuration;
    private readonly List<ControlSlot> slots = new();
    private readonly List<GridCell> placements = new();
    private bool placementsDirty = true;
    private int rowsUsed;

    public ControlLayoutService(ControlRegistry registry, Configuration configuration)
    {
        this.registry = registry;
        this.configuration = configuration;
        Load();
    }

    public IReadOnlyList<ControlSlot> Slots => slots;

    public int RowsUsed
    {
        get
        {
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
            if (IndexOf(all[index].Id) < 0)
            {
                hidden.Add(all[index]);
            }
        }

        return hidden;
    }

    public int IndexOf(ControlSlot slot) => slots.IndexOf(slot);

    public void Move(ControlSlot slot, int insertIndex)
    {
        var from = slots.IndexOf(slot);
        if (from < 0)
        {
            return;
        }

        slots.RemoveAt(from);
        slots.Insert(Math.Clamp(insertIndex, 0, slots.Count), slot);
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
        var target = Math.Clamp(insertIndex, 0, slots.Count);
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
            Commit();
        }
    }

    public void Add(IControlModule module)
    {
        if (IndexOf(module.Id) >= 0)
        {
            return;
        }

        slots.Add(ControlSlot.For(module, module.DefaultSpan));
        Commit();
    }

    public void Reset()
    {
        configuration.ControlPanel = null;
        Load();
        Save();
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
        var placed = new HashSet<string>();
        if (saved is not null)
        {
            for (var index = 0; index < saved.Items.Count; index++)
            {
                var item = saved.Items[index];
                if (!registry.TryGet(item.ModuleId, out var module) || !placed.Add(module.Id))
                {
                    continue;
                }

                var span = ControlSpans.Parse(item.Span);
                if (!ControlSpans.Contains(module.Sizes, span))
                {
                    span = module.DefaultSpan;
                }

                slots.Add(ControlSlot.For(module, span));
            }
        }

        var all = registry.Modules;
        for (var index = 0; index < all.Count; index++)
        {
            if (placed.Add(all[index].Id))
            {
                slots.Add(ControlSlot.For(all[index], all[index].DefaultSpan));
            }
        }

        placementsDirty = true;
        if (saved is null)
        {
            Save();
        }
    }

    private void Commit()
    {
        placementsDirty = true;
        Save();
    }

    private void Solve()
    {
        HomeGridSolver.Solve(slots, Columns, SolverRows, placements);
        var used = 0;
        for (var index = 0; index < placements.Count && index < slots.Count; index++)
        {
            used = Math.Max(used, placements[index].Row + slots[index].RowSpan);
        }

        rowsUsed = used;
        placementsDirty = false;
    }

    private void Save()
    {
        var layout = new ControlLayout();
        for (var index = 0; index < slots.Count; index++)
        {
            layout.Items.Add(new ControlItem
            {
                ModuleId = slots[index].Id,
                Span = ControlSpans.Serialize(slots[index].Span),
            });
        }

        configuration.ControlPanel = layout;
        configuration.Save();
    }
}
