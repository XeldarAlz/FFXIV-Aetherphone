using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Crater;

internal sealed class CraterLabels
{
    private const ulong NameSalt = 0x6D6F67676C65UL;

    private static readonly LocString[] BotNames =
    {
        L.Crater.BotMogwin, L.Crater.BotKupka, L.Crater.BotMoglin, L.Crater.BotMogsy, L.Crater.BotPukla,
        L.Crater.BotKumop,
    };

    private static readonly LocString[] WeaponTitles =
    {
        L.Crater.Shell, L.Crater.Grenade, L.Crater.Cluster, L.Crater.Drill, L.Crater.Shield, L.Crater.Teleport,
    };

    private readonly string[] teamNames = new string[CraterRules.MaxTeams];
    private readonly string[] turnLines = new string[CraterRules.MaxTeams];
    private readonly string[] winLines = new string[CraterRules.MaxTeams];
    private readonly string[] weaponNames = new string[CraterRules.WeaponCount];
    private readonly string[] fuseLabels = new string[CraterRules.MaxFuse + 1];
    private readonly string[] fuseHints = new string[CraterRules.MaxFuse + 1];
    private readonly int[] botNames = new int[CraterRules.MaxTeams];
    private LanguageInfo? language;
    private bool hotSeat;
    private int humanTeams;

    public void Configure(ulong seed, bool hotSeatMatch, int humans)
    {
        hotSeat = hotSeatMatch;
        humanTeams = humans;
        var random = GameRandom.FromSeed(seed ^ NameSalt);
        var offset = random.Next(BotNames.Length);
        for (var team = 0; team < CraterRules.MaxTeams; team++)
        {
            botNames[team] = (offset + team) % BotNames.Length;
        }

        language = null;
    }

    public string TeamName(int team)
    {
        Sync();
        return teamNames[Slot(team)];
    }

    public string TurnLine(int team)
    {
        Sync();
        return turnLines[Slot(team)];
    }

    public string WinLine(int team)
    {
        Sync();
        return winLines[Slot(team)];
    }

    public string WeaponName(CraterWeapon weapon)
    {
        Sync();
        return weaponNames[(int)weapon];
    }

    public string FuseLabel(int seconds)
    {
        Sync();
        return fuseLabels[Math.Clamp(seconds, CraterRules.MinFuse, CraterRules.MaxFuse)];
    }

    public string FuseHint(int seconds)
    {
        Sync();
        return fuseHints[Math.Clamp(seconds, CraterRules.MinFuse, CraterRules.MaxFuse)];
    }

    private static int Slot(int team) => Math.Clamp(team, 0, CraterRules.MaxTeams - 1);

    private void Sync()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        for (var team = 0; team < CraterRules.MaxTeams; team++)
        {
            if (hotSeat)
            {
                teamNames[team] = GameSeats.Name(team);
                turnLines[team] = Loc.T(L.Crater.TeamTurn, teamNames[team]);
                winLines[team] = GameSeats.WinLine(team);
                continue;
            }

            if (team < humanTeams)
            {
                teamNames[team] = Loc.T(L.Crater.You);
                turnLines[team] = Loc.T(L.Crater.YourTurn);
                winLines[team] = Loc.T(L.Crater.YourTeamWins);
                continue;
            }

            teamNames[team] = Loc.T(BotNames[botNames[team]]);
            turnLines[team] = Loc.T(L.Crater.TeamTurn, teamNames[team]);
            winLines[team] = Loc.T(L.Crater.TeamWins, teamNames[team]);
        }

        for (var weapon = 0; weapon < CraterRules.WeaponCount; weapon++)
        {
            weaponNames[weapon] = Loc.T(WeaponTitles[weapon]);
        }

        for (var seconds = CraterRules.MinFuse; seconds <= CraterRules.MaxFuse; seconds++)
        {
            fuseLabels[seconds] = Loc.T(L.Crater.FuseSeconds, GameNumber.Label(seconds));
            fuseHints[seconds] = Loc.T(L.Crater.GrenadeHint, GameNumber.Label(seconds));
        }
    }
}
