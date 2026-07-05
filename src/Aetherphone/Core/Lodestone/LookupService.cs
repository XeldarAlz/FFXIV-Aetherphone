using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Localization;
using NetStone.Model.Parseables.Character;
using NetStone.Model.Parseables.Character.ClassJob;
using NetStone.Model.Parseables.Character.Gear;
using NetStone.Model.Parseables.FreeCompany;
using NetStone.Model.Parseables.FreeCompany.Members;
using NetStone.Model.Parseables.Search.Character;
using NetStone.Model.Parseables.Search.FreeCompany;
using NetStone.Search.Character;
using NetStone.Search.FreeCompany;
using NetStone.StaticData;

namespace Aetherphone.Core.Lodestone;

internal sealed class CharacterSearchResult
{
    public volatile LookupState State = LookupState.Idle;

    public volatile CharacterMatch[] Matches = Array.Empty<CharacterMatch>();
}

internal sealed class FreeCompanySearchResult
{
    public volatile LookupState State = LookupState.Idle;

    public volatile FreeCompanyMatch[] Matches = Array.Empty<FreeCompanyMatch>();
}

internal sealed class CharacterDetailResult
{
    public volatile LookupState State = LookupState.Idle;

    public volatile CharacterDetail? Detail;
}

internal sealed class FreeCompanyDetailResult
{
    public volatile LookupState State = LookupState.Idle;

    public volatile FreeCompanyDetail? Detail;

    public volatile RosterSnapshot Roster = RosterSnapshot.Empty;

    public int RosterLoadingFlag;

    public bool RosterLoading => Volatile.Read(ref RosterLoadingFlag) == 1;
}

internal sealed class LookupService : IDisposable
{
    private const int MaxResults = 30;
    private const int RosterPageSize = 50;

    private static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DetailTtl = TimeSpan.FromMinutes(15);

    private readonly LodestoneService lodestone;
    private readonly CancellationTokenSource cancellation = new();

    private readonly Dictionary<string, CacheEntry<CharacterSearchResult>> characterSearches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CacheEntry<FreeCompanySearchResult>> freeCompanySearches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CacheEntry<CharacterDetailResult>> characterDetails = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CacheEntry<FreeCompanyDetailResult>> freeCompanyDetails = new(StringComparer.Ordinal);

    private readonly object sync = new();

    public LookupService(LodestoneService lodestone)
    {
        this.lodestone = lodestone;
    }

    public CharacterSearchResult SearchCharacters(string name, string region, bool regionIsDataCenter, bool force)
    {
        var trimmedName = name.Trim();
        var trimmedRegion = region.Trim();
        var key = string.Concat(trimmedName, "|", regionIsDataCenter ? "dc:" : "w:", trimmedRegion);

        lock (sync)
        {
            if (!force && characterSearches.TryGetValue(key, out var cached) && !cached.IsStale(SearchTtl))
            {
                return cached.Value;
            }

            var entry = new CacheEntry<CharacterSearchResult>(new CharacterSearchResult());
            entry.Value.State = LookupState.Loading;
            characterSearches[key] = entry;
            _ = RunCharacterSearchAsync(entry.Value, trimmedName, trimmedRegion, regionIsDataCenter);
            return entry.Value;
        }
    }

    public FreeCompanySearchResult SearchFreeCompanies(string name, string region, bool regionIsDataCenter, bool force)
    {
        var trimmedName = name.Trim();
        var trimmedRegion = region.Trim();
        var key = string.Concat(trimmedName, "|", regionIsDataCenter ? "dc:" : "w:", trimmedRegion);

        lock (sync)
        {
            if (!force && freeCompanySearches.TryGetValue(key, out var cached) && !cached.IsStale(SearchTtl))
            {
                return cached.Value;
            }

            var entry = new CacheEntry<FreeCompanySearchResult>(new FreeCompanySearchResult());
            entry.Value.State = LookupState.Loading;
            freeCompanySearches[key] = entry;
            _ = RunFreeCompanySearchAsync(entry.Value, trimmedName, trimmedRegion, regionIsDataCenter);
            return entry.Value;
        }
    }

