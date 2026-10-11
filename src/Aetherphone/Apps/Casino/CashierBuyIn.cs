using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal readonly record struct BuyInBounds(long WalletCoins, long Rate)
{
    public static BuyInBounds Of(CasinoStateDto? state, long walletCoins) =>
        new(Math.Max(0, walletCoins), CasinoCashier.Rate(state));

    public bool Allows(long coins) => coins >= ChipsAmounts.MinimumCoins && coins <= WalletCoins;

    public long ChipsFor(long coins) => coins <= 0 ? 0 : coins * Rate;
}

internal sealed class CashierBuyIn
{
    private const int FieldDigits = 12;

    private readonly CasinoStore store;
    private readonly CasinoTextCache texts = new();
    private readonly long[] quick = new long[ChipsAmounts.QuickCount];
    private int quickCount;
    private long quickWallet = -1;
    private string buffer = string.Empty;

    public CashierBuyIn(CasinoStore store)
    {
        this.store = store;
    }

    public long Coins =>
        long.TryParse(buffer, NumberStyles.None, CultureInfo.InvariantCulture, out var coins) && coins > 0 ? coins : 0;

    public void Reset(long suggestedChips)
    {
        var coins = ChipsAmounts.CoinsFor(suggestedChips, store.Rate);
        buffer = coins > 0 ? coins.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    public void Clear()
    {
        buffer = string.Empty;
    }

    public int TileCount(long walletCoins)
    {
        if (walletCoins != quickWallet)
        {
            quickWallet = walletCoins;
            quickCount = ChipsAmounts.Quick(walletCoins, quick);
        }

        return quickCount;
    }

    public void Draw(ImDrawListPtr drawList, AppSkin ui, in CashierLayout layout, in BuyInBounds bounds,
        bool interactive, bool overlay)
    {
        var heading = layout.BuyHeading;
        Typography.Draw(drawList, heading.Min, Typography.FitText(Loc.T(L.Chips.BuyHeading), heading.Width,
            TextStyles.SubheadlineEmphasized), ui.TitleInk, TextStyles.SubheadlineEmphasized);
        DrawField(drawList, ui, layout.Field, interactive);
        if (layout.HasTiles)
        {
            DrawTiles(drawList, ui, layout, interactive, overlay);
        }

        var coins = Coins;
        var line = LineFor(bounds, coins, out var warning);
        var status = layout.Status;
        Typography.Draw(drawList, status.Min, Typography.FitText(line, status.Width, TextStyles.Footnote),
            warning ? ui.Accent : ui.BodyInk, TextStyles.Footnote);

        var allowed = bounds.Allows(coins);
        var label = allowed ? texts.Compact(L.Strip.GetChipsFor, bounds.ChipsFor(coins)) : Loc.T(L.Chips.BuyHeading);
        if (Button.Draw(drawList, layout.Buy, label, ui.Ink, ButtonStyle.Prominent,
                enabled: interactive && allowed && !store.MovingMoney, overlay: overlay, id: "cashier.getchips"))
        {
            store.BuyChips(coins);
        }
    }

    private void DrawTiles(ImDrawListPtr drawList, AppSkin ui, in CashierLayout layout, bool interactive,
        bool overlay)
    {
        var enabled = interactive && !store.MovingMoney;
        var style = Button.LabelStyle(layout.TileRow.Height);
        for (var index = 0; index < layout.TileCount && index < quickCount; index++)
        {
            var rect = layout.Tile(index);
            var hovered = enabled && (overlay
                ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
                : UiInteract.Hover(rect.Min, rect.Max));
            var face = Button.Surface(drawList, rect, ui.Ink, ButtonStyle.Gray, ButtonRole.Normal, enabled, hovered,
                ImGui.GetID($"cashier.quick{index}"));
            var text = NumberText.Compact(quick[index]);
            var size = CurrencyGlyph.MeasureAmount(text, style);
            var room = face.Face.Width - face.Face.Height * 0.5f;
            var origin = new Vector2(face.Face.Center.X - MathF.Min(size.X, room) * 0.5f,
                face.Face.Center.Y - size.Y * 0.5f);
            if (size.X <= room)
            {
                CurrencyGlyph.DrawAmount(drawList, origin, text, CurrencyKind.Coins, face.LabelInk, style,
                    face.LabelInk.W);
            }
            else
            {
                Typography.DrawCentered(drawList, face.Face.Center, text, face.LabelInk, style);
            }

            if (enabled && UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                buffer = quick[index].ToString(CultureInfo.InvariantCulture);
                store.BuyChips(quick[index]);
            }
        }
    }

    private string LineFor(in BuyInBounds bounds, long coins, out bool warning)
    {
        warning = false;
        if (bounds.WalletCoins < ChipsAmounts.MinimumCoins)
        {
            warning = true;
            return Loc.T(L.Casino.NotEnoughCoins);
        }

        if (coins <= 0)
        {
            return texts.Numbers(L.Strip.BuyInRange, ChipsAmounts.MinimumCoins, bounds.WalletCoins);
        }

        if (coins > bounds.WalletCoins)
        {
            warning = true;
            return Loc.T(L.Casino.NotEnoughCoins);
        }

        return texts.Number(L.Strip.BecomesChips, bounds.ChipsFor(coins));
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin ui, Rect field, bool interactive)
    {
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        var glyph = Typography.LineHeight(TextStyles.Body) * CurrencyGlyph.GlyphFraction;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, new Vector2(capsule.Min.X + inset + glyph * 0.5f,
            field.Center.Y), glyph);
        var textLeft = capsule.Min.X + inset + glyph + inset * 0.5f;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(textLeft, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Max.X - inset - textLeft);
        using (ImRaii.Disabled(!interactive))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##cashierCoins", Loc.T(L.Strip.CoinsFieldHint), ref buffer, FieldDigits + 1,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        ImGui.SetCursorScreenPos(cursor);
    }
}
