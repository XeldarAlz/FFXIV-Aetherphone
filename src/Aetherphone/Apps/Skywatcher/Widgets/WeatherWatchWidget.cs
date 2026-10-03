using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Skywatcher.Widgets;

internal sealed class WeatherWatchWidget : IHomeWidget
{
    private const int ForecastWindows = 6;
    private const int SlotCount = 3;
    private const uint CurrentLocation = 0;
    private const uint SampleHere = 130;
    private const float CardPaddingUnits = 9f;
    private const float GlyphUnits = 13f;
    private const float ArrowUnits = 8f;
    private const string FirstKey = "zone1";
    private const string SecondKey = "zone2";
    private const string ThirdKey = "zone3";
    private const string FirstDefault = "0";
    private const string SecondDefault = "132";
    private const string ThirdDefault = "129";

    private sealed class Slot
    {
        public readonly List<WeatherWindow> Forecast = new(ForecastWindows);
        public uint Territory = uint.MaxValue;
        public bool Here;
        public string Name = string.Empty;
        public CachedText Change;
    }

    private sealed class Watch
    {
        public readonly Slot[] Slots = { new(), new(), new() };
        public WeatherPulse Pulse;
        public string Config = string.Empty;
        public bool Sample;
    }

    private readonly WeatherService weather;
    private readonly WidgetStates<Watch> watches = new();
    private readonly List<WeatherWindow> scratch = new(ForecastWindows);
    private readonly WidgetOption[] options;

    public WeatherWatchWidget(WeatherService weather)
    {
        this.weather = weather;
        options = new[]
        {
            new WidgetOption(FirstKey, L.WidgetsLife.ZoneFirst, Choices, FirstDefault),
            new WidgetOption(SecondKey, L.WidgetsLife.ZoneSecond, Choices, SecondDefault),
            new WidgetOption(ThirdKey, L.WidgetsLife.ZoneThird, Choices, ThirdDefault),
        };
    }

    public string Id => "skywatcher.zones";
    public string DisplayName => Loc.T(L.WidgetsLife.ZonesName);
    public string Description => Loc.T(L.WidgetsLife.ZonesDescription);
    public string AppId => "skywatcher";
    public WidgetSizeSet Sizes => WidgetSizeSet.Medium;
    public IReadOnlyList<WidgetOption> Options => options;

    public float Relevance(string config)
    {
        var best = 0f;
        for (var slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            var territory = Resolve(TerritoryFor(config, slotIndex), false);
            if (territory == CurrentLocation)
            {
                continue;
            }

            weather.Forecast(territory, scratch, ForecastWindows);
            best = MathF.Max(best, WeatherWidgetPaint.Relevance(scratch) * 0.8f);
        }

        return best;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var watch = watches.For(context.InstanceKey);
        Refresh(watch, context.Config, context.Preview);
        var now = EorzeaTime.Now();
        var content = WidgetMetrics.Content(context);
        var gutter = WidgetMetrics.Gutter * context.Scale;
        var cardWidth = (content.Width - gutter * (SlotCount - 1)) / SlotCount;
        for (var slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            var left = content.Min.X + (cardWidth + gutter) * slotIndex;
            var card = new Rect(new Vector2(left, content.Min.Y), new Vector2(left + cardWidth, content.Max.Y));
            DrawCard(context, ink, watch.Slots[slotIndex], card, now);
        }
    }

    private void Refresh(Watch watch, string config, bool preview)
    {
        var sample = preview && weather.CurrentTerritory == 0;
        var due = watch.Pulse.Due(weather.CurrentTerritory, weather.LiveWeatherId());
        if (!due && string.Equals(config, watch.Config, StringComparison.Ordinal) && sample == watch.Sample)
        {
            return;
        }

        watch.Config = config;
        watch.Sample = sample;
        for (var slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            var slot = watch.Slots[slotIndex];
            var chosen = TerritoryFor(config, slotIndex);
            slot.Here = chosen == CurrentLocation;
            var territory = Resolve(chosen, sample);
            if (territory != slot.Territory)
            {
                slot.Territory = territory;
                slot.Change.Reset();
            }

            slot.Name = territory == CurrentLocation ? string.Empty : weather.ZoneName(territory);
            if (slot.Here && !sample)
            {
                weather.Forecast(slot.Forecast, ForecastWindows);
                continue;
            }

            weather.Forecast(territory, slot.Forecast, ForecastWindows);
        }
    }

    private uint Resolve(uint chosen, bool sample)
    {
        if (chosen != CurrentLocation)
        {
            return chosen;
        }

        return sample ? SampleHere : weather.CurrentTerritory;
    }

    private static uint TerritoryFor(string config, int slotIndex)
    {
        var value = slotIndex switch
        {
            0 => WidgetConfig.Get(config, FirstKey, FirstDefault),
            1 => WidgetConfig.Get(config, SecondKey, SecondDefault),
            _ => WidgetConfig.Get(config, ThirdKey, ThirdDefault),
        };
        return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : CurrentLocation;
    }

