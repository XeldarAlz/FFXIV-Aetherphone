using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts.Widgets;

internal sealed class HuntsLiveWidget : IHomeWidget
{
    private const int RefreshMilliseconds = 2000;
    private const int ActivateMilliseconds = 10000;
    private const int Capacity = 8;
    private const int MediumRows = 3;
    private const int ControlBase = 200;
    private const float RowMaximumUnits = 40f;
    private const float RowMinimumUnits = 30f;
    private const float BadgeHeightUnits = 18f;
    private const float BadgePaddingUnits = 6f;
    private const float LiveRelevance = 0.9f;
    private const string SettingsIntent = "hunts.tab.settings";
    private const string RankS = "S";
    private const string RankSS = "SS";
    private const string FallbackLanguage = "en";

    private static readonly Vector4 HuntsAccent = AppAccents.For("hunts");

    private readonly HuntsService hunts;
    private readonly HuntMobCatalog catalog;
    private readonly Configuration configuration;
    private readonly string[] mobIds = new string[Capacity];
    private readonly string[] worldIds = new string[Capacity];
    private readonly int[] instances = new int[Capacity];
    private readonly string[] ranks = new string[Capacity];
    private readonly DateTimeOffset[] spawnedAt = new DateTimeOffset[Capacity];
    private readonly CachedText[] agoTexts = new CachedText[Capacity];
    private readonly CachedText[] placeTexts = new CachedText[Capacity];
    private readonly Dictionary<string, string> mobNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> worldNames = new(StringComparer.Ordinal);
    private int liveCount;
    private int openCount;
    private string namesLanguage = string.Empty;
    private WidgetRefresh refresh;
    private WidgetRefresh activate;
    private CachedText eyebrowText;
    private string eyebrowSource = string.Empty;
    private CachedText openText;

    public HuntsLiveWidget(HuntsService hunts, HuntMobCatalog catalog, Configuration configuration)
    {
        this.hunts = hunts;
        this.catalog = catalog;
        this.configuration = configuration;
    }

    public string Id => "hunts.live";
    public string DisplayName => Loc.T(L.Apps.Hunts);
    public string Description => Loc.T(L.WidgetsAdventure.HuntsDescription);
    public string AppId => "hunts";
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public WidgetRoute Target(in WidgetContext context) =>
        IsSetUp ? WidgetRoute.App(AppId) : WidgetRoute.Tab(AppId, SettingsIntent);

    public float Relevance(string config)
    {
        Sync();
        return IsSetUp && liveCount > 0 ? LiveRelevance : 0f;
    }

