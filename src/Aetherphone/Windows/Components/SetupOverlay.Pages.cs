using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed partial class SetupOverlay
{
    private const string LodestoneProfileUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";
    private const float TopMarginUnits = 56f;
    private const float ButtonsGapUnits = 24f;
    private const float FieldHeightUnits = 44f;

    private readonly record struct FeatureRow(FontAwesomeIcon Icon, Vector4 Tint, LocString Title, LocString Body);

    private static readonly FeatureRow[] Features =
    {
        new(FontAwesomeIcon.CommentDots, new Vector4(0.30f, 0.78f, 0.42f, 1f), L.Setup.FeatureMessageTitle,
            L.Setup.FeatureMessageBody),
        new(FontAwesomeIcon.Hashtag, new Vector4(0.33f, 0.62f, 0.96f, 1f), L.Setup.FeatureSocialTitle,
            L.Setup.FeatureSocialBody),
        new(FontAwesomeIcon.Store, new Vector4(0.95f, 0.62f, 0.28f, 1f), L.Setup.FeatureToolsTitle,
            L.Setup.FeatureToolsBody),
        new(FontAwesomeIcon.Music, new Vector4(0.78f, 0.42f, 0.92f, 1f), L.Setup.FeaturePlayTitle,
            L.Setup.FeaturePlayBody),
    };

    private static Vector4 Fade(Vector4 color, float alpha) => color with { W = color.W * alpha };

    private static float LineBlock(in TextStyle style) => Typography.Measure("Ay", style).Y;

    private static float WrappedHeight(string text, in TextStyle style, float width) =>
        Typography.CountWrappedLines(text, style, width) * LineBlock(style) * 1.25f;

    private static float BodyWidth(Rect screen) => screen.Width * 0.78f;

    private static float CenteredTop(Rect screen, float contentHeight, int buttonSlots)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var top = screen.Min.Y + TopMarginUnits * scale;
        var bottom = ButtonRect(screen, Vector2.Zero, buttonSlots - 1).Min.Y - ButtonsGapUnits * scale;
        return top + MathF.Max(0f, (bottom - top - contentHeight) * 0.42f);
    }

    private void DrawWelcome(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawHeroPage(drawList, screen, theme, offset, alpha, HeroMotif.Constellation, Loc.T(L.Setup.WelcomeTitle),
            TextStyles.LargeTitle, Loc.T(L.Setup.WelcomeBody), 1);
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.GetStarted), theme.Accent, alpha,
                live))
        {
            AdvancePage();
        }
    }

    private void DrawAccount(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        if (session.IsSignedIn)
        {
            DrawAccountSignedIn(screen, theme, offset, alpha, live);
            return;
        }

        if (flow.XivAuthActive)
        {
            DrawAccountXivAuth(screen, theme, offset, alpha, live);
            return;
        }

        if (flow.LodestoneActive)
        {
            DrawAccountLodestone(screen, theme, offset, alpha, live);
            return;
        }

        DrawAccountLanding(screen, theme, offset, alpha, live);
    }

    private void DrawAccountLanding(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var body = Loc.T(L.Setup.AccountBody);
        var player = gameData.LocalPlayer;
        var name = player?.Name.TextValue ?? string.Empty;
        var world = gameData.WorldName(gameData.LocalHomeWorldId);
        var hasPlayer = player is not null && name.Length > 0;
        var logInFirst = Loc.T(L.Account.LogInFirst);
        var extraHeight = hasPlayer
            ? 24f * scale + 62f * scale
            : 20f * scale + WrappedHeight(logInFirst, TextStyles.Subheadline, BodyWidth(screen));
        var contentHeight = HeaderHeight(screen, body, TextStyles.Body) + extraHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.UserCircle, theme.Accent,
            Loc.T(L.Setup.AccountTitle), body, TextStyles.Body, top);
        if (hasPlayer)
        {
            DrawIdentityCard(drawList, screen, offset, y + 24f * scale, name, world, alpha);
        }
        else
        {
            Typography.DrawWrappedCentered(drawList, logInFirst, TextStyles.Subheadline, Fade(InkMuted, alpha),
                new Vector2(screen.Center.X + offset.X, y + 20f * scale), BodyWidth(screen));
        }

        var ready = live && !flow.Busy && name.Length > 0 && world.Length > 0;
        if (Primary(drawList, ButtonRect(screen, offset, 2), Loc.T(L.Account.XivSignIn), theme.Accent, alpha, live,
                ready))
        {
            flow.StartXivAuth(name, world);
        }

        if (Secondary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.SignIn), alpha, live) && ready)
        {
            flow.StartLodestone(name, world);
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SetUpLater), theme.Accent, alpha,
                live))
        {
            flow.Reset();
            AdvancePage();
        }

        DrawStatusLine(drawList, screen, offset, alpha, 3);
    }

    private void DrawAccountSignedIn(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var user = session.CurrentUser;
        var displayName = user?.DisplayName ?? user?.Name ?? gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        var body = Loc.T(L.Setup.SignedInBody, displayName);
        var avatarRadius = 38f * scale;
        var contentHeight = HeaderHeight(screen, body, TextStyles.Body) + 24f * scale + avatarRadius * 2f;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.CheckCircle,
            new Vector4(0.30f, 0.78f, 0.42f, 1f), Loc.T(L.Setup.SignedInTitle), body, TextStyles.Body, top);
        if (user is not null)
        {
            var avatarCenter = new Vector2(screen.Center.X + offset.X, y + 24f * scale + avatarRadius);
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, user.Name, user.World, user.AvatarUrl,
                images, lodestone, 1.6f, 48, alpha);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme.Accent, alpha, live))
        {
            AdvancePage();
        }
    }

    private void DrawAccountXivAuth(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var body = Loc.T(L.Account.XivIntro);
        var contentHeight = HeaderHeight(screen, body, TextStyles.Body) + 18f * scale + 54f * scale + 76f * scale;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.ShieldAlt, theme.Accent,
            Loc.T(L.Account.XivTitle), body, TextStyles.Body, top);
        if (flow.XivUserCode.Length > 0)
        {
            var codeRect = CardRect(screen, offset, y + 18f * scale, 54f * scale);
            if (DrawCodeCard(drawList, codeRect, flow.XivUserCode, theme.Accent, alpha, live))
            {
                ImGui.SetClipboardText(flow.XivUserCode);
            }

            y = codeRect.Max.Y;
        }

        var waitCenter = new Vector2(screen.Center.X + offset.X, y + 34f * scale);
        LoadingPulse.Spinner(waitCenter, 9f * scale, theme.Accent, alpha, drawList);
        Typography.DrawCentered(drawList, waitCenter + new Vector2(0f, 28f * scale), Loc.T(L.Account.XivWaiting),
            Fade(InkMuted, alpha), TextStyles.Footnote);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 0);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.XivOpen), alpha, live) &&
            flow.XivVerificationUri is { } verificationUri)
        {
            UrlActions.OpenInBrowser(verificationUri);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Common.Cancel), alpha, live))
        {
            flow.CancelXivAuth();
        }
    }

    private void DrawAccountLodestone(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var body = Loc.T(L.Account.VerifyIntro);
        var stepsWidth = screen.Width - 80f * scale;
        var step1 = Loc.T(L.Account.Step1);
        var step2 = Loc.T(L.Account.Step2);
        var step3 = Loc.T(L.Account.Step3);
        var step4 = Loc.T(L.Account.Step4);
        var stepsHeight = StepLineHeight(step1, stepsWidth, scale) + StepLineHeight(step2, stepsWidth, scale) +
                          StepLineHeight(step3, stepsWidth, scale) + StepLineHeight(step4, stepsWidth, scale);
        var contentHeight = HeaderHeight(screen, body, TextStyles.Body) + 16f * scale + 54f * scale + 16f * scale +
                            stepsHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.Key, theme.Accent,
            Loc.T(L.Account.VerifyTitle), body, TextStyles.Body, top);
        var codeRect = CardRect(screen, offset, y + 16f * scale, 54f * scale);
        if (DrawCodeCard(drawList, codeRect, flow.LodestoneCode, theme.Accent, alpha, live))
        {
            ImGui.SetClipboardText(flow.LodestoneCode);
        }

        y = codeRect.Max.Y + 16f * scale;
        var stepsLeft = screen.Min.X + offset.X + 40f * scale;
        y = DrawStepLine(drawList, "1", step1, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "2", step2, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "3", step3, stepsLeft, y, stepsWidth, alpha, theme, scale);
        DrawStepLine(drawList, "4", step4, stepsLeft, y, stepsWidth, alpha, theme, scale);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 2);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.CopyCode), alpha, live))
        {
            ImGui.SetClipboardText(flow.LodestoneCode);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Account.OpenProfile), alpha, live))
        {
            UrlActions.OpenInBrowser(LodestoneProfileUrl);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.VerifyAdded), theme.Accent, alpha, live,
                !flow.Busy))
        {
            flow.VerifyLodestone();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Common.Cancel), theme.Accent, alpha,
                live))
        {
            flow.Reset();
        }

        DrawStatusLine(drawList, screen, offset, alpha, 3);
    }

    private void DrawProfile(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        if (!profilePrefilled && session.CurrentUser is { } user)
        {
            displayNameDraft = user.DisplayName ?? string.Empty;
            handleDraft = SanitizeHandle(user.Handle ?? string.Empty);
            profilePrefilled = true;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var body = Loc.T(L.Setup.ProfileBody);
        var hint = handleRejected ? Loc.T(L.Setup.HandleTaken) : Loc.T(L.Setup.HandleRules);
        var fieldHeight = FieldHeightUnits * scale;
        var contentHeight = HeaderHeight(screen, body, TextStyles.Body) + 34f * scale + fieldHeight + 32f * scale +
                            fieldHeight + 12f * scale + WrappedHeight(hint, TextStyles.Footnote, BodyWidth(screen));
        var top = CenteredTop(screen, contentHeight, 2) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.AddressCard, theme.Accent,
            Loc.T(L.Setup.ProfileTitle), body, TextStyles.Body, top);
        var nameRect = CardRect(screen, offset, y + 34f * scale, fieldHeight);
        DrawField(drawList, nameRect, "setupDisplayName", Loc.T(L.Setup.DisplayNameLabel), ref displayNameDraft,
            DisplayNameMax, alpha, live);
        var handleRect = CardRect(screen, offset, nameRect.Max.Y + 32f * scale, fieldHeight);
        DrawField(drawList, handleRect, "setupHandle", Loc.T(L.Setup.HandleLabel), ref handleDraft, HandleMax, alpha,
            live, "@");
        handleDraft = SanitizeHandle(handleDraft);
        var hintInk = handleRejected ? Fade(theme.Danger, alpha) : Fade(InkMuted, alpha);
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, hintInk,
            new Vector2(screen.Center.X + offset.X, handleRect.Max.Y + 12f * scale), BodyWidth(screen));
        var valid = !profileSaving && IsHandleValid(handleDraft) && displayNameDraft.Trim().Length > 0;
        var saveLabel = profileSaving ? Loc.T(L.Account.Saving) : Loc.T(L.Onboarding.Continue);
        if (Primary(drawList, ButtonRect(screen, offset, 1), saveLabel, theme.Accent, alpha, live, valid))
        {
            SaveProfile();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SkipForNow), theme.Accent, alpha,
                live))
        {
            AdvancePage();
        }
    }

    private void DrawPhoto(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        if (pickingPhoto)
        {
            DrawPhotoPicker(screen, theme);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var user = session.CurrentUser;
        var name = user?.Name ?? gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        var world = user?.World ?? gameData.WorldName(gameData.LocalHomeWorldId);
        var body = Loc.T(L.Setup.PhotoBody);
        var bodyWidth = BodyWidth(screen);
        var avatarRadius = 54f * scale;
        var titleHeight = LineBlock(TextStyles.Title1);
        var contentHeight = avatarRadius * 2f + 30f * scale + titleHeight + 12f * scale +
                            WrappedHeight(body, TextStyles.Body, bodyWidth);
        var top = CenteredTop(screen, contentHeight, 2) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var avatarCenter = new Vector2(centerX, top + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, name, world, user?.AvatarUrl, images,
            lodestone, 2.4f, 64, alpha);
        var titleCenter = new Vector2(centerX, top + avatarRadius * 2f + 30f * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, Loc.T(L.Setup.PhotoTitle), Fade(InkStrong, alpha),
            TextStyles.Title1);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Body, Fade(InkMuted, alpha),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + 12f * scale), bodyWidth);
        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Setup.ChoosePhoto), theme.Accent, alpha, live,
                !avatarBusy))
        {
            picker.Open();
            pickingPhoto = true;
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme.Accent,
                alpha, live))
        {
            AdvancePage();
        }
    }

    private void DrawPhotoPicker(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var area = new Rect(
            new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale),
            new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale));
        var context = new PhoneContext(area, theme, navigation);
        var labels = new ImagePickCropLabels(Loc.T(L.Setup.ChoosePhoto), Loc.T(L.Account.ImportFromPc),
            Loc.T(L.Photos.NoPhotos), Loc.T(L.Account.MoveAndScale), Loc.T(L.Account.Use), Loc.T(L.Account.Saving),
            Loc.T(L.Account.GestureHint));
        var result = picker.Draw(area, context, labels, theme.Accent, avatarBusy);
        if (result == ImagePickCropEvent.Cancelled)
        {
            pickingPhoto = false;
            return;
        }

        if (result == ImagePickCropEvent.Committed && !avatarBusy && picker.SourcePath.Length > 0)
        {
            avatarBusy = true;
            AvatarUploader.Upload(account, media, session, picker.SourcePath, picker.Crop, cancellation.Token, uploaded =>
            {
                avatarBusy = false;
                avatarOutcome = uploaded ? 1 : 2;
            });
        }
    }

    private void DrawAnalytics(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var body = Loc.T(L.Settings.ConsentMessage);
        var contentHeight = HeaderHeight(screen, body, TextStyles.Subheadline);
        var top = CenteredTop(screen, contentHeight, 2) + offset.Y;
        DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.ChartBar, new Vector4(0.33f, 0.62f, 0.96f, 1f),
            Loc.T(L.Setup.AnalyticsTitle), body, TextStyles.Subheadline, top);
        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Settings.ConsentAccept), theme.Accent, alpha,
                live))
        {
            SetAnalyticsConsent(true);
            AdvancePage();
        }

        if (Secondary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Settings.ConsentDecline), alpha, live))
        {
            SetAnalyticsConsent(false);
            AdvancePage();
        }
    }

    private void DrawFeatures(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = ImGuiHelpers.GlobalScale;
        var body = Loc.T(L.Onboarding.AllInOneBody);
        var bodyWidth = BodyWidth(screen);
        var titleHeight = LineBlock(TextStyles.Title1);
        var rowHeight = 62f * scale;
        var contentHeight = titleHeight + 12f * scale + WrappedHeight(body, TextStyles.Body, bodyWidth) +
                            24f * scale + Features.Length * rowHeight;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var titleCenter = new Vector2(centerX, top + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, Loc.T(L.Onboarding.AllInOneTitle), Fade(InkStrong, alpha),
            TextStyles.Title1);
        var y = Typography.DrawWrappedCentered(drawList, body, TextStyles.Body, Fade(InkMuted, alpha),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + 12f * scale), bodyWidth);
        y += 24f * scale;
        var rowLeft = screen.Min.X + offset.X + 36f * scale;
        var rowWidth = screen.Width - 72f * scale;
        for (var index = 0; index < Features.Length; index++)
        {
            y = DrawFeatureRow(drawList, Features[index], rowLeft, y, rowWidth, alpha, scale);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme.Accent, alpha, live))
        {
            AdvancePage();
        }
    }

    private static float DrawFeatureRow(ImDrawListPtr drawList, in FeatureRow row, float left, float top, float width,
        float alpha, float scale)
    {
        var tile = 40f * scale;
        var tileRect = new Rect(new Vector2(left, top), new Vector2(left + tile, top + tile));
        Squircle.Fill(drawList, tileRect.Min, tileRect.Max, 12f * scale,
            ImGui.GetColorU32(Fade(row.Tint, 0.92f * alpha)));
        AppSkin.Icon(drawList, tileRect.Center, row.Icon.ToIconString(), new Vector4(1f, 1f, 1f, alpha), 0.86f);
        var textLeft = tileRect.Max.X + 14f * scale;
        var textWidth = width - tile - 14f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(Loc.T(row.Title), textWidth,
            TextStyles.Headline), Fade(InkStrong, alpha), TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + 22f * scale), Typography.FitText(Loc.T(row.Body),
            textWidth, TextStyles.Footnote), Fade(InkMuted, alpha), TextStyles.Footnote);
        return top + 62f * scale;
    }

    private void DrawFeedback(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawHeroPage(drawList, screen, theme, offset, alpha, HeroMotif.Care, Loc.T(L.Onboarding.FeedbackTitle),
            TextStyles.Title1, Loc.T(L.Onboarding.FeedbackBody), 1);
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme.Accent, alpha, live))
        {
            AdvancePage();
        }
    }

    private void DrawReady(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawHeroPage(drawList, screen, theme, offset, alpha, HeroMotif.Constellation, Loc.T(L.Onboarding.WelcomeTitle),
            TextStyles.Title1, Loc.T(L.Setup.ReadyBody), 1);
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Setup.StartUsing), theme.Accent, alpha, live))
        {
            Complete();
        }
    }

    private static void DrawHeroPage(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, Vector2 offset,
        float alpha, HeroMotif motif, string title, in TextStyle titleStyle, string body, int buttonSlots)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var bodyWidth = BodyWidth(screen);
        var heroHeight = 170f * scale;
        var titleHeight = LineBlock(titleStyle);
        var contentHeight = heroHeight + 24f * scale + titleHeight + 12f * scale +
                            WrappedHeight(body, TextStyles.Body, bodyWidth);
        var top = CenteredTop(screen, contentHeight, buttonSlots) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        OnboardingHero.Draw(drawList, new Vector2(centerX, top + heroHeight * 0.5f), motif, theme.Accent, scale, 1f,
            alpha);
        var titleCenter = new Vector2(centerX, top + heroHeight + 24f * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, title, Fade(InkStrong, alpha), titleStyle);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Body, Fade(InkMuted, alpha),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + 12f * scale), bodyWidth);
    }

    private static float HeaderHeight(Rect screen, string body, in TextStyle bodyStyle)
    {
        var scale = ImGuiHelpers.GlobalScale;
        return 60f * scale + 22f * scale + LineBlock(TextStyles.Title1) + 12f * scale +
               WrappedHeight(body, bodyStyle, BodyWidth(screen));
    }

    private static float DrawHeader(ImDrawListPtr drawList, Rect screen, Vector2 offset, float alpha,
        FontAwesomeIcon icon, Vector4 tint, string title, string body, in TextStyle bodyStyle, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var centerX = screen.Center.X + offset.X;
        var tileHalf = 30f * scale;
        var tileCenter = new Vector2(centerX, top + tileHalf);
        Squircle.Fill(drawList, tileCenter - new Vector2(tileHalf, tileHalf),
            tileCenter + new Vector2(tileHalf, tileHalf), 18f * scale, ImGui.GetColorU32(Fade(tint, 0.92f * alpha)));
        AppSkin.Icon(drawList, tileCenter, icon.ToIconString(), new Vector4(1f, 1f, 1f, alpha), 1.25f);
        var titleHeight = LineBlock(TextStyles.Title1);
        var titleCenter = new Vector2(centerX, top + tileHalf * 2f + 22f * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, title, Fade(InkStrong, alpha), TextStyles.Title1);
        var bodyTop = titleCenter.Y + titleHeight * 0.5f + 12f * scale;
        return Typography.DrawWrappedCentered(drawList, body, bodyStyle, Fade(InkMuted, alpha),
            new Vector2(centerX, bodyTop), BodyWidth(screen));
    }

    private void DrawStatusLine(ImDrawListPtr drawList, Rect screen, Vector2 offset, float alpha, int buttonSlots)
    {
        var message = flow.Status;
        if (message.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(screen.Center.X + offset.X,
            ButtonRect(screen, offset, buttonSlots - 1).Min.Y - 18f * scale);
        Typography.DrawCentered(drawList, center, message, Fade(InkMuted, alpha), TextStyles.Footnote);
    }

    private static void DrawIdentityCard(ImDrawListPtr drawList, Rect screen, Vector2 offset, float top, string name,
        string world, float alpha)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rect = CardRect(screen, offset, top, 62f * scale);
        Squircle.Fill(drawList, rect.Min, rect.Max, 14f * scale, ImGui.GetColorU32(Fade(CardFill, alpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 14f * scale, ImGui.GetColorU32(Fade(CardStroke, alpha)), 1f);
        var left = rect.Min.X + 16f * scale;
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + 10f * scale), Loc.T(L.Account.SigningInAs),
            Fade(InkMuted, alpha), TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + 30f * scale), $"{name}@{world}",
            Fade(InkStrong, alpha), TextStyles.Headline);
    }

    private static bool DrawCodeCard(ImDrawListPtr drawList, Rect rect, string value, Vector4 accent, float alpha,
        bool live)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = live && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = 14f * scale;
        var fill = hovered ? Fade(accent, 0.16f * alpha) : Fade(CardFill, alpha);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(accent, 0.55f * alpha)),
            1.4f * scale);
        Typography.DrawCentered(drawList, rect.Center, value, Fade(accent, alpha), 1.5f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static float StepLineHeight(string text, float width, float scale)
    {
        var textWidth = width - 32f * scale;
        var lines = Typography.CountWrappedLines(text, TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.25f;
        return MathF.Max(20f * scale, lines * lineHeight) + 10f * scale;
    }

    private static float DrawStepLine(ImDrawListPtr drawList, string number, string text, float left, float top,
        float width, float alpha, PhoneTheme theme, float scale)
    {
        var badgeRadius = 10f * scale;
        var badgeCenter = new Vector2(left + badgeRadius, top + badgeRadius);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Fade(theme.Accent, alpha)), 24);
        Typography.DrawCentered(drawList, badgeCenter, number, new Vector4(1f, 1f, 1f, alpha), 0.72f, FontWeight.Bold);
        var textLeft = left + badgeRadius * 2f + 12f * scale;
        var textWidth = width - badgeRadius * 2f - 12f * scale;
        var lines = Typography.CountWrappedLines(text, TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.25f;
        DrawWrappedLeft(drawList, text, TextStyles.Footnote, Fade(InkStrong, 0.88f * alpha),
            new Vector2(textLeft, top + badgeRadius - lineHeight * 0.5f + 2f * scale), textWidth);
        var bottom = MathF.Max(badgeRadius * 2f, lines * lineHeight);
        return top + bottom + 10f * scale;
    }

    private static void DrawWrappedLeft(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 color,
        Vector2 position, float maxWidth)
    {
        var lineHeight = LineBlock(style) * 1.25f;
        var y = position.Y;
        var length = text.Length;
        var lineStart = 0;
        var lastSpace = -1;
        for (var index = 0; index <= length; index++)
        {
            var atEnd = index == length;
            if (!atEnd && text[index] != ' ')
            {
                continue;
            }

            var candidate = text.Substring(lineStart, index - lineStart);
            if (Typography.Measure(candidate, style).X > maxWidth && lastSpace > lineStart)
            {
                Typography.Draw(drawList, new Vector2(position.X, y), text.Substring(lineStart, lastSpace - lineStart),
                    color, style);
                y += lineHeight;
                lineStart = lastSpace + 1;
            }

            lastSpace = index;
            if (atEnd)
            {
                Typography.Draw(drawList, new Vector2(position.X, y), text.Substring(lineStart), color, style);
            }
        }
    }

    private void DrawField(ImDrawListPtr drawList, Rect rect, string id, string label, ref string value, int maxLength,
        float alpha, bool live, string? prefix = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, rect.Min.Y - 20f * scale), label,
            Fade(InkMuted, alpha), TextStyles.Footnote);
        Squircle.Fill(drawList, rect.Min, rect.Max, 12f * scale, ImGui.GetColorU32(Fade(CardFill, alpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, 12f * scale, ImGui.GetColorU32(Fade(CardStroke, alpha)), 1f);
        var textLeft = rect.Min.X + 14f * scale;
        if (prefix is not null)
        {
            var prefixSize = Typography.Measure(prefix, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - prefixSize.Y * 0.5f), prefix,
                Fade(InkMuted, alpha), TextStyles.Body);
            textLeft += prefixSize.X + 4f * scale;
        }

        if (!live)
        {
            if (value.Length > 0)
            {
                var valueSize = Typography.Measure(value, TextStyles.Body);
                Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - valueSize.Y * 0.5f), value,
                    Fade(InkStrong, alpha), TextStyles.Body);
            }

            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(textLeft, rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Max.X - 14f * scale - textLeft);
        var transparent = new Vector4(0f, 0f, 0f, 0f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, transparent)
                   .Push(ImGuiCol.FrameBgHovered, transparent)
                   .Push(ImGuiCol.FrameBgActive, transparent)
                   .Push(ImGuiCol.Text, InkStrong))
        {
            ImGui.InputText($"##{id}", ref value, maxLength);
        }
    }

    private static Rect CardRect(Rect screen, Vector2 offset, float top, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = MathF.Min(screen.Width - 56f * scale, 312f * scale);
        var left = screen.Center.X + offset.X - width * 0.5f;
        return new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    private static Rect ButtonRect(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = MathF.Min(screen.Width - 56f * scale, 300f * scale);
        var height = 48f * scale;
        var bottom = screen.Max.Y - 62f * scale - slotFromBottom * (height + 12f * scale) + offset.Y;
        var left = screen.Center.X + offset.X - width * 0.5f;
        return new Rect(new Vector2(left, bottom - height), new Vector2(left + width, bottom));
    }

    private static Vector2 TextActionCenter(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var rect = ButtonRect(screen, offset, slotFromBottom);
        return new Vector2(rect.Center.X, rect.Max.Y - 14f * ImGuiHelpers.GlobalScale);
    }

    private static (Rect Left, Rect Right) HalfButtonRects(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var full = ButtonRect(screen, offset, slotFromBottom);
        var gap = 10f * scale;
        var half = (full.Width - gap) * 0.5f;
        return (new Rect(full.Min, new Vector2(full.Min.X + half, full.Max.Y)),
            new Rect(new Vector2(full.Max.X - half, full.Min.Y), full.Max));
    }

    private static bool Primary(ImDrawListPtr drawList, Rect rect, string label, Vector4 accent, float alpha,
        bool live, bool enabled = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = live && enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = hovered ? Palette.Mix(accent, Vector4.One, 0.14f) : accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, 15f * scale,
            ImGui.GetColorU32(Fade(fill, (enabled ? 1f : 0.4f) * alpha)));
        Typography.DrawCentered(drawList, rect.Center, label,
            new Vector4(1f, 1f, 1f, (enabled ? 1f : 0.72f) * alpha), TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool Secondary(ImDrawListPtr drawList, Rect rect, string label, float alpha, bool live)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = live && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = hovered ? 0.16f : 0.09f;
        Squircle.Fill(drawList, rect.Min, rect.Max, 15f * scale,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fill * alpha)));
        Typography.DrawCentered(drawList, rect.Center, label, Fade(InkStrong, alpha), TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool TextAction(ImDrawListPtr drawList, Vector2 center, string label, Vector4 accent, float alpha,
        bool live)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var padding = new Vector2(10f * scale, 8f * scale);
        var hovered = live && ImGui.IsMouseHoveringRect(center - size * 0.5f - padding, center + size * 0.5f + padding);
        var ink = hovered ? Palette.Mix(accent, Vector4.One, 0.25f) : accent;
        Typography.DrawCentered(drawList, center, label, Fade(ink, alpha), TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
