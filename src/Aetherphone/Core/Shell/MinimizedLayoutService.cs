namespace Aetherphone.Core.Shell;

internal readonly struct MinimizedSlot
{
    public readonly MinimizedPart Part;
    public readonly bool Enabled;

    public MinimizedSlot(MinimizedPart part, bool enabled)
    {
        Part = part;
        Enabled = enabled;
    }
}

internal sealed class MinimizedLayoutService
{
    private readonly IMinimizedConfiguration configuration;
    private readonly MinimizedSlot[] slots = new MinimizedSlot[MinimizedParts.Count];

    public MinimizedLayoutService(IMinimizedConfiguration configuration)
    {
        this.configuration = configuration;
        Load();
    }

    public ReadOnlySpan<MinimizedSlot> Slots => slots;

    public int Revision { get; private set; }

    public bool IsEnabled(MinimizedPart part)
    {
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index].Part == part)
            {
                return slots[index].Enabled;
            }
        }

        return false;
    }

    public void SetEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= slots.Length || slots[index].Enabled == enabled)
        {
            return;
        }

        slots[index] = new MinimizedSlot(slots[index].Part, enabled);
        Commit();
    }

    public void Move(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || index >= slots.Length || target < 0 || target >= slots.Length)
        {
            return;
        }

        (slots[index], slots[target]) = (slots[target], slots[index]);
        Commit();
    }

    public void MovePage(int index, int direction)
    {
        if (index < 0 || index >= slots.Length || direction == 0)
        {
            return;
        }

        var step = Math.Sign(direction);
        for (var target = index + step; target >= 0 && target < slots.Length; target += step)
        {
            if (!MinimizedParts.IsPage(slots[target].Part))
            {
                continue;
            }

            (slots[index], slots[target]) = (slots[target], slots[index]);
            Commit();
            return;
        }
    }

    public void Reset()
    {
        LoadDefaults();
        Commit();
    }

    private void Load()
    {
        var saved = configuration.MinimizedLayout;
        if (saved is null || saved.Items.Count == 0)
        {
            LoadDefaults();
            return;
        }

        var placed = new bool[MinimizedParts.Count];
        var count = 0;
        for (var index = 0; index < saved.Items.Count && count < slots.Length; index++)
        {
            var item = saved.Items[index];
            if (!MinimizedParts.TryParse(item.PartId, out var part) || placed[(int)part])
            {
                continue;
            }

            placed[(int)part] = true;
            slots[count++] = new MinimizedSlot(part, item.Enabled);
        }

        var defaults = MinimizedParts.Default;
        for (var index = 0; index < defaults.Length && count < slots.Length; index++)
        {
            var part = defaults[index];
            if (placed[(int)part])
            {
                continue;
            }

            placed[(int)part] = true;
            slots[count++] = new MinimizedSlot(part, MinimizedParts.EnabledByDefault(part));
        }

        Revision++;
    }

    private void LoadDefaults()
    {
        var defaults = MinimizedParts.Default;
        for (var index = 0; index < defaults.Length; index++)
        {
            slots[index] = new MinimizedSlot(defaults[index], MinimizedParts.EnabledByDefault(defaults[index]));
        }

        Revision++;
    }

    private void Commit()
    {
        Revision++;
        var layout = new MinimizedLayout();
        for (var index = 0; index < slots.Length; index++)
        {
            layout.Items.Add(new MinimizedLayoutItem
            {
                PartId = MinimizedParts.Id(slots[index].Part),
                Enabled = slots[index].Enabled,
            });
        }

        configuration.MinimizedLayout = layout;
        configuration.Save();
    }
}
