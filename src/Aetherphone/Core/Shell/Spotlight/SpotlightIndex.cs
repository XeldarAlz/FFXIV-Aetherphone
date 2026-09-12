using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Message;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Market;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell.Spotlight;

internal enum SpotlightKind : byte
{
    Calculation,
    App,
    Action,
    Contact,
    DmThread,
    SettingsPage,
    Shortcut,
    Aetheryte,
    Conversation,
    Note,
    Guide,
    Venue,
    MarketItem,
    StoreApp,
}

internal readonly struct SpotlightResult
{
    public readonly SpotlightKind Kind;
    public readonly string Title;
    public readonly string Subtitle;
    public readonly string Payload;
    public readonly uint ItemId;
    public readonly Guid EntityId;
    public readonly int PageIndex;
    public readonly int Score;

    public SpotlightResult(SpotlightKind kind, string title, string subtitle, string payload, uint itemId,
        Guid entityId, int pageIndex, int score)
    {
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        Payload = payload;
        ItemId = itemId;
        EntityId = entityId;
        PageIndex = pageIndex;
        Score = score;
    }
}

internal sealed class SpotlightIndex
{
    private const int KindCount = 14;
    private const int MaxApps = 6;
    private const int MaxActions = 4;
    private const int MaxContacts = 5;
    private const int MaxDmThreads = 5;
    private const int MaxSettings = 5;
    private const int MaxShortcuts = 4;
    private const int MaxAetherytes = 5;
    private const int MaxConversations = 4;
    private const int MaxNotes = 4;
    private const int MaxGuides = 4;
    private const int MaxVenues = 4;
    private const int MaxItems = 5;
    private const int MaxStoreApps = 3;

    private const int CalculationBias = 2000;
    private const int AppBias = 300;
    private const int ActionBias = 240;
    private const int ContactBias = 220;
    private const int DmThreadBias = 210;
    private const int SettingsBias = 200;
    private const int ShortcutBias = 190;
    private const int AetheryteBias = 160;
    private const int ConversationBias = 130;
    private const int NoteBias = 120;
    private const int GuideBias = 100;
    private const int VenueBias = 90;
    private const int MarketBias = 70;
    private const int StoreBias = 20;

    private const int ExactQuality = 1000;
    private const int PrefixQuality = 800;
    private const int WordStartQuality = 600;
    private const int ContainsQuality = 400;
    private const int LengthPenaltyCap = 48;

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly AppInstaller installer;
    private readonly ContactBook contacts;
    private readonly DmLauncher dmLauncher;
    private readonly ChatSearch chatSearch = new();
    private readonly ChatInbox chatInbox;
    private readonly ChatLog chatLog;
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly MarketItemIndex marketIndex;
    private readonly MarketLauncher marketLauncher;
    private readonly ShortcutStore shortcuts;
    private readonly ShortcutRunner shortcutRunner;
    private readonly MapData maps;
    private readonly StratsManifestStore stratsManifest;
    private readonly VenuesService venues;
    private readonly ThemeProvider themes;
    private readonly CallHub calls;
    private readonly Configuration configuration;
    private readonly ISpotlightPages? settingsPages;
    private readonly ISpotlightNotes? noteTarget;
    private readonly ISpotlightConversations? conversationSource;
    private readonly ISpotlightStoreApps? storeTarget;
    private readonly ISpotlightFights? fightTarget;
    private readonly ISpotlightVenues? venueTarget;
    private readonly List<SpotlightResult> results = new();
    private readonly List<MarketItemRef> marketScratch = new();
    private readonly int[] sectionBest = new int[KindCount];
    private readonly SectionComparer comparer;
    private string lastQuery = string.Empty;

