using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Social;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class HoldemTable
{
    private const float TagLift = 10f;
    private const float HeroCardOverlap = 0.66f;
    private const float HeroHoverLift = 6f;
    private const float PotPanelWidth = 230f;
    private const int PotPanelMaxLines = 8;

    private static readonly string[] ReactionGlyphs =
    {
        IconGlyph.Of(FontAwesomeIcon.ThumbsUp),
        IconGlyph.Of(FontAwesomeIcon.Heart),
        IconGlyph.Of(FontAwesomeIcon.Laugh),
        IconGlyph.Of(FontAwesomeIcon.Fire),
        IconGlyph.Of(FontAwesomeIcon.GlassCheers),
        IconGlyph.Of(FontAwesomeIcon.Handshake),
    };

    private static readonly string PeekGlyph = IconGlyph.Of(FontAwesomeIcon.Eye);

    private readonly int[] dealtSeats = new int[HoldemRules.MaxSeats];
    private readonly int[] heroCards = new int[HoldemRules.HoleCards + HoldemRules.BoardSize];
    private string[] potLines = Array.Empty<string>();
    private CasinoHoldemRoomStateDto? potLinesSource;
    private LanguageInfo? potLinesLanguage;
    private bool payoutSounded;

    private bool Flying(int tag)
    {
        for (var index = 0; index < flights.Count; index++)
        {
            if (flights.Tag(index) == tag)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShowingWinners(CasinoHoldemRoomStateDto board) =>
        board.Phase >= HoldemPhases.Showdown && board.Phase != HoldemPhases.Voided
        && string.Equals(winnersHand, board.HandId, StringComparison.Ordinal);

    private Vector2 HeroCardCenter(int slot, float scale)
    {
        var width = HoldemTableLayout.HeroCardWidth * scale;
        var step = width * HeroCardOverlap;
        return layout.HeroCardsCenter + new Vector2((slot - 0.5f) * step, 0f);
    }

    private Vector2 SeatCardCenter(int seat, int slot, float scale)
    {
        var width = HoldemTableLayout.SeatCardWidth * scale;
        return layout.SeatCardsAnchor(seat) + new Vector2((slot - 0.5f) * width * 0.6f, 0f);
    }

    private void DrawBoard(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, float scale)
    {
        var cards = board.Board ?? Array.Empty<int>();
        var width = layout.BoardCardWidth;
        var winners = ShowingWinners(board);
        for (var index = 0; index < HoldemRules.BoardSize; index++)
        {
            var center = layout.BoardSlot(index);
            if (Flying(HoldemPlayback.BoardTagBase + index))
            {
                continue;
            }

            if (index < cards.Length && playback.BoardDealt(index) && PlayingCards.IsCard(cards[index]))
            {
                var card = cards[index];
                HoldemArt.DrawCard(drawList, center, width, card, true, 1f, scale, 1f, winners && winningCards[card]);
                continue;
            }

            var half = new Vector2(width * 0.5f, PlayingCards.HeightFor(width) * 0.5f);
            PlayingCards.DrawSlot(drawList, new Rect(center - half, center + half), PlayingCards.RoundingFor(width),
                scale);
        }
    }

    private void DrawPot(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, float scale)
    {
        if (board.PotTotal <= 0)
        {
            potsOpen = false;
            return;
        }

        var center = layout.PotCenter;
        var text = texts.Number(L.Holdem.PotTotal, board.PotTotal);
        HoldemArt.DrawAmount(drawList, center, text, CasinoColors.Money, practice, scale, TextStyles.Headline);
        var size = Typography.Measure(text, TextStyles.Headline);
        var halfWidth = size.X * 0.5f + Typography.LineHeight(TextStyles.Headline) + 6f * scale;
        DrawPotScatter(drawList, board, center, halfWidth, scale);
        var pots = board.Pots?.Length ?? 0;
        if (pots <= 1)
        {
            potsOpen = false;
            return;
        }

        var chipCenter = new Vector2(center.X + halfWidth + 14f * scale, center.Y);
        HoldemArt.DrawTag(drawList, chipCenter, Loc.T(L.Holdem.SidePotShort), CasinoColors.LightB, scale);
        var hitMin = new Vector2(center.X - halfWidth, center.Y - 12f * scale);
        var hitMax = new Vector2(chipCenter.X + 18f * scale, center.Y + 12f * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(hitMin, hitMax), Loc.T(L.Holdem.SidePotsHint), HoverLabelSide.Below);
        }

        if (UiInteract.Click(hitMin, hitMax, hovered))
        {
            potsOpen = !potsOpen;
        }
    }

    private void DrawPotScatter(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, Vector2 center,
        float halfWidth, float scale)
    {
        if (!string.Equals(scatterHand, board.HandId, StringComparison.Ordinal))
        {
            scatterHand = board.HandId;
            HoldemPotScatter.Fill(HoldemPotScatter.SeedOf(board.HandIndex), scatter);
        }

        var count = Math.Min(scatter.Length, 2 + (int)Math.Min(4, Math.Log10(Math.Max(1, board.PotTotal))));
        var color = practice ? CasinoColors.Practice : BlackjackTableArt.TopChipColor(board.PotTotal);
        for (var index = 0; index < count; index++)
        {
            var offset = scatter[index];
            var side = offset.X < 0f ? -1f : 1f;
            var position = new Vector2(center.X + side * (halfWidth + 6f * scale + MathF.Abs(offset.X) * 10f * scale),
                center.Y + offset.Y * 14f * scale);
            BlackjackTableArt.DrawDisc(drawList, position, 5f * scale, color, 1f);
        }
    }

    private void DrawPotDetails(ImDrawListPtr drawList, AppSkin ui, CasinoHoldemRoomStateDto board, float scale)
    {
        RefreshPotLines(board);
        if (potLines.Length == 0)
        {
            return;
        }

        var width = MathF.Min(PotPanelWidth * scale, layout.Felt.Width * 0.86f);
        var pad = Metrics.Space.Sm * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Caption1);
        var height = pad * 2f + lineHeight * potLines.Length;
        var min = new Vector2(layout.PotCenter.X - width * 0.5f, layout.PotCenter.Y + 14f * scale);
        var max = new Vector2(min.X + width, min.Y + height);
        Material.LiquidGlass(drawList, min, max, Metrics.Radius.Md * scale, scale, GlassTone.Dark, 0f);
        for (var index = 0; index < potLines.Length; index++)
        {
            var line = Typography.FitText(potLines[index], width - pad * 2f, TextStyles.Caption1);
            var ink = index % 2 == 0 ? CasinoColors.Money : CasinoColors.InkBody;
            Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad + index * lineHeight), line, ink,
                TextStyles.Caption1);
        }

        if (UiInteract.ClickedOutside(min, max))
        {
            potsOpen = false;
        }
    }

    private void RefreshPotLines(CasinoHoldemRoomStateDto board)
    {
        if (ReferenceEquals(potLinesSource, board) && ReferenceEquals(potLinesLanguage, Loc.Current))
        {
            return;
        }

        potLinesSource = board;
        potLinesLanguage = Loc.Current;
        var pots = board.Pots ?? Array.Empty<CasinoHoldemPotDto>();
        var count = Math.Min(pots.Length, PotPanelMaxLines / 2);
        potLines = new string[count * 2];
        for (var index = 0; index < count; index++)
        {
            var pot = pots[index];
            potLines[index * 2] = index == 0
                ? Loc.T(L.Holdem.MainPot, NumberText.Group(pot.Amount))
                : Loc.T(L.Holdem.SidePot, GameNumber.Label(index), NumberText.Group(pot.Amount));
            potLines[index * 2 + 1] = EligibleNames(board, pot.Eligible);
        }
    }

    private string EligibleNames(CasinoHoldemRoomStateDto board, int[]? eligible)
    {
        if (eligible is null || eligible.Length == 0)
        {
            return string.Empty;
        }

        var names = new string[eligible.Length];
        for (var index = 0; index < eligible.Length; index++)
        {
            names[index] = eligible[index] == mySeat
                ? Loc.T(L.Holdem.You)
                : SeatAt(board, eligible[index])?.DisplayName ?? string.Empty;
        }

        return string.Join(", ", names);
    }

    private void DrawSeats(ImDrawListPtr drawList, AppSkin ui, CasinoHoldemRoomStateDto board, long remaining,
        float phase, float scale)
    {
        var count = 0;
        var seats = board.Seats ?? Array.Empty<CasinoHoldemSeatDto>();
        for (var index = 0; index < seats.Length && count < dealtSeats.Length; index++)
        {
            if (HoldemSeatStates.DealtIn(seats[index].State))
            {
                dealtSeats[count] = seats[index].SeatIndex;
                count++;
            }
        }

        HoldemRules.Blinds(new ReadOnlySpan<int>(dealtSeats, 0, count), board.Button, out var smallBlind,
            out var bigBlind);
        var window = board.WindowSeconds > 0 ? board.WindowSeconds : HoldemRules.TurnSeconds;
        for (var seat = 0; seat < layout.SeatCount; seat++)
        {
            var dto = SeatAt(board, seat);
            if (dto is null)
            {
                DrawEmptySeat(drawList, ui, seat, scale);
                continue;
            }

            var center = layout.SeatCenter(seat);
            var radius = layout.PuckFor(seat);
            var acting = board.CursorSeat == seat && HoldemPhases.Betting(board.Phase);
            if (acting)
            {
                BlackjackTableArt.DrawActingGlow(drawList, center, radius * 2.2f, CasinoColors.LightA);
            }

            var dim = dto.State is HoldemSeatStates.Folded or HoldemSeatStates.SittingOut or HoldemSeatStates.Waiting
                || !dto.Connected;
            AvatarView.DrawRemote(drawList, center, radius, ui.Theme, dto.DisplayName, string.Empty, dto.AvatarUrl,
                images, lodestone, 1f, 32, dim ? 0.45f : 1f, Frames.Of(dto.FrameId));
            if (acting)
            {
                TurnTimerRing.Draw(drawList, center, radius + 4f * scale, remaining, window, CasinoColors.LightA,
                    scale);
            }

            if (!dto.Connected)
            {
                drawList.AddCircleFilled(new Vector2(center.X + radius * 0.7f, center.Y - radius * 0.7f), 3.4f * scale,
                    ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.5f + 0.4f * MathF.Sin(phase * 3f) }), 12);
            }

            DrawSeatTop(drawList, board, dto, center, radius, scale);
            DrawCapsule(drawList, dto, acting, scale);
            if (board.Button == seat && board.HandId.Length > 0)
            {
                HoldemArt.DrawDealerButton(drawList, layout.DealerButton(seat), scale);
            }

            DrawSeatBet(drawList, board, dto, seat == smallBlind, seat == bigBlind, scale);
            if (seat != mySeat)
            {
                DrawSeatCards(drawList, board, dto, scale);
            }

            DrawSeatResult(drawList, board, dto, scale);
        }
    }

    private void DrawEmptySeat(ImDrawListPtr drawList, AppSkin ui, int seat, float scale)
    {
        var center = layout.SeatCenter(seat);
        var open = mySeat < 0 && !seatFlow.Busy;
        if (!open)
        {
            drawList.AddCircle(center, layout.PuckFor(seat), ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.35f }),
                32, MathF.Max(1f, scale));
            return;
        }

        var radius = MathF.Max(layout.PuckFor(seat), HoldemTableLayout.SitSpotRadius * scale);
        var corner = new Vector2(radius, radius);
        var hovered = UiInteract.Hover(center - corner, center + corner);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoldemArt.DrawSitSpot(drawList, center, radius, Loc.T(L.Holdem.Sit), hovered, scale);
        if (UiInteract.Click(center - corner, center + corner, hovered))
        {
            BeginBuyIn(seat);
        }
    }

    private void DrawSeatTop(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        Vector2 center, float radius, float scale)
    {
        var top = new Vector2(center.X, center.Y - radius - TagLift * scale);
        var action = ActionLabel(dto);
        if (action.Key is not null && HoldemPhases.Betting(board.Phase) && dto.LastAction.Length > 0)
        {
            var tint = dto.State == HoldemSeatStates.Folded ? CasinoColors.InkMuted : CasinoColors.InkTitle;
            HoldemArt.DrawTag(drawList, top, Loc.T(action), tint, scale);
            return;
        }

        var title = TitleOf(dto.Title);
        if (title != BalanceTitle.None)
        {
            StatusTitle.Draw(drawList, top, title, HoldemTableLayout.CapsuleWidth * scale, scale);
        }
    }

    internal static LocString ActionLabel(CasinoHoldemSeatDto dto)
    {
        if (dto.State == HoldemSeatStates.AllIn)
        {
            return L.Holdem.ActionAllIn;
        }

        return dto.LastAction switch
        {
            HoldemActions.FoldVerb or HoldemActions.AutoFoldVerb => L.Holdem.ActionFold,
            HoldemActions.CheckVerb or HoldemActions.AutoCheckVerb => L.Holdem.ActionCheck,
            HoldemActions.CallVerb => L.Holdem.ActionCall,
            HoldemActions.BetVerb => L.Holdem.ActionBet,
            HoldemActions.RaiseVerb => L.Holdem.ActionRaise,
            HoldemActions.AllInVerb => L.Holdem.ActionAllIn,
            HoldemActions.SmallBlindVerb => L.Holdem.SmallBlindShort,
            HoldemActions.BigBlindVerb or HoldemActions.PostVerb => L.Holdem.BigBlindShort,
            _ => default,
        };
    }

    internal static BalanceTitle TitleOf(string title) => title switch
    {
        "shark" => BalanceTitle.Shark,
        "high_roller" => BalanceTitle.HighRoller,
        "vip" => BalanceTitle.Vip,
        "whale" => BalanceTitle.Whale,
        "legend" => BalanceTitle.Legend,
        _ => BalanceTitle.None,
    };

    private void DrawCapsule(ImDrawListPtr drawList, CasinoHoldemSeatDto dto, bool acting, float scale)
    {
        var seat = dto.SeatIndex;
        var rect = layout.IsBottom(seat) ? HeroCapsuleRect(seat, scale) : layout.CapsuleRect(seat);
        var radius = rect.Height * 0.4f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(0.03f, 0.03f, 0.06f, 0.78f)));
        if (acting)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(CasinoColors.LightA),
                MathF.Max(1f, 1.2f * scale));
        }

        var inner = rect.Width - 8f * scale;
        var nameStyle = TextStyles.Footnote;
        var stackStyle = TextStyles.SubheadlineEmphasized;
        var nameLine = Typography.FitText(seat == mySeat ? Loc.T(L.Holdem.You) : dto.DisplayName, inner, nameStyle);
        var folded = dto.State is HoldemSeatStates.Folded or HoldemSeatStates.SittingOut;
        Typography.DrawCentered(drawList,
            new Vector2(rect.Center.X, rect.Min.Y + 1f * scale + Typography.LineHeight(nameStyle) * 0.5f), nameLine,
            folded ? CasinoColors.InkBody : CasinoColors.InkTitle, nameStyle);
        var second = SecondLine(dto, out var ink);
        Typography.DrawCentered(drawList,
            new Vector2(rect.Center.X, rect.Max.Y - 1f * scale - Typography.LineHeight(stackStyle) * 0.5f),
            Typography.FitText(second, inner, stackStyle), ink, stackStyle);
        DrawTimeBankPips(drawList, dto, rect, acting, scale);
    }

    private Rect HeroCapsuleRect(int seat, float scale)
    {
        var center = layout.SeatCenter(seat);
        var radius = layout.PuckFor(seat);
        var half = new Vector2(HoldemTableLayout.CapsuleWidth, HoldemTableLayout.CapsuleHeight) * 0.5f * scale;
        var capsuleCenter = new Vector2(center.X + radius + 6f * scale + half.X, center.Y);
        return new Rect(capsuleCenter - half, capsuleCenter + half);
    }

    private string SecondLine(CasinoHoldemSeatDto dto, out Vector4 ink)
    {
        switch (dto.State)
        {
            case HoldemSeatStates.AllIn:
                ink = CasinoColors.Money;
                return Loc.T(L.Holdem.ActionAllIn);
            case HoldemSeatStates.SittingOut:
                ink = CasinoColors.InkBody;
                return Loc.T(L.Holdem.SittingOut);
            case HoldemSeatStates.Waiting:
                ink = CasinoColors.InkBody;
                return Loc.T(L.Holdem.WaitingShort);
        }

        ink = practice ? CasinoColors.Practice : CasinoColors.Money;
        return NumberText.Compact(dto.Stack);
    }

    private static void DrawTimeBankPips(ImDrawListPtr drawList, CasinoHoldemSeatDto dto, in Rect capsule, bool acting,
        float scale)
    {
        if (!acting || dto.TimeBankLeft <= 0)
        {
            return;
        }

        var radius = 2.2f * scale;
        var step = radius * 2.8f;
        var left = capsule.Center.X - step * (HoldemRules.TimeBankUses - 1) * 0.5f;
        var y = capsule.Max.Y + radius + 2f * scale;
        for (var pip = 0; pip < HoldemRules.TimeBankUses; pip++)
        {
            var filled = pip < dto.TimeBankLeft;
            drawList.AddCircleFilled(new Vector2(left + pip * step, y), radius,
                ImGui.GetColorU32(filled ? CasinoColors.LightB : CasinoColors.InkMuted with { W = 0.35f }), 10);
        }
    }

    private void DrawSeatBet(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        bool smallBlind, bool bigBlind, float scale)
    {
        var anchor = layout.BetAnchor(dto.SeatIndex);
        if (board.Phase == HoldemPhases.Preflop && (smallBlind || bigBlind))
        {
            HoldemArt.DrawTag(drawList, anchor - new Vector2(0f, 14f * scale),
                Loc.T(smallBlind ? L.Holdem.SmallBlindShort : L.Holdem.BigBlindShort), CasinoColors.LightB, scale);
        }

        if (dto.Bet <= 0)
        {
            return;
        }

        HoldemArt.DrawAmount(drawList, anchor, NumberText.Compact(dto.Bet), CasinoColors.Money, practice, scale,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawSeatCards(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        float scale)
    {
        var seat = dto.SeatIndex;
        var cards = dto.Cards;
        var real = HoldemPlayback.HasRealCards(cards);
        if (!HoldemSeatStates.Live(dto.State) && !(dto.Shown && real))
        {
            return;
        }

        if (!playback.HoleDealt(seat) && !real)
        {
            return;
        }

        var winners = ShowingWinners(board);
        for (var slot = 0; slot < HoldemRules.HoleCards; slot++)
        {
            if (Flying(HoldemPlayback.HoleTagBase + seat * HoldemRules.HoleCards + slot))
            {
                continue;
            }

            if (real && cards!.Length > slot)
            {
                var width = HoldemTableLayout.ShownCardWidth * scale;
                var center = layout.SeatCardsAnchor(seat) + new Vector2((slot - 0.5f) * width * 0.9f, 0f);
                HoldemArt.DrawCard(drawList, center, width, cards[slot], true, 1f, scale, 1f,
                    winners && winningCards[cards[slot]]);
                continue;
            }

            HoldemArt.DrawCard(drawList, SeatCardCenter(seat, slot, scale), HoldemTableLayout.SeatCardWidth * scale,
                HoldemRules.FaceDown, false, 1f, scale);
        }
    }

    private void DrawSeatResult(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        float scale)
    {
        if (!ShowingWinners(board) || !HoldemPhases.Over(board.Phase) || payoutClock < ChipFlightSeconds)
        {
            return;
        }

        var won = payoutAmounts[dto.SeatIndex];
        if (won <= 0)
        {
            return;
        }

        var anchor = layout.BetAnchor(dto.SeatIndex);
        var label = texts.Signed(won);
        HoldemArt.DrawTag(drawList, anchor, label, CasinoColors.Money, scale);
        if (dto.HandRank >= 0 && dto.Shown)
        {
            var name = HoldemHandNames.Describe(dto.HandRank);
            var width = HoldemTableLayout.CapsuleWidth * 1.4f * scale;
            Typography.DrawCentered(drawList, anchor + new Vector2(0f, 16f * scale),
                Typography.FitText(name, width, TextStyles.Footnote), CasinoColors.MoneyHighlight, TextStyles.Footnote);
        }
    }

    private void DrawHero(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine,
        CasinoHoldemSeatDto? hero, float delta, float scale)
    {
        if (hero is null || mySeat < 0)
        {
            return;
        }

        var cards = HeroHole(mine, hero);
        if (cards is null || (!HoldemSeatStates.Live(hero.State) && !hero.Shown))
        {
            peel.SnapTo(0f);
            return;
        }

        if (!playback.HoleDealt(mySeat) && !HoldemPlayback.HasRealCards(hero.Cards))
        {
            return;
        }

        var width = HoldemTableLayout.HeroCardWidth * scale;
        var height = PlayingCards.HeightFor(width);
        var center = layout.HeroCardsCenter;
        var half = new Vector2(width * (0.5f + HeroCardOverlap * 0.5f), height * 0.5f);
        var hovered = UiInteract.Hover(center - half, center + half);
        if (hovered)
        {
            UiInteract.ReportGestureSurface();
        }

        var revealed = hero.Shown || board.Phase >= HoldemPhases.Showdown;
        var target = hovered || revealed ? 1f : 0f;
        var value = peel.Step(target, PeelSmoothing, delta);
        var squash = MathF.Abs(MathF.Cos(value * MathF.PI));
        var faceUp = value > 0.5f;
        var lift = new Vector2(0f, -HeroHoverLift * scale * value);
        var winners = ShowingWinners(board);
        for (var slot = 0; slot < HoldemRules.HoleCards && slot < cards.Length; slot++)
        {
            if (Flying(HoldemPlayback.HoleTagBase + mySeat * HoldemRules.HoleCards + slot))
            {
                continue;
            }

            HoldemArt.DrawCard(drawList, HeroCardCenter(slot, scale) + lift, width, cards[slot], faceUp, squash, scale,
                1f, winners && faceUp && winningCards[Math.Clamp(cards[slot], 0, PlayingCards.DeckSize - 1)]);
        }

        if (hovered)
        {
            HoverTooltip.Show(new Rect(center - half, center + half), Loc.T(L.Holdem.PeelHint), HoverLabelSide.Above);
            return;
        }

        if (!revealed && value < 0.05f)
        {
            PhoneIcon.Draw(drawList, HeroCardCenter(1, scale), PeekGlyph, CasinoColors.InkTitle with { W = 0.75f },
                width * 0.4f);
        }
    }

    private static int[]? HeroHole(CasinoHoldemYouDto? mine, CasinoHoldemSeatDto hero)
    {
        if (mine?.Cards is { Length: HoldemRules.HoleCards } own && HoldemPlayback.HasRealCards(own))
        {
            return own;
        }

        return HoldemPlayback.HasRealCards(hero.Cards) ? hero.Cards : null;
    }

    private void DrawShelf(ImDrawListPtr drawList, AppSkin ui, CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine,
        CasinoHoldemSeatDto? hero, float scale)
    {
        var shelf = layout.Shelf;
        if (inlineReason.Length > 0)
        {
            DrawReason(drawList, ui, shelf, scale);
            return;
        }

        Material.LiquidGlass(drawList, shelf.Min, shelf.Max, shelf.Height * 0.5f, scale, GlassTone.Dark, 0f);
        var pad = Metrics.Space.Md * scale;
        var inner = new Rect(new Vector2(shelf.Min.X + pad, shelf.Min.Y), new Vector2(shelf.Max.X - pad, shelf.Max.Y));
        var cards = hero is null ? null : HeroHole(mine, hero);
        if (hero is null || cards is null || !HoldemSeatStates.Live(hero.State))
        {
            var message = Loc.T(hero is null ? L.Holdem.WatchingHint : StatusOf(board, hero));
            Typography.DrawCentered(drawList, inner.Center, Typography.FitText(message, inner.Width,
                TextStyles.Subheadline), CasinoColors.InkTitle, TextStyles.Subheadline);
            return;
        }

        var strength = StrengthOf(cards, board.Board);
        var label = HoldemHandNames.Describe(strength);
        var half = inner.Width * 0.5f;
        var labelStyle = TextStyles.SubheadlineEmphasized;
        var labelHeight = Typography.LineHeight(labelStyle);
        Typography.Draw(drawList, new Vector2(inner.Min.X, inner.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, half - Metrics.Space.Sm * scale, labelStyle), CasinoColors.InkTitle, labelStyle);
        var chance = mine?.WinChance ?? -1;
        if (chance < 0)
        {
            return;
        }

        var caption = Loc.T(L.Holdem.WinChance);
        var percent = HoldemHandNames.Percent(chance);
        var captionStyle = TextStyles.Footnote;
        var percentStyle = TextStyles.FootnoteEmphasized;
        var percentSize = Typography.Measure(percent, percentStyle);
        var right = inner.Max.X;
        var barLeft = inner.Min.X + half + Metrics.Space.Sm * scale;
        var barRight = right - percentSize.X - Metrics.Space.Xs * scale;
        var captionHeight = Typography.LineHeight(captionStyle);
        Typography.Draw(drawList, new Vector2(barLeft, inner.Center.Y - captionHeight),
            Typography.FitText(caption, right - barLeft, captionStyle), CasinoColors.InkBody, captionStyle);
        var bar = new Rect(new Vector2(barLeft, inner.Center.Y + 3f * scale),
            new Vector2(MathF.Max(barLeft, barRight), inner.Center.Y + 9f * scale));
        HoldemArt.DrawBar(drawList, bar, chance / (float)HoldemRules.WinChanceScale, CasinoColors.LightB);
        Typography.Draw(drawList, new Vector2(right - percentSize.X, bar.Center.Y - percentSize.Y * 0.5f), percent,
            CasinoColors.InkTitle, percentStyle);
    }

    internal int StrengthOf(int[] hole, int[]? board)
    {
        var count = 0;
        for (var index = 0; index < hole.Length && count < heroCards.Length; index++)
        {
            heroCards[count++] = hole[index];
        }

        if (board is not null)
        {
            for (var index = 0; index < board.Length && count < heroCards.Length; index++)
            {
                if (PlayingCards.IsCard(board[index]))
                {
                    heroCards[count++] = board[index];
                }
            }
        }

        return Strength(new ReadOnlySpan<int>(heroCards, 0, count));
    }

    internal static int Strength(ReadOnlySpan<int> cards)
    {
        if (cards.Length >= HoldemHands.HandSize)
        {
            return HoldemHands.Evaluate(cards);
        }

        if (cards.Length < HoldemRules.HoleCards)
        {
            return -1;
        }

        var first = HoldemHands.RankValue(cards[0]);
        var second = HoldemHands.RankValue(cards[1]);
        if (first == second)
        {
            return HoldemHands.Pack(HoldemHands.Pair, first, 0, 0, 0, 0);
        }

        return HoldemHands.Pack(HoldemHands.HighCard, Math.Max(first, second), Math.Min(first, second), 0, 0, 0);
    }

    private void DrawReason(ImDrawListPtr drawList, AppSkin ui, in Rect shelf, float scale)
    {
        var message = Loc.T(CasinoReasons.MessageFor(inlineReason));
        var height = CasinoNotice.Height(CasinoNoticeKind.Reason, string.Empty, message, shelf.Width, scale);
        var top = shelf.Max.Y - height;
        CasinoNotice.Draw(drawList, ui, CasinoNoticeKind.Reason, string.Empty, message, shelf.Min.X, top, shelf.Width,
            scale);
        var min = new Vector2(shelf.Min.X, top);
        var hovered = UiInteract.Hover(min, shelf.Max);
        if (UiInteract.Click(min, shelf.Max, hovered))
        {
            inlineReason = string.Empty;
        }
    }

    private static LocString StatusOf(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto hero) => hero.State switch
    {
        HoldemSeatStates.Folded => L.Holdem.StatusFolded,
        HoldemSeatStates.SittingOut => L.Holdem.StatusSittingOut,
        HoldemSeatStates.Waiting => L.Holdem.StatusWaitingBigBlind,
        HoldemSeatStates.AllIn => L.Holdem.StatusAllIn,
        _ => board.Phase == HoldemPhases.Waiting ? L.Holdem.RibbonWaiting : L.Holdem.StatusNextHand,
    };

    private void DrawChipFlights(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, float scale)
    {
        if (sweepClock < ChipFlightSeconds)
        {
            var progress = sweepClock / ChipFlightSeconds;
            for (var seat = 0; seat < layout.SeatCount; seat++)
            {
                var amount = sweepAmounts[seat];
                if (amount > 0)
                {
                    BlackjackTableArt.DrawFlightDisc(drawList, layout.BetAnchor(seat), layout.PotCenter, progress,
                        ChipColor(amount), scale);
                }
            }
        }

        if (payoutClock < 0f || payoutClock >= ChipFlightSeconds
            || !string.Equals(winnersHand, board.HandId, StringComparison.Ordinal))
        {
            payoutSounded = payoutClock >= 0f && payoutSounded;
            return;
        }

        if (!payoutSounded)
        {
            payoutSounded = true;
            CasinoSfx.Play(UiSound.ChipSlide);
        }

        var travel = payoutClock / ChipFlightSeconds;
        for (var seat = 0; seat < layout.SeatCount; seat++)
        {
            var amount = payoutAmounts[seat];
            if (amount > 0)
            {
                BlackjackTableArt.DrawFlightDisc(drawList, layout.PotCenter, layout.SeatCenter(seat), travel,
                    practice ? CasinoColors.Practice : CasinoColors.Money, scale);
            }
        }
    }

    private Vector4 ChipColor(long amount) => practice ? CasinoColors.Practice : BlackjackTableArt.TopChipColor(amount);

    private void DrawCardFlights(ImDrawListPtr drawList, float scale)
    {
        for (var index = 0; index < flights.Count; index++)
        {
            if (!flights.Visible(index))
            {
                continue;
            }

            var pose = flights.Pose(index);
            HoldemArt.DrawCard(drawList, pose.Center, pose.Width, flights.Card(index), pose.FaceUp, pose.Squash,
                scale);
        }
    }

    private void DrawReaction(ImDrawListPtr drawList, float scale)
    {
        if (reaction < 0 || mySeat < 0 || reactionClock >= ReactionSeconds)
        {
            return;
        }

        var alpha = Math.Clamp((ReactionSeconds - reactionClock) / 0.4f, 0f, 1f)
            * Math.Clamp(reactionClock / 0.15f, 0f, 1f);
        var center = layout.SeatCenter(mySeat);
        var rise = reactionClock * 8f * scale;
        HoldemArt.DrawBubble(drawList,
            new Vector2(center.X, center.Y - layout.PuckFor(mySeat) - TagLift * scale - rise), ReactionGlyphs[reaction],
            alpha, scale);
    }
}
