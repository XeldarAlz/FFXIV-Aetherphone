using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
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

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float ResumeHeight = 58f;
    private const float ResumeTile = 38f;
    private const float ResumePad = 12f;
    private const float ResumeWashLeft = 0.16f;
    private const float ResumeWashRight = 0.04f;
    private const float ResumeRimAlpha = 0.5f;
    private const float FloorBlockGap = 16f;
    private const float FloorHeaderGap = 10f;
    private const float FooterGap = 20f;
    private const string ResumeButtonId = "casino.resume.go";
    private const string ShelvesAnchor = "casino.shelves";

    private void ResetLobby()
    {
        if (casino.State is null)
        {
            stripHero.Forget();
            return;
        }

        stripHero.Snap(FloorBalance());
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
        DrawWordmark(ImGui.GetWindowDrawList(), body, scale);
        using (UiScope.PushId("casino.lobby"))
        using (var surface = AppSurface.Begin(body))
        {
            lobbyRefresh.Draw(body, surface.Pull, surface.Dragging, casino.State is null, ui.MutedInk, refreshFloor);
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = FloorBlockGap * scale;
            var cursorY = DrawStakeNotice(origin, width, scale);
            var state = casino.State;
            if (state is not null && CasinoTonight.From(state).Reached)
            {
                cursorY = DrawLimitReached(drawList, new Vector2(origin.X, cursorY), width, scale) + gap;
            }

            cursorY = DrawResume(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY = DrawHeroBlock(drawList, new Vector2(origin.X, cursorY), width, delta, scale) + gap;
            cursorY = DrawShortcuts(drawList, new Vector2(origin.X, cursorY), width, scale);
            var live = !router.IsTransitioning && !LaunchActive;
            for (var shelfIndex = 0; shelfIndex < StripCatalog.Shelves.Length; shelfIndex++)
            {
                cursorY = DrawShelf(drawList, StripCatalog.Shelves[shelfIndex], new Vector2(origin.X, cursorY), width,
                    live, delta, scale) + gap;
            }

            cursorY = DrawLiveNow(drawList, new Vector2(origin.X, cursorY), width, scale);
            cursorY = DrawFloorFooter(drawList, new Vector2(origin.X, cursorY), width, scale);
            if (!cashier.IsOpen && !missionsSheet.IsOpen)
            {
                bonusShelf.DrawShower(drawList, scale);
            }

            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private float DrawShelf(ImDrawListPtr drawList, StripShelf shelf, Vector2 origin, float width, bool live,
        float delta, float scale)
    {
        var top = FloorHeader.Draw(drawList, ui, origin, width, Loc.T(StripCatalog.TitleOf(shelf)),
            Loc.T(StripCatalog.SublineOf(shelf)), scale) + FloorHeaderGap * scale;
        if (shelf == StripShelf.Machines && casino.Jackpot > 0)
        {
            top = DrawJackpot(drawList, new Vector2(origin.X, top), width, delta, scale) + FloorHeaderGap * scale;
        }

        var bottom = shelves.DrawGrid(drawList, this, shelf, new Vector2(origin.X, top), width, floorLights.Phase,
            delta, live, scale, out var tapped, out var source);
        if (shelf == StripShelf.Cards)
        {
            UiAnchors.Report(ShelvesAnchor, new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, bottom)));
        }

        if (tapped.GameId is not null)
        {
            OpenEntry(tapped, source);
        }

        return bottom;
    }

    private void OpenEntry(in StripEntry entry, Rect source) => OpenGame(entry.GameId, source);

    private float DrawResume(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rack = casino.State?.TableSitting;
        if (rack is null || rack.TableId.Length == 0)
        {
            return origin.Y;
        }

        var gameId = ClientGameId(rack.GameKind);
        var pad = ResumePad * scale;
        var labels = Typography.LineHeight(TextStyles.BodyEmphasized) + Typography.LineHeight(TextStyles.Footnote);
        var height = MathF.Max(ResumeHeight * scale, pad * 2f + MathF.Max(ResumeTile * scale, labels));
        var max = new Vector2(origin.X + width, origin.Y + height);
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.resume"), origin, max, out var cardMin, out var cardMax);
        var radius = Metrics.Radius.Lg * scale;
        Squircle.FillHorizontalGradient(drawList, cardMin, cardMax, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = ResumeWashLeft }),
            ImGui.GetColorU32(CasinoColors.Money with { W = ResumeWashRight }));
        Squircle.Stroke(drawList, cardMin, cardMax, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = ResumeRimAlpha }), MathF.Max(1f, scale));
        var tile = ResumeTile * scale;
        var tileCenter = new Vector2(origin.X + pad + tile * 0.5f, origin.Y + height * 0.5f);
        CasinoArt.GameTile(drawList, gameId.Length > 0 ? gameId : CasinoGames.Blackjack, tileCenter, tile);
        var buttonLabel = Loc.T(L.Strip.ResumeAction);
        var buttonWidth = Button.WidthFor(buttonLabel, ButtonSize.Regular);
        var buttonHeight = Button.RegularHeight * scale;
        var button = new Rect(new Vector2(max.X - pad - buttonWidth, tileCenter.Y - buttonHeight * 0.5f),
            new Vector2(max.X - pad, tileCenter.Y + buttonHeight * 0.5f));
        var textLeft = tileCenter.X + tile * 0.5f + ResumePad * scale;
        CoinArt.Labels(drawList, textLeft, button.Min.X - CoinArt.ValueGap * scale, tileCenter.Y,
            Loc.T(L.Strip.ResumeTitle), texts.Compact(L.Strip.ResumeStack, rack.Stack), ui.TitleInk, ui.BodyInk,
            scale);
        var overButton = UiInteract.Hover(button.Min, button.Max);
        var face = Button.Surface(drawList, button, ui.Ink.WithAccent(CasinoColors.Money), ButtonStyle.Prominent,
            ButtonRole.Normal, true, overButton, ImGui.GetID(ResumeButtonId));
        Button.DrawLabel(drawList, face with { LabelInk = CasinoColors.InkOnLight with { W = face.LabelInk.W } },
            buttonLabel, ResumeButtonId);
        if (UiInteract.Click(button.Min, button.Max, overButton) || (!overButton && UiInteract.Click(origin, max, hovered)))
        {
            OpenTable(rack.TableId);
        }

        return max.Y + FloorBlockGap * scale;
    }

    private float DrawFloorFooter(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var fair = Loc.T(L.Casino.FairnessRow);
        var limits = Loc.T(L.Casino.LimitsRow);
        var height = Button.RegularHeight * scale;
        var gap = FooterGap * scale;
        var fairWidth = MathF.Min(width, Button.WidthFor(fair, ButtonSize.Regular));
        var limitsWidth = MathF.Min(width, Button.WidthFor(limits, ButtonSize.Regular));
        var ink = ui.Ink.WithAccent(ui.MutedInk);
        Rect fairRect;
        Rect limitsRect;
        if (fairWidth + gap + limitsWidth <= width)
        {
            var left = origin.X + (width - fairWidth - gap - limitsWidth) * 0.5f;
            fairRect = new Rect(new Vector2(left, origin.Y), new Vector2(left + fairWidth, origin.Y + height));
            left += fairWidth + gap;
            limitsRect = new Rect(new Vector2(left, origin.Y), new Vector2(left + limitsWidth, origin.Y + height));
        }
        else
        {
            var center = origin.X + width * 0.5f;
            fairRect = new Rect(new Vector2(center - fairWidth * 0.5f, origin.Y),
                new Vector2(center + fairWidth * 0.5f, origin.Y + height));
            limitsRect = new Rect(new Vector2(center - limitsWidth * 0.5f, origin.Y + height),
                new Vector2(center + limitsWidth * 0.5f, origin.Y + height * 2f));
        }

        if (Button.Draw(drawList, fairRect, fair, ink, ButtonStyle.Plain, id: "casino.footer.fair"))
        {
            OpenFairness();
        }

        if (Button.Draw(drawList, limitsRect, limits, ink, ButtonStyle.Plain, id: "casino.footer.limits"))
        {
            OpenLimits();
        }

        return limitsRect.Max.Y;
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
