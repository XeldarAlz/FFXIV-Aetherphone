using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
    bool Busy = false);

internal sealed class BetComposer
{
    public const float Pad = BetDeckLayout.Pad;
    public const float Gap = BetDeckLayout.Gap;
    public const float AmountHeight = BetDeckLayout.AmountHeight;
    public const float ActionHeight = BetDeckLayout.ActionHeight;
    public const float KnobHeight = BetDeckLayout.KnobHeight;

    private const int BufferLength = 16;
    private const float FlashSeconds = 0.5f;
    private const int ManualTab = 0;
    private const int AutoTab = 1;

    private readonly string fieldId;
    private readonly string modeId;
    private readonly string[] modeLabels = new string[2];
    private readonly AutoBetSheet autoSheet;

    private string buffer = string.Empty;
    private bool editing;
    private bool focusPending;
    private long amount;
    private float flash;
    private int tab;
    private string actionLabel = string.Empty;
    private long actionAmount = -1;
    private string actionKey = string.Empty;
    private int actionRemaining = int.MinValue;
    private LanguageInfo? actionLanguage;
    private AutoStop announced;

    public BetComposer(string fieldId)
    {
        this.fieldId = fieldId;
        modeId = fieldId + ".mode";
        autoSheet = new AutoBetSheet(fieldId + ".auto", Auto);
    }

    public AutoBetPlan Auto { get; } = new();

    public long Amount => amount;

    public bool AutoTabSelected => tab == AutoTab;

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
        autoSheet.Draw(screen, ui, bonusAvailable);
    }

    public BetComposerAction Draw(AppSkin ui, Rect deck, in BetComposerModel model, float deltaSeconds)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        if (flash > 0f)
        {
            flash -= deltaSeconds;
        }

        var snapped = CasinoLadder.Clamp(amount, model.MinimumBet, model.MaximumBet, model.Stack);
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
        var layout = BetDeckLayout.Compute(deck, model.Knob, model.FixedAmount, model.AutoAvailable, scale);
        KnobRect = layout.Knob;
        if (layout.HasAmount)
        {
            DrawAmountRow(drawList, ui, layout, model, controlsEnabled);
        }

        var result = DrawActionRow(drawList, ui, layout, model);
        AnnounceAutoStop();
        return result;
    }

    private void DrawAmountRow(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout,
        in BetComposerModel model, bool enabled)
    {
        DrawField(drawList, ui, layout.Field, model, enabled, UiScale.Current);
        if (Quick(ui, layout.Half, Loc.T(L.Casino.BetHalf), enabled))
        {
            amount = CasinoLadder.Half(amount, model.MinimumBet, model.MaximumBet, model.Stack);
            UiFeedback.Play(UiSound.ChipSlide);
        }

        if (Quick(ui, layout.Double, CasinoMultiples.Label(200), enabled))
        {
            amount = CasinoLadder.Double(amount, model.MinimumBet, model.MaximumBet, model.Stack);
            UiFeedback.Play(UiSound.ChipSlide);
        }

        if (Quick(ui, layout.Max, Loc.T(L.Casino.BetMax), enabled))
        {
            amount = CasinoLadder.Top(model.MinimumBet, model.MaximumBet, model.Stack);
            UiFeedback.Play(UiSound.ChipSlide);
        }
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin ui, Rect field, in BetComposerModel model, bool enabled,
        float scale)
    {
        SearchBar.Surface(drawList, field, ui.Ink);
        var capsule = SearchBar.Capsule(field);
        if (flash > 0f)
        {
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, flash / FlashSeconds)),
                Metrics.Stroke.Ring * scale);
        }

        var inset = capsule.Height * 0.4f;
        if (editing && enabled)
        {
            DrawEditor(field, capsule, inset, model);
            return;
        }

        editing = false;
        var text = NumberText.Compact(amount);
        var size = CurrencyGlyph.MeasureAmount(text, TextStyles.SubheadlineEmphasized);
        var origin = new Vector2(capsule.Min.X + inset, capsule.Center.Y - size.Y * 0.5f);
        CurrencyGlyph.DrawAmount(drawList, origin, text, CurrencyKind.Chips,
            enabled ? CasinoColors.Money : ui.MutedInk, TextStyles.SubheadlineEmphasized, enabled ? 1f : 0.5f);
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
        var clamped = CasinoLadder.Clamp(typed, model.MinimumBet, model.MaximumBet, model.Stack);
        if (clamped != typed)
        {
            flash = FlashSeconds;
        }

        amount = clamped;
    }

    private BetComposerAction DrawActionRow(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout,
        in BetComposerModel model)
    {
        if (layout.HasMode)
        {
            DrawModeCluster(drawList, ui, layout, model);
        }

        var actionRect = layout.Action;
        if (Auto.Running)
        {
            var stopLabel = Auto.Remaining < 0
                ? Loc.T(L.Strip.AutoStop)
                : ActionLabel(L.Strip.AutoStopLeft, amount, Auto.Remaining);
            return Button.Draw(actionRect, stopLabel, ui.Ink, ButtonStyle.Tinted)
                ? StopAuto()
                : BetComposerAction.None;
        }

        var canBet = model.Enabled && !model.Busy && amount > 0 && amount <= model.Stack
            && amount >= model.MinimumBet;
        var label = tab == AutoTab
            ? ActionLabel(L.Strip.AutoFor, amount, int.MinValue)
            : ActionLabel(model.Action, amount, int.MinValue);
        var pressed = Button.Draw(actionRect, label, ui.Ink, ButtonStyle.Prominent, enabled: canBet);
        if (!pressed && !(model.Repeat && canBet && tab == ManualTab))
        {
            return BetComposerAction.None;
        }

        if (tab == AutoTab)
        {
            Auto.Start(amount);
            return BetComposerAction.StartAuto;
        }

        return BetComposerAction.Confirm;
    }

    private void DrawModeCluster(ImDrawListPtr drawList, AppSkin ui, in BetDeckLayout layout,
        in BetComposerModel model)
    {
        modeLabels[ManualTab] = Loc.T(L.Strip.Manual);
        modeLabels[AutoTab] = Loc.T(L.Strip.Auto);
        var picked = SegmentStrip.Draw(modeId, layout.Mode, modeLabels, tab,
            Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary), ui.Accent, ui.MutedInk, CasinoColors.InkTitle,
            layout.Mode.Height / UiScale.Current);
        var changeable = !Auto.Running && model.Enabled;
        if (changeable)
        {
            tab = picked;
        }

        var gear = layout.Gear;
        if (gear.Width <= 0f)
        {
            return;
        }

        if (!RoundButton.Icon(drawList, gear.Center, gear.Height * 0.5f, IconGlyph.Of(FontAwesomeIcon.SlidersH),
                ui.Ink, ButtonStyle.Gray, enabled: !Auto.Running))
        {
            return;
        }

        if (changeable)
        {
            tab = AutoTab;
        }

        autoSheet.Open();
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

    private static bool Quick(AppSkin ui, Rect rect, string label, bool enabled) =>
        Button.Draw(rect, label, ui.Ink, ButtonStyle.Gray, enabled: enabled);
}
