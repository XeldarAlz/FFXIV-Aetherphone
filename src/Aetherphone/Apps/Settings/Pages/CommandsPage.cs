using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Commands;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CommandsPage : ISettingsPage
{
    private const float RowHeight = 54f;

    private static readonly PhoneCommandEntry[] Commands = PhoneCommandCatalog.Entries;
    private static readonly SettingsEntry[] Searchable = BuildSearchable();

    public string Title => Loc.T(L.Settings.Commands);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Terminal;
    public Vector4 Tint => new(0.46f, 0.62f, 0.92f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;

    private static SettingsEntry[] BuildSearchable()
    {
        var entries = new SettingsEntry[Commands.Length];
        for (var index = 0; index < Commands.Length; index++)
        {
            entries[index] = new SettingsEntry(Commands[index].Description);
        }

        return entries;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Commands), theme, Loc.T(L.Settings.CommandsHint));
            var card = GroupCard.Begin(theme, Commands.Length, RowHeight);
            for (var index = 0; index < Commands.Length; index++)
            {
                DrawRow(card.NextRow(), Commands[index], theme, scale);
            }

            card.End();
        }
    }

    private static void DrawRow(Rect row, in PhoneCommandEntry entry, Core.Theme.PhoneTheme theme, float scale)
    {
        var syntax = Typography.FitText(entry.Syntax, row.Width, TextStyles.SubheadlineEmphasized);
        var syntaxHeight = Typography.Measure(syntax, TextStyles.SubheadlineEmphasized).Y;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + Metrics.Space.Md * scale), syntax, theme.Accent,
            TextStyles.SubheadlineEmphasized);
        var description = Typography.FitText(Loc.T(entry.Description), row.Width, TextStyles.Footnote);
        Typography.Draw(
            new Vector2(row.Min.X, row.Min.Y + Metrics.Space.Md * scale + syntaxHeight + Metrics.Space.Xxs * scale),
            description, theme.TextMuted, TextStyles.Footnote);
    }
}
