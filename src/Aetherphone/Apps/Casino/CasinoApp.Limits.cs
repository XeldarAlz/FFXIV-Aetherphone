using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const long SavedFlashMilliseconds = 3000;
    private const float GaugePad = 18f;
    private const float GaugeRadius = 50f;
    private const float GaugeThickness = 10f;
    private const float GaugeTrackAlpha = 0.10f;
    private const float GaugeTextGap = 18f;
    private const float GaugeInnerFraction = 0.72f;
    private const float PickerPad = 18f;
    private const float StepperSize = 44f;
    private const float StepperAlpha = 0.10f;
    private const float StepperHoverAlpha = 0.18f;
    private const float SliderRowHeight = 36f;
    private const float PickerButtonHeight = Button.LargeHeight;
    private const float PickerLineGap = 10f;
    private const string LimitSliderId = "casino.limit.slider";

    private long? limitChoice;
    private bool limitsSeeded;
    private bool limitSliderDragging;
    private string limitsSeededAccount = string.Empty;
    private long limitsSavedAtTick;
    private Spring gaugeFill;

    private void ResetLimitsEditor()
    {
        limitChoice = null;
        limitsSeeded = false;
        limitsSeededAccount = string.Empty;
        limitsSavedAtTick = 0;
        gaugeFill.SnapTo(0f);
        casino.TakeLimitsResult();
        casino.TakeLimitsFailure();
    }

    private void DrawLimits(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.limits"))
        using (var surface = AppSurface.Begin(body))
        {
            ConsumeLimitsResult();
            var state = casino.State;
            if (state is null)
            {
                LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
                return;
            }

            SeedLimitChoice(state);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var tonight = CasinoTonight.From(state);
            var cursorY = DrawLimitGauge(drawList, origin, width, state, tonight, scale);
            if (tonight.Reached)
            {
                cursorY = DrawLimitReached(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
            }

            if (HouseLimitActive(state))
            {
                cursorY = CoinArt.DrawPanel(ui, new Vector2(origin.X, cursorY + CardGap * scale), width,
                    FontAwesomeIcon.ShieldAlt, AccentRing.Indigo, Loc.T(L.Casino.HouseLimitTitle),
                    texts.Number(L.Casino.HouseLimitLine, state.LossLimit), scale);
            }

            var pickerTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Casino.SelfLimitHeading), scale);
            cursorY = DrawLimitPicker(drawList, new Vector2(origin.X, pickerTop), width, state, scale);
            if (limitSliderDragging)
            {
                surface.CancelDrag();
            }

            var ceiling = CasinoLimitPicker.Ceiling;
            var hint = texts.Numbers(L.Casino.SelfLimitHint, CasinoLimitPicker.Floor, ceiling);
            var hintTop = cursorY + Metrics.Space.Sm * scale;
            var hintHeight = Typography.DrawWrappedLeft(new Vector2(origin.X + Metrics.Space.Lg * scale, hintTop),
                hint, ui.BodyInk, TextStyles.Footnote, width - Metrics.Space.Lg * 2f * scale);
            CoinArt.Reserve(origin, width, hintTop + hintHeight + CoinArt.BottomPad * scale);
        }
    }

    private static bool HouseLimitActive(CasinoStateDto state) =>
        state.LossLimit > 0 && (state.SelfLossLimit is null || state.LossLimit < state.SelfLossLimit.Value);

    private void ConsumeLimitsResult()
    {
        if (casino.TakeLimitsFailure())
        {
            UiFeedback.Play(UiSound.Blocked);
            confirm.Alert(null, Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)), Loc.T(L.Common.Close));
        }

        var result = casino.TakeLimitsResult();
        if (result is null)
        {
            return;
        }

        UiFeedback.Play(UiSound.Success);
        limitsSeeded = false;
        limitsSavedAtTick = Environment.TickCount64;
    }

    private void SeedLimitChoice(CasinoStateDto state)
    {
        var account = session.CurrentUser?.Id ?? string.Empty;
        if (limitsSeeded && string.Equals(account, limitsSeededAccount, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(account, limitsSeededAccount, StringComparison.Ordinal))
        {
            limitsSavedAtTick = 0;
        }

        limitChoice = state.SelfLossLimit;
        limitsSeeded = true;
        limitsSeededAccount = account;
    }

    private float DrawLimitGauge(ImDrawListPtr drawList, Vector2 origin, float width, CasinoStateDto state,
        in CasinoTonight tonight, float scale)
    {
        var pad = GaugePad * scale;
        var radius = GaugeRadius * scale;
        var height = pad * 2f + radius * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        var center = new Vector2(min.X + pad + radius, min.Y + pad + radius);
        var thickness = GaugeThickness * scale;
        var ringRadius = radius - thickness * 0.5f;
        ProgressRing.Track(drawList, center, ringRadius, thickness, Palette.WithAlpha(ui.TitleInk, GaugeTrackAlpha));
        var shown = gaugeFill.Step(tonight.Fraction, Motion.PageSettle,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        ProgressRing.Fill(drawList, center, ringRadius, thickness, shown, ToneInk(tonight.Tone));
        if (tonight.HasLimit && !tonight.Reached)
        {
            var inner = (ringRadius - thickness) * 2f * GaugeInnerFraction;
            var room = NumberText.Group(tonight.Headroom);
            var roomStyle = WidgetText.FitStyle(room, TextStyles.Title2, inner, true);
            var roomHeight = Typography.LineHeight(roomStyle);
            var captionHeight = Typography.LineHeight(TextStyles.Footnote);
            var blockTop = center.Y - (roomHeight + captionHeight) * 0.5f;
            WidgetText.TabularCentered(drawList, new Vector2(center.X, blockTop + roomHeight * 0.5f), room,
                ui.TitleInk, roomStyle, inner);
            Typography.DrawCentered(drawList, new Vector2(center.X, blockTop + roomHeight + captionHeight * 0.5f),
                Typography.FitText(Loc.T(L.Casino.LimitLeftCaption), inner, TextStyles.Footnote), ui.BodyInk,
                TextStyles.Footnote);
        }
        else
        {
            ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.HandHoldingHeart,
                tonight.Reached ? theme.Danger : ui.MutedInk, radius * 0.62f);
        }

        var textLeft = center.X + radius + GaugeTextGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var headline = Typography.LineHeight(TextStyles.Headline);
        var title3 = Typography.LineHeight(TextStyles.Title3);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var pending = PendingLine(state);
        var lines = headline + title3 + footnote + (pending.Length > 0 ? footnote : 0f);
        var top = center.Y - lines * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(L.Casino.NetHeading), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        top += headline;
        var result = TonightResult(tonight.NetLoss, out var resultInk);
        if (tonight.NetLoss != 0)
        {
            CurrencyGlyph.DrawAmount(drawList, new Vector2(textLeft, top),
                Typography.FitText(result, MathF.Max(1f, textWidth - CurrencyGlyph.Reserve(title3)), TextStyles.Title3),
                CurrencyKind.Chips, resultInk, TextStyles.Title3);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, top),
                Typography.FitText(result, textWidth, TextStyles.Title3), resultInk, TextStyles.Title3);
        }

        top += title3;
        var limitLine = tonight.HasLimit
            ? texts.Number(L.Casino.SelfLimitCurrent, tonight.Limit)
            : Loc.T(L.Casino.TonightNoLimit);
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(limitLine, textWidth, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
        if (pending.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + footnote),
                Typography.FitText(pending, textWidth, TextStyles.Footnote), ui.Accent, TextStyles.Footnote);
        }

        return max.Y;
    }

    private string PendingLine(CasinoStateDto state)
    {
        if (state.PendingRaiseAtUnix is null)
        {
            return string.Empty;
        }

        return state.PendingRaiseLimit is long raise
            ? texts.Number(L.Casino.PendingRaise, raise)
            : Loc.T(L.Casino.PendingRemove);
    }

    private float DrawLimitPicker(ImDrawListPtr drawList, Vector2 origin, float width, CasinoStateDto state,
        float scale)
    {
        limitSliderDragging = false;
        if (limitChoice is not long chosen)
        {
            return DrawLimitOff(drawList, origin, width, state, scale);
        }

        var pad = PickerPad * scale;
        var stepper = StepperSize * scale;
        var valueStyle = TextStyles.WidgetDisplayCompact;
        var valueHeight = Typography.LineHeight(valueStyle);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var lineGap = PickerLineGap * scale;
        var sliderHeight = SliderRowHeight * scale;
        var buttonHeight = PickerButtonHeight * scale;
        var hasSelf = state.SelfLossLimit is not null;
        var flash = Environment.TickCount64 - limitsSavedAtTick < SavedFlashMilliseconds;
        var height = pad * 2f + MathF.Max(stepper, valueHeight) + footnote + lineGap + sliderHeight + footnote +
                     lineGap + footnote + lineGap + buttonHeight;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);

        var ceiling = CasinoLimitPicker.Ceiling;
        var left = min.X + pad;
        var right = max.X - pad;
        var rowCenterY = min.Y + pad + MathF.Max(stepper, valueHeight) * 0.5f;
        if (DrawStepper(drawList, new Vector2(left + stepper * 0.5f, rowCenterY), FontAwesomeIcon.Minus,
                chosen > CasinoLimitPicker.Floor, "minus"))
        {
            chosen = CasinoLimitPicker.Nudge(chosen, -1, ceiling);
        }

        if (DrawStepper(drawList, new Vector2(right - stepper * 0.5f, rowCenterY), FontAwesomeIcon.Plus,
                chosen < ceiling, "plus"))
        {
            chosen = CasinoLimitPicker.Nudge(chosen, 1, ceiling);
        }

        var valueText = NumberText.Group(chosen);
        var valueWidthLimit = right - left - stepper * 2f - Metrics.Space.Md * scale * 2f;
        var fittedStyle = WidgetText.FitStyle(valueText, valueStyle,
            MathF.Max(1f, valueWidthLimit - CurrencyGlyph.Reserve(valueHeight)), true);
        var fittedHeight = Typography.LineHeight(fittedStyle);
        var glyphReserve = CurrencyGlyph.Reserve(fittedHeight);
        var valueWidth = glyphReserve + WidgetText.TabularWidth(valueText, fittedStyle);
        var valueLeft = (left + right - valueWidth) * 0.5f;
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips,
            new Vector2(valueLeft + fittedHeight * CurrencyGlyph.GlyphFraction * 0.5f, rowCenterY),
            fittedHeight * CurrencyGlyph.GlyphFraction);
        WidgetText.Tabular(drawList, new Vector2(valueLeft + glyphReserve, rowCenterY - fittedHeight * 0.5f),
            valueText, ui.TitleInk, fittedStyle);
        var top = min.Y + pad + MathF.Max(stepper, valueHeight);
        Typography.DrawCentered(drawList, new Vector2((left + right) * 0.5f, top + footnote * 0.5f),
            texts.Number(L.Casino.LimitCoinEquivalent, CasinoChipLots.CoinsFor(chosen)), ui.MutedInk,
            TextStyles.Footnote);
        top += footnote + lineGap;

        var sliderRow = new Rect(new Vector2(left, top), new Vector2(right, top + sliderHeight));
        var slider = Slider.Draw(LimitSliderId, sliderRow, CasinoLimitPicker.FractionOf(chosen, ceiling), theme,
            Metrics.Space.Sm * scale, Metrics.Space.Sm * scale);
        limitSliderDragging = slider.Dragging;
        if (slider.Dragging || slider.Released)
        {
            chosen = CasinoLimitPicker.FromFraction(slider.Value, ceiling);
        }

        top += sliderHeight;
        Typography.Draw(drawList, new Vector2(left, top), NumberText.Group(CasinoLimitPicker.Floor), ui.MutedInk,
            TextStyles.Footnote);
        var ceilingText = NumberText.Group(ceiling);
        Typography.Draw(drawList, new Vector2(right - Typography.Measure(ceilingText, TextStyles.Footnote).X, top),
            ceilingText, ui.BodyInk, TextStyles.Footnote);
        top += footnote + lineGap;

        limitChoice = chosen;
        var change = CasinoLimitPicker.ChangeOf(chosen, state.SelfLossLimit, state.PendingRaiseAtUnix is not null,
            state.PendingRaiseLimit);
        var changeText = ChangeLine(change, flash, out var changeInk);
        Typography.DrawCentered(drawList, new Vector2((left + right) * 0.5f, top + footnote * 0.5f),
            Typography.FitText(changeText, right - left, TextStyles.Footnote), changeInk, TextStyles.Footnote);
        top += footnote + lineGap;

        var gap = CardGap * scale;
        var secondaryLabel = hasSelf ? Loc.T(L.Casino.LimitRemove) : Loc.T(L.Common.Cancel);
        var secondaryWidth = Button.WidthFor(secondaryLabel, ButtonSize.Large);
        var saveRect = new Rect(new Vector2(left, top), new Vector2(right - secondaryWidth - gap, top + buttonHeight));
        var canSave = change != LimitChange.None && !casino.SavingLimits;
        if (Button.Draw(drawList, saveRect, Loc.T(L.Casino.SelfLimitSave), ui.Ink, ButtonStyle.Prominent,
                enabled: canSave, id: "casino.limits.save"))
        {
            casino.SetLimits(chosen);
        }

        var secondaryRect = new Rect(new Vector2(right - secondaryWidth, top), new Vector2(right, top + buttonHeight));
        if (Button.Draw(drawList, secondaryRect, secondaryLabel, ui.Ink, ButtonStyle.Gray,
                enabled: !casino.SavingLimits, id: "casino.limits.secondary"))
        {
            if (hasSelf)
            {
                AskRemoveLimit();
            }
            else
            {
                limitChoice = null;
            }
        }

        return max.Y;
    }

    private string ChangeLine(LimitChange change, bool flash, out Vector4 ink)
    {
        if (flash && change == LimitChange.None)
        {
            ink = ui.Accent;
            return Loc.T(L.Casino.SelfLimitSaved);
        }

        switch (change)
        {
            case LimitChange.StartsNow:
                ink = ui.Accent;
                return Loc.T(L.Casino.LimitStartsNow);
            case LimitChange.NextDay:
                ink = AccentRing.Orange;
                return Loc.T(L.Casino.LimitNextDay);
            default:
                ink = ui.MutedInk;
                return Loc.T(L.Casino.LimitUnchanged);
        }
    }

    private float DrawLimitOff(ImDrawListPtr drawList, Vector2 origin, float width, CasinoStateDto state,
        float scale)
    {
        var pad = PickerPad * scale;
        var buttonHeight = PickerButtonHeight * scale;
        var pending = PendingLine(state);
        var body = pending.Length > 0 ? pending : Loc.T(L.Casino.LimitOffBody);
        var title = Loc.T(L.Casino.LimitOffTitle);
        var panelHeight = CoinArt.PanelHeight(title, body, width, scale);
        var height = panelHeight + buttonHeight + pad;
        var max = new Vector2(origin.X + width, origin.Y + height);
        CoinArt.Panel(drawList, ui, origin, width, height, FontAwesomeIcon.HandHoldingHeart, AccentRing.Rose,
            title, body, scale);
        var buttonTop = origin.Y + panelHeight;
        var rect = new Rect(new Vector2(origin.X + pad, buttonTop), new Vector2(max.X - pad, buttonTop + buttonHeight));
        if (Button.Draw(drawList, rect, Loc.T(L.Casino.LimitSetAction), ui.Ink, ButtonStyle.Tinted,
                enabled: !casino.SavingLimits, id: "casino.limits.set"))
        {
            limitChoice = CasinoLimitPicker.Snap(CasinoLimits.SuggestedLimit,
                CasinoLimitPicker.Ceiling);
        }

        return max.Y;
    }

    private bool DrawStepper(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, bool enabled, string id)
    {
        var size = StepperSize * UiScale.Current;
        var half = new Vector2(size * 0.5f, size * 0.5f);
        var hovered = enabled && UiInteract.Hover(center - half, center + half);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(ImGui.GetID(id), pressed, Motion.PressScaleControl);
        var alpha = hovered ? StepperHoverAlpha : StepperAlpha;
        drawList.AddCircleFilled(center, size * 0.5f * factor,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, enabled ? alpha : alpha * 0.5f)), 32);
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), enabled ? ui.TitleInk : ui.MutedInk, 0.8f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - half, center + half, hovered);
    }

    private void AskRemoveLimit()
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Casino.LimitRemoveTitle),
            Message = Loc.T(L.Casino.LimitRemoveBody),
            ConfirmLabel = Loc.T(L.Casino.LimitRemove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = RemoveLimit,
        });
    }

    private void RemoveLimit()
    {
        casino.SetLimits(null);
    }
}
