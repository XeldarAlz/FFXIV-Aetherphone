using Aetherphone.Core.Game;

namespace Aetherphone.Harness.Scenarios;

internal sealed record CharacterProfile(string Name, ulong ContentId, uint HomeWorldId, uint CurrentWorldId,
    string CompanyTag, byte[] Customize);

internal sealed record Scenario(string Name, string Path, CharacterProfile Character, SimulatedGame Game,
    uint TerritoryId, uint MapId, bool QuestsCompleted, double Collected, IReadOnlyList<string> Warnings);
