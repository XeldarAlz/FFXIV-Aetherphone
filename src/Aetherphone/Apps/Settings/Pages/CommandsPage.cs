using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CommandsPage : ISettingsPage
{
    private const float RowHeight = 54f;

    private readonly record struct CommandEntry(string Syntax, LocString Description);

    private static readonly CommandEntry[] Commands =
    {
        new(AepConstants.PrimaryCommand, L.Settings.CommandToggle),
        new(AepConstants.AliasCommand, L.Settings.CommandAlias),
        new($"{AepConstants.PrimaryCommand} market [item]", L.Settings.CommandMarket),
        new($"{AepConstants.PrimaryCommand} reset", L.Settings.CommandReset),
        new($"{AepConstants.PrimaryCommand} test", L.Settings.CommandTest),
    };

    private static readonly SettingsEntry[] Searchable =
    {
        new(L.Settings.CommandToggle),
        new(L.Settings.CommandAlias),
        new(L.Settings.CommandMarket),
        new(L.Settings.CommandReset),
        new(L.Settings.CommandTest),
    };

    public string Title => Loc.T(L.Settings.Commands);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Terminal;
    public Vector4 Tint => new(0.46f, 0.62f, 0.92f, 1f);
    public ReadOnlySpan<SettingsEntry> Entries => Searchable;

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

    private static void DrawRow(Rect row, CommandEntry entry, Core.Theme.PhoneTheme theme, float scale)
    {
        var syntax = Typography.FitText(entry.Syntax, row.Width, 0.92f, FontWeight.SemiBold);
        var syntaxHeight = Typography.Measure(syntax, 0.92f, FontWeight.SemiBold).Y;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 10f * scale), syntax, theme.Accent, 0.92f,
            FontWeight.SemiBold);
        var description = Typography.FitText(Loc.T(entry.Description), row.Width, 0.8f, FontWeight.Regular);
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 10f * scale + syntaxHeight + 4f * scale), description,
            theme.TextMuted, 0.8f);
    }
}
