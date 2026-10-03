using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class UnreadCountsTests
{
    private const string Velvet = "velvet";
    private const string Chirper = "chirper";

    [Fact]
    public void ExcludingSubtractsOnlyTheExcludedType()
    {
        var counts = Counts((Velvet, SocialActivity.TypeLike, 2), (Velvet, SocialActivity.TypeConnectRequest, 1));

        Assert.Equal(3, counts.Of(Velvet));
        Assert.Equal(2, counts.Excluding(Velvet, SocialActivity.TypeConnectRequest));
        Assert.Equal(3, counts.Excluding(Velvet, SocialActivity.TypeComment));
    }

    [Fact]
    public void ExcludingNeverGoesBelowZero()
    {
        var counts = new UnreadCounts(new Dictionary<string, int> { [Velvet] = 1 },
            new[] { new NotificationUnreadCountDto(Velvet, SocialActivity.TypeConnectRequest, 2) });

        Assert.Equal(0, counts.Excluding(Velvet, SocialActivity.TypeConnectRequest));
    }

    [Fact]
    public void ClearingAnAppClearsItsBreakdownAndLeavesTheOthers()
    {
        var counts = Counts((Velvet, SocialActivity.TypeLike, 2), (Chirper, SocialActivity.TypeLike, 4));

        var cleared = counts.WithoutApp(Velvet);

        Assert.Equal(0, cleared.Of(Velvet));
        Assert.Equal(0, cleared.Of(Velvet, SocialActivity.TypeLike));
        Assert.Equal(4, cleared.Of(Chirper));
        Assert.Equal(4, cleared.Of(Chirper, SocialActivity.TypeLike));
    }

    [Fact]
    public void RecountingAnAppCountsUnreadItemsAfterTheWatermarkPerType()
    {
        var counts = Counts((Velvet, SocialActivity.TypeLike, 9), (Chirper, SocialActivity.TypeLike, 4));
        var latest = new[]
        {
            Item("a", Velvet, SocialActivity.TypeLike, 200),
            Item("b", Velvet, SocialActivity.TypeLike, 210),
            Item("c", Velvet, SocialActivity.TypeConnectRequest, 220),
            Item("d", Velvet, SocialActivity.TypeLike, 100),
            Item("e", Velvet, SocialActivity.TypeLike, 230, read: true),
            Item("f", Chirper, SocialActivity.TypeLike, 240),
        };

        var recounted = counts.WithAppRecounted(Velvet, latest, 150);

        Assert.Equal(3, recounted.Of(Velvet));
        Assert.Equal(2, recounted.Of(Velvet, SocialActivity.TypeLike));
        Assert.Equal(1, recounted.Of(Velvet, SocialActivity.TypeConnectRequest));
        Assert.Equal(2, recounted.Excluding(Velvet, SocialActivity.TypeConnectRequest));
        Assert.Equal(4, recounted.Of(Chirper));
    }

    [Fact]
    public void CountsFromAnOlderServerKeepReportingNoBreakdown()
    {
        var counts = new UnreadCounts(new Dictionary<string, int> { [Velvet] = 2 }, null);

        Assert.False(counts.HasBreakdown);
        Assert.False(counts.WithoutApp(Velvet).HasBreakdown);
        Assert.False(counts.WithAppRecounted(Velvet, Array.Empty<NotificationDto>(), 0).HasBreakdown);
    }

    [Fact]
    public void EmptyCountsHaveNothingOutstanding()
    {
        Assert.False(UnreadCounts.Empty.AnyOutstanding);
        Assert.True(Counts((Velvet, SocialActivity.TypeLike, 1)).AnyOutstanding);
    }

    private static UnreadCounts Counts(params (string App, int Type, int Count)[] entries)
    {
        var byApp = new Dictionary<string, int>(StringComparer.Ordinal);
        var byType = new NotificationUnreadCountDto[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            byType[index] = new NotificationUnreadCountDto(entry.App, entry.Type, entry.Count);
            byApp[entry.App] = byApp.GetValueOrDefault(entry.App, 0) + entry.Count;
        }

        return new UnreadCounts(byApp, byType);
    }

    private static NotificationDto Item(string id, string app, int type, long createdAtUnix, bool read = false)
    {
        return new NotificationDto(id, type, app, null, "actor", "Actor", "Actor", "actor", null, null,
            createdAtUnix, Read: read);
    }
}
