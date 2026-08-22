using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private const string FaloopRegisterUrl = "https://faloop.app/register";

    private string signupUsername = string.Empty;
    private string signupPassword = string.Empty;
    private string signupError = string.Empty;

    private void OpenSignup()
    {
        signupUsername = string.Empty;
        signupPassword = string.Empty;
        signupError = string.Empty;
        router.Push(new HuntsView(HuntsRoute.Signup));
    }

    private void CloseSignup()
    {
        router.Pop();
        menu.Close();
    }

    private void SubmitSignup()
    {
        signupError = string.Empty;
        hunts.LoginFlow.Login(signupUsername, signupPassword);
        signupUsername = string.Empty;
        signupPassword = string.Empty;
    }

    private void DrawSignupHeader(Rect content, float scale)
    {
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;

        var titleStyle = new TextStyle(1.15f, FontWeight.SemiBold);
        var title = Loc.T(L.Hunts.SignupTitle);
        var titleTop = rowCenterY - Typography.Measure(title, titleStyle).Y * 0.5f;
        Marquee.DrawCenteredAuto("hunts.signup.header", title, content.Center.X, titleTop, content.Width, titleStyle,
            ui.TitleInk);

        if (AppHeader.DrawBack(content, scale, "hunts.signup.back", ui.Accent))
        {
            CloseSignup();
        }
    }

    private void DrawSignupBody(Rect body, float scale)
    {
        if (hunts.LoginFlow.ConsumeSucceeded())
        {
            hunts.Retry();
            hunts.EnsureHistoryLoaded();
            CloseSignup();
            return;
        }

        using (AppSurface.Begin(body))
        {
            Gap(8f);

            if (hunts.IsAuthenticated)
            {
                if (EmptyState.Draw(body, ui, FontAwesomeIcon.Wifi, Loc.T(L.Hunts.SignupAuthenticatedMessage),
                        string.Empty, Loc.T(L.Hunts.SignupLogoutButton)))
                {
                    hunts.Logout();
                }

                return;
            }

            ui.HelpText(Loc.T(L.Hunts.SignupIntro));
            Gap(16f);
            DrawSignupCreateAccountButton(scale);
            Gap(20f);
            ui.HelpText(Loc.T(L.Hunts.SignupLoginIntro));
            Gap(16f);
            DrawSignupForm(scale);
        }
    }

    private void DrawSignupCreateAccountButton(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var height = 38f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (ui.GhostButton(rect, Loc.T(L.Hunts.SignupCreateAccount)))
        {
            UrlActions.OpenInBrowser(FaloopRegisterUrl);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawSignupForm(float scale)
    {
        ui.Field(Loc.T(L.Hunts.SignupUsernameLabel), "##hunts.signup.username", ref signupUsername, 64, false);
        Gap(12f);
        ui.Field(Loc.T(L.Hunts.SignupPasswordLabel), "##hunts.signup.password", ref signupPassword, 128, false,
            Metrics.Size.FieldHeight, ImGuiInputTextFlags.Password);
        Gap(16f);

        if (hunts.LoginFlow.ConsumeFailure() is not null)
        {
            signupError = Loc.T(L.Hunts.SignupFailed);
        }

        if (signupError.Length > 0)
        {
            ImGui.PushTextWrapPos(0f);
            using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.Danger))
            {
                Typography.Wrapped(signupError);
            }

            ImGui.PopTextWrapPos();
            Gap(12f);
        }

        var busy = hunts.LoginFlow.Busy;
        var canSubmit = !busy && signupUsername.Length > 0 && signupPassword.Length > 0;
        var label = busy ? Loc.T(L.Hunts.SignupLoggingIn) : Loc.T(L.Hunts.SignupLoginButton);
        var width = ImGui.GetContentRegionAvail().X;
        var height = 40f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (AppSkin.PillButton(rect, label, true, canSubmit, frameTheme))
        {
            SubmitSignup();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
