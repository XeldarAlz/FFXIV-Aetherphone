namespace Aetherphone.Core.Localization;

internal static class L
{
    internal static class Common
    {
        public static readonly LocString Loading = new("common.loading", "Loading…");
        public static readonly LocString Searching = new("common.searching", "Searching…");
        public static readonly LocString Search = new("common.search", "Search");
        public static readonly LocString Cancel = new("common.cancel", "Cancel");
        public static readonly LocString Close = new("common.close", "Close");
        public static readonly LocString Alerts = new("common.alerts", "Alerts");
        public static readonly LocString Live = new("common.live", "LIVE");
        public static readonly LocString Hq = new("common.hq", "HQ");
        public static readonly LocString Nq = new("common.nq", "NQ");
        public static readonly LocString ComingSoon = new("common.comingSoon", "Coming soon");
    }

    internal static class Social
    {
        public static readonly LocString LikedChirp = new("social.likedChirp", "liked your chirp");
        public static readonly LocString LikedPhoto = new("social.likedPhoto", "liked your photo");
        public static readonly LocString CommentedChirp = new("social.commentedChirp", "commented on your chirp");
        public static readonly LocString CommentedPhoto = new("social.commentedPhoto", "commented on your photo");
        public static readonly LocString Followed = new("social.followed", "started following you");
        public static readonly LocString ConnectionRequest = new("social.connectionRequest", "wants to connect with you");
        public static readonly LocString ConnectionAccepted = new("social.connectionAccepted", "accepted your connection request");
        public static readonly LocString FollowersTitle = new("social.followersTitle", "Followers");
        public static readonly LocString FollowingTitle = new("social.followingTitle", "Following");
        public static readonly LocString LikedByTitle = new("social.likedByTitle", "Liked by");
        public static readonly LocString ListEmpty = new("social.listEmpty", "No one here yet");
        public static readonly LocString ActivityTitle = new("social.activityTitle", "Notifications");
        public static readonly LocString ActivityTab = new("social.activityTab", "Activity");
        public static readonly LocString ActivityEmpty = new("social.activityEmpty", "Nothing here yet. Interactions with your posts will show up here");
    }

    internal static class Moderation
    {
        public static readonly LocString InReview = new("moderation.inReview", "In review");
        public static readonly LocString InReviewHint = new("moderation.inReviewHint", "Only you can see this until the review finishes");
        public static readonly LocString RemovedTitle = new("moderation.removedTitle", "Post removed");
        public static readonly LocString RemovedAdult = new("moderation.removedAdult", "Your post was removed because it appears to contain adult content, which is not allowed here.");
        public static readonly LocString RemovedViolence = new("moderation.removedViolence", "Your post was removed because it appears to contain violent or graphic content.");
        public static readonly LocString RemovedHarassment = new("moderation.removedHarassment", "Your post was removed because it appears to contain abusive or harassing language.");
        public static readonly LocString RemovedHate = new("moderation.removedHate", "Your post was removed because it appears to contain hateful content.");
        public static readonly LocString RemovedSelfHarm = new("moderation.removedSelfHarm", "Your post was removed because it appears to reference self-harm.");
        public static readonly LocString RemovedPolicy = new("moderation.removedPolicy", "Your post was removed for violating the community guidelines.");
        public static readonly LocString RemovedFooter = new("moderation.removedFooter", "If you believe this is a mistake, let us know through the Feedback app.");
        public static readonly LocString RemovedDismiss = new("moderation.removedDismiss", "OK");
    }

    internal static class Apps
    {
        public static readonly LocString Messages = new("app.messages", "Messages");
        public static readonly LocString Contacts = new("app.contacts", "Contacts");
        public static readonly LocString Character = new("app.character", "Character");
        public static readonly LocString Chirper = new("app.chirper", "Chirper");
        public static readonly LocString Aethergram = new("app.aethergram", "Aethergram");
        public static readonly LocString Velvet = new("app.velvet", "Velvet");
        public static readonly LocString Camera = new("app.camera", "Camera");
        public static readonly LocString Photos = new("app.photos", "Photos");
        public static readonly LocString Skywatcher = new("app.skywatcher", "Skywatcher");
        public static readonly LocString Venues = new("app.venues", "Venues");
        public static readonly LocString Market = new("app.market", "Market");
        public static readonly LocString Wallet = new("app.wallet", "Wallet");
        public static readonly LocString Music = new("app.music", "Music");
        public static readonly LocString Clock = new("app.clock", "Clock");
        public static readonly LocString Timers = new("app.timers", "Timers");
        public static readonly LocString Dailies = new("app.dailies", "Dailies");
        public static readonly LocString Games = new("app.games", "Games");
        public static readonly LocString Notifications = new("app.notifications", "Notifications");
        public static readonly LocString News = new("app.news", "News");
        public static readonly LocString Fishing = new("app.fishing", "Fishing");
        public static readonly LocString Maps = new("app.maps", "Maps");
        public static readonly LocString Collections = new("app.collections", "Collections");
        public static readonly LocString Inventory = new("app.inventory", "Inventory");
        public static readonly LocString Settings = new("app.settings", "Settings");
        public static readonly LocString FindPeople = new("app.findpeople", "Find People");
        public static readonly LocString Feedback = new("app.feedback", "Feedback");
        public static readonly LocString Polls = new("app.polls", "Polls");
        public static readonly LocString Calendar = new("app.calendar", "Calendar");
        public static readonly LocString Notes = new("app.notes", "Notes");
        public static readonly LocString Calculator = new("app.calculator", "Calculator");
        public static readonly LocString Friends = new("app.friends", "Friends");
        public static readonly LocString Chat = new("app.chat", "Chat");
    }

    internal static class DirectMessages
    {
        public static readonly LocString Empty = new("dm.empty", "No conversations yet");
        public static readonly LocString EmptyHint = new("dm.emptyHint", "Message a friend from your friend list");
        public static readonly LocString SignInPrompt = new("dm.signInPrompt", "Sign in to message your friends");
        public static readonly LocString NewMessage = new("dm.newMessage", "New message");
        public static readonly LocString NewGroup = new("dm.newGroup", "New group");
        public static readonly LocString GroupFallback = new("dm.groupFallback", "Group");
        public static readonly LocString PhotoPreview = new("dm.photoPreview", "Photo");
        public static readonly LocString NoMutualTitle = new("dm.noMutualTitle", "No mutual friends yet");
        public static readonly LocString NoMutualFriends = new("dm.noMutualFriends", "Share numbers in-game to start messaging.");
        public static readonly LocString To = new("dm.to", "To");
        public static readonly LocString GroupNameHint = new("dm.groupNameHint", "Group name (optional)");
        public static readonly LocString CreateGroup = new("dm.createGroup", "Create group");
        public static readonly LocString StartChat = new("dm.startChat", "Message");
        public static readonly LocString Details = new("dm.details", "Details");
        public static readonly LocString Members = new("dm.members", "Members");
        public static readonly LocString MembersCount = new("dm.membersCount", "{0} members");
        public static readonly LocString AddPeople = new("dm.addPeople", "Add people");
        public static readonly LocString Add = new("dm.add", "Add");
        public static readonly LocString LeaveChat = new("dm.leaveChat", "Leave chat");
        public static readonly LocString ConfirmLeave = new("dm.confirmLeave", "Leave this chat?");
        public static readonly LocString Leaving = new("dm.leaving", "Leaving…");
        public static readonly LocString LeaveFailed = new("dm.leaveFailed", "Could not leave the chat");
        public static readonly LocString Rename = new("dm.rename", "Rename");
        public static readonly LocString RenameHint = new("dm.renameHint", "Group name");
        public static readonly LocString Save = new("dm.save", "Save");
        public static readonly LocString Owner = new("dm.owner", "Owner");
        public static readonly LocString SysCreated = new("dm.sysCreated", "{0} started the group");
        public static readonly LocString SysAdded = new("dm.sysAdded", "{0} added {1}");
        public static readonly LocString SysRemoved = new("dm.sysRemoved", "{0} removed {1}");
        public static readonly LocString SysLeft = new("dm.sysLeft", "{0} left");
        public static readonly LocString SysRenamed = new("dm.sysRenamed", "{0} renamed the chat to {1}");
    }

    internal static class FindPeople
    {
        public static readonly LocString Character = new("findPeople.character", "Character");
        public static readonly LocString FreeCompany = new("findPeople.freeCompany", "Free Company");
        public static readonly LocString NameHint = new("findPeople.nameHint", "Name");
        public static readonly LocString WorldHint = new("findPeople.worldHint", "World or DC (optional)");
        public static readonly LocString Prompt = new("findPeople.prompt", "Search the Lodestone");
        public static readonly LocString PromptHint = new("findPeople.promptHint", "Find characters and free companies");
        public static readonly LocString NoResults = new("findPeople.noResults", "No matches found");
        public static readonly LocString Failed = new("findPeople.failed", "Couldn't reach the Lodestone");
        public static readonly LocString TryAgain = new("findPeople.tryAgain", "Try Again");
        public static readonly LocString Members = new("findPeople.members", "{0} members");
        public static readonly LocString Recruiting = new("findPeople.recruiting", "Recruiting");
        public static readonly LocString Closed = new("findPeople.closed", "Not recruiting");
        public static readonly LocString CharacterTitle = new("findPeople.characterTitle", "Character");
        public static readonly LocString FreeCompanyTitle = new("findPeople.freeCompanyTitle", "Free Company");
        public static readonly LocString Combat = new("findPeople.combat", "Combat");
        public static readonly LocString Crafter = new("findPeople.crafter", "Crafters");
        public static readonly LocString Gatherer = new("findPeople.gatherer", "Gatherers");
        public static readonly LocString Gear = new("findPeople.gear", "Gear");
        public static readonly LocString GrandCompany = new("findPeople.grandCompany", "Grand Company");
        public static readonly LocString Slogan = new("findPeople.slogan", "Slogan");
        public static readonly LocString Roster = new("findPeople.roster", "Roster");
        public static readonly LocString Rank = new("findPeople.rank", "Rank");
        public static readonly LocString Message = new("findPeople.message", "Message");
        public static readonly LocString PageOf = new("findPeople.pageOf", "Page {0} of {1}");
        public static readonly LocString Active = new("findPeople.active", "{0} active");
    }

    internal static class Collections
    {
        public static readonly LocString Mounts = new("collections.mounts", "Mounts");
        public static readonly LocString Minions = new("collections.minions", "Minions");
        public static readonly LocString Emotes = new("collections.emotes", "Emotes");
        public static readonly LocString Orchestrions = new("collections.orchestrions", "Orchestrions");
        public static readonly LocString Hairstyles = new("collections.hairstyles", "Hairstyles");
        public static readonly LocString Facewear = new("collections.facewear", "Facewear");
        public static readonly LocString Achievements = new("collections.achievements", "Achievements");
        public static readonly LocString TriadCards = new("collections.triadCards", "Triple Triad");
        public static readonly LocString Search = new("collections.search", "Search");
        public static readonly LocString FilterAll = new("collections.filterAll", "All");
        public static readonly LocString FilterOwned = new("collections.filterOwned", "Owned");
        public static readonly LocString FilterMissing = new("collections.filterMissing", "Missing");
        public static readonly LocString AllSources = new("collections.allSources", "All sources");
        public static readonly LocString Sources = new("collections.sources", "Sources");
        public static readonly LocString Source = new("collections.source", "Source");
        public static readonly LocString HowToObtain = new("collections.howToObtain", "How to obtain");
        public static readonly LocString Patch = new("collections.patch", "Patch");
        public static readonly LocString Tradeable = new("collections.tradeable", "Tradeable");
        public static readonly LocString Yes = new("collections.yes", "Yes");
        public static readonly LocString No = new("collections.no", "No");
        public static readonly LocString Community = new("collections.community", "Owned by players");
        public static readonly LocString Points = new("collections.points", "Points");
        public static readonly LocString CardStats = new("collections.cardStats", "Card stats");
        public static readonly LocString Owned = new("collections.owned", "Owned");
        public static readonly LocString Missing = new("collections.missing", "Missing");
        public static readonly LocString Details = new("collections.details", "Details");
        public static readonly LocString About = new("collections.about", "About");

        public static readonly LocString LinkHint = new("collections.linkHint", "Link your character to see what you own.");
        public static readonly LocString CollectionPrivate = new("collections.collectionPrivate", "This collection is private on the Lodestone.");
        public static readonly LocString OwnedUnavailable = new("collections.ownedUnavailable", "Couldn't load your owned items right now.");
        public static readonly LocString Failed = new("collections.failed", "Couldn't reach FFXIV Collect.");
        public static readonly LocString TryAgain = new("collections.tryAgain", "Try again");
        public static readonly LocString NoResults = new("collections.noResults", "No items match your filters.");
        public static readonly LocString CompletePercent = new("collections.completePercent", "{0}% complete");
    }

    internal static class Venues
    {
        public static readonly LocString LiveNow = new("venues.liveNow", "Live");
        public static readonly LocString Today = new("venues.today", "Today");
        public static readonly LocString Upcoming = new("venues.upcoming", "Upcoming");
        public static readonly LocString All = new("venues.all", "All");
        public static readonly LocString Search = new("venues.search", "Search venues");
        public static readonly LocString AllDataCenters = new("venues.allDataCenters", "All DCs");
        public static readonly LocString AllSources = new("venues.allSources", "All");
        public static readonly LocString SourceFfxiv = new("venues.sourceFfxiv", "FFXIV Venues");
        public static readonly LocString SourcePartake = new("venues.sourcePartake", "Partake.gg");
        public static readonly LocString Tags = new("venues.tags", "Tags");
        public static readonly LocString ClearTags = new("venues.clearTags", "Clear tags");
        public static readonly LocString Favorites = new("venues.favorites", "Favorites");
        public static readonly LocString NoVenues = new("venues.noVenues", "No venues found");
        public static readonly LocString Failed = new("venues.failed", "Couldn't reach venue listings");
        public static readonly LocString Teleport = new("venues.teleport", "Teleport");
        public static readonly LocString Open = new("venues.open", "Open");
        public static readonly LocString Discord = new("venues.discord", "Discord");
        public static readonly LocString NeedsLifestream = new("venues.needsLifestream", "Lifestream is not installed");
        public static readonly LocString Details = new("venues.details", "Details");
        public static readonly LocString When = new("venues.when", "When");
        public static readonly LocString DataCenter = new("venues.dataCenter", "Data Center");
        public static readonly LocString World = new("venues.world", "World");
        public static readonly LocString Location = new("venues.location", "Location");
        public static readonly LocString Host = new("venues.host", "Host");
        public static readonly LocString Attendees = new("venues.attendees", "Attendees");
        public static readonly LocString About = new("venues.about", "About");
        public static readonly LocString EventsCount = new("venues.eventsCount", "{0} events");
        public static readonly LocString MoreCount = new("venues.moreCount", "+{0} more");
        public static readonly LocString UntilTime = new("venues.untilTime", "until {0}");
        public static readonly LocString HostedBy = new("venues.hostedBy", "Hosted by {0}");
        public static readonly LocString EmptyHint = new("venues.emptyHint", "Try a different filter or search");
        public static readonly LocString Retry = new("venues.retry", "Retry");
    }

    internal static class Maps
    {
        public static readonly LocString Search = new("maps.search", "Search places");
        public static readonly LocString Favorites = new("maps.favorites", "Favorites");
        public static readonly LocString CurrentLocation = new("maps.currentLocation", "Current Location");
        public static readonly LocString Unknown = new("maps.unknown", "Unknown");
        public static readonly LocString NoZones = new("maps.noZones", "No places found");
        public static readonly LocString NeedsLifestream = new("maps.needsLifestream", "Lifestream is not installed");
    }

