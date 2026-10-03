using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Songs;

internal readonly struct CatalogShelf
{
    public readonly string Key;
    public readonly LocString Title;
    public readonly CatalogRequest Request;

    public CatalogShelf(string key, LocString title, CatalogRequest request)
    {
        Key = key;
        Title = title;
        Request = request;
    }
}

internal readonly struct CatalogHero
{
    public readonly CatalogShelf Shelf;
    public readonly LocString Subtitle;

    public CatalogHero(CatalogShelf shelf, LocString subtitle)
    {
        Shelf = shelf;
        Subtitle = subtitle;
    }
}

internal readonly struct CatalogGenre
{
    public readonly string Key;
    public readonly LocString Title;
    public readonly FontAwesomeIcon Icon;
    public readonly int Hue;
    public readonly CatalogShelf[] Shelves;

    public CatalogGenre(string key, LocString title, FontAwesomeIcon icon, int hue, CatalogShelf[] shelves)
    {
        Key = key;
        Title = title;
        Icon = icon;
        Hue = hue;
        Shelves = shelves;
    }
}

internal static class MusicCatalogShelves
{
    public const string MixFavourites = "mix.favourites";
    public const string MixDiscovery = "mix.discovery";
    public const string MixBecause = "mix.because";
    public const string PlaylistPrefix = "playlist.";
    public const string GenrePrefix = "genre.";
    private const string PlaylistUrlPrefix = "https://www.youtube.com/playlist?list=";

    public static readonly CatalogHero[] Heroes =
    [
        Hero("hero.orchestra", L.Music.New.HeroOrchestraTitle, L.Music.New.HeroOrchestraSub,
            "final fantasy xiv orchestral arrangement"),
        Hero("hero.lofi", L.Music.New.HeroLofiTitle, L.Music.New.HeroLofiSub, "lofi chill beats to relax"),
        Hero("hero.anime", L.Music.New.HeroAnimeTitle, L.Music.New.HeroAnimeSub, "best anime openings"),
        Hero("hero.jrpg", L.Music.New.HeroJrpgTitle, L.Music.New.HeroJrpgSub, "jrpg battle themes"),
        Hero("hero.pixel", L.Music.New.HeroPixelTitle, L.Music.New.HeroPixelSub, "8 bit chiptune game music"),
    ];

    public static readonly CatalogShelf Releases =
        Shelf("new.releases", L.Music.New.Releases, "new music this week official audio");

    public static readonly CatalogShelf Trending =
        Shelf("new.trending", L.Music.New.Trending, "trending songs official music video");

    public static readonly CatalogShelf Chart = Shelf("new.chart", L.Music.New.Chart, "top hits this week");

    public static readonly CatalogShelf[] GameShelves =
    [
        Shelf("new.ffxiv", L.Music.New.Ffxiv, "final fantasy xiv soundtrack"),
        Shelf("new.games", L.Music.New.Games, "video game soundtrack orchestral"),
        Shelf("new.lofi", L.Music.New.Lofi, "lofi hip hop beats"),
        Shelf("new.jpop", L.Music.New.Jpop, "j-pop hits"),
        Shelf("new.anime", L.Music.New.Anime, "anime opening full song"),
        Shelf("new.chiptune", L.Music.New.Chiptune, "chiptune music"),
    ];

    public static readonly CatalogShelf[] Starter =
    [
        Shelf("home.ffxiv.essentials", L.Music.Home.StarterEssentials, "final fantasy xiv soundtrack best"),
        Shelf("home.ffxiv.battles", L.Music.Home.StarterBattles, "final fantasy xiv boss theme"),
        Shelf("home.ffxiv.calm", L.Music.Home.StarterCalm, "final fantasy xiv relaxing music"),
    ];

    public static readonly CatalogShelf Chill = new("mix.chill", L.Music.Home.ChillMix,
        CatalogRequest.Query("chill relaxing music mix", MusicCatalog.MixTtlSeconds));

