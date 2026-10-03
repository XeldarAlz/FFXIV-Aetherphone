using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Venues;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Maps.Widgets;

internal sealed class TeleportWidget : IHomeWidget
{
    private const int AvailabilityMilliseconds = 5000;
    private const int LocationMilliseconds = 2000;
    private const int SlotCount = 4;
    private const int SmallSlots = 2;
    private const int ControlBase = 300;
    private const float ButtonUnits = 32f;
    private const float ButtonMaximumUnits = 40f;

    private static readonly Vector4 MapsAccent = AppAccents.For("maps");
    private static readonly string[] SlotKeys = { "favorite1", "favorite2", "favorite3", "favorite4" };

    private readonly MapData maps;
    private readonly Configuration configuration;
    private readonly IReadOnlyList<WidgetOption> options;
    private readonly uint[] chosen = new uint[SlotCount];
    private WidgetRefresh availability;
    private WidgetRefresh location;
    private bool lifestreamAvailable;
    private string zone = string.Empty;

    public TeleportWidget(MapData maps, Configuration configuration)
    {
        this.maps = maps;
        this.configuration = configuration;
        options = new[]
        {
            new WidgetOption(SlotKeys[0], L.WidgetsAdventure.SlotFirst, CollectFavorites),
            new WidgetOption(SlotKeys[1], L.WidgetsAdventure.SlotSecond, CollectFavorites),
            new WidgetOption(SlotKeys[2], L.WidgetsAdventure.SlotThird, CollectFavorites),
            new WidgetOption(SlotKeys[3], L.WidgetsAdventure.SlotFourth, CollectFavorites),
        };
    }

    public string Id => "maps.teleport";
    public string DisplayName => Loc.T(L.WidgetsAdventure.TeleportName);
    public string Description => Loc.T(L.WidgetsAdventure.TeleportDescription);
    public string AppId => "maps";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;
    public IReadOnlyList<WidgetOption> Options => options;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        if (availability.Due(AvailabilityMilliseconds))
        {
            lifestreamAvailable = LifestreamBridge.IsAvailable();
        }

