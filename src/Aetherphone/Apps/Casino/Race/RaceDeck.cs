using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
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
    string Primary,
    bool Ready,
    bool Enabled,
    string Ride,
    bool Repeat);

internal sealed class RaceDeck
{
    private const float FlashSeconds = 0.5f;
    private const string PrimaryId = "casino.race.bet";

    private long amount = RaceRules.MinBet;
    private float flash;
    private ChipsPending pending;

    public long Amount => amount;

    public int PickedKind { get; private set; }

    public void Reset(long value)
    {
        amount = value;
        flash = 0f;
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

        var rideWidth = model.Ride.Length == 0
            ? 0f
            : DeckActions.PillWidth(Typography.Measure(model.Ride, Button.LabelStyle(RaceDeckLayout.ActionHeight * scale)).X,
                RaceDeckLayout.ActionHeight * scale, scale);
        var layout = RaceDeckLayout.Compute(deck, rideWidth, scale);
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
        if (layout.HasRide && Button.Draw(layout.Ride, model.Ride, ui.Ink, ButtonStyle.Gray,
                enabled: model.Enabled && model.Ready))
        {
            result = RaceDeckAction.Ride;
        }

        var ready = model.Enabled && model.Ready && amount >= RaceRules.MinBet;
        var canBet = ready && amount <= model.Stack;
        var pressed = chips is null || !model.Ready
            ? Button.Draw(layout.Primary, model.Primary, ui.Ink, ButtonStyle.Prominent, enabled: canBet, id: PrimaryId)
            : chips.Primary(layout.Primary, model.Primary, amount, model.Stack, ready, ui.Ink, PrimaryId, ref pending)
              == ChipsPress.Place;
        if (pressed || (model.Repeat && canBet))
        {
            result = RaceDeckAction.Bet;
        }

        return result;
    }

    private void DrawAmount(ImDrawListPtr drawList, AppSkin ui, in RaceDeckLayout layout, in RaceDeckModel model,
        float scale)
    {
        var field = layout.Field;
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        if (flash > 0f)
        {
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, flash / FlashSeconds)),
                Metrics.Stroke.Ring * scale);
        }

        var style = TextStyles.Headline;
        var text = RaceAmounts.Text(amount);
        var size = RaceAmounts.Measure(text, style);
        var inset = capsule.Height * 0.4f;
        RaceAmounts.Draw(drawList, new Vector2(capsule.Min.X + inset, capsule.Center.Y - size.Y * 0.5f), text,
            model.Enabled ? CasinoColors.Money : ui.MutedInk, style, model.Enabled ? 1f : 0.5f);
        var enabled = model.Enabled;
        if (Button.Draw(layout.Half, Loc.T(L.Casino.BetHalf), ui.Ink, ButtonStyle.Gray, enabled: enabled))
        {
            amount = CasinoLadder.Half(amount, RaceRules.MinBet, model.MaximumBet, long.MaxValue);
            UiFeedback.Play(UiSound.ChipSlide);
        }

        if (Button.Draw(layout.Double, CasinoMultiples.Label(200), ui.Ink, ButtonStyle.Gray, enabled: enabled))
        {
            amount = CasinoLadder.Double(amount, RaceRules.MinBet, model.MaximumBet, long.MaxValue);
            UiFeedback.Play(UiSound.ChipSlide);
        }

        if (!Button.Draw(layout.Max, Loc.T(L.Casino.BetMax), ui.Ink, ButtonStyle.Gray, enabled: enabled))
        {
            return;
        }

        amount = CasinoLadder.Top(RaceRules.MinBet, model.MaximumBet, long.MaxValue);
        UiFeedback.Play(UiSound.ChipSlide);
    }
}
