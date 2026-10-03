using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const long WeekSeconds = 7L * 24L * 60L * 60L;

    private readonly CollectionsDigest digest = new();

    private CollectionsDigest Digest()
    {
        var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
        var language = Loc.Current.Code;
        if (digest.IsCurrent(catalog.Revision, journal.Revision, minute, language))
        {
            return digest;
        }

        if (tracking)
        {
            catalog.RequestSummary(lodestoneId);
        }

        digest.Clear();
        digest.CharacterName = gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        BuildTiles();
        BuildRecent();
        BuildWishlist();
        BuildUpNext();
        digest.Stamp(catalog.Revision, journal.Revision, minute, language);
        return digest;
    }

    private void BuildTiles()
    {
        var ownedSum = 0;
        var totalSum = 0;
        var categories = CollectionCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            var tile = digest.Tiles[index];
            var entry = catalog.RequestCatalog(category);
            var progress = Progress(category);
            tile.Fraction = 0f;
            tile.Percent = string.Empty;
            if (!tracking)
            {
                tile.Mode = TileMode.Plain;
                tile.Count = entry.Total > 0 ? entry.Total.ToString("N0", Loc.Culture) : string.Empty;
                continue;
            }

            if (progress is null)
            {
                tile.Mode = TileMode.Loading;
                tile.Count = string.Empty;
                continue;
            }

            if (!progress.HasPercent || progress.Total <= 0)
            {
                tile.Mode = lodestoneId is null ? TileMode.NotLinked : TileMode.Private;
                tile.Count = Loc.T(lodestoneId is null ? L.Collections.StateNotLinked : L.Collections.StatePrivate);
                continue;
            }

            tile.Mode = TileMode.Ring;
            tile.Fraction = Math.Clamp(progress.Count / (float)progress.Total, 0f, 1f);
            tile.Percent = Loc.T(L.Collections.Percent, (int)MathF.Floor(tile.Fraction * 100f));
            tile.Count = CountLabel(progress.Count, progress.Total);
            if (!CollectionsCatalogService.HasLocalUnlocks(category))
            {
                continue;
            }

            ownedSum += progress.Count;
            totalSum += progress.Total;
        }

        if (totalSum <= 0)
        {
            return;
        }

        digest.HasOverall = true;
        digest.OverallFraction = Math.Clamp(ownedSum / (float)totalSum, 0f, 1f);
        digest.OverallPercent = Loc.T(L.Collections.Percent, (int)MathF.Floor(digest.OverallFraction * 100f));
        digest.OverallCount = CountLabel(ownedSum, totalSum);
        var newCount = CountSince(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - WeekSeconds);
        digest.NewThisWeek = newCount > 0 ? Loc.Plural(L.Collections.NewThisWeek, newCount) : string.Empty;
    }

    private static string CountLabel(int count, int total) =>
        string.Concat(count.ToString("N0", Loc.Culture), " / ", total.ToString("N0", Loc.Culture));

    private int CountSince(long sinceUnix)
    {
        var recent = journal.Recent;
        var count = 0;
        for (var index = 0; index < recent.Count; index++)
        {
            if (recent[index].UnlockedUnix >= sinceUnix)
            {
                count++;
            }
        }

        return count;
    }

    private void BuildRecent()
    {
        if (!tracking)
        {
            return;
        }

        var recent = journal.Recent;
        for (var index = 0; index < recent.Count && digest.Recent.Count < CollectionsDigest.RecentLimit; index++)
        {
            var unlock = recent[index];
            var item = catalog.RequestCatalog(unlock.Category).Find(unlock.Id);
            if (item is null)
            {
                continue;
            }

            digest.Recent.Add(new DigestRow(item, TimeText.Ago(unlock.UnlockedUnix), RowBadge.None));
        }
    }

    private void BuildWishlist()
    {
        if (!tracking)
        {
            return;
        }

        var pins = journal.Pins;
        for (var index = 0; index < pins.Count; index++)
        {
            var pin = pins[index];
            var item = catalog.RequestCatalog(pin.Category).Find(pin.Id);
            if (item is null)
            {
                continue;
            }

            digest.Wishlist.Add(new DigestRow(item, item.RarityText, BadgeFor(item)));
        }
    }

    private void BuildUpNext()
    {
        if (!tracking)
        {
            return;
        }

        var categories = CollectionCategories.All;
        for (var index = 0; index < categories.Length; index++)
        {
            var category = categories[index];
            if (!CollectionsCatalogService.HasLocalUnlocks(category))
            {
                continue;
            }

            var entry = catalog.RequestCatalog(category);
            var owned = OwnedIds(category);
            if (entry.State != CollectionState.Ready || owned is null)
            {
                continue;
            }

            CollectionSuggestions.Collect(entry.Items, owned, digest.Candidates);
        }

        CollectionSuggestions.Rank(digest.Candidates, CollectionsDigest.UpNextLimit);
        for (var index = 0; index < digest.Candidates.Count; index++)
        {
            var item = digest.Candidates[index];
            digest.UpNext.Add(new DigestRow(item, item.RarityText, RowBadge.None));
        }
    }

    private RowBadge BadgeFor(CollectionItem item)
    {
        var owned = OwnedIds(item.Category);
        if (owned is null)
        {
            return RowBadge.None;
        }

        return owned.Contains(item.Id) ? RowBadge.Owned : RowBadge.Missing;
    }
}