    public CharacterDetailResult CharacterDetail(string id, string fallbackName, string fallbackWorld, bool force)
    {
        lock (sync)
        {
            if (!force && characterDetails.TryGetValue(id, out var cached) && !cached.IsStale(DetailTtl))
            {
                return cached.Value;
            }

            var entry = new CacheEntry<CharacterDetailResult>(new CharacterDetailResult());
            entry.Value.State = LookupState.Loading;
            characterDetails[id] = entry;
            _ = RunCharacterDetailAsync(entry.Value, id, fallbackName, fallbackWorld);
            return entry.Value;
        }
    }

    public FreeCompanyDetailResult FreeCompanyDetail(string id, bool force)
    {
        lock (sync)
        {
            if (!force && freeCompanyDetails.TryGetValue(id, out var cached) && !cached.IsStale(DetailTtl))
            {
                return cached.Value;
            }

            var entry = new CacheEntry<FreeCompanyDetailResult>(new FreeCompanyDetailResult());
            entry.Value.State = LookupState.Loading;
            freeCompanyDetails[id] = entry;
            _ = RunFreeCompanyDetailAsync(entry.Value, id);
            return entry.Value;
        }
    }

    public void RequestRosterPage(string id, FreeCompanyDetailResult result, int page)
    {
        var snapshot = result.Roster;
        if (page < 0 || page >= snapshot.PageCount)
        {
            return;
        }

        if (page == snapshot.Page && snapshot.Members.Length > 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref result.RosterLoadingFlag, 1, 0) != 0)
        {
            return;
        }

