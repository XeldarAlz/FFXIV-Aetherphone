using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Widgets;

internal sealed class QuickTogglesWidget : IHomeWidget
{
    private const int DoNotDisturb = 0;
    private const int Silent = 1;
    private const int Calls = 2;
    private const int LockPosition = 3;
    private const int DarkMode = 4;
    private const int SmallCount = 4;
    private const int MediumCount = 5;
    private const int LabelLines = 2;

    private static readonly FontAwesomeIcon[] Icons =
    {
        FontAwesomeIcon.Moon, FontAwesomeIcon.BellSlash, FontAwesomeIcon.Phone, FontAwesomeIcon.Thumbtack,
        FontAwesomeIcon.Adjust,
    };

    private static readonly Vector4[] Accents =
    {
        AccentRing.Indigo, AccentRing.Red, AccentRing.Green, AccentRing.Azure, AccentRing.Violet,
    };

    private static readonly LocString[] Labels =
    {
        L.Settings.DoNotDisturb, L.Settings.SilentMode, L.Phone.Calls, L.ControlCenter.LockPosition,
        L.WidgetsUtility.DarkMode,
    };

    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly CallHub calls;

    public QuickTogglesWidget(Configuration configuration, ThemeProvider themes, CallHub calls)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.calls = calls;
    }

    public string Id => "settings.toggles";
    public string DisplayName => Loc.T(L.WidgetsUtility.QuickTogglesName);
    public string Description => Loc.T(L.WidgetsUtility.QuickTogglesDescription);
    public string AppId => "settings";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawGrid(context, ink, content);
            return;
        }

        DrawRow(context, ink, content);
    }

    private void DrawGrid(in WidgetContext context, in WidgetInk ink, Rect content)
    {
        var halfWidth = content.Width * 0.25f;
        var halfHeight = content.Height * 0.25f;
        var diameter = MathF.Min(WidgetMetrics.ControlLarge,
            MathF.Min(content.Width, content.Height) * 0.5f / context.Scale - WidgetMetrics.RowGap);
        for (var index = 0; index < SmallCount; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var center = new Vector2(content.Min.X + halfWidth * (1 + column * 2),
                content.Min.Y + halfHeight * (1 + row * 2));
            DrawToggle(context, ink, index, center, diameter);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect content)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var cell = content.Width / MediumCount;
        var diameter = MathF.Min(WidgetMetrics.ControlLarge, cell / scale - WidgetMetrics.RowGap * 2f);
        var lineHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var gap = WidgetMetrics.Gutter * scale;
        var block = diameter * scale + gap + lineHeight * LabelLines;
        var top = content.Center.Y - block * 0.5f;
        var labelWidth = cell - WidgetMetrics.RowGap * scale;
        for (var index = 0; index < MediumCount; index++)
        {
            var centerX = content.Min.X + cell * (index + 0.5f);
            var center = new Vector2(centerX, top + diameter * scale * 0.5f);
            var value = DrawToggle(context, ink, index, center, diameter);
            var lines = WidgetText.Clamp(Loc.T(Labels[index]), WidgetType.Caption, labelWidth, LabelLines);
            WidgetText.LinesCentered(drawList, lines, new Vector2(centerX, top + diameter * scale + gap),
                value ? ink.Primary : ink.Secondary, WidgetType.Caption, lineHeight);
        }
    }

    private bool DrawToggle(in WidgetContext context, in WidgetInk ink, int index, Vector2 center, float diameter)
    {
        var value = Read(index);
        var next = WidgetControls.Toggle(context, ink, index, center, diameter, Icons[index], value, Accents[index]);
        if (next != value)
        {
            Apply(index, next);
        }

        return next;
    }

    private bool Read(int index) => index switch
    {
        DoNotDisturb => configuration.DoNotDisturb,
        Silent => configuration.SilentMode,
        Calls => configuration.CallsEnabled,
        LockPosition => configuration.LockPosition,
        DarkMode => !WidgetInk.IsLightTheme(themes.Current),
        _ => false,
    };

    private void Apply(int index, bool value)
    {
        switch (index)
        {
            case DoNotDisturb:
                configuration.DoNotDisturb = value;
                break;
            case Silent:
                configuration.SilentMode = value;
                break;
            case Calls:
                calls.SetEnabled(value);
                return;
            case LockPosition:
                configuration.LockPosition = value;
                break;
            case DarkMode:
                configuration.ThemeMode = value ? ThemeMode.Dark : ThemeMode.Light;
                themes.Apply(configuration);
                break;
            default:
                return;
        }

        configuration.Save();
    }

    public void Dispose()
    {
    }
}
