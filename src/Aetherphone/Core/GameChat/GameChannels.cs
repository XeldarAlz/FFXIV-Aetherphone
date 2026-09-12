using System.Collections.Frozen;
using System.Globalization;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Game.Text;

namespace Aetherphone.Core.GameChat;

internal static class GameChannels
{
    public const int LinkshellSlots = 8;
    public const string SayKey = "say";
    public const string TellKey = "tell";
    public const string PartyKey = "party";
    public const string AllianceKey = "alliance";
    public const string FreeCompanyKey = "fc";
    public const string NoviceKey = "novice";
    public const string EchoKey = "echo";
    public const string SystemKey = "system";
    public const string EmoteKey = "emote";
    public const string FollowGameKey = "game";

    private static readonly GameChannel[] Catalog = BuildCatalog();
    private static readonly FrozenDictionary<XivChatType, GameChannel> ByKind = BuildKindIndex();
    private static readonly FrozenDictionary<string, GameChannel> ByKey = BuildKeyIndex();
    private static readonly string[] Names = new string[Catalog.Length];
    private static readonly string[] ShortCodes = BuildShortCodes();
    private static LanguageInfo? namesLanguage;

    public static ReadOnlySpan<GameChannel> All => Catalog;

    public static string ShortCode(GameChannel channel) => ShortCodes[channel.Index];

    private static string[] BuildShortCodes()
    {
        var codes = new string[Catalog.Length];
        for (var index = 0; index < Catalog.Length; index++)
        {
            codes[index] = ShortCodeFor(Catalog[index]);
        }

        return codes;
    }

    private static string ShortCodeFor(GameChannel channel)
    {
        var slot = (channel.Slot + 1).ToString(CultureInfo.InvariantCulture);
        return channel.Category switch
        {
            ChannelCategory.Linkshell => string.Concat("LS", slot),
            ChannelCategory.CrossWorld => string.Concat("CW", slot),
            _ => channel.Key switch
            {
                SayKey => "Say",
                "shout" => "Sh",
                "yell" => "Y",
                EmoteKey => "Em",
                PartyKey => "P",
                AllianceKey => "A",
                "pvpteam" => "PvP",
                FreeCompanyKey => "FC",
                NoviceKey => "NN",
                TellKey => "Tell",
                EchoKey => "Echo",
                SystemKey => "Sys",
                _ => channel.Key.ToUpperInvariant(),
            },
        };
    }

    public static bool TryResolve(XivChatType kind, out GameChannel channel) => ByKind.TryGetValue(kind, out channel!);

    public static bool TryByKey(string key, out GameChannel channel) => ByKey.TryGetValue(key, out channel!);

    public static GameChannel ByIndex(int index) => Catalog[index];

    public static string DisplayName(GameChannel channel)
    {
        if (!ReferenceEquals(namesLanguage, Loc.Current))
        {
            namesLanguage = Loc.Current;
            for (var index = 0; index < Catalog.Length; index++)
            {
                Names[index] = Format(Catalog[index]);
            }
        }

        return Names[channel.Index];
    }

    private static string Format(GameChannel channel) =>
        channel.IsSlotted ? Loc.T(channel.Label, channel.Slot + 1) : Loc.T(channel.Label);

