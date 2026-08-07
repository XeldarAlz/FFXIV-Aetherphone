namespace Aetherphone.Core.Localization;

internal static class L
{
    internal static class Common
    {
        public static readonly LocString Loading = new("common.loading", "Loading…");
        public static readonly LocString AppDrawFailure = new("common.appDrawFailure", "This app hit a problem. Reopen it to try again.");
        public static readonly LocString Searching = new("common.searching", "Searching…");
        public static readonly LocString Search = new("common.search", "Search");
        public static readonly LocString Refresh = new("common.refresh", "Refresh");
        public static readonly LocString Emoji = new("common.emoji", "Emoji");
        public static readonly LocString Cancel = new("common.cancel", "Cancel");
        public static readonly LocString Retry = new("common.retry", "Retry");
        public static readonly LocString Copied = new("common.copied", "Copied");
        public static readonly LocString LoadFailed = new("common.loadFailed", "Could not load");
        public static readonly LocString FileNotDownloaded =
            new("common.fileNotDownloaded", "That file is stored online and is not on this PC yet. Open it once in File Explorer to download it, then pick it again.");
        public static readonly LocString LoadFailedHint =
            new("common.loadFailedHint", "Check your connection and try again.");
        public static readonly LocString Close = new("common.close", "Close");
        public static readonly LocString Alerts = new("common.alerts", "Alerts");
        public static readonly LocString Live = new("common.live", "LIVE");
        public static readonly LocString Hq = new("common.hq", "HQ");
        public static readonly LocString Nq = new("common.nq", "NQ");
        public static readonly LocString OpenInBrowser = new("common.openInBrowser", "Click to open in browser");
        public static readonly LocString OpenInWindow = new("common.openInWindow", "Open in a window");
        public static readonly LocString Next = new("common.next", "Next");
        public static readonly LocString Previous = new("common.previous", "Previous");
        public static readonly LocString PhotoCounter = new("common.photoCounter", "{0}/{1}");
        public static readonly LocString PhotoStep = new("common.photoStep", "Photo {0} of {1}");
        public static readonly LocString PhotoLimit = new("common.photoLimit", "You can add up to {0} photos");
        public static readonly LocString ImageFailed = new("common.imageFailed", "Couldn't load image");
        public static readonly LocString Pin = new("common.pin", "Pin");
        public static readonly LocString Unpin = new("common.unpin", "Unpin");
        public static readonly LocString ChangePhoto = new("common.changePhoto", "Change photo");
        public static readonly LocString SendPhoto = new("common.sendPhoto", "Send photo");
        public static readonly LocString ImportFromPc = new("common.importFromPc", "Import from PC");
        public static readonly LocString SaveToGallery = new("common.saveToGallery", "Save to gallery");
        public static readonly LocString SavedToGallery = new("common.savedToGallery", "Saved to gallery");
        public static readonly LocString NoPhotos = new("common.noPhotos", "No photos in your gallery yet");
        public static readonly LocString FileKindImages = new("common.fileKindImages", "Images");
        public static readonly LocString FileKindAudio = new("common.fileKindAudio", "Audio");
        public static readonly LocString FileKindAll = new("common.fileKindAll", "All files");
        public static readonly LocString RateLimited = new("common.rateLimited", "Too many requests. Retrying in {0}s");
    }

    internal static class Social
    {
        public static readonly LocString RoleManagement = new("social.roleManagement", "Management");
        public static readonly LocString RoleDeveloper = new("social.roleDeveloper", "Developer");
        public static readonly LocString RoleModerator = new("social.roleModerator", "Moderator");
        public static readonly LocString RoleVerified = new("social.roleVerified", "Verified");
        public static readonly LocString RolePatreon = new("social.rolePatreon", "Patreon member");
        public static readonly LocString RoleSupport = new("social.roleSupport", "Support");
        public static readonly LocString RoleContributor = new("social.roleContributor", "Contributor");
        public static readonly LocString RoleAide = new("social.roleAide", "Aide");
        public static readonly LocString RoleAurelia = new("social.roleAurelia", "Aurelia");
        public static readonly LocString AspectSquare = new("social.aspectSquare", "Square");
        public static readonly LocString AspectPortrait = new("social.aspectPortrait", "Portrait");
        public static readonly LocString AspectLandscape = new("social.aspectLandscape", "Landscape");
        public static readonly LocString LikedChirp = new("social.likedChirp", "liked your chirp");
        public static readonly LocString LikedPhoto = new("social.likedPhoto", "liked your photo");
        public static readonly LocString LikedComment = new("social.likedComment", "liked your comment");
        public static readonly LocString CommentedChirp = new("social.commentedChirp", "commented on your chirp");
        public static readonly LocString CommentedPhoto = new("social.commentedPhoto", "commented on your photo");
        public static readonly LocString MentionedChirp = new("social.mentionedChirp", "mentioned you in a chirp");
        public static readonly LocString MentionedPhoto = new("social.mentionedPhoto", "mentioned you in a photo");
        public static readonly LocString MentionedComment = new("social.mentionedComment", "mentioned you in a comment");
        public static readonly LocString RepostedChirp = new("social.repostedChirp", "rechirped your chirp");
        public static readonly LocString RepostedPhoto = new("social.repostedPhoto", "reposted your photo");
        public static readonly LocString QuotedChirp = new("social.quotedChirp", "quoted your chirp");
        public static readonly LocString QuotedPhoto = new("social.quotedPhoto", "quoted your photo");
        public static readonly LocString ViewProfile = new("social.viewProfile", "View profile");
        public static readonly LocString BlockAction = new("social.blockAction", "Block");
        public static readonly LocString BlockConfirm = new("social.blockConfirm", "Block {0}? You won't see each other's posts, comments, or profile, and any follows between you are removed.");
        public static readonly LocString BlockedUsers = new("social.blockedUsers", "Blocked people");
        public static readonly LocString BlockedEmpty = new("social.blockedEmpty", "You haven't blocked anyone.");
        public static readonly LocString BlockedHint = new("social.blockedHint", "Blocking applies across Chirper, Aethergram, and calls. Tap someone to unblock them.");
        public static readonly LocString Unblock = new("social.unblock", "Unblock");
        public static readonly LocString UnblockConfirm = new("social.unblockConfirm", "Unblock {0}? They'll be able to see your posts and follow you again.");
        public static readonly LocString MentionSearching = new("social.mentionSearching", "Looking for people");
        public static readonly LocString TaggedPhoto = new("social.taggedPhoto", "tagged you in a photo");
        public static readonly LocString AudienceEveryone = new("social.audienceEveryone", "Everyone");
        public static readonly LocString AudienceFollowing = new("social.audienceFollowing", "People you follow");
        public static readonly LocString AudienceNoOne = new("social.audienceNoOne", "No one");
        public static readonly LocString Followed = new("social.followed", "started following you");
        public static readonly LocString ConnectionRequest = new("social.connectionRequest", "wants to connect with you");
        public static readonly LocString ConnectionAccepted = new("social.connectionAccepted", "accepted your connection request");
        public static readonly LocString FollowersTitle = new("social.followersTitle", "Followers");
        public static readonly LocString FollowingTitle = new("social.followingTitle", "Following");
        public static readonly LocString LikedByTitle = new("social.likedByTitle", "Liked by");
        public static readonly LocString ListEmpty = new("social.listEmpty", "No one here yet");
        public static readonly LocString MutualsTitle = new("social.mutualsTitle", "Followed by");
        public static readonly LocString FollowsYou = new("social.followsYou", "Follows you");
        public static readonly LocString FollowedByOne = new("social.followedByOne", "Followed by {0}");
        public static readonly LocString FollowedByTwo = new("social.followedByTwo", "Followed by {0} and {1}");
        public static readonly LocString FollowedByOneMoreOne = new("social.followedByOneMoreOne", "Followed by {0} and 1 other");
        public static readonly LocString FollowedByOneMoreMany = new("social.followedByOneMoreMany", "Followed by {0} and {1} others");
        public static readonly LocString FollowedByTwoMoreOne = new("social.followedByTwoMoreOne", "Followed by {0}, {1} and 1 other");
        public static readonly LocString FollowedByTwoMoreMany = new("social.followedByTwoMoreMany", "Followed by {0}, {1} and {2} others");
        public static readonly LocString AllowMessages = new("social.allowMessages", "Who can message you");
        public static readonly LocString MessagesAudienceHint = new("social.messagesAudienceHint", "Controls who can start a new conversation with you on Aethergram. People you have replied to can always message you.");
        public static readonly LocString ActivityTitle = new("social.activityTitle", "Notifications");
        public static readonly LocString ActivityTab = new("social.activityTab", "Activity");
        public static readonly LocString ActivityEmpty = new("social.activityEmpty", "Nothing here yet. Interactions with your posts will show up here");
        public static readonly LocString FollowRequests = new("social.followRequests", "Follow requests");
        public static readonly LocString FollowRequestsCount = new("social.followRequestsCount", "Follow requests ({0})");
        public static readonly LocString RequestedFollow = new("social.requestedFollow", "requested to follow you");
        public static readonly LocString AcceptedFollow = new("social.acceptedFollow", "accepted your follow request");
        public static readonly LocString Requested = new("social.requested", "Requested");
        public static readonly LocString Confirm = new("social.confirm", "Confirm");
        public static readonly LocString Delete = new("social.delete", "Delete");
    }

    internal static class PhotoTag
    {
        public static readonly LocString TagPeople = new("photoTag.tagPeople", "Tag people");
        public static readonly LocString TapToTag = new("photoTag.tapToTag", "Tap the photo to tag someone");
        public static readonly LocString PickPerson = new("photoTag.pickPerson", "Tag someone");
        public static readonly LocString SearchHint = new("photoTag.searchHint", "Search by name or @username");
        public static readonly LocString NoPeople = new("photoTag.noPeople", "No one found");
        public static readonly LocString TaggedTab = new("photoTag.taggedTab", "Tagged");
        public static readonly LocString PostsTab = new("photoTag.postsTab", "Posts");
        public static readonly LocString NoTagged = new("photoTag.noTagged", "No photos of you yet");
        public static readonly LocString TagLimit = new("photoTag.tagLimit", "You can tag up to {0} people");
        public static readonly LocString SettingsTitle = new("photoTag.settingsTitle", "Tags and mentions");
        public static readonly LocString SignInPrompt = new("photoTag.signInPrompt", "Sign in to Aethernet to choose who can mention and tag you");
        public static readonly LocString AllowMentions = new("photoTag.allowMentions", "Allow mentions from");
        public static readonly LocString AllowTags = new("photoTag.allowTags", "Allow tags from");
        public static readonly LocString AudienceHint = new("photoTag.audienceHint", "Choose who can mention you in posts and comments, and who can tag you in photos.");
        public static readonly LocString ApprovalHeader = new("photoTag.approvalHeader", "Approval");
        public static readonly LocString ApproveManually = new("photoTag.approveManually", "Manually approve tags");
        public static readonly LocString ApproveHint = new("photoTag.approveHint", "Tags stay hidden until you approve them, and never reach your Tagged tab without you.");
    }

    internal static class Story
    {
        public static readonly LocString YourStory = new("story.yourStory", "Your story");
        public static readonly LocString NewStory = new("story.newStory", "New Story");
        public static readonly LocString DeleteMessage = new("story.deleteMessage", "Delete this story? It disappears for everyone right away.");
        public static readonly LocPlural SeenBy = new("story.seenBy", "Seen by {0}", "Seen by {0}");
        public static readonly LocString NoViewers = new("story.noViewers", "No one has seen this yet");
        public static readonly LocString ViewersTrimmed = new("story.viewersTrimmed", "Showing the latest {0} of {1}");
        public static readonly LocString DeleteFailed = new("story.deleteFailed", "Couldn't delete the story");
    }

    internal static class Safety
    {
        public static readonly LocString Title = new("safety.title", "Moderation and safety");
        public static readonly LocString Summary = new("safety.summary", "Warnings and removals");
        public static readonly LocString UnreadSummary = new("safety.unreadSummary", "{0} unread");
        public static readonly LocString Empty = new("safety.empty", "Nothing here. No moderator has acted on your account.");
        public static readonly LocString SignInPrompt = new("safety.signInPrompt", "Sign in to see moderation notices for your account.");
        public static readonly LocString RetentionHint = new("safety.retentionHint", "Notices stay here for 180 days, whether or not you have read them.");
        public static readonly LocString LoadOlder = new("safety.loadOlder", "Load older notices");
        public static readonly LocString PostedOn = new("safety.postedOn", "You posted it {0}");
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
        public static readonly LocString RemovedCommentTitle = new("moderation.removedCommentTitle", "Comment removed");
        public static readonly LocString RemovedCommentAdult = new("moderation.removedCommentAdult", "Your comment was removed because it appears to contain adult content, which is not allowed here.");
        public static readonly LocString RemovedCommentViolence = new("moderation.removedCommentViolence", "Your comment was removed because it appears to contain violent or graphic content.");
        public static readonly LocString RemovedCommentHarassment = new("moderation.removedCommentHarassment", "Your comment was removed because it appears to contain abusive or harassing language.");
        public static readonly LocString RemovedCommentHate = new("moderation.removedCommentHate", "Your comment was removed because it appears to contain hateful content.");
        public static readonly LocString RemovedCommentSelfHarm = new("moderation.removedCommentSelfHarm", "Your comment was removed because it appears to reference self-harm.");
        public static readonly LocString RemovedCommentPolicy = new("moderation.removedCommentPolicy", "Your comment was removed for violating the community guidelines.");
        public static readonly LocString WarningTitle = new("moderation.warningTitle", "Warning from moderators");
        public static readonly LocString WarningBody = new("moderation.warningBody", "A moderator reviewed your activity. Please follow the community guidelines. Repeated violations can lead to a ban.");
        public static readonly LocString ReportUpdateTitle = new("moderation.reportUpdateTitle", "Report update");
        public static readonly LocString ReportResolvedBody = new("moderation.reportResolvedBody", "Thanks for your report. We reviewed it and took action.");
        public static readonly LocString ReportDismissedBody = new("moderation.reportDismissedBody", "Thanks for your report. Our moderators reviewed it and took the appropriate action.");

        public static readonly LocString NoticeRemovedChirp = new("moderation.notice.removedChirp", "Your chirp was removed");
        public static readonly LocString NoticeRemovedGram = new("moderation.notice.removedGram", "Your gram was removed");
        public static readonly LocString NoticeRemovedVelvetPost = new("moderation.notice.removedVelvetPost", "Your Velvet post was removed");
        public static readonly LocString NoticeRemovedStory = new("moderation.notice.removedStory", "Your story was removed");
        public static readonly LocString NoticeRemovedComment = new("moderation.notice.removedComment", "Your comment was removed");
        public static readonly LocString NoticeRemovedAd = new("moderation.notice.removedAd", "Your ad was removed");
        public static readonly LocString NoticeRemovedMuster = new("moderation.notice.removedMuster", "Your muster was removed");
        public static readonly LocString NoticeRemovedMessage = new("moderation.notice.removedMessage", "A message you sent was removed");
        public static readonly LocString NoticeRemovedContent = new("moderation.notice.removedContent", "Something you posted was removed");
        public static readonly LocString NoticeAvatarRemoved = new("moderation.notice.avatarRemoved", "Your profile picture was removed");
        public static readonly LocString NoticeProfileCleared = new("moderation.notice.profileCleared", "Part of your profile was cleared");
        public static readonly LocString NoticeProfileClearedFields = new("moderation.notice.profileClearedFields", "Cleared: {0}");
        public static readonly LocString NoticeSuspendedTitle = new("moderation.notice.suspendedTitle", "Your account was suspended");
        public static readonly LocString NoticeSuspendedFor = new("moderation.notice.suspendedFor", "You cannot sign in until {0}.");
        public static readonly LocString NoticeSuspendedPermanent = new("moderation.notice.suspendedPermanent", "This suspension does not expire on its own.");
        public static readonly LocString NoticeSignedOutTitle = new("moderation.notice.signedOutTitle", "A moderator signed you out");
        public static readonly LocString NoticeSignedOutBody = new("moderation.notice.signedOutBody", "Your sessions were ended on every device. Your account is fine and nothing was removed.");
        public static readonly LocString NoticeQuoted = new("moderation.notice.quoted", "You posted: “{0}”");
        public static readonly LocString NoticeQuotedPhotos = new("moderation.notice.quotedPhotos", "{0} attached");
        public static readonly LocString NoticePhotoCount = new("moderation.notice.photoCount", "{0} photo(s)");
        public static readonly LocString NoticeModeratorNote = new("moderation.notice.moderatorNote", "From the moderator: {0}");
        public static readonly LocString NoticeWarningConsequence = new("moderation.notice.warningConsequence", "Please follow the community guidelines. Repeated breaks of the same rule can lead to a temporary suspension.");
        public static readonly LocString NoticeThanksTitle = new("moderation.notice.thanksTitle", "Thanks for looking out for everyone");
        public static readonly LocString NoticeThanksBody = new("moderation.notice.thanksBody", "Our moderators reviewed your report and it has been handled. Reports like yours are what keep Aethernet safe, and we read every single one. Thank you for taking the time.");
        public static readonly LocString NoticeBadgeTitle = new("moderation.notice.badgeTitle", "New badge");
        public static readonly LocString NoticeBadgeBodyOne = new("moderation.notice.badgeBodyOne", "The Aetherphone team granted you the {0} badge. It now shows next to your name, and you can manage it in Settings under Account.");
        public static readonly LocString NoticeBadgeBodyMany = new("moderation.notice.badgeBodyMany", "The Aetherphone team granted you new badges: {0}. They now show next to your name, and you can manage them in Settings under Account.");
        public static readonly LocString NoticeBadgeBodyFallback = new("moderation.notice.badgeBodyFallback", "The Aetherphone team granted you a new badge. You can see it in Settings under Account.");
        public static readonly LocString NoticeBadgeRevokedTitle = new("moderation.notice.badgeRevokedTitle", "Badge removed");
        public static readonly LocString NoticeBadgeRevokedBodyOne = new("moderation.notice.badgeRevokedBodyOne", "The {0} badge was removed from your account and no longer shows next to your name. If you think this was a mistake, reach out to us on our Discord server.");
        public static readonly LocString NoticeBadgeRevokedBodyMany = new("moderation.notice.badgeRevokedBodyMany", "These badges were removed from your account: {0}. They no longer show next to your name. If you think this was a mistake, reach out to us on our Discord server.");
        public static readonly LocString NoticeBadgeRevokedBodyFallback = new("moderation.notice.badgeRevokedBodyFallback", "A badge was removed from your account. If you think this was a mistake, reach out to us on our Discord server.");
    }

    internal static class Apps
    {
        public static readonly LocString Contacts = new("app.contacts", "Contacts");
        public static readonly LocString Character = new("app.character", "Character");
        public static readonly LocString Health = new("app.health", "Health");
        public static readonly LocString Housing = new("app.housing", "Housing");
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
        public static readonly LocString Announcements = new("app.announcements", "Announcements");
        public static readonly LocString Muster = new("app.muster", "Muster");
        public static readonly LocString YellowPages = new("app.yellowpages", "Yellow Pages");
        public static readonly LocString Calendar = new("app.calendar", "Calendar");
        public static readonly LocString Notes = new("app.notes", "Notes");
        public static readonly LocString Calculator = new("app.calculator", "Calculator");
        public static readonly LocString Linkpearl = new("app.linkpearl", "Linkpearl");
        public static readonly LocString Message = new("app.message", "Message");
        public static readonly LocString Jobs = new("app.jobs", "Jobs");
        public static readonly LocString AppStore = new("app.appstore", "App Store");
        public static readonly LocString Shortcuts = new("app.shortcuts", "Shortcuts");
    }

    internal static class Shortcuts
    {
        public static readonly LocString TabShortcuts = new("shortcuts.tabShortcuts", "Shortcuts");
        public static readonly LocString TabPlugins = new("shortcuts.tabPlugins", "Plugins");
        public static readonly LocString NewShortcut = new("shortcuts.newShortcut", "New Shortcut");
        public static readonly LocString EditShortcut = new("shortcuts.editShortcut", "Edit Shortcut");
        public static readonly LocString Untitled = new("shortcuts.untitled", "Untitled");
        public static readonly LocString LibraryEmpty = new("shortcuts.libraryEmpty", "No shortcuts yet");
        public static readonly LocString LibraryEmptyHint = new("shortcuts.libraryEmptyHint", "Tap + to build one, or open the Plugins tab.");
        public static readonly LocString NoSteps = new("shortcuts.noSteps", "No steps");
        public static readonly LocString MoreSteps = new("shortcuts.moreSteps", "+{0} more");
        public static readonly LocString StepOpenNamed = new("shortcuts.stepOpenNamed", "Open {0}");
        public static readonly LocString StepWaitNamed = new("shortcuts.stepWaitNamed", "Wait {0}s");
        public static readonly LocString Running = new("shortcuts.running", "Running");
        public static readonly LocString RunDone = new("shortcuts.runDone", "Ran {0}");
        public static readonly LocString RunRejected = new("shortcuts.runRejected", "The game refused that command");
        public static readonly LocString RunPluginMissing = new("shortcuts.runPluginMissing", "That plugin is not loaded");
        public static readonly LocString RunStep = new("shortcuts.runStep", "Step {0} of {1}");
        public static readonly LocString RunHolding = new("shortcuts.runHolding", "Waiting for the game");
        public static readonly LocString RunNotLoggedIn = new("shortcuts.runNotLoggedIn", "Log in to run this");
        public static readonly LocString RunGameBusy = new("shortcuts.runGameBusy", "The game stayed busy, so it stopped");
        public static readonly LocString SearchPlugins = new("shortcuts.searchPlugins", "Search plugins and commands");
        public static readonly LocString NoPluginsFound = new("shortcuts.noPluginsFound", "No plugins match that search.");
        public static readonly LocString SearchShortcuts = new("shortcuts.searchShortcuts", "Search your shortcuts");
        public static readonly LocString NoMatches = new("shortcuts.noMatches", "No shortcuts match that search.");
        public static readonly LocString PluginDisabled = new("shortcuts.pluginDisabled", "Disabled");
        public static readonly LocString PluginCommandCount = new("shortcuts.pluginCommandCount", "{0} commands");
        public static readonly LocString PluginBy = new("shortcuts.pluginBy", "by {0}");
        public static readonly LocString AddToHome = new("shortcuts.addToHome", "Add to Home");
        public static readonly LocString OpenPlugin = new("shortcuts.openPlugin", "Open");
        public static readonly LocString PluginSettings = new("shortcuts.pluginSettings", "Settings");
        public static readonly LocString Commands = new("shortcuts.commands", "Commands");
        public static readonly LocString CommandsHint = new("shortcuts.commandsHint", "Tap + on a command to start a shortcut from it.");
        public static readonly LocString NoCommands = new("shortcuts.noCommands", "This plugin registers no commands.");
        public static readonly LocString NewFromCommand = new("shortcuts.newFromCommand", "New shortcut from this");
        public static readonly LocString ChoosePlugin = new("shortcuts.choosePlugin", "Choose Plugin");
        public static readonly LocString ChooseIcon = new("shortcuts.chooseIcon", "Choose Icon");
        public static readonly LocString NameHint = new("shortcuts.nameHint", "Shortcut name");
        public static readonly LocString Appearance = new("shortcuts.appearance", "Appearance");
        public static readonly LocString Steps = new("shortcuts.steps", "Steps");
        public static readonly LocString StepsHint = new("shortcuts.stepsHint", "Commands run in order, exactly as if you typed them. Game macro waits like <wait.2> are honored.");
        public static readonly LocString AddCommand = new("shortcuts.addCommand", "Command");
        public static readonly LocString AddWait = new("shortcuts.addWait", "Wait");
        public static readonly LocString AddOpen = new("shortcuts.addOpen", "Open");
        public static readonly LocString AddLink = new("shortcuts.addLink", "Link");
        public static readonly LocString PasteMacro = new("shortcuts.pasteMacro", "Paste Macro");
        public static readonly LocString PasteEmpty = new("shortcuts.pasteEmpty", "There is no macro text on the clipboard.");
        public static readonly LocString UrlHint = new("shortcuts.urlHint", "https://example.com");
        public static readonly LocString KindOpenUrl = new("shortcuts.kindOpenUrl", "OPEN LINK");
        public static readonly LocString RunLinkRejected = new("shortcuts.runLinkRejected", "That link could not be opened");
        public static readonly LocString CommandHint = new("shortcuts.commandHint", "/emote or any command");
        public static readonly LocString WaitSeconds = new("shortcuts.waitSeconds", "{0} seconds");
        public static readonly LocString KindCommand = new("shortcuts.kindCommand", "COMMAND");
        public static readonly LocString KindWait = new("shortcuts.kindWait", "WAIT");
        public static readonly LocString KindOpenPlugin = new("shortcuts.kindOpenPlugin", "OPEN PLUGIN");
        public static readonly LocString MoveUp = new("shortcuts.moveUp", "Move up");
        public static readonly LocString MoveDown = new("shortcuts.moveDown", "Move down");
        public static readonly LocString RemoveStep = new("shortcuts.removeStep", "Remove step");
        public static readonly LocString Options = new("shortcuts.options", "Options");
        public static readonly LocString ShowOnHome = new("shortcuts.showOnHome", "Show on Home Screen");
        public static readonly LocString TestRun = new("shortcuts.testRun", "Run Now");
        public static readonly LocString Save = new("shortcuts.save", "Save");
        public static readonly LocString Edit = new("shortcuts.edit", "Edit");
        public static readonly LocString Duplicate = new("shortcuts.duplicate", "Duplicate");
        public static readonly LocString Share = new("shortcuts.share", "Share");
        public static readonly LocString Copied = new("shortcuts.copied", "Copied");
        public static readonly LocString ImportShortcut = new("shortcuts.importShortcut", "Import Shortcut");
        public static readonly LocString ImportAdd = new("shortcuts.importAdd", "Add to My Shortcuts");
        public static readonly LocString ImportWillRun = new("shortcuts.importWillRun", "This shortcut will run");
        public static readonly LocString ImportBadCode = new("shortcuts.importBadCode", "That is not an Aetherphone shortcut code.");
        public static readonly LocString ImportMalformed = new("shortcuts.importMalformed", "That shortcut code is damaged or too large.");
        public static readonly LocString ImportUnsafeLink = new("shortcuts.importUnsafeLink", "That code holds a link that is not http or https.");
        public static readonly LocString CopyName = new("shortcuts.copyName", "{0} copy");
        public static readonly LocString DeleteShortcut = new("shortcuts.deleteShortcut", "Delete Shortcut");
        public static readonly LocString DeleteConfirm = new("shortcuts.deleteConfirm", "Delete this shortcut?");
        public static readonly LocString Delete = new("shortcuts.delete", "Delete");
        public static readonly LocString KeepIt = new("shortcuts.keepIt", "Keep");
        public static readonly LocString Color = new("shortcuts.color", "Color");
        public static readonly LocString CustomColor = new("shortcuts.customColor", "Custom color");
        public static readonly LocString Symbol = new("shortcuts.symbol", "Symbol");
        public static readonly LocString PluginIcon = new("shortcuts.pluginIcon", "Plugin icon");
        public static readonly LocString PluginIconNone = new("shortcuts.pluginIconNone", "None");
        public static readonly LocString LimitReached = new("shortcuts.limitReached", "You can keep up to {0} shortcuts.");
        public static readonly LocString StepLimitReached = new("shortcuts.stepLimitReached", "A shortcut can hold up to {0} steps.");
        public static readonly LocString Ok = new("shortcuts.ok", "OK");
    }

    internal static class Store
    {
        public static readonly LocString Today = new("store.today", "Today");
        public static readonly LocString Apps = new("store.apps", "Apps");
        public static readonly LocString Search = new("store.search", "Search");
        public static readonly LocString SearchHint = new("store.searchHint", "Apps and features");
        public static readonly LocString BrowseCategories = new("store.browseCategories", "Browse Categories");
        public static readonly LocString Get = new("store.get", "GET");
        public static readonly LocString Open = new("store.open", "OPEN");
        public static readonly LocString Remove = new("store.remove", "Remove");
        public static readonly LocString Installing = new("store.installing", "Installing");
        public static readonly LocString OnHome = new("store.onHome", "On your Home Screen");
        public static readonly LocString NotInstalled = new("store.notInstalled", "Not installed");
        public static readonly LocString AppOfTheDay = new("store.appOfTheDay", "APP OF THE DAY");
        public static readonly LocString NewHere = new("store.newHere", "NEW TO YOUR PHONE");
        public static readonly LocString EverythingInstalled =
            new("store.everythingInstalled", "Everything is on your Home Screen");
        public static readonly LocString EverythingInstalledHint =
            new("store.everythingInstalledHint", "Remove an app and it comes back here");
        public static readonly LocString NoResults = new("store.noResults", "No apps match that");
        public static readonly LocString Information = new("store.information", "Information");
        public static readonly LocString Preview = new("store.preview", "Preview");
        public static readonly LocString Description = new("store.description", "About");
        public static readonly LocString Developer = new("store.developer", "Developer");
        public static readonly LocString DeveloperName = new("store.developerName", "Aetherphone");
        public static readonly LocString Category = new("store.category", "Category");
        public static readonly LocString Languages = new("store.languages", "Languages");
        public static readonly LocString LanguageCount = new("store.languageCount", "{0} languages");
        public static readonly LocString Unavailable = new("store.unavailable", "Not available right now");
        public static readonly LocString CategorySocial = new("store.categorySocial", "Social");
        public static readonly LocString CategoryChat = new("store.categoryChat", "Communication");
        public static readonly LocString CategoryCreativity = new("store.categoryCreativity", "Photo & Video");
        public static readonly LocString CategoryPlay = new("store.categoryPlay", "Entertainment");
        public static readonly LocString CategoryAdventure = new("store.categoryAdventure", "Adventuring");
        public static readonly LocString CategoryWork = new("store.categoryWork", "Productivity");
        public static readonly LocString CategoryTools = new("store.categoryTools", "Utilities");
    }

    internal static class StoreCopy
    {
        public static readonly LocString ChirperSub = new("storeCopy.chirperSub", "Short posts, whole realm");
        public static readonly LocString ChirperBody = new("storeCopy.chirperBody",
            "Follow adventurers across every world, post what you are up to, and catch the timeline between duties.");
        public static readonly LocString AethergramSub = new("storeCopy.aethergramSub", "Your screenshots, shared");
        public static readonly LocString AethergramBody = new("storeCopy.aethergramBody",
            "Post your best shots, build a grid worth scrolling, and see what everyone else is capturing.");
        public static readonly LocString VelvetSub = new("storeCopy.velvetSub", "After dark, adults only");
        public static readonly LocString VelvetBody = new("storeCopy.velvetBody",
            "An 18+ space for connections, collaborative writing and private messages, kept well apart from the rest of your phone.");
        public static readonly LocString PollsSub = new("storeCopy.pollsSub", "Ask the whole server");
        public static readonly LocString PollsBody = new("storeCopy.pollsBody",
            "Put a question to Eorzea and watch the votes land while you wait.");
        public static readonly LocString AnnouncementsSub = new("storeCopy.announcementsSub", "Word from the team");
        public static readonly LocString AnnouncementsBody = new("storeCopy.announcementsBody",
            "Release notes, downtime warnings and everything else the Aetherphone team wants you to know, delivered straight to your phone.");
        public static readonly LocString VenuesSub = new("storeCopy.venuesSub", "Nightlife, mapped");
        public static readonly LocString VenuesBody = new("storeCopy.venuesBody",
            "Find player-run venues, opening hours and directions without leaving the game.");
        public static readonly LocString MusterSub = new("storeCopy.musterSub", "Call a meetup, see who's coming");
        public static readonly LocString MusterBody = new("storeCopy.musterBody",
            "Announce a spontaneous meetup with a real map location and watch the RSVPs land with one tap. Your friends always see your musters, everyone else can find the public ones, and it all disappears when the muster ends.");
        public static readonly LocString YellowPagesSub = new("storeCopy.yellowPagesSub", "Eorzea's classifieds board");
        public static readonly LocString YellowPagesBody = new("storeCopy.yellowPagesBody",
            "Post an ad once and reach your whole region: venue nights with schedules and an Open Now switch, gil services with prices up front, and recruitment calls for free companies, statics, and venue staff. Ads expire on their own, so the board never goes stale.");
        public static readonly LocString LinkpearlSub = new("storeCopy.linkpearlSub", "Every channel, one app");
        public static readonly LocString LinkpearlBody = new("storeCopy.linkpearlBody",
            "Linkshells, tells and free company chat gathered into one readable place, with mute controls and search.");
        public static readonly LocString MessageSub = new("storeCopy.messageSub", "Calls and chats");
        public static readonly LocString MessageBody = new("storeCopy.messageBody",
            "Message and call the people you have swapped numbers with. Photos, voice notes and group chats included.");
        public static readonly LocString CameraSub = new("storeCopy.cameraSub", "Snap the moment");
        public static readonly LocString CameraBody = new("storeCopy.cameraBody",
            "Take a shot straight from the phone and drop it into your gallery.");
        public static readonly LocString PhotosSub = new("storeCopy.photosSub", "Your gallery");
        public static readonly LocString PhotosBody = new("storeCopy.photosBody",
            "Browse everything you have captured by day or album, and open any shot full screen.");
        public static readonly LocString MusicSub = new("storeCopy.musicSub", "Radio for the realm");
        public static readonly LocString MusicBody = new("storeCopy.musicBody",
            "Stream stations from around the world or queue up songs, with a mini player that follows you.");
        public static readonly LocString GamesSub = new("storeCopy.gamesSub", "Pocket distractions");
        public static readonly LocString GamesBody = new("storeCopy.gamesBody",
            "A small arcade for queue times, with high scores worth chasing.");
        public static readonly LocString NewsSub = new("storeCopy.newsSub", "Patch notes and posts");
        public static readonly LocString NewsBody = new("storeCopy.newsBody",
            "The Lodestone straight to your phone: maintenance, patch notes and announcements.");
        public static readonly LocString FishingSub = new("storeCopy.fishingSub", "Routes and windows");
        public static readonly LocString FishingBody = new("storeCopy.fishingBody",
            "Track ocean fishing rotations, baits and the windows worth waiting for.");
        public static readonly LocString SkywatcherSub = new("storeCopy.skywatcherSub", "Weather ahead");
        public static readonly LocString SkywatcherBody = new("storeCopy.skywatcherBody",
            "See the forecast for any zone and plan around the weather you actually need.");
        public static readonly LocString MapsSub = new("storeCopy.mapsSub", "Find your way");
        public static readonly LocString MapsBody = new("storeCopy.mapsBody",
            "Every zone map with aetherytes and markers, in your pocket.");
        public static readonly LocString CollectionsSub = new("storeCopy.collectionsSub", "Everything you own");
        public static readonly LocString CollectionsBody = new("storeCopy.collectionsBody",
            "Mounts, minions, orchestrion rolls and more, with whatever is still missing.");
        public static readonly LocString InventorySub = new("storeCopy.inventorySub", "Bags at a glance");
        public static readonly LocString InventoryBody = new("storeCopy.inventoryBody",
            "Search every bag, retainer and saddlebag without opening a single window.");
        public static readonly LocString JobsSub = new("storeCopy.jobsSub", "Levels and gear");
        public static readonly LocString JobsBody = new("storeCopy.jobsBody",
            "Every class and job with levels, gear and progress in one place.");
        public static readonly LocString CharacterSub = new("storeCopy.characterSub", "Your day, tracked");
        public static readonly LocString CharacterBody = new("storeCopy.characterBody",
            "Rings, streaks and history for the things you do every day.");
        public static readonly LocString HealthSub = new("storeCopy.healthSub", "Your adventurer's activity");
        public static readonly LocString HealthBody = new("storeCopy.healthBody",
            "Estimated steps, distance, swimming, hydration and personal goals for your character. A fictional activity tracker for roleplay and statistics.");
        public static readonly LocString HousingSub = new("storeCopy.housingSub", "Plots on a map");
        public static readonly LocString HousingBody = new("storeCopy.housingBody",
            "Browse reported openings ward by ward, watch a plot and get reminded before the lottery closes.");
        public static readonly LocString WalletSub = new("storeCopy.walletSub", "Gil and currencies");
        public static readonly LocString WalletBody = new("storeCopy.walletBody",
            "Every currency you carry, with caps and totals you can actually read.");
        public static readonly LocString MarketSub = new("storeCopy.marketSub", "Prices, live");
        public static readonly LocString MarketBody = new("storeCopy.marketBody",
            "Universalis prices for any item, with alerts when something drops.");
        public static readonly LocString DailiesSub = new("storeCopy.dailiesSub", "Never miss a reset");
        public static readonly LocString DailiesBody = new("storeCopy.dailiesBody",
            "Daily and weekly duties, what is done, and when the next reset lands.");
        public static readonly LocString NotesSub = new("storeCopy.notesSub", "Quick thoughts");
        public static readonly LocString NotesBody = new("storeCopy.notesBody",
            "Jot down macros, rotations and reminders, and find them again later.");
        public static readonly LocString CalendarSub = new("storeCopy.calendarSub", "Plan the week");
        public static readonly LocString CalendarBody = new("storeCopy.calendarBody",
            "Events, reminders and reset days on one calendar.");
        public static readonly LocString TimersSub = new("storeCopy.timersSub", "Count it down");
        public static readonly LocString TimersBody = new("storeCopy.timersBody",
            "Timers for crafting, cooldowns and anything you cannot afford to miss.");
        public static readonly LocString ClockSub = new("storeCopy.clockSub", "Eorzea and local");
        public static readonly LocString ClockBody = new("storeCopy.clockBody",
            "World clock, alarms and Eorzea time side by side.");
        public static readonly LocString ShortcutsSub = new("storeCopy.shortcutsSub", "One tap, many commands");
        public static readonly LocString ShortcutsBody = new("storeCopy.shortcutsBody",
            "Turn any run of commands into a home screen icon, and pin your other plugins next to them.");
        public static readonly LocString CalculatorSub = new("storeCopy.calculatorSub", "Numbers, fast");
        public static readonly LocString CalculatorBody = new("storeCopy.calculatorBody",
            "A calculator that stays out of your way.");
        public static readonly LocString SettingsSub = new("storeCopy.settingsSub", "Make it yours");
        public static readonly LocString SettingsBody = new("storeCopy.settingsBody",
            "Wallpapers, themes, sounds, language and everything else about the phone.");
        public static readonly LocString NotificationsSub = new("storeCopy.notificationsSub", "Everything you missed");
        public static readonly LocString NotificationsBody = new("storeCopy.notificationsBody",
            "One place for every alert your phone has raised.");
        public static readonly LocString FeedbackSub = new("storeCopy.feedbackSub", "Tell us what broke");
        public static readonly LocString FeedbackBody = new("storeCopy.feedbackBody",
            "Send a bug report or an idea, with screenshots attached.");
        public static readonly LocString StoreSub = new("storeCopy.storeSub", "Apps for your phone");
        public static readonly LocString StoreBody = new("storeCopy.storeBody",
            "Browse everything the phone can do and put it on your Home Screen.");
    }

    internal static class DirectMessages
    {
        public static readonly LocString Empty = new("dm.empty", "No conversations yet");
        public static readonly LocString EmptyHint = new("dm.emptyHint", "Message a friend from your friend list");
        public static readonly LocString SignInPrompt = new("dm.signInPrompt", "Sign in to message your friends");
        public static readonly LocString NewMessage = new("dm.newMessage", "New message");
        public static readonly LocString GroupFallback = new("dm.groupFallback", "Group");
        public static readonly LocString PhotoPreview = new("dm.photoPreview", "Photo");
        public static readonly LocString VoicePreview = new("dm.voicePreview", "Voice message");
        public static readonly LocString PostPreview = new("dm.postPreview", "Post");
        public static readonly LocString StoryReplyPreview = new("dm.storyReplyPreview", "Story reply");
        public static readonly LocString NoMutualTitle = new("dm.noMutualTitle", "No mutual friends yet");
        public static readonly LocString NoMutualFriends = new("dm.noMutualFriends", "Share numbers in-game to start messaging.");
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
        public static readonly LocString RenameHint = new("dm.renameHint", "Group name");
        public static readonly LocString Save = new("dm.save", "Save");
        public static readonly LocString Owner = new("dm.owner", "Owner");
        public static readonly LocString SysCreated = new("dm.sysCreated", "{0} started the group");
        public static readonly LocString SysAdded = new("dm.sysAdded", "{0} added {1}");
        public static readonly LocString SysRemoved = new("dm.sysRemoved", "{0} removed {1}");
        public static readonly LocString SysLeft = new("dm.sysLeft", "{0} left");
        public static readonly LocString SysRenamed = new("dm.sysRenamed", "{0} renamed the chat to {1}");
        public static readonly LocString LocationPreview = new("dm.locationPreview", "Location");
        public static readonly LocString LocationShared = new("dm.locationShared", "Shared location");
        public static readonly LocString LocationOpenMap = new("dm.locationOpenMap", "Open map");
        public static readonly LocString LocationWard = new("dm.locationWard", "Ward {0}");
        public static readonly LocString LocationPlot = new("dm.locationPlot", "Plot {0}");
        public static readonly LocString LocationRoom = new("dm.locationRoom", "Room {0}");
    }

    internal static class Message
    {
        public static readonly LocString ThreadEmpty = new("message.threadEmpty", "Say hello");
        public static readonly LocString TabChats = new("message.tabChats", "Chats");
        public static readonly LocString Archived = new("message.archived", "Archived");
        public static readonly LocString Archive = new("message.archive", "Archive");
        public static readonly LocString Unarchive = new("message.unarchive", "Unarchive");
        public static readonly LocString NoArchived = new("message.noArchived", "No archived chats");
        public static readonly LocString FilterDirect = new("message.filterDirect", "Direct");
        public static readonly LocString FilterGroups = new("message.filterGroups", "Groups");
        public static readonly LocString Favorites = new("message.favorites", "Favorites");
        public static readonly LocString AddFavorite = new("message.addFavorite", "Add to favorites");
        public static readonly LocString RemoveFavorite = new("message.removeFavorite", "Remove from favorites");
        public static readonly LocString Notes = new("message.notes", "Notes");
        public static readonly LocString NotesHint = new("message.notesHint", "Add a private note about this contact");
        public static readonly LocString Number = new("message.number", "Number");
        public static readonly LocString Handle = new("message.handle", "Handle");
        public static readonly LocString LocalTime = new("message.localTime", "Local time");
        public static readonly LocString Added = new("message.added", "Added");
        public static readonly LocString SignInPrompt = new("message.signInPrompt", "Sign in to Aethernet in Settings to use Message");
        public static readonly LocString NoContacts = new("message.noContacts", "Add friends by number in the Contacts tab to call them");
        public static readonly LocString ReplyAction = new("message.replyAction", "Reply");
        public static readonly LocString InfoAction = new("message.infoAction", "Info");
        public static readonly LocString InfoTitle = new("message.infoTitle", "Message info");
        public static readonly LocString You = new("message.you", "You");
        public static readonly LocString ReplyingTo = new("message.replyingTo", "Replying to {0}");
        public static readonly LocString OriginalUnavailable = new("message.originalUnavailable", "Original message unavailable");
        public static readonly LocString ReadSection = new("message.readSection", "Read");
        public static readonly LocString SentSection = new("message.sentSection", "Sent");
        public static readonly LocString ReadBy = new("message.readBy", "Read by");
        public static readonly LocString SentTo = new("message.sentTo", "Sent to");
        public static readonly LocString NotReadYet = new("message.notReadYet", "Not yet");
        public static readonly LocString ForwardAction = new("message.forwardAction", "Forward");
        public static readonly LocString ForwardTitle = new("message.forwardTitle", "Forward to");
        public static readonly LocString ForwardedLabel = new("message.forwardedLabel", "Forwarded");
        public static readonly LocString DeleteAction = new("message.deleteAction", "Delete for everyone");
        public static readonly LocString DeleteConfirm = new("message.deleteConfirm", "Delete this message for everyone in this chat?");
        public static readonly LocString DeletedBody = new("message.deletedBody", "This message was deleted");
        public static readonly LocString MuteAction = new("message.muteAction", "Mute");
        public static readonly LocString UnmuteAction = new("message.unmuteAction", "Unmute");
        public static readonly LocString RecordVoiceHint = new("message.recordVoiceHint", "Record a voice message");
        public static readonly LocString ShareLocation = new("message.shareLocation", "Share location");
        public static readonly LocString ShareLocationConfirm = new("message.shareLocationConfirm", "Send your current location?");
        public static readonly LocString LocationUnavailable = new("message.locationUnavailable", "Your location could not be read.");
        public static readonly LocString LocationSendFailed = new("message.locationSendFailed", "Could not send your location.");
        public static readonly LocString EditAction = new("message.editAction", "Edit");
        public static readonly LocString EditingLabel = new("message.editingLabel", "Editing message");
        public static readonly LocString EditedAt = new("message.editedAt", "edited {0}");
        public static readonly LocString StarAction = new("message.starAction", "Star");
        public static readonly LocString UnstarAction = new("message.unstarAction", "Unstar");
        public static readonly LocString StarredTitle = new("message.starredTitle", "Starred messages");
        public static readonly LocString NoStarred = new("message.noStarred", "No starred messages yet");
        public static readonly LocString ReactionsTitle = new("message.reactionsTitle", "Reactions");
        public static readonly LocString ReactionAdd = new("message.reactionAdd", "React with this");
        public static readonly LocString HostingMuster = new("message.hostingMuster", "Hosting a meetup, open it");
        public static readonly LocString ReactionRemove = new("message.reactionRemove", "Remove your reaction");
        public static readonly LocString TapToRemove = new("message.tapToRemove", "Click to remove");
        public static readonly LocString DraftPrefix = new("message.draftPrefix", "Draft:");
        public static readonly LocString PresenceOnline = new("message.presenceOnline", "online");
        public static readonly LocString PresenceLastSeen = new("message.presenceLastSeen", "last seen {0}");
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
        public static readonly LocString CollectionNotTracked = new("collections.collectionNotTracked", "This collection can't be tracked from the Lodestone.");
        public static readonly LocString OwnedUnavailable = new("collections.ownedUnavailable", "Couldn't load your owned items right now.");
        public static readonly LocString Failed = new("collections.failed", "Couldn't reach FFXIV Collect.");
        public static readonly LocString TryAgain = new("collections.tryAgain", "Try again");
        public static readonly LocString NoResults = new("collections.noResults", "No items match your filters.");
        public static readonly LocString CompletePercent = new("collections.completePercent", "{0}% complete");
    }

    internal static class Muster
    {
        public static readonly LocString NotifStartedTitle = new("muster.notifStartedTitle", "Muster called");
        public static readonly LocString NotifStartedBody = new("muster.notifStartedBody", "{0} called a muster");
        public static readonly LocString NotifRsvpTitle = new("muster.notifRsvpTitle", "On their way");
        public static readonly LocString NotifRsvpBody = new("muster.notifRsvpBody", "{0} is on their way to your muster");
        public static readonly LocString NotifEndedTitle = new("muster.notifEndedTitle", "Muster called off");
        public static readonly LocString NotifEndedBody = new("muster.notifEndedBody", "A muster you joined was called off early");
        public static readonly LocString SetUpAccount = new("muster.setUpAccount", "Set up your account in Settings");
        public static readonly LocString CategorySocial = new("muster.categorySocial", "Social");
        public static readonly LocString CategoryRoleplay = new("muster.categoryRoleplay", "Roleplay");
        public static readonly LocString CategoryPve = new("muster.categoryPve", "PvE");
        public static readonly LocString CategoryPvp = new("muster.categoryPvp", "PvP");
        public static readonly LocString CategoryHuntTrain = new("muster.categoryHuntTrain", "Hunt train");
        public static readonly LocString CategoryTreasureHunt = new("muster.categoryTreasureHunt", "Treasure hunt");
        public static readonly LocString CategoryDeepDungeon = new("muster.categoryDeepDungeon", "Deep dungeon");
        public static readonly LocString CategoryFishing = new("muster.categoryFishing", "Fishing");
        public static readonly LocString CategoryGoldSaucer = new("muster.categoryGoldSaucer", "Gold Saucer");
        public static readonly LocString CategoryGpose = new("muster.categoryGpose", "Gpose");
        public static readonly LocString CategoryFates = new("muster.categoryFates", "FATEs");
        public static readonly LocString CategoryOther = new("muster.categoryOther", "Other");
        public static readonly LocString RegionNa = new("muster.regionNa", "NA");
        public static readonly LocString RegionEu = new("muster.regionEu", "EU");
        public static readonly LocString RegionJp = new("muster.regionJp", "JP");
        public static readonly LocString RegionOce = new("muster.regionOce", "OCE");
        public static readonly LocString YourMuster = new("muster.yourMuster", "Your muster");
        public static readonly LocString StartMuster = new("muster.startMuster", "Start a muster");
        public static readonly LocString FriendsSection = new("muster.friendsSection", "Friends");
        public static readonly LocString HappeningNow = new("muster.happeningNow", "Happening now");
        public static readonly LocString StartingSoon = new("muster.startingSoon", "Starting soon");
        public static readonly LocString LoadMore = new("muster.loadMore", "Load more");
        public static readonly LocString EmptyTitle = new("muster.emptyTitle", "No musters right now");
        public static readonly LocString EmptyHint = new("muster.emptyHint", "Start one and let people know where to find you");
        public static readonly LocString StartsIn = new("muster.startsIn", "in {0}");
        public static readonly LocString EndsIn = new("muster.endsIn", "ends in {0}");
        public static readonly LocString StartsAt = new("muster.startsAt", "starts at {0}");
        public static readonly LocString RunsFor = new("muster.runsFor", "runs for {0}");
        public static readonly LocString DurationHoursMinutes = new("muster.durationHoursMinutes", "{0}h {1}m");
        public static readonly LocString DurationHours = new("muster.durationHours", "{0}h");
        public static readonly LocString DurationMinutes = new("muster.durationMinutes", "{0}m");
        public static readonly LocString GoingCount = new("muster.goingCount", "{0} going");
        public static readonly LocString AtCapacity = new("muster.atCapacity", "at capacity");
        public static readonly LocString UnavailableTitle = new("muster.unavailableTitle", "Nothing to see here");
        public static readonly LocString UnavailableHint = new("muster.unavailableHint", "This muster has ended or is unavailable");
        public static readonly LocString FlagOnMap = new("muster.flagOnMap", "Flag on map");
        public static readonly LocString CopyDetails = new("muster.copyDetails", "Copy details");
        public static readonly LocString Copied = new("muster.copied", "Copied");
        public static readonly LocString OnMyWay = new("muster.onMyWay", "On my way");
        public static readonly LocString CantMakeIt = new("muster.cantMakeIt", "Can't make it");
        public static readonly LocString ManageAction = new("muster.manageAction", "Manage");
        public static readonly LocString ReportTitle = new("muster.reportTitle", "Report muster");
        public static readonly LocString WhereSection = new("muster.whereSection", "Where");
        public static readonly LocString NewMuster = new("muster.newMuster", "New muster");
        public static readonly LocString CategorySection = new("muster.categorySection", "Category");
        public static readonly LocString DescriptionLabel = new("muster.descriptionLabel", "What's happening");
        public static readonly LocString UseMyLocation = new("muster.useMyLocation", "Use my location");
        public static readonly LocString ClearLocation = new("muster.clearLocation", "Clear");
        public static readonly LocString MeetingSpot = new("muster.meetingSpot", "Meeting spot");
        public static readonly LocString WhenSection = new("muster.whenSection", "When");
        public static readonly LocString StartLabel = new("muster.startLabel", "Starts");
        public static readonly LocString DurationLabel = new("muster.durationLabel", "For");
        public static readonly LocString Now = new("muster.now", "Now");
        public static readonly LocString WhoSection = new("muster.whoSection", "Who");
        public static readonly LocString LimitAttendance = new("muster.limitAttendance", "Limit attendance");
        public static readonly LocString UnlistWhenFull = new("muster.unlistWhenFull", "Hide from directory when full");
        public static readonly LocString ListPublicly = new("muster.listPublicly", "List publicly");
        public static readonly LocString PublicHint = new("muster.publicHint", "Your contacts always see your muster, even when it is not listed publicly.");
        public static readonly LocString CallIt = new("muster.callIt", "Call the muster");
        public static readonly LocString NeedWhere = new("muster.needWhere", "Add your location or name a meeting spot");
        public static readonly LocString NeedDescription = new("muster.needDescription", "Say what you are mustering for");
        public static readonly LocString ErrorAlreadyHosting = new("muster.errorAlreadyHosting", "You are already hosting a muster");
        public static readonly LocString ErrorInvalid = new("muster.errorInvalid", "Check the details and try again");
        public static readonly LocString ErrorRateLimited = new("muster.errorRateLimited", "Too many musters in a row, try again later");
        public static readonly LocString ErrorFailed = new("muster.errorFailed", "Couldn't start the muster");
        public static readonly LocString AttendeesSection = new("muster.attendeesSection", "Who's coming");
        public static readonly LocString NoAttendees = new("muster.noAttendees", "No one yet, give it a moment");
        public static readonly LocString CapacityLine = new("muster.capacityLine", "{0} of {1} spots filled");
        public static readonly LocString ListedPublicly = new("muster.listedPublicly", "Listed publicly");
        public static readonly LocString ListedPrivately = new("muster.listedPrivately", "Contacts only");
        public static readonly LocString CopyInvite = new("muster.copyInvite", "Copy invite");
        public static readonly LocString EndMuster = new("muster.endMuster", "End muster");
        public static readonly LocString EndConfirm = new("muster.endConfirm", "End this muster? It disappears for everyone right away.");
        public static readonly LocString EndFailed = new("muster.endFailed", "Couldn't end the muster");
        public static readonly LocString Ending = new("muster.ending", "Ending…");
        public static readonly LocString NotifNoticeTitle = new("muster.notifNoticeTitle", "Muster update");
        public static readonly LocString NotifNoticeStarting = new("muster.notifNoticeStarting", "{0}: starting now, come on over");
        public static readonly LocString NotifNoticeMoved = new("muster.notifNoticeMoved", "{0} moved the meeting spot");
        public static readonly LocString NotifNoticeWrapping = new("muster.notifNoticeWrapping", "{0} is wrapping up");
        public static readonly LocString InvitePreview = new("muster.invitePreview", "Muster invite");
        public static readonly LocString InviteUnavailable = new("muster.inviteUnavailable", "This muster has ended");
        public static readonly LocString InviteOpen = new("muster.inviteOpen", "View muster");
        public static readonly LocString ScopeMyDc = new("muster.scopeMyDc", "My DC");
        public static readonly LocString ScopeRegion = new("muster.scopeRegion", "Region");
        public static readonly LocString ScopeEverywhere = new("muster.scopeEverywhere", "Everywhere");
        public static readonly LocString Filters = new("muster.filters", "Filters");
        public static readonly LocString ClearFilters = new("muster.clearFilters", "Clear");
        public static readonly LocString Done = new("muster.done", "Done");
        public static readonly LocString GoingSection = new("muster.goingSection", "You're going");
        public static readonly LocString DcTravel = new("muster.dcTravel", "DC travel");
        public static readonly LocString Travel = new("muster.travel", "Travel");
        public static readonly LocString NoticesSection = new("muster.noticesSection", "Notices");
        public static readonly LocString NoticeStartingNow = new("muster.noticeStartingNow", "Starting now");
        public static readonly LocString NoticeMovedSpots = new("muster.noticeMovedSpots", "Moved spots");
        public static readonly LocString NoticeWrappingUp = new("muster.noticeWrappingUp", "Wrapping up");
        public static readonly LocString NoticeAgo = new("muster.noticeAgo", "{0} ago");
        public static readonly LocString StatusRunningLate = new("muster.statusRunningLate", "Running late");
        public static readonly LocString StatusHere = new("muster.statusHere", "I'm here");
        public static readonly LocString StatusWhereExactly = new("muster.statusWhereExactly", "Where exactly?");
        public static readonly LocString InviteToParty = new("muster.inviteToParty", "Invite to party");
        public static readonly LocString Invited = new("muster.invited", "Invited");
        public static readonly LocString DifferentDataCenter = new("muster.differentDataCenter", "Different data center");
        public static readonly LocString NeedDataCenter = new("muster.needDataCenter", "Could not detect your data center");
        public static readonly LocString DataCenterSection = new("muster.dataCenterSection", "Data center");
        public static readonly LocString FilterAll = new("muster.filterAll", "All");
        public static readonly LocString MyDataCenter = new("muster.myDataCenter", "My data center");
        public static readonly LocString DataCenterHint = new("muster.dataCenterHint", "Pick which data center the directory reads from.");
        public static readonly LocString TravelTo = new("muster.travelTo", "Travel to {0}");
        public static readonly LocString OnThisWorld = new("muster.onThisWorld", "You're on {0}");
        public static readonly LocString TeleportTo = new("muster.teleportTo", "Teleport to {0}");
        public static readonly LocString TravelBusy = new("muster.travelBusy", "Lifestream is busy right now");
        public static readonly LocString TravelNotAttuned = new("muster.travelNotAttuned", "You haven't attuned to {0} yet");
        public static readonly LocString TravelBlocked = new("muster.travelBlocked", "You can't teleport right now");
        public static readonly LocString TravelNoWorld = new("muster.travelNoWorld", "You can't travel to {0} from here");
        public static readonly LocString YoureHere = new("muster.youreHere", "You're already here");
        public static readonly LocString ImGoing = new("muster.imGoing", "I'm going");
        public static readonly LocString StatGoing = new("muster.statGoing", "Going");
        public static readonly LocString StatEndsIn = new("muster.statEndsIn", "Ends in");
        public static readonly LocString StatStartsIn = new("muster.statStartsIn", "Starts in");
        public static readonly LocString StatSpots = new("muster.statSpots", "Spots left");
        public static readonly LocString YourStatus = new("muster.yourStatus", "Your status");
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

    internal static class Housing
    {
        // Chrome and navigation
        public static readonly LocString Map = new("housing.map", "Map");
        public static readonly LocString List = new("housing.list", "List");
        public static readonly LocString Watchlist = new("housing.watchlist", "Watchlist");
        public static readonly LocString Settings = new("housing.settings", "Housing Settings");
        public static readonly LocString Details = new("housing.details", "Plot Details");
        public static readonly LocString ChooseWorld = new("housing.chooseWorld", "Choose World");
        public static readonly LocString ChooseWard = new("housing.chooseWard", "Choose Another Ward");
        public static readonly LocString ViewAsList = new("housing.viewAsList", "View as List");
        public static readonly LocString BackToMap = new("housing.backToMap", "Back to Map");
        public static readonly LocString WorldLabel = new("housing.worldLabel", "World");
        public static readonly LocString DistrictLabel = new("housing.districtLabel", "District");
        public static readonly LocString DistrictMist = new("housing.districtMist", "Mist");
        public static readonly LocString DistrictMistShort = new("housing.districtMistShort", "Mist");
        public static readonly LocString DistrictLavenderBeds =
            new("housing.districtLavenderBeds", "The Lavender Beds");
        public static readonly LocString DistrictLavenderBedsShort =
            new("housing.districtLavenderBedsShort", "Lavender");
        public static readonly LocString DistrictGoblet = new("housing.districtGoblet", "The Goblet");
        public static readonly LocString DistrictGobletShort = new("housing.districtGobletShort", "Goblet");
        public static readonly LocString DistrictShirogane = new("housing.districtShirogane", "Shirogane");
        public static readonly LocString DistrictShiroganeShort =
            new("housing.districtShiroganeShort", "Shirogane");
        public static readonly LocString DistrictEmpyreum = new("housing.districtEmpyreum", "Empyreum");
        public static readonly LocString DistrictEmpyreumShort = new("housing.districtEmpyreumShort", "Empyreum");
        public static readonly LocString WardLabel = new("housing.wardLabel", "Ward");
        public static readonly LocString WardNumber = new("housing.wardNumber", "Ward {0}");
        public static readonly LocString PlotNumber = new("housing.plotNumber", "Plot {0}");
        public static readonly LocString PlotTitle = new("housing.plotTitle", "Plot {0} ({1})");
        public static readonly LocString PlaceLine = new("housing.placeLine", "{0}, Ward {1}");
        public static readonly LocString SelectWorldTitle = new("housing.selectWorldTitle", "Select a world");
        public static readonly LocString SearchWorlds = new("housing.searchWorlds", "Search worlds");
        public static readonly LocString NoWorldMatches = new("housing.noWorldMatches", "No worlds match that");
        public static readonly LocString HomeWorld = new("housing.homeWorld", "Home world");

        // Plot facts
        public static readonly LocString SizeSmall = new("housing.sizeSmall", "Small");
        public static readonly LocString SizeMedium = new("housing.sizeMedium", "Medium");
        public static readonly LocString SizeLarge = new("housing.sizeLarge", "Large");
        public static readonly LocString SizeUnknown = new("housing.sizeUnknown", "Unknown size");
        public static readonly LocString PhaseEntry = new("housing.phaseEntry", "Entry period");
        public static readonly LocString PhaseResults = new("housing.phaseResults", "Results period");
        public static readonly LocString PhaseUnavailable = new("housing.phaseUnavailable", "Unavailable period");
        public static readonly LocString PhaseUnknown = new("housing.phaseUnknown", "Phase unknown");
        public static readonly LocString PhaseExpired = new("housing.phaseExpired", "Expired, refreshing");
        public static readonly LocString EligibilityPrivate = new("housing.eligibilityPrivate", "Private buyers");
        public static readonly LocString EligibilityFreeCompany =
            new("housing.eligibilityFreeCompany", "Free Companies");
        public static readonly LocString EligibilityBoth = new("housing.eligibilityBoth", "Private & Free Company");
        public static readonly LocString ModeLottery = new("housing.modeLottery", "Lottery");
        public static readonly LocString ModeFcfs = new("housing.modeFcfs", "First come, first served");
        public static readonly LocString MainDivision = new("housing.mainDivision", "Main division");
        public static readonly LocString Subdivision = new("housing.subdivision", "Subdivision");
        public static readonly LocString NotReported = new("housing.notReported", "Not reported");
        public static readonly LocString TimeUnknown = new("housing.timeUnknown", "Unknown");
        public static readonly LocString PriceGil = new("housing.priceGil", "{0} gil");
        public static readonly LocString EntriesLabel = new("housing.entriesLabel", "Reported entries");
        public static readonly LocString PriceLabel = new("housing.priceLabel", "Price");
        public static readonly LocString EligibilityLabel = new("housing.eligibilityLabel", "Eligibility");
        public static readonly LocString PurchaseLabel = new("housing.purchaseLabel", "Purchase");
        public static readonly LocString DivisionLabel = new("housing.divisionLabel", "Division");
        public static readonly LocString OddsApproximate =
            new("housing.oddsApproximate", "Approximate odds: 1 in {0}");
        public static readonly LocString EntriesCaveat = new("housing.entriesCaveat",
            "Entry counts reflect the most recently reported scan and may have changed.");
        public static readonly LocString RegionLabel = new("housing.regionLabel", "Region");
        public static readonly LocString DataCenterLabel = new("housing.dataCenterLabel", "Data centre");
        public static readonly LocString FirstReported = new("housing.firstReported", "First reported");
        public static readonly LocString PhaseEndsLabel = new("housing.phaseEndsLabel", "Phase ends");
        public static readonly LocString ScannedLabel = new("housing.scannedLabel", "Last scanned");
        public static readonly LocString ProviderLabel = new("housing.providerLabel", "Data provider");
        public static readonly LocString ExactTime = new("housing.exactTime", "Exact time");
        public static readonly LocString StatusLabel = new("housing.statusLabel", "Status");

        // Countdowns and freshness
        public static readonly LocString CountdownDays = new("housing.countdownDays", "{0}d {1:00}h {2:00}m");
        public static readonly LocString CountdownHours = new("housing.countdownHours", "{0:00}h {1:00}m");
        public static readonly LocString CountdownMinutes = new("housing.countdownMinutes", "{0}m {1:00}s");
        public static readonly LocString CountdownUnderMinute =
            new("housing.countdownUnderMinute", "Under 1 minute");
        public static readonly LocString CountdownEnded = new("housing.countdownEnded", "Ended");
        public static readonly LocString Remaining = new("housing.remaining", "{0} remaining");
        public static readonly LocString ScannedJustNow = new("housing.scannedJustNow", "Scanned just now");
        public static readonly LocString ScannedMinutes = new("housing.scannedMinutes", "Scanned {0} minutes ago");
        public static readonly LocString ScannedHours = new("housing.scannedHours", "Scanned {0} hours ago");
        public static readonly LocString ScannedDays = new("housing.scannedDays", "Scanned {0} days ago");
        public static readonly LocString ScannedUnknown = new("housing.scannedUnknown", "Scan time unknown");
        public static readonly LocString AgeNow = new("housing.ageNow", "now");
        public static readonly LocString AgeMinutes = new("housing.ageMinutes", "{0}m");
        public static readonly LocString AgeHours = new("housing.ageHours", "{0}h");
        public static readonly LocString AgeDays = new("housing.ageDays", "{0}d");
        public static readonly LocString FreshnessLive = new("housing.freshnessLive", "Live");
        public static readonly LocString FreshnessRecent = new("housing.freshnessRecent", "Recent");
        public static readonly LocString FreshnessStale = new("housing.freshnessStale", "Stale");
        public static readonly LocString FreshnessCached = new("housing.freshnessCached", "Cached");
        public static readonly LocString FreshnessUnknown = new("housing.freshnessUnknown", "Unknown");
        public static readonly LocString UpdatedAgo = new("housing.updatedAgo", "Updated {0}");
        public static readonly LocString Updating = new("housing.updating", "Updating…");
        public static readonly LocString CachedBanner = new("housing.cachedBanner",
            "Live updates are unavailable. Showing housing data saved {0}.");
        public static readonly LocString AgeJustNow = new("housing.ageJustNow", "just now");
        public static readonly LocString AgeMinutesAgo = new("housing.ageMinutesAgo", "{0} minutes ago");
        public static readonly LocString AgeHoursAgo = new("housing.ageHoursAgo", "{0} hours ago");
        public static readonly LocString AgeDaysAgo = new("housing.ageDaysAgo", "{0} days ago");

        // Actions
        public static readonly LocString Watch = new("housing.watch", "Watch");
        public static readonly LocString Watching = new("housing.watching", "Watching");
        public static readonly LocString Unwatch = new("housing.unwatch", "Unwatch");
        public static readonly LocString RemindMe = new("housing.remindMe", "Remind Me");
        public static readonly LocString ReminderSet = new("housing.reminderSet", "Reminder Set");
        public static readonly LocString ChangeReminder = new("housing.changeReminder", "Change");
        public static readonly LocString CancelReminder = new("housing.cancelReminder", "Cancel reminder");
        public static readonly LocString DetailsAction = new("housing.detailsAction", "Details");
        public static readonly LocString ReminderPrompt =
            new("housing.reminderPrompt", "Notify me before this phase ends:");
        public static readonly LocPlural LeadMinutes = new("housing.leadMinutes", "{0} minute", "{0} minutes");
        public static readonly LocPlural LeadHours = new("housing.leadHours", "{0} hour", "{0} hours");
        public static readonly LocString ReminderConfirmed = new("housing.reminderConfirmed",
            "Reminder set for {0} before the {1} ends. {2}, plot {3}.");
        public static readonly LocString ReminderUnavailable = new("housing.reminderUnavailable",
            "No phase deadline was reported for this plot, so a reminder cannot be scheduled yet.");
        public static readonly LocString Filters = new("housing.filters", "Filters");
        public static readonly LocString FiltersCount = new("housing.filtersCount", "Filters ({0})");
        public static readonly LocString ClearFilters = new("housing.clearFilters", "Clear Filters");
        public static readonly LocString Refresh = new("housing.refresh", "Refresh");
        public static readonly LocString Retry = new("housing.retry", "Retry");
        public static readonly LocString ZoomIn = new("housing.zoomIn", "Zoom in");
        public static readonly LocString ZoomOut = new("housing.zoomOut", "Zoom out");
        public static readonly LocString ResetMap = new("housing.resetMap", "Reset map");
        public static readonly LocString Recenter = new("housing.recenter", "Centre on selected plot");
        public static readonly LocString Legend = new("housing.legend", "Legend");
        public static readonly LocString MatchingPlots = new("housing.matchingPlots", "{0} matching");

        // Filters
        public static readonly LocString FilterSizes = new("housing.filterSizes", "Plot size");
        public static readonly LocString FilterPhase = new("housing.filterPhase", "Lottery phase");
        public static readonly LocString FilterEligibility = new("housing.filterEligibility", "Who can buy");
        public static readonly LocString FilterDivision = new("housing.filterDivision", "Division");
        public static readonly LocString FilterData = new("housing.filterData", "Data");
        public static readonly LocString FilterOtherPhases = new("housing.filterOtherPhases", "Other");
        public static readonly LocString FilterFreshOnly = new("housing.filterFreshOnly", "Fresh scans only");
        public static readonly LocString FilterWatchedOnly = new("housing.filterWatchedOnly", "Watched plots only");
        public static readonly LocString FilterMaxEntries = new("housing.filterMaxEntries", "Max reported entries");
        public static readonly LocString FilterAnyEntries = new("housing.filterAnyEntries", "Any");
        public static readonly LocString ShowAvailableOnly = new("housing.showAvailableOnly", "Available only");
        public static readonly LocString ShowAllPlots = new("housing.showAllPlots", "All plots");

        // Sorting
        public static readonly LocString SortEntries = new("housing.sortEntries", "Fewest entries");
        public static readonly LocString SortScanned = new("housing.sortScanned", "Recently scanned");
        public static readonly LocString SortSize = new("housing.sortSize", "Plot size");
        public static readonly LocString SortPrice = new("housing.sortPrice", "Price");
        public static readonly LocString SortWard = new("housing.sortWard", "Ward and plot");
        public static readonly LocString SortLabel = new("housing.sortLabel", "Sort");

        // Empty, loading and error states
        public static readonly LocString LoadingFirst =
            new("housing.loadingFirst", "Checking residential listings…");
        public static readonly LocString LoadingRefresh = new("housing.loadingRefresh", "Updating housing plots…");
        public static readonly LocString NoFilterMatches =
            new("housing.noFilterMatches", "No plots match the current filters.");
        public static readonly LocString NoOpenings =
            new("housing.noOpenings", "No available plots were reported in Ward {0}.");
        public static readonly LocString NoScans =
            new("housing.noScans", "No recent housing scans are available for this ward.");
        public static readonly LocString NoScansHint = new("housing.noScansHint",
            "A missing report does not mean every plot is sold: it means nobody has walked this ward recently.");
        public static readonly LocString Offline = new("housing.offline", "Housing data could not be reached.");
        public static readonly LocString OfflineHint = new("housing.offlineHint",
            "Check your connection, or preview the demo data to explore the app offline.");
        public static readonly LocString NoWorldTitle = new("housing.noWorldTitle", "Pick a world to start");
        public static readonly LocString NoWorldHint = new("housing.noWorldHint",
            "Housing could not read your home world yet. Choose one and it becomes your preferred world.");
        public static readonly LocString WatchlistEmpty = new("housing.watchlistEmpty", "No watched plots yet");
        public static readonly LocString WatchlistEmptyHint = new("housing.watchlistEmptyHint",
            "Tap a plot on the map and choose Watch to keep an eye on it here.");
        public static readonly LocString NoLongerReported =
            new("housing.noLongerReported", "No longer reported, last seen {0}");
        public static readonly LocString LastKnown = new("housing.lastKnown", "Last known state");
        public static readonly LocString ClearWatchlist = new("housing.clearWatchlist", "Clear watchlist");
        public static readonly LocString ClearWatchlistConfirm = new("housing.clearWatchlistConfirm",
            "Remove all {0} watched plots? Their reminders are cancelled too.");
        public static readonly LocString MapHint = new("housing.mapHint",
            "Available plots appear as markers. Select a marker to view its lottery details.");
        public static readonly LocString GotIt = new("housing.gotIt", "Got it");

        // Legend
        public static readonly LocString LegendSmall = new("housing.legendSmall", "Circle: small");
        public static readonly LocString LegendMedium = new("housing.legendMedium", "Diamond: medium");
        public static readonly LocString LegendLarge = new("housing.legendLarge", "Hexagon: large");
        public static readonly LocString LegendWatched = new("housing.legendWatched", "Notch: watched");
        public static readonly LocString LegendStale = new("housing.legendStale", "Dashed ring: stale scan");
        public static readonly LocString LegendSelected = new("housing.legendSelected", "Outer ring: selected");

        // Settings
        public static readonly LocString SettingsData = new("housing.settingsData", "Data");
        public static readonly LocString SettingsWorld = new("housing.settingsWorld", "World");
        public static readonly LocString SettingsNotifications = new("housing.settingsNotifications", "Reminders");
        public static readonly LocString SettingsMap = new("housing.settingsMap", "Map");
        public static readonly LocString SettingsDiagnostics = new("housing.settingsDiagnostics", "Diagnostics");
        public static readonly LocString AutoRefresh = new("housing.autoRefresh", "Refresh automatically");
        public static readonly LocString RefreshInterval = new("housing.refreshInterval", "Refresh every");
        public static readonly LocString RefreshMinutes = new("housing.refreshMinutes", "{0} min");
        public static readonly LocString FollowCurrentWorld =
            new("housing.followCurrentWorld", "Follow the world I am visiting");
        public static readonly LocString FollowCurrentWorldHint = new("housing.followCurrentWorldHint",
            "Off by default: your preferred world stays put when you world-visit.");
        public static readonly LocString PreferredWorld = new("housing.preferredWorld", "Preferred world");
        public static readonly LocString NotifyEntry = new("housing.notifyEntry", "Entry period reminders");
        public static readonly LocString NotifyResults = new("housing.notifyResults", "Results period reminders");
        public static readonly LocString ReminderLead = new("housing.reminderLead", "Default lead time");
        public static readonly LocString FreshnessThreshold = new("housing.freshnessThreshold", "Treat scans as live for");
        public static readonly LocString ClearCache = new("housing.clearCache", "Clear saved housing data");
        public static readonly LocString MinutesSuffix = new("housing.minutesSuffix", "min");
        public static readonly LocString ReminderLeadHint = new("housing.reminderLeadHint",
            "Used when you create a reminder. You can still pick a different lead time per plot.");
        public static readonly LocString GameMapHint = new("housing.gameMapHint",
            "The district map and plot positions are read from your own game installation.");
        public static readonly LocString GameMapUnavailable =
            new("housing.gameMapUnavailable", "District map unavailable");
        public static readonly LocString GameMapUnavailableHint = new("housing.gameMapUnavailableHint",
            "Aetherphone could not read this district's map from your game files, so it has no plot positions to draw. The list shows the same plots without a map.");
        public static readonly LocString GameMapUnavailableDetail = new("housing.gameMapUnavailableDetail",
            "District map unavailable ({0}). Copy the map diagnostics below to see why.");
        public static readonly LocString CopyMapDiagnostics =
            new("housing.copyMapDiagnostics", "Copy map diagnostics");
        public static readonly LocString CopiedMapDiagnostics = new("housing.copiedMapDiagnostics",
            "Map diagnostics copied to the clipboard.");
        public static readonly LocString MapSourceLabel = new("housing.mapSourceLabel", "Map source");
        public static readonly LocString ProviderStatus = new("housing.providerStatus", "Provider");
        public static readonly LocString LastRefresh = new("housing.lastRefresh", "Last successful refresh");
        public static readonly LocString OpenPlotsReported = new("housing.openPlotsReported", "Reported openings");
        public static readonly LocString ApiEndpointLabel = new("housing.apiEndpointLabel", "Endpoint");
        public static readonly LocString ProxyCacheAge = new("housing.proxyCacheAge", "Service cache age");
        public static readonly LocString ServiceUnavailable = new("housing.serviceUnavailable",
            "The Aetherphone housing service could not be reached.");
        public static readonly LocString DataSourceNotice = new("housing.dataSourceNotice",
            "Housing reads Aetherphone's housing service, which polls and caches the public PaissaDB API once for all users rather than each client polling it. The PaissaHouse plugin is not required.");
        public static readonly LocString RefreshIntervalHint = new("housing.refreshIntervalHint",
            "Housing polls no faster than every {0} minutes, and only while this app is open. Ward data only changes when a player walks the ward, so checking more often shows you nothing new. Refresh manually any time.");

        // Notifications
        public static readonly LocString NotifyEntryTitle = new("housing.notifyEntryTitle", "Housing Reminder");
        public static readonly LocString NotifyEntryBody =
            new("housing.notifyEntryBody", "Plot {0} in {1} has {2} left in the entry period.");
        public static readonly LocString NotifyEntryDetail =
            new("housing.notifyEntryDetail", "Reported entries: {0} · {1}");
        public static readonly LocString NotifyResultsTitle = new("housing.notifyResultsTitle", "Housing Results");
        public static readonly LocString NotifyResultsBody = new("housing.notifyResultsBody",
            "The results period ends in {0}. Check the estate placard before the claim or refund window closes.");
    }

    internal static class Phone
    {
        public static readonly LocString AddToCall = new("phone.addToCall", "Add to Call");
        public static readonly LocString SignInPrompt = new("phone.signInPrompt", "Sign in to Aethernet in Settings to make calls");
        public static readonly LocString NoOneFound = new("phone.noOneFound", "No one found");
        public static readonly LocString Connecting = new("phone.connecting", "Connecting to call service…");
        public static readonly LocString UseHeadphones = new("phone.useHeadphones", "Use headphones to avoid echo");
        public static readonly LocString EnableTitle = new("phone.enableTitle", "Phone Calls");
        public static readonly LocString EnableBody = new("phone.enableBody", "Voice calls with other Aetherphone users");
        public static readonly LocString Enable = new("phone.enable", "Enable");
        public static readonly LocString StatusCalling = new("phone.statusCalling", "Calling…");
        public static readonly LocString StatusConnecting = new("phone.statusConnecting", "Connecting…");
        public static readonly LocString Reconnecting = new("phone.reconnecting", "Reconnecting…");
        public static readonly LocString ConnectionLost = new("phone.connectionLost", "Connection lost");
        public static readonly LocString ReturnToCall = new("phone.returnToCall", "Tap to return to call");
        public static readonly LocString SettingsTitle = new("phone.settingsTitle", "Phone Calls");
        public static readonly LocString SummaryOn = new("phone.summaryOn", "On");
        public static readonly LocString SummaryOff = new("phone.summaryOff", "Off");
        public static readonly LocString Calls = new("phone.calls", "Calls");
        public static readonly LocString EnablePhoneCalls = new("phone.enablePhoneCalls", "Enable Phone Calls");
        public static readonly LocString Microphone = new("phone.microphone", "Microphone");
        public static readonly LocString Speaker = new("phone.speaker", "Speaker");
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
        public static readonly LocString CallDeclined = new("phone.callDeclined", "Call declined");
        public static readonly LocString Unavailable = new("phone.unavailable", "Unavailable");
        public static readonly LocString GroupCall = new("phone.groupCall", "Group call");
        public static readonly LocString ContactsSection = new("phone.contactsSection", "Contacts");
        public static readonly LocString NoContactsTitle = new("phone.noContactsTitle", "No one to call yet");
        public static readonly LocString SignInTitle = new("phone.signInTitle", "Sign in to call");
        public static readonly LocString FilterHint = new("phone.filterHint", "Search contacts");
        public static readonly LocString NewCall = new("phone.newCall", "New Call");
        public static readonly LocString Outgoing = new("phone.outgoing", "Outgoing");
        public static readonly LocString Incoming = new("phone.incoming", "Incoming");
        public static readonly LocString Missed = new("phone.missed", "Missed");
        public static readonly LocString NoRecentCalls = new("phone.noRecentCalls", "No Recent Calls");
        public static readonly LocString NoRecentCallsHint = new("phone.noRecentCallsHint", "Calls you make and receive will appear here");
        public static readonly LocString ContactInfo = new("phone.contactInfo", "Info");
        public static readonly LocString MissedCallBody = new("phone.missedCallBody", "Missed call");
        public static readonly LocString OutcomeUnavailableTitle = new("phone.outcomeUnavailableTitle", "Couldn't reach {0}");
        public static readonly LocString OutcomeUnavailableBody = new("phone.outcomeUnavailableBody", "They're not available right now.");
        public static readonly LocString OutcomeDeclinedTitle = new("phone.outcomeDeclinedTitle", "{0} declined");
        public static readonly LocString OutcomeDeclinedBody = new("phone.outcomeDeclinedBody", "They can't take the call right now.");
        public static readonly LocString OutcomeNoAnswerBody = new("phone.outcomeNoAnswerBody", "{0} didn't pick up.");
        public static readonly LocString OutcomeDroppedTitle = new("phone.outcomeDroppedTitle", "Call dropped");
        public static readonly LocString OutcomeDroppedBody = new("phone.outcomeDroppedBody", "The connection was lost.");
        public static readonly LocString OutcomeDismiss = new("phone.outcomeDismiss", "OK");
        public static readonly LocString End = new("phone.end", "End");
    }

    internal static class Friends
    {
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
        public static readonly LocString Pending = new("friends.pending", "Waiting for them to add your number");
        public static readonly LocString PendingShort = new("friends.pendingShort", "Pending");
        public static readonly LocString Call = new("friends.call", "Call");
        public static readonly LocString EditName = new("friends.editName", "Edit Name");
        public static readonly LocString RenameFailed = new("friends.renameFailed", "Could not update the name");
        public static readonly LocString Remove = new("friends.remove", "Remove");
        public static readonly LocString ConfirmRemove = new("friends.confirmRemove", "Remove {0} from your contacts?");
        public static readonly LocString RemoveFailed = new("friends.removeFailed", "Could not remove the contact");
        public static readonly LocString NewNumberTitle = new("friends.newNumberTitle", "Request a New Number");
        public static readonly LocString NewNumberBody = new("friends.newNumberBody", "If someone you do not trust has your number, you can ask for a new one. Everyone who saved your old number will lose it.");
        public static readonly LocString ReasonHint = new("friends.reasonHint", "Tell us briefly why you need a new number");
        public static readonly LocString SendRequest = new("friends.sendRequest", "Send Request");
        public static readonly LocString Sending = new("friends.sending", "Sending…");
        public static readonly LocString RequestPending = new("friends.requestPending", "Your request is waiting for review");
        public static readonly LocString RequestApproved = new("friends.requestApproved", "Your number was changed. Share the new one with people you trust");
        public static readonly LocString RequestDenied = new("friends.requestDenied", "Your last request was declined");
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
        public static readonly LocString AccentCustom = new("settings.accentCustom", "Custom");
        public static readonly LocString PhoneCase = new("settings.phoneCase", "Case");
        public static readonly LocString Wallpaper = new("settings.wallpaper", "Wallpaper");
        public static readonly LocString TextSize = new("settings.textSize", "Text Size");
        public static readonly LocString PhoneSize = new("settings.phoneSize", "Phone Size");
        public static readonly LocString ClockFormat = new("settings.clockFormat", "Clock");
        public static readonly LocString Use24HourClock = new("settings.use24HourClock", "24-hour time");
        public static readonly LocString Notifications = new("settings.notifications", "Notifications");
        public static readonly LocString DoNotDisturb = new("settings.doNotDisturb", "Do Not Disturb");
        public static readonly LocString Vibration = new("settings.vibration", "Vibration");
        public static readonly LocString VibrationHint = new("settings.vibrationHint", "The phone shakes briefly when a notification arrives.");
        public static readonly LocString QuietWhileBusy = new("settings.quietWhileBusy", "Quiet While Busy");
        public static readonly LocString QuietWhileBusyHint = new("settings.quietWhileBusyHint", "Hold banners, sound and vibration during combat, duties, cutscenes and loading. Notifications still arrive.");
        public static readonly LocString ShowNotificationBanner = new("settings.showNotificationBanner", "Show Notification Banner");
        public static readonly LocString ShowNotificationBannerHint = new("settings.showNotificationBannerHint", "Display banner notifications at the top of the screen.");
        public static readonly LocString NotificationApps = new("settings.notificationApps", "Apps");
        public static readonly LocString AllowNotifications = new("settings.allowNotifications", "Allow Notifications");
        public static readonly LocString NotificationsOff = new("settings.notificationsOff", "Off");
        public static readonly LocString SoundDefault = new("settings.soundDefault", "Default");
        public static readonly LocString Immersion = new("settings.immersion", "Immersion");
        public static readonly LocString Behavior = new("settings.behavior", "Behavior");
        public static readonly LocString ScrollWhileIdle = new("settings.scrollWhileIdle", "Scroll while idle");
        public static readonly LocString ScrollWhileIdleHint = new("settings.scrollWhileIdleHint", "Your character scrolls through their phone (Tomescroll emote) while standing still and out of combat. Does nothing if you haven't unlocked the emote.");
        public static readonly LocString ShowInGpose = new("settings.showInGpose", "Show in Group Pose");
        public static readonly LocString ShowInGposeHint = new("settings.showInGposeHint", "Keep the phone available while you're in Group Pose, so you can open it during photo shoots. Turn it off to keep your screen clear for screenshots.");
        public static readonly LocString ImportScreenshots = new("settings.importScreenshots", "Import screenshots");
        public static readonly LocString ImportScreenshotsHint = new("settings.importScreenshotsHint", "Copy screenshots you take into the Photos gallery, including ones from ReShade and GShade. Only shots taken while the phone is running are copied, and the originals stay where they are.");
        public static readonly LocString NativeFileDialog = new("settings.nativeFileDialog", "Use the Windows file browser");
        public static readonly LocString NativeFileDialogHint = new("settings.nativeFileDialogHint", "Pick photos and sounds with the Windows file browser. Turn it off if importing a file crashes your game.");
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
        public static readonly LocString CopySupportInfo = new("settings.copySupportInfo", "Copy Support Info");
        public static readonly LocString SupportInfoCopied = new("settings.supportInfoCopied", "Copied to clipboard");
        public static readonly LocString CreditsLinks = new("settings.creditsLinks", "Credits & links");
        public static readonly LocString SupportAetherphone = new("settings.supportAetherphone", "Support Aetherphone");
        public static readonly LocString SupportHint = new("settings.supportHint", "Aetherphone is free and made in my spare time. If you enjoy it, a pledge on Patreon helps me keep building and improving it. Thank you for being here.");
        public static readonly LocString JoinDiscord = new("settings.joinDiscord", "Join our Discord");
        public static readonly LocString VisitWebsite = new("settings.visitWebsite", "Visit our website");
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
        public static readonly LocString TellArchiveTitle = new("settings.tellArchiveTitle", "Chat History");
        public static readonly LocString TellArchive = new("settings.tellArchive", "Save tell history on this PC");
        public static readonly LocString TellArchiveHint = new("settings.tellArchiveHint", "Tells are saved as plain text files on this PC so conversations survive a restart. They are never uploaded anywhere. Turn this off to keep new tells in memory only. Deleting a conversation also deletes its file.");
        public static readonly LocString ReadReceipts = new("settings.readReceipts", "Read receipts");
        public static readonly LocString LastSeenOnline = new("settings.lastSeenOnline", "Last seen online");
        public static readonly LocString ChatPrivacyHint = new("settings.chatPrivacyHint", "These apply to the Message app. If you turn read receipts or last seen off, you will not send them and you will not see them from others either.");
        public static readonly LocString Commands = new("settings.commands", "Commands");
        public static readonly LocString CommandsSummary = new("settings.commandsSummary", "Slash commands");
        public static readonly LocString CommandsHint = new("settings.commandsHint", "Type these into the chat box. Reset brings the phone back to the middle of your screen if you ever move it out of view.");
        public static readonly LocString CommandToggle = new("settings.commandToggle", "Show or hide the phone");
        public static readonly LocString CommandAlias = new("settings.commandAlias", "Alias for /phone");
        public static readonly LocString CommandMarket = new("settings.commandMarket", "Open the market board, optionally searching an item");
        public static readonly LocString CommandReset = new("settings.commandReset", "Move the phone back to the center of the screen");
        public static readonly LocString CommandTest = new("settings.commandTest", "Send a sample notification");
    }

    internal static class Changelog
    {
        public static readonly LocString[] Release0999 =
        {
            new("changelog.r0999.0",
                "Added seven phone cases: Atomos, Baby Bat, Dwarf Rabbit, Enkidu, Horror, Kupo, and Runic"),
            new("changelog.r0999.1",
                "Every phone case was recompressed, so the plugin downloads at less than half its old size even with the seven new cases"),
            new("changelog.r0999.2",
                "A message now appears in an open conversation the moment it arrives, instead of waiting for the next refresh"),
            new("changelog.r0999.3",
                "Opening a conversation, or coming back to one you left open, now pulls the newest messages straight away"),
            new("changelog.r0999.4",
                "Chat now says when it cannot reach the server and offers a retry, instead of quietly showing an old transcript"),
            new("changelog.r0999.5",
                "Landscape now runs the hardware buttons along the top and bottom edges and keeps the screen the size it has in portrait, contributed by Raya"),
            new("changelog.r0999.6",
                "Photo pickers now draw only the thumbnails you can see, so opening one with a large library no longer stalls, contributed by BluntEXE"),
            new("changelog.r0999.7",
                "A shortcut icon borrowed from another plugin now fills the tile all the way to its rounded edge"),
            new("changelog.r0999.8",
                "Housing filter chips no longer wrap their labels, and the filter drawer rows sit on the standard spacing, contributed by Chaosvanguard"),
            new("changelog.r0999.9",
                "Fixed one refused request pausing all the others: an upload stopped by the request limit no longer holds back sends and refreshes"),
            new("changelog.r0999.10",
                "Fixed the Doman Enclave daily reporting your whole allowance as outstanding, ignoring what you had already donated, contributed by Farroness"),
            new("changelog.r0999.11",
                "Fixed the venue website link falling back to a page that does not exist on FFXIV Venues, contributed by Raya"),
            new("changelog.r0999.12",
                "Fixed the case artwork rotating the wrong way in landscape and leaving a hairline gap along the edge of the phone, contributed by Raya"),
            new("changelog.r0999.13",
                "Fixed the German wording for the physical ranged role, two game names, and a Dailies tour card, contributed by Silkie"),
        };

        public static readonly LocString[] Release0998 =
        {
            new("changelog.r0998.0",
                "Added Housing, a new app that browses every housing ward on a map, tracks the plots you are watching, and reminds you before a lottery entry period closes, contributed by Chaosvanguard"),
            new("changelog.r0998.1",
                "Added Community Radio to Music, with more details coming soon"),
            new("changelog.r0998.2",
                "Added new phone cases, contributed by Silkie, Zivyl, Remi, and Rania"),
            new("changelog.r0998.3",
                "Badges have been completely reworked: they are now granted or earned under certain conditions, with the details on the Discord server"),
            new("changelog.r0998.4",
                "A fullscreen photo can be popped out into an ordinary window with a title bar, resized freely, and named after the app it came from"),
            new("changelog.r0998.5",
                "A photo can be filed into an album from the viewer, without leaving for the album first"),
            new("changelog.r0998.6",
                "The market now shows what lands in your retainer after tax, and names the city with the lowest rate"),
            new("changelog.r0998.7",
                "When another world, data center, or region sells an item cheaper, the Prices card names it and its price"),
            new("changelog.r0998.8",
                "An item that only sells HQ now opens on the HQ tab instead of an empty NQ one"),
            new("changelog.r0998.9",
                "Hovering a market row reveals the full item name, so families such as Ballroom Etiquette are no longer indistinguishable"),
            new("changelog.r0998.10",
                "Calls gained a Speaker picker, so a call plays through the output device you choose instead of always following the system default"),
            new("changelog.r0998.11",
                "Quiet While Busy holds banners, sounds, and shakes during combat, duties, cutscenes, and zoning; the notification still arrives and still counts as unread"),
            new("changelog.r0998.12",
                "Notification toasts can be turned off globally or per app, contributed by Radvo"),
            new("changelog.r0998.13",
                "An alarm whose minute passed while the plugin was unloaded now still fires, within a ten minute catch-up window"),
            new("changelog.r0998.14",
                "Every chat app now keeps a draft per conversation, so what you wrote for one person no longer follows you into the next"),
            new("changelog.r0998.15",
                "Velvet can pass on a profile, which drops them for the session without blocking them"),
            new("changelog.r0998.16",
                "Leaving the Velvet profile editor with unsaved changes now asks before discarding them"),
            new("changelog.r0998.17",
                "A Velvet profile you have already reported no longer offers the flag again"),
            new("changelog.r0998.18",
                "Dragging an icon past the last Home Screen page now opens a new page"),
            new("changelog.r0998.19",
                "Escape now backs out of the topmost thing on screen: a confirmation, the report sheet, the share sheet, then Control Center"),
            new("changelog.r0998.20",
                "Control Center can be pulled open by dragging the status band down, not only by tapping it"),
            new("changelog.r0998.21",
                "The community rules unlock their accept button once you have read to the end, instead of after a fixed countdown"),
            new("changelog.r0998.22",
                "Dock icons now raise the same name pill as the rest of the Home Screen"),
            new("changelog.r0998.23",
                "Removing an app from the Home Screen now asks first"),
            new("changelog.r0998.24",
                "Dalamud's settings button beside the plugin now opens the phone on the Settings app"),
            new("changelog.r0998.25",
                "A shortcut can use any plugin's icon, picked from the appearance sheet"),
            new("changelog.r0998.26",
                "Copying a chat bubble, a Linkpearl line, or a travel command now shows a confirmation, so a copy is no longer silent"),
            new("changelog.r0998.27",
                "An album's rename and delete are now reachable from a menu button on the card, not only by right-click"),
            new("changelog.r0998.28",
                "Empty screens now offer the next step: an empty photo library offers Camera, and Velvet offers Clear filters when your own filters are hiding everyone"),
            new("changelog.r0998.29",
                "A list that failed to load now says so and offers Retry, instead of reading as an empty list"),
            new("changelog.r0998.30",
                "Camera remembers the grid and the flash, and Photos reopens on the tab you left"),
            new("changelog.r0998.31",
                "The emoji picker takes the caret when it opens, so you can type to search instead of hunting the grid"),
            new("changelog.r0998.32",
                "Games that run on a clock now show a Paused veil when the phone loses focus, instead of looking frozen"),
            new("changelog.r0998.33",
                "The battery icon warns as a whole below twenty percent and breathes below ten, rather than tinting the fill alone"),
            new("changelog.r0998.34",
                "An empty inbox now offers New message instead of only describing itself"),
            new("changelog.r0998.35",
                "About gained a Copy Support Info row that puts versions, OS, language, and sound state on the clipboard, carrying no account or character identifiers"),
            new("changelog.r0998.36",
                "Fixed encryption and imported WAV and MP3 playback on Linux and Wine, contributed by Ehno"),
            new("changelog.r0998.37",
                "Fixed a message the game rejects disappearing in silence: the text stays in the box and the phone says the send failed"),
            new("changelog.r0998.38",
                "Fixed the housing translations: every language now uses the wording its own game client uses, and district names are translated"),
            new("changelog.r0998.39",
                "Fixed the calendar starting its week on Sunday in the five languages whose week starts on Monday"),
            new("changelog.r0998.40",
                "Fixed mini-games running on while the phone was not focused, and Tetris and Sudoku taking the keyboard while you were typing in the game"),
            new("changelog.r0998.41",
                "Fixed Settings showing badges you no longer hold"),
            new("changelog.r0998.42",
                "Fixed screens collapsing onto the avatar while a profile photo had not loaded yet, most visibly in the Velvet profile editor"),
            new("changelog.r0998.43",
                "Fixed the photo viewer's buttons stacking up over the photo"),
            new("changelog.r0998.44",
                "Fixed the Yellow Pages ad form cutting off its failure message instead of wrapping it, contributed by Raya"),
            new("changelog.r0998.45",
                "Moved the Velvet photo gallery below the profile details, contributed by Farroness"),
            new("changelog.r0998.46",
                "Fixed the emote idle scroll firing while an event window was open, contributed by Chaosvanguard"),
            new("changelog.r0998.47",
                "Fixed Escape doing nothing when the phone had been opened with a slash command"),
            new("changelog.r0998.48",
                "Fixed the app badge counting the conversation you are currently reading"),
            new("changelog.r0998.49",
                "Fixed blocking someone in Velvet leaving them in the Discover deck"),
            new("changelog.r0998.50",
                "Fixed Block on a Velvet profile acting without asking first"),
            new("changelog.r0998.51",
                "Fixed a blocking moderation notice vanishing for good once dismissed: it now leaves a notification behind"),
            new("changelog.r0998.52",
                "Fixed two notification settings showing in English in every translated language"),
            new("changelog.r0998.53",
                "Notifications no longer appear at the title screen, where there is no session to act on them"),
        };

        public static readonly LocString[] Release0997 =
        {
            new("changelog.r0997.0",
                "Added Shortcuts, a new app that turns a run of commands into a single tap and pins it to the home screen"),
            new("changelog.r0997.1",
                "You can now resize the phone by dragging the bottom right corner of its frame"),
            new("changelog.r0997.2",
                "Phone Size in Settings is a slider now, so you can pick any size instead of six fixed ones"),
            new("changelog.r0997.3",
                "The whole screen scales with the phone, so text and icons grow with it instead of staying small"),
            new("changelog.r0997.4",
                "Text Size is a slider too now, and it goes down to 70 percent for people who want smaller text"),
            new("changelog.r0997.5",
                "Accent is a full color picker now, so you can use any color you like instead of the five presets"),
            new("changelog.r0997.6",
                "Ringtone volume and notification volume are sliders now, so you can pick any level instead of four fixed steps"),
            new("changelog.r0997.7",
                "The minimized phone now stays the same size whatever size the phone itself is"),
            new("changelog.r0997.8",
                "Fixed notifications coming back after you cleared them, and badges staying lit after everything had been read"),
            new("changelog.r0997.9",
                "Fixed taps in the Control Center doing nothing while a menu or picker was open in the app behind it"),
        };

        public static readonly LocString[] Release0996 =
        {
            new("changelog.r0996.0", "You can now link your Patreon account from the account section in Settings"),
            new("changelog.r0996.1", "Linking grants your member perks automatically, including the Patreon member badge, and they follow your membership as it changes"),
            new("changelog.r0996.2", "Settings shows whether your membership is active once linked, and you can unlink Patreon at any time"),
        };

        public static readonly LocString[] Release0995 =
        {
            new("changelog.r0995.0", "The phone has a new body: a machined metal frame and a glass band, with one curve shared by the screen, the glass, and every case"),
            new("changelog.r0995.1", "Cases are now hand-painted art instead of flat tints, starting with Titanium and Silkie; the Silkie design was made by Nui"),
            new("changelog.r0995.2", "Case art can spill past the edge of the phone, and it stays visible while the phone is minimized"),
            new("changelog.r0995.3", "The phone now ships with its own ringtones and notification sounds, and calls and notifications each pick from their own library"),
            new("changelog.r0995.4", "Accounts can now carry badges such as Verified, Support, Contributor, and Patreon member, shown as a glyph and a colored name across the social apps, each with its own animated effect; more information about badges will be shared in the near future"),
            new("changelog.r0995.5", "Settings lists your badges under your account, and you can unequip any badge you would rather not show"),
            new("changelog.r0995.6", "Reporting a message now attaches its photo or voice note to the report as evidence"),
            new("changelog.r0995.7", "Posts, comments, and stories removed by moderation now disappear from every phone right away"),
            new("changelog.r0995.8", "A suspension now locks only the social apps; the rest of the phone keeps working"),
            new("changelog.r0995.9", "Comments, galleries, likers, story viewers, inboxes, and other long lists now load more as you scroll instead of stopping at the first batch"),
            new("changelog.r0995.10", "A Velvet post's audience can be changed after posting, from the menu on the post"),
            new("changelog.r0995.11", "The Support Aetherphone button in Settings moved under the account section"),
            new("changelog.r0995.12", "Fixed an issue where old tags could still be seen on Velvet profiles"),
            new("changelog.r0995.13", "Tags that are no longer offered now appear as removable chips when you edit your Velvet profile, so you can clear them off"),
            new("changelog.r0995.14", "Fixed an issue where notifications could stop appearing, or could not be dismissed or turned off"),
            new("changelog.r0995.15", "Fixed tapping a notification doing nothing when its app was already open"),
            new("changelog.r0995.16", "Fixed conversations being marked as read while you were not looking at them"),
            new("changelog.r0995.17", "Fixed profiles showing an outdated copy: they now refresh every time you open one"),
            new("changelog.r0995.18", "Fixed the Velvet profile editor staying open after a save and saying nothing when saving failed"),
            new("changelog.r0995.19", "Fixed a freshly created Velvet post not showing in your profile gallery until the next refresh"),
            new("changelog.r0995.20", "Fixed Discover cards in Velvet stacking on top of each other"),
            new("changelog.r0995.21", "Fixed the Velvet request count lagging behind and counting requests it could not show"),
            new("changelog.r0995.22", "Fixed Yellow Pages inquiry notifications not opening the conversation"),
            new("changelog.r0995.23", "Fixed clicks passing through one window into whatever sat behind it, contributed by Ehno"),
            new("changelog.r0995.24", "Fixed taps leaking through the emoji picker, mention popups, and report categories to the buttons underneath"),
            new("changelog.r0995.25", "Fixed cut-off text, hover scrolling, and overlapping layouts across many screens, contributed by Ehno"),
            new("changelog.r0995.26", "Fixed the Jobs header shifting when the categories button hides, with long titles scrolling instead of clipping, contributed by Ehno"),
            new("changelog.r0995.27", "Fixed very large images being able to exhaust memory: images are now checked and capped before they are decoded, contributed by BluntEXE"),
            new("changelog.r0995.28", "Fixed long confirmation popup titles being cut off instead of wrapping"),
            new("changelog.r0995.29", "Fixed icons sitting slightly off-center in round buttons"),
            new("changelog.r0995.30", "The network now gives every account far more headroom, so sending several messages in a row no longer trips a rate limit"),
            new("changelog.r0995.31", "A rare network pause now lasts seconds instead of minutes"),
            new("changelog.r0995.32", "A small capsule under the clock counts down any network pause, so the phone never goes quiet without saying why"),
        };

        public static readonly LocString[] Release0994 =
        {
            new("changelog.r0994.0", "Fixed apps closing the instant you opened them and returning you to the home screen while you were signed out"),
            new("changelog.r0994.1", "Fixed Settings closing the same way, which had left no way to sign back in; you can now open Settings and sign in again"),
        };

        public static readonly LocString[] Release0993 =
        {
            new("changelog.r0993.0", "Stopped the phone from making any network requests while you are signed out, including the background retries that used to run from the title screen"),
            new("changelog.r0993.1", "Fixed photos and voice notes that fail to load retrying nonstop; the phone now waits a couple of minutes before trying again"),
            new("changelog.r0993.2", "Fixed Velvet comments wrapping their text at the wrong width"),
            new("changelog.r0993.3", "Backend maintenance and optimizations"),
        };

        public static readonly LocString[] Release0992 =
        {
            new("changelog.r0992.0", "Fixed the view count on Yellow Pages ads, which always stayed at zero, so opening an ad now counts as a view"),
            new("changelog.r0992.1", "Added a menu to every post in Velvet, in the feed and on the post itself, so you can open it, report it, block whoever posted it, or delete your own"),
            new("changelog.r0992.2", "Rebuilt the profile page in Chirper and Aethergram: the name and handle now sit beside the photo, the buttons share a single row, and the empty gaps are gone"),
            new("changelog.r0992.3", "Removed one more suggested tag from Velvet, so it no longer shows up when you edit your profile or tag a post"),
            new("changelog.r0992.4", "Brought back the Windows file browser when you pick a photo or a sound, so it remembers your folders and shows thumbnails again"),
            new("changelog.r0992.5", "Added a switch in Settings under Behavior that swaps it for a simple browser inside the phone, which turns itself on if picking a file could crash your game"),
            new("changelog.r0992.6", "Split the Immersion settings in two, so the window, screenshot, and startup options now live on a new Behavior page"),
        };

        public static readonly LocString[] Release0991 =
        {
            new("changelog.r0991.0", "Removed a few suggested tags from Velvet, so they no longer appear when you edit your profile or tag a post"),
        };

        public static readonly LocString[] Release0990 =
        {
            new("changelog.r0990.0", "Added the App Store, a new app where you browse every app on the phone by category, install the ones you want, and remove the ones you do not, with a product page for each"),
            new("changelog.r0990.1", "Added Muster, a new app for meetups where you post a live announcement with your in-game location, start time, and duration, browse what is happening on your data center, RSVP, travel there in one tap, and keep everyone posted with quick updates; invites can be sent into any chat"),
            new("changelog.r0990.2", "Added Yellow Pages, a new classifieds board where you post a place, a service, or a call for people, with photos, a housing address, opening hours, and end-to-end encrypted inquiries; ads renew, expire on their own, and share into any chat as a card"),
            new("changelog.r0990.3", "Added Announcements, a new app where news from the team lands on every phone as a banner, a notification, and an unread badge"),
            new("changelog.r0990.4", "Added Jobs, a new app that lists your classes by role, switches to a gearset with one tap, and lets you sort your gearsets into your own categories, contributed by K.I.R.O"),
            new("changelog.r0990.5", "Added Health, a new app that tracks your character's activity: estimated steps and distance on foot, swimming, active time, hydration reminders, goals, history, streaks, and personal records, contributed by YozoraCho"),
            new("changelog.r0990.6", "Games adds seven titles: Sudoku, Chess against the phone, Stack, Crystal Drop, Beat, Blade Throw, and Trivia"),
            new("changelog.r0990.7", "The Games launcher now leads with a daily challenge and counts your streak"),
            new("changelog.r0990.8", "Aethergram gets direct messages: an inbox in the top bar, replies, reactions, edits, voice notes, typing indicators, read receipts, and a Message button on profiles"),
            new("changelog.r0990.9", "Messages from strangers land in a separate Requests tab with no notification and no read receipts until you accept, and a conversation can be deleted for your side only"),
            new("changelog.r0990.10", "Aethergram posts can be sent into a chat with the paper plane, to several people at once"),
            new("changelog.r0990.11", "Aethergram stories now chain the way Instagram does, and you can reply to one with a message or a quick reaction that lands in the chat with the story attached"),
            new("changelog.r0990.12", "Accounts can be private: follows become requests you confirm or delete, they get their own row at the top of Activity, and people who do not follow you see a locked grid"),
            new("changelog.r0990.13", "Posts can be saved with a bookmark and found again in a Saved grid on your own profile"),
            new("changelog.r0990.14", "Profiles show a Followed by line built from people you both follow, and a Follows you chip next to the handle"),
            new("changelog.r0990.15", "Aethergram and Velvet posts can now be portrait or landscape, not only square"),
            new("changelog.r0990.16", "You can share your in-game location in a ChocoChat, Velvet, or Aethergram chat, and tapping the card drops a map flag"),
            new("changelog.r0990.17", "Chirper reactions are now full color emoji, seven of them new, and they sit in their own row under a post instead of being cut off after the third"),
            new("changelog.r0990.18", "Velvet's Discover filters are now a full-screen editor covering every part of a profile, with chips that cycle through neutral, include, and exclude, and new tone, pace, and style tags"),
            new("changelog.r0990.19", "Velvet's profile fields were rebuilt: eight genders you can pick more than one of, a new sexuality field, IRL and non-IRL intents, a flat role list, a separate kinks card, plain subject tags for limits, and poly, with no twelve-selection cap"),
            new("changelog.r0990.20", "Velvet's Feed tab has an Everyone and Connections switch, and each post can go to your connections only or to everyone"),
            new("changelog.r0990.21", "Velvet profile galleries are real now: people you are connected to see the grid, and strangers see how many photos are locked"),
            new("changelog.r0990.22", "The lock button in Velvet and Aethergram opens a real encryption screen, a locked chat shows a banner that takes you straight to recovery code entry, and the phone nudges you to save a code if you have none"),
            new("changelog.r0990.23", "Settings lists every Aethernet account stored on this phone and switches between them with one tap, so playing an alt no longer takes your main's handle, number, and chats off the phone"),
            new("changelog.r0990.24", "The Camera hides nameplates in your photos, contributed by K.I.R.O"),
            new("changelog.r0990.25", "The Camera also hides hotbars, the chat log, and target info while the shutter fires, and puts back exactly what it hid"),
            new("changelog.r0990.26", "The Camera has a rotate button that turns the phone sideways, so wide shots save as genuinely wide photos"),
            new("changelog.r0990.27", "Screenshots you take in game are imported into Photos automatically, including ReShade and GShade ones; the toggle is in Settings under Immersion"),
            new("changelog.r0990.28", "Photos gets custom albums and a button that opens the photos folder on your PC, contributed by Syrilai"),
            new("changelog.r0990.29", "Photos gets a share button that sends a photo into a chat, sets it as your wallpaper, or hands it to Aethergram or Velvet to post"),
            new("changelog.r0990.30", "Skywatcher gets a Control tab that overrides the zone's weather and the Eorzean clock, with a scrub track across the day, Dawn, Noon, Dusk, and Midnight presets, and every weather the zone can roll; it is cosmetic and only you see it"),
            new("changelog.r0990.31", "Settings, Appearance now offers ten phone cases: Titanium, Graphite, Silver, Gold, Rose, Midnight, Jade, Coral, Lavender, and Porcelain"),
            new("changelog.r0990.32", "Folders can be tinted with the phone's accent colors and scroll instead of running out of room, contributed by K.I.R.O"),
            new("changelog.r0990.33", "The labels under Home Screen icons can be hidden with a Show App Names toggle, contributed by BluntEXE"),
            new("changelog.r0990.34", "Every scrolling surface on the phone now scrolls like a phone: drag with momentum, pull down to refresh, and a fling never fires the button under your finger, contributed by Valiice"),
            new("changelog.r0990.35", "Aetherphone now honors Dalamud's Reduce Motion setting and settles its animations instantly when it is on"),
            new("changelog.r0990.36", "The phone now follows your 12 or 24-hour clock preference everywhere, and picks the format your language implies if you never touch the setting, contributed by K.I.R.O"),
            new("changelog.r0990.37", "Music gains favorite radio stations, contributed by Hubkaw"),
            new("changelog.r0990.38", "The App Store product page names the developer behind each app, contributed by YozoraCho"),
            new("changelog.r0990.39", "Aethergram has its own settings screen behind a gear on your profile, carrying the Who can message you choice"),
            new("changelog.r0990.40", "Chirper now pages through a profile's whole chirp history instead of stopping after the first page"),
            new("changelog.r0990.41", "Every notification type has its own badge icon, rechirp notifications quote the chirp, and repost and quote notifications take you to the post"),
            new("changelog.r0990.42", "Each social app pulls its own activity feed, with older notifications loading as you scroll"),
            new("changelog.r0990.43", "Chirper and Aethergram stopped polling the feed every 25 seconds and have a refresh button instead"),
            new("changelog.r0990.44", "Tap the Aethergram home tab or the Chirper title to jump the feed back to the top and refresh it, contributed by K.I.R.O"),
            new("changelog.r0990.45", "The community rules are rewritten: every app has its own sectioned rule set with a What Is Allowed list, and Muster and Yellow Pages carry their own"),
            new("changelog.r0990.46", "The community rules can be reopened from inside Aethergram, Chirper, and Velvet at any time, contributed by K.I.R.O"),
            new("changelog.r0990.47", "Velvet is unavailable on Lalafell characters and now explains why on screen instead of quietly vanishing"),
            new("changelog.r0990.91", "Aethernet's security has been strengthened, with tighter protections around your account and everything the phone keeps for you"),
            new("changelog.r0990.92", "Several security weaknesses in Chirper, Aethergram, and Velvet were found and fixed, contributed by SHIGYL"),
            new("changelog.r0990.49", "Searching for people now needs at least four letters and matches from the start of a name, so nobody can sweep the search box to collect profiles"),
            new("changelog.r0990.50", "New accounts no longer take your character name as their display name or handle; you pick your own while setting up the phone"),
            new("changelog.r0990.51", "A muster host's character name and world stay hidden until you RSVP"),
            new("changelog.r0990.52", "Removing an account from the phone keeps its encryption key on the device, so signing back in still opens your old chats"),
            new("changelog.r0990.53", "Sign-in failures now explain themselves: brand-new characters can take a day to appear on the Lodestone, Chinese and Korean servers cannot be verified there, and the Lodestone throttles at peak hours"),
            new("changelog.r0990.54", "Skywatcher's panels are rebuilt as frosted cards with real depth and every weather glyph sits in its own sky chip, fixing gray text on gray backgrounds in fog and clouds"),
            new("changelog.r0990.55", "The Clock app and its widget follow a time override"),
            new("changelog.r0990.56", "Flow is rebuilt on rectangular boards that use the whole screen, with Easy, Medium, and Hard tiers that each keep their own best"),
            new("changelog.r0990.57", "Home Screen icons stay in the exact spot you drop them, gaps and all"),
            new("changelog.r0990.58", "The Home Screen wallpaper follows your Light or Dark theme instead of the real-world clock, and crossfades when you switch"),
            new("changelog.r0990.59", "Uninstalling an app now stops all of its background work and notifications at once and takes its widgets off the Home Screen with it"),
            new("changelog.r0990.60", "The About window is gone, replaced by a Support Aetherphone button at the bottom of Settings"),
            new("changelog.r0990.61", "The Skywatcher, Calendar, Clock, Timers, and Activity Home Screen widgets were reworked at every size, contributed by BluntEXE"),
            new("changelog.r0990.62", "Collections reads your unlocks from the game itself, so it works without waiting on an external site and no longer misses race-specific hairstyles, contributed by Syrilai"),
            new("changelog.r0990.63", "Dailies replaced Notify when tasks reset with a Show badge toggle"),
            new("changelog.r0990.64", "New messages in an open chat now arrive in about a second instead of waiting for the next poll"),
            new("changelog.r0990.65", "The phone now asks for far less over the network: unchanged checks cost a few hundred bytes, responses are compressed, and it backs off properly when the server asks it to"),
            new("changelog.r0990.66", "Social feeds no longer grow without bound while you scroll"),
            new("changelog.r0990.67", "Fixed encrypted chats becoming unreadable after a sign-out or a character switch by keeping your keys instead of dropping them"),
            new("changelog.r0990.68", "Fixed the phone binding your profile to the wrong Lodestone character, which could show a stranger's portrait as yours"),
            new("changelog.r0990.69", "Fixed Velvet still showing the previous character's profile, connections, feed, and chats after switching to an alt"),
            new("changelog.r0990.70", "Fixed the phone going unreachable while it was closed or calls were turned off, which silenced chat and social alerts"),
            new("changelog.r0990.71", "Fixed Light mode inking apps that paint their own background, which turned chat bubbles and headers black"),
            new("changelog.r0990.72", "Fixed a large sweep of text overflow: labels that were cut off or drawn over their neighbours now scroll when you hover them, across Chirper, Velvet, Aethergram, Market, Collections, Inventory, Fishing, Notifications, Activity, Settings, and the Messages, Music, Maps, and Linkpearl rows, contributed by BluntEXE"),
            new("changelog.r0990.73", "Fixed emoji in Chirper posts merging into each other and overlapping the line above, contributed by BluntEXE"),
            new("changelog.r0990.74", "Fixed Enter posting instead of picking the highlighted person while you tag someone, and the suggestion list opening on top of the home button"),
            new("changelog.r0990.75", "Fixed hyphenated words and the accent color swatches running past the edge of their panel"),
            new("changelog.r0990.76", "Fixed taps landing on the row instead of the button inside it, on Maps and Venues favorite stars, Music playlist buttons, story rings in the feeds, Velvet's accept, decline, and unblock, and unstarring a message"),
            new("changelog.r0990.77", "Fixed the media control's Stop and Previous buttons overlapping, so one tap could fire both, contributed by BluntEXE"),
            new("changelog.r0990.78", "Fixed Control Center modules you removed coming back after a restart, contributed by BluntEXE"),
            new("changelog.r0990.79", "Fixed Control Center opening on top of the Camera"),
            new("changelog.r0990.80", "Fixed the Photos viewer letterboxing inside the app margins instead of filling the screen, and the previous and next arrows pointing the wrong way"),
            new("changelog.r0990.81", "Fixed Skywatcher disagreeing with the game about the current weather inside duties, cutscenes, and weather-locked zones, and a moon showing on a daytime window"),
            new("changelog.r0990.82", "Fixed every non-English client falling back to the same cloud palette, glyph, and ambience regardless of the real weather"),
            new("changelog.r0990.83", "Fixed songs sticking on Buffering forever and thumbnails failing to load on Linux and Wine, contributed by BluntEXE"),
            new("changelog.r0990.84", "Fixed the game crashing under Wine while the phone sampled its signal bars, contributed by K.I.R.O"),
            new("changelog.r0990.85", "Fixed the wrong roulette being marked as done in Dailies, contributed by Syrilai"),
            new("changelog.r0990.86", "Fixed Flow tubes detaching on a fast drag, and levels that looked finished but never ended"),
            new("changelog.r0990.87", "Fixed Bubbles refusing to pop matching bubbles after a new row, never ending a game, and shots drifting off the aim preview after a wall bounce"),
            new("changelog.r0990.88", "Fixed the idle Tomescroll animation stealing back an emote you played yourself"),
            new("changelog.r0990.89", "Fixed the phone crashing the game for ReShade users when picking a picture from the PC"),
            new("changelog.r0990.90", "Fixed the phone forgetting where you left it on screen after closing the game"),
        };

        public static readonly LocString[] Release0989 =
        {
            new("changelog.r0989.0", "Velvet is now hidden on Lalafell characters while a community poll on the matter gathers votes"),
        };

        public static readonly LocString[] Release0988 =
        {
            new("changelog.r0988.0", "Chirper now lets you rechirp a post so your followers see it, or quote it to add your own thoughts above the original"),
            new("changelog.r0988.1", "Velvet profiles now have a gender field, and Discover has a matching filter so you can choose how you appear and narrow who you see"),
            new("changelog.r0988.2", "Velvet now asks for your gender while you set up your profile, so you can be found from the moment you join"),
            new("changelog.r0988.3", "Chirper, Aethergram, and Velvet now show the community guidelines the first time you open each one, so the ground rules are clear before you post"),
            new("changelog.r0988.4", "If a character is banned, the phone now shows a full-screen notice with the reason"),
            new("changelog.r0988.5", "You now get a notification when a moderator sends you a warning or updates one of your reports"),
            new("changelog.r0988.6", "Music now has a repeat mode, so you can loop the current song or your whole queue"),
            new("changelog.r0988.7", "Calls now let you know clearly when one is declined, goes unanswered, or drops"),
        };

        public static readonly LocString[] Release0987 =
        {
            new("changelog.r0987.3", "After hearing your concerns, we have completely removed anonymous usage analytics for everyone; Aetherphone no longer collects any analytics at all, so feel free to use the phone all you like"),
            new("changelog.r0987.4", "We have removed AI moderation completely; your posts and comments are no longer reviewed by AI"),
            new("changelog.r0987.6", "Removed the KupoAI app from the phone; there are no longer any AI features in Aetherphone"),
            new("changelog.r0987.5", "Moderation is now handled through your reports, with new protections added to the report system, and people who act inappropriately will now receive a warning"),
            new("changelog.r0987.0", "Photos and avatars now load faster: images are cached on your device instead of being downloaded again every time you scroll"),
            new("changelog.r0987.2", "When moderation removes one of your comments, the notification now says it was a comment and gives the reason"),
        };

        public static readonly LocString[] Release0986 =
        {
            new("changelog.r0986.0", "Fixed a connection issue with the server, so the phone now stays connected more reliably"),
        };

        public static readonly LocString[] Release0985 =
        {
            new("changelog.r0985.3", "Aetherphone now has full-color emoji: tap the new emoji button to add them to your chats, posts, and comments, and they render inline right where you type"),
            new("changelog.r0985.4", "Aetherphone now gives each character its own social accounts: switch characters in game and the social apps sign out, ready for you to sign in as whoever you are playing"),
            new("changelog.r0985.5", "Your encrypted chats can now move with you: save a recovery code and use it to restore them when you sign in on a new device"),
            new("changelog.r0985.0", "Velvet's Discover filters now include a region, so you can narrow the people you see to NA, EU, JP, or OCE"),
            new("changelog.r0985.1", "Linkpearl now has a bell button on the Chats tab that pauses every chat and linkshell notification at once, and turns them back on when you tap it again"),
            new("changelog.r0985.2", "Fixed search in Velvet so you can find people by name, handle, or the tags on their profile"),
            new("changelog.r0985.6", "Fixed the Doman Enclave restoration showing up as a daily in Dailies; it now tracks on its weekly reset"),
            new("changelog.r0985.7", "Fixed the sample notification from the /phone test command refusing to clear from the notification center"),
        };

        public static readonly LocString[] Release0984 =
        {
            new("changelog.r0984.0", "KupoAI is in maintenance and is hidden from the phone for now; it will return in a later update with smarter, more intelligent answers"),
        };

        public static readonly LocString[] Release0983 =
        {
            new("changelog.r0983.0", "Music now has playlists: create your own, add songs from Now Playing or search, and play, rename, or delete them whenever you like"),
            new("changelog.r0983.1", "The phone can now stay open during Group Pose for photo shoots, with a new Show in Group Pose toggle in Settings under Immersion"),
            new("changelog.r0983.2", "Velvet's Discover list now keeps loading more people as you scroll, instead of stopping after the first page"),
            new("changelog.r0983.3", "A warm welcome to all our new Brazilian friends: Aetherphone now speaks Português (Brasil)"),
            new("changelog.r0983.4", "Fixed Music, ringtones, and call audio turning down the whole game's volume and leaving it lowered; the phone now keeps its own volume separate from the game"),
            new("changelog.r0983.5", "Fixed Chinese and Japanese text running off the right edge of the screen instead of wrapping onto the next line"),
        };

        public static readonly LocString[] Release0982 =
        {
            new("changelog.r0982.0", "Fixed sending a photo or voice note in a chat sometimes failing to go through"),
            new("changelog.r0982.1", "Fixed a post removed by moderation still showing in your feed until you reloaded the phone"),
            new("changelog.r0982.2", "Fixed long names in the story tray overlapping the tiles beside them"),
            new("changelog.r0982.3", "Fixed image feeds shaking, and their photos and text reflowing, while you scrolled or moved between screens"),
        };

        public static readonly LocString[] Release0981 =
        {
            new("changelog.r0981.0", "Fixed the Discord link so it now opens the current server"),
        };

        public static readonly LocString[] Release0980 =
        {
            new("changelog.r0980.0", "Activity is now a daily tracker: close your rings as you clear roulettes and duties, keep a history of your streaks, and watch a home widget fill up through the day"),
            new("changelog.r0980.1", "Dailies has been rebuilt around gradient cards and now fills itself in, detecting the roulettes and hunt bills you have already done"),
            new("changelog.r0980.2", "The Photos gallery has been rebuilt with a day-by-day library, smart month albums, and a full-screen viewer"),
            new("changelog.r0980.3", "Collections has been redrawn with colored category tiles and glass cards to match the rest of the phone"),
            new("changelog.r0980.4", "Inventory has been redrawn with colored source tiles and glass cards to match the rest of the phone"),
            new("changelog.r0980.5", "Wallet has been redrawn with the same gradient-card look as the rest of the phone"),
            new("changelog.r0980.6", "Fishing has been rebuilt in the phone's gradient look and now carries the Ruby Route schedule alongside Indigo"),
            new("changelog.r0980.7", "Music now starts a song the instant you tap it, instead of waiting for the whole track to load, and its search now reaches long mixes and full albums"),
            new("changelog.r0980.8", "Music radio can now be filtered by country and language and sorted, making it easier to find something to listen to"),
            new("changelog.r0980.9", "Velvet's feed is now a full post-card timeline, on par with Aethergram and Chirper"),
            new("changelog.r0980.10", "Photos and voice notes you send in ChocoChat or Velvet are now end-to-end encrypted, the same way your messages already were"),
            new("changelog.r0980.11", "You can now rename a saved contact in ChocoChat"),
            new("changelog.r0980.12", "About now links to the Aetherphone Discord and website"),
            new("changelog.r0980.33", "Settings now has a Delete account button in the Account section that removes your account and erases everything held for it on Aethernet"),
            new("changelog.r0980.15", "You can now block someone from the phone, and a block now holds everywhere: they leave your chats, they can no longer show up on your posts' likes and comments, and they cannot reach you"),
            new("changelog.r0980.16", "The Calendar app now loads events through Aetherphone's own service instead of a third-party key"),

            new("changelog.r0980.17", "The Chirper and Aethergram feeds now stay smooth however far you scroll, drawing only the posts on screen"),
            new("changelog.r0980.18", "Photo grids now load small thumbnails and keep full resolution for the viewer alone, so galleries open faster and use far less memory"),
            new("changelog.r0980.19", "The phone now keeps its graphics memory in check, so a long browsing session no longer builds up and slows the game down"),
            new("changelog.r0980.21", "The phone is sturdier now: a glitch inside one app can no longer bring the whole phone down with it"),
            new("changelog.r0980.22", "Aethernet now answers noticeably faster across feeds, profiles, chats, and reactions, with its busiest lookups rebuilt and freshly indexed"),
            new("changelog.r0980.31", "Aethernet now stands up to abuse and sudden spikes in traffic: rate limits, a cap on live connections, and health checks keep it responsive even when everyone is online at once"),
            new("changelog.r0980.20", "The connection now holds steady even with thousands of players online at once, and calls and live updates recover on their own after a network hiccup"),
            new("changelog.r0980.32", "Everything you post and send is now backed up off-site automatically, so your data stays safe even if something goes wrong"),

            new("changelog.r0980.23", "Fixed a tap on a chat menu also pressing the message bubble behind it"),
            new("changelog.r0980.24", "Fixed the photo picker in chat showing old photos: it now refreshes every time you open it"),
            new("changelog.r0980.25", "Fixed Collections showing the wrong progress on its tiles"),
            new("changelog.r0980.26", "Fixed the clock reading the wrong time when your custom clock format included the AM or PM marker"),
            new("changelog.r0980.27", "Fixed the phone forgetting your Lock Position setting whenever you minimized or maximized it"),
            new("changelog.r0980.28", "Fixed the phone being willing to open any kind of link: it now opens only ordinary web links"),
            new("changelog.r0980.29", "Fixed some conversations showing no last-message preview in your chat list"),
            new("changelog.r0980.30", "Fixed loading older messages in a conversation sometimes skipping or repeating one"),
        };

        public static readonly LocString[] Release0970 =
        {
            new("changelog.r0970.37", "Velvet photos are now for your connections only: the feed no longer shows posts from people you have not connected with"),
            new("changelog.r0970.38", "Choosing who can see a Velvet post is gone, because every post now goes to your connections and no further"),
            new("changelog.r0970.41", "You can now disconnect from someone in Velvet without blocking them, which ends the connection for both of you"),
            new("changelog.r0970.42", "Disconnecting in Velvet takes the chat out of both inboxes, and connecting again later brings the conversation back"),
            new("changelog.r0970.0", "Velvet now has stories: share a photo your connections can watch for 24 hours before it disappears on its own"),
            new("changelog.r0970.1", "You can now tap the seen count on your own story to see everyone who watched it, and when"),
            new("changelog.r0970.22", "You can now mention people with @ in a chirp, a caption, or a comment across Chirper, Aethergram, and Velvet: the handle becomes a link that opens their profile"),
            new("changelog.r0970.23", "Typing @ while composing now suggests people to pick from, so you no longer have to remember a handle exactly"),
            new("changelog.r0970.24", "Being mentioned now reaches you as a notification that opens the post it came from"),
            new("changelog.r0970.25", "You can now tag people in your Aethergram photos: turn on Tag people while composing, tap the photo, and choose who it is"),
            new("changelog.r0970.26", "A photo carrying tags now shows a small person chip: tap it to reveal the names, and tap a name to open that profile"),
            new("changelog.r0970.27", "Aethergram profiles now have a Tagged tab, holding the photos you have been tagged in"),
            new("changelog.r0970.28", "Settings now has a Tags and mentions screen, where you choose who can mention you in posts and comments, and who can tag you in photos"),
            new("changelog.r0970.29", "You can now hold tags for review before they appear: turn on Manually approve tags, and nothing reaches your Tagged tab until you approve it"),
            new("changelog.r0970.34", "The phone now tells you when a newer Aetherphone is out: an update button appears beneath it, and clicking it takes you straight to Dalamud's plugin installer"),
            new("changelog.r0970.2", "Tapping a profile photo in Chirper, Aethergram, Velvet, or Message now opens it enlarged"),

            new("changelog.r0970.3", "The story tray now scrolls away with the feed instead of staying pinned above the posts"),
            new("changelog.r0970.4", "Aethergram's bottom tabs now light up and name themselves as you point at them"),
            new("changelog.r0970.5", "Notification rows now highlight as you point at them in Chirper, Aethergram, and Velvet"),
            new("changelog.r0970.6", "Photo captions on Aethergram, Velvet, and stories now go through moderation alongside the photo itself"),
            new("changelog.r0970.7", "A story photo that is still in review is now held back in the story viewer, the way it already was everywhere else"),
            new("changelog.r0970.17", "A story that has expired is now treated as gone everywhere, including its viewer list"),
            new("changelog.r0970.8", "Aetherphone is now about a third smaller to download and install: the wallpapers it ships with are packed far more efficiently at the same resolution"),
            new("changelog.r0970.9", "Encrypted chats now set up on computers that run the game through Wine or Proton, where creating the security key used to fail"),
            new("changelog.r0970.10", "A PC that cannot support encrypted chats now says so in the chat encryption sheet, instead of showing Setting up encryption forever"),
            new("changelog.r0970.21", "Turkish uppercase labels are now spelled correctly throughout the phone: headings like UYARILAR no longer come out with a lowercase ı in the middle"),

            new("changelog.r0970.18", "Fixed Aetherphone closing the game on startup for players whose Windows country or regional format is set to Russia or Belarus"),
            new("changelog.r0970.19", "Fixed some labels, such as the Ringtone row in Settings and the Control Center tiles, staying in the previous language after you changed language"),
            new("changelog.r0970.30", "Fixed the reason a call was declined or unavailable, the Wallet section headers, and the Calculator error staying in English whatever language your phone was set to"),
            new("changelog.r0970.31", "Fixed the edited marker on a message reading backwards in Turkish, Japanese, and German, where the time has to come first"),
            new("changelog.r0970.32", "Fixed a post's detail view still showing In review after the review had finished, and hiding the comments other people had left"),
            new("changelog.r0970.20", "Fixed the details under a Velvet profile's name landing on top of each other when the person is looking for several things at once"),
            new("changelog.r0970.33", "Fixed speckles appearing along the edges of small rounded corners"),
            new("changelog.r0970.11", "Fixed composing a story framing your photo as a square when it publishes tall"),
            new("changelog.r0970.12", "Fixed sharing a story showing no progress, leaving the Share button looking untouched for the whole upload"),
            new("changelog.r0970.13", "Fixed being offered a new story tile beside the ring of the story you had already posted"),
            new("changelog.r0970.14", "Fixed a story's caption and its seen count landing on top of each other"),
            new("changelog.r0970.15", "Fixed leaving an app in the middle of a story leaving that story hanging over the app when you came back"),
            new("changelog.r0970.16", "Fixed the story delete confirmation saying it couldn't delete the post"),
            new("changelog.r0970.35", "Fixed the phone forgetting where you last left it minimized, so minimizing it again returns it to that spot instead of the full phone's corner"),
            new("changelog.r0970.36", "Fixed the phone forgetting where you had placed it, full size and minimized, whenever the plugin was turned off and on again or the game restarted"),
            new("changelog.r0970.39", "Fixed importing a photo from your PC doing nothing visible when composing an Aethergram or Velvet post: it now lands in the photo grid, selected and numbered"),
            new("changelog.r0970.40", "Fixed Aethergram saying nothing when you tried to add a ninth photo to a post, instead of telling you eight is the limit"),
        };

        public static readonly LocString[] Release0960 =
        {
            new("changelog.r0960.0", "Velvet has been rebuilt from the ground up, with an after-dark look and every screen redrawn around Discover"),
            new("changelog.r0960.1", "Velvet's Discover filters now live behind a filter button, leaving the whole screen to the people you are browsing"),
            new("changelog.r0960.2", "Velvet's role and tag pickers are now sorted into categories that match how roleplay communities actually describe themselves"),
            new("changelog.r0960.3", "Setting up Velvet is now a guided flow that walks you through your identity, what you are looking for, and your profile"),
            new("changelog.r0960.4", "Velvet now has a guided tour that shows you around the first time you open it"),
            new("changelog.r0960.5", "Velvet now speaks every language Aetherphone supports, instead of always showing English"),
            new("changelog.r0960.6", "You can now send an intro message with a Velvet connection request, and choose who is allowed to message you"),
            new("changelog.r0960.7", "Japanese, Chinese, Russian, and Korean text now renders everywhere in the phone instead of turning into question marks, whatever language your phone is set to"),
            new("changelog.r0960.8", "New messages and notifications now reach you the moment they are sent, instead of waiting for the next refresh"),
            new("changelog.r0960.9", "You can now stay signed in on several computers at once: a second install no longer knocks the first offline, and calls ring properly again"),
            new("changelog.r0960.10", "Picking up a call on one device now stops the ringing on the others"),
            new("changelog.r0960.11", "The phone now goes quiet while it is closed or minimized, cutting its background network use to a fraction"),
            new("changelog.r0960.12", "Photos and other media now load faster and are reused from cache instead of being downloaded again every time"),
            new("changelog.r0960.13", "KupoAI now answers everyday questions far more reliably instead of coming up empty when your wording does not match the wiki"),
            new("changelog.r0960.14", "KupoAI now reads wiki tables and infoboxes, so unlock requirements, item costs, and step tables make it into its answers"),
            new("changelog.r0960.15", "KupoAI now recognizes the colloquial names people actually use for pages, not just their official titles"),
            new("changelog.r0960.16", "KupoAI replies no longer carry stray bracketed citation numbers"),
            new("changelog.r0960.17", "The phone now opens by itself when you log in; you can turn this off in Settings"),
            new("changelog.r0960.18", "Dragging the minimized phone no longer drags the full phone with it: each one remembers its own place on screen"),
            new("changelog.r0960.19", "Fixed comments overlapping each other in a Chirper post thread when they had likes"),
            new("changelog.r0960.20", "The Aetherphone website is now available in all eight languages and follows your browser's language automatically"),
            new("changelog.r0960.21", "Plugin developers can now propose their plugin as a phone app through the new App integration request form on GitHub"),
            new("changelog.r0960.22", "Aethergram and Velvet posts can now hold up to 8 photos instead of just one"),
            new("changelog.r0960.23", "Posts with several photos now show dot indicators and arrows, so you can swipe through them straight from the feed"),
            new("changelog.r0960.24", "Composing a multi-photo post now walks you through framing each photo in turn before you write the caption"),
            new("changelog.r0960.25", "Aethergram now has stories: share a photo that everyone can watch for 24 hours before it disappears on its own"),
            new("changelog.r0960.26", "The Aethergram feed now opens with a story tray, where a bright ring marks anyone whose story you have not watched yet"),
            new("changelog.r0960.27", "Fixed photo grids and chat image bubbles shaking every frame in the gallery, the post composers, the wallpaper picker, and message threads"),
            new("changelog.r0960.28", "Fixed the compose button also clicking the post sitting behind it in Chirper, Velvet, and Message"),
            new("changelog.r0960.29", "Fixed Chirper reacting to the post underneath when you clicked an open menu on top of it"),
        };

        public static readonly LocString[] Release0950 =
        {
            new("changelog.r0950.0", "Aetherphone now has its own website at www.aetherphone.net, where you can explore the phone and all of its apps"),
            new("changelog.r0950.1", "Velvet chats now let you react to messages with an emoji, and clicking a reaction count shows who reacted"),
            new("changelog.r0950.2", "You can now reply to a Velvet message, with the original quoted inside your bubble"),
            new("changelog.r0950.3", "You can now edit or delete a Velvet message you already sent"),
            new("changelog.r0950.4", "You can now record and send voice notes in Velvet"),
            new("changelog.r0950.5", "You can now search for radio stations by name in Music"),
            new("changelog.r0950.6", "Radio browsing in Music now loads more stations as you keep scrolling and offers more genres to explore"),
            new("changelog.r0950.7", "Chirper and Aethergram now load more posts automatically as you scroll to the bottom"),
            new("changelog.r0950.8", "Message and Velvet now load older messages when you scroll to the top of a conversation"),
            new("changelog.r0950.9", "Encrypted chats look cleaner now that the redundant lock icon no longer appears on every message bubble"),
        };

        public static readonly LocString[] Release0940 =
        {
            new("changelog.r0940.0", "You can now like comments in Chirper, Aethergram, and Velvet, and you get notified when someone likes yours"),
            new("changelog.r0940.1", "Aetherphone now connects through api.aetherphone.net, its own permanent address; the switch happens automatically and needs nothing from you"),
            new("changelog.r0940.2", "Server traffic now runs behind additional protection against outages and abuse, for a more reliable connection"),
        };

        public static readonly LocString[] Release0931 =
        {
            new("changelog.r0931.0", "Fixed encrypted chats being stuck on Setting up encryption: the key exchange now completes, and Message and Velvet conversations lock end-to-end as intended"),
            new("changelog.r0931.1", "Encryption setup now retries on its own after a connection hiccup instead of staying stuck until you relog"),
            new("changelog.r0931.2", "An open chat now notices when your contact becomes ready for encryption and locks the conversation without you having to reopen it"),
        };

        public static readonly LocString[] Release0930 =
        {
            new("changelog.r0930.0", "You can now reply to any message in Message: the quoted original shows inside your bubble, and clicking it jumps back to that message"),
            new("changelog.r0930.1", "React to messages with a quick emoji strip; reaction counts appear under the bubble, and clicking a count shows who reacted"),
            new("changelog.r0930.2", "You can now edit a text you already sent, and edited messages show a small marker next to the time"),
            new("changelog.r0930.3", "Delete a message for everyone in the chat; it is replaced by a quiet placeholder"),
            new("changelog.r0930.4", "Forward a message to another chat: it arrives with a Forwarded label and you land in that conversation right away"),
            new("changelog.r0930.5", "Record voice notes with the mic button and send them as playable bubbles with progress and duration"),
            new("changelog.r0930.6", "Search inside a conversation from the chat header and jump between matches"),
            new("changelog.r0930.7", "Star messages you want to keep and find them all in the new Starred screen"),
            new("changelog.r0930.8", "Mute a busy chat from the list: its banners stop and it no longer counts toward the app badge"),
            new("changelog.r0930.9", "Chats now show when the other person is online or when they were last seen"),
            new("changelog.r0930.10", "Text you typed but never sent is kept as a draft and previewed in the chat list"),
            new("changelog.r0930.11", "Message info on your own messages shows when they were sent and read, member by member in groups"),
            new("changelog.r0930.12", "The encryption banner is now a lock in the chat header: click it to see a security code you can compare with your contact"),
            new("changelog.r0930.13", "New privacy toggles in Settings let you turn off read receipts and hide when you were last online"),
            new("changelog.r0930.14", "Rebuilt Music as a Spotify-style experience with a dark look, a personalized home screen, a dedicated search screen, and radio categories"),
            new("changelog.r0930.15", "Playing a song no longer switches screens: a mini player slides in at the bottom instead"),
            new("changelog.r0930.16", "Now Playing opens as a full-screen sheet with album art, drag-to-seek, and a volume slider"),
            new("changelog.r0930.17", "You can now pause and resume playback, including radio stations, from Music, the mini player, or the Dynamic Island"),
            new("changelog.r0930.18", "Folders on the Home screen now show a badge with the total unread count of the apps inside"),
            new("changelog.r0930.19", "Added page-flip buttons at the left and right edges of the Home screen as an alternative to swiping"),
            new("changelog.r0930.20", "Fresh installs now start with a curated two-page Home layout and a stocked dock"),
            new("changelog.r0930.21", "Repacked Control Center into a tidy grid with no empty cells; existing layouts move over automatically"),
            new("changelog.r0930.22", "The media control in Control Center is now a large square tile with artwork, title, and playback buttons"),
            new("changelog.r0930.23", "Darkened the backdrop behind Control Center so Home icons no longer show through the tiles"),
            new("changelog.r0930.24", "Tapping the Dynamic Island now opens the app it is showing, even while the island is expanded"),
            new("changelog.r0930.25", "A burst of messages from one conversation now updates a single banner instead of playing one banner per message"),
            new("changelog.r0930.26", "Fixed new-message alerts never appearing while the phone was closed or minimized with Message left open"),
            new("changelog.r0930.27", "Fixed long tooltips spilling past the window edge; they now wrap and stay inside"),
        };

        public static readonly LocString[] Release0920 =
        {
            new("changelog.r0920.0", "Added KupoAI, a new app that answers your questions about Final Fantasy XIV straight from the wiki, complete with sources to read more"),
            new("changelog.r0920.1", "Combined Friends, Phone, and your direct messages into a single app called ChocoChat, so your chats, contacts, and calls all live in one place"),
            new("changelog.r0920.2", "The Calls tab now keeps a full call history like a real phone, and badges any calls you missed"),
            new("changelog.r0920.3", "A brief connection drop no longer ends your call: it quietly reconnects on its own within a short grace period"),
            new("changelog.r0920.4", "You can now reopen an ongoing call from the Dynamic Island at the top of the screen"),
            new("changelog.r0920.5", "You can browse your chats and contacts during a call, and switch to another call without hanging up first"),
            new("changelog.r0920.6", "Merged the Chat, Contacts, and Find People apps into one in-game messaging app and renamed it Linkpearl, with a new pearl icon"),
            new("changelog.r0920.7", "Added a guided setup after the welcome screen that walks you through signing in, setting up your profile and photo, and choosing your analytics preference"),
            new("changelog.r0920.8", "Reporting now happens through one consistent popup everywhere, where you pick a category and add details, instead of a different form in each app"),
            new("changelog.r0920.9", "The welcome tour now points right at the real buttons and widgets on screen as it guides you around the phone"),
            new("changelog.r0920.10", "Links in your messages are now underlined and open in your browser when you click them"),
            new("changelog.r0920.11", "New automatic moderation reviews everything posted to the social apps and flags or removes anything inappropriate to keep the feeds safe"),
        };

        public static readonly LocString[] Release0910 =
        {
            new("changelog.r0910.0", "Your one-to-one and group chats in Messages, and your Velvet messages, are now end-to-end encrypted, so only you and the people you're talking to can read them. Not even the server can"),
            new("changelog.r0910.1", "Encryption is automatic: your key is created quietly the first time you sign in, with nothing to set up and no passphrase to remember"),
            new("changelog.r0910.2", "On a new computer a fresh key is created automatically, and older messages become readable again once your chat partners come online"),
            new("changelog.r0910.3", "Added an Encrypted Chats page in Settings to check your encryption status or reset your key"),
            new("changelog.r0910.4", "Encrypted messages show a small lock, and a banner lets you know when a conversation is end-to-end encrypted"),
            new("changelog.r0910.5", "You can now report a message: right-click it and choose Report. The message and a few before it are shared with the moderators, decrypted, so they can review it"),
            new("changelog.r0910.6", "Right-clicking a message now opens a quick menu to report it or copy its text"),
        };

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
        public static readonly LocString PhotoRejected = new("account.photoRejected", "That photo could not be saved. Try a different image.");
        public static readonly LocString Verifying = new("account.verifying", "Verifying via Lodestone…");
        public static readonly LocString SignOut = new("account.signOut", "Sign out");
        public static readonly LocString CharacterLabel = new("account.characterLabel", "Character");
        public static readonly LocString HomeWorldLabel = new("account.homeWorldLabel", "Home World");
        public static readonly LocString SignOutConfirmTitle = new("account.signOutConfirmTitle", "Sign out?");
        public static readonly LocString SignOutConfirmBody = new("account.signOutConfirmBody", "You can sign back in anytime. Your account and data stay safe.");
        public static readonly LocString DeleteAccount = new("account.deleteAccount", "Delete account");
        public static readonly LocString DeleteAccountHint = new("account.deleteAccountHint", "Permanently deletes your Aethernet account from the server: profile, posts, comments, messages, photos, and connections. The phone itself keeps working. This cannot be undone.");
        public static readonly LocString DeleteConfirmTitle = new("account.deleteConfirmTitle", "Delete your account?");
        public static readonly LocString DeleteConfirmBody = new("account.deleteConfirmBody", "This permanently erases your profile, posts, comments, messages, and photos from the Aetherphone servers. There is no way to get them back.");
        public static readonly LocString DeleteConfirmAction = new("account.deleteConfirmAction", "Delete forever");
        public static readonly LocString DeleteFailed = new("account.deleteFailed", "Deletion didn't go through. Check your connection and try again.");
        public static readonly LocString ChangePhoto = new("account.changePhoto", "Change Photo");
        public static readonly LocString ImportFromPc = new("account.importFromPc", "Import from PC");
        public static readonly LocString MoveAndScale = new("account.moveAndScale", "Move and Scale");
        public static readonly LocString Use = new("account.use", "Use");
        public static readonly LocString Saving = new("account.saving", "Saving…");
        public static readonly LocString GestureHint = new("account.gestureHint", "Drag to move · scroll to zoom");
        public static readonly LocString NameTitle = new("account.nameTitle", "Name and Username");
        public static readonly LocString NameHint = new("account.nameHint", "This is how you appear across every Aethernet app. Your character name and home world stay private.");
        public static readonly LocString DisplayNameLabel = new("account.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("account.handleLabel", "Username");
        public static readonly LocString HandleRules = new("account.handleRules", "3-15 characters: letters, numbers, or _");
        public static readonly LocString HandleTaken = new("account.handleTaken", "That username is taken");
        public static readonly LocString Save = new("account.save", "Save");
        public static readonly LocString SignIn = new("account.signIn", "Sign in with Lodestone");
        public static readonly LocString XivSignIn = new("account.xivSignIn", "Sign in with XIVAuth");
        public static readonly LocString LodestoneHint = new("account.lodestoneHint", "No XIVAuth account? Verify with a Lodestone code instead.");
        public static readonly LocString XivTitle = new("account.xivTitle", "Approve on XIVAuth");
        public static readonly LocString XivIntro = new("account.xivIntro", "We opened XIVAuth in your browser. Approve this device to finish signing in. If you're asked for a code, enter the one below.");
        public static readonly LocString XivWaiting = new("account.xivWaiting", "Waiting for approval…");
        public static readonly LocString XivOpen = new("account.xivOpen", "Open XIVAuth");
        public static readonly LocString XivConnecting = new("account.xivConnecting", "Connecting to XIVAuth…");
        public static readonly LocString PatreonSection = new("account.patreonSection", "Patreon");
        public static readonly LocString PatreonLink = new("account.patreonLink", "Link Patreon");
        public static readonly LocString PatreonHint = new("account.patreonHint", "Back Aetherphone on Patreon and link your account to unlock your member perks. Perks follow your membership automatically.");
        public static readonly LocString PatreonWaitingBody = new("account.patreonWaitingBody", "We opened Patreon in your browser. Approve the link there. This page updates on its own once it's done.");
        public static readonly LocString PatreonOpen = new("account.patreonOpen", "Open Patreon");
        public static readonly LocString PatreonStatusLabel = new("account.patreonStatusLabel", "Membership");
        public static readonly LocString PatreonLinkedActive = new("account.patreonLinkedActive", "Active");
        public static readonly LocString PatreonLinkedInactive = new("account.patreonLinkedInactive", "Not active");
        public static readonly LocString PatreonInactiveHint = new("account.patreonInactiveHint", "Your Patreon account is linked, but there's no active paid membership right now. Your perks unlock as soon as your membership is active.");
        public static readonly LocString PatreonUnlink = new("account.patreonUnlink", "Unlink Patreon");
        public static readonly LocString PatreonUnlinkTitle = new("account.patreonUnlinkTitle", "Unlink Patreon?");
        public static readonly LocString PatreonUnlinkBody = new("account.patreonUnlinkBody", "This disconnects your Patreon account and removes your Patreon member perks. You can link again any time.");
        public static readonly LocString PatreonLinkedTitle = new("account.patreonLinkedTitle", "Patreon linked");
        public static readonly LocString PatreonLinkedBody = new("account.patreonLinkedBody", "Your Patreon account is now connected. Your member perks follow your membership automatically.");
        public static readonly LocString PatreonFailedTitle = new("account.patreonFailedTitle", "Patreon link failed");
        public static readonly LocString PatreonFailedBody = new("account.patreonFailedBody", "We couldn't finish linking your Patreon account. Please try again.");
        public static readonly LocPlural Followers = new("account.followers", "{0} follower", "{0} followers");
        public static readonly LocString AccountsSection = new("account.accountsSection", "Accounts");
        public static readonly LocString BadgesSection = new("account.badgesSection", "Badges");
        public static readonly LocString BadgesHint = new("account.badgesHint", "Badges are granted by the Aetherphone team. Turning one off hides it from everyone, along with its name color and effects. Turn it back on whenever you like.");
        public static readonly LocString AddAccount = new("account.addAccount", "Add account");
        public static readonly LocString AddAccountTakenTitle = new("account.addAccountTakenTitle", "Already signed in here");
        public static readonly LocString AddAccountTakenBody = new("account.addAccountTakenBody", "{0} already has an account on this phone. To add another one, log in to that character in the game and sign in from there. It then stays in this list for every character.");
        public static readonly LocString FollowCharacter = new("account.followCharacter", "Follow current character");
        public static readonly LocString FollowCharacterHint = new("account.followCharacterHint", "On: the phone uses the account of whichever character you are playing. Off: the account you picked stays active on every character.");
        public static readonly LocString SwitchHint = new("account.switchHint", "Tap an account to use it right away. Apps, messages and your phone number follow the account you pick.");
        public static readonly LocString PlayingAs = new("account.playingAs", "Playing {0}, signed in as {1}");
        public static readonly LocString RemoveAccount = new("account.removeAccount", "Remove");
        public static readonly LocString RemoveAccountTitle = new("account.removeAccountTitle", "Remove {0}?");
        public static readonly LocString RemoveAccountBody = new("account.removeAccountBody", "This signs that account out on this phone and takes it off the list. The account itself and everything in it stay safe. Sign in from that character again whenever you want it back.");
        public static readonly LocString AltSignInTitle = new("account.altSignInTitle", "Not signed in on this character");
        public static readonly LocString AltSignInBody = new("account.altSignInBody", "You're now playing {0}. This character isn't signed in to Aethernet, so social apps, messaging, and calls stay empty until you sign in.");
        public static readonly LocString SignedOutTitle = new("account.signedOutTitle", "Signed out");
        public static readonly LocString SignedOutBody = new("account.signedOutBody", "Your Aethernet session ended, so social apps, messaging, and calls stay empty. Open Settings and sign in again to reconnect.");
        public static readonly LocString FailDismiss = new("account.fail.dismiss", "Got it");
        public static readonly LocString FailCharacterNotFoundTitle = new("account.fail.characterNotFound.title", "Character not found");
        public static readonly LocString FailCharacterNotFoundBody = new("account.fail.characterNotFound.body", "We couldn't find {0} on {1} in the Lodestone search. Brand-new characters can take up to a day to appear. In your Character settings, set Character Search to Public, then Verify again. Characters on Chinese or Korean servers aren't on the international Lodestone yet, so they can't be verified.");
        public static readonly LocString FailCodeNotFoundTitle = new("account.fail.codeNotFound.title", "Code not saved yet");
        public static readonly LocString FailCodeNotFoundBody = new("account.fail.codeNotFound.body", "We found your character, but the code isn't in your profile yet. Lodestone can take a minute to update after you save. Wait a moment, then Verify again. If it keeps happening, press Cancel below and try again with a new code.");
        public static readonly LocString FailLodestoneUnavailableTitle = new("account.fail.lodestoneUnavailable.title", "Lodestone unavailable");
        public static readonly LocString FailLodestoneUnavailableBody = new("account.fail.lodestoneUnavailable.body", "We couldn't read the Lodestone just now: it sometimes limits lookups during busy hours. This is on Square Enix's side, not yours. Keep the code in your profile and Verify again in a minute or two.");
        public static readonly LocString FailTimeoutTitle = new("account.fail.timeout.title", "Verification timed out");
        public static readonly LocString FailTimeoutBody = new("account.fail.timeout.body", "The Lodestone took too long to respond. Your code is fine, just Verify again in a moment.");
        public static readonly LocString FailChallengeExpiredTitle = new("account.fail.challengeExpired.title", "Code expired");
        public static readonly LocString FailChallengeExpiredBody = new("account.fail.challengeExpired.body", "This sign-in code expired. Start again to get a fresh one.");
        public static readonly LocString FailBannedTitle = new("account.fail.banned.title", "Character blocked");
        public static readonly LocString FailBannedBody = new("account.fail.banned.body", "This character can't sign in to Aethernet. Reach out to support if you think this is a mistake.");
        public static readonly LocString BanScreenTitle = new("account.ban.title", "Account suspended");
        public static readonly LocString BanScreenBody = new("account.ban.body", "This character has been banned from Aethernet and can no longer sign in.");
        public static readonly LocString BanScreenReason = new("account.ban.reason", "Reason: {0}");
        public static readonly LocString BanScreenTimeoutTitle = new("account.ban.timeoutTitle", "Account suspended for now");
        public static readonly LocString BanScreenLifts = new("account.ban.lifts", "You can sign in again after {0}. Nothing you posted was deleted by this suspension.");
        public static readonly LocString BanScreenContact = new("account.ban.contact", "If you believe this is a mistake, contact support.");
        public static readonly LocString BanScreenSocialLocked = new("account.ban.socialLocked", "Social apps are locked, but the rest of the phone still works.");
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

    internal static class Encryption
    {
        public static readonly LocString Title = new("encryption.title", "Encrypted Chats");
        public static readonly LocString StateActive = new("encryption.stateActive", "Active");
        public static readonly LocString StateSettingUp = new("encryption.stateSettingUp", "Setting up…");
        public static readonly LocString StateUnavailable = new("encryption.stateUnavailable", "Sign in required");
        public static readonly LocString StateUnsupported = new("encryption.stateUnsupported", "Unavailable on this PC");
        public static readonly LocString StateLocked = new("encryption.stateLocked", "Locked on this device");
        public static readonly LocString Intro = new("encryption.intro", "End-to-end encryption keeps your chats between you and the people you write to. Not even the Aethernet server can read them.");
        public static readonly LocString NotSignedIn = new("encryption.notSignedIn", "Sign in to your Aethernet account first.");
        public static readonly LocString UnsupportedBody = new("encryption.unsupportedBody", "This computer cannot create the security key that encrypted chats need, so Messages and Velvet chats stay unavailable here. This usually happens when the game runs through Wine or Proton. The rest of Aetherphone works normally.");
        public static readonly LocString SettingUp = new("encryption.settingUp", "Setting up encryption…");
        public static readonly LocString UnsupportedSummary = new("encryption.unsupportedSummary", "This PC cannot set up encryption, so messages here are not encrypted.");
        public static readonly LocString ActiveHint = new("encryption.activeHint", "Encryption is active on this device. It works automatically. There is nothing to set up.");
        public static readonly LocString NewDeviceHint = new("encryption.newDeviceHint", "On a new computer a fresh key is created automatically. Older messages become readable again once your chat partners come online.");
        public static readonly LocString LocalStoreUnavailable = new("encryption.localStoreUnavailable", "This PC can't store the encryption key securely, so a fresh key will be created each session. Older messages become readable again once your chat partners come online.");
        public static readonly LocString LockedBody = new("encryption.lockedBody", "This device doesn't have the encryption key for this account, so messages here can't be read yet. This usually happens after switching to a different computer. Your messages are safe: open Aetherphone on the computer that already has your key, or create a new key here. If you create a new key, older messages become readable again once your chat partners come online.");
        public static readonly LocString NewKeyButton = new("encryption.newKeyButton", "Create a new key on this device…");
        public static readonly LocString LockedRecoverBody = new("encryption.lockedRecoverBody", "This device doesn't have your encryption key yet. Enter the recovery code you saved to restore your chats here, with your full history.");
        public static readonly LocString LockedBanner = new("encryption.lockedBanner", "Chats are locked on this device. Tap to unlock.");
        public static readonly LocString RecoveryNudgeBanner = new("encryption.recoveryNudgeBanner", "Protect your chat history: set up a recovery code");
        public static readonly LocString RecoverySectionTitle = new("encryption.recoverySectionTitle", "Recovery code");
        public static readonly LocString RecoveryNotSetBody = new("encryption.recoveryNotSetBody", "Set up a recovery code so you can restore your chats if you reinstall or move to another PC. Without it, chats on a new PC start fresh.");
        public static readonly LocString RecoverySetupButton = new("encryption.recoverySetupButton", "Set up recovery code…");
        public static readonly LocString RecoveryConfiguredBody = new("encryption.recoveryConfiguredBody", "A recovery code is set up for this account. Keep it somewhere safe: it's what unlocks your chats on another PC.");
        public static readonly LocString RecoveryRegenerateButton = new("encryption.recoveryRegenerateButton", "Create a new recovery code…");
        public static readonly LocString RecoverySaveTitle = new("encryption.recoverySaveTitle", "Save your recovery code");
        public static readonly LocString RecoverySaveBody = new("encryption.recoverySaveBody", "This is the only way to restore your chats on another PC, and it can't be shown again. Keep it somewhere safe and private: anyone with this code can read your chats.");
        public static readonly LocString RecoveryCopy = new("encryption.recoveryCopy", "Copy code");
        public static readonly LocString RecoverySavedButton = new("encryption.recoverySavedButton", "I've saved it");
        public static readonly LocString RecoveryCodeLabel = new("encryption.recoveryCodeLabel", "Recovery code");
        public static readonly LocString RecoveryUnlockButton = new("encryption.recoveryUnlockButton", "Unlock my chats");
        public static readonly LocString RecoveryWrongCode = new("encryption.recoveryWrongCode", "That code didn't work. Check it and try again.");
        public static readonly LocString ForgotBody = new("encryption.forgotBody", "A new key will be created. Messages encrypted with the old key become readable again once your chat partners come online.");
        public static readonly LocString ForgotConfirm = new("encryption.forgotConfirm", "Reset key");
        public static readonly LocString ResetButton = new("encryption.resetButton", "Reset encryption key…");
        public static readonly LocString KeyVersion = new("encryption.keyVersion", "Key version {0}");
        public static readonly LocString Working = new("encryption.working", "Working…");
        public static readonly LocString Failed = new("encryption.failed", "Something went wrong. Try again.");
        public static readonly LocString EncryptedPlaceholder = new("encryption.encryptedPlaceholder", "Encrypted message");
        public static readonly LocString NoKeyPlaceholder = new("encryption.noKeyPlaceholder", "Can't decrypt this message");
        public static readonly LocString SafetyChanged = new("encryption.safetyChanged", "{0}'s security key changed.");
        public static readonly LocString EncryptedIndicator = new("encryption.encryptedIndicator", "End-to-end encrypted");
        public static readonly LocString PlaintextIndicator = new("encryption.plaintextIndicator", "Not encrypted");
        public static readonly LocString ReportDisclosure = new("encryption.reportDisclosure", "This message and up to 5 previous messages, including photos and voice notes, will be shared with the moderators, decrypted.");
        public static readonly LocString ReportMessageAction = new("encryption.reportMessageAction", "Report message");
        public static readonly LocString CopyTextAction = new("encryption.copyTextAction", "Copy text");
        public static readonly LocString InfoTitle = new("encryption.infoTitle", "Encryption");
        public static readonly LocString WaitingMembers = new("encryption.waitingMembers", "{0} can't receive encrypted messages yet. Messages stay unencrypted until everyone has an encryption key.");
        public static readonly LocString SecurityCode = new("encryption.securityCode", "Security code");
        public static readonly LocString SecurityCodeHint = new("encryption.securityCodeHint", "Compare this code with {0}. If both phones show the same code, this chat is end-to-end encrypted.");
        public static readonly LocString SecurityCodeUnavailable = new("encryption.securityCodeUnavailable", "The security code appears once both of you have encryption keys.");
        public static readonly LocString CopyCode = new("encryption.copyCode", "Copy code");
        public static readonly LocString MemberReady = new("encryption.memberReady", "Ready for encryption");
        public static readonly LocString MemberNoKey = new("encryption.memberNoKey", "No encryption key yet");
    }

    internal static class Report
    {
        public static readonly LocString Action = new("report.action", "Report");
        public static readonly LocString PostTitle = new("report.postTitle", "Report post");
        public static readonly LocString UserTitle = new("report.userTitle", "Report user");
        public static readonly LocString CategoryHint = new("report.categoryHint", "Select a reason");
        public static readonly LocString CategorySpam = new("report.categorySpam", "Spam");
        public static readonly LocString CategoryHarassment = new("report.categoryHarassment", "Harassment or bullying");
        public static readonly LocString CategoryHateSpeech = new("report.categoryHateSpeech", "Hate speech");
        public static readonly LocString CategoryInappropriate = new("report.categoryInappropriate", "Inappropriate content");
        public static readonly LocString CategoryImpersonation = new("report.categoryImpersonation", "Impersonation");
        public static readonly LocString CategoryScam = new("report.categoryScam", "Scam or fraud");
        public static readonly LocString CategoryOther = new("report.categoryOther", "Something else");
        public static readonly LocString DetailsHint = new("report.detailsHint", "Add details (optional)");
        public static readonly LocString Submit = new("report.submit", "Report");
        public static readonly LocString Sending = new("report.sending", "Sending…");
        public static readonly LocString Sent = new("report.sent", "Report submitted. Thank you.");
        public static readonly LocString Failed = new("report.failed", "Couldn't submit the report");
    }

    internal static class Share
    {
        public static readonly LocString Action = new("share.action", "Share");
        public static readonly LocString Title = new("share.title", "Share to");
        public static readonly LocString SetAsWallpaper = new("share.setAsWallpaper", "Set as wallpaper");
        public static readonly LocString NoTargets = new("share.noTargets", "No apps can open this yet");
    }

    internal static class Music
    {
        public static readonly LocString RadioStations = new("music.radioStations", "Radio stations");
        public static readonly LocString RecentlyPlayed = new("music.recentlyPlayed", "Recently played");
        public static readonly LocString TuningIn = new("music.tuningIn", "Tuning in…");
        public static readonly LocString NoStations = new("music.noStations", "No stations found");
        public static readonly LocString NoResults = new("music.noResults", "No results");
        public static readonly LocString NoResultsSub = new("music.noResultsSub", "Check the spelling or try different keywords");
        public static readonly LocString SearchEmptyTitle = new("music.searchEmptyTitle", "Play what you love");
        public static readonly LocString SearchEmptySub = new("music.searchEmptySub", "Search for songs and artists");
        public static readonly LocString SearchSongs = new("music.searchSongs", "Search songs");
        public static readonly LocString ScopeSongs = new("music.scopeSongs", "Songs");
        public static readonly LocString ScopeLongPlays = new("music.scopeLongPlays", "Long plays");
        public static readonly LocString ScopeAll = new("music.scopeAll", "All");
        public static readonly LocString SortPopular = new("music.sortPopular", "Popular");
        public static readonly LocString SortTrending = new("music.sortTrending", "Trending");
        public static readonly LocString SortTopVoted = new("music.sortTopVoted", "Top voted");
        public static readonly LocString SortName = new("music.sortName", "Name");
        public static readonly LocString SortBitrate = new("music.sortBitrate", "Bitrate");
        public static readonly LocString FilterCountry = new("music.filterCountry", "Country");
        public static readonly LocString FilterLanguage = new("music.filterLanguage", "Language");
        public static readonly LocString AllCountries = new("music.allCountries", "All countries");
        public static readonly LocString AllLanguages = new("music.allLanguages", "All languages");
        public static readonly LocString LiveLower = new("music.liveLower", "live");
        public static readonly LocString Buffering = new("music.buffering", "Buffering…");
        public static readonly LocString Paused = new("music.paused", "Paused");
        public static readonly LocString ConnectionLost = new("music.connectionLost", "Connection lost");
        public static readonly LocString Reconnecting = new("music.reconnecting", "Reconnecting…");
        public static readonly LocString CommunityRadio = new("music.communityRadio", "Community Radio");
        public static readonly LocString CommunityEmpty = new("music.communityEmpty", "No community stations yet");
        public static readonly LocString CommunityEmptySub = new("music.communityEmptySub",
            "When someone opens a station, it shows up here");
        public static readonly LocString ListeningCount = new("music.listeningCount", "{0} listening");
        public static readonly LocString WatchOnTwitch = new("music.watchOnTwitch", "Watch on Twitch");
        public static readonly LocString OffAir = new("music.offAir", "Off air");
        public static readonly LocString HostedBy = new("music.hostedBy", "Hosted by {0}");
        public static readonly LocString ListenLive = new("music.listenLive", "Listen live");
        public static readonly LocString StopListening = new("music.stopListening", "Stop");
        public static readonly LocString ReportStation = new("music.reportStation", "Report station");
        public static readonly LocString ReportStationTitle = new("music.reportStationTitle",
            "Report this station");
        public static readonly LocString FollowStation = new("music.followStation", "Follow");
        public static readonly LocString FollowingStation = new("music.followingStation", "Following");
        public static readonly LocPlural StationFollowers =
            new("music.stationFollowers", "{0} follower", "{0} followers");
        public static readonly LocString NotifLiveBody = new("music.notifLiveBody", "is live on {0}");
        public static readonly LocString NotifLiveGeneric = new("music.notifLiveGeneric", "is live now");
        public static readonly LocString MyStation = new("music.myStation", "My station");
        public static readonly LocString StationArtwork = new("music.stationArtwork", "Artwork");
        public static readonly LocString OnAir = new("music.onAir", "On air");
        public static readonly LocString StationNameLabel = new("music.stationNameLabel", "Station name");
        public static readonly LocString StationDescriptionLabel = new("music.stationDescriptionLabel", "Description");
        public static readonly LocString StationLinksLabel = new("music.stationLinksLabel", "Links");
        public static readonly LocString StationTagsLabel =
            new("music.stationTagsLabel", "Tags (up to 5, separated by commas)");
        public static readonly LocString StationTagsHint = new("music.stationTagsHint", "lofi, jazz, chill");
        public static readonly LocString AllTags = new("music.allTags", "All");
        public static readonly LocString NextBroadcast = new("music.nextBroadcast", "Next broadcast {0}");
        public static readonly LocString ScheduleLabel = new("music.scheduleLabel", "Next broadcast");
        public static readonly LocString ScheduleNone = new("music.scheduleNone", "No broadcast scheduled");
        public static readonly LocString ScheduleRepeat = new("music.scheduleRepeat", "Repeat weekly");
        public static readonly LocString ScheduleClear = new("music.scheduleClear", "Clear");
        public static readonly LocString CommunityMatches = new("music.communityMatches", "Community stations");
        public static readonly LocString StationSave = new("music.stationSave", "Save changes");
        public static readonly LocString StationSaved = new("music.stationSaved", "Saved");
        public static readonly LocString StationSaveFailed = new("music.stationSaveFailed", "Could not save");
        public static readonly LocString StationBroadcast = new("music.stationBroadcast", "Broadcast settings");
        public static readonly LocString StationServer = new("music.stationServer", "Server");
        public static readonly LocString StationPort = new("music.stationPort", "Port");
        public static readonly LocString StationMount = new("music.stationMount", "Mount");
        public static readonly LocString StationUser = new("music.stationUser", "User");
        public static readonly LocString StationPassword = new("music.stationPassword", "Password");
        public static readonly LocString StationFormat = new("music.stationFormat", "Format");
        public static readonly LocString StationCopied = new("music.stationCopied", "Copied");
        public static readonly LocString StationHelp = new("music.stationHelp",
            "Paste these into butt, or Rocket Broadcaster if you want to stream your desktop audio. "
            + "MP3 only: other formats connect fine and reach listeners as silence.");
        public static readonly LocString CouldntPlay = new("music.couldntPlay", "Couldn't play this track");
        public static readonly LocString NowPlayingState = new("music.nowPlayingState", "Now playing");
        public static readonly LocString PlaybackFailed = new("music.playbackFailed", "Playback failed");
        public static readonly LocString Repeat = new("music.repeat", "Repeat");
        public static readonly LocString GoodMorning = new("music.goodMorning", "Good morning");
        public static readonly LocString GoodAfternoon = new("music.goodAfternoon", "Good afternoon");
        public static readonly LocString GoodEvening = new("music.goodEvening", "Good evening");
        public static readonly LocString MadeForYou = new("music.madeForYou", "Made for you");
        public static readonly LocString PlayingFrom = new("music.playingFrom", "Playing from");
        public static readonly LocString SourceSearch = new("music.sourceSearch", "Search results");
        public static readonly LocString SourceRadioSearch = new("music.sourceRadioSearch", "Radio search");
        public static readonly LocString SearchStations = new("music.searchStations", "Search stations");
        public static readonly LocString RadioSearchTitle = new("music.radioSearchTitle", "Find your station");
        public static readonly LocString RadioSearchSub = new("music.radioSearchSub", "Search by name, genre, or country");
        public static readonly LocString YourPlaylists = new("music.yourPlaylists", "Your playlists");
        public static readonly LocString AddToPlaylist = new("music.addToPlaylist", "Add to playlist");
        public static readonly LocString AddFavoriteStation = new("music.addFavoriteStation", "Add to Favorites");
        public static readonly LocString RemoveFavoriteStation = new("music.removeFavoriteStation", "Remove from Favorites");
        public static readonly LocString FavoriteStations = new("music.favoriteStations", "Favorite stations");
        public static readonly LocString NewPlaylist = new("music.newPlaylist", "New playlist");
        public static readonly LocString PlaylistNameHint = new("music.playlistNameHint", "Playlist name");
        public static readonly LocString CreatePlaylist = new("music.createPlaylist", "Create");
        public static readonly LocString RenamePlaylist = new("music.renamePlaylist", "Rename");
        public static readonly LocString DeletePlaylist = new("music.deletePlaylist", "Delete playlist");
        public static readonly LocString DeletePlaylistButton = new("music.deletePlaylistButton", "Delete");
        public static readonly LocString DeletePlaylistConfirm = new("music.deletePlaylistConfirm", "Delete this playlist? This cannot be undone.");
        public static readonly LocString PlayAll = new("music.playAll", "Play all");
        public static readonly LocString PlaylistEmptyTitle = new("music.playlistEmptyTitle", "No songs yet");
        public static readonly LocString PlaylistEmptySub = new("music.playlistEmptySub", "Add songs from search or while you listen");
        public static readonly LocString NoPlaylistsYet = new("music.noPlaylistsYet", "No playlists yet");
        public static readonly LocString SongOne = new("music.songOne", "1 song");
        public static readonly LocString SongsMany = new("music.songsMany", "{0} songs");
    }

    internal static class Messages
    {
        public static readonly LocString Empty = new("messages.empty", "No messages yet");
        public static readonly LocString Placeholder = new("messages.placeholder", "Message");
        public static readonly LocString TabChats = new("messages.tabChats", "Chats");
        public static readonly LocString TabDirect = new("messages.tabDirect", "Direct");
        public static readonly LocString TabLinkshells = new("messages.tabLinkshells", "Linkshells");
        public static readonly LocString LinkshellsEmpty = new("messages.linkshellsEmpty", "No linkshell chatter yet");
        public static readonly LocString DeleteHistoryConfirm = new("messages.deleteHistoryConfirm", "Delete this conversation history? This can't be undone.");
        public static readonly LocString DeleteHistoryButton = new("messages.deleteHistoryButton", "Delete");
        public static readonly LocString DeleteHistoryCancel = new("messages.deleteHistoryCancel", "Cancel");
        public static readonly LocString Linkshell = new("messages.linkshell", "Linkshell {0}");
        public static readonly LocString CrossWorldLinkshell = new("messages.crossWorldLinkshell", "Crossworld Linkshell {0}");
        public static readonly LocString Mute = new("messages.mute", "Mute");
        public static readonly LocString Unmute = new("messages.unmute", "Unmute");
        public static readonly LocString PauseNotifications = new("messages.pauseNotifications", "Pause notifications");
        public static readonly LocString ResumeNotifications = new("messages.resumeNotifications", "Resume notifications");
        public static readonly LocString CopyMessage = new("messages.copyMessage", "Copy message");
        public static readonly LocString CopyName = new("messages.copyName", "Copy name");
        public static readonly LocString SendFailed = new("messages.sendFailed", "Couldn't send that message. Check for unsupported characters.");
    }

    internal static class Character
    {
        public static readonly LocString LogInToView = new("character.logInToView", "Log in to view your character");
        public static readonly LocString Activity = new("character.activity", "Activity");
        public static readonly LocString Today = new("character.today", "Today");
        public static readonly LocString ThisSession = new("character.thisSession", "This session");
        public static readonly LocString RingProgress = new("character.ringProgress", "Progress");
        public static readonly LocString RingAdventure = new("character.ringAdventure", "Adventure");
        public static readonly LocString RingFortune = new("character.ringFortune", "Fortune");
        public static readonly LocString Experience = new("character.experience", "Experience");
        public static readonly LocString Duties = new("character.duties", "Duties");
        public static readonly LocString GilEarned = new("character.gilEarned", "Gil earned");
        public static readonly LocString TimePlayed = new("character.timePlayed", "Time played");
        public static readonly LocString NewCollectibles = new("character.newCollectibles", "New collectibles");
        public static readonly LocString LevelsGained = new("character.levelsGained", "{0} levels gained");
        public static readonly LocString PercentOfGoal = new("character.percentOfGoal", "{0}% of goal");
        public static readonly LocString Mounts = new("character.mounts", "Mounts");
        public static readonly LocString Minions = new("character.minions", "Minions");
        public static readonly LocString Retainers = new("character.retainers", "Retainers");
        public static readonly LocString VenturesReady = new("character.venturesReady", "{0} ready");
        public static readonly LocString VenturesActive = new("character.venturesActive", "{0} running");
        public static readonly LocString GoalsSection = new("character.goalsSection", "Daily goals");
        public static readonly LocString GoalLevels = new("character.goalLevels", "Level progress");
        public static readonly LocString LevelsShort = new("character.levelsShort", "{0} Lv");
        public static readonly LocString GoalsHint = new("character.goalsHint", "Rings close when you reach these goals. Progress resets at midnight.");
        public static readonly LocString DurationHoursMinutes = new("character.durationHoursMinutes", "{0}h {1}m");
        public static readonly LocString DurationMinutes = new("character.durationMinutes", "{0}m");
        public static readonly LocString History = new("character.history", "History");
        public static readonly LocString ThisWeek = new("character.thisWeek", "This week");
        public static readonly LocString Streaks = new("character.streaks", "Streaks");
        public static readonly LocString CurrentStreak = new("character.currentStreak", "Current streak");
        public static readonly LocString BestStreak = new("character.bestStreak", "Best streak");
        public static readonly LocPlural StreakDays = new("character.streakDays", "{0} day", "{0} days");
        public static readonly LocString StreaksHint = new("character.streaksHint", "A day counts toward your streak when all three rings close.");
        public static readonly LocString PersonalBests = new("character.personalBests", "Personal bests");
        public static readonly LocString RingClosedBody = new("character.ringClosedBody", "You reached today's goal.");
        public static readonly LocString AllRingsTitle = new("character.allRingsTitle", "All rings closed");
        public static readonly LocString AllRingsBody = new("character.allRingsBody", "You hit all three goals today. Perfect day!");
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
        public static readonly LocString SearchInfo = new("contacts.searchInfo", "Search Info");
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
        public static readonly LocString Save = new("chirper.save", "Save");
        public static readonly LocString Saving = new("chirper.saving", "Saving…");
        public static readonly LocString HandleTaken = new("chirper.handleTaken", "That username is taken");
        public static readonly LocString HandleRules = new("chirper.handleRules", "3-15 characters: letters, numbers, or _");
        public static readonly LocString ProfileError = new("chirper.profileError", "Couldn't load this profile");
        public static readonly LocString React = new("chirper.react", "React");
        public static readonly LocString ReactLike = new("chirper.reactLike", "Like");
        public static readonly LocString ReactLove = new("chirper.reactLove", "Love");
        public static readonly LocString ReactLaugh = new("chirper.reactLaugh", "Haha");
        public static readonly LocString ReactWow = new("chirper.reactWow", "Wow");
        public static readonly LocString ReactSad = new("chirper.reactSad", "Sad");
        public static readonly LocString ReactAngry = new("chirper.reactAngry", "Angry");
        public static readonly LocString ReactFire = new("chirper.reactFire", "Fire");
        public static readonly LocString ReactSkull = new("chirper.reactSkull", "Skull");
        public static readonly LocString ReactSob = new("chirper.reactSob", "Sob");
        public static readonly LocString ReactBomb = new("chirper.reactBomb", "Bomb");
        public static readonly LocString ReactEyes = new("chirper.reactEyes", "Eyes");
        public static readonly LocString ReactHundred = new("chirper.reactHundred", "100");
        public static readonly LocString ReactQuestion = new("chirper.reactQuestion", "Question");
        public static readonly LocPlural Posts = new("chirper.posts", "{0} post", "{0} posts");
        public static readonly LocPlural Likes = new("chirper.likes", "{0} like", "{0} likes");
        public static readonly LocString DeleteConfirmMessage = new("chirper.deleteConfirmMessage", "Delete this post? This can't be undone.");
        public static readonly LocString DeleteCommentConfirmMessage = new("chirper.deleteCommentConfirmMessage", "Delete this comment? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("chirper.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("chirper.deleteCancel", "Cancel");
        public static readonly LocString DeleteFailed = new("chirper.deleteFailed", "Couldn't delete the post");
        public static readonly LocString DeleteCommentFailed = new("chirper.deleteCommentFailed", "Couldn't delete the comment");
        public static readonly LocString DeleteComment = new("chirper.deleteComment", "Delete comment");
        public static readonly LocString PostTitle = new("chirper.postTitle", "Post");
        public static readonly LocString NoComments = new("chirper.noComments", "No replies yet. Start the conversation");
        public static readonly LocString EarlierComments = new("chirper.earlierComments", "View earlier replies");
        public static readonly LocString AddComment = new("chirper.addComment", "Add a reply…");
        public static readonly LocString RepliesTitle = new("chirper.repliesTitle", "Replies");
        public static readonly LocString ChirpsTitle = new("chirper.chirpsTitle", "Chirps");
        public static readonly LocString Reply = new("chirper.reply", "Reply");
        public static readonly LocString More = new("chirper.more", "More");
        public static readonly LocString Repost = new("chirper.repost", "Rechirp");
        public static readonly LocString Unrepost = new("chirper.unrepost", "Undo rechirp");
        public static readonly LocString Reposted = new("chirper.reposted", "{0} rechirped");
        public static readonly LocString Quote = new("chirper.quote", "Quote");
        public static readonly LocString QuoteTitle = new("chirper.quoteTitle", "Quote Chirp");
        public static readonly LocString Unavailable = new("chirper.unavailable", "This chirp is unavailable");
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
        public static readonly LocString NoComments = new("aethergram.noComments", "No comments yet");
        public static readonly LocString EarlierComments = new("aethergram.earlierComments", "View earlier comments");
        public static readonly LocString AddComment = new("aethergram.addComment", "Add a comment…");
        public static readonly LocString ProfileError = new("aethergram.profileError", "Couldn't load this profile");
        public static readonly LocString EditProfile = new("aethergram.editProfile", "Edit Profile");
        public static readonly LocString ChangePhoto = new("aethergram.changePhoto", "Change Photo");
        public static readonly LocString DisplayNameLabel = new("aethergram.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("aethergram.handleLabel", "Username");
        public static readonly LocString BioLabel = new("aethergram.bioLabel", "Bio");
        public static readonly LocString HandleRules = new("aethergram.handleRules", "3-15 characters: letters, numbers, or _");
        public static readonly LocString HandleTaken = new("aethergram.handleTaken", "That username is taken");
        public static readonly LocString Save = new("aethergram.save", "Save");
        public static readonly LocString FindPeople = new("aethergram.findPeople", "Find People");
        public static readonly LocString SearchByName = new("aethergram.searchByName", "Search by name, @username, or world");
        public static readonly LocString NameOrWorld = new("aethergram.nameOrWorld", "Name, @username, or world");
        public static readonly LocPlural Posts = new("aethergram.posts", "{0} post", "{0} posts");
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
        public static readonly LocString Profile = new("aethergram.profile", "Profile");
        public static readonly LocString InboxTitle = new("aethergram.inboxTitle", "Messages");
        public static readonly LocString InboxEmpty = new("aethergram.inboxEmpty", "No messages yet");
        public static readonly LocString InboxEmptyHint = new("aethergram.inboxEmptyHint", "Message someone from their profile");
        public static readonly LocString MessageButton = new("aethergram.message", "Message");
        public static readonly LocString ThreadEmpty = new("aethergram.threadEmpty", "Say hello");
        public static readonly LocString Settings = new("aethergram.settings", "Settings");
        public static readonly LocString ChatsTab = new("aethergram.chatsTab", "Chats");
        public static readonly LocString Requests = new("aethergram.requests", "Requests");
        public static readonly LocString RequestsCount = new("aethergram.requestsCount", "Requests ({0})");
        public static readonly LocString RequestsEmpty = new("aethergram.requestsEmpty", "No message requests");
        public static readonly LocString RequestBanner = new("aethergram.requestBanner", "{0} wants to send you messages");
        public static readonly LocString AcceptRequest = new("aethergram.acceptRequest", "Accept");
        public static readonly LocString DeleteConversation = new("aethergram.deleteConversation", "Delete conversation");
        public static readonly LocString DeleteConversationMessage = new("aethergram.deleteConversationMessage",
            "This deletes the conversation for you. This can't be undone.");
        public static readonly LocString SendTo = new("aethergram.sendTo", "Send to");
        public static readonly LocString SharedPost = new("aethergram.sharedPost", "Shared a post");
        public static readonly LocString PostUnavailable = new("aethergram.postUnavailable", "Post unavailable");
        public static readonly LocString Send = new("aethergram.send", "Send");
        public static readonly LocString Sent = new("aethergram.sent", "Sent");
        public static readonly LocString ReplyToStory = new("aethergram.replyToStory", "Reply to {0}");
        public static readonly LocString RepliedToYourStory = new("aethergram.repliedToYourStory", "Replied to your story");
        public static readonly LocString YouRepliedToStory = new("aethergram.youRepliedToStory", "You replied to their story");
        public static readonly LocString StoryUnavailable = new("aethergram.storyUnavailable", "Story unavailable");
        public static readonly LocString PrivateTitle = new("aethergram.privateTitle", "This account is private");
        public static readonly LocString PrivateSubtitle = new("aethergram.privateSubtitle", "Follow this account to see their photos");
        public static readonly LocString SavedTitle = new("aethergram.savedTitle", "Saved");
        public static readonly LocString SavedEmpty = new("aethergram.savedEmpty", "Nothing saved yet");
        public static readonly LocString PrivateAccount = new("aethergram.privateAccount", "Private account");
        public static readonly LocString PrivateAccountHint = new("aethergram.privateAccountHint", "Only followers can see your photos and stories. New followers must send a request.");
    }

    internal static class Velvet
    {
        public static readonly LocString GateLeave = new("velvet.gateLeave", "Not now");
        public static readonly LocString GateWorking = new("velvet.gateWorking", "One moment…");
        public static readonly LocString TabDiscover = new("velvet.tabDiscover", "Discover");
        public static readonly LocString TabFeed = new("velvet.tabFeed", "Feed");
        public static readonly LocString SearchPeopleHint = new("velvet.searchPeopleHint", "Search by name or tag");
        public static readonly LocString Messages = new("velvet.messages", "Messages");
        public static readonly LocString MessagesEmpty = new("velvet.messagesEmpty", "No conversations yet");
        public static readonly LocString ThreadEmpty = new("velvet.threadEmpty", "Say hello");
        public static readonly LocString Connect = new("velvet.connect", "Connect");
        public static readonly LocString Requested = new("velvet.requested", "Requested");
        public static readonly LocString Message = new("velvet.message", "Message");
        public static readonly LocString MessageHint = new("velvet.messageHint", "Write a message…");
        public static readonly LocString Send = new("velvet.send", "Send");
        public static readonly LocString LookingForLabel = new("velvet.lookingForLabel", "Looking for");
        public static readonly LocString RegionLabel = new("velvet.regionLabel", "Region");
        public static readonly LocString RegionAny = new("velvet.regionAny", "Any");
        public static readonly LocString PresenceOnline = new("velvet.presenceOnline", "Online");
        public static readonly LocString PresenceAway = new("velvet.presenceAway", "Away");
        public static readonly LocString PresenceDnd = new("velvet.presenceDnd", "Do not disturb");
        public static readonly LocString PresenceOffline = new("velvet.presenceOffline", "Offline");
        public static readonly LocString EditProfile = new("velvet.editProfile", "Edit profile");
        public static readonly LocString PronounsLabel = new("velvet.pronounsLabel", "Pronouns");
        public static readonly LocString DynamicLabel = new("velvet.dynamicLabel", "Your vibe");
        public static readonly LocString DiscoverableLabel = new("velvet.discoverableLabel", "Appear in Discover");
        public static readonly LocString Save = new("velvet.save", "Save");
        public static readonly LocString Saving = new("velvet.saving", "Saving…");
        public static readonly LocString SaveFailed = new("velvet.saveFailed", "Couldn't save your profile. Check your connection and try again.");
        public static readonly LocString NewPost = new("velvet.newPost", "New Post");
        public static readonly LocString Share = new("velvet.share", "Share");
        public static readonly LocString CaptionHint = new("velvet.captionHint", "Write a caption…");
        public static readonly LocString Block = new("velvet.block", "Block");
        public static readonly LocString NotInterested = new("velvet.notInterested", "Not interested");
        public static readonly LocString DiscardEdits =
            new("velvet.discardEdits", "You have changes you have not saved yet. Leave without saving them?");
        public static readonly LocString DiscardEditsConfirm = new("velvet.discardEditsConfirm", "Discard changes");
        public static readonly LocString KeepEditing = new("velvet.keepEditing", "Keep editing");
        public static readonly LocString Blocked = new("velvet.blocked", "Blocked");
        public static readonly LocString Unblock = new("velvet.unblock", "Unblock");
        public static readonly LocString Like = new("velvet.like", "Like");
        public static readonly LocString Comments = new("velvet.comments", "Comments");
        public static readonly LocString NoComments = new("velvet.noComments", "No comments yet. Say something.");
        public static readonly LocString AddComment = new("velvet.addComment", "Add a comment…");
        public static readonly LocString DeleteConfirmMessage = new("velvet.deleteConfirmMessage", "Delete this post? This can't be undone.");
        public static readonly LocString DeleteConfirm = new("velvet.deleteConfirm", "Delete");
        public static readonly LocString DeleteCancel = new("velvet.deleteCancel", "Cancel");
        public static readonly LocString DeleteFailed = new("velvet.deleteFailed", "Couldn't delete the post");
        public static readonly LocString DeleteCommentConfirmMessage = new("velvet.deleteCommentConfirmMessage", "Delete this comment? This can't be undone.");
        public static readonly LocString DeleteCommentFailed = new("velvet.deleteCommentFailed", "Couldn't delete the comment");
        public static readonly LocString TabMe = new("velvet.tabMe", "Me");
        public static readonly LocString Settings = new("velvet.settings", "Settings");
        public static readonly LocString OnboardIntent = new("velvet.onboardIntent", "What brings you here?");
        public static readonly LocString Back = new("velvet.back", "Back");
        public static readonly LocString EnterVelvet = new("velvet.enterVelvet", "Enter Velvet");
        public static readonly LocString Requests = new("velvet.requests", "Requests");
        public static readonly LocString Accept = new("velvet.accept", "Accept");
        public static readonly LocString WantsToConnect = new("velvet.wantsToConnect", "wants to connect");
        public static readonly LocString SentRequests = new("velvet.sentRequests", "Sent");
        public static readonly LocString Disconnect = new("velvet.disconnect", "Disconnect");
        public static readonly LocString DisconnectConfirmMessage = new("velvet.disconnectConfirmMessage", "Remove this connection?");
        public static readonly LocString PeopleToMeet = new("velvet.peopleToMeet", "People to meet");
        public static readonly LocString RelNotSaying = new("velvet.relNotSaying", "Rather not say");
        public static readonly LocString RelSingle = new("velvet.relSingle", "Single");
        public static readonly LocString RelTaken = new("velvet.relTaken", "Taken");
        public static readonly LocString RelPoly = new("velvet.relPoly", "Poly");
        public static readonly LocString RelOpen = new("velvet.relOpen", "Open relationship");
        public static readonly LocString RelComplicated = new("velvet.relComplicated", "It's complicated");
        public static readonly LocString DisplayNameLabel = new("velvet.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("velvet.handleLabel", "Handle");
        public static readonly LocString SafetyHeader = new("velvet.safetyHeader", "Safety");
        public static readonly LocString ChangePhoto = new("velvet.changePhoto", "Change photo");
        public static readonly LocString MoveAndScale = new("velvet.moveAndScale", "Move and scale");
        public static readonly LocString GestureHint = new("velvet.gestureHint", "Drag to move, scroll to zoom");
        public static readonly LocString ImportFromPc = new("velvet.importFromPc", "Import from PC");
        public static readonly LocString SendPicture = new("velvet.sendPicture", "Send a picture");
        public static readonly LocString SaveToGallery = new("velvet.saveToGallery", "Save to gallery");
        public static readonly LocString SavedToGallery = new("velvet.savedToGallery", "Saved to gallery");
        public static readonly LocString NoPhotos = new("velvet.noPhotos", "No photos in your gallery yet");
        public static readonly LocString Use = new("velvet.use", "Use");
        public static readonly LocString IntentErp = new("velvet.intentErp", "ERP");
        public static readonly LocString IntentGpose = new("velvet.intentGpose", "GPose");
        public static readonly LocString IntentRelationship = new("velvet.intentRelationship", "Relationship");
        public static readonly LocString IntentCollab = new("velvet.intentCollab", "Collab");
        public static readonly LocString IntentFriends = new("velvet.intentFriends", "Friends");
        public static readonly LocString IntentSharing = new("velvet.intentSharing", "Sharing");
        public static readonly LocString IntentWandering = new("velvet.intentWandering", "Wandering");
        public static readonly LocString OpenToAnything = new("velvet.openToAnything", "Open to anything");
        public static readonly LocString LookingForOne = new("velvet.lookingForOne", "Looking for {0}");
        public static readonly LocString Photos = new("velvet.photos", "Photos");
        public static readonly LocString MyPhotos = new("velvet.myPhotos", "My photos");
        public static readonly LocString NoPhotosShared = new("velvet.noPhotosShared", "No photos shared yet.");
        public static readonly LocString NoPhotosMine = new("velvet.noPhotosMine", "You have not shared any photos yet.");
        public static readonly LocString ConnectToSeePhotos = new("velvet.connectToSeePhotos", "Connect with {0} to see their photos");
        public static readonly LocPlural ConnectToUnlock =
            new("velvet.connectToUnlock", "Connect to unlock {0} photo", "Connect to unlock {0} photos");
        public static readonly LocString IntroTitle = new("velvet.introTitle", "Send an intro");
        public static readonly LocString IntroduceYourselfTo = new("velvet.introduceYourselfTo", "Introduce yourself to {0}");
        public static readonly LocString YourIntro = new("velvet.yourIntro", "Your intro");
        public static readonly LocString IntroSheetHint = new("velvet.introSheetHint", "Your intro lands in their Requests. A reply accepts you.");
        public static readonly LocString SendIntro = new("velvet.sendIntro", "Send intro");
        public static readonly LocString Reply = new("velvet.reply", "Reply");
        public static readonly LocString Activity = new("velvet.activity", "Activity");
        public static readonly LocString Post = new("velvet.post", "Post");
        public static readonly LocString IntentAny = new("velvet.intentAny", "Any");
        public static readonly LocString IntentErpBlurb = new("velvet.intentErpBlurb", "Erotic roleplay and scenes");
        public static readonly LocString IntentGposeBlurb = new("velvet.intentGposeBlurb", "Group pose shoots and art");
        public static readonly LocString IntentRelationshipBlurb =
            new("velvet.intentRelationshipBlurb", "Something with feelings");
        public static readonly LocString IntentCollabBlurb =
            new("velvet.intentCollabBlurb", "Writing and story partners");
        public static readonly LocString IntentFriendsBlurb =
            new("velvet.intentFriendsBlurb", "Just here to make friends");
        public static readonly LocString IntentSharingBlurb = new("velvet.intentSharingBlurb", "Trading photos and media");
        public static readonly LocString IntentWanderingBlurb = new("velvet.intentWanderingBlurb", "Seeing who is around");
        public static readonly LocString IntentIrl = new("velvet.intentIrl", "IRL");
        public static readonly LocString IntentNonIrl = new("velvet.intentNonIrl", "Non-IRL");
        public static readonly LocString IntentIrlBlurb = new("velvet.intentIrlBlurb", "Open to more than the game");
        public static readonly LocString IntentNonIrlBlurb =
            new("velvet.intentNonIrlBlurb", "In character and in game only");
        public static readonly LocString CatTone = new("velvet.catTone", "Tone");
        public static readonly LocString CatPace = new("velvet.catPace", "Pace");
        public static readonly LocString CatStyle = new("velvet.catStyle", "Style");
        public static readonly LocString CatOther = new("velvet.catOther", "Other");
        public static readonly LocString DiscoverLoading = new("velvet.discoverLoading", "Looking for people…");
        public static readonly LocString DiscoverNone = new("velvet.discoverNone", "No one here yet.");
        public static readonly LocString DiscoverNoneHint =
            new("velvet.discoverNoneHint", "Try clearing filters or check back later.");
        public static readonly LocPlural PhotoBadge = new("velvet.photoBadge", "{0} photo", "{0} photos");
        public static readonly LocString FilterClearAll = new("velvet.filterClearAll", "Clear all");
        public static readonly LocString FilterDone = new("velvet.filterDone", "Done");
        public static readonly LocString FiltersTitle = new("velvet.filtersTitle", "Filters");
        public static readonly LocString FilterHint =
            new("velvet.filterHint", "Tap once to include, tap again to exclude.");
        public static readonly LocString FilterMuteHint =
            new("velvet.filterMuteHint",
                "Excluded chips are saved and hide matching people and posts everywhere in Velvet.");
        public static readonly LocString PostTagsTitle = new("velvet.postTagsTitle", "Tags");
        public static readonly LocString PostTagsEmpty = new("velvet.postTagsEmpty", "Add tags");
        public static readonly LocString PostTagsHint =
            new("velvet.postTagsHint", "Tag what this post contains so people can filter it out.");
        public static readonly LocPlural PostTagsRemaining =
            new("velvet.postTagsRemaining", "{0} tag left", "{0} tags left");
        public static readonly LocString FeedNone = new("velvet.feedNone", "Nothing shared yet");
        public static readonly LocString FeedNoneHint = new("velvet.feedNoneHint", "Be the first to post.");
        public static readonly LocString FeedNoneFiltered =
            new("velvet.feedNoneFiltered", "Your filters are hiding everything here.");
        public static readonly LocString FeedScopeAll = new("velvet.feedScopeAll", "Everyone");
        public static readonly LocString FeedScopeConnections = new("velvet.feedScopeConnections", "Connections");
        public static readonly LocString AudienceConnections = new("velvet.audienceConnections", "Connections only");
        public static readonly LocString AudiencePublic = new("velvet.audiencePublic", "Everyone on Velvet");
        public static readonly LocString MakePublic = new("velvet.makePublic", "Share with everyone");
        public static readonly LocString MakeConnections = new("velvet.makeConnections", "Limit to connections");
        public static readonly LocString ImageUnavailable = new("velvet.imageUnavailable", "Image unavailable");
        public static readonly LocString GateTagline =
            new("velvet.gateTagline", "A private, adults only corner of the suite. Moonlit, unhurried, yours.");
        public static readonly LocString GateConsent =
            new("velvet.gateConsent", "By entering you confirm you are 18 or older. Be kind, be discreet.");
        public static readonly LocString GateEnterAction = new("velvet.gateEnterAction", "Enter");
        public static readonly LocString UnavailableTitle = new("velvet.unavailableTitle", "Velvet is unavailable");
        public static readonly LocString UnavailableBody = new("velvet.unavailableBody",
            "Velvet is an adults only space and is not available on Lalafell characters. If you recently changed your race, this clears once the Lodestone reflects it.");
        public static readonly LocString DiscoveryHeader = new("velvet.discoveryHeader", "Discovery");
        public static readonly LocString DiscoverableHelp =
            new("velvet.discoverableHelp", "When on, your profile can be found by others in Discover.");
        public static readonly LocString WhoCanMessage = new("velvet.whoCanMessage", "Who can message you");
        public static readonly LocString WhoEveryone = new("velvet.whoEveryone", "Everyone");
        public static readonly LocString WhoFriends = new("velvet.whoFriends", "Friends");
        public static readonly LocString WhoNoOne = new("velvet.whoNoOne", "No one");
        public static readonly LocString WhoHelp =
            new("velvet.whoHelp", "Choose who can send you a one line intro. Friends means friends of friends.");
        public static readonly LocString BlockedNone = new("velvet.blockedNone", "No one blocked.");
        public static readonly LocString ChatsTab = new("velvet.chatsTab", "Chats");
        public static readonly LocString RequestsCount = new("velvet.requestsCount", "Requests ({0})");
        public static readonly LocString MessagesEmptyHint =
            new("velvet.messagesEmptyHint", "Send an intro from Discover.");
        public static readonly LocString RequestsEmpty = new("velvet.requestsEmpty", "No requests");
        public static readonly LocString RequestsEmptyHint =
            new("velvet.requestsEmptyHint", "Intros you receive land here.");
        public static readonly LocString ProfileTitle = new("velvet.profileTitle", "Profile");
        public static readonly LocString ProfileUnavailable = new("velvet.profileUnavailable", "Profile unavailable");
        public static readonly LocString ProfileUnavailableHint =
            new("velvet.profileUnavailableHint", "This person may be private or no longer here.");
        public static readonly LocString Report = new("velvet.report", "Report");
        public static readonly LocString ReportProfile = new("velvet.reportProfile", "Report profile");
        public static readonly LocString ReportPost = new("velvet.reportPost", "Report post");
        public static readonly LocString More = new("velvet.more", "More");
        public static readonly LocString ViewPost = new("velvet.viewPost", "View post");
        public static readonly LocString BlockConfirm =
            new("velvet.blockConfirm",
                "Block {0}? You won't see each other in Velvet, and any connection between you is removed.");
        public static readonly LocString IntroduceYourself = new("velvet.introduceYourself", "Introduce yourself");
        public static readonly LocString CardIdentity = new("velvet.cardIdentity", "Identity");
        public static readonly LocString CardAbout = new("velvet.cardAbout", "About");
        public static readonly LocString CardIntent = new("velvet.cardIntent", "Intent");
        public static readonly LocString CardRole = new("velvet.cardRole", "Role");
        public static readonly LocString CardRelationship = new("velvet.cardRelationship", "Relationship");
        public static readonly LocString CardTags = new("velvet.cardTags", "Tags");
        public static readonly LocString CardLimits = new("velvet.cardLimits", "Limits");
        public static readonly LocString CardGender = new("velvet.cardGender", "Gender");
        public static readonly LocString GenderFemale = new("velvet.genderFemale", "Female");
        public static readonly LocString GenderMale = new("velvet.genderMale", "Male");
        public static readonly LocString GenderFemboy = new("velvet.genderFemboy", "Femboy");
        public static readonly LocString GenderFemalePlus = new("velvet.genderFemalePlus", "Female+");
        public static readonly LocString GenderMalePlus = new("velvet.genderMalePlus", "Male+");
        public static readonly LocString GenderGenderfluid = new("velvet.genderGenderfluid", "Genderfluid");
        public static readonly LocString GenderNonbinary = new("velvet.genderNonbinary", "Nonbinary");
        public static readonly LocString GenderTransgender = new("velvet.genderTransgender", "Transgender");
        public static readonly LocString CardSexuality = new("velvet.cardSexuality", "Sexuality");
        public static readonly LocString SexualityStraight = new("velvet.sexualityStraight", "Straight");
        public static readonly LocString SexualityGay = new("velvet.sexualityGay", "Gay");
        public static readonly LocString SexualityLesbian = new("velvet.sexualityLesbian", "Lesbian");
        public static readonly LocString SexualityBi = new("velvet.sexualityBi", "Bi");
        public static readonly LocString SexualityPan = new("velvet.sexualityPan", "Pan");
        public static readonly LocString SexualityAsexual = new("velvet.sexualityAsexual", "Asexual");
        public static readonly LocString SexualityDemisexual = new("velvet.sexualityDemisexual", "Demisexual");
        public static readonly LocString CardKinks = new("velvet.cardKinks", "Kinks");
        public static readonly LocString LikesTitle = new("velvet.likesTitle", "Likes");
        public static readonly LocString NoLikes = new("velvet.noLikes", "No likes yet.");
        public static readonly LocString CommentsCount = new("velvet.commentsCount", "Comments · {0}");
        public static readonly LocString SignedOutTitle = new("velvet.signedOutTitle", "Velvet is after dark");
        public static readonly LocString SignedOutHint =
            new("velvet.signedOutHint", "Sign in to your account to step inside.");
        public static readonly LocString ObTitleIdentity = new("velvet.obTitleIdentity", "Make your entrance");
        public static readonly LocString ObTitleAbout = new("velvet.obTitleAbout", "Say hello");
        public static readonly LocString ObTitleReady = new("velvet.obTitleReady", "You are all set");
        public static readonly LocString ObSubIdentity =
            new("velvet.obSubIdentity", "This is the first thing people see in Discover.");
        public static readonly LocString ObSubIntent =
            new("velvet.obSubIntent", "Choose everything that fits. It shapes who finds you.");
        public static readonly LocString ObSubAbout = new("velvet.obSubAbout", "A line or two goes a long way.");
        public static readonly LocString ObSubReady =
            new("velvet.obSubReady", "A couple of last touches, then step inside.");
        public static readonly LocString Continue = new("velvet.continue", "Continue");
        public static readonly LocString ObHandleHelp = new("velvet.obHandleHelp",
            "Your handle is how people @mention you. You can change all of this later from Edit profile.");
        public static readonly LocString AddPhoto = new("velvet.addPhoto", "Add a photo");
        public static readonly LocString YourRole = new("velvet.yourRole", "Your role");
        public static readonly LocString RoleErpHelp =
            new("velvet.roleErpHelp", "Optional. Shown because you are here for ERP.");
        public static readonly LocString VibeOptionalHelp =
            new("velvet.vibeOptionalHelp", "Optional. A few tags help the right people find you.");
        public static readonly LocString ObDiscoverableHelp = new("velvet.obDiscoverableHelp",
            "When on, your profile can be found by others. When off, only people you connect with can see you.");
        public static readonly LocString ObConductHelp = new("velvet.obConductHelp",
            "Velvet is for adults. Be kind and discreet, and remember block and report are always one tap away.");
    }

    internal static class Calculator
    {
        public static readonly LocString Error = new("calculator.error", "Error");
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
        public static readonly LocString DayToday = new("clock.dayToday", "Today");
        public static readonly LocString DayTomorrow = new("clock.dayTomorrow", "Tomorrow");
        public static readonly LocString DayYesterday = new("clock.dayYesterday", "Yesterday");
        public static readonly LocString AlarmsEmpty = new("clock.alarmsEmpty", "No alarms yet. Tap + to add one.");
        public static readonly LocString NewAlarm = new("clock.newAlarm", "New Alarm");
        public static readonly LocString EditAlarm = new("clock.editAlarm", "Edit Alarm");
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
        public static readonly LocString IndigoRoute = new("fishing.indigoRoute", "Indigo Route");
        public static readonly LocString RubyRoute = new("fishing.rubyRoute", "Ruby Route");
        public static readonly LocString DeparturesNote = new("fishing.departuresNote", "Voyages depart every 2 hours from the Fisher's Guild in Limsa Lominsa.");
        public static readonly LocString InDays = new("fishing.inDays", "in {0}d {1}h");
    }

    internal static class Dailies
    {
        public static readonly LocString Daily = new("dailies.daily", "Daily");
        public static readonly LocString Weekly = new("dailies.weekly", "Weekly");
        public static readonly LocString AllDone = new("dailies.allDone", "All done");
        public static readonly LocString NothingLeft = new("dailies.nothingLeft", "Nothing left to do");
        public static readonly LocString Remaining = new("dailies.remaining", "{0} remaining");
        public static readonly LocString Resets = new("dailies.resets", "Resets {0}");
        public static readonly LocString ShowBadge = new("dailies.showBadge", "Show badge");
        public static readonly LocString ShowBadgeNote = new("dailies.showBadgeNote", "Count unfinished tasks on the Home icon");
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
        public static readonly LocString RemoveConfirm =
            new("home.removeConfirm", "Remove {0} from the Home Screen? You can add it back from the App Library.");
        public static readonly LocString SizeSmall = new("home.sizeSmall", "Small");
        public static readonly LocString SizeMedium = new("home.sizeMedium", "Medium");
        public static readonly LocString SizeLarge = new("home.sizeLarge", "Large");
        public static readonly LocString Local = new("home.local", "Local");
        public static readonly LocString Eorzea = new("home.eorzea", "Eorzea");
        public static readonly LocString NoEvents = new("home.noEvents", "No upcoming events");
        public static readonly LocString HomeScreen = new("home.homeScreen", "Home Screen");
        public static readonly LocString ShowAppNames = new("home.showAppNames", "Show App Names");
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
        public static readonly LocString Library = new("photos.library", "Library");
        public static readonly LocString Albums = new("photos.albums", "Albums");
        public static readonly LocString Recents = new("photos.recents", "Recents");
        public static readonly LocString Today = new("photos.today", "Today");
        public static readonly LocString Yesterday = new("photos.yesterday", "Yesterday");
        public static readonly LocString OpenFolder = new("photos.openFolder", "Open folder");
        public static readonly LocString CreateAlbum = new("photos.createAlbum", "New Album");
        public static readonly LocString CreateAlbumButton = new("photos.createAlbumButton", "Create Album");
        public static readonly LocString AlbumName = new("photos.albumName", "Album name");
        public static readonly LocString AddPhotos = new("photos.addPhotos", "Add Photos");
        public static readonly LocString AddToAlbum = new("photos.addToAlbum", "Add to Album");
        public static readonly LocString AlreadyInAllAlbums =
            new("photos.alreadyInAllAlbums", "This photo is already in every album");
        public static readonly LocString RemoveFromAlbum = new("photos.removeFromAlbum", "Remove from Album");
        public static readonly LocString DeleteAlbum = new("photos.deleteAlbum", "Delete Album");
        public static readonly LocString DeleteAlbumConfirm = new("photos.deleteAlbumConfirm", "Delete {0}?");
        public static readonly LocString DeleteAlbumBody = new("photos.deleteAlbumBody", "Photos in the album won't be deleted.");
        public static readonly LocString Rename = new("photos.renameAlbum", "Rename Album");
        public static readonly LocString EmptyAlbum = new("photos.emptyAlbum", "No photos yet");
        public static readonly LocString AlbumExists = new("photos.albumExists", "An album with this name already exists");
        public static readonly LocString Done = new("photos.done", "Done");
        public static readonly LocString NoAlbums = new("photos.noAlbums", "No Albums");
        public static readonly LocString AlbumNamePlaceholder = new("photos.albumNamePlaceholder", "My Album");
        public static readonly LocString CreateAlbumHint = new("photos.noAlbumsHint", "Take a photo or create an album");
    }

    internal static class Skywatcher
    {
        public static readonly LocString NextFewHours = new("skywatcher.nextFewHours", "Next Few Hours");
        public static readonly LocString Forecast = new("skywatcher.forecast", "Forecast");
        public static readonly LocString Now = new("skywatcher.now", "Now");
        public static readonly LocString NoData = new("skywatcher.noData", "No weather data here");
        public static readonly LocString Continuing = new("skywatcher.continuing", "{0} continuing");
        public static readonly LocString ForNextHours = new("skywatcher.forNextHours", "{0} for the next few hours");
        public static readonly LocString Control = new("skywatcher.control", "Control");
        public static readonly LocString Time = new("skywatcher.time", "Time");
        public static readonly LocString Weather = new("skywatcher.weather", "Weather");
        public static readonly LocString Dawn = new("skywatcher.dawn", "Dawn");
        public static readonly LocString Noon = new("skywatcher.noon", "Noon");
        public static readonly LocString Dusk = new("skywatcher.dusk", "Dusk");
        public static readonly LocString Midnight = new("skywatcher.midnight", "Midnight");
        public static readonly LocString Natural = new("skywatcher.natural", "Natural");
        public static readonly LocString Reset = new("skywatcher.reset", "Reset");

        public static readonly LocString LocalOnly = new("skywatcher.localOnly",
            "Only you see this. Fates, mobs and fishing follow the real sky.");

        public static readonly LocString CombatPaused = new("skywatcher.combatPaused",
            "Paused while you are in combat");

        public static readonly LocString NothingToChange = new("skywatcher.nothingToChange",
            "No weather to change here");
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
        public static readonly LocString SectionCurrency = new("wallet.sectionCurrency", "Currency");
        public static readonly LocString SectionHunt = new("wallet.sectionHunt", "Hunt");
        public static readonly LocString SectionTomestones = new("wallet.sectionTomestones", "Tomestones");
        public static readonly LocString SectionPvp = new("wallet.sectionPvp", "PvP");
        public static readonly LocString SectionCrafting = new("wallet.sectionCrafting", "Crafting & Gathering");
        public static readonly LocString SectionOther = new("wallet.sectionOther", "Other");
    }

    internal static class Jobs
    {
        public static readonly LocString LogInToView = new("jobs.logInToView", "Log in to view your jobs");
        public static readonly LocString NoGearsets = new("jobs.noGearsets", "Create a gearset for a job in-game to see it here.");
        public static readonly LocString NoGearset = new("jobs.noGearset", "No gearset");
        public static readonly LocString SectionTank = new("jobs.sectionTank", "Tank");
        public static readonly LocString SectionHealer = new("jobs.sectionHealer", "Healer");
        public static readonly LocString SectionMelee = new("jobs.sectionMelee", "Melee DPS");
        public static readonly LocString SectionPhysicalRanged = new("jobs.sectionPhysicalRanged", "Physical Ranged DPS");
        public static readonly LocString SectionMagicalRanged = new("jobs.sectionMagicalRanged", "Magical Ranged DPS");
        public static readonly LocString SectionHand = new("jobs.sectionHand", "Disciples of the Hand");
        public static readonly LocString SectionLand = new("jobs.sectionLand", "Disciples of the Land");
        public static readonly LocString LevelItemLevel = new("jobs.levelItemLevel", "{0} · Lv{1} · iLv{2}");
        public static readonly LocString LevelOnly = new("jobs.levelOnly", "{0} · Lv{1}");
        public static readonly LocString Active = new("jobs.active", "ACTIVE");
        public static readonly LocString BackgroundColor = new("jobs.backgroundColor", "Background color");
        public static readonly LocString CustomColor = new("jobs.customColor", "Custom color…");
        public static readonly LocString ColorNamePlaceholder = new("jobs.colorNamePlaceholder", "Name this color");
        public static readonly LocString SaveColor = new("jobs.saveColor", "Save");
        public static readonly LocString UpdateColor = new("jobs.updateColor", "Update");
        public static readonly LocString DeleteColor = new("jobs.deleteColor", "Delete");
        public static readonly LocString DeleteColorConfirm = new("jobs.deleteColorConfirm", "Delete \"{0}\"? This can't be undone.");
        public static readonly LocString Categories = new("jobs.categories", "Categories");
        public static readonly LocString NewCategory = new("jobs.newCategory", "New category…");
        public static readonly LocString NewCategoryTitle = new("jobs.newCategoryTitle", "New category");
        public static readonly LocString RenameCategory = new("jobs.renameCategory", "Rename category");
        public static readonly LocString CategoryNamePlaceholder = new("jobs.categoryNamePlaceholder", "Name this category");
        public static readonly LocString SaveCategory = new("jobs.saveCategory", "Save");
        public static readonly LocString RemoveFromCategory = new("jobs.removeFromCategory", "Remove from category");
        public static readonly LocString DeleteCategory = new("jobs.deleteCategory", "Delete");
        public static readonly LocString DeleteCategoryConfirm = new("jobs.deleteCategoryConfirm", "Delete \"{0}\"? Its gearsets go back to their role sections.");
        public static readonly LocString EmptyCategory = new("jobs.emptyCategory", "No gearsets here yet. Use a gearset's ··· menu to add one.");
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
        public static readonly LocString CheaperOn = new("market.cheaperOn", "Cheaper on {0}");
        public static readonly LocString AfterTax = new("market.afterTax", "You keep after {0}% tax ({1})");
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
        public static readonly LocString Paused = new("games.paused", "Paused");
        public static readonly LocString PausedHint = new("games.pausedHint", "Click the phone to carry on");
        public static readonly LocString Lines = new("games.lines", "Lines");
        public static readonly LocString Level = new("games.level", "Level");
        public static readonly LocString Moves = new("games.moves", "Moves");
        public static readonly LocString Undo = new("games.undo", "Undo");
        public static readonly LocString NextLevel = new("games.nextLevel", "Next Level");
        public static readonly LocPlural AttemptsCount = new("games.attemptsCount", "{0} attempt", "{0} attempts");
        public static readonly LocString Nonogram = new("games.nonogram", "Nonogram");
        public static readonly LocString Left = new("games.left", "Left");
        public static readonly LocString Flow = new("games.flow", "Flow");
        public static readonly LocString Flows = new("games.flows", "Flows");
        public static readonly LocString Filled = new("games.filled", "Filled");
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
        public static readonly LocString Sudoku = new("games.sudoku", "Sudoku");
        public static readonly LocString Chess = new("games.chess", "Chess");
        public static readonly LocString Notes = new("games.notes", "Notes");
        public static readonly LocString Erase = new("games.erase", "Erase");
        public static readonly LocString Hint = new("games.hint", "Hint");
        public static readonly LocString Mistakes = new("games.mistakes", "Mistakes");
        public static readonly LocString Thinking = new("games.thinking", "Thinking…");
        public static readonly LocString Check = new("games.check", "Check!");
        public static readonly LocString Checkmate = new("games.checkmate", "Checkmate");
        public static readonly LocString Stalemate = new("games.stalemate", "Stalemate");
        public static readonly LocString Promote = new("games.promote", "Promote to");
        public static readonly LocString Stack = new("games.stack", "Stack");
        public static readonly LocString CrystalDrop = new("games.crystalDrop", "Crystal Drop");
        public static readonly LocString Beat = new("games.beat", "Beat");
        public static readonly LocString Combo = new("games.combo", "Combo");
        public static readonly LocString Perfect = new("games.perfect", "Perfect!");
        public static readonly LocString Good = new("games.good", "Good");
        public static readonly LocString Miss = new("games.miss", "Miss");
        public static readonly LocString Daily = new("games.daily", "Daily Challenge");
        public static readonly LocString Blade = new("games.blade", "Blade Throw");
        public static readonly LocString Trivia = new("games.trivia", "Trivia");
        public static readonly LocString WhatIsThis = new("games.whatIsThis", "What is this?");
        public static readonly LocString PickTheIcon = new("games.pickTheIcon", "Pick the right one");
        public static readonly LocString ChooseCategory = new("games.chooseCategory", "Choose a category");
        public static readonly LocString CategoryAll = new("games.categoryAll", "Everything");
        public static readonly LocString CategoryMounts = new("games.categoryMounts", "Mounts");
        public static readonly LocString CategoryMinions = new("games.categoryMinions", "Minions");
        public static readonly LocString CategoryActions = new("games.categoryActions", "Actions");
        public static readonly LocString CategoryEmotes = new("games.categoryEmotes", "Emotes");
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
        public static readonly LocString Tomorrow = new("time.tomorrow", "Tomorrow");
        public static readonly LocString InMinutes = new("time.inMinutes", "in {0}m");
        public static readonly LocString InHours = new("time.inHours", "in {0}h");
        public static readonly LocString InHoursMinutes = new("time.inHoursMinutes", "in {0}h {1}m");
    }

    internal static class Plugin
    {
        public static readonly LocString CommandHelp =new("plugin.commandHelp", "Toggle the Aetherphone. /phone run [shortcut] runs a shortcut, /phone market [item] opens the market board, /phone reset recenters the phone, /phone test sends a sample notification.");
        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /phone.");
        public static readonly LocString RunUsage = new("plugin.runUsage", "Type /phone run followed by a shortcut name.");
        public static readonly LocString ShortcutNotFound = new("plugin.shortcutNotFound", "No shortcut named {0}.");
        public static readonly LocString SearchTheMarket = new("plugin.searchTheMarket", "Search the Market");
        public static readonly LocString SideButtonHint = new("plugin.sideButtonHint", "Tap to minimize · Hold to turn off");
        public static readonly LocString MaximizeHint = new("plugin.maximizeHint", "Maximize");
        public static readonly LocString LockPositionHint = new("plugin.lockPositionHint", "Lock position");
        public static readonly LocString UnlockPositionHint = new("plugin.unlockPositionHint", "Unlock position");
        public static readonly LocString ResizeHint = new("plugin.resizeHint", "Drag to resize");
        public static readonly LocString DndEnableHint = new("plugin.dndEnableHint", "Turn on Do Not Disturb");
        public static readonly LocString DndDisableHint = new("plugin.dndDisableHint", "Turn off Do Not Disturb");
        public static readonly LocString UpdateChip = new("plugin.updateChip", "Update to {0}");

        public static readonly LocString UpdateChipHint = new("plugin.updateChipHint",
            "A newer Aetherphone is ready. Click to open Dalamud's plugin installer.");
    }

    internal static class Feedback
    {
        public static readonly LocString SendFeedback = new("feedback.sendFeedback", "Send Feedback");
        public static readonly LocString Placeholder = new("feedback.placeholder", "What's on your mind? Suggestions, bug reports, feature ideas…");
        public static readonly LocString Send = new("feedback.send", "Send");
        public static readonly LocString Sending = new("feedback.sending", "Sending…");
        public static readonly LocString Sent = new("feedback.sent", "Feedback Sent");
        public static readonly LocString ThankYou = new("feedback.thankYou", "Thank you for your feedback!");
        public static readonly LocString SentMessage = new("feedback.sentMessage", "Your message has been sent to the developer.");
        public static readonly LocString ConfirmMessage = new("feedback.confirmMessage", "Send this feedback to the developer?");
        public static readonly LocString SendMore = new("feedback.sendMore", "Send more feedback");
        public static readonly LocString Cooldown = new("feedback.cooldown", "You can send again in {0}");
        public static readonly LocString ErrorMessage = new("feedback.errorMessage", "Couldn't send your feedback. Please try again.");
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

    internal static class Announcements
    {
        public static readonly LocString SignInRequired = new("announcements.signInRequired", "Sign in to Aethernet in Settings to read announcements");
        public static readonly LocString SignInTitle = new("announcements.signInTitle", "Sign in required");
        public static readonly LocString NewBadge = new("announcements.newBadge", "NEW");
        public static readonly LocString EmptyTitle = new("announcements.emptyTitle", "Nothing announced yet");
        public static readonly LocString EmptyHint = new("announcements.emptyHint", "News from the Aetherphone team lands here.");
        public static readonly LocString UnavailableTitle = new("announcements.unavailableTitle", "Announcement unavailable");
        public static readonly LocString UnavailableHint = new("announcements.unavailableHint", "This announcement was taken down.");
    }

    internal static class Catalogs
    {
        public static readonly LocString AccentViolet = new("catalog.accent.violet", "Violet");
        public static readonly LocString AccentBlue = new("catalog.accent.blue", "Blue");
        public static readonly LocString AccentGreen = new("catalog.accent.green", "Green");
        public static readonly LocString AccentPink = new("catalog.accent.pink", "Pink");
        public static readonly LocString AccentAmber = new("catalog.accent.amber", "Amber");
        public static readonly LocString CaseTitanium = new("catalog.case.titanium", "Titanium");
        public static readonly LocString CaseBlack = new("catalog.case.black", "Black");
        public static readonly LocString CaseBlue = new("catalog.case.blue", "Blue");
        public static readonly LocString CaseGreen = new("catalog.case.green", "Green");
        public static readonly LocString CaseGrey = new("catalog.case.grey", "Grey");
        public static readonly LocString CaseLavender = new("catalog.case.lavender", "Lavender");
        public static readonly LocString CasePink = new("catalog.case.pink", "Pink");
        public static readonly LocString CasePurple = new("catalog.case.purple", "Purple");
        public static readonly LocString CaseTeal = new("catalog.case.teal", "Teal");
        public static readonly LocString CaseWhite = new("catalog.case.white", "White");
        public static readonly LocString CaseYellow = new("catalog.case.yellow", "Yellow");
        public static readonly LocString CaseBlackCat = new("catalog.case.blackcatgradient", "Black Cat");
        public static readonly LocString CaseBruteBomber = new("catalog.case.brutebombergradient", "Brute Bomber");
        public static readonly LocString CaseDancingGreen = new("catalog.case.dancinggreengradient", "Dancing Green");
        public static readonly LocString CaseGridania = new("catalog.case.gridaniagradient", "Gridania");
        public static readonly LocString CaseHoneyBLovely = new("catalog.case.honeyblovelygradient", "Honey B. Lovely");
        public static readonly LocString CaseHowlingBlade = new("catalog.case.howlingbladegradient", "Howling Blade");
        public static readonly LocString CaseLimsa = new("catalog.case.limsagradient", "Limsa Lominsa");
        public static readonly LocString CaseLindwurm = new("catalog.case.lindwurmgradient", "Lindwurm");
        public static readonly LocString CaseMoogle = new("catalog.case.mooglegradient", "Moogle");
        public static readonly LocString CaseRedHotDeepBlue = new("catalog.case.redhotdeepbluegradient", "Red Hot Deep Blue");
        public static readonly LocString CaseSolutionNine = new("catalog.case.solution9gradient", "Solution Nine");
        public static readonly LocString CaseSphene = new("catalog.case.sphenegradient", "Sphene");
        public static readonly LocString CaseSugarRiot = new("catalog.case.sugarriotgradient", "Sugar Riot");
        public static readonly LocString CaseTheTyrant = new("catalog.case.thetyrantgradient", "The Tyrant");
        public static readonly LocString CaseTuliyollal = new("catalog.case.tuliyollalgradient", "Tuliyollal");
        public static readonly LocString CaseUldah = new("catalog.case.uldahgradient", "Ul'dah");
        public static readonly LocString CaseVampFatale = new("catalog.case.vampfatalegradient", "Vamp Fatale");
        public static readonly LocString CaseWickedThunder = new("catalog.case.wickedthundergradient", "Wicked Thunder");
        public static readonly LocString CaseSilkie = new("catalog.case.silkie", "Silkie");
        public static readonly LocString CaseFatCat = new("catalog.case.fatcat", "Fat Cat");
        public static readonly LocString CaseCosmicEx = new("catalog.case.cosmicex", "Cosmic EX");
        public static readonly LocString CaseCaduceus = new("catalog.case.caduceus", "Caduceus");
        public static readonly LocString CaseMagicalGirl = new("catalog.case.magicalgirl", "Magical Girl");
        public static readonly LocString CaseAtomos = new("catalog.case.atomos", "Atomos");
        public static readonly LocString CaseBabyBat = new("catalog.case.babybat", "Baby Bat");
        public static readonly LocString CaseDwarfRabbit = new("catalog.case.dwarfrabbit", "Dwarf Rabbit");
        public static readonly LocString CaseEnkidu = new("catalog.case.enkidu", "Enkidu");
        public static readonly LocString CaseHorror = new("catalog.case.horror", "Horror");
        public static readonly LocString CaseKupo = new("catalog.case.mooglecase", "Kupo");
        public static readonly LocString CaseRunic = new("catalog.case.runic", "Runic");
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
        public static readonly LocString TapToContinue = new("onboarding.tapToContinue", "Tap to continue");
        public static readonly LocString WelcomeTitle = new("onboarding.welcomeTitle", "Welcome to Aetherphone");
        public static readonly LocString AllInOneTitle = new("onboarding.allInOneTitle", "Everything in one place");
        public static readonly LocString AllInOneBody = new("onboarding.allInOneBody", "Chat, music, weather, the market board, mini-games and more, all in your pocket.");
        public static readonly LocString FeedbackTitle = new("onboarding.feedbackTitle", "Still a work in progress");
        public static readonly LocString FeedbackBody = new("onboarding.feedbackBody", "This plugin is nowhere near a full release yet. I would love your opinions, feature ideas, and criticism. Reach me on GitHub: the links are in Settings, About Aetherphone.");
        public static readonly LocString WidgetTourTitle = new("onboarding.widgetTourTitle", "Live at a glance");
        public static readonly LocString WidgetTourBody = new("onboarding.widgetTourBody", "Widgets live on your Home Screen and update on their own. This one shows the Eorzean weather wherever you're standing.");
        public static readonly LocString MyNumberTourTitle = new("onboarding.myNumberTourTitle", "Your very own number");
        public static readonly LocString CustomizeTitle = new("onboarding.customizeTitle", "Make it your own");
        public static readonly LocString CustomizeBody = new("onboarding.customizeBody", "Press and hold anywhere on the Home Screen to rearrange icons, resize widgets, and add new ones.");
        public static readonly LocString ControlCenterTitle = new("onboarding.controlCenterTitle", "Control Center");
        public static readonly LocString HomeTourTitle = new("onboarding.homeTourTitle", "This is your Home Screen");
        public static readonly LocString HomeTourBody = new("onboarding.homeTourBody", "Your phone is ready. Before you dive in, here's a quick look around.");
        public static readonly LocString AppsTourTitle = new("onboarding.appsTourTitle", "Your apps");
        public static readonly LocString AppsTourBody = new("onboarding.appsTourBody", "Tap any icon to open an app. The bar at the bottom of the screen always brings you back home.");
        public static readonly LocString ControlCenterTapBody = new("onboarding.controlCenterTapBody", "Tap the top of the screen to open Control Center.");
        public static readonly LocString ControlCenterInsideTitle = new("onboarding.controlCenterInsideTitle", "Everything at hand");
        public static readonly LocString ControlCenterInsideBody = new("onboarding.controlCenterInsideBody", "Volume, brightness, accent color and your notifications all live here. Tap the bottom edge to close it anytime; for now, Continue will do it for you.");
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
        public static readonly LocString CharacterBody = new("onboarding.characterBody", "Your day in Eorzea at a glance: three rings that fill as you play and reset at midnight.");
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
        public static readonly LocString TimersBody = new("onboarding.timersBody", "Countdowns to the daily, Grand Company and weekly resets, plus Fashion Report, the Jumbo Cactpot and your retainer ventures. Switch on reminders and the phone nudges you.");
        public static readonly LocString DailiesBody = new("onboarding.dailiesBody", "A simple checklist for your daily and weekly routines. Tick things off and it all resets right on schedule.");
        public static readonly LocString FishingBody = new("onboarding.fishingBody", "Bite windows, handy tips and the best spots for the fish worth chasing.");
        public static readonly LocString NotificationsBody = new("onboarding.notificationsBody", "A running history of everything your phone has pinged you about, so nothing slips past you.");
        public static readonly LocString VelvetDiscoverTitle = new("onboarding.velvetDiscoverTitle", "Discover people");
        public static readonly LocString VelvetDiscoverBody = new("onboarding.velvetDiscoverBody", "Browse profiles filtered by what people are looking for, and send a connection request when you find someone interesting.");
        public static readonly LocString VelvetFilterTitle = new("onboarding.velvetFilterTitle", "Filter by intent");
        public static readonly LocString VelvetFilterBody = new("onboarding.velvetFilterBody", "Narrow the people here to exactly what you want, from ERP to gpose to just making friends.");
        public static readonly LocString VelvetFeedTitle = new("onboarding.velvetFeedTitle", "The live feed");
        public static readonly LocString VelvetFeedBody = new("onboarding.velvetFeedBody", "This is the live feed, where people share photos and posts across Velvet. Everything here stays inside Velvet.");
        public static readonly LocString VelvetActivityTitle = new("onboarding.velvetActivityTitle", "Your activity");
        public static readonly LocString VelvetActivityBody = new("onboarding.velvetActivityBody", "Likes, comments and new intros land here. Tap the bell any time to catch up.");
        public static readonly LocString VelvetMessagesTitle = new("onboarding.velvetMessagesTitle", "Requests and messages");
        public static readonly LocString VelvetMessagesBody = new("onboarding.velvetMessagesBody", "Accept or decline requests, then chat privately with the connections you make.");
        public static readonly LocString VelvetProfileTitle = new("onboarding.velvetProfileTitle", "Your profile");
        public static readonly LocString VelvetProfileBody = new("onboarding.velvetProfileBody", "Set up your intro, vibe, tags and limits, and choose whether you're discoverable to others.");
        public static readonly LocString VelvetKindTitle = new("onboarding.velvetKindTitle", "Consent and Respect");
        public static readonly LocString VelvetKindBody = new("onboarding.velvetKindBody", "This space is for everyone. Discriminatory, hateful or harmful content isn't welcome and can get you banned.");
        public static readonly LocString FeedbackIntroBody = new("onboarding.feedbackIntroBody", "Tell the developer what you think: suggestions, bug reports, feature ideas, or just a hello.");
        public static readonly LocString FeedbackWriteTitle = new("onboarding.feedbackWriteTitle", "Write and send");
        public static readonly LocString FeedbackWriteBody = new("onboarding.feedbackWriteBody", "Type your message and tap Send. Your feedback goes directly to the developer's dashboard.");
        public static readonly LocString FeedbackPrivacyTitle = new("onboarding.feedbackPrivacyTitle", "Honest and respectful");
        public static readonly LocString FeedbackPrivacyBody = new("onboarding.feedbackPrivacyBody", "Your character name is attached so the developer knows who you are in game. Be constructive and kind.");
        public static readonly LocString MessageBody = new("onboarding.messageBody", "Message and call your friends in one place. Add friends by number in Contacts, chat in Chats, and talk over voice from Calls.");
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
        public static readonly LocString ChirperTabsTitle = new("onboarding.chirperTabsTitle", "Two feeds");
        public static readonly LocString ChirperTabsBody = new("onboarding.chirperTabsBody", "For You shows chirps from everyone; Following keeps it to the people you follow. Swap between them any time.");
        public static readonly LocString ChirperSearchTitle = new("onboarding.chirperSearchTitle", "Find people");
        public static readonly LocString ChirperSearchBody = new("onboarding.chirperSearchBody", "Search for other players by name or handle, and follow them to build your Following feed.");
        public static readonly LocString ChirperActivityTitle = new("onboarding.chirperActivityTitle", "Never miss a mention");
        public static readonly LocString ChirperActivityBody = new("onboarding.chirperActivityBody", "Likes, replies and new followers all land under the bell.");
        public static readonly LocString AethergramSearchTitle = new("onboarding.aethergramSearchTitle", "Find people");
        public static readonly LocString AethergramSearchBody = new("onboarding.aethergramSearchBody", "Tap Search to look up other players, browse their grids, and follow the ones you like.");
        public static readonly LocString AethergramActivityTitle = new("onboarding.aethergramActivityTitle", "Likes and comments");
        public static readonly LocString AethergramActivityBody = new("onboarding.aethergramActivityBody", "Hearts, comments and new followers show up under this tab the moment they happen.");
        public static readonly LocString AethergramProfileTitle = new("onboarding.aethergramProfileTitle", "Your profile");
        public static readonly LocString AethergramProfileBody = new("onboarding.aethergramProfileBody", "Tap your avatar to set up your profile and watch your grid fill up with your shots.");
        public static readonly LocString VelvetComposeTitle = new("onboarding.velvetComposeTitle", "Share to the feed");
        public static readonly LocString VelvetComposeBody = new("onboarding.velvetComposeBody", "Post thoughts and photos for your connections. Everything you share stays inside Velvet.");
        public static readonly LocString MessageCallsTitle = new("onboarding.messageCallsTitle", "Voice calls");
        public static readonly LocString MessageContactsTitle = new("onboarding.messageContactsTitle", "Your address book");
        public static readonly LocString MessageContactsBody = new("onboarding.messageContactsBody", "Friends you add by number live in Contacts. Tap the tab to take a look.");
        public static readonly LocString MessageNumberCopyBody = new("onboarding.messageNumberCopyBody", "This card is your number. Tap it to copy, then share it in game so friends can add you.");
        public static readonly LocString MessageAddFriendTitle = new("onboarding.messageAddFriendTitle", "Add a friend");
        public static readonly LocString MessageAddFriendBody = new("onboarding.messageAddFriendBody", "Got someone's number? Tap the plus and their card appears right here.");
        public static readonly LocString MusicSearchTitle = new("onboarding.musicSearchTitle", "Find any track");
        public static readonly LocString MusicSearchBody = new("onboarding.musicSearchBody", "Type a song or artist and the results stream straight to your phone.");
        public static readonly LocString MusicRadioTitle = new("onboarding.musicRadioTitle", "Tune the radio");
        public static readonly LocString MusicRadioBody = new("onboarding.musicRadioBody", "Pick a genre to browse live radio stations. Tap one and it starts playing instantly.");
        public static readonly LocString MarketSearchTitle = new("onboarding.marketSearchTitle", "Search anything");
        public static readonly LocString MarketSearchBody = new("onboarding.marketSearchBody", "Type a couple of letters to search every marketable item, or look one up straight from the game.");
        public static readonly LocString MarketScopeTitle = new("onboarding.marketScopeTitle", "Pick your scope");
        public static readonly LocString MarketScopeBody = new("onboarding.marketScopeBody", "Compare prices on your world, your data center, or the whole region. Your pick is remembered.");
        public static readonly LocString VenuesTimeTitle = new("onboarding.venuesTimeTitle", "Now or later");
        public static readonly LocString VenuesTimeBody = new("onboarding.venuesTimeBody", "Filter events by when they happen: live right now, today, upcoming, or everything.");
        public static readonly LocString VenuesFilterTitle = new("onboarding.venuesFilterTitle", "Narrow it down");
        public static readonly LocString VenuesFilterBody = new("onboarding.venuesFilterBody", "Tap a chip to filter by data center, source, tags, or just your favorites.");
        public static readonly LocString VenuesSearchTitle = new("onboarding.venuesSearchTitle", "Find a venue");
        public static readonly LocString VenuesSearchBody = new("onboarding.venuesSearchBody", "Know the name? Search venues and events directly here.");
        public static readonly LocString GamesFeaturedTitle = new("onboarding.gamesFeaturedTitle", "Today's pick");
        public static readonly LocString GamesFeaturedBody = new("onboarding.gamesFeaturedBody", "A different game is featured every day. Tap the card to jump straight in.");
        public static readonly LocString GamesLibraryTitle = new("onboarding.gamesLibraryTitle", "Browse the arcade");
        public static readonly LocString GamesLibraryBody = new("onboarding.gamesLibraryBody", "Every game sorted by genre, with your best score under each title.");
        public static readonly LocString CameraModesTitle = new("onboarding.cameraModesTitle", "Pick a mode");
        public static readonly LocString CameraModesBody = new("onboarding.cameraModesBody", "Square gives a centered crop, Photo uses the full viewfinder.");
        public static readonly LocString CameraFlashTitle = new("onboarding.cameraFlashTitle", "Screen flash");
        public static readonly LocString CameraFlashBody = new("onboarding.cameraFlashBody", "With the flash on, the screen blinks white as you capture. Handy in dark zones.");
        public static readonly LocString CameraShutterTitle = new("onboarding.cameraShutterTitle", "Say cheese");
        public static readonly LocString CameraShutterBody = new("onboarding.cameraShutterBody", "Tap the shutter to snap what's behind the phone. Shots land straight in the Photos app.");
        public static readonly LocString PhotosEmptyTitle = new("onboarding.photosEmptyTitle", "Nothing here yet?");
        public static readonly LocString PhotosEmptyBody = new("onboarding.photosEmptyBody", "Photos come from the Camera app. Take a shot and it appears in this grid instantly.");
        public static readonly LocString NotesNewTitle = new("onboarding.notesNewTitle", "Start a note");
        public static readonly LocString NotesNewBody = new("onboarding.notesNewBody", "Tap the plus to open a fresh note. It saves itself as you type.");
        public static readonly LocString NotesReminderTitle = new("onboarding.notesReminderTitle", "Add a reminder");
        public static readonly LocString NotesReminderBody = new("onboarding.notesReminderBody", "On this tab the plus creates a reminder. Give it a due date and the phone nudges you.");
        public static readonly LocString CalendarAgendaTitle = new("onboarding.calendarAgendaTitle", "Day agenda");
        public static readonly LocString CalendarAgendaBody = new("onboarding.calendarAgendaBody", "Whatever day you pick, its events line up here, community happenings beside your own plans.");
        public static readonly LocString SettingsAccountTitle = new("onboarding.settingsAccountTitle", "Your Aethernet account");
        public static readonly LocString SettingsAccountBody = new("onboarding.settingsAccountBody", "Sign in here to unlock the social side of the phone: Chirper, Aethergram, Polls and more.");
        public static readonly LocString SettingsAppearanceTitle = new("onboarding.settingsAppearanceTitle", "Looks and themes");
        public static readonly LocString SettingsAppearanceBody = new("onboarding.settingsAppearanceBody", "Theme, accent color and wallpaper all live in Appearance. Make the phone yours.");
        public static readonly LocString SettingsTutorialsTitle = new("onboarding.settingsTutorialsTitle", "Tours live here");
        public static readonly LocString SettingsTutorialsBody = new("onboarding.settingsTutorialsBody", "Replay any tour or turn tips off entirely from Tutorials.");
        public static readonly LocString NotificationsHistoryTitle = new("onboarding.notificationsHistoryTitle", "Your history");
        public static readonly LocString NotificationsHistoryBody = new("onboarding.notificationsHistoryBody", "Everything the phone pinged you about stacks up here. Tap one to jump to its app, or clear them all up top.");
        public static readonly LocString MessagesListTitle = new("onboarding.messagesListTitle", "Pick up the thread");
        public static readonly LocString MessagesListBody = new("onboarding.messagesListBody", "Every /tell becomes a conversation here. Tap one to read and reply without leaving the game.");
        public static readonly LocString MessagesLinkshellsTitle = new("onboarding.messagesLinkshellsTitle", "Linkshells too");
        public static readonly LocString MessagesLinkshellsBody = new("onboarding.messagesLinkshellsBody", "Tap the tabs to switch views. Linkshells mirrors your linkshell and cross-world channels.");
        public static readonly LocString FeedbackSendTitle = new("onboarding.feedbackSendTitle", "Send it off");
        public static readonly LocString FeedbackSendBody = new("onboarding.feedbackSendBody", "When you're happy with it, tap Send. It goes straight to the developer's dashboard.");
        public static readonly LocString PollsVoteTitle = new("onboarding.pollsVoteTitle", "Cast your vote");
        public static readonly LocString PollsVoteBody = new("onboarding.pollsVoteBody", "Each card is one community poll. Tap an option to vote. You can switch your pick while it's open.");
        public static readonly LocString SkywatcherCurrentTitle = new("onboarding.skywatcherCurrentTitle", "Right now");
        public static readonly LocString SkywatcherCurrentBody = new("onboarding.skywatcherCurrentBody", "This is the zone you're standing in and its live weather, refreshed as you travel.");
        public static readonly LocString MapsLocationTitle = new("onboarding.mapsLocationTitle", "You are here");
        public static readonly LocString MapsLocationBody = new("onboarding.mapsLocationBody", "Maps always knows where you're standing. Your current zone and region sit right at the top.");
        public static readonly LocString MapsSearchTitle = new("onboarding.mapsSearchTitle", "Find any aetheryte");
        public static readonly LocString MapsSearchBody = new("onboarding.mapsSearchBody", "Type a zone or aetheryte name to jump straight to it. With Lifestream installed, one tap travels there.");
        public static readonly LocString MapsStarTitle = new("onboarding.mapsStarTitle", "Star your favorites");
        public static readonly LocString MapsStarBody = new("onboarding.mapsStarBody", "Expand an expansion, then tap the star beside any destination to pin it to your Favorites.");
        public static readonly LocString FindPeopleSearchTitle = new("onboarding.findPeopleSearchTitle", "Search the Lodestone");
        public static readonly LocString FindPeopleSearchBody = new("onboarding.findPeopleSearchBody", "Type a character's name here. The world field is set to your data center, but you can point it anywhere.");
        public static readonly LocString FindPeopleKindTitle = new("onboarding.findPeopleKindTitle", "Characters or Free Companies");
        public static readonly LocString FindPeopleKindBody = new("onboarding.findPeopleKindBody", "Flip this to look up Free Companies instead, with crests, slogans and full member rosters.");
        public static readonly LocString NewsCategoriesTitle = new("onboarding.newsCategoriesTitle", "Four feeds in one");
        public static readonly LocString NewsCategoriesBody = new("onboarding.newsCategoriesBody", "Topics, notices, maintenance windows and patch updates, each on its own tab.");
        public static readonly LocString NewsReadTitle = new("onboarding.newsReadTitle", "Read the full story");
        public static readonly LocString NewsReadBody = new("onboarding.newsReadBody", "Tap any card or row to open the full article in your browser.");
        public static readonly LocString NewsRefreshTitle = new("onboarding.newsRefreshTitle", "Fresh off the Lodestone");
        public static readonly LocString NewsRefreshBody = new("onboarding.newsRefreshBody", "News refreshes on its own, but a tap here pulls the latest right now.");
        public static readonly LocString ContactsListTitle = new("onboarding.contactsListTitle", "Friends at a glance");
        public static readonly LocString ContactsListBody = new("onboarding.contactsListBody", "Your friend list, online first, with portraits. Tap anyone for actions like messaging and party invites.");
        public static readonly LocString ContactsSearchTitle = new("onboarding.contactsSearchTitle", "Find someone fast");
        public static readonly LocString ContactsSearchBody = new("onboarding.contactsSearchBody", "Start typing a name to filter the list instantly.");
        public static readonly LocString CharacterRingsTitle = new("onboarding.characterRingsTitle", "Your three rings");
        public static readonly LocString CharacterRingsBody = new("onboarding.characterRingsBody", "Progress tracks experience, Adventure counts duties, and Fortune counts gil earned today. Play to close all three.");
        public static readonly LocString CharacterSummaryTitle = new("onboarding.characterSummaryTitle", "The numbers behind them");
        public static readonly LocString CharacterSummaryBody = new("onboarding.characterSummaryBody", "Today's totals in detail: experience, duties, gil, playtime and new collectibles, with your current session below.");
        public static readonly LocString ClockIntroBody = new("onboarding.clockIntroBody", "Eorzea time, server time and your own cities, ticking side by side. And that's just the first tab.");
        public static readonly LocString ClockTabsTitle = new("onboarding.clockTabsTitle", "More than a clock");
        public static readonly LocString ClockTabsBody = new("onboarding.clockTabsBody", "Alarms, a stopwatch and a timer live behind these tabs. Tap here to try the alarms.");
        public static readonly LocString ClockAddTitle = new("onboarding.clockAddTitle", "Add your own");
        public static readonly LocString ClockAddBody = new("onboarding.clockAddBody", "This plus creates a new alarm here, or adds a city on the World tab.");
        public static readonly LocString CalculatorTapeTitle = new("onboarding.calculatorTapeTitle", "Your running tape");
        public static readonly LocString CalculatorTapeBody = new("onboarding.calculatorTapeBody", "Past results pile up here as you calculate. Tap any old result to drop it into a new sum.");
        public static readonly LocString TimersResetsTitle = new("onboarding.timersResetsTitle", "Counting down");
        public static readonly LocString TimersResetsBody = new("onboarding.timersResetsBody", "Daily, Grand Company and weekly resets counted down live, each with the time it lands for you.");
        public static readonly LocString TimersRemindersTitle = new("onboarding.timersRemindersTitle", "Never miss a reset");
        public static readonly LocString TimersRemindersBody = new("onboarding.timersRemindersBody", "Flip a toggle and the phone pings you when that reset hits or a retainer venture finishes.");
        public static readonly LocString DailiesCadenceTitle = new("onboarding.dailiesCadenceTitle", "Two rhythms");
        public static readonly LocString DailiesCadenceBody = new("onboarding.dailiesCadenceBody", "Your routines split into Daily and Weekly. Tap here to flip over to the weekly list.");
        public static readonly LocString DailiesBadgeTitle = new("onboarding.dailiesBadgeTitle", "Quiet the badge");
        public static readonly LocString DailiesBadgeBody = new("onboarding.dailiesBadgeBody", "The Home icon counts what is still unfinished. Turn it off and the app stays quiet until you open it.");
        public static readonly LocString FishingHeroTitle = new("onboarding.fishingHeroTitle", "Next voyage");
        public static readonly LocString FishingHeroBody = new("onboarding.fishingHeroBody", "This card is your next boarding window, with the route, its time of day and a countdown to departure.");
        public static readonly LocString FishingBlueTitle = new("onboarding.fishingBlueTitle", "Blue fish aboard");
        public static readonly LocString FishingBlueBody = new("onboarding.fishingBlueBody", "The rare blue fish catchable on this route, each with the bait that tempts it.");
        public static readonly LocString FishingUpcomingTitle = new("onboarding.fishingUpcomingTitle", "Plan your trip");
        public static readonly LocString FishingUpcomingBody = new("onboarding.fishingUpcomingBody", "Boats leave every two hours. Scroll the schedule to find a departure that suits you.");
        public static readonly LocString WalletGilTitle = new("onboarding.walletGilTitle", "Gil at a glance");
        public static readonly LocString WalletGilBody = new("onboarding.walletGilBody", "Your live gil balance, refreshed as you earn and spend.");
        public static readonly LocString WalletCurrenciesTitle = new("onboarding.walletCurrenciesTitle", "Caps included");
        public static readonly LocString WalletCurrenciesBody = new("onboarding.walletCurrenciesBody", "Tomestones, seals and scrip grouped by family. Capped currencies show a bar so you know when to spend.");
        public static readonly LocString InventorySearchTitle = new("onboarding.inventorySearchTitle", "Search everything");
        public static readonly LocString InventorySearchBody = new("onboarding.inventorySearchBody", "Type an item name to search your bags, saddlebag, retainers and FC chest all at once.");
        public static readonly LocString InventorySummaryTitle = new("onboarding.inventorySummaryTitle", "The headline numbers");
        public static readonly LocString InventorySummaryBody = new("onboarding.inventorySummaryBody", "How much you're carrying and your gil, front and center.");
        public static readonly LocString InventorySourcesTitle = new("onboarding.inventorySourcesTitle", "Retainers remembered");
        public static readonly LocString InventorySourcesBody = new("onboarding.inventorySourcesBody", "Open a retainer or the FC chest once and the phone keeps a snapshot here, browsable any time.");
        public static readonly LocString CollectionsCategoryTitle = new("onboarding.collectionsCategoryTitle", "Pick a category");
        public static readonly LocString CollectionsCategoryBody = new("onboarding.collectionsCategoryBody", "Each tile is a collection with your completion ring. Tap Mounts to open its catalog.");
        public static readonly LocString CollectionsSearchTitle = new("onboarding.collectionsSearchTitle", "Find anything");
        public static readonly LocString CollectionsSearchBody = new("onboarding.collectionsSearchBody", "Search the whole catalog by name, or filter by where it comes from.");
        public static readonly LocString CollectionsMissingTitle = new("onboarding.collectionsMissingTitle", "What's still missing");
        public static readonly LocString CollectionsMissingBody = new("onboarding.collectionsMissingBody", "With your Lodestone linked, flip to Missing to see exactly what's left to hunt down.");
        public static readonly LocString StoreTourTitle = new("onboarding.storeTourTitle", "Get more apps");
        public static readonly LocString StoreTourBody = new("onboarding.storeTourBody", "The phone starts with a handful of apps. The App Store has the rest, and you decide which ones live on your Home Screen.");
        public static readonly LocString AppStoreBody = new("onboarding.appStoreBody", "Every app on the phone comes from here. Install what you want, skip what you don't, and come back whenever you change your mind.");
        public static readonly LocString AppStoreGetTitle = new("onboarding.appStoreGetTitle", "Install an app");
        public static readonly LocString AppStoreGetBody = new("onboarding.appStoreGetBody", "Tap Get to add an app to your Home Screen, or tap the row itself to read what it does first.");
        public static readonly LocString AppStoreBrowseTitle = new("onboarding.appStoreBrowseTitle", "Browse by category");
        public static readonly LocString AppStoreBrowseBody = new("onboarding.appStoreBrowseBody", "Apps groups everything by what it's for: social, utilities, games and more.");
        public static readonly LocString AppStoreSearchTitle = new("onboarding.appStoreSearchTitle", "Know what you want?");
        public static readonly LocString AppStoreSearchBody = new("onboarding.appStoreSearchBody", "Search finds an app by name in one go.");
        public static readonly LocString AppStoreRemoveTitle = new("onboarding.appStoreRemoveTitle", "Removing is safe");
        public static readonly LocString AppStoreRemoveBody = new("onboarding.appStoreRemoveBody", "Press and hold an icon on the Home Screen to remove an app. Your data stays put, and you can install it again from here.");
        public static readonly LocString JobsBody = new("onboarding.jobsBody", "Every gearset you own, grouped by role, with the one you're wearing marked as active.");
        public static readonly LocString JobsSwitchTitle = new("onboarding.jobsSwitchTitle", "Switch in a tap");
        public static readonly LocString JobsSwitchBody = new("onboarding.jobsSwitchBody", "Tap any row to equip that gearset. Crafters and gatherers switch the same way.");
        public static readonly LocString JobsCategoriesTitle = new("onboarding.jobsCategoriesTitle", "Your own groups");
        public static readonly LocString JobsCategoriesBody = new("onboarding.jobsCategoriesBody", "Build custom categories to keep raid sets, crafters or alt jobs together, in whatever order suits you.");
        public static readonly LocString JobsColorTitle = new("onboarding.jobsColorTitle", "Pick a color");
        public static readonly LocString JobsColorBody = new("onboarding.jobsColorBody", "The palette recolors the app. Choose a preset or mix your own and save it.");
        public static readonly LocString MusterBody = new("onboarding.musterBody", "Player meetups happening right now across the data centers. Find one, say you're coming, and travel over.");
        public static readonly LocString MusterScopeTitle = new("onboarding.musterScopeTitle", "How far to look");
        public static readonly LocString MusterScopeBody = new("onboarding.musterScopeBody", "Narrow the list to your data center or open it up to the whole region. The globe pins any data center you like.");
        public static readonly LocString MusterCategoriesTitle = new("onboarding.musterCategoriesTitle", "Only what you're after");
        public static readonly LocString MusterCategoriesBody = new("onboarding.musterCategoriesBody", "Filter by what's happening: hangouts, hunts, raids, roleplay and the rest.");
        public static readonly LocString MusterStartTitle = new("onboarding.musterStartTitle", "Host your own");
        public static readonly LocString MusterStartBody = new("onboarding.musterStartBody", "Set a place, a time and how many can come, and your meetup shows up for everyone else.");
        public static readonly LocString MusterSafetyTitle = new("onboarding.musterSafetyTitle", "Meet with care");
        public static readonly LocString MusterSafetyBody = new("onboarding.musterSafetyBody", "Hosts only see who you are once you say you're coming. Keep it welcoming, and report anything that isn't.");
        public static readonly LocString YellowPagesBody = new("onboarding.yellowPagesBody", "Player classifieds: shops, services, venues and hires, all posted by other players.");
        public static readonly LocString YellowPagesScopeTitle = new("onboarding.yellowPagesScopeTitle", "Set your reach");
        public static readonly LocString YellowPagesScopeBody = new("onboarding.yellowPagesScopeBody", "Ads are filtered to your region by default. Widen or narrow that here.");
        public static readonly LocString YellowPagesSearchTitle = new("onboarding.yellowPagesSearchTitle", "Search the listings");
        public static readonly LocString YellowPagesSearchBody = new("onboarding.yellowPagesSearchBody", "Type what you need, or use the category tiles underneath to jump straight to a section.");
        public static readonly LocString YellowPagesPostTitle = new("onboarding.yellowPagesPostTitle", "Post your own ad");
        public static readonly LocString YellowPagesPostBody = new("onboarding.yellowPagesPostBody", "Write it, add photos and opening hours, and it stays up until it expires. You can edit or renew it any time.");
        public static readonly LocString YellowPagesInquiriesTitle = new("onboarding.yellowPagesInquiriesTitle", "Replies land here");
        public static readonly LocString YellowPagesInquiriesBody = new("onboarding.yellowPagesInquiriesBody", "When someone asks about an ad, the conversation opens in Inquiries, encrypted end to end.");
        public static readonly LocString YellowPagesSafetyTitle = new("onboarding.yellowPagesSafetyTitle", "Trade carefully");
        public static readonly LocString YellowPagesSafetyBody = new("onboarding.yellowPagesSafetyBody", "Nobody vets these ads. Agree on the gil up front, meet in game, and report anything that smells like a scam.");
        public static readonly LocString AnnouncementsBody = new("onboarding.announcementsBody", "News straight from the Aetherphone team: releases, downtime and anything else worth knowing.");
        public static readonly LocString AnnouncementsCardTitle = new("onboarding.announcementsCardTitle", "Read the latest");
        public static readonly LocString AnnouncementsCardBody = new("onboarding.announcementsCardBody", "The newest post sits on top. Tap it for the full story; anything you haven't read is highlighted.");
        public static readonly LocString AnnouncementsQuietTitle = new("onboarding.announcementsQuietTitle", "Keeping it quiet");
        public static readonly LocString AnnouncementsQuietBody = new("onboarding.announcementsQuietBody", "Announcements always stays on the phone, but you can turn its notifications off in Settings.");
        public static readonly LocString HealthBody = new("onboarding.healthBody", "Your character's activity: every yalm walked, swum and flown, counted while you play.");
        public static readonly LocString HealthTodayTitle = new("onboarding.healthTodayTitle", "Today at a glance");
        public static readonly LocString HealthTodayBody = new("onboarding.healthTodayBody", "Steps against your daily goal, with active time, energy and hydration right underneath.");
        public static readonly LocString HealthTabsTitle = new("onboarding.healthTabsTitle", "Dig into the details");
        public static readonly LocString HealthTabsBody = new("onboarding.healthTabsBody", "Activity, water, goals, history and your profile each get a tab. Tap Goals to set your own targets.");
        public static readonly LocString HealthGoalsTitle = new("onboarding.healthGoalsTitle", "Set your targets");
        public static readonly LocString HealthGoalsBody = new("onboarding.healthGoalsBody", "Switch a goal on and it appears on the overview with a progress bar and your streak.");
        public static readonly LocString HealthPrivacyTitle = new("onboarding.healthPrivacyTitle", "Stays on your machine");
        public static readonly LocString HealthPrivacyBody = new("onboarding.healthPrivacyBody", "None of this is uploaded anywhere. It's tracked per character and saved with your phone.");
    }

    internal static class Setup
    {
        public static readonly LocString WelcomeTitle = new("setup.welcomeTitle", "Aetherphone");
        public static readonly LocString WelcomeBody = new("setup.welcomeBody", "Your very own smartphone, right here in Eorzea. A few quick steps and it's ready to go.");
        public static readonly LocString SetUpLater = new("setup.setUpLater", "Set Up Later");
        public static readonly LocString SkipForNow = new("setup.skipForNow", "Skip for Now");
        public static readonly LocString AccountTitle = new("setup.accountTitle", "Aethernet Account");
        public static readonly LocString AccountBody = new("setup.accountBody", "One account unlocks every social app: Chirper, Aethergram, Message and more. Sign in with your character, no password needed.");
        public static readonly LocString SignedInTitle = new("setup.signedInTitle", "You're signed in");
        public static readonly LocString SignedInBody = new("setup.signedInBody", "Signed in as {0}. Next, make your profile yours.");
        public static readonly LocString ProfileTitle = new("setup.profileTitle", "Your Profile");
        public static readonly LocString ProfileBody = new("setup.profileBody", "This is how other players see you across the social apps. You can change it anytime, or set a different one per app.");
        public static readonly LocString DisplayNameLabel = new("setup.displayNameLabel", "Display name");
        public static readonly LocString HandleLabel = new("setup.handleLabel", "Handle");
        public static readonly LocString HandleRules = new("setup.handleRules", "3 to 15 characters: lowercase letters, numbers and underscores.");
        public static readonly LocString HandleTaken = new("setup.handleTaken", "That handle isn't available. Try another one.");
        public static readonly LocString PhotoTitle = new("setup.photoTitle", "Profile Photo");
        public static readonly LocString PhotoBody = new("setup.photoBody", "Add a photo so friends recognize you. Without one, your Lodestone portrait is shown instead.");
        public static readonly LocString ChoosePhoto = new("setup.choosePhoto", "Choose a Photo");
        public static readonly LocString FeatureMessageTitle = new("setup.featureMessageTitle", "Stay in touch");
        public static readonly LocString FeatureMessageBody = new("setup.featureMessageBody", "Chats, calls and contacts with players everywhere.");
        public static readonly LocString FeatureSocialTitle = new("setup.featureSocialTitle", "Share your story");
        public static readonly LocString FeatureSocialBody = new("setup.featureSocialBody", "Post on Chirper and Aethergram with one account.");
        public static readonly LocString FeatureToolsTitle = new("setup.featureToolsTitle", "Tools for Eorzea");
        public static readonly LocString FeatureToolsBody = new("setup.featureToolsBody", "Market board, weather, maps, timers and news.");
        public static readonly LocString FeaturePlayTitle = new("setup.featurePlayTitle", "Take a break");
        public static readonly LocString FeaturePlayBody = new("setup.featurePlayBody", "Music, radio and a shelf of mini-games.");
        public static readonly LocString ReadyBody = new("setup.readyBody", "You're all set. Enjoy your Aetherphone.");
        public static readonly LocString StartUsing = new("setup.startUsing", "Start Using Aetherphone");
    }

    internal static class YellowPages
    {
        public static readonly LocString SetUpAccount = new("yellowpages.setUpAccount", "Sign in to browse the classifieds.");
        public static readonly LocString ScopeRegion = new("yellowpages.scopeRegion", "Region");
        public static readonly LocString ScopeMyDc = new("yellowpages.scopeMyDc", "My DC");
        public static readonly LocString ScopeEverywhere = new("yellowpages.scopeEverywhere", "Everywhere");
        public static readonly LocString SearchLabel = new("yellowpages.searchLabel", "Search ads");
        public static readonly LocString OpenSection = new("yellowpages.openSection", "Open tonight");
        public static readonly LocString BrowseSection = new("yellowpages.browseSection", "Browse by");
        public static readonly LocString IntentCategories = new("yellowpages.intentCategories", "{0} categories");
        public static readonly LocString FilterAll = new("yellowpages.filterAll", "All");
        public static readonly LocString LatestSection = new("yellowpages.latestSection", "Latest ads");
        public static readonly LocString LoadMore = new("yellowpages.loadMore", "Load more");
        public static readonly LocString EmptyTitle = new("yellowpages.emptyTitle", "Nothing listed yet");
        public static readonly LocString EmptyHint = new("yellowpages.emptyHint", "Widen the scope or clear the filters, or be the first to post an ad.");
        public static readonly LocString PostAd = new("yellowpages.postAd", "New ad");
        public static readonly LocString YourAds = new("yellowpages.yourAds", "Your ads");
        public static readonly LocString YourAdsCount = new("yellowpages.yourAdsCount", "{0} of 3 live");
        public static readonly LocString IntentGo = new("yellowpages.intentGo", "Go somewhere");
        public static readonly LocString IntentHire = new("yellowpages.intentHire", "Hire someone");
        public static readonly LocString IntentJoin = new("yellowpages.intentJoin", "Join something");
        public static readonly LocString CategoryVenueNight = new("yellowpages.categoryVenueNight", "Venue nights");
        public static readonly LocString CategoryEventShow = new("yellowpages.categoryEventShow", "Events and shows");
        public static readonly LocString CategoryCasino = new("yellowpages.categoryCasino", "Casinos and game nights");
        public static readonly LocString CategoryHousingTour = new("yellowpages.categoryHousingTour", "Housing tours and open plots");
        public static readonly LocString CategoryCrafting = new("yellowpages.categoryCrafting", "Crafting and melds");
        public static readonly LocString CategoryGathering = new("yellowpages.categoryGathering", "Gathering");
        public static readonly LocString CategoryGlamour = new("yellowpages.categoryGlamour", "Glamour and design");
        public static readonly LocString CategoryPortraits = new("yellowpages.categoryPortraits", "Portraits and gpose");
        public static readonly LocString CategoryPerformance = new("yellowpages.categoryPerformance", "Music and performance");
        public static readonly LocString CategoryCoaching = new("yellowpages.categoryCoaching", "Carries and coaching");
        public static readonly LocString CategoryOddJobs = new("yellowpages.categoryOddJobs", "Odd jobs");
        public static readonly LocString CategoryFreeCompany = new("yellowpages.categoryFreeCompany", "Free companies");
        public static readonly LocString CategoryRaidStatic = new("yellowpages.categoryRaidStatic", "Statics and raiding");
        public static readonly LocString CategoryVenueStaff = new("yellowpages.categoryVenueStaff", "Venue staff");
        public static readonly LocString CategoryCommunity = new("yellowpages.categoryCommunity", "Communities");
        public static readonly LocString CategoryMods = new("yellowpages.categoryMods", "Mods and tools");
        public static readonly LocString CategoryHousingDesign = new("yellowpages.categoryHousingDesign", "Housing and interior design");
        public static readonly LocString CategoryWeddings = new("yellowpages.categoryWeddings", "Weddings and ceremonies");
        public static readonly LocString CategoryWriting = new("yellowpages.categoryWriting", "Writing and RP services");
        public static readonly LocString ModLinkLabel = new("yellowpages.modLinkLabel", "Mod page link");
        public static readonly LocString ModLinkHint = new("yellowpages.modLinkHint", "https link to XIV Mod Archive, Heliosphere, Glamour Dresser or GitHub. Mod ads carry a link and photos, never a price.");
        public static readonly LocString ModLinkAction = new("yellowpages.modLinkAction", "Open the mod page");
        public static readonly LocString ModLinkCopied = new("yellowpages.modLinkCopied", "Link copied");
        public static readonly LocString NeedModLink = new("yellowpages.needModLink", "Add a link to the mod page.");
        public static readonly LocString ModBadge = new("yellowpages.modBadge", "Mod");
        public static readonly LocString InquiriesTitle = new("yellowpages.inquiriesTitle", "Inquiries");
        public static readonly LocString EarlierMessages = new("yellowpages.earlierMessages", "View earlier messages");
        public static readonly LocString NoInquiriesTitle = new("yellowpages.noInquiriesTitle", "No inquiries yet");
        public static readonly LocString NoInquiriesHint = new("yellowpages.noInquiriesHint", "Questions about your ads, and the ones you asked about, land here.");
        public static readonly LocString InquiryHint = new("yellowpages.inquiryHint", "Write a message");
        public static readonly LocString InquiryCount = new("yellowpages.inquiryCount", "{0} inquiries");
        public static readonly LocString InquiryConsentHint = new("yellowpages.inquiryConsentHint", "Posting an ad is consent to be asked about it, so this reaches the poster even if their messages are closed.");
        public static readonly LocString InquiryLocked = new("yellowpages.inquiryLocked", "Unlock your encryption key to send inquiries.");
        public static readonly LocString InquirySendFailed = new("yellowpages.inquirySendFailed", "Could not send. Their encryption keys are not ready yet.");
        public static readonly LocString InquiryEncrypted = new("yellowpages.inquiryEncrypted", "End to end encrypted");
        public static readonly LocString NotifInquiryTitle = new("yellowpages.notifInquiryTitle", "New inquiry");
        public static readonly LocString NotifInquiryBody = new("yellowpages.notifInquiryBody", "Someone messaged you about \"{0}\".");
        public static readonly LocString NotifInquiryGeneric = new("yellowpages.notifInquiryGeneric", "Someone messaged you about one of your ads.");
        public static readonly LocString PriceAsk = new("yellowpages.priceAsk", "Ask for price");
        public static readonly LocString PriceGil = new("yellowpages.priceGil", "{0} gil");
        public static readonly LocString PriceFrom = new("yellowpages.priceFrom", "from {0} gil");
        public static readonly LocString OpenNow = new("yellowpages.openNow", "Open now");
        public static readonly LocString OpenClosesAt = new("yellowpages.openClosesAt", "Open now · closes {0}");
        public static readonly LocString ClosesAt = new("yellowpages.closesAt", "closes {0} your time");
        public static readonly LocString OpensAt = new("yellowpages.opensAt", "Opens {0}");
        public static readonly LocString Expired = new("yellowpages.expired", "Expired");
        public static readonly LocString ExpiresDays = new("yellowpages.expiresDays", "Expires in {0}d");
        public static readonly LocString ExpiresHours = new("yellowpages.expiresHours", "Expires in {0}h");
        public static readonly LocString AfterDarkChip = new("yellowpages.afterDarkChip", "18+");
        public static readonly LocString PhotoCount = new("yellowpages.photoCount", "{0} photos");
        public static readonly LocString UnavailableTitle = new("yellowpages.unavailableTitle", "Ad unavailable");
        public static readonly LocString UnavailableHint = new("yellowpages.unavailableHint", "This ad expired or was taken down.");
        public static readonly LocString ScheduleSection = new("yellowpages.scheduleSection", "Weekly schedule");
        public static readonly LocString ScheduleYourTime = new("yellowpages.scheduleYourTime", "Weekly · shown in your time");
        public static readonly LocString RenewedAgo = new("yellowpages.renewedAgo", "renewed {0}");
        public static readonly LocString RequirementsSection = new("yellowpages.requirementsSection", "Requirements");
        public static readonly LocString WhereSection = new("yellowpages.whereSection", "Where");
        public static readonly LocString WardPlot = new("yellowpages.wardPlot", "Ward {0}, Plot {1}");
        public static readonly LocString FlagOnMap = new("yellowpages.flagOnMap", "Set map flag");
        public static readonly LocString CopyDetails = new("yellowpages.copyDetails", "Copy details");
        public static readonly LocString Copied = new("yellowpages.copied", "Copied");
        public static readonly LocString Travel = new("yellowpages.travel", "Travel there");
        public static readonly LocString ManageAction = new("yellowpages.manageAction", "Manage your ads");
        public static readonly LocString Save = new("yellowpages.save", "Save this ad");
        public static readonly LocString Unsave = new("yellowpages.unsave", "Remove from saved");
        public static readonly LocString ShareAd = new("yellowpages.shareAd", "Copy share token");
        public static readonly LocString ReportTitle = new("yellowpages.reportTitle", "Report this ad");
        public static readonly LocString NewAd = new("yellowpages.newAd", "New ad");
        public static readonly LocString WhatPosting = new("yellowpages.whatPosting", "What are you posting?");
        public static readonly LocString ArchetypePlace = new("yellowpages.archetypePlace", "A place");
        public static readonly LocString ArchetypePlaceHint = new("yellowpages.archetypePlaceHint", "A venue or event night: address, weekly schedule, and an Open Now switch you flip at the door.");
        public static readonly LocString ArchetypeService = new("yellowpages.archetypeService", "A service");
        public static readonly LocString ArchetypeServiceHint = new("yellowpages.archetypeServiceHint", "Work for gil: crafting, portraits, glamour, performance, coaching. Price up front.");
        public static readonly LocString ArchetypeCall = new("yellowpages.archetypeCall", "A call");
        public static readonly LocString ArchetypeCallHint = new("yellowpages.archetypeCallHint", "Recruitment: free company, static, venue staff. What you need and how many slots.");
        public static readonly LocString PostRules = new("yellowpages.postRules", "Ads run 14 days (places 30) and renew with one tap. 3 live ads per account. Gil only.");
        public static readonly LocString CategorySection = new("yellowpages.categorySection", "Category");
        public static readonly LocString TitleLabel = new("yellowpages.titleLabel", "Title");
        public static readonly LocString BodyLabel = new("yellowpages.bodyLabel", "Description");
        public static readonly LocString TagsLabel = new("yellowpages.tagsLabel", "Tags");
        public static readonly LocString TagsHint = new("yellowpages.tagsHint", "Comma separated, up to 8. They power search.");
        public static readonly LocString UseMyLocation = new("yellowpages.useMyLocation", "Use my location");
        public static readonly LocString ClearLocation = new("yellowpages.clearLocation", "Clear");
        public static readonly LocString AddressNoteLabel = new("yellowpages.addressNoteLabel", "Address note");
        public static readonly LocString ScheduleHint = new("yellowpages.scheduleHint", "Times are entered in your clock and shown to every reader in theirs.");
        public static readonly LocString DaysLabel = new("yellowpages.daysLabel", "Days");
        public static readonly LocString OpensLabel = new("yellowpages.opensLabel", "Opens at");
        public static readonly LocString ClosesLabel = new("yellowpages.closesLabel", "Closes at");
        public static readonly LocString DurationLabel = new("yellowpages.durationLabel", "Open for");
        public static readonly LocString DurationHours = new("yellowpages.durationHours", "{0}h");
        public static readonly LocString DurationMinutes = new("yellowpages.durationMinutes", "{0}m");
        public static readonly LocString DurationHoursMinutes = new("yellowpages.durationHoursMinutes", "{0}h {1}m");
        public static readonly LocString PriceSection = new("yellowpages.priceSection", "Pricing");
        public static readonly LocString PriceFixed = new("yellowpages.priceFixed", "Fixed price");
        public static readonly LocString PriceFromLabel = new("yellowpages.priceFromLabel", "Starting at");
        public static readonly LocString PriceGilLabel = new("yellowpages.priceGilLabel", "Price in gil");
        public static readonly LocString TurnaroundLabel = new("yellowpages.turnaroundLabel", "Turnaround");
        public static readonly LocString CallSection = new("yellowpages.callSection", "Who you need");
        public static readonly LocString SlotsLabel = new("yellowpages.slotsLabel", "Open slots");
        public static readonly LocString RequirementsLabel = new("yellowpages.requirementsLabel", "Requirements");
        public static readonly LocString AfterDarkToggle = new("yellowpages.afterDarkToggle", "After Dark (18+)");
        public static readonly LocString AfterDarkHint = new("yellowpages.afterDarkHint", "Hidden from readers unless they opt in. Keep the ad itself non-explicit.");
        public static readonly LocString AllowInquiriesToggle = new("yellowpages.allowInquiriesToggle", "Let readers message me");
        public static readonly LocString AllowInquiriesHint = new("yellowpages.allowInquiriesHint", "Turn this off and nobody can open an inquiry about this ad. Conversations you already have stay open.");
        public static readonly LocString NeedTitle = new("yellowpages.needTitle", "Give your ad a title.");
        public static readonly LocString NeedBody = new("yellowpages.needBody", "Add a description.");
        public static readonly LocString NeedDataCenter = new("yellowpages.needDataCenter", "Log in to a world first.");
        public static readonly LocString NeedOpenWindow = new("yellowpages.needOpenWindow", "Keep the doors open for at least {0} minutes.");
        public static readonly LocString PublishAd = new("yellowpages.publishAd", "Publish ad");
        public static readonly LocString ErrorTooMany = new("yellowpages.errorTooMany", "You already have 3 active ads.");
        public static readonly LocString ErrorInvalid = new("yellowpages.errorInvalid", "Something in the ad was rejected. Check the fields and try again.");
        public static readonly LocString ErrorRateLimited = new("yellowpages.errorRateLimited", "You are posting too fast. Give it a minute.");
        public static readonly LocString ErrorFailed = new("yellowpages.errorFailed", "Could not publish the ad. Try again.");
        public static readonly LocString MineHint = new("yellowpages.mineHint", "Ads renew only near expiry, and readers stop seeing them the moment they lapse.");
        public static readonly LocString NoAdsTitle = new("yellowpages.noAdsTitle", "No ads yet");
        public static readonly LocString NoAdsHint = new("yellowpages.noAdsHint", "Post a place, a service, or a recruitment call and it runs for weeks.");
        public static readonly LocString Renew = new("yellowpages.renew", "Renew");
        public static readonly LocString OpenNowAction = new("yellowpages.openNowAction", "Open up");
        public static readonly LocString CloseNow = new("yellowpages.closeNow", "Close");
        public static readonly LocString DeleteAd = new("yellowpages.deleteAd", "Delete");
        public static readonly LocString DeleteConfirm = new("yellowpages.deleteConfirm", "Delete this ad? Its photos and saves go with it.");
        public static readonly LocString Deleting = new("yellowpages.deleting", "Deleting");
        public static readonly LocString DeleteFailed = new("yellowpages.deleteFailed", "Could not delete the ad.");
        public static readonly LocString HiddenStatus = new("yellowpages.hiddenStatus", "Hidden pending review");
        public static readonly LocString SavedTitle = new("yellowpages.savedTitle", "Saved ads");
        public static readonly LocString NoSavedTitle = new("yellowpages.noSavedTitle", "Nothing saved");
        public static readonly LocString NoSavedHint = new("yellowpages.noSavedHint", "Save an ad and it stays here until it expires.");
        public static readonly LocString NotifHiddenTitle = new("yellowpages.notifHiddenTitle", "Ad hidden");
        public static readonly LocString NotifHiddenBody = new("yellowpages.notifHiddenBody", "\"{0}\" was hidden after reports and is pending review.");
        public static readonly LocString NotifExpiringTitle = new("yellowpages.notifExpiringTitle", "Ad expiring");
        public static readonly LocString NotifExpiringBody = new("yellowpages.notifExpiringBody", "\"{0}\" expires within a day. Renew it to keep it listed.");
        public static readonly LocString NotifExpiringGeneric = new("yellowpages.notifExpiringGeneric", "One of your ads expires within a day. Renew it to keep it listed.");
        public static readonly LocString NotifHiddenGeneric = new("yellowpages.notifHiddenGeneric", "One of your ads was hidden after reports and is pending review.");
        public static readonly LocString NotifOpenedTitle = new("yellowpages.notifOpenedTitle", "Open now");
        public static readonly LocString NotifOpenedBody = new("yellowpages.notifOpenedBody", "\"{0}\" just opened its doors.");
        public static readonly LocString NotifOpenedGeneric = new("yellowpages.notifOpenedGeneric", "A place you saved is open now.");
        public static readonly LocString EditAd = new("yellowpages.editAd", "Edit");
        public static readonly LocString EditAdTitle = new("yellowpages.editAdTitle", "Edit ad");
        public static readonly LocString SaveChanges = new("yellowpages.saveChanges", "Save changes");
        public static readonly LocString InquireAction = new("yellowpages.inquireAction", "Message the poster");
        public static readonly LocString InquireHint = new("yellowpages.inquireHint", "Opens an inquiry here in Yellow Pages, attached to this ad.");
        public static readonly LocString InquiriesClosed = new("yellowpages.inquiriesClosed", "Messages are off");
        public static readonly LocString InquiriesClosedHint = new("yellowpages.inquiriesClosedHint", "The poster turned off messages for this ad. Use the details above to reach them in game.");
        public static readonly LocString ViewCount = new("yellowpages.viewCount", "{0} views");
        public static readonly LocString AnnounceMuster = new("yellowpages.announceMuster", "Announce on Muster");
        public static readonly LocString AfterDarkConfirmTitle = new("yellowpages.afterDarkConfirmTitle", "After Dark");
        public static readonly LocString AfterDarkConfirmBody = new("yellowpages.afterDarkConfirmBody", "Show 18+ ads while browsing? They stay hidden for everyone who has not opted in, and the ads themselves must still be non-explicit.");
        public static readonly LocString AfterDarkConfirmYes = new("yellowpages.afterDarkConfirmYes", "Show 18+ ads");
        public static readonly LocString BrowseTab = new("yellowpages.browseTab", "Browse");
        public static readonly LocString SavedTab = new("yellowpages.savedTab", "Saved");
        public static readonly LocString MineTab = new("yellowpages.mineTab", "My ads");
        public static readonly LocString AdPreview = new("yellowpages.adPreview", "Yellow Pages ad");
        public static readonly LocString AdUnavailable = new("yellowpages.adUnavailable", "Ad unavailable");
        public static readonly LocString AdOpen = new("yellowpages.adOpen", "Open in Yellow Pages");
    }

    internal static class Conduct
    {
        public static readonly LocString Eyebrow = new("conduct.eyebrow", "Community Guidelines");
        public static readonly LocString Acknowledge = new("conduct.acknowledge", "I have read and understood these rules. I accept that breaking them may get my account suspended or banned.");
        public static readonly LocString AgreeAction = new("conduct.agreeAction", "I understand and agree");
        public static readonly LocString WaitAction = new("conduct.waitAction", "Please read the rules… {0}");
        public static readonly LocString ReadToEndAction = new("conduct.readToEndAction", "Scroll to the end to continue");

        public static readonly LocString ChirperTitle = new("conduct.chirper.title", "Chirper Community Rules");
        public static readonly LocString ChirperIntro = new("conduct.chirper.intro", "Before you start posting, please read the rules of the community.");
        public static readonly LocString ChirperAllowedTitle = new("conduct.chirper.allowed.title", "What Is Allowed");
        public static readonly LocString ChirperAllowedLead = new("conduct.chirper.allowed.lead", "Examples of acceptable content include:");
        public static readonly LocString[] ChirperAllowedItems =
        {
            new("conduct.chirper.allowed.1", "Daily adventures and stories"),
            new("conduct.chirper.allowed.2", "Roleplay updates and creative writing"),
            new("conduct.chirper.allowed.3", "Community discussions, questions, and guides"),
            new("conduct.chirper.allowed.4", "Glamour, screenshots, and achievements"),
            new("conduct.chirper.allowed.5", "Humor and memes"),
        };
        public static readonly LocString ChirperAppropriateTitle = new("conduct.chirper.appropriate.title", "Keep It Appropriate");
        public static readonly LocString ChirperAppropriateLead = new("conduct.chirper.appropriate.lead", "Chirper is a public community platform. Do not post or promote:");
        public static readonly LocString[] ChirperAppropriateItems =
        {
            new("conduct.chirper.appropriate.1", "Explicit sexual content or nudity"),
            new("conduct.chirper.appropriate.2", "ERP advertisements or sexual solicitation"),
            new("conduct.chirper.appropriate.3", "Fetish content"),
            new("conduct.chirper.appropriate.4", "Graphic sexual language intended for arousal"),
        };
        public static readonly LocString ChirperRespectTitle = new("conduct.chirper.respect.title", "Be Respectful");
        public static readonly LocString ChirperRespectLead = new("conduct.chirper.respect.lead", "Treat others with respect. Do not engage in:");
        public static readonly LocString[] ChirperRespectItems =
        {
            new("conduct.chirper.respect.1", "Harassment or bullying"),
            new("conduct.chirper.respect.2", "Hate speech or slurs"),
            new("conduct.chirper.respect.3", "Threats or targeted abuse"),
            new("conduct.chirper.respect.4", "Impersonation of real people or misinformation intended to deceive"),
        };
        public static readonly LocString ChirperSpamTitle = new("conduct.chirper.spam.title", "No Spam or Advertising");
        public static readonly LocString ChirperSpamLead = new("conduct.chirper.spam.lead", "Keep the feed worth browsing. Do not post:");
        public static readonly LocString[] ChirperSpamItems =
        {
            new("conduct.chirper.spam.1", "Repetitive posts or feed flooding"),
            new("conduct.chirper.spam.2", "Venue, business, or service advertisements: use Yellow Pages instead"),
            new("conduct.chirper.spam.3", "Engagement manipulation or automated accounts"),
            new("conduct.chirper.spam.4", "Malicious links, scams, or phishing"),
        };
        public static readonly LocString ChirperCreatorsTitle = new("conduct.chirper.creators.title", "Respect Creators");
        public static readonly LocString ChirperCreatorsLead = new("conduct.chirper.creators.lead", "Only upload content you have the right to share. Do not:");
        public static readonly LocString[] ChirperCreatorsItems =
        {
            new("conduct.chirper.creators.1", "Post stolen or leaked content"),
            new("conduct.chirper.creators.2", "Remove watermarks or repost commissioned work without permission"),
            new("conduct.chirper.creators.3", "Claim someone else's creations as your own"),
            new("conduct.chirper.creators.4", "Post AI-generated content"),
        };
        public static readonly LocString ChirperPrivacyTitle = new("conduct.chirper.privacy.title", "Protect Privacy");
        public static readonly LocString ChirperPrivacyBody = new("conduct.chirper.privacy.body", "Do not share another person's personal information, private conversations, or confidential content without their permission.");
        public static readonly LocString ChirperChildSafetyTitle = new("conduct.chirper.childSafety.title", "Child Safety");
        public static readonly LocString ChirperChildSafetyBody = new("conduct.chirper.childSafety.body", "Any content involving or sexualizing child-like characters or minors is strictly prohibited, regardless of lore or stated age.");
        public static readonly LocString ChirperDiscretionTitle = new("conduct.chirper.discretion.title", "Moderator Discretion");
        public static readonly LocString ChirperDiscretionBody = new("conduct.chirper.discretion.body", "Posts that are excessively suggestive, disruptive, or otherwise inappropriate for a public community may be removed at moderator discretion.");

        public static readonly LocString AethergramTitle = new("conduct.aethergram.title", "Aethergram Community Rules");
        public static readonly LocString AethergramIntro = new("conduct.aethergram.intro", "Before you start sharing photos, please read the rules of the community.");
        public static readonly LocString AethergramSfwTitle = new("conduct.aethergram.sfw.title", "Keep It SFW");
        public static readonly LocString AethergramSfwLead = new("conduct.aethergram.sfw.lead", "Aethergram is a safe-for-work platform. Do not post or promote:");
        public static readonly LocString[] AethergramSfwItems =
        {
            new("conduct.aethergram.sfw.1", "Nudity or explicit sexual content"),
            new("conduct.aethergram.sfw.2", "Sexually suggestive images, poses, or captions"),
            new("conduct.aethergram.sfw.3", "ERP advertisements or sexual solicitation"),
            new("conduct.aethergram.sfw.4", "Fetish content"),
            new("conduct.aethergram.sfw.5", "Explicit sexual language in posts, profiles, or comments"),
            new("conduct.aethergram.sfw.6", "Graphic violence or gore"),
        };
        public static readonly LocString AethergramContextTitle = new("conduct.aethergram.context.title", "Context Matters");
        public static readonly LocString AethergramContextLead = new("conduct.aethergram.context.lead", "Some content may be reviewed based on presentation, including:");
        public static readonly LocString[] AethergramContextItems =
        {
            new("conduct.aethergram.context.1", "Swimwear or lingerie"),
            new("conduct.aethergram.context.2", "Romantic or intimate poses"),
            new("conduct.aethergram.context.3", "Suggestive camera angles or captions"),
        };
        public static readonly LocString AethergramChildlikeTitle = new("conduct.aethergram.childlike.title", "Child-like Characters");
        public static readonly LocString AethergramChildlikeBody = new("conduct.aethergram.childlike.body", "Any content that sexualizes characters with child-like appearances or proportions is strictly prohibited, regardless of lore or stated age. Kitten Modded characters are not allowed.");
        public static readonly LocString AethergramAllowedTitle = new("conduct.aethergram.allowed.title", "What Is Allowed");
        public static readonly LocString AethergramAllowedLead = new("conduct.aethergram.allowed.lead", "Examples of acceptable content include:");
        public static readonly LocString[] AethergramAllowedItems =
        {
            new("conduct.aethergram.allowed.1", "Glamour and fashion showcases"),
            new("conduct.aethergram.allowed.2", "Character portraits and GPose photography"),
            new("conduct.aethergram.allowed.3", "Casual roleplay and screenshots"),
            new("conduct.aethergram.allowed.4", "Wedding and event photos"),
            new("conduct.aethergram.allowed.5", "Combat, emotes, and social activities"),
            new("conduct.aethergram.allowed.6", "Romantic content that is not sexual in nature"),
            new("conduct.aethergram.allowed.7", "Memes"),
        };
        public static readonly LocString AethergramIrlTitle = new("conduct.aethergram.irl.title", "In-Game Content Only");
        public static readonly LocString AethergramIrlBody = new("conduct.aethergram.irl.body", "Aethergram is a place for in-game moments. Do not post real-life photographs or other real-world content. Memes are the exception.");
        public static readonly LocString AethergramRespectTitle = new("conduct.aethergram.respect.title", "Be Respectful");
        public static readonly LocString AethergramRespectLead = new("conduct.aethergram.respect.lead", "Treat others with respect. Do not engage in:");
        public static readonly LocString[] AethergramRespectItems =
        {
            new("conduct.aethergram.respect.1", "Harassment or bullying"),
            new("conduct.aethergram.respect.2", "Hate speech or slurs"),
            new("conduct.aethergram.respect.3", "Threats or targeted abuse"),
            new("conduct.aethergram.respect.4", "Harassment through edited or manipulated images"),
            new("conduct.aethergram.respect.5", "Impersonation of real people or misinformation intended to deceive"),
        };
        public static readonly LocString AethergramSpamTitle = new("conduct.aethergram.spam.title", "No Spam or Advertising");
        public static readonly LocString AethergramSpamLead = new("conduct.aethergram.spam.lead", "Keep the feed worth browsing. Do not post:");
        public static readonly LocString[] AethergramSpamItems =
        {
            new("conduct.aethergram.spam.1", "Repetitive posts or feed flooding"),
            new("conduct.aethergram.spam.2", "Venue, business, or service advertisements: use Yellow Pages instead"),
            new("conduct.aethergram.spam.3", "Engagement manipulation or automated accounts"),
            new("conduct.aethergram.spam.4", "Excessive watermarks or promotional overlays"),
            new("conduct.aethergram.spam.5", "Malicious links, scams, or phishing"),
        };
        public static readonly LocString AethergramPrivacyTitle = new("conduct.aethergram.privacy.title", "Protect Privacy");
        public static readonly LocString AethergramPrivacyBody = new("conduct.aethergram.privacy.body", "Do not share another person's personal information, private conversations, or confidential content without their permission.");
        public static readonly LocString AethergramCreatorsTitle = new("conduct.aethergram.creators.title", "Respect Creators");
        public static readonly LocString AethergramCreatorsLead = new("conduct.aethergram.creators.lead", "Only upload content you have the right to share. Do not:");
        public static readonly LocString[] AethergramCreatorsItems =
        {
            new("conduct.aethergram.creators.1", "Post stolen or leaked content"),
            new("conduct.aethergram.creators.2", "Remove watermarks or repost commissioned work without permission"),
            new("conduct.aethergram.creators.3", "Claim someone else's creations as your own"),
            new("conduct.aethergram.creators.4", "Post AI-generated content"),
        };
        public static readonly LocString AethergramDiscretionTitle = new("conduct.aethergram.discretion.title", "Moderator Discretion");
        public static readonly LocString AethergramDiscretionBody = new("conduct.aethergram.discretion.body", "Moderators will consider the overall context and intent. Content that appears intended to be sexually suggestive or otherwise inappropriate for a safe-for-work platform may be removed at moderator discretion.");

        public static readonly LocString VelvetTitle = new("conduct.velvet.title", "Velvet Community Rules");
        public static readonly LocString VelvetIntro = new("conduct.velvet.intro", "Velvet is an 18+ space. Before you continue, please read the rules of the community.");
        public static readonly LocString VelvetAdultsTitle = new("conduct.velvet.adults.title", "Adults Only (18+)");
        public static readonly LocString VelvetAdultsBody = new("conduct.velvet.adults.body", "Velvet is for adults aged 18 and above. Any content involving minors, child-like characters (including Lalafell and Kitten Mods), or underage roleplay is strictly prohibited and results in a permanent ban.");
        public static readonly LocString VelvetAllowedTitle = new("conduct.velvet.allowed.title", "What Is Allowed");
        public static readonly LocString VelvetAllowedLead = new("conduct.velvet.allowed.lead", "Examples of acceptable content include:");
        public static readonly LocString[] VelvetAllowedItems =
        {
            new("conduct.velvet.allowed.1", "Personal ads looking for mature roleplay"),
            new("conduct.velvet.allowed.2", "Suggestive screenshots (nudity is allowed)"),
            new("conduct.velvet.allowed.3", "Adult-oriented discussions"),
            new("conduct.velvet.allowed.4", "Relationship communities"),
            new("conduct.velvet.allowed.5", "Character storytelling"),
        };
        public static readonly LocString VelvetConsentTitle = new("conduct.velvet.consent.title", "Consent First");
        public static readonly LocString VelvetConsentLead = new("conduct.velvet.consent.lead", "Respect other users. Do not engage in:");
        public static readonly LocString[] VelvetConsentItems =
        {
            new("conduct.velvet.consent.1", "Unsolicited explicit content"),
            new("conduct.velvet.consent.2", "Coercion or pressure into ERP"),
            new("conduct.velvet.consent.3", "Contacting users who have declined or blocked you"),
        };
        public static readonly LocString VelvetBoundariesTitle = new("conduct.velvet.boundaries.title", "Respect Boundaries");
        public static readonly LocString VelvetBoundariesLead = new("conduct.velvet.boundaries.lead", "Only interact through intended platform features. Do not:");
        public static readonly LocString[] VelvetBoundariesItems =
        {
            new("conduct.velvet.boundaries.1", "Bypass mutual connections"),
            new("conduct.velvet.boundaries.2", "Use alternate accounts to evade blocks"),
            new("conduct.velvet.boundaries.3", "Ask others to contact someone on your behalf"),
        };
        public static readonly LocString VelvetIllegalTitle = new("conduct.velvet.illegal.title", "Illegal and Prohibited Content");
        public static readonly LocString VelvetIllegalLead = new("conduct.velvet.illegal.lead", "Zero tolerance, and fantasy or roleplay is no exemption. The following result in an immediate permanent ban:");
        public static readonly LocString[] VelvetIllegalItems =
        {
            new("conduct.velvet.illegal.1", "Child sexual abuse material (CSAM)"),
            new("conduct.velvet.illegal.2", "Sexual exploitation"),
            new("conduct.velvet.illegal.3", "Non-consensual intimate imagery or revenge porn"),
            new("conduct.velvet.illegal.4", "Deepfake or AI-generated explicit images of real people"),
            new("conduct.velvet.illegal.5", "Blackmail or sextortion"),
            new("conduct.velvet.illegal.6", "Incest, bestiality, or necrophilia"),
            new("conduct.velvet.illegal.7", "Sexualized violence, snuff, or extreme gore"),
        };
        public static readonly LocString VelvetPrivacyTitle = new("conduct.velvet.privacy.title", "Protect Privacy");
        public static readonly LocString VelvetPrivacyLead = new("conduct.velvet.privacy.lead", "Doxxing results in a permanent ban. Never share another person's personal information without permission, including:");
        public static readonly LocString[] VelvetPrivacyItems =
        {
            new("conduct.velvet.privacy.1", "Real names"),
            new("conduct.velvet.privacy.2", "Addresses or phone numbers"),
            new("conduct.velvet.privacy.3", "Government IDs"),
            new("conduct.velvet.privacy.4", "Workplace information"),
            new("conduct.velvet.privacy.5", "Private conversations"),
        };
        public static readonly LocString VelvetCreatorsTitle = new("conduct.velvet.creators.title", "Respect Creators");
        public static readonly LocString VelvetCreatorsLead = new("conduct.velvet.creators.lead", "Only upload content you have the right to share. Do not:");
        public static readonly LocString[] VelvetCreatorsItems =
        {
            new("conduct.velvet.creators.1", "Post stolen or leaked content"),
            new("conduct.velvet.creators.2", "Remove watermarks or repost commissioned work without permission"),
            new("conduct.velvet.creators.3", "Claim someone else's creations as your own"),
            new("conduct.velvet.creators.4", "Post AI-generated content"),
        };
        public static readonly LocString VelvetSpamTitle = new("conduct.velvet.spam.title", "No Spam or Scams");
        public static readonly LocString VelvetSpamLead = new("conduct.velvet.spam.lead", "Keep the feed worth browsing. Do not post:");
        public static readonly LocString[] VelvetSpamItems =
        {
            new("conduct.velvet.spam.1", "Repetitive posts or feed flooding"),
            new("conduct.velvet.spam.2", "Venue, business, or service advertisements: use Yellow Pages instead"),
            new("conduct.velvet.spam.3", "Engagement manipulation or automated accounts"),
            new("conduct.velvet.spam.4", "Malicious links, scams, or phishing"),
            new("conduct.velvet.spam.5", "Sale or promotion of illegal services"),
        };
        public static readonly LocString VelvetRespectTitle = new("conduct.velvet.respect.title", "Be Respectful");
        public static readonly LocString VelvetRespectLead = new("conduct.velvet.respect.lead", "Treat others with respect. Do not engage in:");
        public static readonly LocString[] VelvetRespectItems =
        {
            new("conduct.velvet.respect.1", "Harassment or bullying"),
            new("conduct.velvet.respect.2", "Hate speech or slurs"),
            new("conduct.velvet.respect.3", "Threats or targeted abuse"),
            new("conduct.velvet.respect.4", "Stalking or repeated unwanted contact"),
            new("conduct.velvet.respect.5", "Impersonation of real people or misinformation intended to deceive"),
        };
        public static readonly LocString VelvetModerationTitle = new("conduct.velvet.moderation.title", "Respect Moderation");
        public static readonly LocString VelvetModerationBody = new("conduct.velvet.moderation.body", "Do not evade bans, create alternate accounts to avoid enforcement, or harass moderators. Appeals are welcome if made respectfully.");

        public static readonly LocString MusterTitle = new("conduct.muster.title", "Muster Community Rules");
        public static readonly LocString MusterIntro = new("conduct.muster.intro", "Before you host or join a meetup, please read the rules of the community.");
        public static readonly LocString MusterAllowedTitle = new("conduct.muster.allowed.title", "What Is Allowed");
        public static readonly LocString MusterAllowedLead = new("conduct.muster.allowed.lead", "Examples of acceptable musters include:");
        public static readonly LocString[] MusterAllowedItems =
        {
            new("conduct.muster.allowed.1", "Spontaneous meetups and hangouts"),
            new("conduct.muster.allowed.2", "Hunt trains, map parties, and duty groups"),
            new("conduct.muster.allowed.3", "Roleplay scenes and social gatherings"),
            new("conduct.muster.allowed.4", "Screenshot sessions, fishing trips, and Gold Saucer nights"),
            new("conduct.muster.allowed.5", "Community events open to anyone who shows up"),
        };
        public static readonly LocString MusterHostingTitle = new("conduct.muster.hosting.title", "Host in Good Faith");
        public static readonly LocString MusterHostingLead = new("conduct.muster.hosting.lead", "Your listing is a promise to everyone who shows up. Do not:");
        public static readonly LocString[] MusterHostingItems =
        {
            new("conduct.muster.hosting.1", "Fake locations or misleading listings"),
            new("conduct.muster.hosting.2", "List a muster you do not intend to host"),
            new("conduct.muster.hosting.3", "Leave a finished muster live in the directory"),
            new("conduct.muster.hosting.4", "Disappear on attendees instead of ending the muster"),
        };
        public static readonly LocString MusterAppropriateTitle = new("conduct.muster.appropriate.title", "Keep It Appropriate");
        public static readonly LocString MusterAppropriateLead = new("conduct.muster.appropriate.lead", "The directory is public and safe for work. Do not post:");
        public static readonly LocString[] MusterAppropriateItems =
        {
            new("conduct.muster.appropriate.1", "Explicit sexual content or nudity"),
            new("conduct.muster.appropriate.2", "ERP meetups or sexual solicitation"),
            new("conduct.muster.appropriate.3", "Fetish gatherings"),
            new("conduct.muster.appropriate.4", "NSFW descriptions or meeting spots"),
        };
        public static readonly LocString MusterChildSafetyTitle = new("conduct.muster.childSafety.title", "Child Safety");
        public static readonly LocString MusterChildSafetyBody = new("conduct.muster.childSafety.body", "Any content involving or sexualizing child-like characters or minors is strictly prohibited, regardless of lore or stated age.");
        public static readonly LocString MusterInGameTitle = new("conduct.muster.inGame.title", "In-Game Meetups Only");
        public static readonly LocString MusterInGameBody = new("conduct.muster.inGame.body", "Musters are for meeting inside the game. Do not use them to arrange real-life meetings or to collect personal contact details from attendees.");
        public static readonly LocString MusterRespectTitle = new("conduct.muster.respect.title", "Be Respectful");
        public static readonly LocString MusterRespectLead = new("conduct.muster.respect.lead", "Treat others with respect. Do not engage in:");
        public static readonly LocString[] MusterRespectItems =
        {
            new("conduct.muster.respect.1", "Harassment or bullying"),
            new("conduct.muster.respect.2", "Hate speech or slurs"),
            new("conduct.muster.respect.3", "Threats or targeted abuse"),
            new("conduct.muster.respect.4", "Musters aimed at singling out or harassing a player"),
            new("conduct.muster.respect.5", "Impersonation of real people or misinformation intended to deceive"),
        };
        public static readonly LocString MusterSpamTitle = new("conduct.muster.spam.title", "No Spam or Advertising");
        public static readonly LocString MusterSpamLead = new("conduct.muster.spam.lead", "Keep the directory worth browsing. Do not post:");
        public static readonly LocString[] MusterSpamItems =
        {
            new("conduct.muster.spam.1", "Repeated or duplicate musters"),
            new("conduct.muster.spam.2", "Venue, business, or service advertisements: use Yellow Pages instead"),
            new("conduct.muster.spam.3", "Paid services of any kind, whether gil or real money"),
            new("conduct.muster.spam.4", "Malicious links, scams, or phishing"),
        };
        public static readonly LocString MusterPrivacyTitle = new("conduct.muster.privacy.title", "Protect Privacy");
        public static readonly LocString MusterPrivacyBody = new("conduct.muster.privacy.body", "Do not share another person's personal information, private conversations, or confidential content without their permission.");
        public static readonly LocString MusterDiscretionTitle = new("conduct.muster.discretion.title", "Moderator Discretion");
        public static readonly LocString MusterDiscretionBody = new("conduct.muster.discretion.body", "Musters that are misleading, disruptive, or otherwise inappropriate for a public directory may be removed at moderator discretion.");

        public static readonly LocString YellowPagesTitle = new("conduct.yellowpages.title", "Yellow Pages Community Rules");
        public static readonly LocString YellowPagesIntro = new("conduct.yellowpages.intro", "Before you post an ad, please read the rules of the community.");
        public static readonly LocString YellowPagesAllowedTitle = new("conduct.yellowpages.allowed.title", "What Is Allowed");
        public static readonly LocString YellowPagesAllowedLead = new("conduct.yellowpages.allowed.lead", "Examples of acceptable ads include:");
        public static readonly LocString[] YellowPagesAllowedItems =
        {
            new("conduct.yellowpages.allowed.1", "Venue nights, game nights, and housing tours with honest schedules"),
            new("conduct.yellowpages.allowed.2", "Crafting, gathering, portraits, glamour, and performance work for gil"),
            new("conduct.yellowpages.allowed.3", "Recruiting for free companies, statics, and venue staff"),
            new("conduct.yellowpages.allowed.4", "Clear prices and clear expectations"),
            new("conduct.yellowpages.allowed.5", "Mod ads that show the work and link to the mod page: SFW mods only, no NSFW mods"),
            new("conduct.yellowpages.allowed.6", "Plugin ads only for modding and GPose plugins: QoL, UI, and automation are excluded"),
        };
        public static readonly LocString YellowPagesGilTitle = new("conduct.yellowpages.gil.title", "Gil Only");
        public static readonly LocString YellowPagesGilLead = new("conduct.yellowpages.gil.lead", "Ads may only ask for gil. The following are never allowed:");
        public static readonly LocString[] YellowPagesGilItems =
        {
            new("conduct.yellowpages.gil.1", "Real money, gift cards, or payments taken outside the game"),
            new("conduct.yellowpages.gil.2", "RMT, gil selling, account services, or third-party boosting shops"),
            new("conduct.yellowpages.gil.3", "Selling mods, plugins, or commissions for them: those ads link out and carry no price"),
            new("conduct.yellowpages.gil.4", "Trading account access or characters"),
        };
        public static readonly LocString YellowPagesHonestTitle = new("conduct.yellowpages.honest.title", "Honest Listings");
        public static readonly LocString YellowPagesHonestLead = new("conduct.yellowpages.honest.lead", "An ad is a promise to whoever answers it. Do not post:");
        public static readonly LocString[] YellowPagesHonestItems =
        {
            new("conduct.yellowpages.honest.1", "Fake listings or services you cannot deliver"),
            new("conduct.yellowpages.honest.2", "Prices, schedules, or turnaround times you cannot honor"),
            new("conduct.yellowpages.honest.3", "Impersonating another venue, crafter, or free company"),
            new("conduct.yellowpages.honest.4", "Reposting the same ad to dodge the expiry cycle"),
            new("conduct.yellowpages.honest.5", "Ads left live once the work or event is over"),
        };
        public static readonly LocString YellowPagesAppropriateTitle = new("conduct.yellowpages.appropriate.title", "Keep It Appropriate");
        public static readonly LocString YellowPagesAppropriateLead = new("conduct.yellowpages.appropriate.lead", "The After Dark tag marks mature venues and late-night events. It is not permission to post:");
        public static readonly LocString[] YellowPagesAppropriateItems =
        {
            new("conduct.yellowpages.appropriate.1", "Nudity or explicit sexual content"),
            new("conduct.yellowpages.appropriate.2", "ERP, escort, or sexual solicitation services"),
            new("conduct.yellowpages.appropriate.3", "Fetish services"),
            new("conduct.yellowpages.appropriate.4", "Explicit language in titles, photos, or ad text"),
        };
        public static readonly LocString YellowPagesChildSafetyTitle = new("conduct.yellowpages.childSafety.title", "Child Safety");
        public static readonly LocString YellowPagesChildSafetyBody = new("conduct.yellowpages.childSafety.body", "Any content involving or sexualizing child-like characters or minors is strictly prohibited, regardless of lore or stated age.");
        public static readonly LocString YellowPagesRespectTitle = new("conduct.yellowpages.respect.title", "Be Respectful");
        public static readonly LocString YellowPagesRespectLead = new("conduct.yellowpages.respect.lead", "Treat others with respect. Do not engage in:");
        public static readonly LocString[] YellowPagesRespectItems =
        {
            new("conduct.yellowpages.respect.1", "Harassment or bullying"),
            new("conduct.yellowpages.respect.2", "Hate speech or slurs"),
            new("conduct.yellowpages.respect.3", "Threats or targeted abuse"),
            new("conduct.yellowpages.respect.4", "Pressuring or badgering someone through inquiries"),
            new("conduct.yellowpages.respect.5", "Impersonation of real people or misinformation intended to deceive"),
        };
        public static readonly LocString YellowPagesSpamTitle = new("conduct.yellowpages.spam.title", "No Spam");
        public static readonly LocString YellowPagesSpamLead = new("conduct.yellowpages.spam.lead", "Keep the board worth browsing. Do not post:");
        public static readonly LocString[] YellowPagesSpamItems =
        {
            new("conduct.yellowpages.spam.1", "Repetitive or duplicate ads"),
            new("conduct.yellowpages.spam.2", "Ads filed under unrelated categories or tags"),
            new("conduct.yellowpages.spam.3", "Engagement manipulation or automated accounts"),
            new("conduct.yellowpages.spam.4", "Malicious links, scams, or phishing"),
        };
        public static readonly LocString YellowPagesPrivacyTitle = new("conduct.yellowpages.privacy.title", "Protect Privacy");
        public static readonly LocString YellowPagesPrivacyBody = new("conduct.yellowpages.privacy.body", "Do not share another person's personal information, private conversations, or confidential content without their permission.");
        public static readonly LocString YellowPagesCreatorsTitle = new("conduct.yellowpages.creators.title", "Respect Creators");
        public static readonly LocString YellowPagesCreatorsLead = new("conduct.yellowpages.creators.lead", "Only upload content you have the right to share. Do not:");
        public static readonly LocString[] YellowPagesCreatorsItems =
        {
            new("conduct.yellowpages.creators.1", "Post stolen or leaked content"),
            new("conduct.yellowpages.creators.2", "Remove watermarks or repost commissioned work without permission"),
            new("conduct.yellowpages.creators.3", "Claim someone else's creations as your own"),
            new("conduct.yellowpages.creators.4", "Post AI-generated content"),
        };
        public static readonly LocString YellowPagesDiscretionTitle = new("conduct.yellowpages.discretion.title", "Moderator Discretion");
        public static readonly LocString YellowPagesDiscretionBody = new("conduct.yellowpages.discretion.body", "Ads that are misleading, off-topic, or otherwise inappropriate for a public board may be removed at moderator discretion.");
    }

    internal static class Health
    {
        public static readonly LocString Title = new("health.title", "Health");
        public static readonly LocString Welcome = new("health.welcome", "Welcome");
        public static readonly LocString TabOverview = new("health.tabOverview", "Overview");
        public static readonly LocString TabActivity = new("health.tabActivity", "Activity");
        public static readonly LocString TabWater = new("health.tabWater", "Water");
        public static readonly LocString TabGoals = new("health.tabGoals", "Goals");
        public static readonly LocString TabHistory = new("health.tabHistory", "History");
        public static readonly LocString TabProfile = new("health.tabProfile", "Profile");
        public static readonly LocString LogInPrompt = new("health.logInPrompt", "Log in to view your adventurer's Health.");
        public static readonly LocString StepsTodayCaption = new("health.stepsTodayCaption", "estimated steps today · goal {0}");
        public static readonly LocString OnFoot = new("health.onFoot", "On foot");
        public static readonly LocString ActiveTime = new("health.activeTime", "Active time");
        public static readonly LocString EstEnergy = new("health.estEnergy", "Est. energy");
        public static readonly LocString Hydration = new("health.hydration", "Hydration");
        public static readonly LocString Kcal = new("health.kcal", "{0} kcal");
        public static readonly LocString GoalsSection = new("health.goalsSection", "Goals");
        public static readonly LocString NoActiveGoals = new("health.noActiveGoals", "No active goals. Add some on the Goals tab.");
        public static readonly LocString Streak = new("health.streak", "Streak");
        public static readonly LocString CurrentStreak = new("health.currentStreak", "Current streak");
        public static readonly LocPlural StreakDayCount = new("health.streakDays", "{0} day", "{0} days");
        public static readonly LocString Today = new("health.today", "Today");
        public static readonly LocString Session = new("health.session", "Session");
        public static readonly LocString AllTime = new("health.allTime", "All-time");
        public static readonly LocString Swimming = new("health.swimming", "Swimming");
        public static readonly LocString Diving = new("health.diving", "Diving");
        public static readonly LocString Mounted = new("health.mounted", "Mounted travel");
        public static readonly LocString Flying = new("health.flying", "Flying");
        public static readonly LocString Teleports = new("health.teleports", "Teleports");
        public static readonly LocString DistanceSkipped = new("health.distanceSkipped", "Distance skipped");
        public static readonly LocString TeleportHint = new("health.teleportHint", "Teleport distance skipped is a same-map straight-line estimate only; cross-zone teleports are counted without distance.");
        public static readonly LocString Records = new("health.records", "Records");
        public static readonly LocString MostStepsInDay = new("health.mostStepsInDay", "Most steps in a day");
        public static readonly LocString LongestOnFootSession = new("health.longestOnFootSession", "Longest on-foot session");
        public static readonly LocString LongestSwimSession = new("health.longestSwimSession", "Longest swim session");
        public static readonly LocString DrinksToday = new("health.drinksToday", "{0} / {1} drinks today");
        public static readonly LocString DrinkWater = new("health.drinkWater", "Drink Water");
        public static readonly LocString DrinkKindWater = new("health.drinkKindWater", "Water");
        public static readonly LocString DrinkKindTea = new("health.drinkKindTea", "Tea");
        public static readonly LocString DrinkKindCoffee = new("health.drinkKindCoffee", "Coffee");
        public static readonly LocString DrinkKindJuice = new("health.drinkKindJuice", "Juice");
        public static readonly LocString CustomDrink = new("health.customDrink", "Custom drink");
        public static readonly LocString Name = new("health.name", "Name");
        public static readonly LocString ServingMl = new("health.servingMl", "Serving (ml)");
        public static readonly LocString LogCustomDrink = new("health.logCustomDrink", "Log custom drink");
        public static readonly LocString DrinkFallback = new("health.drinkFallback", "Drink");
        public static readonly LocString UndoLastDrink = new("health.undoLastDrink", "Undo last drink");
        public static readonly LocString DailyGoalDrinks = new("health.dailyGoalDrinks", "Daily goal (drinks)");
        public static readonly LocString NoDrinksToday = new("health.noDrinksToday", "No drinks logged yet today.");
        public static readonly LocString DrinkEntry = new("health.drinkEntry", "{0}  {1}");
        public static readonly LocString Reminders = new("health.reminders", "Reminders");
        public static readonly LocString HydrationReminders = new("health.hydrationReminders", "Hydration reminders");
        public static readonly LocString EveryMinutes = new("health.everyMinutes", "Every (min)");
        public static readonly LocString QuietFrom = new("health.quietFrom", "Quiet from");
        public static readonly LocString QuietUntil = new("health.quietUntil", "Quiet until");
        public static readonly LocString PauseDuringDuties = new("health.pauseDuringDuties", "Pause during combat / duties");
        public static readonly LocString Edit = new("health.edit", "Edit");
        public static readonly LocString EditDisabled = new("health.editDisabled", "Edit (disabled)");
        public static readonly LocString AddGoal = new("health.addGoal", "Add goal");
        public static readonly LocString NewGoal = new("health.newGoal", "New goal");
        public static readonly LocString ResetDefaultGoals = new("health.resetDefaultGoals", "Reset to default goals");
        public static readonly LocString ResetGoalsTitle = new("health.resetGoalsTitle", "Reset goals");
        public static readonly LocString ResetGoalsMessage = new("health.resetGoalsMessage", "Replace your goals with the defaults?");
        public static readonly LocString Reset = new("health.reset", "Reset");
        public static readonly LocString Cancel = new("health.cancel", "Cancel");
        public static readonly LocString Confirm = new("health.confirm", "Confirm");
        public static readonly LocString Type = new("health.type", "Type");
        public static readonly LocString Scope = new("health.scope", "Scope");
        public static readonly LocString Target = new("health.target", "Target");
        public static readonly LocString Enabled = new("health.enabled", "Enabled");
        public static readonly LocString DeleteGoal = new("health.deleteGoal", "Delete goal");
        public static readonly LocString Done = new("health.done", "Done");
        public static readonly LocString GoalFallback = new("health.goalFallback", "Goal");
        public static readonly LocString TypeSteps = new("health.typeSteps", "Steps");
        public static readonly LocString TypeOnFootDistance = new("health.typeOnFootDistance", "On-foot distance");
        public static readonly LocString TypeWalkingDistance = new("health.typeWalkingDistance", "Walking distance");
        public static readonly LocString TypeRunningDistance = new("health.typeRunningDistance", "Running distance");
        public static readonly LocString TypeSwimmingDistance = new("health.typeSwimmingDistance", "Swimming distance");
        public static readonly LocString TypeActiveTime = new("health.typeActiveTime", "Active time");
        public static readonly LocString TypeDrinksLogged = new("health.typeDrinksLogged", "Drinks logged");
        public static readonly LocString TypeDrinkVolume = new("health.typeDrinkVolume", "Drink volume");
        public static readonly LocString TypeTeleports = new("health.typeTeleports", "Teleports");
        public static readonly LocString TypeTeleportDistance = new("health.typeTeleportDistance", "Teleport distance");
        public static readonly LocString TypeEnergy = new("health.typeEnergy", "Est. energy");
        public static readonly LocString ScopeDaily = new("health.scopeDaily", "Daily");
        public static readonly LocString ScopeWeekly = new("health.scopeWeekly", "Weekly");
        public static readonly LocString ScopeSession = new("health.scopeSession", "Session");
        public static readonly LocString ScopeAllTime = new("health.scopeAllTime", "All-time");
        public static readonly LocString NoActivity = new("health.noActivity", "No activity recorded yet.");
        public static readonly LocString HistoryDayHeader = new("health.historyDayHeader", "{0}  ·  {1} goals · {2} tp");
        public static readonly LocString StepsValue = new("health.stepsValue", "{0} steps");
        public static readonly LocString Active = new("health.active", "Active");
        public static readonly LocString DrinksValue = new("health.drinksValue", "{0} drinks");
        public static readonly LocString Energy = new("health.energy", "Energy");
        public static readonly LocString Adventurer = new("health.adventurer", "Adventurer");
        public static readonly LocString ProfileSummary = new("health.profileSummary", "Profile summary");
        public static readonly LocString World = new("health.world", "World");
        public static readonly LocString RaceClan = new("health.raceClan", "Race / Clan");
        public static readonly LocString RaceClanValue = new("health.raceClanValue", "{0} / {1}");
        public static readonly LocString Height = new("health.height", "Height");
        public static readonly LocString Reading = new("health.reading", "Reading");
        public static readonly LocString HeightSourceGame = new("health.heightSourceGame", "Game");
        public static readonly LocString HeightSourceManual = new("health.heightSourceManual", "Manual");
        public static readonly LocString HeightSourceUnavailable = new("health.heightSourceUnavailable", "Unavailable");
        public static readonly LocString HeightWithSource = new("health.heightWithSource", "{0}  ·  {1}");
        public static readonly LocString RefreshHeight = new("health.refreshHeight", "Refresh height");
        public static readonly LocString AutoRefreshHeight = new("health.autoRefreshHeight", "Auto-refresh on change");
        public static readonly LocString ManualOverrideCm = new("health.manualOverrideCm", "Manual override (cm)");
        public static readonly LocString OverrideOff = new("health.overrideOff", "off");
        public static readonly LocString ClearOverride = new("health.clearOverride", "Clear override");
        public static readonly LocString FictionalWeight = new("health.fictionalWeight", "Fictional weight");
        public static readonly LocString Current = new("health.current", "Current");
        public static readonly LocString NotSet = new("health.notSet", "not set");
        public static readonly LocString EnterWeight = new("health.enterWeight", "Enter weight ({0})");
        public static readonly LocString WeightLabel = new("health.weightLabel", "Weight ({0})");
        public static readonly LocString SetWeight = new("health.setWeight", "Set weight");
        public static readonly LocString ClearWeight = new("health.clearWeight", "Clear weight");
        public static readonly LocString EstimateActivityEnergy = new("health.estimateActivityEnergy", "Estimate activity energy");
        public static readonly LocString WeightHint = new("health.weightHint", "Character weight is optional and used only for fictional activity-energy estimates.");
        public static readonly LocString SuggestedTapToUse = new("health.suggestedTapToUse", "Suggested (tap to use)");
        public static readonly LocString SuggestionEntry = new("health.suggestionEntry", "{0}  ·  {1}");
        public static readonly LocString SuggestLean = new("health.suggestLean", "Lean");
        public static readonly LocString SuggestAverage = new("health.suggestAverage", "Average");
        public static readonly LocString SuggestSturdy = new("health.suggestSturdy", "Sturdy");
        public static readonly LocString SuggestionHint = new("health.suggestionHint", "Fictional estimates from your character's height and build.");
        public static readonly LocString Units = new("health.units", "Units");
        public static readonly LocString UnitEorzean = new("health.unitEorzean", "Eorzean");
        public static readonly LocString UnitMetric = new("health.unitMetric", "Metric");
        public static readonly LocString UnitImperial = new("health.unitImperial", "Imperial");
        public static readonly LocString UnitEorzeanSub = new("health.unitEorzeanSub", "Yalms / Malms / Ponz");
        public static readonly LocString UnitMetricSub = new("health.unitMetricSub", "Metres / km / kg / ml");
        public static readonly LocString UnitImperialSub = new("health.unitImperialSub", "Feet / miles / lb / fl oz");
        public static readonly LocString StrideLength = new("health.strideLength", "Stride length");
        public static readonly LocString YalmsPerStep = new("health.yalmsPerStep", "Yalms per step");
        public static readonly LocString SuggestFromHeight = new("health.suggestFromHeight", "Suggest from height");
        public static readonly LocString SuggestStrideFromHeight = new("health.suggestStrideFromHeight", "Suggest stride from height");
        public static readonly LocString StrideHint = new("health.strideHint", "Only walking and running produce steps. Raw distance is stored, so changing stride never loses progress.");
        public static readonly LocString StrideHintSetup = new("health.strideHintSetup", "Only walking and running produce estimated steps. Raw distance is stored, so changing stride never loses progress.");
        public static readonly LocString TrackingStatus = new("health.trackingStatus", "Tracking status");
        public static readonly LocString Status = new("health.status", "Status");
        public static readonly LocString ResetSection = new("health.resetSection", "Reset");
        public static readonly LocString ResetSession = new("health.resetSession", "Reset session");
        public static readonly LocString ResetToday = new("health.resetToday", "Reset today");
        public static readonly LocString ResetTodayConfirm = new("health.resetTodayConfirm", "Reset today's activity?");
        public static readonly LocString ResetTodayHydration = new("health.resetTodayHydration", "Reset today's hydration");
        public static readonly LocString ResetTodayHydrationConfirm = new("health.resetTodayHydrationConfirm", "Clear today's hydration entries?");
        public static readonly LocString ResetHistory = new("health.resetHistory", "Reset history");
        public static readonly LocString ResetHistoryConfirm = new("health.resetHistoryConfirm", "Delete recent activity history?");
        public static readonly LocString ResetRecords = new("health.resetRecords", "Reset personal records");
        public static readonly LocString ResetRecordsConfirm = new("health.resetRecordsConfirm", "Reset personal records?");
        public static readonly LocString ResetAll = new("health.resetAll", "Reset all Health data");
        public static readonly LocString ResetAllConfirm = new("health.resetAllConfirm", "Erase ALL Health data for this character? This cannot be undone.");
        public static readonly LocString Disclaimer = new("health.disclaimer", "Health tracks fictional activity performed by your FFXIV character. Its steps, calories, hydration, and wellness values are estimates intended for roleplay and statistics.");
        public static readonly LocString DisclaimerShort = new("health.disclaimerShort", "Health tracks fictional activity performed by your FFXIV character. Its values are estimates intended for roleplay and statistics.");
        public static readonly LocString WelcomeAdventurer = new("health.welcomeAdventurer", "Welcome, Adventurer!");
        public static readonly LocString SetupSub1 = new("health.setupSub1", "Let's set up your adventurer's health profile.");
        public static readonly LocString SetupSub2 = new("health.setupSub2", "Choose your daily expedition goals.");
        public static readonly LocString SetupSub3 = new("health.setupSub3", "Optional fictional energy estimates.");
        public static readonly LocString SetupSub4 = new("health.setupSub4", "Tune how travel becomes estimated steps.");
        public static readonly LocString SetupSub5 = new("health.setupSub5", "Review your profile and begin.");
        public static readonly LocString StepOf = new("health.stepOf", "Step {0} of {1}  ·  {2}");
        public static readonly LocString PreferredUnits = new("health.preferredUnits", "Preferred units");
        public static readonly LocString DailyGoals = new("health.dailyGoals", "Daily goals");
        public static readonly LocString Steps = new("health.steps", "Steps");
        public static readonly LocString SwimmingYalms = new("health.swimmingYalms", "Swimming (yalms)");
        public static readonly LocString HydrationDrinks = new("health.hydrationDrinks", "Hydration (drinks)");
        public static readonly LocString FictionalEnergy = new("health.fictionalEnergy", "Fictional energy");
        public static readonly LocString Movement = new("health.movement", "Movement");
        public static readonly LocString Review = new("health.review", "Review");
        public static readonly LocString StepsGoal = new("health.stepsGoal", "Steps goal");
        public static readonly LocString SwimGoal = new("health.swimGoal", "Swim goal");
        public static readonly LocString HydrationGoal = new("health.hydrationGoal", "Hydration goal");
        public static readonly LocString Weight = new("health.weight", "Weight");
        public static readonly LocString EnergyEstimates = new("health.energyEstimates", "Energy estimates");
        public static readonly LocString On = new("health.on", "On");
        public static readonly LocString Off = new("health.off", "Off");
        public static readonly LocString DrinksSuffix = new("health.drinksSuffix", "{0} drinks");
        public static readonly LocString Back = new("health.back", "Back");
        public static readonly LocString Begin = new("health.begin", "Begin");
        public static readonly LocString Next = new("health.next", "Next");
        public static readonly LocString WeightUnitKg = new("health.weightUnitKg", "kg");
        public static readonly LocString WeightUnitLb = new("health.weightUnitLb", "lb");
        public static readonly LocString WeightUnitPonz = new("health.weightUnitPonz", "ponz");
        public static readonly LocString StatusNotLoggedIn = new("health.statusNotLoggedIn", "Paused: not logged in");
        public static readonly LocString StatusTrackingSwimming = new("health.statusTrackingSwimming", "Tracking swimming");
        public static readonly LocString StatusTrackingOnFoot = new("health.statusTrackingOnFoot", "Tracking on-foot movement");
        public static readonly LocString StatusPaused = new("health.statusPaused", "Paused");
        public static readonly LocString StatusPlayerUnavailable = new("health.statusPlayerUnavailable", "Paused: player unavailable");
        public static readonly LocString StatusLoading = new("health.statusLoading", "Paused: loading");
        public static readonly LocString StatusMounted = new("health.statusMounted", "Paused: mounted");
        public static readonly LocString StatusFlying = new("health.statusFlying", "Paused: flying");
        public static readonly LocString StatusIdle = new("health.statusIdle", "Idle");
        public static readonly LocString NotifyHydrationTitle = new("health.notifyHydrationTitle", "Hydration");
        public static readonly LocString NotifyHydrationBody = new("health.notifyHydrationBody", "Your adventurer has not logged a drink recently.");
        public static readonly LocString NotifyGoalTitle = new("health.notifyGoalTitle", "Goal complete");
        public static readonly LocString NotifyGoalBody = new("health.notifyGoalBody", "{0} - done!");
        public static readonly LocString DefaultGoalWalk1000 = new("health.defaultGoalWalk1000", "Walk 1,000 steps");
        public static readonly LocString DefaultGoalWalk5000 = new("health.defaultGoalWalk5000", "Walk 5,000 steps");
        public static readonly LocString DefaultGoalWalk10000 = new("health.defaultGoalWalk10000", "Walk 10,000 steps");
        public static readonly LocString DefaultGoalWalkMalm = new("health.defaultGoalWalkMalm", "Walk 1 malm");
        public static readonly LocString DefaultGoalSwim500 = new("health.defaultGoalSwim500", "Swim 500 yalms");
        public static readonly LocString DefaultGoalDrinks = new("health.defaultGoalDrinks", "Log 4 drinks");
        public static readonly LocString DefaultGoalActive30 = new("health.defaultGoalActive30", "Remain active for 30 minutes");
        public static readonly LocString UnitKm = new("health.unitKm", " km");
        public static readonly LocString UnitM = new("health.unitM", " m");
        public static readonly LocString UnitMi = new("health.unitMi", " mi");
        public static readonly LocString UnitFt = new("health.unitFt", " ft");
        public static readonly LocString UnitMalms = new("health.unitMalms", " malms");
        public static readonly LocString UnitYalms = new("health.unitYalms", " yalms");
        public static readonly LocString UnitCm = new("health.unitCm", " cm");
        public static readonly LocString UnitFulm = new("health.unitFulm", " fulm");
        public static readonly LocString UnitIlm = new("health.unitIlm", " ilm");
        public static readonly LocString UnitKg = new("health.unitKg", " kg");
        public static readonly LocString UnitPonz = new("health.unitPonz", " ponz");
        public static readonly LocString UnitLb = new("health.unitLb", " lb");
        public static readonly LocString UnitFlOz = new("health.unitFlOz", " fl oz");
        public static readonly LocString UnitLitre = new("health.unitLitre", " L");
        public static readonly LocString UnitMl = new("health.unitMl", " ml");
        public static readonly LocString HeightImperial = new("health.heightImperial", "{0}{1} {2}{3}");
        public static readonly LocString DurationHm = new("health.durationHm", "{0}h {1}m");
        public static readonly LocString DurationM = new("health.durationM", "{0}m");
    }
}
