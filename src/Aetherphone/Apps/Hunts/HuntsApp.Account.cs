using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const string RegisterUrl = "https://faloop.app/register";
    private const float AccountHeroTile = 56f;
    private const float AccountButtonHeight = 42f;
    private const float CenteredLineSpacing = 1.25f;
    private const string CenteredProbe = "Ay";

    private string signupUsername = string.Empty;
    private string signupPassword = string.Empty;
    private bool signupFailed;

    private void OpenAccount()
    {
        signupUsername = string.Empty;
        signupPassword = string.Empty;
        signupFailed = false;
        Push(new HuntsView(HuntsRoute.Account, BackTitle: RootTitle()));
    }

    private void ConsumeLoginResult()
    {
        var onAccount = router.Current.Route == HuntsRoute.Account;
        if (hunts.LoginFlow.ConsumeSucceeded())
        {
            hunts.Retry();
            hunts.EnsureHistoryLoaded();
            UiFeedback.Play(UiSound.Success);
            if (onAccount)
            {
                router.Pop();
            }
        }

        if (hunts.LoginFlow.ConsumeFailure() is not null)
        {
            signupFailed = true;
            UiFeedback.Play(UiSound.Caution);
        }
    }

    private void DrawAccount(in PhoneContext context, HuntsView view)
    {
        var scale = UiScale.Current;
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("hunts.account"))
        using (AppSurface.Begin(navBar.Body))
        {
            if (hunts.IsAuthenticated)
            {
                DrawSignedIn(scale);
            }
            else
            {
                DrawSignInForm(scale);
            }

            BottomSpacer(scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "hunts.account.nav", Loc.T(L.Hunts.AccountTitle),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, view.BackTitle, back);
    }

    private float DrawAccountHero(string title, string body, Vector4 tint, string glyph, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = HuntsArt.CardPadding * scale;
        var tile = AccountHeroTile * scale;
        var inner = width - pad * 2f;
        var titleHeight = CenteredBlockHeight(title, TextStyles.Title3, inner);
        var bodyHeight = CenteredBlockHeight(body, TextStyles.Subheadline, inner);
        var height = pad + tile + HuntsArt.RowGap * scale + titleHeight + HuntsArt.LineGap * scale * 2f + bodyHeight +
                     pad;
        var card = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(drawList, card.Min, card.Max, HuntsArt.CardRadius * scale, elevated: true);
        var tileMin = new Vector2(card.Center.X - tile * 0.5f, card.Min.Y + pad);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(tint));
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, glyph, AccentRing.Ink, tile * 0.5f);
        var top = tileMin.Y + tile + HuntsArt.RowGap * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title3, ui.TitleInk,
            new Vector2(card.Center.X, top), inner);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(card.Center.X, MathF.Max(titleBottom, top + titleHeight) + HuntsArt.LineGap * scale * 2f), inner);
        ImGui.Dummy(new Vector2(width, height + HuntsArt.CardGap * scale));
        return height;
    }

    private static float CenteredBlockHeight(string text, in TextStyle style, float width) =>
        Typography.WrapText(text, style, width).Length * Typography.Measure(CenteredProbe, style).Y *
        CenteredLineSpacing;

    private void DrawSignedIn(float scale)
    {
        var connected = hunts.RealtimeConnected;
        DrawAccountHero(Loc.T(L.Hunts.SignupAuthenticatedMessage),
            connected ? Loc.T(L.Hunts.AccountLiveBody) : Loc.T(L.Hunts.RealtimeReconnectingTooltip),
            connected ? HuntsArt.OpenColor : HuntsArt.LiveColor, PhoneIcons.CircleCheckFilled, scale);
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, origin + new Vector2(width, AccountButtonHeight * scale));
        if (ui.DangerGhostButton(rect, Loc.T(L.Hunts.SignupLogoutButton)))
        {
            hunts.Logout();
            UiFeedback.Play(UiSound.ToggleOff);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (AccountButtonHeight + HuntsArt.CardGap) * scale));
        DrawFootnote(Loc.T(L.Hunts.SignupIntro), scale);
    }

    private void DrawSignInForm(float scale)
    {
        DrawAccountHero(Loc.T(L.Hunts.AccountHeroTitle), Loc.T(L.Hunts.SignupLoginIntro), ui.Accent,
            PhoneIcons.UserCircle, scale);
        ui.Field(Loc.T(L.Hunts.SignupUsernameLabel), "##hunts.signup.username", ref signupUsername, 64, false);
        Gap(Metrics.Space.Md);
        ui.Field(Loc.T(L.Hunts.SignupPasswordLabel), "##hunts.signup.password", ref signupPassword, 128, false,
            Metrics.Size.FieldHeight, ImGuiInputTextFlags.Password);
        Gap(Metrics.Space.Lg);
        if (signupFailed)
        {
            var errorOrigin = ImGui.GetCursorScreenPos();
            var errorWidth = ImGui.GetContentRegionAvail().X;
            var errorHeight = Typography.DrawWrappedLeft(errorOrigin, Loc.T(L.Hunts.SignupFailed), frameTheme.Danger,
                TextStyles.Subheadline, errorWidth);
            ImGui.Dummy(new Vector2(errorWidth, errorHeight + Metrics.Space.Md * scale));
        }

        var busy = hunts.LoginFlow.Busy;
        var canSubmit = !busy && signupUsername.Length > 0 && signupPassword.Length > 0;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = AccountButtonHeight * scale;
        var loginRect = new Rect(origin, origin + new Vector2(width, height));
        if (AppSkin.PillButton(loginRect, busy ? Loc.T(L.Hunts.SignupLoggingIn) : Loc.T(L.Hunts.SignupLoginButton),
                true, canSubmit, frameTheme))
        {
            signupFailed = false;
            hunts.LoginFlow.Login(signupUsername, signupPassword);
            signupPassword = string.Empty;
            UiFeedback.Play(UiSound.Tap);
        }

        var createRect = new Rect(new Vector2(origin.X, origin.Y + height + Metrics.Space.Md * scale),
            new Vector2(origin.X + width, origin.Y + height * 2f + Metrics.Space.Md * scale));
        if (ui.GhostButton(createRect, Loc.T(L.Hunts.SignupCreateAccount)))
        {
            UrlActions.OpenInBrowser(RegisterUrl);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height * 2f + Metrics.Space.Md * scale + HuntsArt.CardGap * scale));
        DrawFootnote(Loc.T(L.Hunts.SignupIntro), scale);
    }
}
