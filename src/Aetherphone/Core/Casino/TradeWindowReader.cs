using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Aetherphone.Core.Casino;

internal interface ITradeWindowReader : IDisposable
{
    TradeWindowSample Sample();
}

internal sealed unsafe class TradeWindowReader : ITradeWindowReader
{
    public const string TradeAddonName = "Trade";

    private const int MaxNameCandidates = 12;
    private const int MaxNodeDepth = 4;
    private const int MinNameLength = 3;

    private readonly List<string> names = new(MaxNameCandidates);
    private string[] candidates = Array.Empty<string>();
    private bool open;
    private long leftOffer;
    private long rightOffer;

    public TradeWindowReader()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, TradeAddonName, OnOpened);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, TradeAddonName, OnChanged);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, TradeAddonName, OnChanged);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, TradeAddonName, OnClosed);
    }

    public TradeWindowSample Sample()
    {
        return new TradeWindowSample(open, candidates, leftOffer, rightOffer, WalletGil());
    }

    private void OnOpened(AddonEvent type, AddonArgs args)
    {
        open = true;
        candidates = Array.Empty<string>();
        leftOffer = 0;
        rightOffer = 0;
        Read(args);
    }

    private void OnChanged(AddonEvent type, AddonArgs args)
    {
        open = true;
        Read(args);
    }

    private void OnClosed(AddonEvent type, AddonArgs args)
    {
        Read(args);
        open = false;
    }

    private void Read(AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null)
        {
            return;
        }

        names.Clear();
        var left = 0L;
        var right = 0L;
        var middle = addon->X + addon->RootNode->Width * addon->Scale * 0.5f;
        Walk(&addon->UldManager, middle, ref left, ref right, 0);
        if (names.Count > 0)
        {
            candidates = names.ToArray();
        }

        leftOffer = left;
        rightOffer = right;
    }

    private void Walk(AtkUldManager* manager, float middle, ref long left, ref long right, int depth)
    {
        if (manager == null || depth > MaxNodeDepth)
        {
            return;
        }

        for (var index = 0; index < manager->NodeListCount; index++)
        {
            var node = manager->NodeList[index];
            if (node == null || !node->IsVisible())
            {
                continue;
            }

            if (node->Type == NodeType.Text)
            {
                Absorb(((AtkTextNode*)node)->NodeText.ToString(), node->ScreenX < middle, ref left, ref right);
                continue;
            }

            if ((ushort)node->Type < 1000)
            {
                continue;
            }

            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
            {
                Walk(&component->UldManager, middle, ref left, ref right, depth + 1);
            }
        }
    }

    private void Absorb(string text, bool leftSide, ref long left, ref long right)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (TradeNames.TryAmount(text, out var amount))
        {
            if (leftSide)
            {
                left = Math.Max(left, amount);
            }
            else
            {
                right = Math.Max(right, amount);
            }

            return;
        }

        if (text.Length >= MinNameLength && names.Count < MaxNameCandidates && text.IndexOf(' ') > 0)
        {
            names.Add(text);
        }
    }

    private static long WalletGil()
    {
        var manager = InventoryManager.Instance();
        return manager == null ? 0 : manager->GetGil();
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, TradeAddonName, OnOpened);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, TradeAddonName, OnChanged);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, TradeAddonName, OnChanged);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, TradeAddonName, OnClosed);
    }
}
