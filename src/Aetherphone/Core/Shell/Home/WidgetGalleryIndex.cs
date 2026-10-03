using System.Globalization;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Shell.Home;

internal readonly struct WidgetPick
{
    public readonly IHomeWidget Widget;
    public readonly WidgetSize Size;

    public WidgetPick(IHomeWidget widget, WidgetSize size)
    {
        Widget = widget;
        Size = size;
    }
}

internal sealed class WidgetGalleryApp
{
    public WidgetGalleryApp(IPhoneApp app)
    {
        App = app;
    }

    public IPhoneApp App { get; }
    public List<IHomeWidget> Widgets { get; } = new();
    public string CountText { get; set; } = string.Empty;
}

internal sealed class WidgetGalleryIndex
{
    public const int FeaturedCapacity = 5;
    public const int StackCapacity = 3;

    private static readonly string[] FeaturedPriority =
    {
        "skywatcher.forecast",
        "music.nowplaying",
        "calendar.upcoming",
        "photos.shuffle",
        "character.rings",
        "clock.faces",
        "timers.resets",
        "coin.balance",
    };

    private static readonly Comparison<WidgetGalleryApp> AppOrder = CompareApps;
    private static readonly Comparison<Candidate> CandidateOrder = CompareCandidates;

    private readonly WidgetRegistry registry;
    private readonly Func<string, bool> isInstalled;
    private readonly Dictionary<string, WidgetGalleryApp> appsById = new(StringComparer.Ordinal);
    private readonly List<WidgetGalleryApp> known = new();
    private readonly List<WidgetGalleryApp> available = new();
    private readonly List<WidgetGalleryApp> visible = new();
    private readonly List<WidgetPick> featured = new(FeaturedCapacity);
    private readonly List<IHomeWidget> smallStack = new(StackCapacity);
    private readonly List<IHomeWidget> mediumStack = new(StackCapacity);
    private readonly List<Candidate> candidates = new();
    private string query = string.Empty;
    private LanguageInfo? language;
    private int signature;
    private bool built;
    private bool stackMatches = true;

    public WidgetGalleryIndex(WidgetRegistry registry, Func<string, bool> isInstalled)
    {
        this.registry = registry;
        this.isInstalled = isInstalled;
    }

    public IReadOnlyList<WidgetGalleryApp> Apps => visible;
    public IReadOnlyList<WidgetPick> Featured => featured;
    public bool Searching => !query.AsSpan().Trim().IsEmpty;
    public bool ShowsSmartStack => stackMatches && (smallStack.Count > 0 || mediumStack.Count > 0);
    public bool IsEmpty => visible.Count == 0 && !ShowsSmartStack;

    public IReadOnlyList<IHomeWidget> Suggestions(WidgetSize size) =>
        size == WidgetSize.Small ? smallStack : mediumStack;

    public WidgetGalleryApp? Find(string appId) =>
        appsById.TryGetValue(appId, out var entry) && entry.Widgets.Count > 0 ? entry : null;

    public bool Refresh(string text, bool force)
    {
        var currentSignature = Signature();
        var currentLanguage = Loc.Current;
        var sourceChanged = force || !built || currentSignature != signature ||
                            !ReferenceEquals(currentLanguage, language);
        var queryChanged = !string.Equals(text, query, StringComparison.Ordinal);
        if (!sourceChanged && !queryChanged)
        {
            return false;
        }

        query = text;
        signature = currentSignature;
        language = currentLanguage;
        built = true;
        if (sourceChanged)
        {
            RebuildAvailable();
        }

        ApplyQuery();
        return true;
    }

    private bool IsOffered(IHomeWidget widget) => registry.IsAvailable(widget) && isInstalled(widget.AppId);

    private int Signature()
    {
        var all = registry.All;
        var hash = all.Count;
        for (var widgetIndex = 0; widgetIndex < all.Count; widgetIndex++)
        {
            hash = unchecked(hash * 31 + (IsOffered(all[widgetIndex]) ? widgetIndex + 1 : 0));
        }

        return hash;
    }

    private void RebuildAvailable()
    {
        for (var entryIndex = 0; entryIndex < known.Count; entryIndex++)
        {
            known[entryIndex].Widgets.Clear();
        }

        available.Clear();
        candidates.Clear();
        var all = registry.All;
        for (var widgetIndex = 0; widgetIndex < all.Count; widgetIndex++)
        {
            var widget = all[widgetIndex];
            if (!IsOffered(widget) || registry.AppFor(widget) is not { } app)
            {
                continue;
            }

            var entry = EntryFor(app);
            if (entry.Widgets.Count == 0)
            {
                available.Add(entry);
            }

            entry.Widgets.Add(widget);
            candidates.Add(new Candidate(widget, widget.Relevance(string.Empty), Priority(widget.Id), widgetIndex));
        }

        for (var entryIndex = 0; entryIndex < available.Count; entryIndex++)
        {
            var entry = available[entryIndex];
            entry.CountText = Loc.Plural(L.WidgetGallery.WidgetCount, entry.Widgets.Count);
        }

        available.Sort(AppOrder);
        candidates.Sort(CandidateOrder);
        FillFeatured();
        FillStack(smallStack, WidgetSize.Small);
        FillStack(mediumStack, WidgetSize.Medium);
    }

