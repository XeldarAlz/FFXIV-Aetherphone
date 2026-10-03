using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class DisplayPage : ISettingsPage
{
    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.PhoneSize),
        new(L.Settings.TextSize),
        new(L.Settings.LiveGlass),
        new(L.ControlCenter.LockPosition),
        new(L.Minimized.Title),
    };

    public string Title => Loc.T(L.Settings.Display);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Sun;
    public Vector4 Tint => new(0.13f, 0.56f, 0.96f, 1f);
    public string? GuideAnchor => "settings.row.display";
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;
    private const float CardGap = Metrics.Space.Xl;
    private const float PercentScale = 100f;
#if DEBUG
    private const int LiveGlassDetailRows = 2;
#else
    private const int LiveGlassDetailRows = 1;
#endif
    private static readonly LiveGlassSource[] SourceOrder = { LiveGlassSource.World, LiveGlassSource.Composite };
    private static readonly string[] WidthLabels = BuildWidthLabels();
    private static readonly string[] ZoomLabels = BuildZoomLabels();
    private readonly string[] sourceLabels = new string[SourceOrder.Length];
    private LanguageInfo? sourceLabelsLanguage;
    private string widthReadout = string.Empty;
    private int widthReadoutValue = -1;
    private LanguageInfo? widthReadoutLanguage;
    private string zoomReadout = string.Empty;
    private int zoomReadoutPercent = -1;
#if DEBUG
    private string liveGlassReadout = string.Empty;
    private int liveGlassReadoutTenths = -1;
    private Vector2 liveGlassReadoutSize;
#endif
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly MinimizedLayoutService minimizedLayout;

    public DisplayPage(Configuration configuration, ISettingsNavigator navigator,
        MinimizedLayoutService minimizedLayout)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        this.minimizedLayout = minimizedLayout;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawPhoneSizeCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawTextSizeCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLiveGlassCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawLockPositionCard(theme);
            ImGui.Dummy(new Vector2(0f, CardGap * scale));
            DrawMinimizedCard(theme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }
    }

    private void DrawPhoneSizeCard(PhoneTheme theme)
    {
        var effective = PhoneBounds.ClampWidth(configuration.PhoneWidth);
        var card = GroupCard.Begin(theme, 2);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.PhoneSize), WidthReadout(effective), theme);
        var selected = NearestIndex(PhoneSizeCatalog.PresetWidths, effective);
        var picked = SegmentStrip.Draw("settings.phoneSizePreset", card.NextRow(), WidthLabels, selected, theme,
            out var pressed);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.PhoneSizeHint), theme);
        if (!pressed)
        {
            return;
        }

        var width = PhoneBounds.ClampWidth(PhoneSizeCatalog.PresetWidths[picked]);
        if (MathF.Abs(width - configuration.PhoneWidth) <= 0.01f)
        {
            return;
        }

        configuration.PhoneWidth = width;
        configuration.Save();
    }

    private void DrawTextSizeCard(PhoneTheme theme)
    {
        var zoom = TextZoomCatalog.Clamp(configuration.TextZoom);
        var card = GroupCard.Begin(theme, 2);
        SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.TextSize), ZoomReadout(zoom), theme);
        var selected = NearestIndex(TextZoomCatalog.PresetZooms, zoom);
        var picked = SegmentStrip.Draw("settings.textZoomPreset", card.NextRow(), ZoomLabels, selected, theme,
            out var pressed);
        card.End();
        if (!pressed)
        {
            return;
        }

        var next = TextZoomCatalog.PresetZooms[picked];
        if (MathF.Abs(next - configuration.TextZoom) <= 0.001f)
        {
            return;
        }

        configuration.TextZoom = next;
        Plugin.Fonts.SetZoom(next);
        configuration.Save();
    }

    private void DrawLiveGlassCard(PhoneTheme theme)
    {
        var liveGlassOn = configuration.LiveGlass;
        var card = GroupCard.Begin(theme, liveGlassOn ? 1 + LiveGlassDetailRows : 1);
        var liveGlass = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.LiveGlass), configuration.LiveGlass, theme);
        if (liveGlass != configuration.LiveGlass)
        {
            configuration.LiveGlass = liveGlass;
            configuration.Save();
        }

        if (liveGlassOn)
        {
            var sourceIndex = SegmentStrip.Draw("settings.liveGlassSource", card.NextRow(), SourceLabels(),
                SourceIndex(configuration.LiveGlassSource), theme);
            var source = SourceOrder[sourceIndex];
            if (source != configuration.LiveGlassSource)
            {
                configuration.LiveGlassSource = source;
                configuration.Save();
            }

#if DEBUG
            SettingsRow.Info(card.NextRow(), Loc.T(L.Settings.LiveGlassReadout), LiveGlassReadout(), theme);
#endif
        }

        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.LiveGlassHint), theme);
    }

    private void DrawLockPositionCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var lockPosition = SettingsRow.Bool(card.NextRow(), Loc.T(L.ControlCenter.LockPosition),
            configuration.LockPosition, theme);
        card.End();
        SettingsSection.Hint(Loc.T(L.Settings.LockPositionHint), theme);
        if (lockPosition == configuration.LockPosition)
        {
            return;
        }

        configuration.LockPosition = lockPosition;
        configuration.Save();
    }

    private void DrawMinimizedCard(PhoneTheme theme)
    {
        var card = GroupCard.Begin(theme, 1);
        var opened = SettingsRow.Disclosure(card.NextRow(), Loc.T(L.Minimized.Title), string.Empty, theme);
        card.End();
        if (opened)
        {
            navigator.Open(new MinimizedPhonePage(minimizedLayout, configuration));
        }
    }

    private string WidthReadout(float width)
    {
        var rounded = (int)MathF.Round(width);
        if (rounded == widthReadoutValue && ReferenceEquals(widthReadoutLanguage, Loc.Current))
        {
            return widthReadout;
        }

        widthReadoutValue = rounded;
        widthReadoutLanguage = Loc.Current;
        widthReadout = Loc.T(L.Settings.PhoneWidthReadout, rounded);
        return widthReadout;
    }

    private string ZoomReadout(float zoom)
    {
        var percent = (int)MathF.Round(zoom * PercentScale);
        if (percent == zoomReadoutPercent)
        {
            return zoomReadout;
        }

        zoomReadoutPercent = percent;
        zoomReadout = PercentLabel(percent);
        return zoomReadout;
    }

    private string[] SourceLabels()
    {
        if (ReferenceEquals(sourceLabelsLanguage, Loc.Current))
        {
            return sourceLabels;
        }

        sourceLabelsLanguage = Loc.Current;
        sourceLabels[0] = Loc.T(L.Settings.LiveGlassSourceWorld);
        sourceLabels[1] = Loc.T(L.Settings.LiveGlassSourceComposite);
        return sourceLabels;
    }

    private static int SourceIndex(LiveGlassSource source)
    {
        for (var index = 0; index < SourceOrder.Length; index++)
        {
            if (SourceOrder[index] == source)
            {
                return index;
            }
        }

        return 0;
    }

