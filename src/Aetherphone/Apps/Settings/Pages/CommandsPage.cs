using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class CommandsPage : ISettingsPage
{
    private const float RowHeight = 54f;

    private readonly record struct CommandEntry(string Syntax, LocString Description);

    private static readonly CommandEntry[] Entries =
    {
        new(AepConstants.PrimaryCommand, L.Settings.CommandToggle),
        new(AepConstants.AliasCommand, L.Settings.CommandAlias),
        new($"{AepConstants.PrimaryCommand} market [item]", L.Settings.CommandMarket),
        new($"{AepConstants.PrimaryCommand} about", L.Settings.CommandAbout),
        new($"{AepConstants.PrimaryCommand} reset", L.Settings.CommandReset),
        new($"{AepConstants.PrimaryCommand} test", L.Settings.CommandTest),
    };

    public string Title => Loc.T(L.Settings.Commands);
    public string Summary => Loc.T(L.Settings.CommandsSummary);
    public FontAwesomeIcon Icon => FontAwesomeIcon.Terminal;
    public Vector4 Tint => new(0.46f, 0.62f, 0.92f, 1f);

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Commands), theme);
            var card = GroupCard.Begin(theme, Entries.Length, RowHeight);
            for (var index = 0; index < Entries.Length; index++)
            {
                DrawRow(card.NextRow(), Entries[index], theme, scale);
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.CommandsHint), theme);
        }
    }

    private static void DrawRow(Rect row, CommandEntry entry, Core.Theme.PhoneTheme theme, float scale)
    {
        var syntaxHeight = Typography.Measure(entry.Syntax, 0.92f, FontWeight.SemiBold).Y;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 10f * scale), entry.Syntax, theme.Accent, 0.92f,
            FontWeight.SemiBold);
        var description = Loc.T(entry.Description);
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 10f * scale + syntaxHeight + 4f * scale), description,
            theme.TextMuted, 0.8f);
    }
}
