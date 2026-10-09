using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HoldemComposerAction : byte
{
    None,
    Confirm,
    Cancel,
}

internal readonly record struct HoldemRaiseModel(
    int Actions,
    long StreetBet,
    long ToCall,
    long MinRaiseTo,
    long MaxRaiseTo,
    long PotTotal,
    long BigBlind,
    bool Enabled);

internal sealed class HoldemRaiseComposer
{
    public const float Pad = 12f;
    public const float Gap = 8f;
    public const float RowHeight = Metrics.Size.Pill;
    public const float StepperRadius = Metrics.Size.Pill * 0.5f;
    public const float BackShare = 0.30f;

    private const int QuickCount = 5;
    private const string SliderId = "holdem.raise.slider";

    private static readonly LocString[] QuickLabels =
    {
        L.Holdem.QuickMin,
        L.Holdem.QuickHalfPot,
        L.Holdem.QuickThreeQuarterPot,
        L.Holdem.QuickPot,
        L.Holdem.QuickAllIn,
    };

    private static readonly string[] QuickIds =
    {
        "holdem.quick.min", "holdem.quick.half", "holdem.quick.threeQuarter", "holdem.quick.pot", "holdem.quick.allin",
    };

    private readonly CasinoTextCache texts;

    private long amount;
    private bool primed;

    public HoldemRaiseComposer(CasinoTextCache texts)
    {
        this.texts = texts;
    }

    public long Amount => amount;

    public static float DeckHeight => Pad * 2f + RowHeight * 2f + Gap * 2f + Button.LargeHeight;

    public void Open(in HoldemRaiseModel model)
    {
        amount = Minimum(model);
        primed = true;
    }

    public void Close()
    {
        primed = false;
    }

    public int ActionFor(in HoldemRaiseModel model)
    {
        if (amount >= model.MaxRaiseTo && HoldemActions.Allows(model.Actions, HoldemActions.AllIn))
        {
            return HoldemActions.AllIn;
        }

        return HoldemActions.Allows(model.Actions, HoldemActions.Bet) ? HoldemActions.Bet : HoldemActions.Raise;
    }

    public static long Minimum(in HoldemRaiseModel model) =>
        model.MinRaiseTo > model.MaxRaiseTo ? model.MaxRaiseTo : model.MinRaiseTo;

    public HoldemComposerAction Draw(AppSkin ui, Rect deck, in HoldemRaiseModel model)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var minimum = Minimum(model);
        if (!primed)
        {
            Open(model);
        }

        amount = HoldemRules.SnapRaise(amount, model.BigBlind, minimum, model.MaxRaiseTo);
        var left = deck.Min.X + Pad * scale;
        var right = deck.Max.X - Pad * scale;
        var top = deck.Min.Y + Pad * scale;
        var row = RowHeight * scale;
        var gap = Gap * scale;
        DrawQuickRow(ui, new Rect(new Vector2(left, top), new Vector2(right, top + row)), model, minimum, scale);
        top += row + gap;
        DrawSliderRow(ui, drawList, new Rect(new Vector2(left, top), new Vector2(right, top + row)), model, minimum,
            scale);
        top += row + gap;
        var actionRect = new Rect(new Vector2(left, top), new Vector2(right, top + Button.LargeHeight * scale));
        var backWidth = actionRect.Width * BackShare;
        var backRect = new Rect(actionRect.Min, new Vector2(actionRect.Min.X + backWidth, actionRect.Max.Y));
        if (Button.Draw(drawList, backRect, Loc.T(L.Holdem.Back), ui.Ink, ButtonStyle.Gray, id: "holdem.raise.back"))
        {
            primed = false;
            return HoldemComposerAction.Cancel;
        }

        var confirmRect = new Rect(new Vector2(backRect.Max.X + gap, actionRect.Min.Y), actionRect.Max);
        var label = ConfirmLabel(model);
        var enabled = model.Enabled && amount > 0;
        if (!Button.Draw(drawList, confirmRect, Typography.FitText(label, confirmRect.Width - confirmRect.Height,
                Button.LabelStyle(confirmRect.Height)), ui.Ink, ButtonStyle.Prominent, enabled: enabled,
                id: "holdem.raise.confirm"))
        {
            return HoldemComposerAction.None;
        }

