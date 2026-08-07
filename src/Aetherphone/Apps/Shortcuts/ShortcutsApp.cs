using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shortcuts;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Shortcuts;

internal sealed partial class ShortcutsApp : IPhoneApp
{
    private enum ShortcutsScreen : byte
    {
        Home,
        Editor,
        Appearance,
        Plugin,
        PluginPicker,
        Import,
    }

    private const float RowHeight = 62f;

    public string Id => "shortcuts";
    public string DisplayName => Loc.T(L.Apps.Shortcuts);
    public string Glyph => "S";
    public int BadgeCount => 0;

    private readonly ShortcutStore store;
    private readonly ShortcutRunner runner;
    private readonly PluginCatalog catalog;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Shortcuts);
    private readonly ViewRouter<ShortcutsScreen> router;
    private readonly RouterDraw<ShortcutsScreen> drawView;
    private readonly Action back;
    private readonly string[] tabOptions = new string[2];

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;
    private string libraryQuery = string.Empty;

    public ShortcutsApp(ShortcutStore store, ShortcutRunner runner, ConfirmService confirm)
    {
        this.store = store;
        this.runner = runner;
        this.confirm = confirm;
        catalog = store.Catalog;
        router = new ViewRouter<ShortcutsScreen>(ShortcutsScreen.Home);
        drawView = DrawView;
        back = GoBack;
        openPluginDetail = OpenPluginDetail;
        pickStepPlugin = AddOpenPluginStep;
        pickIconPlugin = UsePluginIcon;
    }

    public void OnOpened()
    {
        router.Reset();
        catalog.Invalidate();
        pluginQuery = string.Empty;
        libraryQuery = string.Empty;
    }

    public void OnClosed()
    {
        router.Reset();
        draft = null;
        importEntry = null;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;

        var delta = ImGui.GetIO().DeltaTime;
        if (copiedClock > 0f)
        {
            copiedClock -= delta;
        }

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, delta, drawView);
    }

    private void DrawView(ShortcutsScreen screen, Rect area, int depth)
    {
        var scale = UiScale.Current;
        ui.Body(area);
        switch (screen)
        {
            case ShortcutsScreen.Editor:
                DrawEditor(area, scale);
                return;
            case ShortcutsScreen.Appearance:
                DrawAppearance(area, scale);
                return;
            case ShortcutsScreen.Plugin:
                DrawPluginDetail(area, scale);
                return;
            case ShortcutsScreen.PluginPicker:
                DrawPluginPicker(area, scale);
                return;
            case ShortcutsScreen.Import:
                DrawImport(area, scale);
                return;
            default:
                DrawHome(area, scale);
                return;
        }
    }

    private void GoBack() => router.Pop();

    private void DrawHome(Rect content, float scale)
    {
        DrawTopBar(content, scale);

        var margin = Metrics.Space.Lg * scale;
        var segTop = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var segRow = new Rect(new Vector2(content.Min.X + margin, segTop),
            new Vector2(content.Max.X - margin, segTop + 30f * scale));
        tabOptions[0] = Loc.T(L.Shortcuts.TabShortcuts);
        tabOptions[1] = Loc.T(L.Shortcuts.TabPlugins);
        activeTab = SegmentStrip.Draw("shortcuts.tabs", segRow, tabOptions, activeTab, theme);

        var bodyTop = segRow.Max.Y + Metrics.Space.Sm * scale;
        if (activeTab == 1)
        {
            DrawPluginsTab(content, bodyTop, scale);
            return;
        }

        var body = new Rect(new Vector2(content.Min.X, bodyTop), content.Max);
        if (store.All.Count > 0)
        {
            var searchRow = new Rect(new Vector2(content.Min.X + margin, bodyTop),
                new Vector2(content.Max.X - margin, bodyTop + 36f * scale));
            SearchField.Draw(searchRow, "##shortcuts.librarySearch", Loc.T(L.Shortcuts.SearchShortcuts),
                ref libraryQuery, ui.Palette);
            body = new Rect(new Vector2(content.Min.X, searchRow.Max.Y + Metrics.Space.Sm * scale), content.Max);
        }

        using (AppSurface.Begin(body))
        {
            DrawLibrary(body, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawTopBar(Rect content, float scale)
    {
        var centerY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, centerY), DisplayName, ui.TitleInk, 1.15f,
            FontWeight.SemiBold);
        if (activeTab != 0)
        {
            return;
        }

        var radius = 15f * scale;
        var buttonCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius, centerY);
        if (ui.IconButton(buttonCenter, radius, FontAwesomeIcon.Plus.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.6f, Loc.T(L.Shortcuts.NewShortcut)))
        {
            StartNewShortcut();
        }

        var importCenter = new Vector2(buttonCenter.X - radius * 2.6f, centerY);
        if (ui.IconButton(importCenter, radius, FontAwesomeIcon.FileImport.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.6f, Loc.T(L.Shortcuts.ImportShortcut)))
        {
            BeginImport();
        }
    }

    private void DrawLibrary(Rect body, float scale)
    {
        var shortcuts = store.All;
        if (shortcuts.Count == 0)
        {
            DrawEmptyLibrary(body, scale);
            return;
        }

        var matches = 0;
        for (var index = 0; index < shortcuts.Count; index++)
        {
            if (Matches(shortcuts[index], libraryQuery))
            {
                matches++;
            }
        }

        if (matches == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Shortcuts.NoMatches), ui.MutedInk, TextStyles.Subheadline);
            return;
        }

        var run = runner.Snapshot();
        var card = GroupCard.Begin(theme, matches, RowHeight);
        for (var index = 0; index < shortcuts.Count; index++)
        {
            var entry = shortcuts[index];
            if (!Matches(entry, libraryQuery))
            {
                continue;
            }

            DrawShortcutRow(card.NextRow(), entry, run, scale);
        }

        card.End();
    }

    private static bool Matches(ShortcutEntry entry, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (entry.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < entry.Steps.Count; index++)
        {
            if (entry.Steps[index].Text.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawEmptyLibrary(Rect body, float scale)
    {
        var center = new Vector2(body.Center.X, body.Min.Y + 76f * scale);
        AppSkin.Icon(new Vector2(center.X, center.Y - 22f * scale), FontAwesomeIcon.Bolt.ToIconString(),
            Palette.WithAlpha(ui.MutedInk, 0.7f), 1.6f);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 12f * scale), Loc.T(L.Shortcuts.LibraryEmpty),
            ui.TitleInk, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 34f * scale), Loc.T(L.Shortcuts.LibraryEmptyHint),
            ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawShortcutRow(Rect row, ShortcutEntry entry, in ShortcutRunView run, float scale)
    {
        var tile = 38f * scale;
        var tileCenter = new Vector2(row.Min.X + tile * 0.5f, row.Center.Y);
        ShortcutArt.DrawSurface(ImGui.GetWindowDrawList(), tileCenter, tile, entry, store.Icon(entry), scale);

        var editRadius = 15f * scale;
        var editCenter = new Vector2(row.Max.X - editRadius, row.Center.Y);
        var textLeft = row.Min.X + tile + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, editCenter.X - editRadius - 8f * scale - textLeft);

        var running = run.IsRunning && run.Id == entry.Id;
        var name = ShortcutRunText.Name(entry.Name);
        Marquee.DrawLeftAuto("shortcuts.row." + entry.Id, name, textLeft, row.Center.Y - 16f * scale, textWidth,
            TextStyles.Headline, ui.TitleInk);
        var subtitle = running ? ShortcutRunText.Status(run) : Summarise(entry);
        Marquee.DrawLeftAuto("shortcuts.row.sub." + entry.Id, subtitle, textLeft, row.Center.Y + 5f * scale, textWidth,
            TextStyles.Footnote, running ? ui.Accent : ui.MutedInk);

        if (ui.IconButton(editCenter, editRadius, FontAwesomeIcon.SlidersH.ToIconString(), ui.MutedInk,
                AppSkin.Transparent, 0.62f, Loc.T(L.Shortcuts.Edit)))
        {
            StartEditShortcut(entry);
            return;
        }

        var tapMax = new Vector2(editCenter.X - editRadius, row.Max.Y);
        if (UiInteract.HoverClick(row.Min, tapMax))
        {
            runner.Run(entry);
        }
    }

    private string Summarise(ShortcutEntry entry)
    {
        if (entry.Steps.Count == 0)
        {
            return Loc.T(L.Shortcuts.NoSteps);
        }

        var first = entry.Steps[0];
        var lead = first.Kind switch
        {
            ShortcutStepKind.OpenPlugin => Loc.T(L.Shortcuts.StepOpenNamed, catalog.DisplayName(first.Text)),
            ShortcutStepKind.OpenUrl => Loc.T(L.Shortcuts.StepOpenNamed, ShortcutCommandText.HostOf(first.Text)),
            ShortcutStepKind.Wait => Loc.T(L.Shortcuts.StepWaitNamed, Seconds(first.Seconds)),
            _ => first.Text,
        };

        if (entry.Steps.Count == 1)
        {
            return lead;
        }

        return string.Concat(lead, "  ", Loc.T(L.Shortcuts.MoreSteps, entry.Steps.Count - 1));
    }

    private static string Seconds(float value) => value.ToString("0.#", Loc.Culture);

    public void Dispose()
    {
    }
}