    internal static class Phone
    {
        public static readonly LocString Title = new("phone.title", "Phone");
        public static readonly LocString AddToCall = new("phone.addToCall", "Add to Call");
        public static readonly LocString SignInPrompt = new("phone.signInPrompt", "Sign in to Aethernet in Settings to make calls");
        public static readonly LocString NoOneFound = new("phone.noOneFound", "No one found");
        public static readonly LocString SearchPrompt = new("phone.searchPrompt", "Search for someone to call");
        public static readonly LocString Recents = new("phone.recents", "Recents");
        public static readonly LocString Connecting = new("phone.connecting", "Connecting to call service…");
        public static readonly LocString UseHeadphones = new("phone.useHeadphones", "Use headphones to avoid echo");
        public static readonly LocString EnableTitle = new("phone.enableTitle", "Phone Calls");
        public static readonly LocString EnableBody = new("phone.enableBody", "Voice calls with other Aetherphone users");
        public static readonly LocString Enable = new("phone.enable", "Enable");
        public static readonly LocString SearchHint = new("phone.searchHint", "Name or Name@World");
        public static readonly LocString StatusCalling = new("phone.statusCalling", "Calling…");
        public static readonly LocString StatusConnecting = new("phone.statusConnecting", "Connecting…");
        public static readonly LocString SettingsTitle = new("phone.settingsTitle", "Phone Calls");
        public static readonly LocString SummaryOn = new("phone.summaryOn", "On");
        public static readonly LocString SummaryOff = new("phone.summaryOff", "Off");
        public static readonly LocString Calls = new("phone.calls", "Calls");
        public static readonly LocString EnablePhoneCalls = new("phone.enablePhoneCalls", "Enable Phone Calls");
        public static readonly LocString Microphone = new("phone.microphone", "Microphone");
        public static readonly LocString SystemDefault = new("phone.systemDefault", "System default");
        public static readonly LocString DeviceFallback = new("phone.deviceFallback", "Microphone {0}");
        public static readonly LocString AudioHint = new("phone.audioHint", "Audio plays on your system default output device. Use headphones to avoid echo. A device change applies to your next call.");
        public static readonly LocString IncomingCallBody = new("phone.incomingCallBody", "Incoming call");
        public static readonly LocString Decline = new("phone.decline", "Decline");
        public static readonly LocString Accept = new("phone.accept", "Accept");
        public static readonly LocString AudioCall = new("phone.audioCall", "Aetherphone audio call");
        public static readonly LocString PlusOthers = new("phone.plusOthers", "+{0} others");
        public static readonly LocString NoAnswerTitle = new("phone.noAnswerTitle", "No answer");
        public static readonly LocString NoAnswerBody = new("phone.noAnswerBody", "The call was not answered");
        public static readonly LocString CallEnded = new("phone.callEnded", "Call ended");
        public static readonly LocString ContactsSection = new("phone.contactsSection", "Contacts");
        public static readonly LocString PendingSection = new("phone.pendingSection", "Pending");
        public static readonly LocString NoContactsTitle = new("phone.noContactsTitle", "No one to call yet");
        public static readonly LocString NoContacts = new("phone.noContacts", "Add friends by number in the Friends app to call them");
        public static readonly LocString SignInTitle = new("phone.signInTitle", "Sign in to call");
        public static readonly LocString FilterHint = new("phone.filterHint", "Search contacts");
        public static readonly LocString NotMutual = new("phone.notMutual", "Waiting for them to add you back");
    }

    internal static class Friends
    {
        public static readonly LocString Title = new("friends.title", "Friends");
        public static readonly LocString MyNumber = new("friends.myNumber", "My Number");
        public static readonly LocString ShareHint = new("friends.shareHint", "Share it in-game so friends can add you");
        public static readonly LocString Copied = new("friends.copied", "Copied");
        public static readonly LocString AddFriend = new("friends.addFriend", "Add Friend");
        public static readonly LocString NumberHint = new("friends.numberHint", "Number, e.g. 234-5678");
        public static readonly LocString NameHint = new("friends.nameHint", "Name (optional)");
        public static readonly LocString Add = new("friends.add", "Add");
        public static readonly LocString Adding = new("friends.adding", "Adding…");
        public static readonly LocString InvalidNumber = new("friends.invalidNumber", "That does not look like a phone number");
        public static readonly LocString NotFound = new("friends.notFound", "No one answers at that number");
        public static readonly LocString RateLimited = new("friends.rateLimited", "Too many attempts. Try again in a minute");
        public static readonly LocString AddFailed = new("friends.addFailed", "Could not add the number right now");
        public static readonly LocString Empty = new("friends.empty", "No friends yet");
        public static readonly LocString EmptyHint = new("friends.emptyHint", "Ask for a number in-game and add it here");
        public static readonly LocString CanCall = new("friends.canCall", "You can call each other");
        public static readonly LocString Pending = new("friends.pending", "Waiting for them to add your number");
        public static readonly LocString PendingShort = new("friends.pendingShort", "Pending");
        public static readonly LocString Call = new("friends.call", "Call");
        public static readonly LocString Remove = new("friends.remove", "Remove");
        public static readonly LocString ConfirmRemove = new("friends.confirmRemove", "Remove {0} from your contacts?");
        public static readonly LocString RemoveFailed = new("friends.removeFailed", "Could not remove the contact");
        public static readonly LocString SafetyTitle = new("friends.safetyTitle", "Safety");
        public static readonly LocString NewNumberTitle = new("friends.newNumberTitle", "Request a New Number");
        public static readonly LocString NewNumberBody = new("friends.newNumberBody", "If someone you do not trust has your number, you can ask for a new one. Everyone who saved your old number will lose it.");
        public static readonly LocString ReasonHint = new("friends.reasonHint", "Tell us briefly why you need a new number");
        public static readonly LocString SendRequest = new("friends.sendRequest", "Send Request");
        public static readonly LocString Sending = new("friends.sending", "Sending…");
        public static readonly LocString RequestFailed = new("friends.requestFailed", "Could not send the request right now");
        public static readonly LocString RequestPending = new("friends.requestPending", "Your request is waiting for review");
        public static readonly LocString RequestApproved = new("friends.requestApproved", "Your number was changed. Share the new one with people you trust");
        public static readonly LocString RequestDenied = new("friends.requestDenied", "Your last request was declined");
        public static readonly LocString SignInPrompt = new("friends.signInPrompt", "Sign in to Aethernet in Settings to use Friends");
    }

    internal static class Settings
    {
        public static readonly LocString Title = new("settings.title", "Settings");
        public static readonly LocString GeneralFooter = new("settings.generalFooter", "Personalize how your phone looks, reads and behaves.");
        public static readonly LocString AlertsFooter = new("settings.alertsFooter", "Choose how calls and notifications reach you.");
        public static readonly LocString Appearance = new("settings.appearance", "Appearance");
        public static readonly LocString Theme = new("settings.theme", "Theme");
        public static readonly LocString ThemeLight = new("settings.themeLight", "Light");
        public static readonly LocString ThemeDark = new("settings.themeDark", "Dark");
        public static readonly LocString ThemeAuto = new("settings.themeAuto", "Auto");
        public static readonly LocString Accent = new("settings.accent", "Accent");
        public static readonly LocString Wallpaper = new("settings.wallpaper", "Wallpaper");
        public static readonly LocString TextSize = new("settings.textSize", "Text Size");
        public static readonly LocString PhoneSize = new("settings.phoneSize", "Phone Size");
        public static readonly LocString Notifications = new("settings.notifications", "Notifications");
        public static readonly LocString DoNotDisturb = new("settings.doNotDisturb", "Do Not Disturb");
        public static readonly LocString Vibration = new("settings.vibration", "Vibration");
        public static readonly LocString VibrationHint = new("settings.vibrationHint", "The phone shakes briefly when a notification arrives.");
        public static readonly LocString NotificationApps = new("settings.notificationApps", "Apps");
        public static readonly LocString AllowNotifications = new("settings.allowNotifications", "Allow Notifications");
        public static readonly LocString NotificationsOff = new("settings.notificationsOff", "Off");
        public static readonly LocString SoundDefault = new("settings.soundDefault", "Default");
        public static readonly LocString Immersion = new("settings.immersion", "Immersion");
        public static readonly LocString ScrollWhileIdle = new("settings.scrollWhileIdle", "Scroll while idle");
        public static readonly LocString ScrollWhileIdleHint = new("settings.scrollWhileIdleHint", "Your character scrolls through their phone (Tomescroll emote) while standing still and out of combat. Does nothing if you haven't unlocked the emote.");
        public static readonly LocString OpenOnStartup = new("settings.openOnStartup", "Open at startup");
        public static readonly LocString OpenMinimized = new("settings.openMinimized", "Open minimized");
        public static readonly LocString StartupHint = new("settings.startupHint", "Open the phone automatically when you log in. Open minimized shows it as a small dock that you tap to expand.");
        public static readonly LocString Ringtone = new("settings.ringtone", "Ringtone");
        public static readonly LocString Sound = new("settings.sound", "Sound");
        public static readonly LocString NotificationSound = new("settings.notificationSound", "Notification Sound");
        public static readonly LocString Volume = new("settings.volume", "Volume");
        public static readonly LocString ImportSound = new("settings.importSound", "Import from PC");
        public static readonly LocString SoundImportHint = new("settings.soundImportHint", "Imported files appear in the list below and play at the volume set here, separate from the game's own sound settings.");
        public static readonly LocString Language = new("settings.language", "Language");
        public static readonly LocString About = new("settings.about", "About");
        public static readonly LocString Information = new("settings.information", "Information");
        public static readonly LocString Plugin = new("settings.plugin", "Plugin");
        public static readonly LocString Version = new("settings.version", "Version");
        public static readonly LocString Command = new("settings.command", "Command");
        public static readonly LocString CreditsLinks = new("settings.creditsLinks", "Credits & links");
        public static readonly LocString AboutAetherphone = new("settings.aboutAetherphone", "About Aetherphone");
        public static readonly LocString Changelog = new("settings.changelog", "Changelog");
        public static readonly LocString ChangelogSummary = new("settings.changelogSummary", "What's new");
        public static readonly LocString ChangelogHero = new("settings.changelogHero", "What's New");
        public static readonly LocString ChangelogLatest = new("settings.changelogLatest", "Latest");
        public static readonly LocString Tutorials = new("settings.tutorials", "Tips & Tutorials");
        public static readonly LocString TutorialsSummary = new("settings.tutorialsSummary", "On");
        public static readonly LocString TutorialsOff = new("settings.tutorialsOff", "Off");
        public static readonly LocString TutorialsShow = new("settings.tutorialsShow", "Show tutorials");
        public static readonly LocString TutorialsReplay = new("settings.tutorialsReplay", "Replay welcome");
        public static readonly LocString TutorialsReset = new("settings.tutorialsReset", "Reset all tutorials");
        public static readonly LocString TutorialsHint = new("settings.tutorialsHint", "Tips appear once the first time you open each app. Reset to see them all again.");
        public static readonly LocString Privacy = new("settings.privacy", "Privacy");
        public static readonly LocString PrivacyOn = new("settings.privacyOn", "Sharing");
        public static readonly LocString PrivacyOff = new("settings.privacyOff", "Private");
        public static readonly LocString PrivacyAnalytics = new("settings.privacyAnalytics", "Share anonymous usage");
        public static readonly LocString PrivacyHint = new("settings.privacyHint", "Aetherphone is made by one solo developer. Sharing anonymous usage, which apps you open and for how long plus your region, helps me see what to build next. It never includes your character name, your messages, or any personal data.");
        public static readonly LocString ConsentTitle = new("settings.consentTitle", "Analytics & Consent");
        public static readonly LocString ConsentMessage = new("settings.consentMessage", "Aetherphone is made by one solo developer. To keep making it better for everyone, it really helps to know which apps people use and where to focus next.\n\nWith your OK, the app shares anonymous usage only: which apps you open and for how long, plus your region. It never includes your character name, your messages, or any personal data.\n\nYou can change this anytime in Settings, under Privacy. Thank you for helping shape Aetherphone.");
        public static readonly LocString ConsentAccept = new("settings.consentAccept", "Sure, count me in");
        public static readonly LocString ConsentDecline = new("settings.consentDecline", "No thanks");
        public static readonly LocString Commands = new("settings.commands", "Commands");
        public static readonly LocString CommandsSummary = new("settings.commandsSummary", "Slash commands");
        public static readonly LocString CommandsHint = new("settings.commandsHint", "Type these into the chat box. Reset brings the phone back to the middle of your screen if you ever move it out of view.");
        public static readonly LocString CommandToggle = new("settings.commandToggle", "Show or hide the phone");
        public static readonly LocString CommandAlias = new("settings.commandAlias", "Alias for /phone");
        public static readonly LocString CommandMarket = new("settings.commandMarket", "Open the market board, optionally searching an item");
        public static readonly LocString CommandAbout = new("settings.commandAbout", "Open credits and links");
        public static readonly LocString CommandReset = new("settings.commandReset", "Move the phone back to the center of the screen");
        public static readonly LocString CommandTest = new("settings.commandTest", "Send a sample notification");
    }

    internal static class Changelog
    {
        public static readonly LocString[] Release0900 =
        {
            new("changelog.r0900.0", "Added Friends, a new app to add people by their phone number and share your own number in-game"),
            new("changelog.r0900.1", "Added Messages, a new app for private one-to-one and group chats with your friends"),
            new("changelog.r0900.2", "Calling is now limited to friends who have added each other, so only people you both trust can reach you"),
            new("changelog.r0900.3", "You can now request a new phone number in Friends if someone you would rather not hear from has your old one"),
            new("changelog.r0900.4", "Rebuilt the Home screen around a flexible grid of app icons, folders, and resizable widgets"),
            new("changelog.r0900.5", "Added a dock at the bottom of the Home screen for up to four favorite apps"),
            new("changelog.r0900.6", "Added Home screen widgets, including a Skywatcher forecast, Clock, Calendar, a Photos shuffle, and Resets"),
            new("changelog.r0900.7", "Added a gallery to browse widgets and preview their sizes before placing them"),
            new("changelog.r0900.8", "Added a Home edit mode to rearrange icons, resize widgets, disband folders, and drag items to new pages"),
            new("changelog.r0900.9", "Apps now open and close by growing from and shrinking back to their icon"),
            new("changelog.r0900.10", "Swipe up from the home bar to return to the Home screen from any app"),
            new("changelog.r0900.11", "Added a Home grid density option for five, six, or seven rows, plus a layout reset, in Settings"),
            new("changelog.r0900.12", "Skywatcher now shows live animated weather in the app and on its widget"),
            new("changelog.r0900.13", "Redesigned the Venues cards and added a detail page for each venue"),
            new("changelog.r0900.14", "Added an Activity tab to Chirper, Aethergram, and Velvet that gathers your likes, comments, and follows"),
            new("changelog.r0900.15", "Added a quick menu to posts for actions like reporting and deleting"),
            new("changelog.r0900.16", "Velvet now lets you pin conversations to the top of your chats"),
            new("changelog.r0900.17", "Renamed the old Messages app to Chat, since it covers your linkshell and in-game chat"),
            new("changelog.r0900.18", "Added a mute bell to each linkshell in Chat so you can silence a busy channel straight from the list"),
            new("changelog.r0900.19", "Combined calls and now-playing music into one Dynamic Island that splits in two, just like on a real phone"),
            new("changelog.r0900.20", "Notification banners now spring in, pause while you hover them, and can be flicked upward to dismiss"),
            new("changelog.r0900.21", "Control Center is now customizable: rearrange, resize, add, and remove its controls like the Home screen"),
            new("changelog.r0900.22", "The minimized phone now morphs smoothly to and from full size, with expand and close buttons and an unread badge"),
            new("changelog.r0900.23", "Added photo zoom with panning and double-tap across Photos, Aethergram, Velvet, and Chat"),
            new("changelog.r0900.24", "Redesigned the phone's side buttons and frame with a machined graphite metal finish"),
            new("changelog.r0900.25", "Reworked Settings with real icons, an account banner up top, and animated toggles"),
            new("changelog.r0900.26", "Redesigned the welcome tour with an animated illustration, and tours now glide smoothly between steps"),
            new("changelog.r0900.27", "Action buttons across the app now show matching animated tooltips when you hover them"),
            new("changelog.r0900.28", "Smoothed out app open, close, and minimize animations so they feel physical and can be interrupted"),
            new("changelog.r0900.29", "Tidied up the codebase and shared design system for better performance and a more consistent look"),
            new("changelog.r0900.30", "Fixed the phone's corners so every screen and app uses one consistent rounded shape"),
            new("changelog.r0900.31", "Fixed tooltips that could appear behind Control Center tiles"),
            new("changelog.r0900.32", "Fixed 2048 so swiping slides the tiles instead of leaving duplicates behind"),
        };

