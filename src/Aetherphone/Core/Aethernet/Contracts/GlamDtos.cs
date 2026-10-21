using System.Text.Json.Serialization;

namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record GlamGearDto(
    int Slot = 0,
    uint ItemId = 0,
    byte Stain0 = 0,
    byte Stain1 = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Optional = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GlamModDto[]? Mods = null);

internal sealed record GlamModDto(
    string Name = "",
    string Author = "",
    string Version = "",
    string Link = "",
    GlamOptionDto[]? Options = null);

internal sealed record GlamOptionDto(string Group = "", string[]? Picked = null);
