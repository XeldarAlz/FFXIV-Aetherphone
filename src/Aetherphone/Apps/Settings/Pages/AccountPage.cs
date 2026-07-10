using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
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

    public FontAwesomeIcon Icon => FontAwesomeIcon.User;
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly GameData gameData;
    private readonly RemoteImageCache images;
    private readonly LodestoneService lodestone;
    private readonly ISettingsNavigator navigator;
    private readonly ISettingsPage profilePage;
    private readonly ISettingsPage encryptionPage;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile string code = string.Empty;
    private volatile string? challengeId;
    private volatile string? failureReason;
    private volatile bool busy;
    private volatile bool xivAuthActive;
    private volatile string xivUserCode = string.Empty;
    private volatile string? xivVerificationUri;
    private CancellationTokenSource? xivFlowCancellation;
    private bool meRequested;

    public AccountPage(AethernetSession session, AethernetClient client, GameData gameData,
        RemoteImageCache images, LodestoneService lodestone, ISettingsNavigator navigator,
        ISettingsPage profilePage, ISettingsPage encryptionPage)
    {
        this.session = session;
        this.client = client;
        this.gameData = gameData;
        this.images = images;
        this.lodestone = lodestone;
        this.navigator = navigator;
        this.profilePage = profilePage;
        this.encryptionPage = encryptionPage;
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

        var scale = ImGuiHelpers.GlobalScale;
        var user = session.CurrentUser;
        if (user is null)
        {
            var origin = ImGui.GetCursorScreenPos();
            var center = new Vector2(origin.X + ImGui.GetContentRegionAvail().X * 0.5f, origin.Y + 88f * scale);
            LoadingPulse.Draw(center, 14f * scale, theme.Accent, theme.TextMuted, LoadingPulse.SafeLabel());
            ImGui.Dummy(new Vector2(0f, 168f * scale));
            var loadingSignOut = GroupCard.Begin(theme, 1);
            if (SettingsRow.Action(loadingSignOut.NextRow(), Loc.T(L.Account.SignOut), theme.Danger, theme))
            {
                AskSignOut();
            }

            loadingSignOut.End();
            return;
        }

        DrawIdentityHeader(user, theme, scale);
        ImGui.Dummy(new Vector2(0f, 20f * scale));
        var detailRows = user.Handle.Length > 0 ? 3 : 2;
        var details = GroupCard.Begin(theme, detailRows);
        if (user.Handle.Length > 0)
        {
            SettingsRow.Info(details.NextRow(), Loc.T(L.Account.HandleLabel), $"@{user.Handle}", theme);
        }

        SettingsRow.Info(details.NextRow(), Loc.T(L.Account.CharacterLabel), user.Name, theme);
        SettingsRow.Info(details.NextRow(), Loc.T(L.Account.HomeWorldLabel), user.World, theme);
        details.End();

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var links = GroupCard.Begin(theme, 2);
        if (SettingsRow.Link(links.NextRow(), profilePage.Icon, profilePage.Tint, profilePage.Title,
                profilePage.Summary, theme))
        {
            navigator.Open(profilePage);
        }

        if (SettingsRow.Link(links.NextRow(), encryptionPage.Icon, encryptionPage.Tint, encryptionPage.Title,
                encryptionPage.Summary, theme))
        {
            navigator.Open(encryptionPage);
        }

        links.End();

        ImGui.Dummy(new Vector2(0f, 14f * scale));
        var signOut = GroupCard.Begin(theme, 1);
        if (SettingsRow.Action(signOut.NextRow(), Loc.T(L.Account.SignOut), theme.Danger, theme))
        {
            AskSignOut();
        }

        signOut.End();
        ImGui.Dummy(new Vector2(0f, 14f * scale));
    }

    private void DrawIdentityHeader(UserDto user, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var centerX = origin.X + width * 0.5f;
        var radius = 38f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + 10f * scale + radius);
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, user.Name, user.World, user.AvatarUrl, images,
            lodestone, 2.0f, 64);
        var nameY = avatarCenter.Y + radius + 16f * scale;
        var title = Typography.FitText(user.DisplayName, width - 24f * scale, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(centerX, nameY), title, theme.TextStrong, TextStyles.Title2);
        Typography.DrawCentered(new Vector2(centerX, nameY + 24f * scale), $"{user.Name}@{user.World}",
            theme.TextMuted, TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, nameY + 34f * scale - origin.Y));
    }

    private void AskSignOut()
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Account.SignOutConfirmTitle),
            Message = Loc.T(L.Account.SignOutConfirmBody),
            ConfirmLabel = Loc.T(L.Account.SignOut),
            CancelLabel = Loc.T(L.Common.Cancel),
            Confirm = () =>
            {
                session.SignOut();
                ResetFlow();
            },
        });
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

        if (xivAuthActive)
        {
            DrawXivAuthStep(theme);
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
        var ready = !busy && name.Length > 0 && world.Length > 0;
        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.SignInIntro));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        DrawIdentityCard(name, world, theme);
        ImGui.Dummy(new Vector2(0f, 14f * scale));
        if (PrimaryButton(Loc.T(L.Account.XivSignIn), theme) && ready)
        {
            StartXivAuth(name, world);
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        if (Button(Loc.T(L.Account.SignIn), theme) && ready)
        {
            StartChallenge(name, world);
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.LodestoneHint));
        }

        DrawStatus(theme);
    }

    private void DrawXivAuthStep(PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (Plugin.Fonts.Push(1.3f, FontWeight.SemiBold))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
            {
                ImGui.TextUnformatted(Loc.T(L.Account.XivTitle));
            }
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.XivIntro));
        }

        if (xivUserCode.Length > 0)
        {
            ImGui.Dummy(new Vector2(0f, 10f * scale));
            if (DrawCodeCard(theme, xivUserCode))
            {
                ImGui.SetClipboardText(xivUserCode);
            }
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextWrapped(Loc.T(L.Account.XivWaiting));
        }

        ImGui.Dummy(new Vector2(0f, 12f * scale));
        var spacing = 8f * scale;
        var half = (ImGui.GetContentRegionAvail().X - spacing) * 0.5f;
        if (Button(Loc.T(L.Account.XivOpen), theme, half) && xivVerificationUri is not null)
        {
            UrlActions.OpenInBrowser(xivVerificationUri);
        }

        ImGui.SameLine(0f, spacing);
        if (Button(Loc.T(L.Common.Cancel), theme, half) && failureReason is null)
        {
            CancelXivFlow();
        }
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
        if (DrawCodeCard(theme, code))
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

    private static bool DrawCodeCard(PhoneTheme theme, string value)
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
        var codeSize = Typography.Measure(value, 1.55f, FontWeight.SemiBold);
        var center = new Vector2((start.X + max.X) * 0.5f, (start.Y + max.Y) * 0.5f);
        Typography.Draw(center - codeSize * 0.5f, value, theme.Accent, 1.55f, FontWeight.SemiBold);
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
        var luminance = Palette.Luminance(theme.Accent);
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
            case VerifyFailure.AccessDenied:
                return (Loc.T(L.Account.FailAccessDeniedTitle), Loc.T(L.Account.FailAccessDeniedBody));
            case VerifyFailure.XivAuthUnavailable:
                return (Loc.T(L.Account.FailXivUnavailableTitle), Loc.T(L.Account.FailXivUnavailableBody));
            case VerifyFailure.XivCharacterNotVerified:
                var xivPlayer = gameData.LocalPlayer;
                var xivName = xivPlayer?.Name.TextValue ?? string.Empty;
                var xivWorld = gameData.WorldName(gameData.LocalHomeWorldId);
                return (Loc.T(L.Account.FailXivCharacterTitle),
                    Loc.T(L.Account.FailXivCharacterBody, xivName, xivWorld));
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

    private void StartXivAuth(string name, string world)
    {
        busy = true;
        status = Loc.T(L.Account.XivConnecting);
        Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Began));
        var flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        Interlocked.Exchange(ref xivFlowCancellation, flowCancellation)?.Dispose();
        var token = flowCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await client.StartXivAuthAsync(name, world, token).ConfigureAwait(false);
                if (response is null || !response.Ok || response.FlowId is null || response.VerificationUri is null)
                {
                    var reason = response?.Reason ?? VerifyFailure.Network;
                    status = string.Empty;
                    failureReason = reason;
                    Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
                    busy = false;
                    return;
                }

                xivUserCode = response.UserCode ?? string.Empty;
                xivVerificationUri = response.VerificationUriComplete ?? response.VerificationUri;
                xivAuthActive = true;
                status = string.Empty;
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.CodeShown));
                UrlActions.OpenInBrowser(xivVerificationUri);
                await PollXivLoopAsync(response.FlowId, response.IntervalSeconds, response.ExpiresInSeconds, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"XIVAuth sign-in failed: {exception.Message}");
                status = string.Empty;
                failureReason = VerifyFailure.Network;
                ResetXivFlow();
            }
        });
    }

    private async Task PollXivLoopAsync(string flowId, int intervalSeconds, int expiresInSeconds, CancellationToken token)
    {
        var interval = Math.Max(3, intervalSeconds);
        var deadline = DateTime.UtcNow.AddSeconds(expiresInSeconds > 0 ? expiresInSeconds : 600);
        var consecutiveNetworkFailures = 0;
        while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), token).ConfigureAwait(false);
            var result = await client.PollXivAuthAsync(flowId, token).ConfigureAwait(false);
            if (result.Auth is { } auth)
            {
                session.SignIn(auth.Token, auth.User);
                Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Linked));
                ResetFlow();
                return;
            }

            var reason = result.FailureReason ?? VerifyFailure.Pending;
            if (reason == VerifyFailure.Pending)
            {
                consecutiveNetworkFailures = 0;
                continue;
            }

            if (reason == VerifyFailure.RateLimited)
            {
                consecutiveNetworkFailures = 0;
                interval += 2;
                continue;
            }

            if (reason == VerifyFailure.Network && ++consecutiveNetworkFailures < 5)
            {
                continue;
            }

            if (reason == VerifyFailure.CharacterNotFound)
            {
                reason = VerifyFailure.XivCharacterNotVerified;
            }

            Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, reason));
            status = string.Empty;
            failureReason = reason;
            ResetXivFlow();
            return;
        }

        if (!token.IsCancellationRequested)
        {
            Plugin.Analytics.Track(AnalyticsEvents.SignupStep(SignupStage.Failed, VerifyFailure.Timeout));
            status = string.Empty;
            failureReason = VerifyFailure.Timeout;
            ResetXivFlow();
        }
    }

    private void CancelXivFlow()
    {
        var flowCancellation = xivFlowCancellation;
        try
        {
            flowCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        ResetXivFlow();
    }

    private void ResetXivFlow()
    {
        xivAuthActive = false;
        xivUserCode = string.Empty;
        xivVerificationUri = null;
        busy = false;
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
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
        xivAuthActive = false;
        xivUserCode = string.Empty;
        xivVerificationUri = null;
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
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
        Interlocked.Exchange(ref xivFlowCancellation, null)?.Dispose();
        cancellation.Dispose();
    }
}