        public static readonly LocString[] Release0870 =
        {
            new("changelog.r0870.0", "Added Polls, a new app for voting in single-choice polls"),
            new("changelog.r0870.1", "Notifications now take you straight to the post or profile they're about in Chirper, Aethergram, and Velvet"),
            new("changelog.r0870.2", "Added tappable follower and following lists on profiles, and a liked-by list on posts"),
            new("changelog.r0870.10", "Refined the comment section in Aethergram"),
            new("changelog.r0870.3", "Velvet now lets you cancel connection requests you've sent, review them in a compact Sent section, and disconnect from people you're connected with"),
            new("changelog.r0870.11", "Made the contact's profile picture bigger in Velvet chats"),
            new("changelog.r0870.12", "Refined the edit profile screen in Velvet"),
            new("changelog.r0870.13", "Social apps now show your username and region only, instead of your character name and home world"),
            new("changelog.r0870.4", "You can now choose the region shown on your social profiles in Settings, instead of it always being detected for you"),
            new("changelog.r0870.5", "Settings now shows a badge when there's a new changelog entry you haven't read yet"),
            new("changelog.r0870.6", "Changed the default wallpaper to Dusk"),
            new("changelog.r0870.7", "Fixed the Camera screen corners so they're rounded to match the rest of the phone"),
            new("changelog.r0870.8", "Fixed the phone-scrolling emote continuing to play while the phone is minimized"),
            new("changelog.r0870.9", "Fixed a crash in the Music app on Linux"),
        };

        public static readonly LocString[] Release0860 =
        {
            new("changelog.r0860.0", "Added Sign in with XIVAuth so you can link your account by approving in your browser instead of pasting a Lodestone code"),
            new("changelog.r0860.1", "Overhauled Games and every mini-game with arcade-style polish, screen shake, and juicier feedback"),
            new("changelog.r0860.2", "Redesigned Control Center to open with a tap and show your notifications right inside it"),
            new("changelog.r0860.3", "Home screen app icons and labels now stay legible over any wallpaper"),
            new("changelog.r0860.4", "Long posts and messages now wrap neatly as you type in every text box"),
            new("changelog.r0860.5", "You can now attach up to five photos to your feedback"),
            new("changelog.r0860.6", "Chirper now shows every reaction on a post instead of only the first three"),
            new("changelog.r0860.7", "Your own linkshell and cross-world linkshell messages now line up on the right, like direct messages"),
            new("changelog.r0860.8", "The unread badge on app icons now stays readable on any tile color"),
        };

        public static readonly LocString[] Release0851 =
        {
            new("changelog.r0851.0", "Fixed an issue where the preview of a long ringtone or notification sound kept playing after leaving Settings"),
        };

        public static readonly LocString[] Release0850 =
        {
            new("changelog.r0850.0", "Your local time now shows on your profile in Chirper, Aethergram, and Velvet"),
            new("changelog.r0850.1", "Moved the time zone setting to a new Profile section in Settings"),
            new("changelog.r0850.2", "Added Calendar with a month view of community events and your own reminders"),
            new("changelog.r0850.3", "Added Feedback so you can send thoughts and bug reports straight to the developers"),
            new("changelog.r0850.4", "Chirper, Aethergram, and Velvet now alert you to new likes, comments, and follows while the phone is closed"),
            new("changelog.r0850.5", "Added per-app notification controls to mute or set a custom sound for each app"),
            new("changelog.r0850.6", "Added a Commands page in Settings that lists every slash command"),
            new("changelog.r0850.7", "Added an option to open the phone automatically when you log in, full size or minimized"),
            new("changelog.r0850.8", "Added /phone reset to bring the phone back to the center of your screen"),
            new("changelog.r0850.9", "Refreshed every app icon with crisp new artwork"),
            new("changelog.r0850.10", "Rebuilt the apps on a shared design system for a more consistent look and feel"),
            new("changelog.r0850.11", "Polished animations and transitions throughout the interface"),
            new("changelog.r0850.12", "Tidied up the codebase for better performance and stability"),
            new("changelog.r0850.13", "Improved the home screen with app icons that magnify under your cursor and press in when tapped"),
            new("changelog.r0850.14", "Rebuilt Clock with World Clock, Alarms, Stopwatch, and Timer tabs"),
            new("changelog.r0850.15", "Added world clocks for cities around the globe alongside Eorzea and server time"),
            new("changelog.r0850.16", "Added Notes to jot things down and keep reminders with optional due dates"),
            new("changelog.r0850.17", "Added Calculator for quick everyday sums"),
            new("changelog.r0850.18", "Alarms, timers, and reminders now notify you even when the phone is closed"),
            new("changelog.r0850.19", "Added Chinese, Japanese, Spanish, and Russian translations"),
            new("changelog.r0850.20", "Improved the loading animation"),
            new("changelog.r0850.21", "Gave incoming calls and notifications their own separate sounds, chosen in Settings"),
            new("changelog.r0850.22", "Added Import from PC to use your own MP3 or WAV files as ringtones and notification sounds"),
            new("changelog.r0850.23", "Added a volume control for ringtones and notification sounds"),
            new("changelog.r0850.24", "The phone now remembers its position, keeping separate spots for the full phone and the minimized view"),
            new("changelog.r0850.25", "Velvet now alerts you to new connection requests and when yours are accepted"),
            new("changelog.r0850.26", "The server info bar entry now shows Aetherphone with your unread notification count and always stays in English"),
            new("changelog.r0850.27", "Switching to a language with a different alphabet now shows the loading screen until all its characters are ready"),
            new("changelog.r0850.28", "Fixed an issue where linkshell messages could sometimes appear as direct messages"),
        };

        public static readonly LocString[] Release0840 =
        {
            new("changelog.r0840.0", "Refined accent colors across the social apps for a more cohesive look"),
            new("changelog.r0840.1", "Replaced inline delete buttons with a centered confirmation dialog"),
            new("changelog.r0840.2", "Added the ability to delete your own comments on posts"),
            new("changelog.r0840.3", "Added tooltip labels to action icons so you know what each one does"),
            new("changelog.r0840.4", "Redesigned the News app with dynamic image sizing, pixel-perfect titles, and maintenance status pills"),
            new("changelog.r0840.5", "Added spacing around chat bubbles in Messages and restored keyboard focus after sending"),
        };

        public static readonly LocString[] Release0830 =
        {
            new("changelog.r0830.0", "The minimized phone can now be moved freely, even when the position is locked"),
            new("changelog.r0830.1", "Replaced the side button lock with tap to minimize and hold to turn off"),
            new("changelog.r0830.2", "Control Center now stays in front of other windows"),
            new("changelog.r0830.3", "The minimized phone shakes and shows an unread badge when a notification arrives"),
            new("changelog.r0830.4", "Filled in missing German, French, and Turkish translations across Messages, Notifications, Chirper, Velvet, and Photos"),
        };

        public static readonly LocString[] Release0820 =
        {
            new("changelog.r0820.0", "Added linkshell chat channels to Messages alongside direct messages"),
            new("changelog.r0820.1", "Overhauled the notification center with stacking, swipe to dismiss, and deep links into the right app"),
            new("changelog.r0820.2", "Added a minimized phone window you can restore by tapping it or on an incoming call"),
            new("changelog.r0820.3", "Chirper, Aethergram, and Velvet now each support their own profile picture"),
            new("changelog.r0820.4", "Added follow/unfollow, comment threads, and avatar cropping to Chirper"),
            new("changelog.r0820.5", "Redesigned the Velvet profile and added time zone sharing and secure image DMs"),
            new("changelog.r0820.6", "Added a confirmation step before deleting a photo"),
            new("changelog.r0820.7", "Smoothed out status bar and window transition animations"),
        };

        public static readonly LocString[] Release0810 =
        {
            new("changelog.r0810.0", "Added a subtle shadow behind home screen app labels for readability on bright wallpapers"),
            new("changelog.r0810.1", "Changed the default wallpaper"),
        };

        public static readonly LocString[] Release0800 =
        {
            new("changelog.r0800.0", "Added Velvet, a private 18+ companion app for sharing work and connecting"),
            new("changelog.r0800.1", "Added an adjustable phone window size in Settings"),
            new("changelog.r0800.2", "Added post deletion, captions, and comments across the social apps"),
            new("changelog.r0800.3", "Added this changelog to Settings"),
        };

        public static readonly LocString[] Release0710 =
        {
            new("changelog.r0710.0", "Added a guided onboarding tour with coachmarks"),
            new("changelog.r0710.1", "Added content reporting to Chirper and Aethergram"),
            new("changelog.r0710.2", "Brought call and notification banners in front of other windows"),
            new("changelog.r0710.3", "Localized Phone Calls and filled in missing translations"),
        };

        public static readonly LocString[] Release0700 =
        {
            new("changelog.r0700.0", "Added Phone with group voice calls"),
            new("changelog.r0700.1", "Added Chirper, an X-style microblog"),
            new("changelog.r0700.2", "Added Aethergram, an Instagram-style photo app"),
            new("changelog.r0700.3", "Added Find People, Maps, Collections, and Inventory apps"),
            new("changelog.r0700.4", "Added Ocean Fishing voyage predictions"),
            new("changelog.r0700.5", "Added Dailies to track recurring content"),
            new("changelog.r0700.6", "Rebuilt Activity into a fitness-style dashboard with job mastery rings"),
            new("changelog.r0700.7", "Reworked the Lodestone sign-in flow with an identity card and step guide"),
            new("changelog.r0700.8", "Added Tetris to Games, contributed by Yesanith"),
            new("changelog.r0700.9", "Connected the Aethernet apps to the production backend"),
        };

        public static readonly LocString[] Release0600 =
        {
            new("changelog.r0600.0", "Added Timers for server resets, retainers, and reminders"),
            new("changelog.r0600.1", "Added Venues to browse community events in-game"),
            new("changelog.r0600.2", "Added News with a region-aware Lodestone feed"),
            new("changelog.r0600.3", "Added Light, Dark, and Auto themes for app content"),
            new("changelog.r0600.4", "Added the Side button: tap to close, hold to lock"),
            new("changelog.r0600.5", "Rebuilt Games with new titles and an arcade-style launcher"),
            new("changelog.r0600.6", "Overhauled Clock, Market, Contacts, Wallet, Messages, and Notifications"),
        };

        public static readonly LocString[] Release0500 =
        {
            new("changelog.r0500.0", "Added image wallpapers with Light and Dark variants and custom imports"),
            new("changelog.r0500.1", "Added home screen editing, a lock screen, and Control Center"),
            new("changelog.r0500.2", "Added an idle phone-scrolling emote you can toggle in Settings"),
            new("changelog.r0500.3", "Added full localization in English, French, German, and Turkish"),
            new("changelog.r0500.4", "Refined typography, depth, and spring animations across the phone"),
        };

        public static readonly LocString[] Release0400 =
        {
            new("changelog.r0400.0", "Added Camera with a see-through viewfinder"),
            new("changelog.r0400.1", "Added the Photos gallery"),
        };

        public static readonly LocString[] Release0300 =
        {
            new("changelog.r0300.0", "Redesigned the Music home screen"),
            new("changelog.r0300.1", "Added song search and playback"),
        };

        public static readonly LocString[] Release0200 =
        {
            new("changelog.r0200.0", "Added Market with live Universalis prices"),
            new("changelog.r0200.1", "Added Music, an internet radio player"),
            new("changelog.r0200.2", "Added Wallet to track your gil"),
            new("changelog.r0200.3", "Added Chirper and Aethernet account sign-in"),
            new("changelog.r0200.4", "Added a Text Size accessibility setting"),
            new("changelog.r0200.5", "Moved notifications into an in-shell banner"),
            new("changelog.r0200.6", "Added weather glyphs and a live sky to Skywatcher"),
        };

        public static readonly LocString[] Release0130 =
        {
            new("changelog.r0130.0", "Added Lodestone character portraits"),
            new("changelog.r0130.1", "Laid the networking foundation for online features"),
        };

        public static readonly LocString[] Release0120 =
        {
            new("changelog.r0120.0", "Added an iPhone-style welcome and boot animation"),
        };

        public static readonly LocString[] Release0110 =
        {
            new("changelog.r0110.0", "Added the Games app"),
            new("changelog.r0110.1", "Added a status bar with battery, signal, and network"),
            new("changelog.r0110.2", "Added a lock button to the phone"),
        };

        public static readonly LocString[] Release0100 =
        {
            new("changelog.r0100.0", "Introduced Aetherphone, an in-game smartphone in a single window"),
            new("changelog.r0100.1", "Added the home screen, status bar, and swipe-driven app shell"),
            new("changelog.r0100.2", "Shipped the first apps: Messages, Contacts, Character, Clock, Skywatcher, and Notifications"),
        };
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

    internal static class Profile
    {
        public static readonly LocString Title = new("profile.title", "Profile");
        public static readonly LocString RegionSection = new("profile.regionSection", "Region");
        public static readonly LocString RegionHelp = new("profile.regionHelp", "Your region shows on your social profiles in place of your character name and home world.");
        public static readonly LocString RegionAutomatic = new("profile.regionAutomatic", "Automatic");
        public static readonly LocString TimeZoneSection = new("profile.timeZoneSection", "Time zone");
        public static readonly LocString TimeZoneHelp = new("profile.timeZoneHelp", "Show your local time on your profile so others can find a moment that works for both of you.");
        public static readonly LocString ShareTimeZoneLabel = new("profile.shareTimeZoneLabel", "Share my time zone");
        public static readonly LocString TimeZoneManualLabel = new("profile.timeZoneManualLabel", "Set it manually");
        public static readonly LocString UtcOffsetLabel = new("profile.utcOffsetLabel", "UTC offset");
        public static readonly LocString YourTimeLabel = new("profile.yourTimeLabel", "Your time");

        public static readonly LocString SignInToShare = new("profile.signInToShare", "Sign in to your Aethernet account to share your time zone.");
    }