    public static readonly CatalogGenre[] Genres =
    [
        Genre("pop", L.Music.New.GenrePop, FontAwesomeIcon.Star, 22, "pop"),
        Genre("hiphop", L.Music.New.GenreHipHop, FontAwesomeIcon.Microphone, 2, "hip hop"),
        Genre("rock", L.Music.New.GenreRock, FontAwesomeIcon.Fire, 0, "rock"),
        Genre("electronic", L.Music.New.GenreElectronic, FontAwesomeIcon.Bolt, 15, "electronic dance"),
        Genre("lofi", L.Music.New.GenreLofi, FontAwesomeIcon.Cloud, 6, "lofi"),
        Genre("jazz", L.Music.New.GenreJazz, FontAwesomeIcon.Music, 4, "jazz"),
        Genre("classical", L.Music.New.GenreClassical, FontAwesomeIcon.Feather, 9, "classical music"),
        Genre("game", L.Music.New.GenreGame, FontAwesomeIcon.Gamepad, 17, "video game music"),
        Genre("anime", L.Music.New.GenreAnime, FontAwesomeIcon.Dragon, 20, "anime songs"),
        Genre("jpop", L.Music.New.GenreJpop, FontAwesomeIcon.Heart, 23, "j-pop"),
        Genre("kpop", L.Music.New.GenreKpop, FontAwesomeIcon.Gem, 19, "k-pop"),
        Genre("metal", L.Music.New.GenreMetal, FontAwesomeIcon.Skull, 13, "metal"),
        Genre("rnb", L.Music.New.GenreRnb, FontAwesomeIcon.Moon, 11, "r&b"),
        Genre("chiptune", L.Music.New.GenreChiptune, FontAwesomeIcon.Robot, 8, "chiptune"),
    ];

    private static readonly Dictionary<string, CatalogShelf> ByKey = Index();

    public static bool TryFind(string key, out CatalogShelf shelf) => ByKey.TryGetValue(key, out shelf);

    public static int GenreIndex(string key)
    {
        for (var index = 0; index < Genres.Length; index++)
        {
            if (string.Equals(Genres[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool IsPlaylistKey(string key) => key.StartsWith(PlaylistPrefix, StringComparison.Ordinal);

    public static string PlaylistKey(string playlistId) => PlaylistPrefix + playlistId;

    public static string PlaylistUrl(string playlistKey) =>
        string.Concat(PlaylistUrlPrefix, playlistKey.AsSpan(PlaylistPrefix.Length));

    private static CatalogShelf Shelf(string key, LocString title, string query) =>
        new(key, title, CatalogRequest.Query(query));

    private static CatalogHero Hero(string key, LocString title, LocString subtitle, string query) =>
        new(Shelf(key, title, query), subtitle);

    private static CatalogGenre Genre(string id, LocString title, FontAwesomeIcon icon, int hue, string term)
    {
        var key = GenrePrefix + id;
        CatalogShelf[] shelves =
        [
            Shelf(key + ".hits", L.Music.New.ShelfHits, term + " hits"),
            Shelf(key + ".fresh", L.Music.New.ShelfFresh, "new " + term + " songs"),
            Shelf(key + ".essentials", L.Music.New.ShelfEssentials, term + " classics"),
        ];
        return new CatalogGenre(key, title, icon, hue, shelves);
    }

    private static Dictionary<string, CatalogShelf> Index()
    {
        var index = new Dictionary<string, CatalogShelf>(StringComparer.Ordinal);
        for (var heroIndex = 0; heroIndex < Heroes.Length; heroIndex++)
        {
            index[Heroes[heroIndex].Shelf.Key] = Heroes[heroIndex].Shelf;
        }

        index[Releases.Key] = Releases;
        index[Trending.Key] = Trending;
        index[Chart.Key] = Chart;
        index[Chill.Key] = Chill;
        AddAll(index, GameShelves);
        AddAll(index, Starter);
        for (var genreIndex = 0; genreIndex < Genres.Length; genreIndex++)
        {
            AddAll(index, Genres[genreIndex].Shelves);
        }

        return index;
    }

    private static void AddAll(Dictionary<string, CatalogShelf> index, CatalogShelf[] shelves)
    {
        for (var shelfIndex = 0; shelfIndex < shelves.Length; shelfIndex++)
        {
            index[shelves[shelfIndex].Key] = shelves[shelfIndex];
        }
    }
}
