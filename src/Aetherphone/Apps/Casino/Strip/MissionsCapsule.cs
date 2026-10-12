using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Strip;

internal readonly record struct MissionsTally(int Done, int Total, int Ready);

internal sealed class MissionsCapsule
{
    public const float Height = FloorCapsule.Height;

    private readonly CasinoTextCache texts = new();

    public static MissionsTally Tally(CasinoMissionsDto? missions)
    {
        var total = MissionsCard.Count(missions);
        var done = 0;
        var ready = 0;
        for (var index = 0; index < total; index++)
        {
            var mission = missions!.Missions![index];
            if (!mission.Complete)
            {
                continue;
            }

            done++;
            ready += mission.Claimed ? 0 : 1;
        }

        return new MissionsTally(done, total, ready);
    }

    public static bool ShowsReady(in MissionsTally tally) => tally.Ready > 0;

    public static float Fraction(in MissionsTally tally) =>
        tally.Total <= 0 ? 0f : (float)tally.Done / tally.Total;

    public FloorCapsuleContent Content(AppSkin ui, CasinoMissionsDto missions, bool half)
    {
        var tally = Tally(missions);
        var ready = ShowsReady(tally);
        string line;
        if (half)
        {
            line = texts.Counts(L.Club.MissionProgress, tally.Done, tally.Total);
        }
        else
        {
            line = ready
                ? texts.Count(L.Floor.MissionsReady, tally.Ready)
                : texts.Counts(L.Floor.MissionsProgress, tally.Done, tally.Total);
        }

        return new FloorCapsuleContent("casino.missions.capsule", FontAwesomeIcon.Bullseye,
            ready ? CasinoColors.Money : AccentRing.Azure, Loc.T(L.Floor.MissionsTitle), Fraction(tally), line,
            ready ? CasinoColors.Money : ui.BodyInk);
    }
}
