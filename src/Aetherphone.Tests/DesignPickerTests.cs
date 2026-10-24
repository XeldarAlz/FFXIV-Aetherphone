using System.Globalization;
using Aetherphone.Apps.Prism;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DesignPickerTests
{
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;

    [Fact]
    public void ShowsEveryDesignWithAnEmptyQuery()
    {
        var picker = Loaded();
        picker.Filter(Compare, string.Empty);

        Assert.Equal(4, picker.VisibleCount);
        Assert.Equal(0, picker.PickedCount);
    }

    [Fact]
    public void FiltersByNameIgnoringCase()
    {
        var picker = Loaded();
        picker.Filter(Compare, "WINTER");

        Assert.Equal(1, picker.VisibleCount);
        Assert.Equal("Winter coat", picker.Visible(0).Name);
    }

    [Fact]
    public void MatchesFoldersToo()
    {
        var picker = Loaded();
        picker.Filter(Compare, "raid");

        Assert.Equal(2, picker.VisibleCount);
    }

    [Fact]
    public void MatchesJapaneseNames()
    {
        var picker = Loaded();
        picker.Filter(Compare, "浴衣");

        Assert.Equal(1, picker.VisibleCount);
        Assert.Equal("夏の浴衣", picker.Visible(0).Name);
    }

    [Fact]
    public void KeepsTicksWhileFiltering()
    {
        var picker = Loaded();
        picker.Filter(Compare, "winter");
        picker.Toggle(0);
        picker.Filter(Compare, string.Empty);

        Assert.Equal(1, picker.PickedCount);
        var picked = new List<(Guid Id, string Name)>();
        picker.CopyPicked(picked);
        Assert.Single(picked);
        Assert.Equal("Winter coat", picked[0].Name);
    }

    [Fact]
    public void NeverTicksADesignAlreadyInTheWardrobe()
    {
        var picker = Loaded();
        picker.Filter(Compare, "owned");
        picker.Toggle(0);

        Assert.Equal(0, picker.PickedCount);
    }

    [Fact]
    public void UntickingCountsDown()
    {
        var picker = Loaded();
        picker.Filter(Compare, string.Empty);
        picker.Toggle(1);
        picker.Toggle(1);

        Assert.Equal(0, picker.PickedCount);
    }

    [Theory]
    [InlineData("Raids/Savage/Winter coat", "Winter coat", "Raids/Savage")]
    [InlineData("Winter coat", "Winter coat", "")]
    [InlineData("Folder/Renamed", "Winter coat", "Folder/Renamed")]
    public void ReadsTheFolderFromTheFullPath(string path, string name, string folder) =>
        Assert.Equal(folder, DesignPicker.FolderOf(path, name));

    [Fact]
    public void BuildSortsByFolderThenName()
    {
        var designs = new Dictionary<Guid, (string Name, string Path, uint Color, bool Quick)>
        {
            [Guid.NewGuid()] = ("b", "Zeta/b", 0u, false),
            [Guid.NewGuid()] = ("a", "Alpha/a", 0u, false),
            [Guid.NewGuid()] = ("c", "Alpha/c", 0u, false),
        };

        var built = DesignPicker.Build(designs, _ => false);

        Assert.Equal("a", built[0].Name);
        Assert.Equal("c", built[1].Name);
        Assert.Equal("b", built[2].Name);
    }

    private static DesignPicker Loaded()
    {
        var picker = new DesignPicker();
        picker.Load(
        [
            new DesignChoice(Guid.NewGuid(), "Winter coat", "Raids/Savage", false),
            new DesignChoice(Guid.NewGuid(), "Summer dress", "Raids", false),
            new DesignChoice(Guid.NewGuid(), "夏の浴衣", "Festival", false),
            new DesignChoice(Guid.NewGuid(), "Owned look", string.Empty, true),
        ]);
        return picker;
    }
}