        _ = RunRosterPageAsync(result, id, page);
    }

    private async Task RunCharacterSearchAsync(CharacterSearchResult target, string name, string region, bool regionIsDataCenter)
    {
        try
        {
            var token = cancellation.Token;
            using (await lodestone.ThrottleAsync(token).ConfigureAwait(false))
            {
                var client = await lodestone.ClientAsync(token).ConfigureAwait(false);
                if (client is null)
                {
                    target.State = LookupState.Failed;
                    return;
                }

                var query = new CharacterSearchQuery { CharacterName = name };
                if (region.Length > 0)
                {
                    if (regionIsDataCenter)
                    {
                        query.DataCenter = region;
                    }
                    else
                    {
                        query.World = region;
                    }
                }

                var page = await client.SearchCharacter(query).WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
                var matches = MapCharacterMatches(page);
                target.Matches = matches;
                target.State = matches.Length > 0 ? LookupState.Ready : LookupState.Empty;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            target.State = LookupState.Failed;
            AepLog.Warning($"Lookup character search failed: {exception.Message}");
        }
    }

    private static CharacterMatch[] MapCharacterMatches(CharacterSearchPage? page)
    {
        if (page?.Results is null)
        {
            return Array.Empty<CharacterMatch>();
        }

        var list = new List<CharacterMatch>(MaxResults);
        foreach (var entry in page.Results)
        {
            if (entry?.Id is null || entry.Name is null)
            {
                continue;
            }

            list.Add(new CharacterMatch(entry.Id, entry.Name, string.Empty));
            if (list.Count >= MaxResults)
            {
                break;
            }
        }

        return list.ToArray();
    }

    private async Task RunFreeCompanySearchAsync(FreeCompanySearchResult target, string name, string region, bool regionIsDataCenter)
    {
        try
        {
            var token = cancellation.Token;
            using (await lodestone.ThrottleAsync(token).ConfigureAwait(false))
            {
                var client = await lodestone.ClientAsync(token).ConfigureAwait(false);
                if (client is null)
                {
                    target.State = LookupState.Failed;
                    return;
                }

                var query = new FreeCompanySearchQuery { Name = name };
                if (region.Length > 0)
                {
                    if (regionIsDataCenter)
                    {
                        query.DataCenter = region;
                    }
                    else
                    {
                        query.World = region;
                    }
                }

                var page = await client.SearchFreeCompany(query).WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
                var matches = MapFreeCompanyMatches(page);
                target.Matches = matches;
                target.State = matches.Length > 0 ? LookupState.Ready : LookupState.Empty;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            target.State = LookupState.Failed;
            AepLog.Warning($"Lookup free company search failed: {exception.Message}");
        }
    }

    private static FreeCompanyMatch[] MapFreeCompanyMatches(FreeCompanySearchPage? page)
    {
        if (page?.Results is null)
        {
            return Array.Empty<FreeCompanyMatch>();
        }

        var list = new List<FreeCompanyMatch>(MaxResults);
        foreach (var entry in page.Results)
        {
            if (entry?.Id is null || entry.Name is null)
            {
                continue;
            }

            var world = CleanServer(entry.Server);
            var subtitle = entry.ActiveMembers > 0
                ? (world.Length > 0 ? string.Concat(world, "  ·  ", Loc.T(L.FindPeople.Active, entry.ActiveMembers)) : Loc.T(L.FindPeople.Active, entry.ActiveMembers))
                : world;
            list.Add(new FreeCompanyMatch(
                entry.Id,
                entry.Name,
                world,
                subtitle,
                entry.RecruitmentOpen,
                entry.CrestLayers?.TopLayer ?? entry.CrestLayers?.MiddleLayer ?? entry.CrestLayers?.BottomLayer,
                CrestKey(entry.Id)));
            if (list.Count >= MaxResults)
            {
                break;
            }
        }

        return list.ToArray();
    }

    private async Task RunCharacterDetailAsync(CharacterDetailResult target, string id, string fallbackName, string fallbackWorld)
    {
        try
        {
            var token = cancellation.Token;
            using (await lodestone.ThrottleAsync(token).ConfigureAwait(false))
            {
                var client = await lodestone.ClientAsync(token).ConfigureAwait(false);
                if (client is null)
                {
                    target.State = LookupState.Failed;
                    return;
                }

                var character = await client.GetCharacter(id).WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
                if (character is null)
                {
                    target.State = LookupState.Empty;
                    return;
                }

                var jobs = await BuildJobsAsync(character, token).ConfigureAwait(false);
                target.Detail = BuildCharacterDetail(id, character, fallbackName, fallbackWorld, jobs);
                target.State = LookupState.Ready;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            target.State = LookupState.Failed;
            AepLog.Warning($"Lookup character detail failed: {exception.Message}");
        }
    }

    private static async Task<ClassJobLevel[]> BuildJobsAsync(LodestoneCharacter character, CancellationToken token)
    {
        try
        {
            var info = await character.GetClassJobInfo().WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
            return MapJobs(info);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Lookup class job info failed: {exception.Message}");
            return Array.Empty<ClassJobLevel>();
        }
    }

    private static ClassJobLevel[] MapJobs(CharacterClassJob? info)
    {
        if (info?.ClassJobDict is null)
        {
            return Array.Empty<ClassJobLevel>();
        }

        var rankWord = Loc.T(L.FindPeople.Rank);
        var indexByName = new Dictionary<string, int>(32, StringComparer.Ordinal);
        var list = new List<ClassJobLevel>(32);
        foreach (var pair in info.ClassJobDict)
        {
            var entry = pair.Value;
            if (entry is null || !entry.IsUnlocked || entry.Level <= 0 || entry.Name is null)
            {
                continue;
            }

            var category = Categorize(pair.Key);
            var label = string.Concat(rankWord, " ", entry.Level.ToString());
            if (indexByName.TryGetValue(entry.Name, out var existingIndex))
            {
                if (entry.Level > list[existingIndex].Level)
                {
                    list[existingIndex] = new ClassJobLevel(entry.Name, entry.Level, label, category);
                }

                continue;
            }

            indexByName[entry.Name] = list.Count;
            list.Add(new ClassJobLevel(entry.Name, entry.Level, label, category));
        }

        list.Sort(static (left, right) =>
        {
            var byLevel = right.Level.CompareTo(left.Level);
            return byLevel != 0 ? byLevel : string.CompareOrdinal(left.Name, right.Name);
        });
        return list.ToArray();
    }

    private static JobCategory Categorize(ClassJob job)
    {
        switch (job)
        {
            case ClassJob.Carpenter:
            case ClassJob.Blacksmith:
            case ClassJob.Armorer:
            case ClassJob.Goldsmith:
            case ClassJob.Leatherworker:
            case ClassJob.Weaver:
            case ClassJob.Alchemist:
            case ClassJob.Culinarian:
                return JobCategory.Crafter;
            case ClassJob.Miner:
            case ClassJob.Botanist:
            case ClassJob.Fisher:
                return JobCategory.Gatherer;
            default:
                return JobCategory.Combat;
        }
    }

    private static CharacterDetail BuildCharacterDetail(string id, LodestoneCharacter character, string fallbackName, string fallbackWorld, ClassJobLevel[] jobs)
    {
        var world = character.Server is { Length: > 0 } server ? CleanServer(server) : fallbackWorld;
        var name = character.Name is { Length: > 0 } ? character.Name : fallbackName;
        var grandCompany = character.GrandCompanyName is { Length: > 0 } gc
            ? character.GrandCompanyRank is { Length: > 0 } rank ? string.Concat(gc, " · ", rank) : gc
            : string.Empty;
        var freeCompany = character.FreeCompany is { Exists: true, Name: { Length: > 0 } fcName } ? fcName : string.Empty;

        return new CharacterDetail(
            id,
            name,
            character.Title ?? string.Empty,
            world,
            BuildRaceClan(character),
            grandCompany,
            freeCompany,
            character.Portrait,
            PortraitKey(id),
            jobs,
            MapGear(character.Gear));
    }

    private static string BuildRaceClan(LodestoneCharacter character)
    {
        var race = character.Race ?? string.Empty;
        var tribe = character.Tribe ?? string.Empty;
        if (race.Length > 0 && tribe.Length > 0)
        {
            return string.Concat(race, " · ", tribe);
        }

        return race.Length > 0 ? race : tribe;
    }

    private static GearPiece[] MapGear(CharacterGear? gear)
    {
        if (gear is null)
        {
            return Array.Empty<GearPiece>();
        }

        var slots = new GearEntry?[]
        {
            gear.Mainhand,
            gear.Offhand,
            gear.Head,
            gear.Body,
            gear.Hands,
            gear.Legs,
            gear.Feet,
            gear.Earrings,
            gear.Necklace,
            gear.Bracelets,
            gear.Ring1,
            gear.Ring2,
        };

        var list = new List<GearPiece>(slots.Length);
        for (var index = 0; index < slots.Length; index++)
        {
            var entry = slots[index];
            if (entry is null || !entry.Exists || entry.ItemName is not { Length: > 0 })
            {
                continue;
            }

            var label = entry.ItemLevel > 0 ? string.Concat("i", entry.ItemLevel.ToString()) : string.Empty;
            list.Add(new GearPiece(entry.StrippedItemName ?? entry.ItemName, label));
        }

        return list.ToArray();
    }

    private async Task RunFreeCompanyDetailAsync(FreeCompanyDetailResult target, string id)
    {
        var ownsRosterLoad = false;
        try
        {
            var token = cancellation.Token;
            using (await lodestone.ThrottleAsync(token).ConfigureAwait(false))
            {
                var client = await lodestone.ClientAsync(token).ConfigureAwait(false);
                if (client is null)
                {
                    target.State = LookupState.Failed;
                    return;
                }

                var company = await client.GetFreeCompany(id).WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
                if (company is null)
                {
                    target.State = LookupState.Empty;
                    return;
                }

                target.Detail = BuildFreeCompanyDetail(id, company);
                target.State = LookupState.Ready;
            }

            ownsRosterLoad = Interlocked.CompareExchange(ref target.RosterLoadingFlag, 1, 0) == 0;
            if (ownsRosterLoad)
            {
                await LoadRosterPageAsync(target, id, 0, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            target.State = LookupState.Failed;
            AepLog.Warning($"Lookup free company detail failed: {exception.Message}");
        }
        finally
        {
            if (ownsRosterLoad)
            {
                Interlocked.Exchange(ref target.RosterLoadingFlag, 0);
            }
        }
    }

    private static FreeCompanyDetail BuildFreeCompanyDetail(string id, LodestoneFreeCompany company)
    {
        var name = company.Name ?? string.Empty;
        var tag = company.Tag ?? string.Empty;
        var heading = tag.Length > 0 ? string.Concat(name, "  «", tag, "»") : name;
        var recruiting = company.Recruitment is { Length: > 0 } recruitment && recruitment.StartsWith("Open", StringComparison.OrdinalIgnoreCase);

        return new FreeCompanyDetail(
            id,
            name,
            tag,
            heading,
            company.World ?? string.Empty,
            company.Slogan ?? string.Empty,
            Loc.T(L.FindPeople.Members, company.ActiveMemberCount),
            recruiting,
            company.CrestLayers?.TopLayer ?? company.CrestLayers?.MiddleLayer ?? company.CrestLayers?.BottomLayer,
            CrestKey(id));
    }

    private async Task RunRosterPageAsync(FreeCompanyDetailResult target, string id, int page)
    {
        try
        {
            var token = cancellation.Token;
            using (await lodestone.ThrottleAsync(token).ConfigureAwait(false))
            {
                await LoadRosterPageAsync(target, id, page, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Lookup roster page failed: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref target.RosterLoadingFlag, 0);
        }
    }

    private async Task LoadRosterPageAsync(FreeCompanyDetailResult target, string id, int page, CancellationToken token)
    {
        var client = await lodestone.ClientAsync(token).ConfigureAwait(false);
        if (client is null)
        {
            return;
        }

        var members = await client.GetFreeCompanyMembers(id, page + 1).WaitAsync(LodestoneService.NetStoneTimeout, token).ConfigureAwait(false);
        if (members?.Members is null)
        {
            return;
        }

        target.Roster = new RosterSnapshot(MapRoster(members), page, Math.Max(members.NumPages, 1));
    }

    private static RosterMember[] MapRoster(FreeCompanyMembers members)
    {
        var list = new List<RosterMember>(RosterPageSize);
        foreach (var member in members.Members)
        {
            if (member?.Id is null || member.Name is null)
            {
                continue;
            }

            var world = CleanServer(member.Server);
            var rank = member.FreeCompanyRank ?? string.Empty;
            var subtitle = world.Length > 0 ? string.Concat(rank, "  ·  ", world) : rank;
            list.Add(new RosterMember(member.Id, member.Name, subtitle, world, member.Avatar, MemberAvatarKey(member.Id)));
        }

        return list.ToArray();
    }

    private static string CrestKey(string id) => string.Concat("findpeople:crest:", id);

    private static string PortraitKey(string id) => string.Concat("findpeople:portrait:", id);

    private static string MemberAvatarKey(string id) => string.Concat("findpeople:member:", id);

    private static string CleanServer(string? server)
    {
        if (string.IsNullOrEmpty(server))
        {
            return string.Empty;
        }

        var bracket = server.IndexOf('[');
        if (bracket > 0)
        {
            return server.Substring(0, bracket).Trim();
        }

        return server.Trim();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private sealed class CacheEntry<T>
    {
        private readonly DateTime created = DateTime.UtcNow;

        public CacheEntry(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public bool IsStale(TimeSpan ttl) => DateTime.UtcNow - created >= ttl;
    }
}
