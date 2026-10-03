using Aetherphone.Core.Shell.Spotlight;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RecentLaunchesTests
{
    [Fact]
    public void Starts_Empty()
    {
        var recents = new RecentLaunches();

        Assert.Equal(0, recents.Count);
        Assert.Equal(string.Empty, recents[0]);
    }

    [Fact]
    public void Newest_Launch_Comes_First()
    {
        var recents = new RecentLaunches();
        recents.Note("notes");
        recents.Note("music");
        recents.Note("maps");

        Assert.Equal(3, recents.Count);
        Assert.Equal("maps", recents[0]);
        Assert.Equal("music", recents[1]);
        Assert.Equal("notes", recents[2]);
    }

    [Fact]
    public void Relaunch_Moves_The_App_To_The_Front_Without_A_Duplicate()
    {
        var recents = new RecentLaunches();
        recents.Note("notes");
        recents.Note("music");
        recents.Note("maps");
        recents.Note("notes");

        Assert.Equal(3, recents.Count);
        Assert.Equal("notes", recents[0]);
        Assert.Equal("maps", recents[1]);
        Assert.Equal("music", recents[2]);
    }

    [Fact]
    public void Relaunching_The_Newest_Keeps_The_Order()
    {
        var recents = new RecentLaunches();
        recents.Note("notes");
        recents.Note("music");
        recents.Note("music");

        Assert.Equal(2, recents.Count);
        Assert.Equal("music", recents[0]);
        Assert.Equal("notes", recents[1]);
    }

    [Fact]
    public void Caps_At_Capacity_And_Drops_The_Oldest()
    {
        var recents = new RecentLaunches();
        for (var launch = 0; launch < RecentLaunches.Capacity + 2; launch++)
        {
            recents.Note(string.Concat("app", launch));
        }

        Assert.Equal(RecentLaunches.Capacity, recents.Count);
        Assert.Equal("app7", recents[0]);
        Assert.Equal("app2", recents[RecentLaunches.Capacity - 1]);
        Assert.Equal(string.Empty, recents[RecentLaunches.Capacity]);
    }

    [Fact]
    public void Ignores_Empty_Ids()
    {
        var recents = new RecentLaunches();
        recents.Note(string.Empty);
        recents.Note(null!);

        Assert.Equal(0, recents.Count);
    }
}