        primed = false;
        return HoldemComposerAction.Confirm;
    }

    private string ConfirmLabel(in HoldemRaiseModel model)
    {
        return ActionFor(model) switch
        {
            HoldemActions.AllIn => texts.Number(L.Holdem.AllInFor, amount),
            HoldemActions.Bet => texts.Number(L.Holdem.BetFor, amount),
            _ => texts.Number(L.Holdem.RaiseTo, amount),
        };
    }

    private void DrawQuickRow(AppSkin ui, Rect row, in HoldemRaiseModel model, long minimum, float scale)
    {
        var gap = Gap * scale * 0.5f;
        var width = (row.Width - gap * (QuickCount - 1)) / QuickCount;
        var drawList = ImGui.GetWindowDrawList();
        for (var index = 0; index < QuickCount; index++)
        {
            var min = new Vector2(row.Min.X + index * (width + gap), row.Min.Y);
            var rect = new Rect(min, new Vector2(min.X + width, row.Max.Y));
            var target = QuickAmount(index, model, minimum);
            var label = Typography.FitText(Loc.T(QuickLabels[index]), width - rect.Height * 0.5f,
                Button.LabelStyle(rect.Height));
            var style = target == amount ? ButtonStyle.Tinted : ButtonStyle.Gray;
            if (Button.Draw(drawList, rect, label, ui.Ink, style, enabled: model.Enabled && target > 0,
                    id: QuickIds[index]))
            {
                amount = target;
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }
    }

    internal static long QuickAmount(int index, in HoldemRaiseModel model, long minimum)
    {
        return index switch
        {
            0 => minimum,
            1 => HoldemRules.PotFraction(1, 2, model.StreetBet, model.ToCall, model.PotTotal, model.BigBlind, minimum,
                model.MaxRaiseTo),
            2 => HoldemRules.PotFraction(3, 4, model.StreetBet, model.ToCall, model.PotTotal, model.BigBlind, minimum,
                model.MaxRaiseTo),
            3 => HoldemRules.PotFraction(1, 1, model.StreetBet, model.ToCall, model.PotTotal, model.BigBlind, minimum,
                model.MaxRaiseTo),
            _ => model.MaxRaiseTo,
        };
    }

    private void DrawSliderRow(AppSkin ui, ImDrawListPtr drawList, Rect row, in HoldemRaiseModel model, long minimum,
        float scale)
    {
        var radius = StepperRadius * scale;
        var downCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        var upCenter = new Vector2(row.Max.X - radius, row.Center.Y);
        var enabled = model.Enabled && model.MaxRaiseTo > minimum;
        if (RoundButton.Icon(drawList, downCenter, radius, IconGlyph.Of(FontAwesomeIcon.Minus), ui.Ink,
                ButtonStyle.Gray, Loc.T(L.Holdem.StepDown), HoverLabelSide.Above, enabled))
        {
            amount = HoldemRules.Step(amount, -1, model.BigBlind, minimum, model.MaxRaiseTo);
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        if (RoundButton.Icon(drawList, upCenter, radius, IconGlyph.Of(FontAwesomeIcon.Plus), ui.Ink,
                ButtonStyle.Gray, Loc.T(L.Holdem.StepUp), HoverLabelSide.Above, enabled))
        {
            amount = HoldemRules.Step(amount, 1, model.BigBlind, minimum, model.MaxRaiseTo);
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var inset = radius * 2f + Gap * scale;
        if (!enabled)
        {
            return;
        }

        var result = Slider.Draw(SliderId, row, HoldemRules.SliderFraction(amount, minimum, model.MaxRaiseTo),
            ui.Theme, inset, inset);
        if (result.Dragging || result.Released)
        {
            amount = HoldemRules.AmountAt(result.Value, model.BigBlind, minimum, model.MaxRaiseTo);
        }
    }
}
