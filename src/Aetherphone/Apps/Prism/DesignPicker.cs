using System.Globalization;

namespace Aetherphone.Apps.Prism;

internal readonly record struct DesignChoice(Guid Id, string Name, string Folder, bool Owned);

internal sealed class DesignPicker
{
    private const CompareOptions MatchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
                                                CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;
    private const char FolderSeparator = '/';

    private DesignChoice[] choices = Array.Empty<DesignChoice>();
    private bool[] picked = Array.Empty<bool>();
    private int[] visible = Array.Empty<int>();
    private string filteredFor = string.Empty;
    private bool stale = true;

    public int Count => choices.Length;

    public int VisibleCount { get; private set; }

    public int PickedCount { get; private set; }

    public void Load(DesignChoice[] next)
    {
        choices = next;
        picked = new bool[next.Length];
        visible = new int[next.Length];
        PickedCount = 0;
        VisibleCount = 0;
        stale = true;
    }

    public ref readonly DesignChoice Visible(int row) => ref choices[visible[row]];

    public bool IsPicked(int row) => picked[visible[row]];

    public void Toggle(int row)
    {
        var index = visible[row];
        if (choices[index].Owned)
        {
            return;
        }

        picked[index] = !picked[index];
        PickedCount += picked[index] ? 1 : -1;
    }

    public void Filter(CompareInfo compare, string query)
    {
        if (!stale && string.Equals(query, filteredFor, StringComparison.Ordinal))
        {
            return;
        }

        stale = false;
        filteredFor = query;
        var trimmed = query.Trim();
        var count = 0;
        for (var index = 0; index < choices.Length; index++)
        {
            if (trimmed.Length == 0 || Matches(compare, choices[index], trimmed))
            {
                visible[count++] = index;
            }
        }

        VisibleCount = count;
    }

    public void CopyPicked(List<(Guid Id, string Name)> into)
    {
        into.Clear();
        for (var index = 0; index < choices.Length; index++)
        {
            if (picked[index])
            {
                into.Add((choices[index].Id, choices[index].Name));
            }
        }
    }

    public static bool Matches(CompareInfo compare, in DesignChoice choice, string query) =>
        compare.IndexOf(choice.Name, query, MatchOptions) >= 0 ||
        (choice.Folder.Length > 0 && compare.IndexOf(choice.Folder, query, MatchOptions) >= 0);

    public static string FolderOf(string path, string name)
    {
        var cut = path.LastIndexOf(FolderSeparator);
        if (cut <= 0)
        {
            return string.Empty;
        }

        return path.AsSpan(cut + 1).SequenceEqual(name) ? path[..cut] : path;
    }

    public static DesignChoice[] Build(Dictionary<Guid, (string Name, string Path, uint Color, bool Quick)> designs,
        Func<Guid, bool> owned)
    {
        var built = new DesignChoice[designs.Count];
        var count = 0;
        foreach (var (id, (name, path, _, _)) in designs)
        {
            built[count++] = new DesignChoice(id, name, FolderOf(path, name), owned(id));
        }

        Array.Sort(built, CompareChoices);
        return built;
    }

    private static int CompareChoices(DesignChoice left, DesignChoice right)
    {
        var folder = string.Compare(left.Folder, right.Folder, StringComparison.OrdinalIgnoreCase);
        return folder != 0 ? folder : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
