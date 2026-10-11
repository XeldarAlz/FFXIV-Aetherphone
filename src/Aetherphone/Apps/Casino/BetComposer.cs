using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal enum BetComposerAction : byte
{
    None,
    Confirm,
    StartAuto,
    StopAuto,
}

internal readonly record struct BetComposerModel(
    long MinimumBet,
    long MaximumBet,
    long Stack,
    LocString Action,
    bool Enabled,
    bool AutoAvailable = false,
    bool FixedAmount = false,
    bool Knob = false,
    bool Repeat = false,
    bool Busy = false,
    long Cost = 0,
    bool Rack = false);

internal sealed class BetComposer
{
    public const float Pad = BetDeckLayout.Pad;
    public const float Gap = BetDeckLayout.Gap;
    public const float AmountHeight = BetDeckLayout.AmountHeight;
    public const float ActionHeight = BetDeckLayout.ActionHeight;
    public const float KnobHeight = BetDeckLayout.KnobHeight;

    private const int BufferLength = 16;
    private const float FlashSeconds = 0.5f;
    private const string ActionId = "casino.deck.action";
    private const string AutoId = "casino.deck.auto";
    private const double AutoSweepMilliseconds = 1100.0;
    private const float AutoSweepArc = MathF.PI * 0.9f;
    private const float AutoSweepRadius = 0.78f;
    private const float AutoSweepThickness = 2f;

    private readonly string fieldId;
    private readonly AutoBetSheet autoSheet;

    private string buffer = string.Empty;
    private bool editing;
    private bool focusPending;
    private long amount;
    private float flash;
    private bool armed;
    private bool startQueued;
    private bool pendingAuto;
    private bool startReady;
    private string actionLabel = string.Empty;
    private long actionAmount = -1;
    private string actionKey = string.Empty;
    private int actionRemaining = int.MinValue;
    private LanguageInfo? actionLanguage;
    private AutoStop announced;
    private ChipsPending pending;

    public BetComposer(string fieldId)
    {
        this.fieldId = fieldId;
        autoSheet = new AutoBetSheet(fieldId + ".auto", Auto);
    }

    public AutoBetPlan Auto { get; } = new();

    public long Amount => amount;

    public bool AutoArmed => armed;

    public Rect KnobRect { get; private set; }

    public bool SheetOpen => autoSheet.IsOpen;

    public static float DeckHeightFor(bool knob, bool fixedAmount) =>
        BetDeckLayout.DeckHeightFor(knob, fixedAmount);

    public void Reset(long value)
    {
        amount = value;
        editing = false;
        flash = 0f;
    }

    public void Prefill(long value)
    {
        if (amount > 0)
        {
            return;
        }

        amount = value;
    }

    public void Gate()
    {
        autoSheet.Gate();
    }

    public void DrawOverlay(Rect screen, AppSkin ui, bool bonusAvailable)
    {
        autoSheet.Draw(screen, ui, bonusAvailable, startReady && !Auto.Running);
        if (autoSheet.TakeStart())
        {
            startQueued = true;
        }
    }

    public BetComposerAction Draw(CasinoStage stage, AppSkin ui, Rect deck, in BetComposerModel model,
        float deltaSeconds)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (flash > 0f)
        {
            flash -= deltaSeconds;
        }

        var bound = Bound(model);
        var snapped = CasinoLadder.Clamp(amount, model.MinimumBet, model.MaximumBet, bound);
        if (!model.FixedAmount && snapped != amount && !editing)
        {
            if (amount > 0)
            {
                flash = FlashSeconds;
            }

            amount = snapped;
        }

        var running = Auto.Running;
        var controlsEnabled = model.Enabled && !running && !model.Busy;
        var quick = model.FixedAmount
            ? default
            : DeckAmountRow.Measure(BetDeckLayout.AmountHeight * scale);
        var autoLabel = model.AutoAvailable
            ? Typography.Measure(Loc.T(L.Strip.Auto), Button.LabelStyle(BetDeckLayout.ActionHeight * scale)).X
            : 0f;
        var layout = BetDeckLayout.Compute(deck, model.Knob, model.FixedAmount, model.AutoAvailable, quick,
            autoLabel, scale);
        KnobRect = layout.Knob;
        if (layout.HasAmount)
        {
            DrawAmountRow(drawList, ui, layout, model, controlsEnabled);
        }

