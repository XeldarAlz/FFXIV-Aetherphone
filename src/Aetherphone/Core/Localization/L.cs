namespace Aetherphone.Core.Localization;

internal static class L
{
    internal static class Common
    {
        public static readonly LocString Loading = new("common.loading", "Loading…");

        public static readonly LocString Searching = new("common.searching", "Searching…");

        public static readonly LocString Search = new("common.search", "Search");

        public static readonly LocString Cancel = new("common.cancel", "Cancel");

        public static readonly LocString Alerts = new("common.alerts", "Alerts");

        public static readonly LocString Live = new("common.live", "LIVE");

        public static readonly LocString Hq = new("common.hq", "HQ");

        public static readonly LocString Nq = new("common.nq", "NQ");

        public static readonly LocString ComingSoon = new("common.comingSoon", "Coming soon");
    }

    internal static class Apps
    {
        public static readonly LocString Messages = new("app.messages", "Messages");

        public static readonly LocString Contacts = new("app.contacts", "Contacts");

        public static readonly LocString Character = new("app.character", "Character");

        public static readonly LocString Chirper = new("app.chirper", "Chirper");

        public static readonly LocString Camera = new("app.camera", "Camera");

        public static readonly LocString Photos = new("app.photos", "Photos");

        public static readonly LocString Skywatcher = new("app.skywatcher", "Skywatcher");

        public static readonly LocString Market = new("app.market", "Market");

        public static readonly LocString Wallet = new("app.wallet", "Wallet");

        public static readonly LocString Music = new("app.music", "Music");

        public static readonly LocString Clock = new("app.clock", "Clock");

        public static readonly LocString Games = new("app.games", "Games");

        public static readonly LocString Notifications = new("app.notifications", "Notifications");

        public static readonly LocString Settings = new("app.settings", "Settings");
    }

    internal static class Settings
    {
        public static readonly LocString Title = new("settings.title", "Settings");

        public static readonly LocString Appearance = new("settings.appearance", "Appearance");

        public static readonly LocString Theme = new("settings.theme", "Theme");

        public static readonly LocString Accent = new("settings.accent", "Accent");

        public static readonly LocString Wallpaper = new("settings.wallpaper", "Wallpaper");

        public static readonly LocString TextSize = new("settings.textSize", "Text Size");

        public static readonly LocString Notifications = new("settings.notifications", "Notifications");

        public static readonly LocString DoNotDisturb = new("settings.doNotDisturb", "Do Not Disturb");

        public static readonly LocString Ringtone = new("settings.ringtone", "Ringtone");

        public static readonly LocString Sound = new("settings.sound", "Sound");

        public static readonly LocString Language = new("settings.language", "Language");

        public static readonly LocString About = new("settings.about", "About");

        public static readonly LocString Information = new("settings.information", "Information");

        public static readonly LocString Plugin = new("settings.plugin", "Plugin");

        public static readonly LocString Version = new("settings.version", "Version");

        public static readonly LocString Command = new("settings.command", "Command");

        public static readonly LocString CreditsLinks = new("settings.creditsLinks", "Credits & links");

        public static readonly LocString AboutAetherphone = new("settings.aboutAetherphone", "About Aetherphone");
    }

    internal static class Wallpaper
    {
        public static readonly LocString Title = new("wallpaper.title", "Wallpaper");

        public static readonly LocString MoveAndScale = new("wallpaper.moveAndScale", "Move and Scale");

        public static readonly LocString Add = new("wallpaper.add", "Add Wallpaper");

        public static readonly LocString FromPhotos = new("wallpaper.fromPhotos", "Photos");

        public static readonly LocString FromFiles = new("wallpaper.fromFiles", "Files");

        public static readonly LocString Set = new("wallpaper.set", "Set Wallpaper");

        public static readonly LocString LoadFailed = new("wallpaper.loadFailed", "Couldn't load that image");

        public static readonly LocString Custom = new("wallpaper.custom", "Custom");

        public static readonly LocString GestureHint = new("wallpaper.gestureHint", "Drag to move · scroll to zoom");

