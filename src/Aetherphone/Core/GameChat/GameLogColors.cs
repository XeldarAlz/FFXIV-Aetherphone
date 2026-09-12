using Aetherphone.Core.Theme;

namespace Aetherphone.Core.GameChat;

internal static class GameLogColors
{
    private const uint RgbMask = 0xFFFFFF;
    private const int RedShift = 16;
    private const int GreenShift = 8;
    private const float ByteScale = 1f / 255f;

    public static string? OptionFor(GameChannel channel)
    {
        if (channel.Category == ChannelCategory.Linkshell)
        {
            return string.Concat("ColorLS", (channel.Slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (channel.Category == ChannelCategory.CrossWorld)
        {
            return channel.Slot == 0
                ? "ColorCWLS"
                : string.Concat("ColorCWLS", (channel.Slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return channel.Key switch
        {
            GameChannels.SayKey => "ColorSay",
            "shout" => "ColorShout",
            "yell" => "ColorYell",
            GameChannels.TellKey => "ColorTell",
            GameChannels.PartyKey => "ColorParty",
            GameChannels.AllianceKey => "ColorAlliance",
            "pvpteam" => "ColorPvPGroup",
            GameChannels.FreeCompanyKey => "ColorFCompany",
            GameChannels.NoviceKey => "ColorBeginner",
            GameChannels.EmoteKey => "ColorEmote",
            GameChannels.EchoKey => "ColorEcho",
            GameChannels.SystemKey => "ColorSysMsg",
            _ => null,
        };
    }

    public static bool TryDecode(uint packed, out Vector4 color)
    {
        var rgb = packed & RgbMask;
        if (rgb == 0)
        {
            color = default;
            return false;
        }

        color = new Vector4(((rgb >> RedShift) & 0xFF) * ByteScale, ((rgb >> GreenShift) & 0xFF) * ByteScale,
            (rgb & 0xFF) * ByteScale, 1f);
        return true;
    }

    public static bool TryRead(GameChannel channel, out Vector4 color)
    {
        color = default;
        var option = OptionFor(channel);
        if (option is null)
        {
            return false;
        }

        return Plugin.GameConfig.UiConfig.TryGetUInt(option, out var packed) && TryDecode(packed, out color);
    }

    public static int ImportAll(ChannelStyleStore store)
    {
        var channels = GameChannels.All;
        var draft = new ChannelStyle();
        var imported = 0;
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            if (!TryRead(channel, out var color))
            {
                continue;
            }

            var packed = ChannelInk.Pack(color);
            store.Load(channel.Key, draft);
            draft.SetInk(ChannelStyle.IncomingNameSlot, packed);
            draft.SetInk(ChannelStyle.IncomingBodySlot, packed);
            draft.SetInk(ChannelStyle.OutgoingNameSlot, packed);
            draft.SetInk(ChannelStyle.OutgoingBodySlot, packed);
            store.Apply(channel.Key, draft);
            imported++;
        }

        return imported;
    }
}