    private static void DrawCard(in WidgetContext context, in WidgetInk ink, Slot slot, Rect card, EorzeaTime now)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var radius = WidgetMetrics.InnerRadius(context);
        var hasWeather = slot.Forecast.Count > 0;
        var kind = hasWeather ? WeatherSky.Classify(slot.Forecast[0].Weather.EnglishKey) : WeatherKind.Clouds;
        var isDay = hasWeather
            ? WeatherWidgetPaint.IsDay(slot.Forecast[0], now)
            : WeatherSky.Daylight(now.Hour + now.Minute / 60f) >= 0.5f;
        var sky = WeatherWidgetPaint.Sky(kind, now);
        PaintCard(drawList, ink, card, radius, sky);
        var cardInk = new WeatherInk(ink, sky);
        var inner = new Rect(card.Min + new Vector2(CardPaddingUnits * scale),
            card.Max - new Vector2(CardPaddingUnits * scale));
        var nameLeft = inner.Min.X;
        var headlineHeight = WidgetText.LineHeight(WidgetType.Headline);
        if (slot.Here)
        {
            var arrow = ArrowUnits * scale;
            ProgressRing.CenterIcon(drawList, new Vector2(nameLeft + arrow * 0.5f, inner.Min.Y + headlineHeight * 0.5f),
                FontAwesomeIcon.LocationArrow, cardInk.Primary, arrow);
            nameLeft += arrow + WidgetMetrics.RowGap * scale;
        }

        var name = slot.Name.Length > 0 ? slot.Name : Loc.T(L.WidgetsLife.CurrentLocation);
        WidgetText.Draw(context.DrawList, new Vector2(nameLeft, inner.Min.Y), name, cardInk.Primary,
            WidgetType.Headline, MathF.Max(1f, inner.Max.X - nameLeft));
        var glyphRadius = GlyphUnits * scale;
        var glyphCenter = new Vector2(inner.Min.X + glyphRadius,
            inner.Min.Y + headlineHeight + WidgetMetrics.Gutter * scale + glyphRadius);
        WeatherGlyph.Draw(drawList, kind, glyphCenter, glyphRadius,
            hasWeather ? cardInk.Glyph(kind, isDay) : Faded(cardInk.Glyph(kind, isDay)), isDay);
        var captionHeight = WidgetText.LineHeight(WidgetType.Caption);
        var changeTop = inner.Max.Y - captionHeight;
        if (!hasWeather)
        {
            WidgetText.Draw(context.DrawList, new Vector2(inner.Min.X, changeTop),
                Loc.T(slot.Here ? L.WidgetsLife.NotInWorld : L.Skywatcher.NoData), cardInk.Secondary,
                WidgetType.Caption, inner.Width);
            return;
        }

        var change = WeatherWidgetPaint.ChangeLine(ref slot.Change, slot.Forecast,
            WeatherWidgetPaint.NextChange(slot.Forecast));
        WidgetText.Draw(context.DrawList, new Vector2(inner.Min.X, changeTop), change, cardInk.Secondary,
            WidgetType.Caption, inner.Width);
        WidgetText.Draw(context.DrawList,
            new Vector2(inner.Min.X, changeTop - WidgetMetrics.RowGap * scale - headlineHeight),
            slot.Forecast[0].Weather.Name, cardInk.Primary, WidgetType.Headline, inner.Width);
    }

    private static void PaintCard(ImDrawListPtr drawList, in WidgetInk ink, Rect card, float radius,
        in SkyPalette sky)
    {
        switch (ink.Mode)
        {
            case WidgetMode.FullColor:
                Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ink.Fade(sky.Top)),
                    ImGui.GetColorU32(ink.Fade(sky.Bottom)));
                return;
            case WidgetMode.Dark:
                Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
                    ImGui.GetColorU32(ink.Fade(Dimmed(sky.Top))), ImGui.GetColorU32(ink.Fade(Dimmed(sky.Bottom))));
                return;
            default:
                Squircle.Fill(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ink.Fill));
                return;
        }
    }

    private static Vector4 Dimmed(Vector4 color) => new(color.X * 0.45f, color.Y * 0.45f, color.Z * 0.45f, color.W);

    private static GlyphInk Faded(in GlyphInk glyph) =>
        new(glyph.Glow with { W = glyph.Glow.W * 0.5f }, glyph.Cloud with { W = glyph.Cloud.W * 0.5f }, glyph.Drop,
            glyph.Flake, default);

    private void Choices(List<WidgetChoice> target)
    {
        target.Add(new WidgetChoice(FirstDefault, L.WidgetsLife.CurrentLocation));
        var zones = new List<WeatherZone>();
        weather.WeatherZones(zones);
        for (var index = 0; index < zones.Count; index++)
        {
            target.Add(new WidgetChoice(zones[index].TerritoryId.ToString(CultureInfo.InvariantCulture),
                zones[index].Name));
        }
    }

    public void Dispose()
    {
    }
}