    public SpotlightIndex(IReadOnlyList<IPhoneApp> apps, AppInstaller installer, ContactBook contacts,
        DmLauncher dmLauncher, ChatInbox chatInbox, ChatLog chatLog, LinkpearlLauncher linkpearlLauncher,
        MarketItemIndex marketIndex, MarketLauncher marketLauncher, ShortcutStore shortcuts,
        ShortcutRunner shortcutRunner, MapData maps, StratsManifestStore stratsManifest, VenuesService venues,
        ThemeProvider themes, CallHub calls, Configuration configuration)
    {
        this.apps = apps;
        this.installer = installer;
        this.contacts = contacts;
        this.dmLauncher = dmLauncher;
        this.chatInbox = chatInbox;
        this.chatLog = chatLog;
        this.linkpearlLauncher = linkpearlLauncher;
        this.marketIndex = marketIndex;
        this.marketLauncher = marketLauncher;
        this.shortcuts = shortcuts;
        this.shortcutRunner = shortcutRunner;
        this.maps = maps;
        this.stratsManifest = stratsManifest;
        this.venues = venues;
        this.themes = themes;
        this.calls = calls;
        this.configuration = configuration;
        comparer = new SectionComparer(sectionBest);
        for (var index = 0; index < apps.Count; index++)
        {
            settingsPages ??= apps[index] as ISpotlightPages;
            noteTarget ??= apps[index] as ISpotlightNotes;
            conversationSource ??= apps[index] as ISpotlightConversations;
            storeTarget ??= apps[index] as ISpotlightStoreApps;
            fightTarget ??= apps[index] as ISpotlightFights;
            venueTarget ??= apps[index] as ISpotlightVenues;
        }
    }

    public IReadOnlyList<SpotlightResult> Results => results;

    public bool CallsAvailable => configuration.CallsEnabled;

    public void Clear()
    {
        results.Clear();
        lastQuery = string.Empty;
    }

    public void Search(string query)
    {
        if (string.Equals(query, lastQuery, StringComparison.Ordinal))
        {
            return;
        }

        lastQuery = query;
        results.Clear();
        var trimmed = query.Trim();
        if (trimmed.Length < 2)
        {
            return;
        }

        CollectCalculation(trimmed);
        CollectApps(trimmed);
        CollectActions(trimmed);
        CollectContacts(trimmed);
        CollectDmThreads(trimmed);
        CollectSettings(trimmed);
        CollectShortcuts(trimmed);
        CollectAetherytes(trimmed);
        CollectConversations(trimmed);
        CollectNotes(trimmed);
        CollectGuides(trimmed);
        CollectVenues(trimmed);
        CollectMarketItems(trimmed);
        CollectStoreApps(trimmed);
        Rank();
    }

    public void Activate(in SpotlightResult result, INavigator navigation)
    {
        switch (result.Kind)
        {
            case SpotlightKind.Calculation:
                ImGui.SetClipboardText(result.Title);
                ShellToast.Show();
                break;
            case SpotlightKind.App:
                navigation.Open(result.Payload);
                break;
            case SpotlightKind.Action:
                SpotlightActions.Run((SpotlightActionKind)result.PageIndex, configuration, themes, calls, noteTarget,
                    navigation);
                break;
            case SpotlightKind.Contact:
                dmLauncher.RequestUser(result.Payload);
                navigation.Open("message");
                break;
            case SpotlightKind.DmThread:
                dmLauncher.RequestConversation(result.Payload);
                navigation.Open("message");
                break;
            case SpotlightKind.SettingsPage:
                if (settingsPages is not null && result.PageIndex < settingsPages.SpotlightPageCount)
                {
                    settingsPages.RequestSpotlightPage(result.PageIndex);
                }

                navigation.Open("settings");
                break;
            case SpotlightKind.Shortcut:
                RunShortcut(result.EntityId);
                break;
            case SpotlightKind.Aetheryte:
                Teleport(result);
                break;
            case SpotlightKind.Conversation:
                linkpearlLauncher.Request(result.Payload);
                navigation.Open("messages");
                break;
            case SpotlightKind.Note:
                noteTarget?.RequestNote(result.EntityId);
                navigation.Open("notes");
                break;
            case SpotlightKind.Guide:
                fightTarget?.RequestFight(result.Payload);
                navigation.Open(StratsContent.AppId);
                break;
            case SpotlightKind.Venue:
                venueTarget?.RequestVenue(result.Payload);
                navigation.Open("venues");
                break;
            case SpotlightKind.MarketItem:
                marketLauncher.RequestItem(result.ItemId);
                navigation.Open("market");
                break;
            case SpotlightKind.StoreApp:
                storeTarget?.RequestStoreApp(result.Payload);
                navigation.Open("appstore");
                break;
        }
    }