    internal static class Account
    {
        public static readonly LocString Title = new("account.title", "Aethernet Account");
        public static readonly LocString HeroSubtitle = new("account.heroSubtitle", "Aethernet ID, Profile, Region");
        public static readonly LocString HeroSignInTitle = new("account.heroSignInTitle", "Sign In");
        public static readonly LocString HeroSignInSubtitle = new("account.heroSignInSubtitle", "Set up your Aethernet account, profile and region");
        public static readonly LocString SignedIn = new("account.signedIn", "Signed in");
        public static readonly LocString NotSignedIn = new("account.notSignedIn", "Not signed in");
        public static readonly LocString LogInFirst = new("account.logInFirst", "Log in to your character first");
        public static readonly LocString SignInIntro = new("account.signInIntro", "One account signs you in to every Aethernet app: Chirper, Aethergram and more. Ownership is verified through your Lodestone profile, so there's no password.");
        public static readonly LocString SigningInAs = new("account.signingInAs", "Signing in as");
        public static readonly LocString VerifyTitle = new("account.verifyTitle", "Verify with Lodestone");
        public static readonly LocString VerifyIntro = new("account.verifyIntro", "Add this code to your Lodestone profile. You can remove it afterwards.");
        public static readonly LocString Step1 = new("account.step1", "Copy the code");
        public static readonly LocString Step2 = new("account.step2", "Open your Lodestone profile");
        public static readonly LocString Step3 = new("account.step3", "Paste it into your profile, then click Confirm");
        public static readonly LocString Step4 = new("account.step4", "Verify below");
        public static readonly LocString CopyCode = new("account.copyCode", "Copy code");
        public static readonly LocString OpenProfile = new("account.openProfile", "Open Lodestone");
        public static readonly LocString VerifyAdded = new("account.verifyAdded", "Verify");
        public static readonly LocString RequestingCode = new("account.requestingCode", "Requesting a code…");
        public static readonly LocString CannotReach = new("account.cannotReach", "Could not reach Aethernet. Is the server running?");
        public static readonly LocString Verifying = new("account.verifying", "Verifying via Lodestone…");
        public static readonly LocString CodeNotFound = new("account.codeNotFound", "Code not found on your profile yet. Save it on Lodestone, then Verify again. Make sure both Profile and Character Search visibility are set to Public/Displayed in Results.");
        public static readonly LocString SignOut = new("account.signOut", "Sign out");
        public static readonly LocString SignIn = new("account.signIn", "Sign in with Lodestone");
        public static readonly LocString XivSignIn = new("account.xivSignIn", "Sign in with XIVAuth");
        public static readonly LocString LodestoneHint = new("account.lodestoneHint", "No XIVAuth account? Verify with a Lodestone code instead.");
        public static readonly LocString XivTitle = new("account.xivTitle", "Approve on XIVAuth");
        public static readonly LocString XivIntro = new("account.xivIntro", "We opened XIVAuth in your browser. Approve this device to finish signing in. If you're asked for a code, enter the one below.");
        public static readonly LocString XivWaiting = new("account.xivWaiting", "Waiting for approval…");
        public static readonly LocString XivOpen = new("account.xivOpen", "Open XIVAuth");
        public static readonly LocString XivConnecting = new("account.xivConnecting", "Connecting to XIVAuth…");
        public static readonly LocPlural Followers = new("account.followers", "{0} follower", "{0} followers");
        public static readonly LocPlural Following = new("account.following", "{0} following", "{0} following");
        public static readonly LocString FailDismiss = new("account.fail.dismiss", "Got it");
        public static readonly LocString FailCharacterNotFoundTitle = new("account.fail.characterNotFound.title", "Character not found");
        public static readonly LocString FailCharacterNotFoundBody = new("account.fail.characterNotFound.body", "We couldn't find {0} on {1} in the Lodestone search. In your Character settings, set Character Search to Public, then Verify again.");
        public static readonly LocString FailCodeNotFoundTitle = new("account.fail.codeNotFound.title", "Code not saved yet");
        public static readonly LocString FailCodeNotFoundBody = new("account.fail.codeNotFound.body", "We found your character, but the code isn't in your profile yet. Lodestone can take a minute to update after you save. Wait a moment, then Verify again. If it keeps happening, press Cancel below and try again with a new code.");
        public static readonly LocString FailLodestoneUnavailableTitle = new("account.fail.lodestoneUnavailable.title", "Lodestone unavailable");
        public static readonly LocString FailLodestoneUnavailableBody = new("account.fail.lodestoneUnavailable.body", "The Lodestone didn't respond. This is on Square Enix's side, not yours. Wait a bit, then try again.");
        public static readonly LocString FailTimeoutTitle = new("account.fail.timeout.title", "Verification timed out");
        public static readonly LocString FailTimeoutBody = new("account.fail.timeout.body", "The Lodestone took too long to respond. Your code is fine, just Verify again in a moment.");
        public static readonly LocString FailChallengeExpiredTitle = new("account.fail.challengeExpired.title", "Code expired");
        public static readonly LocString FailChallengeExpiredBody = new("account.fail.challengeExpired.body", "This sign-in code expired. Start again to get a fresh one.");
        public static readonly LocString FailBannedTitle = new("account.fail.banned.title", "Character blocked");
        public static readonly LocString FailBannedBody = new("account.fail.banned.body", "This character can't sign in to Aethernet. Reach out to support if you think this is a mistake.");
        public static readonly LocString FailRateLimitedTitle = new("account.fail.rateLimited.title", "Too many attempts");
        public static readonly LocString FailRateLimitedBody = new("account.fail.rateLimited.body", "You've tried a few times in a row. Wait a minute, then try again.");
        public static readonly LocString FailNetworkTitle = new("account.fail.network.title", "Can't reach Aethernet");
        public static readonly LocString FailNetworkBody = new("account.fail.network.body", "We couldn't reach the Aethernet server. Check your connection, then try again.");
        public static readonly LocString FailAccessDeniedTitle = new("account.fail.accessDenied.title", "Sign-in cancelled");
        public static readonly LocString FailAccessDeniedBody = new("account.fail.accessDenied.body", "The request was declined on XIVAuth. Start again whenever you're ready.");
        public static readonly LocString FailXivUnavailableTitle = new("account.fail.xivUnavailable.title", "XIVAuth unavailable");
        public static readonly LocString FailXivUnavailableBody = new("account.fail.xivUnavailable.body", "We couldn't reach XIVAuth. Wait a moment and try again, or verify with a Lodestone code instead.");
        public static readonly LocString FailXivCharacterTitle = new("account.fail.xivCharacter.title", "Character not verified");
        public static readonly LocString FailXivCharacterBody = new("account.fail.xivCharacter.body", "{0} on {1} isn't a verified character on your XIVAuth account. Add and verify it on xivauth.net, then try again.");
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
        public static readonly LocString TabDirect = new("messages.tabDirect", "Direct");
        public static readonly LocString TabLinkshells = new("messages.tabLinkshells", "Linkshells");
        public static readonly LocString LinkshellsEmpty = new("messages.linkshellsEmpty", "No linkshell chatter yet");
        public static readonly LocString DeleteHistory = new("messages.deleteHistory", "Delete history");
        public static readonly LocString DeleteHistoryConfirm = new("messages.deleteHistoryConfirm", "Delete this conversation history? This can't be undone.");
        public static readonly LocString DeleteHistoryButton = new("messages.deleteHistoryButton", "Delete");
        public static readonly LocString DeleteHistoryCancel = new("messages.deleteHistoryCancel", "Cancel");
        public static readonly LocString Linkshell = new("messages.linkshell", "Linkshell {0}");
        public static readonly LocString CrossWorldLinkshell = new("messages.crossWorldLinkshell", "Crossworld Linkshell {0}");
        public static readonly LocString Mute = new("messages.mute", "Mute");
        public static readonly LocString Unmute = new("messages.unmute", "Unmute");
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
        public static readonly LocString Activity = new("character.activity", "Activity");
        public static readonly LocString Summary = new("character.summary", "Summary");
        public static readonly LocString RingJob = new("character.ringJob", "Job");
        public static readonly LocString RingMastery = new("character.ringMastery", "Mastery");
        public static readonly LocString RingCollection = new("character.ringCollection", "Collect");
        public static readonly LocString JobLevelMax = new("character.jobLevelMax", "MAX");
        public static readonly LocString ExpToLevel = new("character.expToLevel", "{0} XP to Lv {1}");
        public static readonly LocString JobsAtMax = new("character.jobsAtMax", "{0} of {1} jobs maxed");
        public static readonly LocString JobMastery = new("character.jobMastery", "Job Mastery");
        public static readonly LocString Collection = new("character.collection", "Collection");
        public static readonly LocString Mounts = new("character.mounts", "Mounts");
        public static readonly LocString Minions = new("character.minions", "Minions");
        public static readonly LocString Retainers = new("character.retainers", "Retainers");
        public static readonly LocString VenturesReady = new("character.venturesReady", "{0} ready");
        public static readonly LocString VenturesActive = new("character.venturesActive", "{0} running");
        public static readonly LocString RetainersNone = new("character.retainersNone", "Open the summoning bell once");
        public static readonly LocString Achievements = new("character.achievements", "Achievements");
        public static readonly LocString AchievementPoints = new("character.achievementPoints", "Points");
        public static readonly LocString CollectHint = new("character.collectHint", "Open a chat with someone or view a portrait once to link Lodestone for full collection stats");
    }

    internal static class Camera
    {
        public static readonly LocString ModeSquare = new("camera.modeSquare", "SQUARE");
        public static readonly LocString ModePhoto = new("camera.modePhoto", "PHOTO");
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
        public static readonly LocString Plate = new("contacts.plate", "Plate");
        public static readonly LocString Party = new("contacts.party", "Party");
        public static readonly LocString Visit = new("contacts.visit", "Visit");
    }

