using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float ResumeHeight = 76f;
    private const float ResumeTile = 46f;
    private const float FloorSectionGap = 22f;

    private bool lobbyHistoryFailed;

    private void ResetLobby()
    {
        stripHero.Snap(FloorBalance());
        lobbyHistoryFailed = false;
    }

    private long FloorBalance()
    {
        var state = casino.State;
        return (state?.Sitting?.Stack ?? 0) + (state?.TableSitting?.Stack ?? 0);
    }

    private void DrawLobbyTab(Rect body)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        FreshenLobbyHistory();
        using (ImRaii.PushId("casino.lobby"))
        using (var surface = AppSurface.Begin(body))
        {
            lobbyRefresh.Draw(body, surface.Pull, surface.Dragging, casino.State is null, ui.MutedInk, refreshFloor);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = CardGap * scale;
            var cursorY = DrawStakeNotice(origin, width, scale);
            var state = casino.State;
            if (state is not null && CasinoTonight.From(state).Reached)
            {
                cursorY = DrawLimitReached(drawList, new Vector2(origin.X, cursorY), width, scale) + gap;
            }

            cursorY = DrawHeroBlock(drawList, new Vector2(origin.X, cursorY), width, delta, scale) + gap;
            cursorY = DrawResume(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY = DrawCarousel(drawList, new Vector2(origin.X, cursorY), width, delta, scale);
            var tickerBottom = winsTicker.Draw(drawList, floor.Ticker, floor.TickerVersion,
                new Vector2(origin.X, cursorY), width, scale);
            cursorY = tickerBottom > cursorY ? tickerBottom + gap : cursorY;
            cursorY = DrawNearbyTables(drawList, cursorY, origin.X, width, scale);
            cursorY = DrawLiveNow(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY = DrawFloorExtras(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY += FloorSectionGap * scale * 0.5f;
            var shelvesBottom = shelves.Draw(drawList, ui, this, new Vector2(origin.X, cursorY), width, stage.Phase,
                delta, !router.IsTransitioning && !LaunchActive, scale, out var tapped, out var source);
            if (tapped.GameId is not null)
            {
                OpenEntry(tapped, source);
            }

            cursorY = DrawFloorTail(drawList, new Vector2(origin.X, shelvesBottom + FloorSectionGap * scale), width,
                scale);
            cursorY = DrawRecordsRow(drawList, new Vector2(origin.X, cursorY), width, scale);
            if (!cashier.IsOpen)
            {
                bonusShelf.DrawShower(drawList, scale);
            }

            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private void OpenEntry(in Strip.StripEntry entry, Rect source)
    {
        switch (entry.Action)
        {
            case Strip.StripAction.HostTable:
                OpenHostSheet();
                return;
            case Strip.StripAction.HostVenue:
                OpenVenueHostSheet(entry.Venue);
                return;
        }

        OpenGame(entry.GameId, source);
    }

    private float DrawHeroBlock(ImDrawListPtr drawList, Vector2 origin, float width, float delta, float scale)
    {
        var state = casino.State;
        var progress = casino.HasFeature(CasinoFeatures.Levels) ? casino.Progress : null;
        var levelFraction = 0f;
        if (progress is not null)
        {
            var span = progress.NextLevelXp - progress.LevelStartXp;
            levelFraction = span <= 0 ? 1f : Math.Clamp((float)(progress.Xp - progress.LevelStartXp) / span, 0f, 1f);
        }

        var bonuses = casino.HasFeature(CasinoFeatures.Bonus);
        var model = new Strip.StripHeroModel(FloorBalance(), state?.Sitting is not null, progress?.Level ?? 1,
            levelFraction, casino.Ceiling.LevelCap, progress is not null,
            bonuses ? casino.BonusFor(CasinoBonusKinds.Timed) : null,
            bonuses ? casino.BonusFor(CasinoBonusKinds.Streak) : null, casino.ClaimingBonus.Length > 0,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), casino.Rate);
        var action = stripHero.Draw(drawList, ui, model, origin, width, delta, scale, out var bottom);
        switch (action)
        {
            case Strip.StripHeroAction.GetChips:
            case Strip.StripHeroAction.Cashier:
                cashier.Open();
                break;
            case Strip.StripHeroAction.ClaimTimed:
                bonusShelf.Claim(CasinoBonusKinds.Timed, stripHero.TimedCenter);
                break;
            case Strip.StripHeroAction.ClaimStreak:
                bonusShelf.Claim(CasinoBonusKinds.Streak, stripHero.StreakCenter);
                break;
        }

        return bottom;
    }

    private float DrawResume(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rack = casino.State?.TableSitting;
        if (rack is null || rack.TableId.Length == 0)
        {
            return origin.Y;
        }

        var gameId = ClientGameId(rack.GameKind);
        var height = ResumeHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.resume"), origin, max, out var cardMin, out var cardMax);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale);
        Squircle.Stroke(drawList, cardMin, cardMax, Metrics.Radius.Grouped * scale,
            ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, 0.5f)), MathF.Max(1f, scale));
        var pad = Metrics.Space.Lg * scale;
        var tile = ResumeTile * scale;
        var tileCenter = new Vector2(origin.X + pad + tile * 0.5f, origin.Y + height * 0.5f);
        CasinoArt.GameTile(drawList, gameId.Length > 0 ? gameId : CasinoGames.Blackjack, tileCenter, tile);
        var buttonLabel = Loc.T(L.Strip.ResumeAction);
        var buttonWidth = Button.WidthFor(buttonLabel, ButtonSize.Large);
        var buttonHeight = Button.LargeHeight * scale;
        var button = new Rect(new Vector2(max.X - pad - buttonWidth, tileCenter.Y - buttonHeight * 0.5f),
            new Vector2(max.X - pad, tileCenter.Y + buttonHeight * 0.5f));
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        CoinArt.Labels(drawList, textLeft, button.Min.X - CoinArt.ValueGap * scale, tileCenter.Y,
            Loc.T(L.Strip.ResumeTitle), texts.Compact(L.Strip.ResumeStack, rack.Stack), ui.TitleInk, ui.BodyInk,
            scale);
        var overButton = UiInteract.Hover(button.Min, button.Max);
        var pressed = Button.Draw(drawList, button, buttonLabel, ui.Ink, ButtonStyle.Prominent, id: "casino.resume.go");
        if (pressed || (!overButton && UiInteract.Click(origin, max, hovered)))
        {
            OpenTable(rack.TableId);
        }

        return max.Y + CardGap * scale;
    }

    private void ConsumeFloorNotes()
    {
        var refusal = bonusShelf.TakeRefusal();
        if (refusal.Length > 0)
        {
            ShellToast.Show(refusal);
        }

        var rain = floor.TakeRain();
        if (rain > 0)
        {
            ShellToast.Show(texts.Compact(L.Strip.RainLanded, rain));
            UiFeedback.Play(UiSound.CoinShower);
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

        ink = ui.BodyInk;
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
}