        public static readonly LocString Light = new("wallpaper.light", "Light");

        public static readonly LocString Dark = new("wallpaper.dark", "Dark");
    }

    internal static class Account
    {
        public static readonly LocString Title = new("account.title", "Aethernet Account");

        public static readonly LocString SignedIn = new("account.signedIn", "Signed in");

        public static readonly LocString NotSignedIn = new("account.notSignedIn", "Not signed in");

        public static readonly LocString LogInFirst = new("account.logInFirst", "Log in to your character first");

        public static readonly LocString SignInIntro = new("account.signInIntro", "Sign in to Aethernet to use Chirper. Ownership is verified through your Lodestone profile — no password.");

        public static readonly LocString AddCode = new("account.addCode", "Add this code to your Lodestone profile:");

        public static readonly LocString CopyCode = new("account.copyCode", "Copy code");

        public static readonly LocString OpenProfile = new("account.openProfile", "Open Lodestone profile");

        public static readonly LocString VerifyAdded = new("account.verifyAdded", "I've added it — Verify");

        public static readonly LocString RequestingCode = new("account.requestingCode", "Requesting a code…");

        public static readonly LocString CannotReach = new("account.cannotReach", "Could not reach Aethernet. Is the server running?");

        public static readonly LocString Verifying = new("account.verifying", "Verifying via Lodestone…");

        public static readonly LocString CodeNotFound = new("account.codeNotFound", "Code not found on your profile yet. Save it on Lodestone, then Verify again.");

        public static readonly LocString SignOut = new("account.signOut", "Sign out");

        public static readonly LocString SignIn = new("account.signIn", "Sign in with Lodestone");

        public static readonly LocPlural Followers = new("account.followers", "{0} follower", "{0} followers");

        public static readonly LocPlural Following = new("account.following", "{0} following", "{0} following");
    }

    internal static class Music
    {
        public static readonly LocString RadioStations = new("music.radioStations", "Radio stations");

        public static readonly LocString RecentlyPlayed = new("music.recentlyPlayed", "Recently played");

        public static readonly LocString TuningIn = new("music.tuningIn", "Tuning in…");

        public static readonly LocString NoStations = new("music.noStations", "No stations found");

        public static readonly LocString NoResults = new("music.noResults", "No results");

        public static readonly LocString SearchForSong = new("music.searchForSong", "Search for a song");

        public static readonly LocString NowPlaying = new("music.nowPlaying", "Now Playing");

        public static readonly LocString SearchSongs = new("music.searchSongs", "Search songs");

        public static readonly LocString LiveLower = new("music.liveLower", "live");

        public static readonly LocString Buffering = new("music.buffering", "Buffering…");

        public static readonly LocString Playing = new("music.playing", "Playing");

        public static readonly LocString ConnectionLost = new("music.connectionLost", "Connection lost");

        public static readonly LocString CouldntPlay = new("music.couldntPlay", "Couldn't play this track");

        public static readonly LocString NowPlayingState = new("music.nowPlayingState", "Now playing");

        public static readonly LocString PlaybackFailed = new("music.playbackFailed", "Playback failed");
    }

    internal static class Messages
    {
        public static readonly LocString Empty = new("messages.empty", "No messages yet");

        public static readonly LocString Placeholder = new("messages.placeholder", "Message");
    }

    internal static class Character
    {
        public static readonly LocString LogInToView = new("character.logInToView", "Log in to view your character");

        public static readonly LocString Profile = new("character.profile", "Profile");

        public static readonly LocString Equipment = new("character.equipment", "Equipment");

        public static readonly LocString Race = new("character.race", "Race");

        public static readonly LocString Clan = new("character.clan", "Clan");

        public static readonly LocString Gender = new("character.gender", "Gender");

        public static readonly LocString Nameday = new("character.nameday", "Nameday");

        public static readonly LocString Guardian = new("character.guardian", "Guardian");

        public static readonly LocString CityState = new("character.cityState", "City-state");

        public static readonly LocString GrandCompany = new("character.grandCompany", "Grand Company");
    }

    internal static class Camera
    {
        public static readonly LocString ModeSquare = new("camera.modeSquare", "SQUARE");