    private static GameChannel[] BuildCatalog()
    {
        var catalog = new List<GameChannel>(28);
        Add(catalog, SayKey, "/say", L.Linkpearl.ChannelSay, ChannelTints.Say, ChannelCategory.Local,
            XivChatType.Say);
        Add(catalog, "shout", "/shout", L.Linkpearl.ChannelShout, ChannelTints.Shout, ChannelCategory.Local,
            XivChatType.Shout);
        Add(catalog, "yell", "/yell", L.Linkpearl.ChannelYell, ChannelTints.Yell, ChannelCategory.Local,
            XivChatType.Yell);
        Add(catalog, "emote", "/em", L.Linkpearl.ChannelEmote, ChannelTints.Emote, ChannelCategory.Local,
            XivChatType.CustomEmote, XivChatType.StandardEmote);
        Add(catalog, PartyKey, "/party", L.Linkpearl.ChannelParty, ChannelTints.Party, ChannelCategory.Group,
            XivChatType.Party, XivChatType.CrossParty);
        Add(catalog, AllianceKey, "/alliance", L.Linkpearl.ChannelAlliance, ChannelTints.Alliance,
            ChannelCategory.Group, XivChatType.Alliance);
        Add(catalog, "pvpteam", "/pvpteam", L.Linkpearl.ChannelPvpTeam, ChannelTints.PvpTeam, ChannelCategory.Group,
            XivChatType.PvPTeam);
        Add(catalog, FreeCompanyKey, "/freecompany", L.Linkpearl.ChannelFreeCompany, ChannelTints.FreeCompany,
            ChannelCategory.Community, XivChatType.FreeCompany, XivChatType.FreeCompanyAnnouncement);
        Add(catalog, NoviceKey, "/novice", L.Linkpearl.ChannelNoviceNetwork, ChannelTints.NoviceNetwork,
            ChannelCategory.Community, XivChatType.NoviceNetwork);
        for (var slot = 0; slot < LinkshellSlots; slot++)
        {
            AddSlotted(catalog, $"ls{slot + 1}", $"/linkshell{slot + 1}", L.Messages.Linkshell, ChannelTints.Linkshell,
                ChannelCategory.Linkshell, slot, (XivChatType)((int)XivChatType.Ls1 + slot));
        }

        for (var slot = 0; slot < LinkshellSlots; slot++)
        {
            AddSlotted(catalog, $"cwls{slot + 1}", $"/cwlinkshell{slot + 1}", L.Messages.CrossWorldLinkshell,
                ChannelTints.CrossWorldLinkshell, ChannelCategory.CrossWorld, slot, CrossWorldKind(slot));
        }

        Add(catalog, TellKey, "/tell", L.Linkpearl.ChannelTell, ChannelTints.Tell, ChannelCategory.Direct,
            XivChatType.TellIncoming, XivChatType.TellOutgoing);
        Add(catalog, EchoKey, "/echo", L.Linkpearl.ChannelEcho, ChannelTints.Echo, ChannelCategory.System,
            XivChatType.Echo);
        Add(catalog, SystemKey, string.Empty, L.Linkpearl.ChannelSystem, ChannelTints.System, ChannelCategory.System,
            XivChatType.SystemMessage, XivChatType.GatheringSystemMessage, XivChatType.ErrorMessage,
            XivChatType.RetainerSale, XivChatType.Notice);
        return catalog.ToArray();
    }

    private static XivChatType CrossWorldKind(int slot) =>
        slot == 0
            ? XivChatType.CrossLinkShell1
            : (XivChatType)((int)XivChatType.CrossLinkShell2 + slot - 1);

    private static void Add(List<GameChannel> catalog, string key, string command, LocString label, Vector4 tint,
        ChannelCategory category, params XivChatType[] kinds) =>
        catalog.Add(new GameChannel
        {
            Index = catalog.Count,
            Key = key,
            Kinds = kinds,
            Command = command,
            Label = label,
            Tint = tint,
            Category = category,
        });

    private static void AddSlotted(List<GameChannel> catalog, string key, string command, LocString label,
        Vector4 tint, ChannelCategory category, int slot, XivChatType kind) =>
        catalog.Add(new GameChannel
        {
            Index = catalog.Count,
            Key = key,
            Kinds = new[] { kind },
            Command = command,
            Label = label,
            Tint = tint,
            Category = category,
            Slot = slot,
        });

    private static FrozenDictionary<XivChatType, GameChannel> BuildKindIndex()
    {
        var map = new Dictionary<XivChatType, GameChannel>(48);
        for (var index = 0; index < Catalog.Length; index++)
        {
            var channel = Catalog[index];
            for (var kindIndex = 0; kindIndex < channel.Kinds.Length; kindIndex++)
            {
                map[channel.Kinds[kindIndex]] = channel;
            }
        }

        return map.ToFrozenDictionary();
    }

    private static FrozenDictionary<string, GameChannel> BuildKeyIndex()
    {
        var map = new Dictionary<string, GameChannel>(Catalog.Length, StringComparer.Ordinal);
        for (var index = 0; index < Catalog.Length; index++)
        {
            map[Catalog[index].Key] = Catalog[index];
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