    internal static class Chirper
    {
        public static readonly LocString SetUpAccount = new("chirper.setUpAccount", "Set up your account in Settings");
        public static readonly LocString Empty = new("chirper.empty", "No chirps yet. Post the first one");
        public static readonly LocString FollowingEmpty = new("chirper.followingEmpty", "Follow people to see their chirps here");
        public static readonly LocString ExploreEmpty = new("chirper.exploreEmpty", "No chirps yet. Be the first to post");
        public static readonly LocString FindPeople = new("chirper.findPeople", "Find People");
        public static readonly LocString SearchByName = new("chirper.searchByName", "Search by name, @username, or world");
        public static readonly LocString ForYou = new("chirper.forYou", "For You");
        public static readonly LocString Following = new("chirper.following", "Following");
        public static readonly LocString Follow = new("chirper.follow", "Follow");
        public static readonly LocString Unfollow = new("chirper.unfollow", "Unfollow");
        public static readonly LocString NameOrWorld = new("chirper.nameOrWorld", "Name, @username, or world");
        public static readonly LocString Compose = new("chirper.compose", "What's happening?");
        public static readonly LocString NewChirp = new("chirper.newChirp", "New Chirp");
        public static readonly LocString Post = new("chirper.post", "Post");
        public static readonly LocString EditProfile = new("chirper.editProfile", "Edit Profile");
        public static readonly LocString ChangePhoto = new("chirper.changePhoto", "Change Photo");
        public static readonly LocString ImportFromPc = new("chirper.importFromPc", "Import from PC");
        public static readonly LocString MoveAndScale = new("chirper.moveAndScale", "Move and Scale");
        public static readonly LocString GestureHint = new("chirper.gestureHint", "Drag to move · scroll to zoom");
        public static readonly LocString Use = new("chirper.use", "Use");
        public static readonly LocString DisplayNameLabel = new("chirper.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("chirper.handleLabel", "Username");
        public static readonly LocString BioLabel = new("chirper.bioLabel", "Bio");
        public static readonly LocString BioHint = new("chirper.bioHint", "Add a bio");
        public static readonly LocString Save = new("chirper.save", "Save");
        public static readonly LocString Saving = new("chirper.saving", "Saving…");
        public static readonly LocString HandleTaken = new("chirper.handleTaken", "That username is taken");
        public static readonly LocString HandleRules = new("chirper.handleRules", "3–15 characters: letters, numbers, or _");
        public static readonly LocString ProfileError = new("chirper.profileError", "Couldn't load this profile");
        public static readonly LocString React = new("chirper.react", "React");
        public static readonly LocString ReactLike = new("chirper.reactLike", "Like");
        public static readonly LocString ReactLove = new("chirper.reactLove", "Love");
        public static readonly LocString ReactLaugh = new("chirper.reactLaugh", "Haha");
        public static readonly LocString ReactWow = new("chirper.reactWow", "Wow");
        public static readonly LocString ReactSad = new("chirper.reactSad", "Sad");
        public static readonly LocString ReactAngry = new("chirper.reactAngry", "Angry");
        public static readonly LocPlural Posts = new("chirper.posts", "{0} post", "{0} posts");
        public static readonly LocPlural Likes = new("chirper.likes", "{0} like", "{0} likes");
        public static readonly LocString ReportReasonHint = new("chirper.reportReasonHint", "Why are you reporting this? (optional)");
        public static readonly LocString ReportSubmit = new("chirper.reportSubmit", "Report");
        public static readonly LocString ReportSent = new("chirper.reportSent", "Report submitted. Thank you.");
        public static readonly LocString ReportFailed = new("chirper.reportFailed", "Couldn't submit the report");
        public static readonly LocString DeleteConfirmMessage = new("chirper.deleteConfirmMessage", "Delete this post? This can't be undone.");
        public static readonly LocString DeleteCommentConfirmMessage = new("chirper.deleteCommentConfirmMessage", "Delete this comment? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("chirper.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("chirper.deleteCancel", "Cancel");
        public static readonly LocString DeleteFailed = new("chirper.deleteFailed", "Couldn't delete the post");
        public static readonly LocString DeleteCommentFailed = new("chirper.deleteCommentFailed", "Couldn't delete the comment");
        public static readonly LocString DeleteComment = new("chirper.deleteComment", "Delete comment");
        public static readonly LocString PostTitle = new("chirper.postTitle", "Post");
        public static readonly LocString NoComments = new("chirper.noComments", "No replies yet. Start the conversation");
        public static readonly LocString AddComment = new("chirper.addComment", "Add a reply…");
        public static readonly LocString RepliesTitle = new("chirper.repliesTitle", "Replies");
        public static readonly LocString ChirpsTitle = new("chirper.chirpsTitle", "Chirps");
        public static readonly LocString Reply = new("chirper.reply", "Reply");
        public static readonly LocString More = new("chirper.more", "More");
    }

    internal static class Aethergram
    {
        public static readonly LocString SetUpAccount = new("aethergram.setUpAccount", "Set up your account in Settings");
        public static readonly LocString ForYou = new("aethergram.forYou", "For You");
        public static readonly LocString Following = new("aethergram.following", "Following");
        public static readonly LocString Follow = new("aethergram.follow", "Follow");
        public static readonly LocString Unfollow = new("aethergram.unfollow", "Unfollow");
        public static readonly LocString ViewPost = new("aethergram.viewPost", "View post");
        public static readonly LocString FollowingEmpty = new("aethergram.followingEmpty", "Follow people to see their photos here");
        public static readonly LocString ExploreEmpty = new("aethergram.exploreEmpty", "No photos yet. Share the first one");
        public static readonly LocString Empty = new("aethergram.empty", "No photos yet");
        public static readonly LocString ImageFailed = new("aethergram.imageFailed", "Couldn't load image");
        public static readonly LocString ViewComments = new("aethergram.viewComments", "View {0} comments");
        public static readonly LocString NewPost = new("aethergram.newPost", "New Post");
        public static readonly LocString NewAvatar = new("aethergram.newAvatar", "New Photo");
        public static readonly LocString ImportFromPc = new("aethergram.importFromPc", "Import from PC");
        public static readonly LocString MoveAndScale = new("aethergram.moveAndScale", "Move and Scale");
        public static readonly LocString GestureHint = new("aethergram.gestureHint", "Drag to move · scroll to zoom");
        public static readonly LocString CaptionHint = new("aethergram.captionHint", "Write a caption…");
        public static readonly LocString TapToAdjust = new("aethergram.tapToAdjust", "Tap the photo to adjust the crop");
        public static readonly LocString Next = new("aethergram.next", "Next");
        public static readonly LocString Use = new("aethergram.use", "Use");
        public static readonly LocString Share = new("aethergram.share", "Share");
        public static readonly LocString Sharing = new("aethergram.sharing", "Sharing…");
        public static readonly LocString Saving = new("aethergram.saving", "Saving…");
        public static readonly LocString PostTitle = new("aethergram.postTitle", "Post");
        public static readonly LocString CommentsTitle = new("aethergram.comments", "Comments");
        public static readonly LocString GramsTitle = new("aethergram.gramsTitle", "Grams");
        public static readonly LocString NoComments = new("aethergram.noComments", "No comments yet");
        public static readonly LocString AddComment = new("aethergram.addComment", "Add a comment…");
        public static readonly LocString ProfileError = new("aethergram.profileError", "Couldn't load this profile");
        public static readonly LocString EditProfile = new("aethergram.editProfile", "Edit Profile");
        public static readonly LocString ChangePhoto = new("aethergram.changePhoto", "Change Photo");
        public static readonly LocString DisplayNameLabel = new("aethergram.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("aethergram.handleLabel", "Username");
        public static readonly LocString BioLabel = new("aethergram.bioLabel", "Bio");
        public static readonly LocString HandleRules = new("aethergram.handleRules", "3–15 characters: letters, numbers, or _");
        public static readonly LocString HandleTaken = new("aethergram.handleTaken", "That username is taken");
        public static readonly LocString Save = new("aethergram.save", "Save");
        public static readonly LocString FindPeople = new("aethergram.findPeople", "Find People");
        public static readonly LocString SearchByName = new("aethergram.searchByName", "Search by name, @username, or world");
        public static readonly LocString NameOrWorld = new("aethergram.nameOrWorld", "Name, @username, or world");
        public static readonly LocPlural Posts = new("aethergram.posts", "{0} post", "{0} posts");
        public static readonly LocPlural Likes = new("aethergram.likes", "{0} like", "{0} likes");
        public static readonly LocString ReportReasonHint = new("aethergram.reportReasonHint", "Why are you reporting this? (optional)");
        public static readonly LocString ReportSubmit = new("aethergram.reportSubmit", "Report");
        public static readonly LocString ReportSent = new("aethergram.reportSent", "Report submitted. Thank you.");
        public static readonly LocString ReportFailed = new("aethergram.reportFailed", "Couldn't submit the report");
        public static readonly LocString DeleteConfirmMessage = new("aethergram.deleteConfirmMessage", "Delete this post? This can't be undone.");
        public static readonly LocString DeleteCommentConfirmMessage = new("aethergram.deleteCommentConfirmMessage", "Delete this comment? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("aethergram.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("aethergram.deleteCancel", "Cancel");
        public static readonly LocString DeleteFailed = new("aethergram.deleteFailed", "Couldn't delete the post");
        public static readonly LocString DeleteCommentFailed = new("aethergram.deleteCommentFailed", "Couldn't delete the comment");
        public static readonly LocString DeleteComment = new("aethergram.deleteComment", "Delete comment");
        public static readonly LocString Like = new("aethergram.like", "Like");
        public static readonly LocString Comment = new("aethergram.comment", "Comment");
        public static readonly LocString More = new("aethergram.more", "More");
        public static readonly LocString Home = new("aethergram.home", "Home");
        public static readonly LocString Search = new("aethergram.search", "Search");
        public static readonly LocString Post = new("aethergram.post", "Post");
        public static readonly LocString Profile = new("aethergram.profile", "Profile");
    }

    internal static class Velvet
    {
        public static readonly LocString SetUpAccount = new("velvet.setUpAccount", "Set up your account in Settings first");
        public static readonly LocString GateTitle = new("velvet.gateTitle", "Adults only");
        public static readonly LocString GateBody = new("velvet.gateBody", "Velvet is a private, 18+ space for sharing mature work and connecting with like-minded people. By entering you confirm you are 18 or older and consent to see mature content.");
        public static readonly LocString GateDiscretion = new("velvet.gateDiscretion", "This app can show on-screen in-game, so open it when the moment is right for you.");
        public static readonly LocString GateEnter = new("velvet.gateEnter", "I am 18+, enter");
        public static readonly LocString GateLeave = new("velvet.gateLeave", "Not now");
        public static readonly LocString GateWorking = new("velvet.gateWorking", "One moment…");
        public static readonly LocString LockTitle = new("velvet.lockTitle", "Locked");
        public static readonly LocString LockPrompt = new("velvet.lockPrompt", "Enter passcode");
        public static readonly LocString LockWrong = new("velvet.lockWrong", "Wrong passcode");
        public static readonly LocString LockUnlock = new("velvet.lockUnlock", "Unlock");
        public static readonly LocString Hide = new("velvet.hide", "Hide");
        public static readonly LocString TabDiscover = new("velvet.tabDiscover", "Discover");
        public static readonly LocString TabFeed = new("velvet.tabFeed", "Feed");
        public static readonly LocString TabTimeline = new("velvet.tabTimeline", "Timeline");
        public static readonly LocString TabHome = new("velvet.tabHome", "Home");
        public static readonly LocString AllPosts = new("velvet.allPosts", "All posts");
        public static readonly LocString MyPosts = new("velvet.myPosts", "My posts");
        public static readonly LocString MyPostsEmpty = new("velvet.myPostsEmpty", "You haven't posted yet. Tap + to share something");
        public static readonly LocString Pin = new("velvet.pin", "Pin");
        public static readonly LocString Unpin = new("velvet.unpin", "Unpin");
        public static readonly LocString More = new("velvet.more", "More");
        public static readonly LocString SearchPeopleHint = new("velvet.searchPeopleHint", "Search people by tag");
        public static readonly LocString Connections = new("velvet.connections", "Connections");
        public static readonly LocString Messages = new("velvet.messages", "Messages");
        public static readonly LocString DiscoverEmpty = new("velvet.discoverEmpty", "No one to show yet. Widen your filters");
        public static readonly LocString SetupPrompt = new("velvet.setupPrompt", "Set up your profile so others can find you and connect.");
        public static readonly LocString FeedEmpty = new("velvet.feedEmpty", "Nothing here yet. Tap + to share the first");
        public static readonly LocString Fresh = new("velvet.fresh", "Fresh");
        public static readonly LocString ConnectionsEmpty = new("velvet.connectionsEmpty", "Connect with people to message them");
        public static readonly LocString MessagesEmpty = new("velvet.messagesEmpty", "No conversations yet");
        public static readonly LocString ThreadEmpty = new("velvet.threadEmpty", "Say hello");
        public static readonly LocString Connect = new("velvet.connect", "Connect");
        public static readonly LocString Requested = new("velvet.requested", "Requested");
        public static readonly LocString Connected = new("velvet.connected", "Connected");
        public static readonly LocString Message = new("velvet.message", "Message");
        public static readonly LocString MessageHint = new("velvet.messageHint", "Write a message…");
        public static readonly LocString Send = new("velvet.send", "Send");
        public static readonly LocString LookingForLabel = new("velvet.lookingForLabel", "Looking for");
        public static readonly LocString LookingCollab = new("velvet.lookingCollab", "Collab");
        public static readonly LocString LookingErp = new("velvet.lookingErp", "E/RP");
        public static readonly LocString LookingGpose = new("velvet.lookingGpose", "Gpose partner");
        public static readonly LocString LookingSharing = new("velvet.lookingSharing", "Just sharing");
        public static readonly LocString LookingRelationship = new("velvet.lookingRelationship", "Relationship");
        public static readonly LocString LookingFriends = new("velvet.lookingFriends", "Friends");
        public static readonly LocString LookingWandering = new("velvet.lookingWandering", "Just wandering");
        public static readonly LocString LookingAny = new("velvet.lookingAny", "Anything");
        public static readonly LocString PresenceOnline = new("velvet.presenceOnline", "Online");
        public static readonly LocString PresenceAway = new("velvet.presenceAway", "Away");
        public static readonly LocString PresenceDnd = new("velvet.presenceDnd", "Do not disturb");
        public static readonly LocString PresenceOffline = new("velvet.presenceOffline", "Offline");
        public static readonly LocString Locked = new("velvet.locked", "Locked");
        public static readonly LocString Unlock = new("velvet.unlock", "Unlock");
        public static readonly LocString EditProfile = new("velvet.editProfile", "Edit Profile");
        public static readonly LocString IntroLabel = new("velvet.introLabel", "About you");
        public static readonly LocString PronounsLabel = new("velvet.pronounsLabel", "Pronouns");
        public static readonly LocString DynamicLabel = new("velvet.dynamicLabel", "Your vibe");
        public static readonly LocString TagsLabel = new("velvet.tagsLabel", "Tags (comma separated)");
        public static readonly LocString LimitsLabel = new("velvet.limitsLabel", "Hard limits");
        public static readonly LocString DiscoverableLabel = new("velvet.discoverableLabel", "Appear in Discover");
        public static readonly LocString Save = new("velvet.save", "Save");
        public static readonly LocString Saving = new("velvet.saving", "Saving…");
        public static readonly LocString NewPost = new("velvet.newPost", "New Post");
        public static readonly LocString Share = new("velvet.share", "Share");
        public static readonly LocString CaptionHint = new("velvet.captionHint", "Write a caption…");
        public static readonly LocString VisibilityLabel = new("velvet.visibilityLabel", "Who can see it");
        public static readonly LocString VisibilityConnections = new("velvet.visibilityConnections", "Connections");
        public static readonly LocString VisibilityPublic = new("velvet.visibilityPublic", "Everyone in Velvet");
        public static readonly LocString VisibilityUnlockable = new("velvet.visibilityUnlockable", "Unlock only");
        public static readonly LocString Block = new("velvet.block", "Block");
        public static readonly LocString Blocked = new("velvet.blocked", "Blocked");
        public static readonly LocString Unblock = new("velvet.unblock", "Unblock");
        public static readonly LocString SafetyLabel = new("velvet.safetyLabel", "Safety");
        public static readonly LocString BlockedUsers = new("velvet.blockedUsers", "Blocked users");
        public static readonly LocString BlockedEmpty = new("velvet.blockedEmpty", "You haven't blocked anyone.");
        public static readonly LocString Like = new("velvet.like", "Like");
        public static readonly LocString Comment = new("velvet.comment", "Comment");
        public static readonly LocPlural Likes = new("velvet.likes", "{0} like", "{0} likes");
        public static readonly LocString Comments = new("velvet.comments", "Comments");
        public static readonly LocString NoComments = new("velvet.noComments", "No comments yet. Say something.");
        public static readonly LocString AddComment = new("velvet.addComment", "Add a comment…");
        public static readonly LocString ReportReasonHint = new("velvet.reportReasonHint", "Why are you reporting this? (optional)");
        public static readonly LocString ReportSubmit = new("velvet.reportSubmit", "Report");
        public static readonly LocString ReportSent = new("velvet.reportSent", "Report submitted. Thank you.");
        public static readonly LocString ReportFailed = new("velvet.reportFailed", "Couldn't submit the report");
        public static readonly LocString DeleteConfirmMessage = new("velvet.deleteConfirmMessage", "Delete this post? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("velvet.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("velvet.deleteCancel", "Cancel");
        public static readonly LocString DeleteFailed = new("velvet.deleteFailed", "Couldn't delete the post");
        public static readonly LocString DeleteCommentConfirmMessage = new("velvet.deleteCommentConfirmMessage", "Delete this comment? This can't be undone.");
        public static readonly LocString DeleteCommentFailed = new("velvet.deleteCommentFailed", "Couldn't delete the comment");
        public static readonly LocString DeleteComment = new("velvet.deleteComment", "Delete comment");
        public static readonly LocString SettingsSection = new("velvet.settingsSection", "Velvet");
        public static readonly LocString SetPasscode = new("velvet.setPasscode", "Passcode");
        public static readonly LocString ShowExplicit = new("velvet.showExplicit", "Show explicit content");
        public static readonly LocString BlurSoft = new("velvet.blurSoft", "Blur soft content until tapped");
        public static readonly LocString TabHub = new("velvet.tabHub", "Hub");
        public static readonly LocString TabMe = new("velvet.tabMe", "Me");
        public static readonly LocString Settings = new("velvet.settings", "Settings");
        public static readonly LocString OnboardIntent = new("velvet.onboardIntent", "What brings you here?");
        public static readonly LocString OnboardIntentHint = new("velvet.onboardIntentHint", "Pick what you're mainly looking for. You can change it any time.");
        public static readonly LocString OnboardVibe = new("velvet.onboardVibe", "Show your vibe");
        public static readonly LocString OnboardVibeHint = new("velvet.onboardVibeHint", "A short intro and a few tags help the right people find you.");
        public static readonly LocString OnboardPrivacy = new("velvet.onboardPrivacy", "Your privacy");
        public static readonly LocString OnboardPrivacyHint = new("velvet.onboardPrivacyHint", "You choose whether others can find you, and you can lock the app with a PIN.");
        public static readonly LocString Next = new("velvet.next", "Next");
        public static readonly LocString Back = new("velvet.back", "Back");
        public static readonly LocString EnterVelvet = new("velvet.enterVelvet", "Enter Velvet");
        public static readonly LocString Requests = new("velvet.requests", "Requests");
        public static readonly LocString Accept = new("velvet.accept", "Accept");
        public static readonly LocString Decline = new("velvet.decline", "Decline");
        public static readonly LocString WantsToConnect = new("velvet.wantsToConnect", "wants to connect");
        public static readonly LocString SentRequests = new("velvet.sentRequests", "Sent");
        public static readonly LocString Disconnect = new("velvet.disconnect", "Disconnect");
        public static readonly LocString DisconnectConfirmMessage = new("velvet.disconnectConfirmMessage", "Remove this connection?");
        public static readonly LocString StartChat = new("velvet.startChat", "Start a chat");
        public static readonly LocString PeopleToMeet = new("velvet.peopleToMeet", "People to meet");
        public static readonly LocString RelationshipLabel = new("velvet.relationshipLabel", "Relationship");
        public static readonly LocString RelNotSaying = new("velvet.relNotSaying", "Rather not say");
        public static readonly LocString RelSingle = new("velvet.relSingle", "Single");
        public static readonly LocString RelTaken = new("velvet.relTaken", "Taken");
        public static readonly LocString RelOpen = new("velvet.relOpen", "Open");
        public static readonly LocString RelComplicated = new("velvet.relComplicated", "It's complicated");
        public static readonly LocString AppLock = new("velvet.appLock", "App Lock");
        public static readonly LocString AppLockHelp = new("velvet.appLockHelp", "Require a PIN to open Velvet, so nobody who grabs your screen can peek.");
        public static readonly LocString AppLockOn = new("velvet.appLockOn", "On");
        public static readonly LocString AppLockOff = new("velvet.appLockOff", "Off");
        public static readonly LocString SetPin = new("velvet.setPin", "Set a PIN (digits)");
        public static readonly LocString RemovePin = new("velvet.removePin", "Remove lock");
        public static readonly LocString AppearHelp = new("velvet.appearHelp", "Let others find your profile in the Hub.");
        public static readonly LocString TimeZoneLabel = new("velvet.timeZoneLabel", "Time zone");
        public static readonly LocString TimeZoneHelp = new("velvet.timeZoneHelp", "Show others your current local time so it is easy to find a moment that works for both of you.");
        public static readonly LocString ShareTimeZoneLabel = new("velvet.shareTimeZoneLabel", "Share my time zone");
        public static readonly LocString TimeZoneManualLabel = new("velvet.timeZoneManualLabel", "Set it manually");
        public static readonly LocString UtcOffsetLabel = new("velvet.utcOffsetLabel", "UTC offset");
        public static readonly LocString YourTimeLabel = new("velvet.yourTimeLabel", "Your time");
        public static readonly LocString IntroHint = new("velvet.introHint", "A little about you and what you're into…");
        public static readonly LocString VibeHint = new("velvet.vibeHint", "e.g. soft, switch, service, playful");
        public static readonly LocString TagsHint = new("velvet.tagsHint", "Add tags, comma separated");
        public static readonly LocString LimitsHint = new("velvet.limitsHint", "Anything that's off the table");
        public static readonly LocString Suggestions = new("velvet.suggestions", "Tap to add");
        public static readonly LocString IdentityHeader = new("velvet.identityHeader", "Display");
        public static readonly LocString DisplayNameLabel = new("velvet.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("velvet.handleLabel", "Username");
        public static readonly LocString AboutHeader = new("velvet.aboutHeader", "About you");
        public static readonly LocString WantHeader = new("velvet.wantHeader", "What you're after");
        public static readonly LocString SafetyHeader = new("velvet.safetyHeader", "Safety");
        public static readonly LocString ViewProfile = new("velvet.viewProfile", "View my profile");
        public static readonly LocString ChangePhoto = new("velvet.changePhoto", "Change photo");
        public static readonly LocString MoveAndScale = new("velvet.moveAndScale", "Move and scale");
        public static readonly LocString GestureHint = new("velvet.gestureHint", "Drag to move, scroll to zoom");
        public static readonly LocString ImportFromPc = new("velvet.importFromPc", "Import from PC");
        public static readonly LocString SendPicture = new("velvet.sendPicture", "Send a picture");
        public static readonly LocString SaveToGallery = new("velvet.saveToGallery", "Save to gallery");
        public static readonly LocString SavedToGallery = new("velvet.savedToGallery", "Saved to gallery");
        public static readonly LocString NoPhotos = new("velvet.noPhotos", "No photos in your gallery yet");
        public static readonly LocString Use = new("velvet.use", "Use");
        public static readonly LocString VibeCardHelp = new("velvet.vibeCardHelp", "Pick the roles and moods that fit you.");
        public static readonly LocString TagsHeading = new("velvet.tagsHeading", "Tags");
        public static readonly LocString TagsCardHelp = new("velvet.tagsCardHelp", "Add themes so the right people can find you.");
        public static readonly LocString LimitsCardHelp = new("velvet.limitsCardHelp", "Anything off the table, no exceptions.");
        public static readonly LocString LookingForHelp = new("velvet.lookingForHelp", "What you're mainly here for right now.");
        public static readonly LocString RelationshipHelp = new("velvet.relationshipHelp", "Shown on your profile so people know where you stand.");
        public static readonly LocString SelectedLabel = new("velvet.selectedLabel", "Your picks");
        public static readonly LocString SuggestionsLabel = new("velvet.suggestionsLabel", "Popular");
        public static readonly LocString AddVibeHint = new("velvet.addVibeHint", "Add your own, then Enter");
        public static readonly LocString AddTagHint = new("velvet.addTagHint", "Add your own, then Enter");
        public static readonly LocString AddLimitHint = new("velvet.addLimitHint", "Add a limit, then Enter");
    }

    internal static class Clock
    {
        public static readonly LocString Local = new("clock.local", "Local");
        public static readonly LocString InGame = new("clock.inGame", "In-game");
        public static readonly LocString Server = new("clock.server", "Server");
        public static readonly LocString TabWorld = new("clock.tabWorld", "World Clock");
        public static readonly LocString TabAlarms = new("clock.tabAlarms", "Alarms");
        public static readonly LocString TabStopwatch = new("clock.tabStopwatch", "Stopwatch");
        public static readonly LocString TabTimer = new("clock.tabTimer", "Timer");
        public static readonly LocString AddCity = new("clock.addCity", "Add City");
        public static readonly LocString SearchCities = new("clock.searchCities", "Search cities");
        public static readonly LocString WorldEmpty = new("clock.worldEmpty", "Add a city to see its local time.");
        public static readonly LocString DayToday = new("clock.dayToday", "Today");
        public static readonly LocString DayTomorrow = new("clock.dayTomorrow", "Tomorrow");
        public static readonly LocString DayYesterday = new("clock.dayYesterday", "Yesterday");
        public static readonly LocString RemoveCity = new("clock.removeCity", "Remove");
        public static readonly LocString AlarmsEmpty = new("clock.alarmsEmpty", "No alarms yet. Tap + to add one.");
        public static readonly LocString NewAlarm = new("clock.newAlarm", "New Alarm");
        public static readonly LocString EditAlarm = new("clock.editAlarm", "Edit Alarm");
        public static readonly LocString AlarmLabel = new("clock.alarmLabel", "Label");
        public static readonly LocString AlarmLabelHint = new("clock.alarmLabelHint", "Alarm");
        public static readonly LocString Repeat = new("clock.repeat", "Repeat");
        public static readonly LocString RepeatNever = new("clock.repeatNever", "Never");
        public static readonly LocString RepeatEveryDay = new("clock.repeatEveryDay", "Every day");
        public static readonly LocString RepeatWeekdays = new("clock.repeatWeekdays", "Weekdays");
        public static readonly LocString RepeatWeekends = new("clock.repeatWeekends", "Weekends");
        public static readonly LocString Save = new("clock.save", "Save");
        public static readonly LocString DeleteAlarm = new("clock.deleteAlarm", "Delete Alarm");
        public static readonly LocString DeleteAlarmConfirm = new("clock.deleteAlarmConfirm", "Delete this alarm?");
        public static readonly LocString Delete = new("clock.delete", "Delete");
        public static readonly LocString KeepIt = new("clock.keepIt", "Keep");
        public static readonly LocString Start = new("clock.start", "Start");
        public static readonly LocString Stop = new("clock.stop", "Stop");
        public static readonly LocString Pause = new("clock.pause", "Pause");
        public static readonly LocString Resume = new("clock.resume", "Resume");
        public static readonly LocString Reset = new("clock.reset", "Reset");
        public static readonly LocString Lap = new("clock.lap", "Lap");
        public static readonly LocString Cancel = new("clock.cancel", "Cancel");
        public static readonly LocString Hours = new("clock.hours", "hours");
        public static readonly LocString Minutes = new("clock.minutes", "min");
        public static readonly LocString Seconds = new("clock.seconds", "sec");
        public static readonly LocString TimerWhenEnds = new("clock.timerWhenEnds", "When Timer Ends");
        public static readonly LocString Alarm = new("clock.alarm", "Alarm");
        public static readonly LocString TimerTitle = new("clock.timerTitle", "Timer");
        public static readonly LocString TimerFinished = new("clock.timerFinished", "Timer finished");
        public static readonly LocString LapNumber = new("clock.lapNumber", "Lap {0}");
    }

    internal static class Notes
    {
        public static readonly LocString TabNotes = new("notes.tabNotes", "Notes");
        public static readonly LocString TabReminders = new("notes.tabReminders", "Reminders");
        public static readonly LocString NotesEmpty = new("notes.notesEmpty", "No notes yet. Tap + to write one.");
        public static readonly LocString RemindersEmpty = new("notes.remindersEmpty", "No reminders yet. Tap + to add one.");
        public static readonly LocString NoteHint = new("notes.noteHint", "Start typing…");
        public static readonly LocString NoteTitle = new("notes.noteTitle", "Note");
        public static readonly LocString NewNote = new("notes.newNote", "New Note");
        public static readonly LocString Untitled = new("notes.untitled", "New Note");
        public static readonly LocString NoAdditionalText = new("notes.noAdditionalText", "No additional text");
        public static readonly LocString DeleteNote = new("notes.deleteNote", "Delete Note");
        public static readonly LocString DeleteNoteConfirm = new("notes.deleteNoteConfirm", "Delete this note?");
        public static readonly LocString NewReminder = new("notes.newReminder", "New Reminder");
        public static readonly LocString EditReminder = new("notes.editReminder", "Edit Reminder");
        public static readonly LocString ReminderHint = new("notes.reminderHint", "Reminder");
        public static readonly LocString AddReminderHint = new("notes.addReminderHint", "Add a reminder");
        public static readonly LocString RemindMe = new("notes.remindMe", "Remind me on a day");
        public static readonly LocString ReminderDate = new("notes.reminderDate", "Date");
        public static readonly LocString ReminderTime = new("notes.reminderTime", "Time");
        public static readonly LocString Completed = new("notes.completed", "Completed");
        public static readonly LocString Save = new("notes.save", "Save");
        public static readonly LocString Delete = new("notes.delete", "Delete");
        public static readonly LocString KeepIt = new("notes.keepIt", "Keep");
        public static readonly LocString DeleteReminder = new("notes.deleteReminder", "Delete Reminder");
        public static readonly LocString DeleteReminderConfirm = new("notes.deleteReminderConfirm", "Delete this reminder?");
    }

    internal static class Notifications
    {
        public static readonly LocString Empty = new("notifications.empty", "No notifications");
        public static readonly LocString ClearAll = new("notifications.clearAll", "Clear All");
        public static readonly LocString ShowLess = new("notifications.showLess", "Show Less");
    }

    internal static class Timers
    {
        public static readonly LocString ServerResets = new("timers.serverResets", "Server Resets");
        public static readonly LocString Activities = new("timers.activities", "Activities");
        public static readonly LocString Retainers = new("timers.retainers", "Retainers");
        public static readonly LocString Reminders = new("timers.reminders", "Reminders");
        public static readonly LocString DailyReset = new("timers.dailyReset", "Daily Reset");
        public static readonly LocString WeeklyReset = new("timers.weeklyReset", "Weekly Reset");
        public static readonly LocString GrandCompanyReset = new("timers.grandCompanyReset", "Grand Company");
        public static readonly LocString FashionReport = new("timers.fashionReport", "Fashion Report");
        public static readonly LocString JumboCactpot = new("timers.jumboCactpot", "Jumbo Cactpot");
        public static readonly LocString OceanFishing = new("timers.oceanFishing", "Ocean Fishing");
        public static readonly LocString Open = new("timers.open", "Open");
        public static readonly LocString Closed = new("timers.closed", "Closed");
        public static readonly LocString BoardingNow = new("timers.boardingNow", "Boarding now");
        public static readonly LocString Ready = new("timers.ready", "Ready!");
        public static readonly LocString NoVenture = new("timers.noVenture", "No venture");
        public static readonly LocString NotifyVentures = new("timers.notifyVentures", "Notify when ventures finish");
        public static readonly LocString OpenBellOnce = new("timers.openBellOnce", "Open your retainer bell once to load venture timers.");
        public static readonly LocString OceanDay = new("timers.oceanDay", "Day");
        public static readonly LocString OceanSunset = new("timers.oceanSunset", "Sunset");
        public static readonly LocString OceanNight = new("timers.oceanNight", "Night");
        public static readonly LocString InDays = new("timers.inDays", "in {0}d");
        public static readonly LocString InDaysHours = new("timers.inDaysHours", "in {0}d {1}h");
        public static readonly LocString ResetNotice = new("timers.resetNotice", "Server reset is here");
        public static readonly LocString VentureComplete = new("timers.ventureComplete", "Venture complete");
    }

    internal static class Fishing
    {
        public static readonly LocString NowBoarding = new("fishing.nowBoarding", "Now Boarding");
        public static readonly LocString NextVoyage = new("fishing.nextVoyage", "Next Voyage");
        public static readonly LocString Upcoming = new("fishing.upcoming", "Upcoming Voyages");
        public static readonly LocString BlueFish = new("fishing.blueFish", "Blue Fish");
        public static readonly LocString NoBlueFish = new("fishing.noBlueFish", "No blue fish on this route");
        public static readonly LocString Day = new("fishing.day", "Day");
        public static readonly LocString Sunset = new("fishing.sunset", "Sunset");
        public static readonly LocString Night = new("fishing.night", "Night");
        public static readonly LocString DeparturesNote = new("fishing.departuresNote", "Voyages depart every 2 hours from the Fisher's Guild in Limsa Lominsa.");
        public static readonly LocString InDays = new("fishing.inDays", "in {0}d {1}h");
    }

    internal static class Dailies
    {
        public static readonly LocString Daily = new("dailies.daily", "Daily");
        public static readonly LocString Weekly = new("dailies.weekly", "Weekly");
        public static readonly LocString DailyTasks = new("dailies.dailyTasks", "Daily Tasks");
        public static readonly LocString WeeklyTasks = new("dailies.weeklyTasks", "Weekly Tasks");
        public static readonly LocString AllDone = new("dailies.allDone", "All done");
        public static readonly LocString NothingLeft = new("dailies.nothingLeft", "Nothing left to do");
        public static readonly LocString Remaining = new("dailies.remaining", "{0} remaining");
        public static readonly LocString Resets = new("dailies.resets", "Resets {0}");
        public static readonly LocString NotifyReset = new("dailies.notifyReset", "Notify when tasks reset");
        public static readonly LocString AutoTracked = new("dailies.autoTracked", "Auto");
        public static readonly LocString DutyRoulettes = new("dailies.dutyRoulettes", "Duty Roulettes");
        public static readonly LocString BeastTribe = new("dailies.beastTribe", "Tribal Quests");
        public static readonly LocString MiniCactpot = new("dailies.miniCactpot", "Mini Cactpot");
        public static readonly LocString GrandCompanySupply = new("dailies.grandCompanySupply", "GC Supply & Provisioning");
        public static readonly LocString DomanEnclave = new("dailies.domanEnclave", "Doman Enclave");
        public static readonly LocString Levequests = new("dailies.levequests", "Levequest Allowances");
        public static readonly LocString WondrousTails = new("dailies.wondrousTails", "Wondrous Tails");
        public static readonly LocString JumboCactpot = new("dailies.jumboCactpot", "Jumbo Cactpot");
        public static readonly LocString CustomDeliveries = new("dailies.customDeliveries", "Custom Deliveries");
        public static readonly LocString FashionReport = new("dailies.fashionReport", "Fashion Report");
        public static readonly LocString ChallengeLog = new("dailies.challengeLog", "Challenge Log");
        public static readonly LocString RaidLockout = new("dailies.raidLockout", "Raid & Alliance Lockouts");
        public static readonly LocString HuntBills = new("dailies.huntBills", "Hunt Bills");
        public static readonly LocString AutoSection = new("dailies.autoSection", "Auto-tracked");
        public static readonly LocString ManualSection = new("dailies.manualSection", "Check manually");
        public static readonly LocString VotingOpenCloses = new("dailies.votingOpenCloses", "Open · closes {0}");
        public static readonly LocString VotingOpensIn = new("dailies.votingOpensIn", "Opens {0}");
        public static readonly LocString NextDrawing = new("dailies.nextDrawing", "Next drawing {0}");
        public static readonly LocString SealBalance = new("dailies.sealBalance", "{0} seals");
    }

    internal static class ControlCenter
    {
        public static readonly LocString Title = new("controlCenter.title", "Control Center");
        public static readonly LocString LockPosition = new("controlCenter.lockPosition", "Lock Position");
        public static readonly LocString Volume = new("controlCenter.volume", "Volume");
        public static readonly LocString Brightness = new("controlCenter.brightness", "Brightness");
        public static readonly LocString Notifications = new("controlCenter.notifications", "Notification Center");
        public static readonly LocString Accent = new("controlCenter.accent", "Accent");
        public static readonly LocString NotPlaying = new("controlCenter.notPlaying", "Not Playing");
        public static readonly LocString Customize = new("controlCenter.customize", "Customize");
        public static readonly LocString Done = new("controlCenter.done", "Done");
        public static readonly LocString AddControls = new("controlCenter.addControls", "Add a Control");
        public static readonly LocString AllControlsAdded = new("controlCenter.allControlsAdded", "Every control is in place");
        public static readonly LocString EditHint = new("controlCenter.editHint", "Drag to rearrange · tap ⤢ to resize");
    }

    internal static class Home
    {
        public static readonly LocString Done = new("home.done", "Done");
        public static readonly LocString NewFolder = new("home.newFolder", "Folder");
        public static readonly LocString Widgets = new("home.widgets", "Widgets");
        public static readonly LocString AddWidget = new("home.addWidget", "Add Widget");
        public static readonly LocString Remove = new("home.remove", "Remove");
        public static readonly LocString SizeSmall = new("home.sizeSmall", "Small");
        public static readonly LocString SizeMedium = new("home.sizeMedium", "Medium");
        public static readonly LocString SizeLarge = new("home.sizeLarge", "Large");
        public static readonly LocString Local = new("home.local", "Local");
        public static readonly LocString Eorzea = new("home.eorzea", "Eorzea");
        public static readonly LocString NoEvents = new("home.noEvents", "No upcoming events");
        public static readonly LocString HomeScreen = new("home.homeScreen", "Home Screen");
        public static readonly LocString GridComfortable = new("home.gridComfortable", "Comfortable");
        public static readonly LocString GridStandard = new("home.gridStandard", "Standard");
        public static readonly LocString GridCompact = new("home.gridCompact", "Compact");
        public static readonly LocString ResetLayout = new("home.resetLayout", "Reset Home Screen Layout");
        public static readonly LocString ResetLayoutMessage = new("home.resetLayoutMessage",
            "Restore the default icon, widget, and dock arrangement? Folders will be removed.");
        public static readonly LocString ResetLayoutConfirm = new("home.resetLayoutConfirm", "Reset");
    }

    internal static class Photos
    {
        public static readonly LocString NoPhotos = new("photos.noPhotos", "No Photos");
        public static readonly LocString UseCameraHint = new("photos.useCameraHint", "Use the Camera to take a shot");
        public static readonly LocPlural Count = new("photos.count", "{0} Photo", "{0} Photos");
        public static readonly LocString Delete = new("photos.delete", "Delete");
        public static readonly LocString DeleteConfirmMessage = new("photos.deleteConfirmMessage", "Delete this photo? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("photos.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("photos.deleteCancel", "Cancel");
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

    internal static class News
    {
        public static readonly LocString Topics = new("news.topics", "Topics");
        public static readonly LocString Notices = new("news.notices", "Notices");
        public static readonly LocString Maintenance = new("news.maintenance", "Maintenance");
        public static readonly LocString Updates = new("news.updates", "Updates");
        public static readonly LocString NoNews = new("news.noNews", "No news right now");
        public static readonly LocString CouldntReach = new("news.couldntReach", "Couldn't reach the Lodestone");
        public static readonly LocString TryAgain = new("news.tryAgain", "Try Again");
        public static readonly LocString Upcoming = new("news.upcoming", "Upcoming");
        public static readonly LocString Active = new("news.active", "In progress");
        public static readonly LocString Ended = new("news.ended", "Completed");
        public static readonly LocString RegionNorthAmerica = new("news.regionNorthAmerica", "North America");
        public static readonly LocString RegionEurope = new("news.regionEurope", "Europe");
        public static readonly LocString RegionFrance = new("news.regionFrance", "France");
        public static readonly LocString RegionGermany = new("news.regionGermany", "Germany");
        public static readonly LocString RegionJapan = new("news.regionJapan", "Japan");
    }

    internal static class Wallet
    {
        public static readonly LocString LogInToView = new("wallet.logInToView", "Log in to view your wallet");
        public static readonly LocString GilBalance = new("wallet.gilBalance", "GIL BALANCE");
    }

    internal static class Inventory
    {
        public static readonly LocString LogInToView = new("inventory.logInToView", "Log in to view your items");
        public static readonly LocString Search = new("inventory.search", "Search your items");
        public static readonly LocString SearchHint = new("inventory.searchHint", "Search to find where any item is across everything you own.");
        public static readonly LocString NoMatches = new("inventory.noMatches", "Nothing matches that");
        public static readonly LocString SourceInventory = new("inventory.sourceInventory", "Inventory");
        public static readonly LocString SourceArmoury = new("inventory.sourceArmoury", "Armoury Chest");
        public static readonly LocString SourceCrystals = new("inventory.sourceCrystals", "Crystals");
        public static readonly LocString SourceSaddlebag = new("inventory.sourceSaddlebag", "Saddlebag");
        public static readonly LocString SourceEquipped = new("inventory.sourceEquipped", "Equipped");
        public static readonly LocString SourceRetainer = new("inventory.sourceRetainer", "Retainer");
        public static readonly LocString SourceFreeCompany = new("inventory.sourceFreeCompany", "FC Chest");
        public static readonly LocString RetainerNamed = new("inventory.retainerNamed", "Retainer · {0}");
        public static readonly LocString FreeCompanyNamed = new("inventory.freeCompanyNamed", "FC Chest · {0}");
        public static readonly LocString TotalItems = new("inventory.totalItems", "Items carried");
        public static readonly LocString Gil = new("inventory.gil", "Gil");
        public static readonly LocString OnHand = new("inventory.onHand", "On hand");
        public static readonly LocString CachedSources = new("inventory.cachedSources", "Stored away");
        public static readonly LocString RetainerEmpty = new("inventory.retainerEmpty", "Open a retainer at a summoning bell to store their contents here.");
        public static readonly LocString FreeCompanyEmpty = new("inventory.freeCompanyEmpty", "Open your FC chest once to store its contents here.");
        public static readonly LocString Updated = new("inventory.updated", "Updated {0}");
        public static readonly LocString NotYetSeen = new("inventory.notYetSeen", "Not opened yet");
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
        public static readonly LocString AlertBody = new("market.alertBody", "{0} {1} is now {2} on {3}");
    }

    internal static class Games
    {
        public static readonly LocString Tetris = new("games.tetris", "Tetris");
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
        public static readonly LocString Best = new("games.best", "Best");
        public static readonly LocString NewBest = new("games.newBest", "New Best!");
        public static readonly LocString Streak = new("games.streak", "Streak");
        public static readonly LocString Easy = new("games.easy", "Easy");
        public static readonly LocString Medium = new("games.medium", "Medium");
        public static readonly LocString Hard = new("games.hard", "Hard");
        public static readonly LocString GenreMatch = new("games.genreMatch", "Match 3");
        public static readonly LocString GenrePuzzle = new("games.genrePuzzle", "Puzzle");
        public static readonly LocString GenreMemory = new("games.genreMemory", "Memory");
        public static readonly LocString GenreLogic = new("games.genreLogic", "Logic");
        public static readonly LocString GenreArcade = new("games.genreArcade", "Arcade");
        public static readonly LocString Breakout = new("games.breakout", "Breakout");
        public static readonly LocString Bubbles = new("games.bubbles", "Bubbles");
        public static readonly LocString WaterSort = new("games.waterSort", "Water Sort");
        public static readonly LocString Saved = new("games.saved", "Saved");
        public static readonly LocString Next = new("games.next", "Next");
        public static readonly LocString Lines = new("games.lines", "Lines");
        public static readonly LocString Level = new("games.level", "Level");
        public static readonly LocString Lives = new("games.lives", "Lives");
        public static readonly LocString Moves = new("games.moves", "Moves");
        public static readonly LocString Undo = new("games.undo", "Undo");
        public static readonly LocString NextLevel = new("games.nextLevel", "Next Level");
        public static readonly LocPlural AttemptsCount = new("games.attemptsCount", "{0} attempt", "{0} attempts");
        public static readonly LocString Nonogram = new("games.nonogram", "Nonogram");
        public static readonly LocString Left = new("games.left", "Left");
        public static readonly LocString Flow = new("games.flow", "Flow");
        public static readonly LocString Flows = new("games.flows", "Flows");
        public static readonly LocString Solitaire = new("games.solitaire", "Solitaire");
        public static readonly LocString GenreCards = new("games.genreCards", "Cards");
        public static readonly LocString Simon = new("games.simon", "Simon");
        public static readonly LocString Watch = new("games.watch", "Watch");
        public static readonly LocString YourTurn = new("games.yourTurn", "Your Turn");
        public static readonly LocString Flap = new("games.flap", "Flap");
        public static readonly LocString TapToStart = new("games.tapToStart", "Tap to start");
        public static readonly LocString Reversi = new("games.reversi", "Reversi");
        public static readonly LocString GenreStrategy = new("games.genreStrategy", "Strategy");
        public static readonly LocString You = new("games.you", "You");
        public static readonly LocString Cpu = new("games.cpu", "CPU");
        public static readonly LocString Lose = new("games.lose", "You Lose");
        public static readonly LocString Draw = new("games.draw", "Draw");
        public static readonly LocString Pass = new("games.pass", "Pass");
        public static readonly LocString Whack = new("games.whack", "Whack");
        public static readonly LocString Snake = new("games.snake", "Snake");
        public static readonly LocString Featured = new("games.featured", "Featured");
        public static readonly LocString Play = new("games.play", "Play");
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
        public static readonly LocString Today = new("time.today", "Today");
        public static readonly LocString Yesterday = new("time.yesterday", "Yesterday");
        public static readonly LocString InMinutes = new("time.inMinutes", "in {0}m");
        public static readonly LocString InHours = new("time.inHours", "in {0}h");
        public static readonly LocString InHoursMinutes = new("time.inHoursMinutes", "in {0}h {1}m");
    }

    internal static class Plugin
    {
        public static readonly LocString CommandHelp =new("plugin.commandHelp", "Toggle the Aetherphone. /phone market [item] opens the market board, /phone about opens credits & links, /phone reset recenters the phone, /phone test sends a sample notification.");
        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /phone.");
        public static readonly LocString SearchTheMarket = new("plugin.searchTheMarket", "Search the Market");
        public static readonly LocString SideButtonHint = new("plugin.sideButtonHint", "Tap to minimize · Hold to turn off");
        public static readonly LocString MaximizeHint = new("plugin.maximizeHint", "Maximize");
        public static readonly LocString LockPositionHint = new("plugin.lockPositionHint", "Lock position");
        public static readonly LocString UnlockPositionHint = new("plugin.unlockPositionHint", "Unlock position");
        public static readonly LocString DndEnableHint = new("plugin.dndEnableHint", "Turn on Do Not Disturb");
        public static readonly LocString DndDisableHint = new("plugin.dndDisableHint", "Turn off Do Not Disturb");
    }

    internal static class Feedback
    {
        public static readonly LocString SendFeedback = new("feedback.sendFeedback", "Send Feedback");
        public static readonly LocString Subtitle = new("feedback.subtitle", "Share your thoughts, suggestions, or bug reports directly with the developer");
        public static readonly LocString YourFeedback = new("feedback.yourFeedback", "Your feedback");
        public static readonly LocString Placeholder = new("feedback.placeholder", "What's on your mind? Suggestions, bug reports, feature ideas…");
        public static readonly LocString Send = new("feedback.send", "Send");
        public static readonly LocString Sending = new("feedback.sending", "Sending…");
        public static readonly LocString Sent = new("feedback.sent", "Feedback Sent");
        public static readonly LocString ThankYou = new("feedback.thankYou", "Thank you for your feedback!");
        public static readonly LocString SentMessage = new("feedback.sentMessage", "Your message has been sent to the developer.");
        public static readonly LocString ConfirmMessage = new("feedback.confirmMessage", "Send this feedback to the developer?");
        public static readonly LocString SendMore = new("feedback.sendMore", "Send more feedback");
        public static readonly LocString Cooldown = new("feedback.cooldown", "You can send again in {0}");
        public static readonly LocString Error = new("feedback.error", "Something went wrong");
        public static readonly LocString ErrorMessage = new("feedback.errorMessage", "Couldn't send your feedback. Please try again.");
        public static readonly LocString SignInRequired = new("feedback.signInRequired", "Sign in to Aethernet in Settings to send feedback");
        public static readonly LocString AddPhotos = new("feedback.addPhotos", "Add photos");
        public static readonly LocString ImportFromPc = new("feedback.importFromPc", "Import from PC");
        public static readonly LocString NoGallery = new("feedback.noGallery", "No photos in your gallery yet");
    }

    internal static class Polls
    {
        public static readonly LocString SignInRequired = new("polls.signInRequired", "Sign in to Aethernet in Settings to see polls");
        public static readonly LocString Empty = new("polls.empty", "No polls yet");
        public static readonly LocString EmptySubtitle = new("polls.emptySubtitle", "New polls will land here.");
        public static readonly LocString FinalResults = new("polls.finalResults", "Final results");
        public static readonly LocPlural Votes = new("polls.votes", "{0} vote", "{0} votes");
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

    internal static class Calendar
    {
        public static readonly LocString Title = new("calendar.title", "Calendar");
        public static readonly LocString Today = new("calendar.today", "Today");
        public static readonly LocString NoEvents = new("calendar.noEvents", "No Events");
        public static readonly LocString FailedToLoad = new("calendar.failedToLoad", "Couldn't load events");
        public static readonly LocString WeekSun = new("calendar.weekSun", "S");
        public static readonly LocString WeekMon = new("calendar.weekMon", "M");
        public static readonly LocString WeekTue = new("calendar.weekTue", "T");
        public static readonly LocString WeekWed = new("calendar.weekWed", "W");
        public static readonly LocString WeekThu = new("calendar.weekThu", "T");
        public static readonly LocString WeekFri = new("calendar.weekFri", "F");
        public static readonly LocString WeekSat = new("calendar.weekSat", "S");
        public static readonly LocString NewEvent = new("calendar.newEvent", "New Event");
        public static readonly LocString TitlePlaceholder = new("calendar.titlePlaceholder", "Event name");
        public static readonly LocString EventDate = new("calendar.eventDate", "Date");
        public static readonly LocString EventTime = new("calendar.eventTime", "Time");
        public static readonly LocString Save = new("calendar.save", "Save");
        public static readonly LocString DeleteEvent = new("calendar.deleteEvent", "Delete Event");
        public static readonly LocString DeleteConfirmMessage = new("calendar.deleteConfirmMessage", "Are you sure you want to delete this event?");
        public static readonly LocString DeleteConfirm = new("calendar.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("calendar.deleteCancel", "Cancel");
    }

    internal static class Onboarding
    {
        public static readonly LocString Continue = new("onboarding.continue", "Continue");
        public static readonly LocString GetStarted = new("onboarding.getStarted", "Get Started");
        public static readonly LocString GotIt = new("onboarding.gotIt", "Got it");
        public static readonly LocString Skip = new("onboarding.skip", "Skip");
        public static readonly LocString SkipTour = new("onboarding.skipTour", "Skip tour");
        public static readonly LocString TapToContinue = new("onboarding.tapToContinue", "Tap to continue");
        public static readonly LocString WelcomeTitle = new("onboarding.welcomeTitle", "Welcome to Aetherphone");
        public static readonly LocString WelcomeBody = new("onboarding.welcomeBody", "Your very own smartphone, right here in Eorzea. Here's a quick tour of what it can do.");
        public static readonly LocString AllInOneTitle = new("onboarding.allInOneTitle", "Everything in one place");
        public static readonly LocString AllInOneBody = new("onboarding.allInOneBody", "Chat, music, weather, the market board, mini-games and more, all in your pocket.");
        public static readonly LocString FeedbackTitle = new("onboarding.feedbackTitle", "Still a work in progress");
        public static readonly LocString FeedbackBody = new("onboarding.feedbackBody", "This plugin is nowhere near a full release yet. I would love your opinions, feature ideas, and criticism. Reach me on GitHub: the links are in Settings, About Aetherphone.");
        public static readonly LocString BeginTitle = new("onboarding.beginTitle", "Ready when you are");
        public static readonly LocString BeginBody = new("onboarding.beginBody", "Tap the app icon to dive in. You can replay any tip later from Settings.");
        public static readonly LocString WidgetTourTitle = new("onboarding.widgetTourTitle", "Live at a glance");
        public static readonly LocString WidgetTourBody = new("onboarding.widgetTourBody", "Widgets live on your Home Screen and update on their own. This one shows the Eorzean weather wherever you're standing.");
        public static readonly LocString FriendsTourTitle = new("onboarding.friendsTourTitle", "Meet your Friends");
        public static readonly LocString FriendsTourBody = new("onboarding.friendsTourBody", "Give the Friends app a tap. This is where you keep the people you meet in Eorzea. Every app opens like this.");
        public static readonly LocString MyNumberTourTitle = new("onboarding.myNumberTourTitle", "Your very own number");
        public static readonly LocString MyNumberTourBody = new("onboarding.myNumberTourBody", "Every Aetherphone gets its own number. Share yours with new friends so they can add you and give you a call, right here in game.");
        public static readonly LocString ReturnHomeTitle = new("onboarding.returnHomeTitle", "Head back home");
        public static readonly LocString ReturnHomeBody = new("onboarding.returnHomeBody", "This bar brings you home from any app. Give it a tap to go back.");
        public static readonly LocString CustomizeTitle = new("onboarding.customizeTitle", "Make it your own");
        public static readonly LocString CustomizeBody = new("onboarding.customizeBody", "Press and hold anywhere on the Home Screen to rearrange icons, resize widgets, and add new ones.");
        public static readonly LocString ControlCenterTitle = new("onboarding.controlCenterTitle", "Control Center");
        public static readonly LocString ControlCenterBody = new("onboarding.controlCenterBody", "Tap the top of the screen to open Control Center, with volume, brightness, accent color, and your notifications.");
        public static readonly LocString SignalTourTitle = new("onboarding.signalTourTitle", "Live signal");
        public static readonly LocString SignalTourBody = new("onboarding.signalTourBody", "These bars are your real ping to Aethernet, updating as you play. More bars means a faster connection.");
        public static readonly LocString BatteryTourTitle = new("onboarding.batteryTourTitle", "Real battery");
        public static readonly LocString BatteryTourBody = new("onboarding.batteryTourBody", "And this is your device's actual battery, read straight from your computer.");
        public static readonly LocString MinimizeTitle = new("onboarding.minimizeTitle", "Tuck it away");
        public static readonly LocString MinimizeBody = new("onboarding.minimizeBody", "This side button shrinks the phone into a corner. Click it to bring the phone back, or hold it to close.");
        public static readonly LocString LockTitle = new("onboarding.lockTitle", "Lock it in place");
        public static readonly LocString LockBody = new("onboarding.lockBody", "This button locks the phone's position on your screen so it stays put while you play. That's the tour: enjoy your Aetherphone.");
        public static readonly LocString MessagesTitle = new("onboarding.messagesTitle", "Messages");
        public static readonly LocString MessagesBody = new("onboarding.messagesBody", "Every /tell you get in game turns into a chat bubble here. Read and reply straight from your phone, and get a badge the moment someone new writes.");
        public static readonly LocString SkywatcherTitle = new("onboarding.skywatcherTitle", "Skywatcher");
        public static readonly LocString SkywatcherBody = new("onboarding.skywatcherBody", "Live Eorzean weather for wherever you're standing, refreshed as you travel.");
        public static readonly LocString SkywatcherForecastTitle = new("onboarding.skywatcherForecastTitle", "The hours ahead");
        public static readonly LocString SkywatcherForecastBody = new("onboarding.skywatcherForecastBody", "And here's what's coming, hour by hour, so you can plan around the weather.");
        public static readonly LocString MarketTitle = new("onboarding.marketTitle", "Market");
        public static readonly LocString MarketBody = new("onboarding.marketBody", "Live market board prices from across your world, powered by Universalis. Search any item, or right-click one in game to look it up.");
        public static readonly LocString MarketStatsTitle = new("onboarding.marketStatsTitle", "Know before you sell");
        public static readonly LocString MarketStatsBody = new("onboarding.marketStatsBody", "See the cheapest listings, price history and sale trends, and set an alert to get pinged when a price drops.");
        public static readonly LocString VenuesTitle = new("onboarding.venuesTitle", "Venues");
        public static readonly LocString VenuesBody = new("onboarding.venuesBody", "Discover live player-run venues and events, from clubs to photo spots. One tap travels you there with Lifestream.");
        public static readonly LocString MusicTitle = new("onboarding.musicTitle", "Music");
        public static readonly LocString MusicBody = new("onboarding.musicBody", "Your in-game music player. Browse genre radio stations or search for any track you like.");
        public static readonly LocString MusicNowPlayingTitle = new("onboarding.musicNowPlayingTitle", "Always with you");
        public static readonly LocString MusicNowPlayingBody = new("onboarding.musicNowPlayingBody", "Playback keeps going while you play, with a Now Playing banner right on your home screen.");
        public static readonly LocString GamesTitle = new("onboarding.gamesTitle", "Games");
        public static readonly LocString GamesBody = new("onboarding.gamesBody", "A whole pocket arcade, 15 mini-games from puzzles to reflex tests, and every one remembers your best score.");
        public static readonly LocString CameraTitle = new("onboarding.cameraTitle", "Camera");
        public static readonly LocString CameraBody = new("onboarding.cameraBody", "Snap in-game photos straight from your phone. Pick square or photo, frame up with the grid, and tap the shutter.");
        public static readonly LocString PhotosTitle = new("onboarding.photosTitle", "Photos");
        public static readonly LocString PhotosBody = new("onboarding.photosBody", "Every shot you take lands here in a tidy gallery. Tap any photo to view it full-screen.");
        public static readonly LocString SettingsTitle = new("onboarding.settingsTitle", "Make it yours");
        public static readonly LocString SettingsBody = new("onboarding.settingsBody", "Themes, wallpapers, text size and how your phone behaves. Poke around and set it up just how you like.");
        public static readonly LocString ContactsBody = new("onboarding.contactsBody", "Your in-game friends, laid out like a proper address book with their portraits. Tap anyone to start a conversation.");
        public static readonly LocString CharacterBody = new("onboarding.characterBody", "A tidy profile card for your character, with your gear, portrait and the basics all in one glance.");
        public static readonly LocString ChirperBody = new("onboarding.chirperBody", "Welcome to Chirper! A little social feed built just for the Aetherphone community, short posts and timelines with other players.");
        public static readonly LocString ChirperPostTitle = new("onboarding.chirperPostTitle", "Join the conversation");
        public static readonly LocString ChirperPostBody = new("onboarding.chirperPostBody", "Post what's on your mind, follow people, and reply or react to their chirps. It runs on our own network, completely separate from the Lodestone.");
        public static readonly LocString ChirperKindTitle = new("onboarding.chirperKindTitle", "Consent and Respect");
        public static readonly LocString ChirperKindBody = new("onboarding.chirperKindBody", "This space is for everyone. Discriminatory, hateful or harmful content isn't welcome and can get you banned.");
        public static readonly LocString AethergramBody = new("onboarding.aethergramBody", "Welcome to Aethergram! A photo-sharing app made for the Aetherphone community, a lot like the real thing.");
        public static readonly LocString AethergramShareTitle = new("onboarding.aethergramShareTitle", "Share your world");
        public static readonly LocString AethergramShareBody = new("onboarding.aethergramShareBody", "Set up your profile, post your best shots, follow other players, and like or comment on theirs.");
        public static readonly LocString AethergramSafeTitle = new("onboarding.aethergramSafeTitle", "Safe and private");
        public static readonly LocString AethergramSafeBody = new("onboarding.aethergramSafeBody", "It's completely separate from the Lodestone. Nothing here is linked to your character or account.");
        public static readonly LocString AethergramKindTitle = new("onboarding.aethergramKindTitle", "Consent and Respect");
        public static readonly LocString AethergramKindBody = new("onboarding.aethergramKindBody", "This space is for everyone. Discriminatory, hateful or harmful content isn't welcome and can get you banned.");
        public static readonly LocString MapsBody = new("onboarding.mapsBody", "Every zone map with its aetherytes and points of interest. Star the places you visit most for one-tap access.");
        public static readonly LocString FindPeopleBody = new("onboarding.findPeopleBody", "Look up any character or Free Company on the Lodestone, profiles, gear and rosters, right from your phone.");
        public static readonly LocString NewsBody = new("onboarding.newsBody", "The Lodestone feed for your region, topics, notices, maintenance times and updates, with a tap to read the full story.");
        public static readonly LocString CollectionsBody = new("onboarding.collectionsBody", "Track your mounts, minions, emotes, orchestrion rolls and more. See what you've got and what's still out there to find.");
        public static readonly LocString WalletBody = new("onboarding.walletBody", "Gil, tomestones, hunt seals and every currency you care about, with your weekly caps, all at a glance.");
        public static readonly LocString InventoryBody = new("onboarding.inventoryBody", "Peek at what's on you and stashed with your retainers, so you always know what you're carrying.");
        public static readonly LocString ClockBody = new("onboarding.clockBody", "A clean analog clock on Eorzea time, for when you just need to know the hour.");
        public static readonly LocString TimersBody = new("onboarding.timersBody", "Countdowns to the daily, Grand Company and weekly resets, plus Fashion Report, the Jumbo Cactpot and your retainer ventures. Switch on reminders and the phone nudges you.");
        public static readonly LocString DailiesBody = new("onboarding.dailiesBody", "A simple checklist for your daily and weekly routines. Tick things off and it all resets right on schedule.");
        public static readonly LocString FishingBody = new("onboarding.fishingBody", "Bite windows, handy tips and the best spots for the fish worth chasing.");
        public static readonly LocString NotificationsBody = new("onboarding.notificationsBody", "A running history of everything your phone has pinged you about, so nothing slips past you.");
        public static readonly LocString VelvetBody = new("onboarding.velvetBody", "Welcome to Velvet! A private, 18+ space for sharing mature work and connecting with like-minded people, separate from the rest of the phone.");
        public static readonly LocString VelvetDiscoverTitle = new("onboarding.velvetDiscoverTitle", "Discover people");
        public static readonly LocString VelvetDiscoverBody = new("onboarding.velvetDiscoverBody", "Browse profiles filtered by what people are looking for, and send a connection request when you find someone interesting.");
        public static readonly LocString VelvetMessagesTitle = new("onboarding.velvetMessagesTitle", "Requests and messages");
        public static readonly LocString VelvetMessagesBody = new("onboarding.velvetMessagesBody", "Accept or decline requests, then chat privately with the connections you make.");
        public static readonly LocString VelvetProfileTitle = new("onboarding.velvetProfileTitle", "Your profile");
        public static readonly LocString VelvetProfileBody = new("onboarding.velvetProfileBody", "Set up your intro, vibe, tags and limits, and choose whether you're discoverable to others.");
        public static readonly LocString VelvetKindTitle = new("onboarding.velvetKindTitle", "Consent and Respect");
        public static readonly LocString VelvetKindBody = new("onboarding.velvetKindBody", "This space is for everyone. Discriminatory, hateful or harmful content isn't welcome and can get you banned.");
        public static readonly LocString FeedbackIntroTitle = new("onboarding.feedbackIntroTitle", "Feedback");
        public static readonly LocString FeedbackIntroBody = new("onboarding.feedbackIntroBody", "Tell the developer what you think: suggestions, bug reports, feature ideas, or just a hello.");
        public static readonly LocString FeedbackWriteTitle = new("onboarding.feedbackWriteTitle", "Write and send");
        public static readonly LocString FeedbackWriteBody = new("onboarding.feedbackWriteBody", "Type your message and tap Send. Your feedback goes directly to the developer's dashboard.");
        public static readonly LocString FeedbackPrivacyTitle = new("onboarding.feedbackPrivacyTitle", "Honest and respectful");
        public static readonly LocString FeedbackPrivacyBody = new("onboarding.feedbackPrivacyBody", "Your character name is attached so the developer knows who you are in game. Be constructive and kind.");
        public static readonly LocString PhoneBody = new("onboarding.phoneBody", "Call your friends directly in-game and talk over voice chat. The other person needs the Aetherphone plugin too, and you both need to be signed in to Aethernet from Settings.");
        public static readonly LocString PhoneGroupTitle = new("onboarding.phoneGroupTitle", "Group calls");
        public static readonly LocString PhoneGroupBody = new("onboarding.phoneGroupBody", "While a call is active, add more people to bring everyone into the same conversation. Group calls are supported too.");
        public static readonly LocString PhoneVoiceTitle = new("onboarding.phoneVoiceTitle", "Voice settings");
        public static readonly LocString PhoneVoiceBody = new("onboarding.phoneVoiceBody", "You can pick your microphone and adjust voice input options from Settings.");
        public static readonly LocString CalendarBody = new("onboarding.calendarBody", "A month view of community events across Eorzea, right beside your own plans. Tap any day to see what's on.");
        public static readonly LocString CalendarAddBody = new("onboarding.calendarAddBody", "Tap the plus to save your own event with its date and time. It sits on the calendar alongside everything else.");
        public static readonly LocString NotesBody = new("onboarding.notesBody", "A quick place to jot things down. Tap the plus to start a note, and it saves itself as you type.");
        public static readonly LocString NotesRemindersBody = new("onboarding.notesRemindersBody", "Switch to the Reminders tab for a simple to-do list. Give one a due date and the phone nudges you when it's time.");
        public static readonly LocString CalculatorBody = new("onboarding.calculatorBody", "A simple calculator for quick everyday sums, with a running tape of your recent results to scroll back through.");
        public static readonly LocString PollsBody = new("onboarding.pollsBody", "Community polls from across Aethernet. Tap an option to cast your vote and see where everyone stands.");
        public static readonly LocString PollsResultsTitle = new("onboarding.pollsResultsTitle", "Live results");
        public static readonly LocString PollsResultsBody = new("onboarding.pollsResultsBody", "Every vote updates the bars in real time. Once a poll closes, you'll see the final tally.");
    }
}