        public static readonly LocString ModePhoto = new("camera.modePhoto", "PHOTO");

        public static readonly LocString ModePano = new("camera.modePano", "PANO");
    }

    internal static class Contacts
    {
        public static readonly LocString Empty = new("contacts.empty", "Open your in-game friend list once");

        public static readonly LocString Online = new("contacts.online", "Online");

        public static readonly LocString Offline = new("contacts.offline", "Offline");

        public static readonly LocString Detail = new("contacts.detail", "Contact");

        public static readonly LocString Message = new("contacts.message", "Message");

        public static readonly LocString AdventurerPlate = new("contacts.adventurerPlate", "Adventurer Plate");

        public static readonly LocString SearchInfo = new("contacts.searchInfo", "Search Info");

        public static readonly LocString InviteToParty = new("contacts.inviteToParty", "Invite to Party");

        public static readonly LocString VisitEstate = new("contacts.visitEstate", "Visit Estate");
    }

    internal static class Chirper
    {
        public static readonly LocString SetUpAccount = new("chirper.setUpAccount", "Set up your account in Settings");

        public static readonly LocString Empty = new("chirper.empty", "No chirps yet — post the first one");

        public static readonly LocString FindPeople = new("chirper.findPeople", "Find People");

        public static readonly LocString SearchByName = new("chirper.searchByName", "Search by name or name@world");

        public static readonly LocString Following = new("chirper.following", "Following");

        public static readonly LocString Follow = new("chirper.follow", "Follow");

        public static readonly LocString NameOrWorld = new("chirper.nameOrWorld", "Name or Name@World");

        public static readonly LocString Compose = new("chirper.compose", "Chirp something");

        public static readonly LocPlural Likes = new("chirper.likes", "{0} like", "{0} likes");
    }

    internal static class Clock
    {
        public static readonly LocString Local = new("clock.local", "Local");

        public static readonly LocString InGame = new("clock.inGame", "In-game");

        public static readonly LocString Server = new("clock.server", "Server");
    }

    internal static class Notifications
    {
        public static readonly LocString Empty = new("notifications.empty", "No notifications");
    }

    internal static class Photos
    {
        public static readonly LocString NoPhotos = new("photos.noPhotos", "No Photos");

        public static readonly LocString UseCameraHint = new("photos.useCameraHint", "Use the Camera to take a shot");

        public static readonly LocPlural Count = new("photos.count", "{0} Photo", "{0} Photos");
    }

    internal static class Skywatcher
    {
        public static readonly LocString NextFewHours = new("skywatcher.nextFewHours", "Next Few Hours");

        public static readonly LocString Forecast = new("skywatcher.forecast", "Forecast");

        public static readonly LocString Now = new("skywatcher.now", "Now");

        public static readonly LocString NoData = new("skywatcher.noData", "No weather data here");

        public static readonly LocString Continuing = new("skywatcher.continuing", "{0} continuing");

        public static readonly LocString ForNextHours = new("skywatcher.forNextHours", "{0} for the next few hours");
    }

    internal static class Wallet
    {
        public static readonly LocString LogInToView = new("wallet.logInToView", "Log in to view your wallet");

        public static readonly LocString GilBalance = new("wallet.gilBalance", "GIL BALANCE");
    }

    internal static class Market
    {
        public static readonly LocString LoadingItemList = new("market.loadingItemList", "Loading item list…");

        public static readonly LocString NoMatchingItems = new("market.noMatchingItems", "No matching items");

        public static readonly LocString SearchHint = new("market.searchHint", "Search for an item, or right-click any item in-game.");

        public static readonly LocString HoveredInGame = new("market.hoveredInGame", "Hovered in-game");

        public static readonly LocString Favorites = new("market.favorites", "Favorites");

        public static readonly LocString Recent = new("market.recent", "Recent");

        public static readonly LocString LogInToViewPrices = new("market.logInToViewPrices", "Log in to view market prices");

        public static readonly LocString CouldntReach = new("market.couldntReach", "Couldn't reach Universalis");

