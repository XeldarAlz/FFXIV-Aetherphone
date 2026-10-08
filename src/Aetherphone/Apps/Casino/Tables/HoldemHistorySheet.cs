using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HoldemReplayStep : byte
{
    Preflop,
    Flop,
    Turn,
    River,
    Showdown,
}

internal static class HoldemReplay
{
    public const int StepCount = 5;

    public static int BoardCardsAt(HoldemReplayStep step) => step switch
    {
        HoldemReplayStep.Flop => 3,
        HoldemReplayStep.Turn => 4,
        HoldemReplayStep.River or HoldemReplayStep.Showdown => HoldemRules.BoardSize,
        _ => 0,
    };

    public static HoldemReplayStep LastStepOf(CasinoHoldemHistoryHandDto hand)
    {
        var board = hand.Board?.Length ?? 0;
        var shown = false;
        var seats = hand.Seats;
        if (seats is not null)
        {
            for (var index = 0; index < seats.Length; index++)
            {
                shown |= seats[index].Shown && !seats[index].Folded;
            }
        }

        if (shown && board >= HoldemRules.BoardSize)
        {
            return HoldemReplayStep.Showdown;
        }

        return board switch
        {
            >= 5 => HoldemReplayStep.River,
            4 => HoldemReplayStep.Turn,
            3 => HoldemReplayStep.Flop,
            _ => HoldemReplayStep.Preflop,
        };
    }

    public static HoldemReplayStep Advance(HoldemReplayStep step, int direction, HoldemReplayStep last)
    {
        var next = Math.Clamp((int)step + Math.Sign(direction), 0, (int)last);
        return (HoldemReplayStep)next;
    }
}

internal sealed class HoldemHistorySheet
{
    private const float PanelHeightShare = 0.84f;
    private const float RowHeight = 64f;
    private const float MiniCardWidth = 18f;
    private const float ReplayCardWidth = 34f;
    private const float SeatRowHeight = 48f;
    private const float AutoStepSeconds = 1.2f;

    private static readonly LocString[] StepLabels =
    {
        L.Holdem.StepPreflop,
        L.Holdem.StepFlop,
        L.Holdem.StepTurn,
        L.Holdem.StepRiver,
        L.Holdem.StepShowdown,
    };

    private readonly SheetSurface sheet = new("casino.holdem.history");
    private readonly Action<Rect> drawSheetBody;
    private readonly HoldemStore store;
    private readonly CasinoTextCache texts;
    private readonly string[] stepNames = new string[HoldemReplay.StepCount];
    private readonly string[][] stepSets =
    {
        new string[1], new string[2], new string[3], new string[4], new string[HoldemReplay.StepCount],
    };

    private AppSkin skin = null!;
    private string roomId = string.Empty;
    private string myUserId = string.Empty;
    private int selected = -1;
    private HoldemReplayStep step;
    private bool playing;
    private float autoClock;
    private bool failed;
    private LanguageInfo? stepLanguage;

