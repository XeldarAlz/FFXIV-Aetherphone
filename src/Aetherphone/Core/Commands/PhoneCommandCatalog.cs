using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Commands;

internal readonly record struct PhoneCommandEntry(string Syntax, LocString Description);

internal static class PhoneCommandCatalog
{
    private const string Primary = AepConstants.PrimaryCommand;

    public static readonly PhoneCommandEntry[] Entries =
    {
        new(Primary, L.Settings.CommandToggle),
        new(AepConstants.AliasCommand, L.Settings.CommandAlias),
        new($"{Primary} mini", L.Settings.CommandMini),
        new($"{Primary} full", L.Settings.CommandFull),
        new($"{Primary} hide", L.Settings.CommandHide),
        new($"{Primary} open [app]", L.Settings.CommandOpen),
        new($"{Primary} settings", L.Settings.CommandSettings),
        new($"{Primary} photo", L.Settings.CommandPhoto),
        new($"{Primary} dnd", L.Settings.CommandDoNotDisturb),
        new($"{Primary} mute", L.Settings.CommandMute),
        new($"{Primary} music [action]", L.Settings.CommandMusic),
        new($"{Primary} tell [name]", L.Settings.CommandTell),
        new($"{Primary} call [contact]", L.Settings.CommandCall),
        new($"{Primary} run [shortcut]", L.Settings.CommandRun),
        new($"{Primary} market [item]", L.Settings.CommandMarket),
        new($"{Primary} reset", L.Settings.CommandReset),
        new($"{Primary} test", L.Settings.CommandTest),
        new($"{Primary} help", L.Settings.CommandHelp),
    };
}
