using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class HoldemTable
{
    public const float BuyInDeckHeight = DeckPad * 2f + DeckRow + TouchRow * 2f + DeckGap * 3f + Button.LargeHeight;

    private const float DeckPad = 12f;
    private const float DeckGap = 8f;
    private const float DeckRow = 30f;
    private const float TouchRow = Metrics.Size.Pill;
    private const float IconRadius = 22f;
    private const float IconGap = 14f;
    private const float FoldShare = 0.28f;

    private static readonly string HistoryGlyph = IconGlyph.Of(FontAwesomeIcon.History);
    private static readonly string ReactGlyph = IconGlyph.Of(FontAwesomeIcon.Smile);
    private static readonly string TopUpGlyph = IconGlyph.Of(FontAwesomeIcon.Coins);
    private static readonly string SitOutGlyph = IconGlyph.Of(FontAwesomeIcon.Pause);
    private static readonly string BackInGlyph = IconGlyph.Of(FontAwesomeIcon.Play);
    private static readonly string LeaveGlyph = IconGlyph.Of(FontAwesomeIcon.DoorOpen);
    private static readonly string LockGlyph = IconGlyph.Of(FontAwesomeIcon.Lock);
    private static readonly string CloseGlyph = IconGlyph.Of(FontAwesomeIcon.Times);

    private static readonly string[] StepIds =
    {
        "holdem.step.less", "holdem.step.min", "holdem.step.max", "holdem.step.more",
    };

    private readonly string[] postOptions = new string[2];
    private LanguageInfo? postLanguage;

    private void BeginBuyIn(int seat)
    {
        pickedSeat = seat;
        buyIn = 0;
        postNow = false;
        inlineReason = string.Empty;
        deckMode = HoldemDeckMode.BuyIn;
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void DrawDeck(CasinoStage stage, in CasinoStageFrame frame, AppSkin ui, CasinoHoldemRoomStateDto board,
        CasinoHoldemYouDto? mine, CasinoHoldemSeatDto? hero, long remaining, float scale)
    {
        if (!frame.Layout.HasDeck)
        {
            return;
        }

        var deck = frame.Deck;
        var blocked = frame.Blocked;
        if (hero is null)
        {
            if (CasinoSeatMachine.Holds(seatFlow.Stage) || seatFlow.Stage == CasinoSeatStage.Sitting)
            {
                DeckStatus(ui, deck, Loc.T(L.Holdem.Seating), scale);
                return;
            }

            if (deckMode == HoldemDeckMode.BuyIn)
            {
                DrawBuyInDeck(ui, deck, board, blocked, scale);
                return;
            }

            DrawWatchDeck(ui, deck, board, blocked, scale);
            return;
        }

        var prompt = LivePrompt(board, mine);
        if (prompt is not null)
        {
            if (HoldemActions.OnlyShowOrMuck(prompt.Actions))
            {
                DrawShowMuck(ui, deck, board, prompt, scale);
                return;
            }

            var model = RaiseModel(board, hero, prompt, blocked);
            if (deckMode == HoldemDeckMode.Raise)
            {
                DrawRaiseDeck(ui, deck, board, model);
                return;
            }

            DrawActionBar(ui, deck, board, prompt, model, remaining, scale);
            return;
        }

        switch (deckMode)
        {
            case HoldemDeckMode.TopUp:
                DrawTopUpDeck(ui, deck, board, hero, blocked, scale);
                return;
            case HoldemDeckMode.Reactions:
                DrawReactionsDeck(ui, deck, scale);
                return;
        }

        if (deckMode != HoldemDeckMode.Status)
        {
            deckMode = HoldemDeckMode.Status;
        }

        DrawSeatedDeck(ui, deck, board, hero, scale);
    }

    private HoldemRaiseModel RaiseModel(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto hero,
        CasinoHoldemPromptDto prompt, bool blocked)
    {
        var bigBlind = board.BigBlind > 0 ? board.BigBlind : 1;
        return new HoldemRaiseModel(prompt.Actions, hero.Bet, prompt.ToCall, prompt.MinRaiseTo, prompt.MaxRaiseTo,
            prompt.PotTotal, bigBlind, !blocked && !store.ActInFlight);
    }

    private static Rect StatusRect(Rect deck, float scale)
    {
        var top = deck.Min.Y + DeckPad * scale;
        return new Rect(new Vector2(deck.Min.X + DeckPad * scale, top),
            new Vector2(deck.Max.X - DeckPad * scale, top + TouchRow * scale));
    }

    private static Rect TrailingRect(Rect status, float width) =>
        new(new Vector2(MathF.Max(status.Min.X, status.Max.X - width), status.Min.Y), status.Max);

    private static Rect ActionRect(Rect deck, float scale)
    {
        var bottom = deck.Max.Y - DeckPad * scale;
        return new Rect(new Vector2(deck.Min.X + DeckPad * scale, bottom - Button.LargeHeight * scale),
            new Vector2(deck.Max.X - DeckPad * scale, bottom));
    }

    private static void DeckStatus(AppSkin ui, Rect deck, string text, float scale)
    {
        var rect = StatusRect(deck, scale);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), rect.Center,
            Typography.FitText(text, rect.Width, TextStyles.Footnote), CasinoColors.InkTitle, TextStyles.Footnote);
    }

    private void DrawWatchDeck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, bool blocked, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var status = StatusRect(deck, scale);
        var action = ActionRect(deck, scale);
        var royalLocked = string.Equals(roomId, HoldemRules.RoyalRoom, StringComparison.Ordinal) && !practice
            && !HoldemRules.RoyalOpenTo(Balance());
        if (royalLocked)
        {
            var lockCenter = new Vector2(status.Min.X + 8f * scale, status.Center.Y);
            PhoneIcon.Draw(drawList, lockCenter, LockGlyph, CasinoColors.Money, 14f * scale);
            var text = Typography.FitText(Loc.T(L.Holdem.RoyalLocked), status.Width - 22f * scale, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(status.Min.X + 22f * scale,
                    status.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f), text, ui.BodyInk,
                TextStyles.Footnote);
            Button.Draw(drawList, action, Loc.T(L.Holdem.TableLocked), ui.Ink, ButtonStyle.Gray, enabled: false,
                id: "holdem.watch.locked");
            return;
        }

        var line = texts.Counts(L.Holdem.SeatedLine, Seated(board), SeatCountOf(board));
        Typography.DrawCentered(drawList, status.Center, Typography.FitText(line, status.Width, TextStyles.Footnote),
            CasinoColors.InkBody, TextStyles.Footnote);
        var historyCenter = new Vector2(action.Max.X - IconRadius * scale, action.Center.Y);
        if (RoundButton.Icon(drawList, historyCenter, IconRadius * scale, HistoryGlyph, ui.Ink, ButtonStyle.Gray,
                Loc.T(L.Holdem.History), HoverLabelSide.Above, !blocked))
        {
            historySheet.Open(roomId, rooms.AccountId);
        }

        var sitRect = new Rect(action.Min, new Vector2(historyCenter.X - IconRadius * scale - DeckGap * scale,
            action.Max.Y));
        var seat = FirstOpenSeat(board);
        if (!practice && !HoldemRules.CanBuyIn(MinBuyIn(board), MaxBuyIn(board), Bankroll(), chips.Ceiling.MaxBet,
                false))
        {
            if (Button.Draw(drawList, sitRect, Loc.T(L.Holdem.GetChips), ui.Ink, ButtonStyle.Tinted,
                    enabled: !blocked, id: "holdem.watch.chips"))
            {
                openCashier();
            }

            return;
        }

        var label = seat < 0 ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Holdem.SitDown);
        if (Button.Draw(drawList, sitRect, label, ui.Ink, ButtonStyle.Prominent,
                enabled: seat >= 0 && !blocked && !seatFlow.Busy, id: "holdem.watch.sit"))
        {
            BeginBuyIn(seat);
        }
    }

    private static int FirstOpenSeat(CasinoHoldemRoomStateDto board)
    {
        var count = SeatCountOf(board);
        for (var seat = 0; seat < count; seat++)
        {
            if (SeatAt(board, seat) is null)
            {
                return seat;
            }
        }

        return -1;
    }

    private long Bankroll() => chips.State?.Sitting?.Stack ?? 0;

    private long Balance() => Bankroll() + (chips.State?.TableSitting?.Stack ?? 0);

    private static long MinBuyIn(CasinoHoldemRoomStateDto board) =>
        board.MinBuyIn > 0 ? board.MinBuyIn : board.BigBlind * HoldemRules.MinBuyInBigBlinds;

    private static long MaxBuyIn(CasinoHoldemRoomStateDto board) =>
        board.MaxBuyIn > 0 ? board.MaxBuyIn : board.BigBlind * HoldemRules.MaxBuyInBigBlinds;

    private void DrawBuyInDeck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, bool blocked, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bigBlind = board.BigBlind > 0 ? board.BigBlind : 1;
        var minimum = MinBuyIn(board);
        var top = HoldemRules.BuyInCeiling(MaxBuyIn(board), Bankroll(), chips.Ceiling.MaxBet, practice);
        if (buyIn <= 0)
        {
            buyIn = HoldemRules.DefaultBuyIn(minimum, MaxBuyIn(board), Bankroll(), chips.Ceiling.MaxBet, bigBlind,
                practice);
        }

        buyIn = practice ? board.PracticeStack : HoldemRules.SnapBuyIn(buyIn, bigBlind, minimum, top);
        var row = DeckRow * scale;
        var gap = DeckGap * scale;
        var left = deck.Min.X + DeckPad * scale;
        var right = deck.Max.X - DeckPad * scale;
        var y = deck.Min.Y + DeckPad * scale;
        DrawAmountRow(drawList, ui, new Rect(new Vector2(left, y), new Vector2(right, y + row)),
            practice ? L.Holdem.PracticeStack : L.Holdem.BuyIn, buyIn, scale);
        y += row + gap;
        var touch = TouchRow * scale;
        if (practice)
        {
            Typography.DrawCentered(drawList, new Vector2((left + right) * 0.5f, y + touch * 0.5f),
                Typography.FitText(Loc.T(L.Holdem.PracticeNoChips), right - left, TextStyles.Footnote), CasinoColors.InkBody,
                TextStyles.Footnote);
        }
        else
        {
            DrawStepRow(ui, new Rect(new Vector2(left, y), new Vector2(right, y + touch)), bigBlind, minimum, top,
                ref buyIn, !blocked, scale);
        }

        y += touch + gap;
        var penalty = store.RejoinPenalty(roomId, Environment.TickCount64);
        DrawPostRow(ui, new Rect(new Vector2(left, y), new Vector2(right, y + touch)), board, penalty, scale);
        var action = ActionRect(deck, scale);
        var cancel = new Rect(action.Min, new Vector2(action.Min.X + action.Width * FoldShare, action.Max.Y));
        if (Button.Draw(drawList, cancel, Loc.T(L.Holdem.Back), ui.Ink, ButtonStyle.Gray, id: "holdem.buyin.back"))
        {
            deckMode = HoldemDeckMode.Status;
            return;
        }

        var confirm = new Rect(new Vector2(cancel.Max.X + gap, action.Min.Y), action.Max);
        var canSit = practice || (buyIn >= minimum && buyIn <= top);
        var label = practice ? Loc.T(L.Holdem.SitDown) : texts.Number(L.Holdem.SitFor, buyIn);
        if (!Button.Draw(drawList, confirm, Typography.FitText(label, confirm.Width - confirm.Height,
                Button.LabelStyle(confirm.Height)), ui.Ink, ButtonStyle.Prominent,
                enabled: canSit && !blocked && !seatFlow.Busy && pickedSeat >= 0, id: "holdem.buyin.sit"))
        {
            return;
        }

        if (SeatAt(board, pickedSeat) is not null)
        {
            pickedSeat = FirstOpenSeat(board);
        }

        if (pickedSeat < 0)
        {
            inlineReason = CasinoReasons.Full;
            deckMode = HoldemDeckMode.Status;
            return;
        }

        inlineReason = string.Empty;
        seatFlow.Sit(roomId, pickedSeat, buyIn, postNow || penalty);
        deckMode = HoldemDeckMode.Status;
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private static void DrawAmountRow(ImDrawListPtr drawList, AppSkin ui, Rect row, LocString caption, long amount,
        float scale)
    {
        var captionText = Loc.T(caption);
        var style = TextStyles.Footnote;
        var captionHeight = Typography.LineHeight(style);
        var amountText = NumberText.Group(amount);
        var amountStyle = TextStyles.Headline;
        var size = CurrencyGlyph.MeasureAmount(amountText, amountStyle);
        var captionWidth = MathF.Max(0f, row.Width - size.X - Metrics.Space.Md * scale);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - captionHeight * 0.5f),
            Typography.FitText(captionText, captionWidth, style), CasinoColors.InkBody, style);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(row.Max.X - size.X, row.Center.Y - size.Y * 0.5f), amountText,
            CurrencyKind.Chips, CasinoColors.Money, amountStyle);
    }

    private static void DrawStepRow(AppSkin ui, Rect row, long bigBlind, long minimum, long maximum, ref long amount,
        bool enabled, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var gap = DeckGap * scale * 0.5f;
        var width = (row.Width - gap * 3f) / 4f;
        var live = enabled && maximum >= minimum;
        for (var index = 0; index < 4; index++)
        {
            var min = new Vector2(row.Min.X + index * (width + gap), row.Min.Y);
            var rect = new Rect(min, new Vector2(min.X + width, row.Max.Y));
            var label = index switch
            {
                0 => Loc.T(L.Holdem.StepLess),
                1 => Loc.T(L.Holdem.QuickMin),
                2 => Loc.T(L.Holdem.QuickMax),
                _ => Loc.T(L.Holdem.StepMore),
            };
            if (!Button.Draw(drawList, rect, Typography.FitText(label, width - rect.Height * 0.5f,
                    Button.LabelStyle(rect.Height)), ui.Ink, ButtonStyle.Gray, enabled: live, id: StepIds[index]))
            {
                continue;
            }

            amount = index switch
            {
                0 => HoldemRules.StepBuyIn(amount, -1, bigBlind, minimum, maximum),
                1 => minimum,
                2 => maximum,
                _ => HoldemRules.StepBuyIn(amount, 1, bigBlind, minimum, maximum),
            };
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private void DrawPostRow(AppSkin ui, Rect row, CasinoHoldemRoomStateDto board, bool penalty, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (penalty)
        {
            Typography.DrawCentered(drawList, row.Center,
                Typography.FitText(Loc.T(L.Holdem.RejoinPenalty), row.Width, TextStyles.Footnote), CasinoColors.InkBody,
                TextStyles.Footnote);
            return;
        }

        if (board.HandId.Length == 0)
        {
            Typography.DrawCentered(drawList, row.Center,
                Typography.FitText(Loc.T(L.Holdem.DealtNextHand), row.Width, TextStyles.Footnote), CasinoColors.InkBody,
                TextStyles.Footnote);
            return;
        }

        if (!ReferenceEquals(postLanguage, Loc.Current) || postOptions[0] is null)
        {
            postLanguage = Loc.Current;
            postOptions[0] = Loc.T(L.Holdem.WaitForBigBlind);
            postOptions[1] = Loc.T(L.Holdem.PostNow);
        }

        postNow = SegmentStrip.Draw("##holdemPost", row, postOptions, postNow ? 1 : 0, ui.Palette, TouchRow) == 1;
    }

    private void DrawRaiseDeck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, in HoldemRaiseModel model)
    {
        var result = composer.Draw(ui, deck, model);
        if (result == HoldemComposerAction.Cancel)
        {
            deckMode = HoldemDeckMode.Status;
            return;
        }

        if (result != HoldemComposerAction.Confirm)
        {
            return;
        }

        var action = composer.ActionFor(model);
        Act(board, action, composer.Amount);
        deckMode = HoldemDeckMode.Status;
    }

    private void Act(CasinoHoldemRoomStateDto board, int action, long amount)
    {
        inlineReason = string.Empty;
        store.Act(roomId, board.HandId, board.ActionCount, action, amount);
        if (action is HoldemActions.Call or HoldemActions.Bet or HoldemActions.Raise or HoldemActions.AllIn)
        {
            CasinoSfx.Play(UiSound.ChipSlide);
        }
    }

    private void DrawActionBar(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, CasinoHoldemPromptDto prompt,
        in HoldemRaiseModel model, long remaining, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var status = StatusRect(deck, scale);
        var mask = prompt.Actions;
        var timeBankWidth = 0f;
        if (prompt.TimeBankLeft > 0)
        {
            var label = texts.Count(L.Holdem.TimeBankButton, HoldemRules.TimeBankSeconds);
            timeBankWidth = Button.WidthFor(label, ButtonSize.Large);
            var rect = TrailingRect(status, timeBankWidth);
            if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Tinted, enabled: model.Enabled,
                    id: "holdem.timebank"))
            {
                store.TimeBank(roomId, board.HandId, board.ActionCount);
            }

            HoverTooltip.Show(rect, texts.Count(L.Holdem.TimeBankLeft, prompt.TimeBankLeft), HoverLabelSide.Above);
        }

        var seconds = (int)((Math.Max(0, remaining) + 999) / 1000);
        var line = prompt.ToCall > 0
            ? texts.Numbers(L.Holdem.ToCallLine, prompt.ToCall, seconds)
            : texts.Count(L.Holdem.YourMoveLine, seconds);
        var lineWidth = status.Width - timeBankWidth - DeckGap * scale;
        Typography.Draw(drawList, new Vector2(status.Min.X, status.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(line, lineWidth, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);

        var action = ActionRect(deck, scale);
        var gap = DeckGap * scale * 0.75f;
        var third = (action.Width - gap * 2f) / 3f;
        var foldRect = new Rect(action.Min, new Vector2(action.Min.X + third, action.Max.Y));
        var callRect = new Rect(new Vector2(foldRect.Max.X + gap, action.Min.Y),
            new Vector2(foldRect.Max.X + gap + third, action.Max.Y));
        var raiseRect = new Rect(new Vector2(callRect.Max.X + gap, action.Min.Y), action.Max);
        var enabled = model.Enabled;
        if (HoldemActions.Allows(mask, HoldemActions.Fold)
            && Button.Draw(drawList, foldRect, Loc.T(L.Holdem.ActionFold), ui.Ink, ButtonStyle.Gray, enabled: enabled,
                id: "holdem.act.fold"))
        {
            Act(board, HoldemActions.Fold, 0);
        }

        if (HoldemActions.Allows(mask, HoldemActions.Check))
        {
            if (Button.Draw(drawList, callRect, Loc.T(L.Holdem.ActionCheck), ui.Ink, ButtonStyle.Tinted,
                    enabled: enabled, id: "holdem.act.check"))
            {
                Act(board, HoldemActions.Check, 0);
            }
        }
        else if (HoldemActions.Allows(mask, HoldemActions.Call))
        {
            var callAll = prompt.ToCall >= prompt.MaxRaiseTo - model.StreetBet && prompt.MaxRaiseTo > 0;
            var label = texts.Number(callAll ? L.Holdem.AllInFor : L.Holdem.CallFor, prompt.ToCall);
            if (Button.Draw(drawList, callRect, Typography.FitText(label, third - callRect.Height * 0.5f,
                    Button.LabelStyle(callRect.Height)), ui.Ink, ButtonStyle.Tinted, enabled: enabled,
                    id: "holdem.act.call"))
            {
                Act(board, HoldemActions.Call, 0);
            }
        }

        DrawWagerButton(ui, drawList, raiseRect, board, model, third, enabled);
    }

    private void DrawWagerButton(AppSkin ui, ImDrawListPtr drawList, Rect rect, CasinoHoldemRoomStateDto board,
        in HoldemRaiseModel model, float width, bool enabled)
    {
        var mask = model.Actions;
        var canSize = (HoldemActions.Allows(mask, HoldemActions.Bet) || HoldemActions.Allows(mask, HoldemActions.Raise))
            && model.MaxRaiseTo > HoldemRaiseComposer.Minimum(model);
        if (canSize)
        {
            var label = Loc.T(HoldemActions.Allows(mask, HoldemActions.Bet) ? L.Holdem.ActionBet : L.Holdem.ActionRaise);
            if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Prominent, enabled: enabled,
                    id: "holdem.act.raise"))
            {
                composer.Open(model);
                deckMode = HoldemDeckMode.Raise;
                CasinoSfx.Play(UiSound.ChipSlide);
            }

            return;
        }

        if (!HoldemActions.Allows(mask, HoldemActions.AllIn) && !HoldemActions.Wagers(mask))
        {
            return;
        }

        var allIn = texts.Number(L.Holdem.AllInFor, model.MaxRaiseTo);
        if (Button.Draw(drawList, rect, Typography.FitText(allIn, width - rect.Height * 0.5f,
                Button.LabelStyle(rect.Height)), ui.Ink, ButtonStyle.Prominent, enabled: enabled,
                id: "holdem.act.allin"))
        {
            Act(board, HoldemActions.Allows(mask, HoldemActions.AllIn) ? HoldemActions.AllIn : HoldemActions.Raise,
                model.MaxRaiseTo);
        }
    }

    private void DrawShowMuck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, CasinoHoldemPromptDto prompt,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var status = StatusRect(deck, scale);
        var seconds = (int)((Math.Max(0, rooms.Room.RemainingMilliseconds(prompt.DeadlineUnixMs,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) + 999) / 1000);
        var line = seconds > 0 ? texts.Count(L.Holdem.ShowPrompt, seconds) : Loc.T(L.Holdem.ShowPromptOpen);
        Typography.DrawCentered(drawList, status.Center, Typography.FitText(line, status.Width, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        var action = ActionRect(deck, scale);
        var gap = DeckGap * scale;
        var half = (action.Width - gap) * 0.5f;
        var muck = new Rect(action.Min, new Vector2(action.Min.X + half, action.Max.Y));
        var show = new Rect(new Vector2(muck.Max.X + gap, action.Min.Y), action.Max);
        var enabled = !store.ActInFlight;
        if (HoldemActions.Allows(prompt.Actions, HoldemActions.Muck)
            && Button.Draw(drawList, muck, Loc.T(L.Holdem.ActionMuck), ui.Ink, ButtonStyle.Gray, enabled: enabled,
                id: "holdem.act.muck"))
        {
            Act(board, HoldemActions.Muck, 0);
        }

        if (HoldemActions.Allows(prompt.Actions, HoldemActions.Show)
            && Button.Draw(drawList, show, Loc.T(L.Holdem.ActionShow), ui.Ink, ButtonStyle.Prominent,
                enabled: enabled, id: "holdem.act.show"))
        {
            Act(board, HoldemActions.Show, 0);
            CasinoSfx.Play(UiSound.CardSnap);
        }
    }

    private void DrawSeatedDeck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto hero,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var status = StatusRect(deck, scale);
        var waiting = hero.State == HoldemSeatStates.Waiting;
        var message = seatFlow.StandQueued || hero.Leaving ? Loc.T(L.Holdem.LeavingAtHandEnd)
            : Loc.T(StatusOf(board, hero));
        var postWidth = 0f;
        if (waiting && !store.IntentInFlight)
        {
            var label = Loc.T(L.Holdem.PostNow);
            postWidth = Button.WidthFor(label, ButtonSize.Large);
            var rect = TrailingRect(status, postWidth);
            if (Button.Draw(drawList, rect, label, ui.Ink, ButtonStyle.Tinted, id: "holdem.postnow"))
            {
                store.SitOut(roomId, false, true);
            }
        }

        Typography.Draw(drawList, new Vector2(status.Min.X, status.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(message, status.Width - postWidth - DeckGap * scale, TextStyles.Footnote), ui.BodyInk,
            TextStyles.Footnote);

        var action = ActionRect(deck, scale);
        var radius = IconRadius * scale;
        var step = radius * 2f + IconGap * scale;
        var center = new Vector2(action.Center.X - step * 2f, action.Center.Y);
        var between = !HoldemSeatStates.DealtIn(hero.State) || HoldemPhases.Over(board.Phase)
            || board.Phase == HoldemPhases.Waiting;
        if (RoundButton.Icon(drawList, center, radius, ReactGlyph, ui.Ink, ButtonStyle.Gray, Loc.T(L.Holdem.React),
                HoverLabelSide.Above))
        {
            deckMode = HoldemDeckMode.Reactions;
        }

        center.X += step;
        if (RoundButton.Icon(drawList, center, radius, HistoryGlyph, ui.Ink, ButtonStyle.Gray, Loc.T(L.Holdem.History),
                HoverLabelSide.Above))
        {
            historySheet.Open(roomId, rooms.AccountId);
        }

        center.X += step;
        var canTopUp = between && (practice ? board.PracticeRebuy : TopUpCeiling(board, hero) >= board.BigBlind);
        if (RoundButton.Icon(drawList, center, radius, TopUpGlyph, ui.Ink, ButtonStyle.Gray,
                Loc.T(practice ? L.Holdem.Rebuy : L.Holdem.TopUp), HoverLabelSide.Above, canTopUp && !store.IntentInFlight))
        {
            buyIn = 0;
            deckMode = HoldemDeckMode.TopUp;
        }

        center.X += step;
        var sittingOut = hero.State == HoldemSeatStates.SittingOut;
        if (RoundButton.Icon(drawList, center, radius, sittingOut ? BackInGlyph : SitOutGlyph, ui.Ink,
                sittingOut ? ButtonStyle.Tinted : ButtonStyle.Gray,
                Loc.T(sittingOut ? L.Holdem.ImBack : L.Holdem.SitOut), HoverLabelSide.Above, !store.IntentInFlight))
        {
            store.SitOut(roomId, !sittingOut, false);
        }

        center.X += step;
        if (RoundButton.Icon(drawList, center, radius, LeaveGlyph, ui.Ink, ButtonStyle.Gray, Loc.T(L.Holdem.Leave),
                HoverLabelSide.Above, !seatFlow.Busy && !seatFlow.StandQueued))
        {
            inlineReason = string.Empty;
            seatFlow.Stand(roomId);
        }
    }

    private long TopUpCeiling(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto hero)
    {
        var room = HoldemRules.TopUpRoom(hero.Stack, MaxBuyIn(board));
        return Math.Min(room, Bankroll());
    }

    private void DrawTopUpDeck(AppSkin ui, Rect deck, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto hero,
        bool blocked, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bigBlind = board.BigBlind > 0 ? board.BigBlind : 1;
        var top = TopUpCeiling(board, hero);
        if (buyIn <= 0)
        {
            buyIn = top;
        }

        buyIn = practice ? board.PracticeStack : HoldemRules.SnapBuyIn(buyIn, bigBlind, Math.Min(bigBlind, top), top);
        var row = DeckRow * scale;
        var gap = DeckGap * scale;
        var left = deck.Min.X + DeckPad * scale;
        var right = deck.Max.X - DeckPad * scale;
        var y = deck.Min.Y + DeckPad * scale;
        DrawAmountRow(drawList, ui, new Rect(new Vector2(left, y), new Vector2(right, y + row)),
            practice ? L.Holdem.PracticeStack : L.Holdem.TopUp, buyIn, scale);
        y += row + gap;
        var touch = TouchRow * scale;
        if (!practice)
        {
            DrawStepRow(ui, new Rect(new Vector2(left, y), new Vector2(right, y + touch)), bigBlind,
                Math.Min(bigBlind, top), top, ref buyIn, !blocked, scale);
        }

        y += touch + gap;
        Typography.DrawCentered(drawList, new Vector2((left + right) * 0.5f, y + touch * 0.5f),
            Typography.FitText(Loc.T(L.Holdem.TopUpHint), right - left, TextStyles.Footnote), CasinoColors.InkBody,
            TextStyles.Footnote);
        var action = ActionRect(deck, scale);
        var cancel = new Rect(action.Min, new Vector2(action.Min.X + action.Width * FoldShare, action.Max.Y));
        if (Button.Draw(drawList, cancel, Loc.T(L.Holdem.Back), ui.Ink, ButtonStyle.Gray, id: "holdem.topup.back"))
        {
            deckMode = HoldemDeckMode.Status;
            return;
        }

        var confirm = new Rect(new Vector2(cancel.Max.X + gap, action.Min.Y), action.Max);
        var label = practice ? Loc.T(L.Holdem.Rebuy) : texts.Number(L.Holdem.TopUpFor, buyIn);
        if (!Button.Draw(drawList, confirm, Typography.FitText(label, confirm.Width - confirm.Height,
                Button.LabelStyle(confirm.Height)), ui.Ink, ButtonStyle.Prominent,
                enabled: !blocked && buyIn > 0 && !store.IntentInFlight, id: "holdem.topup.confirm"))
        {
            return;
        }

        inlineReason = string.Empty;
        store.TopUp(roomId, buyIn);
        deckMode = HoldemDeckMode.Status;
        CasinoSfx.Play(UiSound.ChipSlide);
    }

    private void DrawReactionsDeck(AppSkin ui, Rect deck, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        DeckStatus(ui, deck, Loc.T(L.Holdem.ReactHint), scale);
        var action = ActionRect(deck, scale);
        var radius = MathF.Min(IconRadius * scale, action.Width / (ReactionGlyphs.Length + 1) * 0.5f - 2f * scale);
        var backWidth = radius * 2f + DeckGap * scale;
        var step = (action.Width - backWidth) / ReactionGlyphs.Length;
        for (var index = 0; index < ReactionGlyphs.Length; index++)
        {
            var center = new Vector2(action.Min.X + step * (index + 0.5f), action.Center.Y);
            if (RoundButton.Icon(drawList, center, radius, ReactionGlyphs[index], ui.Ink, ButtonStyle.Gray,
                    enabled: true))
            {
                reaction = index;
                reactionClock = 0f;
                deckMode = HoldemDeckMode.Status;
                CasinoSfx.Play(UiSound.ChipSlide);
            }
        }

        var backCenter = new Vector2(action.Max.X - radius, action.Center.Y);
        if (RoundButton.Icon(drawList, backCenter, radius, CloseGlyph, ui.Ink,
                ButtonStyle.Tinted, Loc.T(L.Holdem.Back), HoverLabelSide.Above))
        {
            deckMode = HoldemDeckMode.Status;
        }
    }
}
