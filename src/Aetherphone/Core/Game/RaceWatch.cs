using Dalamud.Game.ClientState.Objects.Enums;

namespace Aetherphone.Core.Game;

internal sealed class RaceWatch
{
    internal const int RaceIndex = (int)CustomizeIndex.Race;
    internal const byte LalafellRaceId = 3;

    private const byte UnknownRaceId = 0;

    private volatile byte race = UnknownRaceId;

    public byte? Race => race == UnknownRaceId ? null : race;

    public bool? IsLalafell => Race is { } known ? known == LalafellRaceId : null;

    public void Forget() => race = UnknownRaceId;

    public void Observe(ReadOnlySpan<byte> customize)
    {
        if (customize.Length <= RaceIndex)
        {
            return;
        }

        var observed = customize[RaceIndex];
        if (observed == UnknownRaceId)
        {
            return;
        }

        race = observed;
    }
}