        var result = DrawActionRow(drawList, ui, layout, model, stage.Chips);
        AnnounceAutoStop();
        return result;
    }

    public bool Covers(CasinoStage stage, long need, long stack)
    {
        if (need <= stack)
        {
            return true;
        }

        var desk = stage.Chips;
        if (desk is not null && (desk.Buying || desk.TryAutoBuy(need, stack)))
        {
            return false;
        }

        Auto.Stop(AutoStop.Chips);
        return false;
    }

    private static long Bound(in BetComposerModel model) => model.Rack ? model.Stack : long.MaxValue;

    private void DrawAmountRow(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout,
        in BetComposerModel model, bool enabled)
    {
        DrawField(drawList, ui, layout.Field, model, enabled, UiScale.Current);
        var tapped = layout.Amount.DrawChips(ui.Ink, enabled);
        if (tapped == QuickChip.None)
        {
            return;
        }

        amount = tapped switch
        {
            QuickChip.Half => CasinoLadder.Half(amount, model.MinimumBet, model.MaximumBet, Bound(model)),
            QuickChip.Double => CasinoLadder.Double(amount, model.MinimumBet, model.MaximumBet, Bound(model)),
            _ => CasinoLadder.Top(model.MinimumBet, model.MaximumBet, Bound(model)),
        };
        UiFeedback.Play(UiSound.ChipSlide);
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin ui, Rect field, in BetComposerModel model, bool enabled,
        float scale)
    {
        var capsule = DeckAmountRow.FieldSurface(drawList, field, ui.Ink);
        if (flash > 0f)
        {
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, flash / FlashSeconds)),
                Metrics.Stroke.Ring * scale);
        }

        var inset = DeckAmountRow.FieldInset(scale);
        if (editing && enabled)
        {
            DrawEditor(field, capsule, inset, model);
            return;
        }

        editing = false;
        var text = NumberText.Compact(amount);
        var style = DeckAmountRow.AmountStyle(text, capsule.Width - inset * 2f);
        var size = CurrencyGlyph.MeasureAmount(text, style);
        var origin = new Vector2(capsule.Min.X + inset, capsule.Center.Y - size.Y * 0.5f);
        CurrencyGlyph.DrawAmount(drawList, origin, text, CurrencyKind.Chips,
            enabled ? CasinoColors.Money : ui.MutedInk, style, enabled ? 1f : 0.5f);
        var hovered = enabled && UiInteract.Hover(field.Min, field.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        if (!UiInteract.Click(field.Min, field.Max, hovered))
        {
            return;
        }

        editing = true;
        focusPending = true;
        buffer = amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private void DrawEditor(Rect field, Rect capsule, float inset, in BetComposerModel model)
    {
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        if (focusPending)
        {
            ImGui.SetKeyboardFocusHere();
        }

        bool committed;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, CasinoColors.InkTitle))
        {
            committed = ImGui.InputText(fieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll |
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var active = ImGui.IsItemActive();
        if (focusPending)
        {
            focusPending = false;
            return;
        }

        if (!committed && active)
        {
            return;
        }

        editing = false;
        var typed = long.TryParse(buffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : amount;
        var clamped = CasinoLadder.Clamp(typed, model.MinimumBet, model.MaximumBet, Bound(model));
        if (clamped != typed)
        {
            flash = FlashSeconds;
        }

        amount = clamped;
    }

    private BetComposerAction DrawActionRow(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout,
        in BetComposerModel model, ChipsDesk? desk)
    {
        var running = Auto.Running;
        if (layout.HasAuto && DrawAutoPill(drawList, ui, layout, running))
        {
            armed = true;
            autoSheet.Open();
        }

        var actionRect = layout.Action;
        var need = model.Cost > 0 ? model.Cost : amount;
        var ready = model.Enabled && !model.Busy && amount > 0 && amount >= model.MinimumBet;
        startReady = ready && model.AutoAvailable;
        if (running)
        {
            startQueued = false;
            pendingAuto = false;
            var stopLabel = Auto.Remaining < 0
                ? Loc.T(L.Strip.AutoStop)
                : ActionLabel(L.Strip.AutoStopLeft, amount, Auto.Remaining);
            return Button.Draw(actionRect, stopLabel, ui.Ink, ButtonStyle.Tinted, id: ActionId)
                ? StopAuto()
                : BetComposerAction.None;
        }

        var queued = startQueued ? StartQueued(model, desk, need, ready) : BetComposerAction.None;
        startQueued = false;
        var canBet = ready && need <= model.Stack;
        var label = ActionLabel(model.Action, need, int.MinValue);
        var pressed = desk is null || model.Rack
            ? Button.Draw(actionRect, label, ui.Ink, ButtonStyle.Prominent, enabled: canBet, id: ActionId)
            : desk.Primary(actionRect, label, need, model.Stack, ready, ui.Ink, ActionId, ref pending)
              == ChipsPress.Place;
        if (queued != BetComposerAction.None)
        {
            return queued;
        }

        if (pressed && pendingAuto)
        {
            pendingAuto = false;
            Auto.Start(amount);
            return BetComposerAction.StartAuto;
        }

        if (!pending.Active)
        {
            pendingAuto = false;
        }

        if (!pressed && !(model.Repeat && canBet))
        {
            return BetComposerAction.None;
        }

        armed = false;
        return BetComposerAction.Confirm;
    }

    private BetComposerAction StartQueued(in BetComposerModel model, ChipsDesk? desk, long need, bool ready)
    {
        var route = AutoBetPlan.RouteStart(ready && model.AutoAvailable, need, model.Stack, desk is not null,
            model.Rack, desk is not null && desk.CanAutoCover(need, model.Stack));
        if (route == AutoStartRoute.Start)
        {
            Auto.Start(amount);
            return BetComposerAction.StartAuto;
        }

        if (route == AutoStartRoute.Ignore || desk is null)
        {
            return BetComposerAction.None;
        }

        if (route == AutoStartRoute.TopUp && desk.TryAutoBuy(need, model.Stack))
        {
            pending = new ChipsPending(need, Environment.TickCount64);
            pendingAuto = true;
            return BetComposerAction.None;
        }

        desk.Request(need, ChipsNeedKind.Bet);
        return BetComposerAction.None;
    }

    private bool DrawAutoPill(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout, bool running)
    {
        var scale = UiScale.Current;
        var label = Loc.T(L.Strip.Auto);
        var style = running || armed ? ButtonStyle.Tinted : ButtonStyle.Gray;
        var pill = DeckActions.DrawIconPill(layout.Auto, PhoneIcons.Repeat, layout.AutoCompact ? string.Empty : label,
            ui.Ink, style, true, !running, AutoId, layout.AutoCompact ? label : string.Empty);
        if (running)
        {
            ProgressRing.Sweep(pill.IconCenter, DeckActions.IconSize * AutoSweepRadius * scale,
                AutoSweepThickness * scale, pill.Ink, AutoSweepMilliseconds, AutoSweepArc, 1f, drawList);
        }

        return pill.Pressed;
    }

    private BetComposerAction StopAuto()
    {
        Auto.Stop(AutoStop.Manual);
        Auto.Acknowledge();
        return BetComposerAction.StopAuto;
    }

    private void AnnounceAutoStop()
    {
        var stopped = Auto.Stopped;
        if (stopped == announced)
        {
            return;
        }

        announced = stopped;
        var message = StopMessage(stopped);
        if (message.Key is null)
        {
            return;
        }

        ShellToast.Show(Loc.T(message));
        Auto.Acknowledge();
        announced = AutoStop.None;
    }

    public static LocString StopMessage(AutoStop stopped) => stopped switch
    {
        AutoStop.Count => L.Strip.AutoStoppedCount,
        AutoStop.Profit => L.Strip.AutoStoppedProfit,
        AutoStop.Loss => L.Strip.AutoStoppedLoss,
        AutoStop.Bonus => L.Strip.AutoStoppedBonus,
        AutoStop.Chips => L.Strip.AutoStoppedChips,
        AutoStop.Refused => L.Strip.AutoStoppedRefused,
        _ => default,
    };

    private string ActionLabel(LocString template, long value, int remaining)
    {
        if (template.Key is null)
        {
            return string.Empty;
        }

        if (value == actionAmount && remaining == actionRemaining && ReferenceEquals(actionLanguage, Loc.Current)
            && string.Equals(actionKey, template.Key, StringComparison.Ordinal))
        {
            return actionLabel;
        }

        actionAmount = value;
        actionRemaining = remaining;
        actionLanguage = Loc.Current;
        actionKey = template.Key;
        actionLabel = remaining == int.MinValue
            ? Loc.T(template, NumberText.Compact(value))
            : Loc.T(template, Games.Framework.GameNumber.Label(remaining));
        return actionLabel;
    }
}
