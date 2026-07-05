using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AccountPage : ISettingsPage, IDisposable
{
    private const string LodestoneProfileUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";
    public string Title => Loc.T(L.Account.Title);

    public string Summary =>
        session.IsSignedIn
            ? session.CurrentUser?.DisplayName ?? Loc.T(L.Account.SignedIn)
            : Loc.T(L.Account.NotSignedIn);

    public string Glyph => "@";
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly GameData gameData;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile string code = string.Empty;
    private volatile string? challengeId;
    private volatile string? failureReason;
    private volatile bool busy;
    private bool meRequested;

    public AccountPage(AethernetSession session, AethernetClient client, GameData gameData)
    {
        this.session = session;
        this.client = client;
        this.gameData = gameData;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            if (session.IsSignedIn)
            {
                DrawSignedIn(theme);
            }
            else
            {
                DrawSignedOut(theme);
            }

            if (failureReason is not null)
            {
                ShowFailureAlert();
            }
        }
    }

    private void ShowFailureAlert()
    {
        var (title, message) = FailureText();
        failureReason = null;
        Plugin.Confirm.Alert(title, message, Loc.T(L.Account.FailDismiss));
    }

    private void DrawSignedIn(PhoneTheme theme)
    {
        if (session.CurrentUser is null && !meRequested && !busy)
        {
            meRequested = true;
            StartMe();
        }

        var user = session.CurrentUser;
        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (Plugin.Fonts.Push(1.4f))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(user?.DisplayName ?? Loc.T(L.Account.SignedIn));
            }
        }

        if (user is not null)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextUnformatted($"{user.Name}@{user.World}");
                ImGui.TextUnformatted(
                    $"{Loc.Plural(L.Account.Followers, user.Followers)} · {Loc.Plural(L.Account.Following, user.Following)}");
            }
        }

        ImGui.Dummy(new Vector2(0f, 12f * ImGuiHelpers.GlobalScale));
        if (Button(Loc.T(L.Account.SignOut), theme))
        {
            session.SignOut();
            ResetFlow();
        }
    }

    private void DrawSignedOut(PhoneTheme theme)
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            Typography.DrawCentered(
                new Vector2(ImGui.GetContentRegionAvail().X * 0.5f + ImGui.GetCursorScreenPos().X,
                    ImGui.GetCursorScreenPos().Y + 80f * ImGuiHelpers.GlobalScale), Loc.T(L.Account.LogInFirst),
                theme.TextMuted);
            return;
        }

        if (challengeId is not null)
        {
            DrawVerifyStep(theme);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var name = player.Name.TextValue;
        var world = gameData.WorldName(gameData.LocalHomeWorldId);
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.SignInIntro));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawIdentityCard(name, world, theme);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        if (PrimaryButton(Loc.T(L.Account.SignIn), theme) && !busy && name.Length > 0 && world.Length > 0)
        {
            StartChallenge(name, world);
        }

        DrawStatus(theme);
    }

    private void DrawVerifyStep(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (Plugin.Fonts.Push(1.3f, FontWeight.SemiBold))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(Loc.T(L.Account.VerifyTitle));
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.VerifyIntro));
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
        if (DrawCodeCard(theme))
        {
            ImGui.SetClipboardText(code);
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawStepRow("1", Loc.T(L.Account.Step1), theme);
        DrawStepRow("2", Loc.T(L.Account.Step2), theme);
        DrawStepRow("3", Loc.T(L.Account.Step3), theme);
        DrawStepRow("4", Loc.T(L.Account.Step4), theme);
        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var spacing = 8f * scale;
        var half = (ImGui.GetContentRegionAvail().X - spacing) * 0.5f;
        if (Button(Loc.T(L.Account.CopyCode), theme, half))
        {
            ImGui.SetClipboardText(code);
        }

        ImGui.SameLine(0f, spacing);
        if (Button(Loc.T(L.Account.OpenProfile), theme, half))
        {
            UrlActions.OpenInBrowser(LodestoneProfileUrl);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        if (PrimaryButton(Loc.T(L.Account.VerifyAdded), theme) && !busy && failureReason is null)
        {
            StartVerify();
        }

        ImGui.Dummy(new Vector2(0f, 2f * scale));
        if (GhostButton(Loc.T(L.Common.Cancel), theme) && failureReason is null)
        {
            ResetFlow();
        }

        DrawStatus(theme);
    }

    private static void DrawIdentityCard(string name, string world, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var padding = 12f * scale;
        var start = ImGui.GetCursorScreenPos();
        var label = Loc.T(L.Account.SigningInAs);
        var identity = $"{name}@{world}";
        var labelSize = Typography.Measure(label, 0.82f);
        var identitySize = Typography.Measure(identity, 1.05f, FontWeight.SemiBold);
        var height = padding * 2f + labelSize.Y + 4f * scale + identitySize.Y;
        var max = new Vector2(start.X + width, start.Y + height);
        Squircle.Fill(drawList, start, max, 12f * scale, ImGui.GetColorU32(theme.GroupedCard));
        Typography.Draw(new Vector2(start.X + padding, start.Y + padding), label, theme.TextMuted, 0.82f);
        Typography.Draw(new Vector2(start.X + padding, start.Y + padding + labelSize.Y + 4f * scale), identity,
            theme.TextStrong, 1.05f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
    }

    private bool DrawCodeCard(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 52f * scale;
        var start = ImGui.GetCursorScreenPos();
        var max = new Vector2(start.X + width, start.Y + height);
        var hovered = ImGui.IsMouseHoveringRect(start, max);
        var radius = 14f * scale;
        var background = hovered ? Palette.Mix(theme.GroupedCard, theme.Accent, 0.14f) : theme.GroupedCard;
        Squircle.Fill(drawList, start, max, radius, ImGui.GetColorU32(background));
        Squircle.Stroke(drawList, start, max, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.55f)),
            1.4f * scale);
        var codeSize = Typography.Measure(code, 1.55f, FontWeight.SemiBold);
        var center = new Vector2((start.X + max.X) * 0.5f, (start.Y + max.Y) * 0.5f);
        Typography.Draw(center - codeSize * 0.5f, code, theme.Accent, 1.55f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawStepRow(string number, string text, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var lineHeight = ImGui.GetTextLineHeight();
        var lineStep = lineHeight + 3f * scale;
        var badgeDiameter = 22f * scale;
        var gap = 12f * scale;
        var start = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var textLeft = start.X + badgeDiameter + gap;
        var wrapWidth = MathF.Max(40f * scale, available - badgeDiameter - gap);
        var badgeCenter = new Vector2(start.X + badgeDiameter * 0.5f, start.Y + lineHeight * 0.5f);
        drawList.AddCircleFilled(badgeCenter, badgeDiameter * 0.5f, ImGui.GetColorU32(theme.Accent));
        var luminance = (theme.Accent.X * 0.299f) + (theme.Accent.Y * 0.587f) + (theme.Accent.Z * 0.114f);
        var ink = luminance > 0.6f ? new Vector4(0.10f, 0.10f, 0.12f, 1f) : new Vector4(1f, 1f, 1f, 1f);
        var numberSize = Typography.Measure(number, 0.85f, FontWeight.Bold);
        Typography.Draw(badgeCenter - (numberSize * 0.5f), number, ink, 0.85f, FontWeight.Bold);
        var lineY = start.Y;
        var line = string.Empty;
        var words = text.Split(' ');
        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            var candidate = line.Length == 0 ? words[wordIndex] : string.Concat(line, " ", words[wordIndex]);
            if (line.Length > 0 && Typography.Measure(candidate).X > wrapWidth)
            {
                Typography.Draw(new Vector2(textLeft, lineY), line, theme.TextStrong);
                lineY += lineStep;
                line = words[wordIndex];
            }
            else
            {
                line = candidate;
            }
        }

        Typography.Draw(new Vector2(textLeft, lineY), line, theme.TextStrong);
        var bottom = MathF.Max(start.Y + badgeDiameter, lineY + lineHeight);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(available, (bottom - start.Y) + 10f * scale));
    }

    private void DrawStatus(PhoneTheme theme)
    {
        var message = status;
        if (message.Length == 0)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, 8f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(message);
        }
    }

    private (string Title, string Message) FailureText()
    {
        switch (failureReason)
        {
            case VerifyFailure.CharacterNotFound:
                var player = gameData.LocalPlayer;
                var name = player?.Name.TextValue ?? string.Empty;
                var world = gameData.WorldName(gameData.LocalHomeWorldId);
                return (Loc.T(L.Account.FailCharacterNotFoundTitle),
                    Loc.T(L.Account.FailCharacterNotFoundBody, name, world));
            case VerifyFailure.CodeNotFound:
                return (Loc.T(L.Account.FailCodeNotFoundTitle), Loc.T(L.Account.FailCodeNotFoundBody));
            case VerifyFailure.Timeout:
                return (Loc.T(L.Account.FailTimeoutTitle), Loc.T(L.Account.FailTimeoutBody));
            case VerifyFailure.ChallengeExpired:
                return (Loc.T(L.Account.FailChallengeExpiredTitle), Loc.T(L.Account.FailChallengeExpiredBody));
            case VerifyFailure.Banned:
                return (Loc.T(L.Account.FailBannedTitle), Loc.T(L.Account.FailBannedBody));
            case VerifyFailure.RateLimited:
                return (Loc.T(L.Account.FailRateLimitedTitle), Loc.T(L.Account.FailRateLimitedBody));
            case VerifyFailure.Network:
                return (Loc.T(L.Account.FailNetworkTitle), Loc.T(L.Account.FailNetworkBody));
            default:
                return (Loc.T(L.Account.FailLodestoneUnavailableTitle), Loc.T(L.Account.FailLodestoneUnavailableBody));
        }
    }

    private void StartChallenge(string name, string world)
    {
        busy = true;
        status = Loc.T(L.Account.RequestingCode);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Began));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client.ChallengeAsync(name, world, token).ConfigureAwait(false);
                if (response is null)
                {
                    status = Loc.T(L.Account.CannotReach);
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed));
                    return;
                }

                code = response.Code;
                challengeId = response.ChallengeId;
                status = string.Empty;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.CodeShown));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet challenge failed: {exception.Message}");
                status = Loc.T(L.Account.CannotReach);
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed));
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void StartVerify()
    {
        var id = challengeId;
        if (id is null)
        {
            return;
        }

        busy = true;
        status = Loc.T(L.Account.Verifying);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.VerifyPending));
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await client.VerifyAsync(id, token).ConfigureAwait(false);
                if (result.Auth is { } auth)
                {
                    session.SignIn(auth.Token, auth.User);
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Linked));
                    ResetFlow();
                    return;
                }

                var reason = result.FailureReason ?? VerifyFailure.CodeNotFound;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
                status = string.Empty;
                failureReason = reason;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet verify failed: {exception.Message}");
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, VerifyFailure.Network));
                status = string.Empty;
                failureReason = VerifyFailure.Network;
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void StartMe()
    {
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var me = await client.MeAsync(token).ConfigureAwait(false);
                if (me is not null)
                {
                    session.SetUser(me);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Aethernet profile load failed: {exception.Message}");
            }
        });
    }

    private void ResetFlow()
    {
        challengeId = null;
        code = string.Empty;
        status = string.Empty;
        failureReason = null;
        busy = false;
        meRequested = false;
    }

    private static bool Button(string label, PhoneTheme theme, float width = -1f)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.GroupedCard)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.GroupedCard, theme.Accent, 0.35f))
                   .Push(ImGuiCol.ButtonActive, theme.Accent).Push(ImGuiCol.Text, theme.TextStrong))
        {
            return ImGui.Button(label, new Vector2(width, 34f * ImGuiHelpers.GlobalScale));
        }
    }

    private static bool PrimaryButton(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, theme.Accent)
                   .Push(ImGuiCol.ButtonHovered, Palette.Mix(theme.Accent, theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.ButtonActive, Palette.Mix(theme.Accent, new Vector4(0f, 0f, 0f, 1f), 0.18f))
                   .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f)))
        {
            return ImGui.Button(label, new Vector2(-1f, 38f * ImGuiHelpers.GlobalScale));
        }
    }

    private static bool GhostButton(string label, PhoneTheme theme)
    {
        using (ImRaii.PushColor(ImGuiCol.Button, Palette.WithAlpha(theme.TextStrong, 0f))
                   .Push(ImGuiCol.ButtonHovered, Palette.WithAlpha(theme.TextStrong, 0.08f))
                   .Push(ImGuiCol.ButtonActive, Palette.WithAlpha(theme.TextStrong, 0.14f))
                   .Push(ImGuiCol.Text, theme.TextMuted))
        {
            return ImGui.Button(label, new Vector2(-1f, 32f * ImGuiHelpers.GlobalScale));
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
