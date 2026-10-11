using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Race;

internal enum RaceDeckAction : byte
{
    None,
    Kind,
    Bet,
    Ride,
}

internal readonly record struct RaceDeckModel(
    long MaximumBet,
    long Stack,
    int Kind,
    string Caption,
    bool Ready,
    bool Enabled,
    string Ride,
    bool Repeat,
    long RideAmount = 0);

internal sealed class RaceDeck
{
    private const float FlashSeconds = 0.5f;
    private const string PrimaryId = "casino.race.bet";
    private const string CaptionId = "casino.race.caption";
    private const string RideId = "casino.race.ride";

    private long amount = RaceRules.MinBet;
    private float flash;
    private ChipsPending pending;
    private string betLabel = string.Empty;
    private long betAmount = -1;
    private LanguageInfo? betLanguage;
    private string caption = string.Empty;
    private int captionKey = int.MinValue;
    private long captionPay = -1;
    private CasinoRaceRunnerDto[]? captionRunners;
    private LanguageInfo? captionLanguage;

    public long Amount => amount;

    public int PickedKind { get; private set; }

    public void Reset(long value)
    {
        amount = value;
        flash = 0f;
    }

    public string Caption(int kind, int first, int second, CasinoRaceRunnerDto[] runners)
    {
        if (!RaceRules.IsRunner(first) || first >= runners.Length)
        {
            return string.Empty;
        }

        var key = RaceRules.TicketKey(kind, first, second);
        var pay = RaceTexts.PayOf(kind, first, second, runners);
        if (key == captionKey && pay == captionPay && ReferenceEquals(runners, captionRunners)
            && ReferenceEquals(captionLanguage, Loc.Current))
        {
            return caption;
        }

        captionKey = key;
        captionPay = pay;
        captionRunners = runners;
        captionLanguage = Loc.Current;
        var target = RaceRules.IsPair(kind)
            ? string.Concat(GameNumber.Label(first + 1), "-", GameNumber.Label(second + 1))
            : runners[first].Name;
        caption = Loc.T(L.Race.BetCaption, target, CasinoMultiples.Label((int)Math.Min(int.MaxValue, pay)));
        return caption;
    }

    public RaceDeckAction Draw(ChipsDesk? chips, AppSkin ui, Rect deck, string[] kindLabels, in RaceDeckModel model,
        float deltaSeconds)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        flash = MathF.Max(0f, flash - deltaSeconds);
        var snapped = CasinoLadder.Clamp(amount, RaceRules.MinBet, model.MaximumBet, long.MaxValue);
        if (snapped != amount)
        {
            flash = amount > 0 ? FlashSeconds : 0f;
            amount = snapped;
        }

        var actionHeight = RaceDeckLayout.ActionHeight * scale;
        var rideStyle = Button.LabelStyle(actionHeight);
        var rideAmount = model.RideAmount > 0 ? RaceAmounts.Text(model.RideAmount) : string.Empty;
        var rideWidth = model.Ride.Length == 0
            ? 0f
            : DeckActions.PillWidth(Typography.Measure(model.Ride, rideStyle).X, scale);
        var rideCompactWidth = model.Ride.Length == 0
            ? 0f
            : DeckActions.IconPillWidth(rideAmount.Length > 0 ? Typography.Measure(rideAmount, rideStyle).X : 0f,
                actionHeight, scale);
        var layout = RaceDeckLayout.Compute(deck, DeckAmountRow.Measure(RaceDeckLayout.AmountHeight * scale),
            rideWidth, rideCompactWidth, scale);
        var result = RaceDeckAction.None;
        PickedKind = model.Kind;
        var picked = SegmentStrip.Draw("##raceKind", layout.Segment, kindLabels, model.Kind,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle,
            RaceDeckLayout.SegmentHeight);
        if (picked != model.Kind && model.Enabled)
        {
            PickedKind = picked;
            result = RaceDeckAction.Kind;
        }

        DrawAmount(drawList, ui, layout, model, scale);
        DrawCaption(drawList, layout.Caption, model.Caption, model.Ready);
        if (layout.HasRide && DrawRide(ui, layout, model, rideAmount))
        {
            result = RaceDeckAction.Ride;
        }

