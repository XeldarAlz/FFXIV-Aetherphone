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
    private const float PotPanelWidth = 230f;
    private const float TextPad = 6f;
    private const float PipInset = 4f;
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

    private Vector2 HeroCardCenter(int slot)
    {
        var step = layout.HeroCardPixels * HoldemTableLayout.HeroCardOverlap;
        return layout.HeroCardsCenter + new Vector2((slot - 0.5f) * step, 0f);
    }

    private Vector2 SeatCardCenter(int seat, int slot) =>
        layout.SeatCardsAnchor(seat) + new Vector2((slot - 0.5f) * layout.SeatCardPixels * 0.6f, 0f);

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
        HoldemArt.DrawAmount(drawList, center, text, CasinoColors.Money, practice, scale, TextStyles.Title3);
        var size = Typography.Measure(text, TextStyles.Title3);
        var halfWidth = size.X * 0.5f + Typography.LineHeight(TextStyles.Title3) + 6f * scale;
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
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = pad * 2f + lineHeight * potLines.Length;
        var min = new Vector2(layout.PotCenter.X - width * 0.5f, layout.PotCenter.Y + 14f * scale);
        var max = new Vector2(min.X + width, min.Y + height);
        Material.LiquidGlass(drawList, min, max, Metrics.Radius.Md * scale, scale, GlassTone.Dark, 0f);
        for (var index = 0; index < potLines.Length; index++)
        {
            var line = Typography.FitText(potLines[index], width - pad * 2f, TextStyles.Footnote);
            var ink = index % 2 == 0 ? CasinoColors.Money : CasinoColors.InkBody;
            Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad + index * lineHeight), line, ink,
                TextStyles.Footnote);
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
        var window = board.WindowSeconds > 0 ? board.WindowSeconds : HoldemRules.TurnSeconds;
        for (var seat = 0; seat < layout.SeatCount; seat++)
        {
            var dto = SeatAt(board, seat);
            if (dto is null)
            {
                DrawEmptySeat(drawList, seat, scale);
                continue;
            }

            var center = layout.SeatCenter(seat);
            var radius = layout.PuckFor(seat);
            var acting = board.CursorSeat == seat && HoldemPhases.Betting(board.Phase);
            var hero = layout.IsHero(seat);
            if (acting && !hero)
            {
                BlackjackTableArt.DrawActingGlow(drawList, center, radius * 2.2f, CasinoColors.LightA);
            }

            if (hero)
            {
                DrawCapsule(drawList, board, dto, acting, scale);
            }
            else
            {
                DrawSeatCards(drawList, board, dto, false, scale);
            }

            var dim = dto.State is HoldemSeatStates.Folded or HoldemSeatStates.SittingOut or HoldemSeatStates.Waiting
                || !dto.Connected;
            AvatarView.DrawRemote(drawList, center, radius, ui.Theme, dto.DisplayName, string.Empty, dto.AvatarUrl,
                images, lodestone, 1f, 32, dim ? 0.45f : 1f, Frames.Of(dto.FrameId));
            if (acting)
            {
                TurnTimerRing.Draw(drawList, center, radius + 3.5f * scale, remaining, window, CasinoColors.LightA,
                    scale);
            }

            if (!dto.Connected)
            {
                drawList.AddCircleFilled(new Vector2(center.X + radius * 0.7f, center.Y - radius * 0.7f), 3.4f * scale,
                    ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.5f + 0.4f * MathF.Sin(phase * 3f) }), 12);
            }

            if (!hero)
            {
                DrawSeatCards(drawList, board, dto, true, scale);
                DrawCapsule(drawList, board, dto, acting, scale);
                DrawSeatTop(drawList, board, dto, scale);
            }

            if (board.Button == seat && board.HandId.Length > 0)
            {
                HoldemArt.DrawDealerButton(drawList, layout.DealerButton(seat), scale);
            }

            DrawSeatBet(drawList, dto, scale);
        }
    }

    private void DrawEmptySeat(ImDrawListPtr drawList, int seat, float scale)
    {
        var center = layout.SeatCenter(seat);
        var open = mySeat < 0 && !seatFlow.Busy;
        if (!open)
        {
            drawList.AddCircle(center, layout.PuckFor(seat), ImGui.GetColorU32(CasinoColors.InkMuted with { W = 0.35f }),
                32, MathF.Max(1f, scale));
            return;
        }

        if (SeatSpot.DrawEmpty(drawList, center, layout.SpotRadius, Loc.T(L.Holdem.Sit), CasinoColors.LightA, true,
                layout.SpotScale))
        {
            BeginBuyIn(seat);
        }
    }

    private void DrawSeatTop(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        float scale)
    {
        var top = layout.TagCenter(dto.SeatIndex);
        var action = ActionLabel(dto);
        if (action.Key is not null && HoldemPhases.Betting(board.Phase) && dto.LastAction.Length > 0)
        {
            var tint = dto.State == HoldemSeatStates.Folded ? CasinoColors.InkBody : CasinoColors.InkTitle;
            HoldemArt.DrawTag(drawList, top, Loc.T(action), tint, scale);
            return;
        }

        if (dto.Shown && board.Phase >= HoldemPhases.Showdown)
        {
            return;
        }

        var title = TitleOf(dto.Title);
        if (title != BalanceTitle.None)
        {
            StatusTitle.Draw(drawList, top, title, layout.PodWidth, scale);
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

    private void DrawCapsule(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        bool acting, float scale)
    {
        var seat = dto.SeatIndex;
        var hero = layout.IsHero(seat);
        var rect = layout.CapsuleRect(seat);
        StageText.Capsule(drawList, rect.Min, rect.Max);
        if (acting)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(CasinoColors.LightA),
                MathF.Max(1f, 1.2f * scale));
        }

        var pad = TextPad * scale;
        var left = rect.Min.X + pad;
        var right = rect.Max.X - pad;
        if (hero)
        {
            left = layout.SeatCenter(seat).X + layout.PuckFor(seat) + pad;
            if (board.Button == seat && board.HandId.Length > 0)
            {
                right -= HoldemTableLayout.DealerButtonRadius * 2f * scale + pad * 0.5f;
            }
        }

        var inner = MathF.Max(1f, right - left);
        var centerX = (left + right) * 0.5f;
        var nameStyle = TextStyles.Footnote;
        var stackStyle = TextStyles.Title3;
        var nameHeight = Typography.LineHeight(nameStyle);
        var stackHeight = Typography.LineHeight(stackStyle);
        var top = rect.Center.Y - (nameHeight + stackHeight) * 0.5f;
        var first = FirstLine(board, dto, hero, out var firstInk);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + nameHeight * 0.5f),
            Typography.FitText(first, inner, nameStyle), firstInk, nameStyle);
        var second = SecondLine(board, dto, out var ink);
        var fit = StageText.FitScale(second, inner, stackStyle, StageTextRole.Amount);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + nameHeight + stackHeight * 0.5f),
            Typography.FitText(second, inner, fit, stackStyle.Weight), ink, fit, stackStyle.Weight);
        DrawTimeBankPips(drawList, dto, rect, acting, scale);
    }

    private bool Paying(CasinoHoldemRoomStateDto board, int seat) =>
        ShowingWinners(board) && HoldemPhases.Over(board.Phase) && payoutClock >= ChipFlightSeconds
        && payoutAmounts[seat] > 0;

    private string FirstLine(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto, bool hero, out Vector4 ink)
    {
        if (Paying(board, dto.SeatIndex) && dto.HandRank >= 0 && dto.Shown)
        {
            ink = CasinoColors.MoneyHighlight;
            return HoldemHandNames.Describe(dto.HandRank);
        }

        var folded = dto.State is HoldemSeatStates.Folded or HoldemSeatStates.SittingOut;
        ink = folded ? CasinoColors.InkBody : CasinoColors.InkTitle;
        return hero ? Loc.T(L.Holdem.You) : dto.DisplayName;
    }

    private string SecondLine(CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto, out Vector4 ink)
    {
        if (Paying(board, dto.SeatIndex))
        {
            ink = CasinoColors.Money;
            return texts.Signed(payoutAmounts[dto.SeatIndex]);
        }

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
        var y = capsule.Max.Y - PipInset * scale;
        for (var pip = 0; pip < HoldemRules.TimeBankUses; pip++)
        {
            var filled = pip < dto.TimeBankLeft;
            drawList.AddCircleFilled(new Vector2(left + pip * step, y), radius,
                ImGui.GetColorU32(filled ? CasinoColors.LightB : CasinoColors.InkMuted with { W = 0.35f }), 10);
        }
    }

    private void DrawSeatBet(ImDrawListPtr drawList, CasinoHoldemSeatDto dto, float scale)
    {
        if (dto.Bet <= 0)
        {
            return;
        }

        var seat = dto.SeatIndex;
        var text = NumberText.Compact(dto.Bet);
        var width = layout.BetRect(seat).Width;
        var style = HoldemArt.AmountWidth(text, TextStyles.SubheadlineEmphasized, scale) <= width
            ? TextStyles.SubheadlineEmphasized
            : TextStyles.FootnoteEmphasized;
        HoldemArt.DrawAmount(drawList, layout.BetAnchor(seat), text, CasinoColors.Money, practice, scale, style);
    }

    private void DrawSeatCards(ImDrawListPtr drawList, CasinoHoldemRoomStateDto board, CasinoHoldemSeatDto dto,
        bool front, float scale)
    {
        var seat = dto.SeatIndex;
        var cards = dto.Cards;
        var real = HoldemPlayback.HasRealCards(cards);
        if (real != front || (!HoldemSeatStates.Live(dto.State) && !(dto.Shown && real)))
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
                var width = layout.ShownCardPixels;
                var center = layout.SeatCenter(seat)
                    + new Vector2((slot - 0.5f) * width * HoldemTableLayout.ShownCardStep, 0f);
                HoldemArt.DrawCard(drawList, center, width, cards[slot], true, 1f, scale, 1f,
                    winners && winningCards[cards[slot]]);
                continue;
            }

            HoldemArt.DrawCard(drawList, SeatCardCenter(seat, slot), layout.SeatCardPixels, HoldemRules.FaceDown, false,
                1f, scale);
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

        var width = layout.HeroCardPixels;
        var height = PlayingCards.HeightFor(width);
        var center = layout.HeroCardsCenter;
        var half = new Vector2(width * (0.5f + HoldemTableLayout.HeroCardOverlap * 0.5f), height * 0.5f);
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
        var lift = new Vector2(0f, -HoldemTableLayout.HeroLift * scale * value);
        var winners = ShowingWinners(board);
        for (var slot = 0; slot < HoldemRules.HoleCards && slot < cards.Length; slot++)
        {
            if (Flying(HoldemPlayback.HoleTagBase + mySeat * HoldemRules.HoleCards + slot))
            {
                continue;
            }

            HoldemArt.DrawCard(drawList, HeroCardCenter(slot) + lift, width, cards[slot], faceUp, squash, scale,
                1f, winners && faceUp && winningCards[Math.Clamp(cards[slot], 0, PlayingCards.DeckSize - 1)]);
        }

        if (hovered)
        {
            HoverTooltip.Show(new Rect(center - half, center + half), Loc.T(L.Holdem.PeelHint), HoverLabelSide.Above);
            return;
        }

        if (!revealed && value < 0.05f)
        {
            PhoneIcon.Draw(drawList, HeroCardCenter(1), PeekGlyph, CasinoColors.InkTitle with { W = 0.75f },
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
            DrawRowNote(drawList, inner, message, TextStyles.Subheadline, CasinoColors.InkTitle);
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