    public void Call(in SpotlightResult result, INavigator navigation)
    {
        calls.StartCall(new CallContact(result.Payload, string.Empty, string.Empty, result.Title));
        dmLauncher.RequestCalls();
        navigation.Open("message");
    }

    private void RunShortcut(Guid shortcutId)
    {
        var entry = shortcuts.Find(shortcutId);
        if (entry is null)
        {
            return;
        }

        shortcutRunner.Run(entry);
    }

    private static void Teleport(in SpotlightResult result)
    {
        if (LifestreamBridge.TeleportToAetheryte(result.ItemId) != LifestreamOutcome.NotInstalled)
        {
            return;
        }

        ImGui.SetClipboardText(LifestreamBridge.AetheryteCommand(result.Payload));
        ShellToast.Show();
    }

    private void Rank()
    {
        for (var slot = 0; slot < sectionBest.Length; slot++)
        {
            sectionBest[slot] = int.MinValue;
        }

        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var slot = (int)result.Kind;
            if (result.Score > sectionBest[slot])
            {
                sectionBest[slot] = result.Score;
            }
        }

        results.Sort(comparer);
    }

    private static int Match(string text, string query)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var position = text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
        if (position < 0)
        {
            return 0;
        }

        int quality;
        if (position == 0)
        {
            quality = text.Length == query.Length ? ExactQuality : PrefixQuality;
        }
        else
        {
            quality = IsWordStart(text, position) ? WordStartQuality : ContainsQuality;
        }

        return quality - Math.Min(text.Length, LengthPenaltyCap);
    }

    private static bool IsWordStart(string text, int position) => !char.IsLetterOrDigit(text[position - 1]);

    private void CollectCalculation(string query)
    {
        if (!SpotlightMath.TryEvaluate(query, out var formatted))
        {
            return;
        }

        results.Add(new SpotlightResult(SpotlightKind.Calculation, formatted, query, string.Empty, 0, Guid.Empty, 0,
            CalculationBias));
    }

    private void CollectApps(string query)
    {
        var added = 0;
        for (var index = 0; index < apps.Count && added < MaxApps; index++)
        {
            var app = apps[index];
            if (!installer.IsInstalled(app.Id) || !app.IsAvailable)
            {
                continue;
            }

            var entry = AppStoreCatalog.For(app.Id);
            var score = Match(app.DisplayName, query);
            if (score == 0)
            {
                score = Match(app.Id, query) / 2;
            }

            if (score == 0)
            {
                score = Match(Loc.T(entry.Subtitle), query) / 3;
            }

            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.App, app.DisplayName, Loc.T(entry.Subtitle), app.Id,
                0, Guid.Empty, 0, AppBias + score));
            added++;
        }
    }

    private void CollectActions(string query)
    {
        var added = 0;
        var actions = SpotlightActions.All;
        for (var index = 0; index < actions.Length && added < MaxActions; index++)
        {
            var kind = actions[index];
            var label = Loc.T(SpotlightActions.Label(kind));
            var score = Match(label, query);
            if (score == 0 && SpotlightActions.IsAppearance(kind))
            {
                score = Match(Loc.T(L.Settings.Theme), query);
            }

            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Action, label,
                SpotlightActions.Subtitle(kind, configuration), string.Empty, 0, Guid.Empty, (int)kind,
                ActionBias + score));
            added++;
        }
    }

    private void CollectStoreApps(string query)
    {
        if (!installer.IsInstalled("appstore"))
        {
            return;
        }

        var added = 0;
        for (var index = 0; index < apps.Count && added < MaxStoreApps; index++)
        {
            var app = apps[index];
            if (installer.IsInstalled(app.Id) || !app.IsAvailable)
            {
                continue;
            }

            var score = Match(app.DisplayName, query);
            if (score == 0)
            {
                continue;
            }

            var entry = AppStoreCatalog.For(app.Id);
            results.Add(new SpotlightResult(SpotlightKind.StoreApp, app.DisplayName, Loc.T(entry.Subtitle), app.Id,
                0, Guid.Empty, 0, StoreBias + score));
            added++;
        }
    }

    private void CollectContacts(string query)
    {
        var added = 0;
        var list = contacts.Contacts;
        for (var index = 0; index < list.Length && added < MaxContacts; index++)
        {
            var contact = list[index];
            var score = Math.Max(Match(contact.Alias, query), Match(contact.DisplayName, query));
            score = Math.Max(score, Match(contact.Handle, query));
            score = Math.Max(score, Match(contact.PhoneNumber, query));
            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Contact, ContactBook.DisplayLabel(contact),
                ContactBook.Format(contact.PhoneNumber), contact.UserId, 0, Guid.Empty, 0, ContactBias + score));
            added++;
        }
    }

    private void CollectDmThreads(string query)
    {
        if (conversationSource is null)
        {
            return;
        }

        var added = 0;
        var threads = conversationSource.SpotlightConversations;
        for (var index = 0; index < threads.Length && added < MaxDmThreads; index++)
        {
            var thread = threads[index];
            var title = ConversationTitle.Of(thread, contacts);
            var score = Math.Max(Match(title, query), Match(thread.OtherHandle, query));
            score = Math.Max(score, Match(thread.OtherDisplayName, query));
            if (score == 0)
            {
                score = Match(thread.LastMessagePreview, query) / 2;
            }

            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.DmThread, title,
                ChatText.ListPreview(thread.LastMessagePreview), thread.Id, 0, Guid.Empty, 0, DmThreadBias + score));
            added++;
        }
    }

    private void CollectSettings(string query)
    {
        if (settingsPages is null)
        {
            return;
        }

        var added = 0;
        var pageCount = settingsPages.SpotlightPageCount;
        for (var index = 0; index < pageCount && added < MaxSettings; index++)
        {
            var title = settingsPages.SpotlightPageTitle(index);
            var score = Match(title, query);
            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.SettingsPage, title, string.Empty, string.Empty,
                0, Guid.Empty, index, SettingsBias + score));
            added++;
        }
    }

    private void CollectShortcuts(string query)
    {
        var added = 0;
        var entries = shortcuts.All;
        for (var index = 0; index < entries.Count && added < MaxShortcuts; index++)
        {
            var entry = entries[index];
            var score = Match(entry.Name, query);
            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Shortcut, entry.Name, string.Empty, string.Empty, 0,
                entry.Id, 0, ShortcutBias + score));
            added++;
        }
    }

    private void CollectAetherytes(string query)
    {
        if (!installer.IsInstalled("maps"))
        {
            return;
        }

        var added = 0;
        var regions = maps.Regions;
        for (var regionIndex = 0; regionIndex < regions.Count && added < MaxAetherytes; regionIndex++)
        {
            var region = regions[regionIndex];
            for (var entryIndex = 0; entryIndex < region.Aetherytes.Count && added < MaxAetherytes; entryIndex++)
            {
                var aetheryte = region.Aetherytes[entryIndex];
                var score = Match(aetheryte.Name, query);
                if (score == 0)
                {
                    continue;
                }

                results.Add(new SpotlightResult(SpotlightKind.Aetheryte, aetheryte.Name, region.Name, aetheryte.Name,
                    aetheryte.RowId, Guid.Empty, 0, AetheryteBias + score));
                added++;
            }
        }
    }

    private void CollectConversations(string query)
    {
        chatSearch.Run(query, chatInbox, chatLog);
        var hits = chatSearch.Hits;
        var added = 0;
        for (var index = 0; index < hits.Count && added < MaxConversations; index++)
        {
            var hit = hits[index];
            var score = Math.Max(Match(hit.Title, query), Match(hit.Entry.Text, query) / 2);
            results.Add(new SpotlightResult(SpotlightKind.Conversation, hit.Title, hit.Entry.Text,
                hit.ConversationKey, 0, Guid.Empty, 0, ConversationBias + score));
            added++;
        }
    }

    private void CollectNotes(string query)
    {
        var added = 0;
        var notes = configuration.Notes;
        for (var index = 0; index < notes.Count && added < MaxNotes; index++)
        {
            var note = notes[index];
            var title = note.Title();
            var score = Math.Max(Match(title, query), Match(note.Body, query) / 2);
            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Note, title, note.Preview(), string.Empty,
                0, note.Id, 0, NoteBias + score));
            added++;
        }
    }

    private void CollectGuides(string query)
    {
        var manifest = stratsManifest.Manifest;
        if (manifest is null || !installer.IsInstalled(StratsContent.AppId))
        {
            return;
        }

        var added = 0;
        var groups = manifest.Groups;
        for (var groupIndex = 0; groupIndex < groups.Length && added < MaxGuides; groupIndex++)
        {
            var fights = groups[groupIndex].Fights;
            for (var fightIndex = 0; fightIndex < fights.Length && added < MaxGuides; fightIndex++)
            {
                var fight = fights[fightIndex];
                var score = Math.Max(Match(fight.Title, query), Match(fight.Abbrev, query));
                if (score == 0)
                {
                    continue;
                }

                results.Add(new SpotlightResult(SpotlightKind.Guide, fight.Title, fight.Subtitle, fight.Key, 0,
                    Guid.Empty, 0, GuideBias + score));
                added++;
            }
        }
    }

    private void CollectVenues(string query)
    {
        if (!installer.IsInstalled("venues"))
        {
            return;
        }

        var added = 0;
        var events = venues.Events;
        for (var index = 0; index < events.Count && added < MaxVenues; index++)
        {
            var venue = events[index];
            var score = Math.Max(Match(venue.Title, query), Match(venue.Host, query) / 2);
            if (score == 0)
            {
                continue;
            }

            results.Add(new SpotlightResult(SpotlightKind.Venue, venue.Title, venue.LocationLine, venue.Id, 0,
                Guid.Empty, 0, VenueBias + score));
            added++;
        }
    }

    private void CollectMarketItems(string query)
    {
        if (!marketIndex.Ready || !installer.IsInstalled("market"))
        {
            return;
        }

        marketScratch.Clear();
        marketIndex.Search(query, marketScratch, MaxItems);
        for (var index = 0; index < marketScratch.Count; index++)
        {
            var name = marketScratch[index].Name;
            results.Add(new SpotlightResult(SpotlightKind.MarketItem, name, string.Empty,
                string.Empty, marketScratch[index].Id, Guid.Empty, 0, MarketBias + Match(name, query)));
        }
    }

    private sealed class SectionComparer : IComparer<SpotlightResult>
    {
        private readonly int[] sectionBest;

        public SectionComparer(int[] sectionBest)
        {
            this.sectionBest = sectionBest;
        }

        public int Compare(SpotlightResult left, SpotlightResult right)
        {
            var sectionDelta = sectionBest[(int)right.Kind] - sectionBest[(int)left.Kind];
            if (sectionDelta != 0)
            {
                return sectionDelta;
            }

            if (left.Kind != right.Kind)
            {
                return (int)left.Kind - (int)right.Kind;
            }

            var scoreDelta = right.Score - left.Score;
            if (scoreDelta != 0)
            {
                return scoreDelta;
            }

            return string.CompareOrdinal(left.Title, right.Title);
        }
    }
}