    private WidgetGalleryApp EntryFor(IPhoneApp app)
    {
        if (appsById.TryGetValue(app.Id, out var entry))
        {
            return entry;
        }

        entry = new WidgetGalleryApp(app);
        appsById[app.Id] = entry;
        known.Add(entry);
        return entry;
    }

    private void FillFeatured()
    {
        featured.Clear();
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (featured.Count >= FeaturedCapacity)
            {
                return;
            }

            var widget = candidates[candidateIndex].Widget;
            if (FeaturesApp(widget.AppId))
            {
                continue;
            }

            if (WidgetSizes.Contains(widget.Sizes, WidgetSize.Medium))
            {
                featured.Add(new WidgetPick(widget, WidgetSize.Medium));
                continue;
            }

            if (WidgetSizes.Contains(widget.Sizes, WidgetSize.Small))
            {
                featured.Add(new WidgetPick(widget, WidgetSize.Small));
            }
        }
    }

    private bool FeaturesApp(string appId)
    {
        for (var pickIndex = 0; pickIndex < featured.Count; pickIndex++)
        {
            if (string.Equals(featured[pickIndex].Widget.AppId, appId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void FillStack(List<IHomeWidget> target, WidgetSize size)
    {
        target.Clear();
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (target.Count >= StackCapacity)
            {
                return;
            }

            var widget = candidates[candidateIndex].Widget;
            if (!WidgetSizes.Contains(widget.Sizes, size) || StackHasApp(target, widget.AppId))
            {
                continue;
            }

            target.Add(widget);
        }
    }

    private static bool StackHasApp(List<IHomeWidget> stack, string appId)
    {
        for (var memberIndex = 0; memberIndex < stack.Count; memberIndex++)
        {
            if (string.Equals(stack[memberIndex].AppId, appId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyQuery()
    {
        visible.Clear();
        var trimmed = query.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            stackMatches = true;
            for (var entryIndex = 0; entryIndex < available.Count; entryIndex++)
            {
                visible.Add(available[entryIndex]);
            }

            return;
        }

        stackMatches = Matches(Loc.T(L.WidgetGallery.SmartStack), trimmed);
        for (var entryIndex = 0; entryIndex < available.Count; entryIndex++)
        {
            var entry = available[entryIndex];
            if (Matches(entry.App.DisplayName, trimmed) || AnyWidgetMatches(entry, trimmed))
            {
                visible.Add(entry);
            }
        }
    }

    private static bool AnyWidgetMatches(WidgetGalleryApp entry, ReadOnlySpan<char> trimmed)
    {
        var widgets = entry.Widgets;
        for (var widgetIndex = 0; widgetIndex < widgets.Count; widgetIndex++)
        {
            var widget = widgets[widgetIndex];
            if (Matches(widget.DisplayName, trimmed) || Matches(widget.Description, trimmed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(string source, ReadOnlySpan<char> value) =>
        Loc.Culture.CompareInfo.IndexOf(source.AsSpan(), value,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private static int Priority(string widgetId)
    {
        for (var priorityIndex = 0; priorityIndex < FeaturedPriority.Length; priorityIndex++)
        {
            if (string.Equals(FeaturedPriority[priorityIndex], widgetId, StringComparison.Ordinal))
            {
                return priorityIndex;
            }
        }

        return FeaturedPriority.Length;
    }

    private static int CompareApps(WidgetGalleryApp first, WidgetGalleryApp second)
    {
        var byName = Loc.Culture.CompareInfo.Compare(first.App.DisplayName, second.App.DisplayName,
            CompareOptions.IgnoreCase);
        return byName != 0 ? byName : string.CompareOrdinal(first.App.Id, second.App.Id);
    }

    private static int CompareCandidates(Candidate first, Candidate second)
    {
        var byRelevance = second.Relevance.CompareTo(first.Relevance);
        if (byRelevance != 0)
        {
            return byRelevance;
        }

        var byPriority = first.Priority.CompareTo(second.Priority);
        return byPriority != 0 ? byPriority : first.Order.CompareTo(second.Order);
    }

    private readonly struct Candidate
    {
        public readonly IHomeWidget Widget;
        public readonly float Relevance;
        public readonly int Priority;
        public readonly int Order;

        public Candidate(IHomeWidget widget, float relevance, int priority, int order)
        {
            Widget = widget;
            Relevance = Math.Clamp(relevance, 0f, 1f);
            Priority = priority;
            Order = order;
        }
    }
}
