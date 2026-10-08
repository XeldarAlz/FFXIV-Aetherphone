using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float BankrollAspect = 0.52f;
    private const float BankrollPad = 20f;
    private const float BankrollLuminance = 0.30f;
    private const float BankrollGlassOpacity = 0.94f;
    private const float BankrollIconSize = 30f;
    private const float BankrollIconGap = 10f;
    private const float BankrollSubAlpha = 0.82f;
    private const float BankrollIdleAlpha = 0.55f;
    private const float BankrollIconWashAlpha = 0.22f;
    private const float BankrollButtonHeight = Button.RegularHeight;
    private const float BankrollButtonInkLuminance = 0.22f;
    private const float BankrollButtonGap = 8f;
    private const float TonightBarHeight = 8f;
    private const float TonightPad = 16f;
    private const float TonightRowGap = 10f;
    private const float TonightTrackAlpha = 0.10f;
    private const float SpinCardHeight = 72f;
    private const float SpinTile = 44f;
    private const float SpinBadgeHeight = 26f;
    private const float LiveCardHeight = 116f;
    private const float LiveTile = 36f;
    private const float LivePad = 14f;
    private const float RecentTile = 56f;
    private const float RecentLabelGap = 8f;
    private const int RecentLimit = 4;
    private const float GridTileHeight = 132f;
    private const float GridTile = 48f;
    private const float GridPad = 14f;
    private const long SessionPillAfterSeconds = 45 * 60;

    private readonly int[] recentPicked = new int[RecentLimit];
    private RollingValue chipRoll;
    private Spring tonightFill;
    private bool lobbyHistoryFailed;

    private void ResetLobby()
    {
        chipRoll.Snap((int)Math.Clamp(casino.State?.Sitting?.Stack ?? 0, 0, int.MaxValue));
        tonightFill.SnapTo(0f);
        lobbyHistoryFailed = false;
    }

    private void DrawLobbyTab(Rect body)
    {
        var scale = UiScale.Current;
        FreshenLobbyHistory();
        using (ImRaii.PushId("casino.lobby"))
        using (var surface = AppSurface.Begin(body))
        {
            lobbyRefresh.Draw(body, surface.Pull, surface.Dragging, casino.State is null, ui.MutedInk, refreshFloor);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            var state = casino.State;
            var tonight = state is null ? default : CasinoTonight.From(state);
            if (state is not null && tonight.Reached)
            {
                cursorY = DrawLimitReached(drawList, new Vector2(origin.X, cursorY), width, scale) +
                          CardGap * scale;
            }

            cursorY = DrawBankroll(drawList, new Vector2(origin.X, cursorY), width, scale);
            if (state is not null && !tonight.Reached)
            {
                cursorY = DrawTonight(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width, tonight,
                    scale);
            }

            cursorY = DrawStakeNotice(new Vector2(origin.X, cursorY + CardGap * scale), width, scale);
            var jackpot = casino.Jackpot;
            if (jackpot > 0)
            {
                if (jackpotRail.Draw(ui, jackpot, new Vector2(origin.X, cursorY), width, out var railBottom))
                {
                    OpenGame(CasinoGames.SlotsBird);
                }

                cursorY = railBottom + CardGap * scale;
            }

            cursorY = DrawDailySpinCard(drawList, new Vector2(origin.X, cursorY), width, scale);
            if (AnyRoomLive())
            {
                var liveTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Casino.LiveHeading), scale);
                cursorY = DrawLiveStrip(drawList, new Vector2(origin.X, liveTop), width, scale);
            }

            var recentCount = CasinoRecentGames.Collect(history.Rounds, FloorGameIds, recentPicked);
            if (recentCount > 0)
            {
                var recentTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                    Loc.T(L.Casino.RecentHeading), scale);
                using (ImRaii.PushId("recent"))
                {
                    cursorY = DrawRecentRow(drawList, new Vector2(origin.X, recentTop), width, recentCount, scale);
                }
            }

            var gridTop = SectionTitle(drawList, new Vector2(origin.X, cursorY), width,
                Loc.T(L.Casino.GamesHeading), scale);
            using (ImRaii.PushId("grid"))
            {
                cursorY = DrawGameGrid(drawList, new Vector2(origin.X, gridTop), width, scale);
            }

            cursorY = DrawRecordsCard(drawList, new Vector2(origin.X, cursorY), width, scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private void FreshenLobbyHistory()
    {
        if (lobbyHistoryFailed)
        {
            return;
        }

        history.EnsureFresh();
        if (!history.TakeLoadFailure())
        {
            return;
        }

        lobbyHistoryFailed = true;
        historyLoadFailed = true;
    }

    private float DrawBankroll(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = BankrollPad * scale;
        var iconSize = BankrollIconSize * scale;
        var amountStyle = TextStyles.WidgetDisplay;
        var buttonHeight = BankrollButtonHeight * scale;
        var natural = pad * 2f + iconSize + Typography.LineHeight(amountStyle) + buttonHeight + Metrics.Space.Md * scale;
        var height = MathF.Max(width * BankrollAspect, natural);
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("casino.chipbar", new Rect(min, max));

        var surface = Palette.ShadeToLuminance(ui.Accent with { W = 1f }, BankrollLuminance);
        var radius = Metrics.Radius.Widget * scale;
        Material.AccentGlass(drawList, min, max, radius, scale, surface, BankrollGlassOpacity);

        var state = casino.State;
        var stack = state?.Sitting?.Stack ?? 0;
        var ink = CasinoArt.White;
        var subInk = Palette.WithAlpha(ink, BankrollSubAlpha);
        var left = min.X + pad;
        var right = max.X - pad;
        var iconCenter = new Vector2(left + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f);
        drawList.AddCircleFilled(iconCenter, iconSize * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ink, BankrollIconWashAlpha)), 32);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, iconCenter, iconSize * 0.62f);

        var nameLeft = left + iconSize + BankrollIconGap * scale;
        var session = SessionElapsedLine();
        var sessionWidth = 0f;
        if (session.Length > 0)
        {
            var fittedSession = Typography.FitText(session, (right - nameLeft) * 0.5f, TextStyles.Footnote);
            var sessionSize = Typography.Measure(fittedSession, TextStyles.Footnote);
            sessionWidth = sessionSize.X + BankrollIconGap * scale;
            Typography.Draw(drawList, new Vector2(right - sessionSize.X, iconCenter.Y - sessionSize.Y * 0.5f),
                fittedSession, subInk, TextStyles.Footnote);
        }

        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(nameLeft, iconCenter.Y - nameHeight * 0.5f),
            Typography.FitText(Loc.T(L.Casino.PurseRow), MathF.Max(1f, right - sessionWidth - nameLeft),
                TextStyles.Headline), ink, TextStyles.Headline);

        chipRoll.Update((int)Math.Clamp(stack, 0, int.MaxValue),
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var amount = NumberText.Group(chipRoll.Display);
        var fitted = WidgetText.FitStyle(amount, amountStyle, right - left, true);
        var amountHeight = Typography.LineHeight(fitted);
        var bottomRowTop = max.Y - pad - buttonHeight;
        var amountTop = iconCenter.Y + iconSize * 0.5f +
                        (bottomRowTop - iconCenter.Y - iconSize * 0.5f - amountHeight) * 0.5f;
        WidgetText.Tabular(drawList, new Vector2(left, amountTop), amount,
            stack > 0 ? ink : Palette.WithAlpha(ink, BankrollIdleAlpha), fitted);

        DrawBankrollActions(drawList, left, right, bottomRowTop, buttonHeight, stack, scale);
        return max.Y;
    }

    private void DrawBankrollActions(ImDrawListPtr drawList, float left, float right, float top, float height,
        long stack, float scale)
    {
        var seated = stack > 0 && casino.State?.Sitting is not null;
        var busy = casino.MovingMoney;
        var buyLabel = seated ? Loc.T(L.Casino.TopUp) : Loc.T(L.Casino.BuyIn);
        var buyWidth = Button.WidthFor(buyLabel, ButtonSize.Regular);
        var cashLabel = Loc.T(L.Casino.CashOut);
        var cashWidth = seated ? Button.WidthFor(cashLabel, ButtonSize.Regular) : 0f;
        var gap = BankrollButtonGap * scale;
        var walletWidth = MathF.Max(0f, right - left - buyWidth - cashWidth - (seated ? gap * 2f : gap));
        DrawWalletLine(drawList, left, top, height, walletWidth);

        var buyRect = new Rect(new Vector2(right - buyWidth, top), new Vector2(right, top + height));
        if (DrawBankrollBuy(drawList, buyRect, buyLabel, !busy))
        {
            cashier.Open();
        }

        if (!seated)
        {
            return;
        }

        var cashRect = new Rect(new Vector2(buyRect.Min.X - gap - cashWidth, top),
            new Vector2(buyRect.Min.X - gap, top + height));
        if (Button.Draw(drawList, cashRect, cashLabel, ui.Ink with { Ink = CasinoArt.White }, ButtonStyle.Gray,
                enabled: !busy, id: "casino.bankroll.cash"))
        {
            AskCashOut(casino.State!.Sitting!);
        }
    }

    private bool DrawBankrollBuy(ImDrawListPtr drawList, Rect rect, string label, bool enabled)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ui.Ink.WithAccent(CasinoArt.White), ButtonStyle.Prominent,
            ButtonRole.Normal, enabled, hovered, ImGui.GetID("casino.bankroll.buy"));
        var labelInk = Palette.ShadeToLuminance(ui.Accent with { W = 1f }, BankrollButtonInkLuminance);
        Button.DrawLabel(drawList, face with { LabelInk = labelInk with { W = face.LabelInk.W } }, label);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawWalletLine(ImDrawListPtr drawList, float left, float top, float height, float width)
    {
        if (width <= 0f)
        {
            return;
        }

        var label = Typography.FitText(Loc.T(L.Casino.WalletRow), width, TextStyles.Caption1);
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        var valueHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var blockTop = top + (height - labelHeight - valueHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, blockTop), label,
            Palette.WithAlpha(CasinoArt.White, BankrollSubAlpha), TextStyles.Caption1);
        var balance = NumberText.Group(coins.Wallet?.Balance ?? 0);
        var reserve = CurrencyGlyph.Reserve(valueHeight);
        var fittedBalance = Typography.FitText(balance, MathF.Max(1f, width - reserve),
            TextStyles.SubheadlineEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left, blockTop + labelHeight), fittedBalance,
            CurrencyKind.Coins, CasinoArt.White, TextStyles.SubheadlineEmphasized);
    }

    private string SessionElapsedLine()
    {
        var seenAtUnix = casino.SittingSeenAtUnix;
        if (seenAtUnix <= 0 || !casino.HasChips)
        {
            return string.Empty;
        }

        var elapsedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seenAtUnix;
        if (elapsedSeconds < SessionPillAfterSeconds)
        {
            return string.Empty;
        }

        var minutes = (int)Math.Min(elapsedSeconds / 60, int.MaxValue / 60);
        return texts.Duration(L.Casino.SessionPill, minutes * 60);
    }

    private float DrawTonight(ImDrawListPtr drawList, Vector2 origin, float width, in CasinoTonight tonight,
        float scale)
    {
        var pad = TonightPad * scale;
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var barHeight = TonightBarHeight * scale;
        var rowGap = TonightRowGap * scale;
        var barBlock = tonight.HasLimit ? barHeight + rowGap : 0f;
        var height = pad * 2f + headline + rowGap + barBlock + footnote;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("casino.tonight", new Rect(min, max));
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.tonight"), min, max, out var cardMin, out var cardMax);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);

        var left = min.X + pad;
        var right = max.X - pad;
        var result = TonightResult(tonight.NetLoss, out var resultInk);
        var resultWidth = DrawTonightResult(drawList, new Vector2(right, min.Y + pad), result, resultInk,
            tonight.NetLoss != 0, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, min.Y + pad),
            Typography.FitText(Loc.T(L.Casino.NetHeading), MathF.Max(1f, right - left - resultWidth - pad),
                TextStyles.Headline), ui.TitleInk, TextStyles.Headline);

        var footerTop = min.Y + pad + headline + rowGap;
        if (tonight.HasLimit)
        {
            var shown = tonightFill.Step(tonight.Fraction, Motion.PageSettle,
                MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
            CoinArt.Bar(drawList, new Vector2(left, footerTop), new Vector2(right, footerTop + barHeight), shown,
                Palette.WithAlpha(ui.TitleInk, TonightTrackAlpha), ToneInk(tonight.Tone));
            footerTop += barBlock;
        }

        var chevronCenter = new Vector2(right - 4f * scale, footerTop + footnote * 0.5f);
        CasinoArt.Chevron(drawList, chevronCenter, ui.MutedInk);
        var trailing = tonight.HasLimit
            ? texts.Number(L.Casino.TonightLimit, tonight.Limit)
            : Loc.T(L.Casino.TonightSetLimit);
        var trailingWidth = Typography.Measure(trailing, TextStyles.Footnote).X;
        var trailingRight = chevronCenter.X - CoinArt.ValueGap * scale;
        Typography.Draw(drawList, new Vector2(trailingRight - trailingWidth, footerTop), trailing,
            tonight.HasLimit ? ui.MutedInk : ui.Accent, TextStyles.Footnote);
        var leading = tonight.HasLimit
            ? texts.Number(L.Casino.RoomLeft, tonight.Headroom)
            : Loc.T(L.Casino.TonightNoLimit);
        var leadingInk = tonight.HasLimit && tonight.Tone != TonightTone.Calm ? ToneInk(tonight.Tone) : ui.MutedInk;
        Typography.Draw(drawList, new Vector2(left, footerTop),
            Typography.FitText(leading, MathF.Max(1f, trailingRight - trailingWidth - pad - left),
                TextStyles.Footnote), leadingInk, TextStyles.Footnote);

        if (UiInteract.Click(min, max, hovered))
        {
            OpenLimits();
        }

        return max.Y;
    }

    private static float DrawTonightResult(ImDrawListPtr drawList, Vector2 topRight, string text, Vector4 ink,
        bool withGlyph, in TextStyle style)
    {
        if (!withGlyph)
        {
            var width = Typography.Measure(text, style).X;
            Typography.Draw(drawList, new Vector2(topRight.X - width, topRight.Y), text, ink, style);
            return width;
        }

        var size = CurrencyGlyph.MeasureAmount(text, style);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(topRight.X - size.X, topRight.Y), text, CurrencyKind.Chips,
            ink, style);
        return size.X;
    }

    private string TonightResult(long netLoss, out Vector4 ink)
    {
        if (netLoss > 0)
        {
            ink = ui.TitleInk;
            return texts.Number(L.Casino.TonightResultDown, netLoss);
        }

        if (netLoss < 0)
        {
            ink = CoinArt.GainInk;
            return texts.Number(L.Casino.TonightResultUp, -netLoss);
        }

        ink = ui.MutedInk;
        return Loc.T(L.Casino.TonightResultEven);
    }

    private Vector4 ToneInk(TonightTone tone) => tone switch
    {
        TonightTone.Reached => theme.Danger,
        TonightTone.Close => AccentRing.Orange,
        _ => ui.Accent,
    };

    private float DrawLimitReached(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var resetsAtUnix = coins.Wallet?.ResetsAtUnix ?? 0;
        var body = resetsAtUnix > 0
            ? texts.Moment(L.Casino.LimitReachedBody, resetsAtUnix)
            : Loc.T(L.Casino.LimitReachedBodySoon);
        var title = Loc.T(L.Casino.LimitReachedTitle);
        var height = CoinArt.PanelHeight(title, body, width, scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("casino.tonight", new Rect(origin, max));
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.limitReached"), origin, max, out var cardMin,
            out var cardMax);
        CoinArt.Panel(drawList, ui, cardMin, cardMax.X - cardMin.X, cardMax.Y - cardMin.Y,
            FontAwesomeIcon.HandHoldingHeart, AccentRing.Rose, title, body, scale);
        if (UiInteract.Click(origin, max, hovered))
        {
            OpenLimits();
        }

        return max.Y;
    }

    private float DrawDailySpinCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var claim = Core.Casino.DailySpinStatus.Of(casinoSpin.Answer);
        var height = SpinCardHeight * scale;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("casino.spin", new Rect(min, max));
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.spin"), min, max, out var cardMin, out var cardMax);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);

        var pad = Metrics.Space.Lg * scale;
        var tile = SpinTile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, min.Y + height * 0.5f);
        CasinoArt.GameTile(drawList, CasinoGames.DailySpin, tileCenter, tile);

        var available = Core.Casino.DailySpinStatus.OffersWheel(claim);
        var trailing = max.X - pad;
        if (available)
        {
            var badge = Loc.T(L.Casino.SpinReadyBadge);
            var badgeHeight = SpinBadgeHeight * scale;
            var badgeWidth = Typography.Measure(badge, TextStyles.FootnoteEmphasized).X + badgeHeight;
            var badgeMin = new Vector2(trailing - badgeWidth, tileCenter.Y - badgeHeight * 0.5f);
            var badgeMax = new Vector2(trailing, tileCenter.Y + badgeHeight * 0.5f);
            Squircle.Fill(drawList, badgeMin, badgeMax, badgeHeight * 0.5f, ImGui.GetColorU32(ui.Accent));
            Typography.DrawCentered(drawList, (badgeMin + badgeMax) * 0.5f, badge, CasinoArt.White,
                TextStyles.FootnoteEmphasized);
            trailing = badgeMin.X;
        }
        else
        {
            CasinoArt.Chevron(drawList, new Vector2(trailing - 4f * scale, tileCenter.Y), ui.MutedInk);
            trailing -= 12f * scale;
        }

        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        CoinArt.Labels(drawList, textLeft, trailing - CoinArt.ValueGap * scale, tileCenter.Y,
            Loc.T(L.Casino.SpinCardTitle), DailySpinHint(claim), ui.TitleInk, ui.MutedInk, scale);
        if (UiInteract.Click(min, max, hovered))
        {
            OpenDailySpin();
        }

        return max.Y;
    }

    private string DailySpinHint(Core.Casino.DailySpinClaim claim)
    {
        var answer = casinoSpin.Answer;
        if (claim == Core.Casino.DailySpinClaim.Available || claim == Core.Casino.DailySpinClaim.Unknown)
        {
            return Loc.T(L.Casino.SpinCardHint);
        }

        if (claim == Core.Casino.DailySpinClaim.Denied && answer is not null)
        {
            return Loc.T(Core.Casino.CasinoReasons.MessageFor(answer.Reason));
        }

        return answer is not null && answer.NextSpinAtUnix > 0
            ? texts.Moment(L.Casino.SpinNextAt, answer.NextSpinAtUnix)
            : Loc.T(L.Casino.SpinNextSoon);
    }

    private float DrawLiveStrip(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = CardGap * scale;
        var cardWidth = (width - gap) * 0.5f;
        var height = LiveCardHeight * scale;
        var wheel = new Rect(origin, new Vector2(origin.X + cardWidth, origin.Y + height));
        if (DrawLiveCard(drawList, wheel, CasinoGames.Wheel, L.Casino.GameWheel, scale))
        {
            OpenGame(CasinoGames.Wheel);
        }

        var bingoMin = new Vector2(origin.X + cardWidth + gap, origin.Y);
        var bingoCard = new Rect(bingoMin, new Vector2(bingoMin.X + cardWidth, bingoMin.Y + height));
        if (DrawLiveCard(drawList, bingoCard, CasinoGames.Bingo, L.Casino.GameBingo, scale))
        {
            OpenGame(CasinoGames.Bingo);
        }

        return origin.Y + height;
    }

    private bool DrawLiveCard(ImDrawListPtr drawList, Rect card, string gameId, LocString name, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID(RoomOf(gameId)), card.Min, card.Max, out var cardMin,
            out var cardMax);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);
        var pad = LivePad * scale;
        var tile = LiveTile * scale;
        var tileCenter = new Vector2(card.Min.X + pad + tile * 0.5f, card.Min.Y + pad + tile * 0.5f);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);

        var occupancy = CrowdAt(gameId);
        var crowd = texts.Count(L.Casino.LivePlayers, occupancy);
        var crowdHeight = Typography.LineHeight(TextStyles.Footnote);
        var crowdLeft = tileCenter.X + tile * 0.5f + Metrics.Space.Sm * scale;
        var dotCenter = new Vector2(crowdLeft + CasinoArt.LiveDotRadius * scale, tileCenter.Y);
        var live = occupancy > 0;
        CasinoArt.LiveDot(drawList, dotCenter, scale, live ? ui.Accent : ui.MutedInk, live);
        var crowdX = dotCenter.X + (CasinoArt.LiveDotRadius + 5f) * scale;
        Typography.Draw(drawList, new Vector2(crowdX, tileCenter.Y - crowdHeight * 0.5f),
            Typography.FitText(crowd, MathF.Max(1f, card.Max.X - pad - crowdX), TextStyles.Footnote),
            live ? ui.Accent : ui.MutedInk, TextStyles.Footnote);

        var textWidth = card.Width - pad * 2f;
        var nameTop = tileCenter.Y + tile * 0.5f + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, nameTop),
            Typography.FitText(Loc.T(name), textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var phase = RoomPhaseLine(gameId, RoomOf(gameId), out var open);
        if (phase.Length > 0)
        {
            Typography.Draw(drawList,
                new Vector2(card.Min.X + pad, nameTop + Typography.LineHeight(TextStyles.Headline)),
                Typography.FitText(phase, textWidth, TextStyles.Footnote), open ? ui.Accent : ui.MutedInk,
                TextStyles.Footnote);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private float DrawRecentRow(ImDrawListPtr drawList, Vector2 origin, float width, int count, float scale)
    {
        var columnWidth = width / RecentLimit;
        var tile = RecentTile * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = tile + RecentLabelGap * scale + labelHeight;
        for (var index = 0; index < count; index++)
        {
            var gameIndex = recentPicked[index];
            var gameId = FloorGameIds[gameIndex];
            var columnMin = new Vector2(origin.X + index * columnWidth, origin.Y);
            var columnMax = new Vector2(columnMin.X + columnWidth, origin.Y + height);
            var hovered = UiInteract.Hover(columnMin, columnMax);
            var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var factor = PressFx.Scale(ImGui.GetID(gameId), pressed, Motion.PressScaleControl);
            var center = new Vector2(columnMin.X + columnWidth * 0.5f, origin.Y + tile * 0.5f);
            CasinoArt.GameTile(drawList, gameId, center, tile * factor);
            var name = Typography.FitText(Loc.T(FloorGameNames[gameIndex]), columnWidth - Metrics.Space.Xs * scale,
                TextStyles.Footnote);
            Typography.DrawCentered(drawList,
                new Vector2(center.X, origin.Y + tile + RecentLabelGap * scale + labelHeight * 0.5f), name,
                ui.BodyInk, TextStyles.Footnote);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(columnMin, columnMax, hovered))
            {
                OpenGame(gameId);
            }
        }

        return origin.Y + height;
    }

    private float DrawGameGrid(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var gap = CardGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var tileHeight = GridTileHeight * scale;
        var rows = (FloorGameIds.Length + 1) / 2;
        for (var index = 0; index < FloorGameIds.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            DrawGridTile(drawList, new Rect(min, new Vector2(min.X + tileWidth, min.Y + tileHeight)), index, scale);
        }

        return origin.Y + rows * tileHeight + (rows - 1) * gap;
    }

    private void DrawGridTile(ImDrawListPtr drawList, Rect card, int gameIndex, float scale)
    {
        var gameId = FloorGameIds[gameIndex];
        var hovered = CasinoArt.PressCard(ImGui.GetID(gameId), card.Min, card.Max, out var cardMin, out var cardMax);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);
        var pad = GridPad * scale;
        var tile = GridTile * scale;
        var tileCenter = new Vector2(card.Min.X + pad + tile * 0.5f, card.Min.Y + pad + tile * 0.5f);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);

        var crowd = CrowdAt(gameId);
        if (crowd > 0)
        {
            var count = Games.Framework.GameNumber.Label(crowd);
            var countSize = Typography.Measure(count, TextStyles.FootnoteEmphasized);
            var countX = card.Max.X - pad - countSize.X;
            Typography.Draw(drawList, new Vector2(countX, tileCenter.Y - tile * 0.5f), count, ui.Accent,
                TextStyles.FootnoteEmphasized);
            CasinoArt.LiveDot(drawList,
                new Vector2(countX - (CasinoArt.LiveDotRadius + 4f) * scale,
                    tileCenter.Y - tile * 0.5f + countSize.Y * 0.5f), scale, ui.Accent, true);
        }

        var textWidth = card.Width - pad * 2f;
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var lineTop = card.Max.Y - pad - footnote;
        var room = RoomOf(gameId);
        var open = false;
        var phase = room.Length > 0 ? RoomPhaseLine(gameId, room, out open) : string.Empty;
        var detail = phase.Length > 0 ? phase : MinimumStakeLine(gameId);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, lineTop),
            Typography.FitText(detail, textWidth, TextStyles.Footnote),
            phase.Length > 0 && open ? ui.Accent : ui.MutedInk, TextStyles.Footnote);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, lineTop - nameHeight),
            Typography.FitText(Loc.T(FloorGameNames[gameIndex]), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenGame(gameId);
        }
    }
}