        public static readonly LocString CheapestHq = new("market.cheapestHq", "Cheapest HQ");

        public static readonly LocString Cheapest = new("market.cheapest", "Cheapest");

        public static readonly LocString Prices = new("market.prices", "Prices");

        public static readonly LocString Average = new("market.average", "Average");

        public static readonly LocString Highest = new("market.highest", "Highest");

        public static readonly LocString SalesPerDay = new("market.salesPerDay", "Sales / day");

        public static readonly LocString UpSold = new("market.upSold", "Up / sold");

        public static readonly LocString Updated = new("market.updated", "Updated");

        public static readonly LocString VendorNpc = new("market.vendorNpc", "Vendor (NPC)");

        public static readonly LocString Cheaper = new("market.cheaper", "cheaper");

        public static readonly LocString PriceAlert = new("market.priceAlert", "Price alert");

        public static readonly LocString AddAnotherAlert = new("market.addAnotherAlert", "Add another alert");

        public static readonly LocString SetPriceAlert = new("market.setPriceAlert", "Set a price alert");

        public static readonly LocString CreateAlert = new("market.createAlert", "Create alert");

        public static readonly LocString AtOrBelow = new("market.atOrBelow", "At or below");

        public static readonly LocString AtOrAbove = new("market.atOrAbove", "At or above");

        public static readonly LocString Trend = new("market.trend", "Trend");

        public static readonly LocString Listings = new("market.listings", "Listings");

        public static readonly LocString ListingsCount = new("market.listingsCount", "Listings · {0}");

        public static readonly LocString NoHqListings = new("market.noHqListings", "No HQ listings");

        public static readonly LocString NoListings = new("market.noListings", "No listings");

        public static readonly LocString RecentSales = new("market.recentSales", "Recent sales");

        public static readonly LocString RecentSalesCount = new("market.recentSalesCount", "Recent sales · {0}");

        public static readonly LocString NoHqSales = new("market.noHqSales", "No HQ sales");

        public static readonly LocString NoRecentSales = new("market.noRecentSales", "No recent sales");

        public static readonly LocString SearchItems = new("market.searchItems", "Search items");

        public static readonly LocString Quantity = new("market.quantity", "Qty {0}");

        public static readonly LocString PerDay = new("market.perDay", "{0}/day");

        public static readonly LocString AlertBody = new("market.alertBody", "{0} {1} — now {2} on {3}");
    }

    internal static class Games
    {
        public static readonly LocString Sweeper = new("games.sweeper", "Sweeper");

        public static readonly LocString Pairs = new("games.pairs", "Pairs");

        public static readonly LocString GemSwap = new("games.gemSwap", "Gem Swap");

        public static readonly LocString Boom = new("games.boom", "Boom");

        public static readonly LocString Mines = new("games.mines", "Mines");

        public static readonly LocString Time = new("games.time", "Time");

        public static readonly LocString Attempts = new("games.attempts", "Attempts");

        public static readonly LocString Score = new("games.score", "Score");

        public static readonly LocString GameOver = new("games.gameOver", "Game Over");

        public static readonly LocString YouWin = new("games.youWin", "You Win!");

        public static readonly LocString PlayAgain = new("games.playAgain", "Play Again");

        public static readonly LocPlural AttemptsCount = new("games.attemptsCount", "{0} attempt", "{0} attempts");
    }

    internal static class Time
    {
        public static readonly LocString Now = new("time.now", "now");

        public static readonly LocString JustNow = new("time.justNow", "just now");

        public static readonly LocString MinutesShort = new("time.minutesShort", "{0}m");

        public static readonly LocString HoursShort = new("time.hoursShort", "{0}h");

        public static readonly LocString DaysShort = new("time.daysShort", "{0}d");

        public static readonly LocString MinutesAgo = new("time.minutesAgo", "{0}m ago");

        public static readonly LocString HoursAgo = new("time.hoursAgo", "{0}h ago");

        public static readonly LocString DaysAgo = new("time.daysAgo", "{0}d ago");

        public static readonly LocString InMinutes = new("time.inMinutes", "in {0}m");

