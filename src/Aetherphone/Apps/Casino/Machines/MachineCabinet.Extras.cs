using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed partial class MachineCabinet
{
    private const float ExtraRingWidth = 1.5f;
    private const float BuyRimAlpha = 0.55f;

    private static readonly string[] ExtraIds =
    {
        "casino.machines.turbo",
        "casino.machines.ante",
        "casino.machines.buy",
        "casino.machines.skip",
        "casino.machines.gamble",
    };

    private readonly MachineExtra[] extraKinds = new MachineExtra[MachineExtrasLayout.MaxPills];
    private readonly string[] extraLabels = new string[MachineExtrasLayout.MaxPills];
    private readonly float[] extraWidths = new float[MachineExtrasLayout.MaxPills];
    private readonly Rect[] extraRects = new Rect[MachineExtrasLayout.MaxPills];
    private readonly PanRail extrasRail = new();

    private void DrawExtras(AppSkin ui, Rect row, CasinoSittingDto sitting, bool changeable, bool blocked, float scale)
    {
        if (row.Width <= 0f || row.Height <= 0f)
        {
            return;
        }

        var skippable = !blocked && MachineExtrasLayout.Skippable(playback);
        var offered = !blocked && gamble.Offered && !Composer.Auto.Running;
        var count = MachineExtrasLayout.Pick(extraKinds, machineIndex == 1, skippable, offered);
        var style = MachineExtrasLayout.LabelStyle;
        var iconHeight = Typography.LineHeight(style) * MachineExtrasLayout.IconShare;
        var bet = Composer.Amount;
        for (var index = 0; index < count; index++)
        {
            var kind = extraKinds[index];
            var label = ExtraLabel(kind, bet);
            extraLabels[index] = label;
            extraWidths[index] = MachineExtrasLayout.PillWidth(Typography.Measure(label, style).X,
                MachineExtrasLayout.HasIcon(kind) ? iconHeight : 0f, scale);
        }

        var widths = extraWidths.AsSpan(0, count);
        var panning = !MachineExtrasLayout.Fits(row, widths, scale);
        var offset = 0f;
        if (panning)
        {
            extrasRail.Begin(row, MachineExtrasLayout.ContentWidth(widths, scale));
            offset = extrasRail.Offset;
        }
        else
        {
            extrasRail.Reset();
        }

        MachineExtrasLayout.Place(row, widths, offset, scale, extraRects);
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < count; index++)
        {
            var kind = extraKinds[index];
            var rect = extraRects[index];
            if (rect.Max.X < row.Min.X || rect.Min.X > row.Max.X)
            {
                continue;
            }

            var enabled = ExtraEnabled(kind, changeable, blocked, bet, sitting.Stack);
            var hovered = enabled && (panning ? extrasRail.Hover(rect.Min, rect.Max) : UiInteract.Hover(rect.Min, rect.Max));
            PaintExtra(drawList, ui, rect, kind, extraLabels[index], enabled, hovered, iconHeight, style, scale);
            if (kind == MachineExtra.Ante && enabled)
            {
                HoverTooltip.Show(ExtraIds[(int)kind], rect, Loc.T(L.Machines.AnteHint), HoverLabelSide.Above);
            }

            var tapped = enabled && (panning
                ? extrasRail.Tapped(rect.Min, rect.Max, hovered)
                : UiInteract.Click(rect.Min, rect.Max, hovered));
            if (tapped)
            {
                Activate(kind, bet);
            }
        }

        if (panning)
        {
            extrasRail.End();
        }
    }

    private string ExtraLabel(MachineExtra kind, long bet) => kind switch
    {
        MachineExtra.Ante => Loc.T(L.Machines.AnteBoost),
        MachineExtra.Buy => buyLabel.Get(L.Machines.BuyBonus, SlotsRules.CostOf(SlotsRules.BuyMode, bet)),
        MachineExtra.Skip => Loc.T(L.Machines.Skip),
        MachineExtra.Gamble => Loc.T(L.Machines.Gamble),
        _ => Loc.T(L.Machines.Turbo),
    };

    private static bool ExtraEnabled(MachineExtra kind, bool changeable, bool blocked, long bet, long stack) => kind switch
    {
        MachineExtra.Ante => changeable,
        MachineExtra.Buy => changeable && SlotsRules.CostOf(SlotsRules.BuyMode, bet) <= stack,
        _ => !blocked,
    };

    private static FontAwesomeIcon IconOf(MachineExtra kind) => kind switch
    {
        MachineExtra.Skip => FontAwesomeIcon.Forward,
        MachineExtra.Gamble => FontAwesomeIcon.Dice,
        _ => FontAwesomeIcon.Bolt,
    };

    private bool ExtraOn(MachineExtra kind) => kind switch
    {
        MachineExtra.Turbo => turbo,
        MachineExtra.Ante => ante,
        _ => false,
    };

    private void PaintExtra(ImDrawListPtr drawList, AppSkin ui, Rect rect, MachineExtra kind, string label,
        bool enabled, bool hovered, float iconHeight, in TextStyle style, float scale)
    {
        var on = ExtraOn(kind);
        var face = Button.Surface(drawList, rect, ui.Ink, on ? ButtonStyle.Tinted : ButtonStyle.Gray, ButtonRole.Normal,
            enabled, hovered, ImGui.GetID(ExtraIds[(int)kind]));
        var shape = face.Face;
        var alpha = face.LabelInk.W;
        if (on)
        {
            Squircle.Stroke(drawList, shape.Min, shape.Max, shape.Height * 0.5f,
                ImGui.GetColorU32(ui.Ink.Accent with { W = alpha }), ExtraRingWidth * scale);
        }

        var labelInk = face.LabelInk;
        var iconInk = labelInk;
        if (kind == MachineExtra.Buy)
        {
            Squircle.Stroke(drawList, shape.Min, shape.Max, shape.Height * 0.5f,
                ImGui.GetColorU32(CasinoColors.Money with { W = BuyRimAlpha * alpha }), ExtraRingWidth * scale);
            labelInk = CasinoColors.Money with { W = alpha };
            iconInk = labelInk;
        }
        else if (kind == MachineExtra.Gamble)
        {
            iconInk = ui.Ink.Accent with { W = alpha };
        }

        var labelSize = Typography.Measure(label, style);
        var icon = MachineExtrasLayout.HasIcon(kind) ? iconHeight + MachineExtrasLayout.IconGap * scale : 0f;
        var left = shape.Center.X - (icon + labelSize.X) * 0.5f;
        if (icon > 0f)
        {
            ProgressRing.CenterIcon(drawList, new Vector2(left + iconHeight * 0.5f, shape.Center.Y), IconOf(kind),
                iconInk, iconHeight);
        }

        Typography.Draw(drawList, new Vector2(left + icon, shape.Center.Y - labelSize.Y * 0.5f), label, labelInk,
            style);
    }

    private void Activate(MachineExtra kind, long bet)
    {
        switch (kind)
        {
            case MachineExtra.Turbo:
                turbo = !turbo;
                UiFeedback.Play(turbo ? UiSound.ToggleOn : UiSound.ToggleOff);
                return;
            case MachineExtra.Ante:
                ante = !ante;
                UiFeedback.Play(ante ? UiSound.ToggleOn : UiSound.ToggleOff);
                return;
            case MachineExtra.Buy:
                AskBuy(bet);
                return;
            case MachineExtra.Skip:
                playback.Skip();
                return;
            case MachineExtra.Gamble:
                gamble.Choose();
                CasinoSfx.Play(UiSound.CardSnap);
                return;
        }
    }
}
