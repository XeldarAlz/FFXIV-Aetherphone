using System.Text;
using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace Aetherphone.Core.Casino;

internal sealed unsafe class TradeWindowReader
{
    public const string TradeAddonName = "Trade";

    private byte[] partnerBytes = Array.Empty<byte>();
    private string partnerName = string.Empty;
    private string partnerWorld = string.Empty;

    public TradeWindowSample Sample()
    {
        if (!GameMemory.Attached)
        {
            var simulated = GameMemory.Simulated;
            return new TradeWindowSample(false, string.Empty, string.Empty, 0, 0, simulated?.Gil ?? 0L,
                simulated is not null);
        }

        var inventory = InventoryManager.Instance();
        var walletRead = inventory != null && Plugin.ClientState.IsLoggedIn;
        var walletGil = walletRead ? (long)inventory->GetGil() : 0L;
        if (!WindowVisible())
        {
            return new TradeWindowSample(false, string.Empty, string.Empty, 0, 0, walletGil, walletRead);
        }

        ReadPartner();
        var numbers = TradeNumberArray.Instance();
        var mine = numbers == null ? 0L : numbers->SendGilCount;
        var theirs = numbers == null ? 0L : numbers->ReceiveGilCount;
        return new TradeWindowSample(true, partnerName, partnerWorld, mine, theirs, walletGil, walletRead);
    }

    private void ReadPartner()
    {
        var strings = TradeStringArray.Instance();
        var raw = strings == null || !strings->TradePartnerName.HasValue
            ? ReadOnlySpan<byte>.Empty
            : strings->TradePartnerName.AsSpan();
        if (raw.SequenceEqual(partnerBytes))
        {
            return;
        }

        partnerBytes = raw.ToArray();
        if (raw.IsEmpty)
        {
            partnerName = string.Empty;
            partnerWorld = string.Empty;
            return;
        }

        Split(new ReadOnlySeStringSpan(raw), out partnerName, out partnerWorld);
    }

    private static void Split(ReadOnlySeStringSpan text, out string name, out string world)
    {
        var nameText = new StringBuilder();
        var worldText = new StringBuilder();
        var pastName = false;
        foreach (var payload in text)
        {
            if (payload.Type != ReadOnlySePayloadType.Text)
            {
                pastName |= nameText.Length > 0;
                continue;
            }

            (pastName ? worldText : nameText).Append(Encoding.UTF8.GetString(payload.Body));
        }

        name = nameText.ToString().Trim();
        world = worldText.ToString().Trim();
    }

    private static bool WindowVisible()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(TradeAddonName).Address;
        return addon != null && addon->IsVisible;
    }
}