        var loggedIn = AdventureWidgetArt.IsLoggedIn;
        var slots = context.Size == WidgetSize.Small ? SmallSlots : SlotCount;
        var count = ResolveFavorites(context.Config, slots);
        var sample = context.Preview && (!loggedIn || !lifestreamAvailable || count == 0);
        if (sample)
        {
            count = ResolveSamples(slots);
        }

        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsAdventure.TeleportName, MapsAccent);
        if (!sample)
        {
            if (!loggedIn)
            {
                WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.UserCircle, default,
                    Loc.T(L.WidgetsAdventure.LogIn), string.Empty);
                return;
            }

            if (!lifestreamAvailable)
            {
                WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.Plug, default,
                    Loc.T(L.WidgetsAdventure.NeedsLifestream), string.Empty);
                return;
            }

            if (count == 0)
            {
                WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), FontAwesomeIcon.Star, default,
                    Loc.T(L.WidgetsAdventure.NoFavorites), string.Empty);
                return;
            }
        }

        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawStack(context, ink, sample, content, headerBottom, count);
            return;
        }

        DrawZone(context, ink, content, headerBottom, loggedIn && !sample);
        DrawGrid(context, ink, sample, content, headerBottom, count);
    }

    private void DrawStack(in WidgetContext context, in WidgetInk ink, bool sample, Rect content, float headerBottom,
        int count)
    {
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var available = content.Max.Y - headerBottom - gutter;
        var height = MathF.Min(ButtonUnits * scale, (available - gutter * (SmallSlots - 1)) / SmallSlots);
        var top = content.Max.Y - count * height - (count - 1) * gutter;
        for (var index = 0; index < count; index++)
        {
            var y = top + index * (height + gutter);
            DrawButton(context, ink, sample, index,
                new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + height)));
        }
    }

    private void DrawGrid(in WidgetContext context, in WidgetInk ink, bool sample, Rect content, float headerBottom,
        int count)
    {
        var scale = context.Scale;
        var gutter = WidgetMetrics.Gutter * scale;
        var body = new Rect(new Vector2(content.Min.X, headerBottom + gutter), content.Max);
        var height = MathF.Min(ButtonMaximumUnits * scale, (body.Height - gutter) * 0.5f);
        var width = (body.Width - gutter) * 0.5f;
        var rows = (count + 1) / 2;
        var gridTop = body.Max.Y - height * rows - gutter * (rows - 1);
        for (var index = 0; index < count; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var left = body.Min.X + column * (width + gutter);
            var top = gridTop + row * (height + gutter);
            DrawButton(context, ink, sample, index,
                new Rect(new Vector2(left, top), new Vector2(left + width, top + height)));
        }
    }

    private void DrawZone(in WidgetContext context, in WidgetInk ink, Rect content, float headerBottom, bool live)
    {
        if (!live)
        {
            return;
        }

        if (location.Due(LocationMilliseconds))
        {
            var current = maps.CurrentLocation();
            zone = current.IsKnown ? current.Title : string.Empty;
        }

        if (zone.Length == 0)
        {
            return;
        }

        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var top = content.Min.Y + (headerBottom - content.Min.Y - captionHeight) * 0.5f;
        var fitted = Typography.FitText(zone, content.Width * 0.5f, WidgetType.Caption);
        WidgetText.DrawRight(context.DrawList, content.Max.X, top, fitted, ink.Secondary,
            WidgetType.Caption);
    }

    private void DrawButton(in WidgetContext context, in WidgetInk ink, bool sample, int index, Rect rect)
    {
        var rowId = chosen[index];
        var name = maps.TryGetAetheryte(rowId, out var aetheryte) ? aetheryte.Name : string.Empty;
        if (!WidgetControls.Button(context, ink, ControlBase + index, rect, FontAwesomeIcon.LocationArrow, name) ||
            sample)
        {
            return;
        }

        var outcome = LifestreamBridge.TeleportToAetheryte(rowId);
        switch (outcome)
        {
            case LifestreamOutcome.Started:
                return;
            case LifestreamOutcome.NotInstalled:
                lifestreamAvailable = false;
                ShellToast.Show(Loc.T(L.WidgetsAdventure.NeedsLifestream));
                return;
            case LifestreamOutcome.Busy:
                ShellToast.Show(Loc.T(L.Travel.Busy));
                return;
            case LifestreamOutcome.NotAttuned:
                ShellToast.Show(Loc.T(L.Travel.NotAttuned, name));
                return;
            default:
                ShellToast.Show(Loc.T(L.Travel.Blocked));
                return;
        }
    }

    private int ResolveFavorites(string config, int slots)
    {
        var favorites = configuration.MapFavorites;
        var count = 0;
        for (var slot = 0; slot < slots; slot++)
        {
            var configured = WidgetConfig.GetInt(config, SlotKeys[slot], -1);
            if (configured <= 0)
            {
                continue;
            }

            var rowId = (uint)configured;
            if (favorites.Contains(rowId) && !IsChosen(rowId, count) && maps.TryGetAetheryte(rowId, out _))
            {
                chosen[count] = rowId;
                count++;
            }
        }

        for (var index = 0; index < favorites.Count && count < slots; index++)
        {
            var rowId = favorites[index];
            if (IsChosen(rowId, count) || !maps.TryGetAetheryte(rowId, out _))
            {
                continue;
            }

            chosen[count] = rowId;
            count++;
        }

        return count;
    }

    private int ResolveSamples(int slots)
    {
        var count = 0;
        var samples = WidgetSamples.AetheryteIds;
        for (var index = 0; index < samples.Length && count < slots; index++)
        {
            if (!maps.TryGetAetheryte(samples[index], out _))
            {
                continue;
            }

            chosen[count] = samples[index];
            count++;
        }

        return count;
    }

    private bool IsChosen(uint rowId, int count)
    {
        for (var index = 0; index < count; index++)
        {
            if (chosen[index] == rowId)
            {
                return true;
            }
        }

        return false;
    }

    private void CollectFavorites(List<WidgetChoice> target)
    {
        target.Add(new WidgetChoice(string.Empty, L.WidgetsAdventure.Automatic));
        var favorites = configuration.MapFavorites;
        for (var index = 0; index < favorites.Count; index++)
        {
            if (maps.TryGetAetheryte(favorites[index], out var aetheryte))
            {
                target.Add(new WidgetChoice(favorites[index].ToString(CultureInfo.InvariantCulture), aetheryte.Name));
            }
        }
    }

    public void Dispose()
    {
    }
}