        public static readonly LocString InHours = new("time.inHours", "in {0}h");

        public static readonly LocString InHoursMinutes = new("time.inHoursMinutes", "in {0}h {1}m");
    }

    internal static class Plugin
    {
        public static readonly LocString Dtr = new("plugin.dtr", "Phone");

        public static readonly LocString DtrBadge = new("plugin.dtrBadge", "Phone {0}");

        public static readonly LocString CommandHelp = new("plugin.commandHelp", "Toggle the Aetherphone. /phone market [item] opens the market board, /phone about opens credits & links, /phone test sends a sample notification.");

        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /phone.");

        public static readonly LocString SearchTheMarket = new("plugin.searchTheMarket", "Search the Market");
    }

    internal static class About
    {
        public static readonly LocString LinkDiscussions = new("about.linkDiscussions", "Discussions");

        public static readonly LocString LinkReportBug = new("about.linkReportBug", "Report a bug");

        public static readonly LocString LinkMorePlugins = new("about.linkMorePlugins", "More plugins");

        public static readonly LocString LinkSecurity = new("about.linkSecurity", "Security");

        public static readonly LocString Connect = new("about.connect", "Connect");

        public static readonly LocString MadeWithCare = new("about.madeWithCare", "Made with care");

        public static readonly LocString SupportBody = new("about.supportBody", "I build and maintain this in my spare time. If it has helped you, a sponsorship lets me keep improving it. No pressure, and thank you for being here.");

        public static readonly LocString BecomeSponsor = new("about.becomeSponsor", "Become a Sponsor");

        public static readonly LocString SponsorTooltip = new("about.sponsorTooltip", "Open GitHub Sponsors · right-click to copy");

        public static readonly LocString LinkTooltip = new("about.linkTooltip", "Click to open · right-click to copy");

        public static readonly LocString MadeBy = new("about.madeBy", "Made by {0}");

        public static readonly LocString ReminderHeader = new("about.reminderHeader", "A little reminder");

        public static readonly LocString FactHeader = new("about.factHeader", "Did you know?");

        public static readonly LocString QuoteHeader = new("about.quoteHeader", "Words to live by");

        public static readonly LocString FunHeader = new("about.funHeader", "Just for fun");

        public static readonly LocString[] Reminders =
        {
            new("about.reminder.0", "Been at it a while? Roll your shoulders and take one slow breath."),
            new("about.reminder.1", "Hydration check. When did you last drink some water?"),
            new("about.reminder.2", "Blink a few times and let your eyes rest for a moment."),
            new("about.reminder.3", "Stand up, stretch, and shake out your hands. Future you says thanks."),
            new("about.reminder.4", "Sit up and settle in comfortably. Your back will thank you later."),
            new("about.reminder.5", "Remember to eat something today. You matter more than any score."),
            new("about.reminder.6", "Eyes feel tired? Look at something far away for twenty seconds."),
            new("about.reminder.7", "Whatever you're chasing, you're allowed to take a break whenever."),
            new("about.reminder.8", "You're doing great. Be a little kinder to yourself today."),
            new("about.reminder.9", "A glass of water and a quick stretch can reset a long session."),
            new("about.reminder.10", "Unclench your jaw and drop your shoulders. There you go."),
            new("about.reminder.11", "Rest is part of the journey too. Step away whenever you need to."),
        };

        public static readonly LocString[] Facts =
        {
            new("about.fact.0", "Honey never spoils. Jars over 3,000 years old have been found still edible."),
            new("about.fact.1", "Octopuses have three hearts and blue blood."),
            new("about.fact.2", "A day on Venus is longer than a whole year on Venus."),
            new("about.fact.3", "Bananas are berries, but strawberries aren't."),
            new("about.fact.4", "There are more possible chess games than atoms in the observable universe."),
            new("about.fact.5", "Sharks have been around longer than trees have."),
            new("about.fact.6", "A group of flamingos is called a flamboyance."),
            new("about.fact.7", "Honeybees can recognize individual human faces."),
            new("about.fact.8", "Wombat droppings are cube shaped."),
            new("about.fact.9", "The Eiffel Tower can grow over 15 cm taller on a hot day."),
            new("about.fact.10", "Hot water can sometimes freeze faster than cold water."),
            new("about.fact.11", "A bolt of lightning is roughly five times hotter than the surface of the Sun."),
        };

