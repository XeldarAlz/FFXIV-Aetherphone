using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierExtras
{
    public static readonly TextStyle TitleStyle = TextStyles.SubheadlineEmphasized;
    public static readonly TextStyle HintStyle = TextStyles.Footnote;

    private const string AutoToggleId = "casino.cashier.auto";
    private const string AutoToggleOverlayId = "casino.cashier.auto.overlay";
    private const float DividerAlpha = 0.06f;

    private readonly CasinoStore store;
    private bool limitsRequested;

    public CashierExtras(CasinoStore store)
    {
        this.store = store;
    }

    public static float TextHeight(string hint, float width) =>
        Typography.LineHeight(TitleStyle) + Typography.MeasureWrappedBlock(hint, HintStyle, width).Y;

    public bool TakeLimitsRequest()
    {
        var requested = limitsRequested;
        limitsRequested = false;
        return requested;
    }

    public void Draw(ImDrawListPtr drawList, AppSkin ui, in CashierExtrasLayout layout, bool interactive, bool overlay,
        float scale)
    {
        var card = layout.Card;
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        var pad = CashierLayout.CardPad * scale;
        drawList.AddLine(new Vector2(card.Min.X + pad, layout.Divider), new Vector2(card.Max.X - pad, layout.Divider),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, DividerAlpha)), Metrics.Stroke.Hairline);
        DrawAuto(drawList, ui, layout.Auto, interactive, overlay, scale);
        DrawLimits(drawList, ui, layout.Limits, interactive, overlay, scale);
    }

    private void DrawAuto(ImDrawListPtr drawList, AppSkin ui, in CashierRowLayout row, bool interactive, bool overlay,
        float scale)
    {
        CasinoArt.IconTileAt(drawList, row.IconCenter, CashierLayout.Icon * scale, AccentRing.Azure,
            FontAwesomeIcon.RedoAlt);
        DrawText(drawList, ui, row.Text, Loc.T(L.Chips.AutoTopUp), Loc.T(L.Chips.AutoTopUpHint));
        var on = store.AutoTopUp;
        if (Pressed(row.Row, interactive, overlay, false))
        {
            on = !on;
            store.SetAutoTopUp(on);
            UiFeedback.Play(on ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        Toggle.Draw(overlay ? AutoToggleOverlayId : AutoToggleId, row.Trailing, on, ui.Theme, 1f, false);
    }

    private void DrawLimits(ImDrawListPtr drawList, AppSkin ui, in CashierRowLayout row, bool interactive,
        bool overlay, float scale)
    {
        CasinoArt.IconTileAt(drawList, row.IconCenter, CashierLayout.Icon * scale, AccentRing.Rose,
            FontAwesomeIcon.Heart);
        DrawText(drawList, ui, row.Text, Loc.T(L.Casino.LimitsRow), Loc.T(L.CashierDesk.LimitsHint));
        CasinoArt.Chevron(drawList, row.Trailing.Center, ui.MutedInk);
        if (Pressed(row.Row, interactive, overlay, true))
        {
            limitsRequested = true;
        }
    }

    private static void DrawText(ImDrawListPtr drawList, AppSkin ui, Rect text, string title, string hint)
    {
        Typography.Draw(drawList, text.Min, Typography.FitText(title, text.Width, TitleStyle), ui.TitleInk,
            TitleStyle);
        Typography.DrawWrappedLeft(new Vector2(text.Min.X, text.Min.Y + Typography.LineHeight(TitleStyle)), hint,
            ui.MutedInk, HintStyle, text.Width);
    }

    private static bool Pressed(Rect row, bool interactive, bool overlay, bool tapSound)
    {
        if (!interactive)
        {
            return false;
        }

        var hovered = overlay ? UiInteract.HoverWindowOnly(row.Min, row.Max) : UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered, tapSound);
    }
}