    public HoldemHistorySheet(HoldemStore store, CasinoTextCache texts)
    {
        this.store = store;
        this.texts = texts;
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open(string room, string userId)
    {
        roomId = room;
        myUserId = userId;
        selected = -1;
        playing = false;
        failed = false;
        store.LoadHistory(room);
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
        playing = false;
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        if (store.TakeHistoryFailure())
        {
            failed = true;
        }

        var title = selected >= 0 ? Loc.T(L.Holdem.ReplayTitle) : Loc.T(L.Holdem.HistoryTitle);
        sheet.Draw(screen, CasinoArt.Sheet(ui), title, PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        RefreshSteps();
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##holdemHistory", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            var history = store.HistoryFor(roomId);
            var hands = history?.Hands ?? Array.Empty<CasinoHoldemHistoryHandDto>();
            if (selected >= 0 && selected < hands.Length)
            {
                DrawReplay(skin, hands[selected], UiScale.Current);
                return;
            }

            selected = -1;
            DrawList(skin, hands, UiScale.Current);
        }
    }

    private void RefreshSteps()
    {
        if (ReferenceEquals(stepLanguage, Loc.Current) && stepNames[0] is not null)
        {
            return;
        }

        stepLanguage = Loc.Current;
        for (var index = 0; index < stepNames.Length; index++)
        {
            stepNames[index] = Loc.T(StepLabels[index]);
        }

        for (var set = 0; set < stepSets.Length; set++)
        {
            Array.Copy(stepNames, stepSets[set], set + 1);
        }
    }

    private void DrawList(AppSkin ui, CasinoHoldemHistoryHandDto[] hands, float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        if (hands.Length == 0)
        {
            var message = Loc.T(failed ? L.Holdem.HistoryFailed
                : store.HistoryLoading ? L.Holdem.HistoryLoading : L.Holdem.HistoryEmpty);
            var origin = ImGui.GetCursorScreenPos();
            var height = Typography.DrawWrappedLeft(origin, message, ui.BodyInk, TextStyles.Footnote, width);
            ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
            return;
        }

        for (var index = 0; index < hands.Length; index++)
        {
            var hand = hands[index];
            var origin = ImGui.GetCursorScreenPos();
            var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + RowHeight * scale));
            var hovered = UiInteract.Hover(rect.Min, rect.Max);
            ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale);
            if (hovered)
            {
                Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Grouped * scale,
                    ImGui.GetColorU32(ui.HoverTint));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            DrawHandRow(drawList, ui, hand, rect, scale);
            if (UiInteract.Click(rect.Min, rect.Max, hovered))
            {
                selected = index;
                step = HoldemReplayStep.Preflop;
                playing = true;
                autoClock = 0f;
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, (RowHeight + Metrics.Space.Sm) * scale));
        }
    }

    private void DrawHandRow(ImDrawListPtr drawList, AppSkin ui, CasinoHoldemHistoryHandDto hand, in Rect rect,
        float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var title = texts.Number(L.Holdem.HandNumber, hand.HandIndex);
        var titleStyle = TextStyles.SubheadlineEmphasized;
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, rect.Min.Y + pad * 0.7f), title, ui.TitleInk,
            titleStyle);
        var net = texts.Signed(hand.Net);
        var netSize = Typography.Measure(net, titleStyle);
        Typography.Draw(drawList, new Vector2(rect.Max.X - pad - netSize.X, rect.Min.Y + pad * 0.7f), net,
            hand.Net > 0 ? CasinoColors.Money : ui.MutedInk, titleStyle);
        var cardWidth = MiniCardWidth * scale;
        var cardsY = rect.Max.Y - pad * 0.6f - PlayingCards.HeightFor(cardWidth) * 0.5f;
        var mine = MySeat(hand);
        var x = rect.Min.X + pad + cardWidth * 0.5f;
        var myCards = mine?.Cards;
        for (var card = 0; card < HoldemRules.HoleCards; card++)
        {
            var value = myCards is not null && card < myCards.Length ? myCards[card] : HoldemRules.FaceDown;
            HoldemArt.DrawCard(drawList, new Vector2(x, cardsY), cardWidth, value, value >= 0, 1f, scale);
            x += cardWidth + 2f * scale;
        }

        x += cardWidth * 0.6f;
        var board = hand.Board ?? Array.Empty<int>();
        for (var card = 0; card < board.Length; card++)
        {
            HoldemArt.DrawCard(drawList, new Vector2(x, cardsY), cardWidth, board[card], true, 1f, scale);
            x += cardWidth + 2f * scale;
        }
    }

    private CasinoHoldemHistorySeatDto? MySeat(CasinoHoldemHistoryHandDto hand)
    {
        var seats = hand.Seats;
        if (seats is null || myUserId.Length == 0)
        {
            return null;
        }

        for (var index = 0; index < seats.Length; index++)
        {
            if (string.Equals(seats[index].UserId, myUserId, StringComparison.Ordinal))
            {
                return seats[index];
            }
        }

        return null;
    }

    private void DrawReplay(AppSkin ui, CasinoHoldemHistoryHandDto hand, float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        var drawList = ImGui.GetWindowDrawList();
        var last = HoldemReplay.LastStepOf(hand);
        AdvanceAuto(last);
        var origin = ImGui.GetCursorScreenPos();
        var backRect = new Rect(origin, new Vector2(origin.X + width * 0.34f, origin.Y + Button.SmallHeight * scale));
        if (Button.Draw(drawList, backRect, Loc.T(L.Holdem.ReplayAllHands), ui.Ink, ButtonStyle.Gray,
                id: "holdem.replay.list"))
        {
            selected = -1;
            playing = false;
        }

        var title = texts.Number(L.Holdem.HandNumber, hand.HandIndex);
        var titleSize = Typography.Measure(title, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(origin.X + width - titleSize.X, origin.Y), title, ui.TitleInk,
            TextStyles.Headline);
        ImGui.Dummy(new Vector2(width, Button.SmallHeight * scale + Metrics.Space.Md * scale));

        var stripOrigin = ImGui.GetCursorScreenPos();
        var strip = new Rect(stripOrigin, new Vector2(stripOrigin.X + width, stripOrigin.Y + 30f * scale));
        var picked = SegmentStrip.Draw("##holdemReplaySteps", strip, StepsUpTo(last), (int)step, ui.Palette);
        if (picked != (int)step)
        {
            step = (HoldemReplayStep)Math.Clamp(picked, 0, (int)last);
            playing = false;
        }

        ImGui.Dummy(new Vector2(width, 30f * scale + Metrics.Space.Md * scale));
        DrawReplayBoard(drawList, hand, width, scale);
        DrawReplaySeats(drawList, ui, hand, width, scale);
        DrawReplayControls(drawList, ui, width, last, scale);
        DrawFairness(ui, hand, width, scale);
    }

    private string[] StepsUpTo(HoldemReplayStep last) => stepSets[Math.Clamp((int)last, 0, stepSets.Length - 1)];

    private void AdvanceAuto(HoldemReplayStep last)
    {
        if (!playing)
        {
            return;
        }

        autoClock += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (autoClock < AutoStepSeconds)
        {
            return;
        }

        autoClock = 0f;
        if (step >= last)
        {
            playing = false;
            return;
        }

        step = HoldemReplay.Advance(step, 1, last);
        CasinoSfx.Play(Core.Notifications.UiSound.CardSnap);
    }

    private void DrawReplayBoard(ImDrawListPtr drawList, CasinoHoldemHistoryHandDto hand, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var cardWidth = ReplayCardWidth * scale;
        var gap = 4f * scale;
        var total = cardWidth * HoldemRules.BoardSize + gap * (HoldemRules.BoardSize - 1);
        var left = origin.X + (width - total) * 0.5f + cardWidth * 0.5f;
        var centerY = origin.Y + PlayingCards.HeightFor(cardWidth) * 0.5f;
        var board = hand.Board ?? Array.Empty<int>();
        var visible = HoldemReplay.BoardCardsAt(step);
        for (var index = 0; index < HoldemRules.BoardSize; index++)
        {
            var center = new Vector2(left + index * (cardWidth + gap), centerY);
            if (index < visible && index < board.Length)
            {
                HoldemArt.DrawCard(drawList, center, cardWidth, board[index], true, 1f, scale);
                continue;
            }

            var half = new Vector2(cardWidth * 0.5f, PlayingCards.HeightFor(cardWidth) * 0.5f);
            PlayingCards.DrawSlot(drawList, new Rect(center - half, center + half), PlayingCards.RoundingFor(cardWidth),
                scale);
        }

        var pot = texts.Number(L.Holdem.PotTotal, hand.PotTotal);
        var potY = origin.Y + PlayingCards.HeightFor(cardWidth) + Metrics.Space.Sm * scale;
        Typography.DrawCentered(drawList, new Vector2(origin.X + width * 0.5f, potY + 8f * scale), pot,
            CasinoColors.Money, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(width, PlayingCards.HeightFor(cardWidth) + 28f * scale));
    }

    private void DrawReplaySeats(ImDrawListPtr drawList, AppSkin ui, CasinoHoldemHistoryHandDto hand, float width,
        float scale)
    {
        var seats = hand.Seats ?? Array.Empty<CasinoHoldemHistorySeatDto>();
        var showdown = step == HoldemReplayStep.Showdown;
        var cardWidth = 22f * scale;
        for (var index = 0; index < seats.Length; index++)
        {
            var seat = seats[index];
            var origin = ImGui.GetCursorScreenPos();
            var rowHeight = SeatRowHeight * scale;
            var centerY = origin.Y + rowHeight * 0.5f;
            var cardsLeft = origin.X + cardWidth * 0.5f;
            var cards = seat.Cards ?? Array.Empty<int>();
            for (var card = 0; card < HoldemRules.HoleCards; card++)
            {
                var value = card < cards.Length ? cards[card] : HoldemRules.FaceDown;
                HoldemArt.DrawCard(drawList, new Vector2(cardsLeft + card * (cardWidth + 2f * scale), centerY),
                    cardWidth, value, value >= 0, 1f, scale, seat.Folded ? 0.45f : 1f,
                    showdown && seat.Won > 0 && value >= 0);
            }

            var textLeft = cardsLeft + cardWidth * 2f + Metrics.Space.Sm * scale;
            var amountText = showdown && seat.Won > 0
                ? texts.Signed(seat.Won - seat.Committed)
                : texts.Number(L.Holdem.Committed, seat.Committed);
            var amountSize = Typography.Measure(amountText, TextStyles.FootnoteEmphasized);
            var nameWidth = MathF.Max(0f, origin.X + width - textLeft - amountSize.X - Metrics.Space.Sm * scale);
            var name = Typography.FitText(seat.DisplayName, nameWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(textLeft, centerY - Typography.LineHeight(TextStyles.Footnote)),
                name, seat.Folded ? ui.MutedInk : ui.TitleInk, TextStyles.Footnote);
            var detail = SeatDetail(seat, showdown);
            if (detail.Length > 0)
            {
                Typography.Draw(drawList, new Vector2(textLeft, centerY),
                    Typography.FitText(detail, nameWidth, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
            }

            Typography.Draw(drawList, new Vector2(origin.X + width - amountSize.X, centerY - amountSize.Y * 0.5f),
                amountText, showdown && seat.Won > seat.Committed ? CasinoColors.Money : ui.BodyInk,
                TextStyles.FootnoteEmphasized);
            ImGui.Dummy(new Vector2(width, rowHeight));
        }
    }

    private static string SeatDetail(CasinoHoldemHistorySeatDto seat, bool showdown)
    {
        if (seat.Folded)
        {
            return Loc.T(L.Holdem.ActionFold);
        }

        return showdown && seat.Strength >= 0 ? HoldemHandNames.Describe(seat.Strength) : string.Empty;
    }

    private void DrawReplayControls(ImDrawListPtr drawList, AppSkin ui, float width, HoldemReplayStep last,
        float scale)
    {
        ImGui.Dummy(new Vector2(width, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var radius = 17f * scale;
        var center = new Vector2(origin.X + width * 0.5f, origin.Y + radius);
        if (RoundButton.Icon(drawList, center - new Vector2(radius * 2.6f, 0f), radius,
                IconGlyph.Of(FontAwesomeIcon.ChevronLeft), ui.Ink, ButtonStyle.Gray, Loc.T(L.Holdem.ReplayBack),
                HoverLabelSide.Above, step > HoldemReplayStep.Preflop))
        {
            step = HoldemReplay.Advance(step, -1, last);
            playing = false;
        }

        if (RoundButton.Icon(drawList, center, radius,
                IconGlyph.Of(playing ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play), ui.Ink, ButtonStyle.Tinted,
                Loc.T(playing ? L.Holdem.ReplayPause : L.Holdem.ReplayPlay), HoverLabelSide.Above))
        {
            playing = !playing;
            autoClock = 0f;
            if (playing && step >= last)
            {
                step = HoldemReplayStep.Preflop;
            }
        }

        if (RoundButton.Icon(drawList, center + new Vector2(radius * 2.6f, 0f), radius,
                IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.Ink, ButtonStyle.Gray, Loc.T(L.Holdem.ReplayNext),
                HoverLabelSide.Above, step < last))
        {
            step = HoldemReplay.Advance(step, 1, last);
            playing = false;
        }

        ImGui.Dummy(new Vector2(width, radius * 2f + Metrics.Space.Md * scale));
    }

    private void DrawFairness(AppSkin ui, CasinoHoldemHistoryHandDto hand, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var line = texts.Numbers(L.Holdem.BlindsLine, hand.SmallBlind, hand.BigBlind);
        var rake = hand.Rake > 0 ? texts.Number(L.Holdem.RakeLine, hand.Rake) : string.Empty;
        var height = Typography.DrawWrappedLeft(origin, line, ui.BodyInk, TextStyles.Footnote, width);
        if (rake.Length > 0)
        {
            height += Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + height), rake, ui.MutedInk,
                TextStyles.Footnote, width);
        }

        if (hand.Seed.Length > 0)
        {
            height += Typography.DrawWrappedLeft(new Vector2(origin.X, origin.Y + height),
                texts.Named(L.Holdem.SeedLine, hand.Seed), ui.BodyInk, TextStyles.Footnote, width);
        }

        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Lg * scale));
    }
}