        public static readonly LocString[] Quotes =
        {
            new("about.quote.0", "Done is better than perfect. You can always polish later."),
            new("about.quote.1", "Small steps every day add up to surprising distances."),
            new("about.quote.2", "Comparison is the thief of joy. Run your own race."),
            new("about.quote.3", "Progress, not perfection."),
            new("about.quote.4", "You don't have to be great to start, but you have to start to be great."),
            new("about.quote.5", "Be patient with yourself. Growth takes time."),
            new("about.quote.6", "The best time to begin was yesterday. The second best is right now."),
            new("about.quote.7", "Celebrate the small wins. They count too."),
            new("about.quote.8", "Slow progress is still progress."),
            new("about.quote.9", "Your only real competition is who you were yesterday."),
        };

        public static readonly LocString[] Fun =
        {
            new("about.fun.0", "Why don't scientists trust atoms? Because they make up everything."),
            new("about.fun.1", "I would tell you a chemistry joke, but I know I wouldn't get a reaction."),
            new("about.fun.2", "Why did the scarecrow win an award? He was outstanding in his field."),
            new("about.fun.3", "I'm reading a book about anti-gravity. It's impossible to put down."),
            new("about.fun.4", "Why don't skeletons fight each other? They don't have the guts."),
            new("about.fun.5", "What do you call fake spaghetti? An impasta."),
            new("about.fun.6", "Why did the bicycle fall over? It was two tired."),
            new("about.fun.7", "What do you call cheese that isn't yours? Nacho cheese."),
            new("about.fun.8", "I'm on a seafood diet. I see food, and I eat it."),
            new("about.fun.9", "I only know 25 letters of the alphabet. I don't know y."),
        };
    }

    internal static class Catalogs
    {
        public static readonly LocString AccentViolet = new("catalog.accent.violet", "Violet");

        public static readonly LocString AccentBlue = new("catalog.accent.blue", "Blue");

        public static readonly LocString AccentGreen = new("catalog.accent.green", "Green");

        public static readonly LocString AccentPink = new("catalog.accent.pink", "Pink");

        public static readonly LocString AccentAmber = new("catalog.accent.amber", "Amber");

        public static readonly LocString RingtonePing = new("catalog.ringtone.ping", "Ping");

        public static readonly LocString RingtoneChime = new("catalog.ringtone.chime", "Chime");

        public static readonly LocString RingtoneBell = new("catalog.ringtone.bell", "Bell");

        public static readonly LocString RingtoneAlert = new("catalog.ringtone.alert", "Alert");

        public static readonly LocString RingtoneKnock = new("catalog.ringtone.knock", "Knock");

        public static readonly LocString RingtoneSilent = new("catalog.ringtone.silent", "Silent");

        public static readonly LocString RadioLofi = new("catalog.radio.lofi", "Lofi");

        public static readonly LocString RadioChillout = new("catalog.radio.chillout", "Chillout");

        public static readonly LocString RadioJazz = new("catalog.radio.jazz", "Jazz");

        public static readonly LocString RadioClassical = new("catalog.radio.classical", "Classical");

        public static readonly LocString RadioAmbient = new("catalog.radio.ambient", "Ambient");

        public static readonly LocString RadioElectronic = new("catalog.radio.electronic", "Electronic");

        public static readonly LocString RadioPop = new("catalog.radio.pop", "Pop");

        public static readonly LocString RadioRock = new("catalog.radio.rock", "Rock");

        public static readonly LocString RadioMetal = new("catalog.radio.metal", "Metal");

        public static readonly LocString RadioHipHop = new("catalog.radio.hipHop", "Hip-Hop");

        public static readonly LocString RadioSoundtrack = new("catalog.radio.soundtrack", "Soundtrack");

        public static readonly LocString RadioAnime = new("catalog.radio.anime", "Anime");
    }
}