        var ready = model.Enabled && model.Ready && amount >= RaceRules.MinBet;
        var canBet = ready && amount <= model.Stack;
        var label = BetLabel();
        var pressed = chips is null || !model.Ready
            ? Button.Draw(layout.Primary, label, ui.Ink, ButtonStyle.Prominent, enabled: canBet, id: PrimaryId)
            : chips.Primary(layout.Primary, label, amount, model.Stack, ready, ui.Ink, PrimaryId, ref pending)
              == ChipsPress.Place;
        if (pressed || (model.Repeat && canBet))
        {
            result = RaceDeckAction.Bet;
        }

        return result;
    }

    private static bool DrawRide(AppSkin ui, in RaceDeckLayout layout, in RaceDeckModel model, string rideAmount)
    {
        var enabled = model.Enabled && model.Ready;
        if (!layout.RideCompact)
        {
            return DeckActions.DrawPill(layout.Ride, model.Ride, ui.Ink, ButtonStyle.Gray, enabled, RideId);
        }

        return DeckActions.DrawIconPill(layout.Ride, PhoneIcons.Repeat, rideAmount, ui.Ink, ButtonStyle.Gray, enabled,
            true, RideId, model.Ride).Pressed;
    }

    private string BetLabel()
    {
        if (amount == betAmount && ReferenceEquals(betLanguage, Loc.Current))
        {
            return betLabel;
        }

        betAmount = amount;
        betLanguage = Loc.Current;
        betLabel = Loc.T(L.Strip.BetFor, RaceAmounts.Text(amount));
        return betLabel;
    }

    private static void DrawCaption(ImDrawListPtr drawList, Rect rect, string text, bool strong)
    {
        if (text.Length == 0 || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var preferred = strong ? TextStyles.SubheadlineEmphasized : TextStyles.Subheadline;
        var fitScale = Typography.FitScale(text, rect.Width, preferred.Scale, TextStyles.Footnote.Scale,
            preferred.Weight);
        var style = new TextStyle(fitScale, preferred.Weight);
        var size = Typography.Measure(text, style);
        var ink = strong ? CasinoColors.InkTitle : CasinoColors.InkBody;
        if (size.X <= rect.Width)
        {
            Typography.Draw(drawList, new Vector2(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f), text,
                ink, style);
            return;
        }

        Marquee.DrawCenteredAuto(drawList, CaptionId, text, rect.Center.X, rect.Center.Y - size.Y * 0.5f, rect.Width,
            style, ink);
    }

    private void DrawAmount(ImDrawListPtr drawList, AppSkin ui, in RaceDeckLayout layout, in RaceDeckModel model,
        float scale)
    {
        var field = layout.Field;
        var capsule = DeckAmountRow.FieldSurface(drawList, field, ui.Ink);
        if (flash > 0f)
        {
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, flash / FlashSeconds)),
                Metrics.Stroke.Ring * scale);
        }

        var text = RaceAmounts.Text(amount);
        var inset = DeckAmountRow.FieldInset(scale);
        var style = DeckAmountRow.AmountStyle(text, capsule.Width - inset * 2f);
        var size = RaceAmounts.Measure(text, style);
        RaceAmounts.Draw(drawList, new Vector2(capsule.Min.X + inset, capsule.Center.Y - size.Y * 0.5f), text,
            model.Enabled ? CasinoColors.Money : ui.MutedInk, style, model.Enabled ? 1f : 0.5f);
        var tapped = layout.Amount.DrawChips(ui.Ink, model.Enabled);
        if (tapped == QuickChip.None)
        {
            return;
        }

        amount = tapped switch
        {
            QuickChip.Half => CasinoLadder.Half(amount, RaceRules.MinBet, model.MaximumBet, long.MaxValue),
            QuickChip.Double => CasinoLadder.Double(amount, RaceRules.MinBet, model.MaximumBet, long.MaxValue),
            _ => CasinoLadder.Top(RaceRules.MinBet, model.MaximumBet, long.MaxValue),
        };
        UiFeedback.Play(UiSound.ChipSlide);
    }
}