    private bool IsSetUp => configuration.HuntsAppOpened && hunts.CurrentDataCenter is { Length: > 0 };

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        Sync();
        var setUp = IsSetUp;
        var headerBottom = WidgetChrome.Header(context, ink, AppId, Eyebrow(setUp), HuntsAccent);
        var content = WidgetMetrics.Content(context);
        var body = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * context.Scale * 0.5f),
            content.Max);
        if (context.Preview && (!setUp || liveCount == 0))
        {
            DrawSampleRows(context, ink, body);
            return;
        }

        if (!setUp)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.Crosshairs, default,
                Loc.T(L.WidgetsAdventure.HuntsSetUp), string.Empty);
            return;
        }

        if (hunts.Failed && !hunts.Loaded)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.CloudDownloadAlt, default,
                Loc.T(L.WidgetsAdventure.HuntsFailed), string.Empty);
            return;
        }

        if (!hunts.Loaded)
        {
            WidgetChrome.RedactedRows(context, ink, body, RowsFor(context, body), WidgetRowLead.None);
            return;
        }

        if (liveCount == 0)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.Crosshairs, default,
                Loc.T(L.WidgetsAdventure.NoneLive), OpenWindowsText());
            return;
        }

        DrawLiveRows(context, ink, body);
    }

    private void DrawLiveRows(in WidgetContext context, in WidgetInk ink, Rect body)
    {
        var rows = Math.Min(liveCount, RowsFor(context, body));
        var rowHeight = MathF.Min(RowMaximumUnits * context.Scale, body.Height / Math.Max(1, rows));
        var utcNow = DateTimeOffset.UtcNow;
        for (var index = 0; index < rows; index++)
        {
            var top = body.Min.Y + index * rowHeight;
            var row = new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + rowHeight));
            WidgetControls.Link(context, ink, ControlBase + index, row,
                WidgetRoute.Hunt(mobIds[index], worldIds[index], instances[index]));
            var minutes = spawnedAt[index] == default ? -1 : (long)(utcNow - spawnedAt[index]).TotalMinutes;
            DrawRow(context, ink, row, ranks[index], MobName(mobIds[index]), Place(index), Ago(index, minutes));
        }
    }

    private void DrawSampleRows(in WidgetContext context, in WidgetInk ink, Rect body)
    {
        var rows = Math.Min(WidgetSamples.HuntMarks.Length, RowsFor(context, body));
        var rowHeight = MathF.Min(RowMaximumUnits * context.Scale, body.Height / Math.Max(1, rows));
        for (var index = 0; index < rows; index++)
        {
            var top = body.Min.Y + index * rowHeight;
            var row = new Rect(new Vector2(body.Min.X, top), new Vector2(body.Max.X, top + rowHeight));
            DrawRow(context, ink, row, RankS, WidgetSamples.HuntMarks[index], WidgetSamples.Worlds[index],
                Ago(index, WidgetSamples.HuntMinutesAgo[index]));
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, string rank, string name,
        string place, string ago)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var gap = WidgetMetrics.RowGap * scale;
        var blockTop = row.Center.Y - (headlineHeight + gap + captionHeight) * 0.5f;
        var badgeWidth = DrawRank(drawList, ink, rank, new Vector2(row.Min.X, row.Center.Y), scale);
        var textLeft = row.Min.X + badgeWidth + gutter;
        var agoWidth = ago.Length > 0
            ? WidgetText.TabularRight(drawList, row.Max.X, row.Center.Y - captionHeight * 0.5f, ago,
                ink.Secondary, WidgetType.Caption)
            : 0f;
        var textWidth = MathF.Max(1f, row.Max.X - agoWidth - gutter - textLeft);
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop), name, ink.Primary, WidgetType.Headline, textWidth);
        WidgetText.Draw(drawList, new Vector2(textLeft, blockTop + headlineHeight + gap), place, ink.Secondary,
            WidgetType.Caption, textWidth);
    }

    private static float DrawRank(ImDrawListPtr drawList, in WidgetInk ink, string rank, Vector2 leftCenter,
        float scale)
    {
        var height = BadgeHeightUnits * scale;
        var style = WidgetType.Caption;
        var textSize = Typography.Measure(rank, style);
        var width = MathF.Max(height, textSize.X + BadgePaddingUnits * 2f * scale);
        var min = new Vector2(leftCenter.X, leftCenter.Y - height * 0.5f);
        var max = new Vector2(leftCenter.X + width, leftCenter.Y + height * 0.5f);
        var color = ink.Accent(string.Equals(rank, RankSS, StringComparison.Ordinal)
            ? HuntsApp.RankSSColor
            : HuntsApp.RankSColor);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(color));
        Typography.Draw(drawList, new Vector2(min.X + (width - textSize.X) * 0.5f, leftCenter.Y - textSize.Y * 0.5f),
            rank, ink.OnAccent, style);
        return width;
    }

    private static int RowsFor(in WidgetContext context, Rect body)
    {
        var fit = Math.Max(1, (int)(body.Height / (RowMinimumUnits * context.Scale)));
        return context.Size == WidgetSize.Large ? Math.Min(fit, Capacity) : Math.Min(fit, MediumRows);
    }

    private void Sync()
    {
        if (configuration.HuntsAppOpened && activate.Due(ActivateMilliseconds))
        {
            hunts.EnsureActive();
        }

        if (IsSetUp && refresh.Due(RefreshMilliseconds))
        {
            Snapshot();
        }
    }

    private void Snapshot()
    {
        var windows = hunts.Windows;
        var now = DateTimeOffset.UtcNow;
        liveCount = 0;
        openCount = 0;
        for (var index = 0; index < windows.Length; index++)
        {
            var window = windows[index];
            var mob = catalog.Find(window.MobId);
            if (mob is null || !IsSRank(mob.Rank))
            {
                continue;
            }

            if (!hunts.IsSpawned(window.MobId, window.WorldId, window.ZoneInstance))
            {
                if (HuntWindowMath.Status(window, mob, now) is HuntWindowStatus.Open or HuntWindowStatus.Capped)
                {
                    openCount++;
                }

                continue;
            }

            Insert(window, mob.Rank,
                hunts.SpawnedSince(window.MobId, window.WorldId, window.ZoneInstance) ?? default);
        }
    }

    private void Insert(HuntWindowDto window, string rank, DateTimeOffset since)
    {
        var position = liveCount;
        while (position > 0 && spawnedAt[position - 1] < since)
        {
            position--;
        }

        if (position >= Capacity)
        {
            return;
        }

        var last = Math.Min(liveCount, Capacity - 1);
        for (var index = last; index > position; index--)
        {
            mobIds[index] = mobIds[index - 1];
            worldIds[index] = worldIds[index - 1];
            instances[index] = instances[index - 1];
            ranks[index] = ranks[index - 1];
            spawnedAt[index] = spawnedAt[index - 1];
            placeTexts[index] = placeTexts[index - 1];
        }

        mobIds[position] = window.MobId;
        worldIds[position] = window.WorldId;
        instances[position] = window.ZoneInstance;
        ranks[position] = rank;
        spawnedAt[position] = since;
        placeTexts[position].Reset();
        liveCount = Math.Min(liveCount + 1, Capacity);
    }

    private static bool IsSRank(string rank) =>
        string.Equals(rank, RankS, StringComparison.Ordinal) || string.Equals(rank, RankSS, StringComparison.Ordinal);

    private string Eyebrow(bool setUp)
    {
        var dataCenter = setUp ? hunts.CurrentDataCenter ?? string.Empty : string.Empty;
        if (dataCenter.Length == 0)
        {
            return Loc.T(L.Apps.Hunts);
        }

        if (eyebrowText.IsCurrent(0) && ReferenceEquals(eyebrowSource, dataCenter))
        {
            return eyebrowText.Value;
        }

        eyebrowSource = dataCenter;
        return eyebrowText.Store(0, Loc.T(L.WidgetsAdventure.LiveOn, dataCenter));
    }

    private string OpenWindowsText() =>
        openCount <= 0
            ? string.Empty
            : openText.IsCurrent(openCount)
                ? openText.Value
                : openText.Store(openCount, Loc.T(L.WidgetsAdventure.WindowsOpen, openCount));

    private string MobName(string mobId)
    {
        var language = configuration.Language;
        if (!string.Equals(language, namesLanguage, StringComparison.Ordinal))
        {
            namesLanguage = language;
            mobNames.Clear();
        }

        if (mobNames.TryGetValue(mobId, out var cached))
        {
            return cached;
        }

        var mob = catalog.Find(mobId);
        var name = mob?.Name.GetValueOrDefault(language) ?? mob?.Name.GetValueOrDefault(FallbackLanguage) ??
            HuntsApp.Prettify(mobId);
        mobNames[mobId] = name;
        return name;
    }

    private string Place(int index)
    {
        var instance = instances[index];
        if (placeTexts[index].IsCurrent(instance))
        {
            return placeTexts[index].Value;
        }

        var world = WorldName(worldIds[index]);
        return placeTexts[index].Store(instance,
            instance > 0 ? string.Concat(world, " ", instance.ToString(CultureInfo.InvariantCulture)) : world);
    }

    private string WorldName(string worldId)
    {
        if (worldNames.TryGetValue(worldId, out var cached))
        {
            return cached;
        }

        var name = HuntsApp.Prettify(worldId);
        worldNames[worldId] = name;
        return name;
    }

    private string Ago(int index, long minutes)
    {
        if (minutes < 0)
        {
            return string.Empty;
        }

        return agoTexts[index].IsCurrent(minutes)
            ? agoTexts[index].Value
            : agoTexts[index].Store(minutes, TimeText.Ago(DateTime.UtcNow.AddMinutes(-minutes)));
    }

    public void Dispose()
    {
    }
}