#if DEBUG
    private string LiveGlassReadout()
    {
        var tenths = (int)MathF.Round((float)LiveBackdrop.LastPassMilliseconds * 10f);
        var size = LiveBackdrop.LastCaptureSize;
        if (tenths == liveGlassReadoutTenths && size == liveGlassReadoutSize && liveGlassReadout.Length > 0)
        {
            return liveGlassReadout;
        }

        liveGlassReadoutTenths = tenths;
        liveGlassReadoutSize = size;
        liveGlassReadout = $"{tenths / 10f:0.0} ms, {(int)size.X}x{(int)size.Y}";
        return liveGlassReadout;
    }
#endif

    private static int NearestIndex(IReadOnlyList<float> presets, float value)
    {
        var nearest = 0;
        var nearestDistance = float.MaxValue;
        for (var index = 0; index < presets.Count; index++)
        {
            var distance = MathF.Abs(presets[index] - value);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearest = index;
        }

        return nearest;
    }

    private static string[] BuildWidthLabels()
    {
        var labels = new string[PhoneSizeCatalog.PresetWidths.Count];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = ((int)MathF.Round(PhoneSizeCatalog.PresetWidths[index])).ToString(
                CultureInfo.InvariantCulture);
        }

        return labels;
    }

    private static string[] BuildZoomLabels()
    {
        var labels = new string[TextZoomCatalog.PresetZooms.Count];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = PercentLabel((int)MathF.Round(TextZoomCatalog.PresetZooms[index] * PercentScale));
        }

        return labels;
    }

    private static string PercentLabel(int percent) =>
        string.Concat(percent.ToString(CultureInfo.InvariantCulture), "%");
}
