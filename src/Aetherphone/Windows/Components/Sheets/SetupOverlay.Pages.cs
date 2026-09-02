using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed partial class SetupOverlay
{
    private const string LodestoneProfileUrl = "https://na.finalfantasyxiv.com/lodestone/my/setting/profile/";
    private const string RisingStonesProfileSettingsUrl = "https://ff14risingstones.web.sdo.com/pc/index.html#/me/settings/main";
    private const float TopMarginUnits = 56f;
    private const float ButtonsGapUnits = 24f;
    private const float FieldHeightUnits = 44f;
    private const float SideMarginUnits = 22f;
    private const float ContentMaxUnits = 320f;
    private const float ButtonHeightUnits = 50f;
    private const float PreviewTileUnits = 232f;

    private static readonly Vector4 PreviewInk = new(1f, 1f, 1f, 0.96f);
    private static readonly Vector4 PreviewShadow = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 PreviewBadgeWarm = new(0.95f, 0.38f, 0.62f, 1f);
    private static readonly Vector4 PreviewBadgeCool = new(0.42f, 0.52f, 0.96f, 1f);
    private static readonly Vector4 LightPreviewFallback = new(0.86f, 0.88f, 0.93f, 1f);
    private static readonly Vector4 DarkPreviewFallback = new(0.07f, 0.07f, 0.11f, 1f);
    private static readonly Vector4 SignedInTint = new(0.30f, 0.78f, 0.42f, 1f);

    private string risingStonesUuidDraft = string.Empty;

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

    private static float BodyWidth(Rect screen) => ContentWidth(screen) * 0.94f;

    private static float ContentWidth(Rect screen)
    {
        var scale = UiScale.Current;
        return MathF.Min(screen.Width - 2f * SideMarginUnits * scale, ContentMaxUnits * scale);
    }

    private static float CenteredTop(Rect screen, float contentHeight, int buttonSlots)
    {
        var scale = UiScale.Current;
        var top = screen.Min.Y + TopMarginUnits * scale;
        var bottom = ButtonRect(screen, Vector2.Zero, buttonSlots - 1).Min.Y - ButtonsGapUnits * scale;
        return top + MathF.Max(0f, (bottom - top - contentHeight) * 0.42f);
    }

    private static void DrawBackdrop(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float alpha,
        float rounding)
    {
        Squircle.Fill(drawList, screen.Min, screen.Max, rounding,
            ImGui.GetColorU32(Fade(theme.AppBackground, alpha)));
        drawList.PushClipRect(screen.Min, screen.Max, true);
        var wash = new Vector2(screen.Center.X, screen.Min.Y + screen.Height * 0.20f);
        drawList.AddCircleFilled(wash, screen.Width * 0.78f,
            ImGui.GetColorU32(Fade(theme.Accent, 0.05f * alpha)), 96);
        drawList.AddCircleFilled(wash, screen.Width * 0.46f,
            ImGui.GetColorU32(Fade(theme.Accent, 0.05f * alpha)), 96);
        drawList.PopClipRect();
    }

    private void DrawWelcome(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawHeroPage(drawList, screen, theme, offset, alpha, Loc.T(L.Setup.WelcomeTitle), TextStyles.LargeTitle,
            Loc.T(L.Setup.WelcomeBody));
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.GetStarted), theme, alpha, live))
        {
            AdvancePage();
        }
    }

    private void DrawLanguage(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var languages = Languages.All;
        var rowHeight = Metrics.Size.Row * scale;
        var globeHeight = 64f * scale;
        var listHeight = languages.Length * rowHeight;
        var contentHeight = globeHeight + Metrics.Space.Xxl * scale + listHeight;
        var top = CenteredTop(screen, contentHeight, 0) + offset.Y;
        AppSkin.Icon(drawList, new Vector2(screen.Center.X + offset.X, top + globeHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.Globe), Fade(theme.Accent, alpha), 2.6f);
        var card = CardRect(screen, offset, top + globeHeight + Metrics.Space.Xxl * scale, listHeight);
        DrawCard(drawList, card, theme, alpha);
        var picked = -1;
        for (var index = 0; index < languages.Length; index++)
        {
            var rowTop = card.Min.Y + index * rowHeight;
            var row = new Rect(new Vector2(card.Min.X, rowTop), new Vector2(card.Max.X, rowTop + rowHeight));
            if (DrawSelectRow(drawList, row, theme, languages[index].NativeName,
                    languages[index].Code == Loc.Current.Code, alpha, live, index == 0,
                    index == languages.Length - 1))
            {
                picked = index;
            }
        }

        if (picked >= 0)
        {
            ApplyLanguage(languages[picked]);
        }
    }

    private void DrawAppearance(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Setup.AppearanceBody);
        var bodyWidth = BodyWidth(screen);
        var titleHeight = LineBlock(TextStyles.LargeTitle);
        var tileHeight = PreviewTileUnits * scale;
        var tileWidth = tileHeight * (screen.Height > 0f ? screen.Width / screen.Height : 0.5f);
        var captionHeight = LineBlock(TextStyles.Subheadline) + Metrics.Space.Sm * scale + 22f * scale;
        var rowHeight = Metrics.Size.Row * scale;
        var contentHeight = titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth) + Metrics.Space.Xl * scale +
                            tileHeight + Metrics.Space.Md * scale + captionHeight + Metrics.Space.Xl * scale +
                            rowHeight;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f),
            Loc.T(L.Setup.AppearanceTitle), Fade(theme.TextStrong, alpha), TextStyles.LargeTitle);
        var bodyBottom = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(theme.TextMuted, alpha), new Vector2(centerX, top + titleHeight + Metrics.Space.Md * scale),
            bodyWidth);
        var tilesTop = bodyBottom + Metrics.Space.Xl * scale;
        var gap = Metrics.Space.Xl * scale;
        var lightLeft = centerX - gap * 0.5f - tileWidth;
        var lightRect = new Rect(new Vector2(lightLeft, tilesTop),
            new Vector2(lightLeft + tileWidth, tilesTop + tileHeight));
        var darkLeft = centerX + gap * 0.5f;
        var darkRect = new Rect(new Vector2(darkLeft, tilesTop),
            new Vector2(darkLeft + tileWidth, tilesTop + tileHeight));
        var pickedLight = DrawAppearanceTile(drawList, lightRect, theme, false,
            !dynamicAppearance && !prefersDark, alpha, live);
        var pickedDark = DrawAppearanceTile(drawList, darkRect, theme, true, !dynamicAppearance && prefersDark,
            alpha, live);
        var captionTop = tilesTop + tileHeight + Metrics.Space.Md * scale;
        DrawAppearanceCaption(drawList, lightRect.Center.X, captionTop, theme, Loc.T(L.Settings.ThemeLight),
            !dynamicAppearance && !prefersDark, alpha);
        DrawAppearanceCaption(drawList, darkRect.Center.X, captionTop, theme, Loc.T(L.Settings.ThemeDark),
            !dynamicAppearance && prefersDark, alpha);
        if (pickedLight || pickedDark)
        {
            prefersDark = pickedDark;
            dynamicAppearance = false;
            ApplyAppearance();
        }

        var card = CardRect(screen, offset, captionTop + captionHeight + Metrics.Space.Xl * scale, rowHeight);
        DrawCard(drawList, card, theme, alpha);
        var labelSize = Typography.Measure(Loc.T(L.Setup.AppearanceDynamic), TextStyles.BodyEmphasized);
        Typography.Draw(drawList,
            new Vector2(card.Min.X + Metrics.Space.Lg * scale, card.Center.Y - labelSize.Y * 0.5f),
            Loc.T(L.Setup.AppearanceDynamic), Fade(theme.TextStrong, alpha), TextStyles.BodyEmphasized);
        var toggleSize = new Vector2(Metrics.Size.ToggleWidth * scale, Metrics.Size.ToggleHeight * scale);
        var toggleMin = new Vector2(card.Max.X - Metrics.Space.Lg * scale - toggleSize.X,
            card.Center.Y - toggleSize.Y * 0.5f);
        var dynamicNext = Toggle.Draw("setup.dynamicAppearance", new Rect(toggleMin, toggleMin + toggleSize),
            dynamicAppearance, theme, alpha, live);
        if (dynamicNext != dynamicAppearance)
        {
            dynamicAppearance = dynamicNext;
            ApplyAppearance();
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme, alpha, live))
        {
            AdvancePage();
        }
    }

    private static bool DrawAppearanceTile(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, bool dark,
        bool selected, float alpha, bool live)
    {
        var scale = UiScale.Current;
        var radius = Metrics.Radius.Lg * scale;
        var aspect = rect.Height > 0f ? rect.Width / rect.Height : 0.5f;
        var entry = Plugin.Wallpapers.Resolve(dark ? theme.DarkWallpaperId : theme.LightWallpaperId);
        var fallback = dark ? DarkPreviewFallback : LightPreviewFallback;
        WallpaperRenderer.DrawSingle(drawList, rect, radius, entry, aspect, alpha, Fade(fallback, alpha));
        var clockCenter = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.24f);
        var clock = TimeText.Clock(DateTime.Now);
        Typography.DrawCentered(drawList, clockCenter + new Vector2(0f, 1f * scale), clock,
            Fade(PreviewShadow, alpha), TextStyles.Title3);
        Typography.DrawCentered(drawList, clockCenter, clock, Fade(PreviewInk, alpha), TextStyles.Title3);
        DrawPreviewNotifications(drawList, rect, dark, alpha);
        var ring = selected ? theme.Accent : theme.Separator;
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(ring, alpha)),
            (selected ? Metrics.Stroke.Ring : Metrics.Stroke.Hairline) * scale);
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static void DrawPreviewNotifications(ImDrawListPtr drawList, Rect rect, bool dark, float alpha)
    {
        var inset = rect.Width * 0.09f;
        var height = rect.Height * 0.078f;
        var fill = dark
            ? new Vector4(0.10f, 0.10f, 0.14f, 0.74f * alpha)
            : new Vector4(1f, 1f, 1f, 0.82f * alpha);
        var lineInk = dark
            ? new Vector4(1f, 1f, 1f, 0.34f * alpha)
            : new Vector4(0.12f, 0.12f, 0.14f, 0.30f * alpha);
        for (var index = 0; index < 2; index++)
        {
            var top = rect.Min.Y + rect.Height * (0.68f + index * 0.115f);
            var min = new Vector2(rect.Min.X + inset, top);
            var max = new Vector2(rect.Max.X - inset, top + height);
            Squircle.Fill(drawList, min, max, height * 0.34f, ImGui.GetColorU32(fill));
            var badge = height * 0.52f;
            var badgeMin = new Vector2(min.X + height * 0.24f, min.Y + (height - badge) * 0.5f);
            Squircle.Fill(drawList, badgeMin, badgeMin + new Vector2(badge, badge), badge * 0.3f,
                ImGui.GetColorU32(Fade(index == 0 ? PreviewBadgeWarm : PreviewBadgeCool, alpha)));
            var textLeft = badgeMin.X + badge + height * 0.22f;
            var lineHeight = MathF.Max(1f, height * 0.13f);
            var firstTop = min.Y + height * 0.30f;
            var secondTop = min.Y + height * 0.58f;
            drawList.AddRectFilled(new Vector2(textLeft, firstTop),
                new Vector2(max.X - height * 0.32f, firstTop + lineHeight), ImGui.GetColorU32(lineInk),
                lineHeight * 0.5f);
            drawList.AddRectFilled(new Vector2(textLeft, secondTop),
                new Vector2(max.X - height * 1.5f, secondTop + lineHeight), ImGui.GetColorU32(lineInk),
                lineHeight * 0.5f);
        }
    }

    private static void DrawAppearanceCaption(ImDrawListPtr drawList, float centerX, float top, PhoneTheme theme,
        string label, bool selected, float alpha)
    {
        var scale = UiScale.Current;
        var labelHeight = LineBlock(TextStyles.Subheadline);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + labelHeight * 0.5f), label,
            Fade(theme.TextStrong, alpha), TextStyles.Subheadline);
        var markRadius = 11f * scale;
        var markCenter = new Vector2(centerX, top + labelHeight + Metrics.Space.Sm * scale + markRadius);
        if (!selected)
        {
            drawList.AddCircle(markCenter, markRadius, ImGui.GetColorU32(Fade(theme.Separator, alpha)), 32,
                Metrics.Stroke.Hairline * scale);
            return;
        }

        drawList.AddCircleFilled(markCenter, markRadius, ImGui.GetColorU32(Fade(theme.Accent, alpha)), 32);
        AppSkin.Icon(drawList, markCenter, IconGlyph.Of(FontAwesomeIcon.Check), new Vector4(1f, 1f, 1f, alpha),
            0.62f);
    }

    private void DrawAccount(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        if (session.IsSignedIn)
        {
            DrawAccountSignedIn(screen, theme, offset, alpha, live);
            return;
        }

        if (flow.RisingStonesActive)
        {
            DrawAccountRisingStones(screen, theme, offset, alpha, live);
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
        if (gameData.IsChineseGameClient())
        {
            DrawAccountRisingStonesLanding(screen, theme, offset, alpha, live);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Setup.AccountBody);
        var player = gameData.LocalPlayer;
        var name = player?.Name.TextValue ?? string.Empty;
        var world = gameData.WorldName(gameData.LocalHomeWorldId);
        var hasPlayer = player is not null && name.Length > 0;
        var logInFirst = Loc.T(L.Account.LogInFirst);
        var extraHeight = hasPlayer
            ? Metrics.Space.Xl * scale + 62f * scale
            : Metrics.Space.Xl * scale + WrappedHeight(logInFirst, TextStyles.Subheadline, BodyWidth(screen));
        var contentHeight = HeaderHeight(screen, body) + extraHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.UserCircle, theme.Accent,
            Loc.T(L.Setup.AccountTitle), body, theme, top);
        if (hasPlayer)
        {
            DrawIdentityCard(drawList, screen, theme, offset, y + Metrics.Space.Xl * scale, name, world, alpha);
        }
        else
        {
            Typography.DrawWrappedCentered(drawList, logInFirst, TextStyles.Subheadline,
                Fade(theme.TextMuted, alpha),
                new Vector2(screen.Center.X + offset.X, y + Metrics.Space.Xl * scale), BodyWidth(screen));
        }

        var ready = live && !flow.Busy && name.Length > 0 && world.Length > 0;
        if (Primary(drawList, ButtonRect(screen, offset, 2), Loc.T(L.Account.XivSignIn), theme, alpha, live, ready))
        {
            flow.StartXivAuth(name, world);
        }

        if (Secondary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.SignIn), theme, alpha, live) && ready)
        {
            flow.StartLodestone(name, world);
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SetUpLater), theme, alpha, live))
        {
            flow.Reset();
            AdvancePage();
        }

        DrawStatusLine(drawList, screen, theme, offset, alpha, 3);
    }

    private void DrawAccountRisingStonesLanding(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Account.RisingStonesIntro);
        var hint = Loc.T(L.Account.RisingStonesUuidHint);
        var player = gameData.LocalPlayer;
        var hasPlayer = player is not null && player.Name.TextValue.Length > 0;
        var logInFirst = Loc.T(L.Account.LogInFirst);
        var warning = Loc.T(L.Account.RisingStonesThirdPartyWarning);
        var labelBlock = LineBlock(TextStyles.Footnote) + Metrics.Space.Xs * scale;
        var fieldHeight = FieldHeightUnits * scale;
        var bodyWidth = BodyWidth(screen);
        var warningHeight = WrappedHeight(warning, TextStyles.Footnote, bodyWidth);
        var extraHeight = (hasPlayer
            ? Metrics.Space.Xl * scale + labelBlock + fieldHeight + Metrics.Space.Md * scale +
              WrappedHeight(hint, TextStyles.Footnote, bodyWidth)
            : Metrics.Space.Xl * scale + WrappedHeight(logInFirst, TextStyles.Subheadline, bodyWidth)) +
            Metrics.Space.Md * scale + warningHeight;
        var contentHeight = HeaderHeight(screen, body) + extraHeight;
        var top = CenteredTop(screen, contentHeight, 2) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.UserCircle, theme.Accent,
            Loc.T(L.Setup.AccountTitle), body, theme, top);
        var ready = false;
        float warningTop;
        if (hasPlayer)
        {
            var fieldRect = CardRect(screen, offset, y + Metrics.Space.Xl * scale + labelBlock, fieldHeight);
            DrawField(drawList, fieldRect, theme, "setupRisingStonesUuid", Loc.T(L.Account.RisingStonesUuidLabel),
                ref risingStonesUuidDraft, SignInFlow.RisingStonesUuidMaxLength, alpha, live);
            risingStonesUuidDraft = SanitizeDigits(risingStonesUuidDraft);
            var hintTop = fieldRect.Max.Y + Metrics.Space.Md * scale;
            Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, Fade(theme.TextMuted, alpha),
                new Vector2(screen.Center.X + offset.X, hintTop), bodyWidth);
            warningTop = hintTop + WrappedHeight(hint, TextStyles.Footnote, bodyWidth) + Metrics.Space.Md * scale;
            ready = live && !flow.Busy && risingStonesUuidDraft.Length > 0;
        }
        else
        {
            var noticeTop = y + Metrics.Space.Xl * scale;
            Typography.DrawWrappedCentered(drawList, logInFirst, TextStyles.Subheadline, Fade(theme.TextMuted, alpha),
                new Vector2(screen.Center.X + offset.X, noticeTop), bodyWidth);
            warningTop = noticeTop + WrappedHeight(logInFirst, TextStyles.Subheadline, bodyWidth) +
                         Metrics.Space.Md * scale;
        }

        Typography.DrawWrappedCentered(drawList, warning, TextStyles.Footnote, Fade(theme.Danger, alpha),
            new Vector2(screen.Center.X + offset.X, warningTop), bodyWidth);

        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.RisingStonesSignIn), theme, alpha, live,
                ready))
        {
            flow.StartRisingStones(risingStonesUuidDraft);
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SetUpLater), theme, alpha, live))
        {
            flow.Reset();
            AdvancePage();
        }

        DrawStatusLine(drawList, screen, theme, offset, alpha, 2);
    }

    private void DrawAccountRisingStones(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Account.RisingStonesVerifyIntro);
        var stepsWidth = ContentWidth(screen);
        var step1 = Loc.T(L.Account.Step1);
        var step2 = Loc.T(L.Account.RisingStonesStep2);
        var step3 = Loc.T(L.Account.RisingStonesStep3);
        var step4 = Loc.T(L.Account.Step4);
        var stepsHeight = StepLineHeight(step1, stepsWidth, scale) + StepLineHeight(step2, stepsWidth, scale) +
                          StepLineHeight(step3, stepsWidth, scale) + StepLineHeight(step4, stepsWidth, scale);
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Lg * scale + 54f * scale +
                            Metrics.Space.Lg * scale + stepsHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.Key, theme.Accent,
            Loc.T(L.Account.RisingStonesVerifyTitle), body, theme, top);
        var codeRect = CardRect(screen, offset, y + Metrics.Space.Lg * scale, 54f * scale);
        if (DrawCodeCard(drawList, codeRect, theme, flow.ChallengeCode, alpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        y = codeRect.Max.Y + Metrics.Space.Lg * scale;
        var stepsLeft = codeRect.Min.X;
        y = DrawStepLine(drawList, "1", step1, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "2", step2, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "3", step3, stepsLeft, y, stepsWidth, alpha, theme, scale);
        DrawStepLine(drawList, "4", step4, stepsLeft, y, stepsWidth, alpha, theme, scale);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 2);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.CopyCode), theme, alpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Account.RisingStonesOpen), theme, alpha, live))
        {
            UrlActions.OpenInBrowser(RisingStonesProfileSettingsUrl);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.VerifyAdded), theme, alpha, live,
                !flow.Busy))
        {
            flow.VerifyChallenge();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Common.Cancel), theme, alpha, live))
        {
            flow.Reset();
        }

        DrawStatusLine(drawList, screen, theme, offset, alpha, 3);
    }

    private static string SanitizeDigits(string value)
    {
        var clean = true;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] < '0' || value[index] > '9')
            {
                clean = false;
                break;
            }
        }

        if (clean)
        {
            return value;
        }

        Span<char> digits = stackalloc char[value.Length];
        var length = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] >= '0' && value[index] <= '9')
            {
                digits[length++] = value[index];
            }
        }

        return new string(digits[..length]);
    }

    private void DrawAccountSignedIn(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var user = session.CurrentUser;
        var displayName = user is not null && user.DisplayName.Length > 0
            ? user.DisplayName
            : user?.Name ?? gameData.LocalPlayer?.Name.TextValue ?? string.Empty;
        var body = Loc.T(L.Setup.SignedInBody, displayName);
        var avatarRadius = 38f * scale;
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Xl * scale + avatarRadius * 2f;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.CheckCircle, SignedInTint,
            Loc.T(L.Setup.SignedInTitle), body, theme, top);
        if (user is not null)
        {
            var avatarCenter = new Vector2(screen.Center.X + offset.X,
                y + Metrics.Space.Xl * scale + avatarRadius);
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, user.Name, user.World, user.AvatarUrl,
                images, lodestone, 1.6f, 48, alpha, Frames.Of(user.FrameId));
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme, alpha, live))
        {
            AdvancePage();
        }
    }

    private void DrawAccountXivAuth(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Account.XivIntro);
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Lg * scale + 54f * scale + 76f * scale;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.ShieldAlt, theme.Accent,
            Loc.T(L.Account.XivTitle), body, theme, top);
        if (flow.XivUserCode.Length > 0)
        {
            var codeRect = CardRect(screen, offset, y + Metrics.Space.Lg * scale, 54f * scale);
            if (DrawCodeCard(drawList, codeRect, theme, flow.XivUserCode, alpha, live))
            {
                ImGui.SetClipboardText(flow.XivUserCode);
            }

            y = codeRect.Max.Y;
        }

        var waitCenter = new Vector2(screen.Center.X + offset.X, y + Metrics.Space.Xxl * scale);
        LoadingPulse.Spinner(waitCenter, 9f * scale, theme.Accent, alpha, drawList);
        Typography.DrawCentered(drawList, waitCenter + new Vector2(0f, Metrics.Space.Xl * scale),
            Loc.T(L.Account.XivWaiting), Fade(theme.TextMuted, alpha), TextStyles.Footnote);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 0);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.XivOpen), theme, alpha, live) &&
            flow.XivVerificationUri is { } verificationUri)
        {
            UrlActions.OpenInBrowser(verificationUri);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Common.Cancel), theme, alpha, live))
        {
            flow.CancelXivAuth();
        }
    }

    private void DrawAccountLodestone(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Account.VerifyIntro);
        var stepsWidth = ContentWidth(screen);
        var step1 = Loc.T(L.Account.Step1);
        var step2 = Loc.T(L.Account.Step2);
        var step3 = Loc.T(L.Account.Step3);
        var step4 = Loc.T(L.Account.Step4);
        var stepsHeight = StepLineHeight(step1, stepsWidth, scale) + StepLineHeight(step2, stepsWidth, scale) +
                          StepLineHeight(step3, stepsWidth, scale) + StepLineHeight(step4, stepsWidth, scale);
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Lg * scale + 54f * scale +
                            Metrics.Space.Lg * scale + stepsHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.Key, theme.Accent,
            Loc.T(L.Account.VerifyTitle), body, theme, top);
        var codeRect = CardRect(screen, offset, y + Metrics.Space.Lg * scale, 54f * scale);
        if (DrawCodeCard(drawList, codeRect, theme, flow.ChallengeCode, alpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        y = codeRect.Max.Y + Metrics.Space.Lg * scale;
        var stepsLeft = codeRect.Min.X;
        y = DrawStepLine(drawList, "1", step1, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "2", step2, stepsLeft, y, stepsWidth, alpha, theme, scale);
        y = DrawStepLine(drawList, "3", step3, stepsLeft, y, stepsWidth, alpha, theme, scale);
        DrawStepLine(drawList, "4", step4, stepsLeft, y, stepsWidth, alpha, theme, scale);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 2);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.CopyCode), theme, alpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Account.OpenProfile), theme, alpha, live))
        {
            UrlActions.OpenInBrowser(LodestoneProfileUrl);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.VerifyAdded), theme, alpha, live,
                !flow.Busy))
        {
            flow.VerifyChallenge();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Common.Cancel), theme, alpha, live))
        {
            flow.Reset();
        }

        DrawStatusLine(drawList, screen, theme, offset, alpha, 3);
    }

    private void DrawIdentity(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        if (pickingPhoto)
        {
            DrawPhotoPicker(screen, theme);
            return;
        }

        if (!profilePrefilled && session.CurrentUser is { } current)
        {
            displayNameDraft = current.DisplayName ?? string.Empty;
            handleDraft = SanitizeHandle(current.Handle ?? string.Empty);
            profilePrefilled = true;
        }

        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var user = session.CurrentUser;
        var body = Loc.T(L.Setup.IdBody);
        var hint = handleRejected ? Loc.T(L.Setup.HandleTaken) : Loc.T(L.Setup.HandleRules);
        var bodyWidth = BodyWidth(screen);
        var avatarRadius = 44f * scale;
        var titleHeight = LineBlock(TextStyles.Title1);
        var fieldHeight = FieldHeightUnits * scale;
        var labelBlock = LineBlock(TextStyles.Footnote) + Metrics.Space.Xs * scale;
        var contentHeight = avatarRadius * 2f + Metrics.Space.Xl * scale + titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth) + Metrics.Space.Xl * scale +
                            labelBlock + fieldHeight + Metrics.Space.Lg * scale + labelBlock + fieldHeight +
                            Metrics.Space.Md * scale + WrappedHeight(hint, TextStyles.Footnote, bodyWidth);
        var top = CenteredTop(screen, contentHeight, 2) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var avatarCenter = new Vector2(centerX, top + avatarRadius);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme,
            user?.Name ?? gameData.LocalPlayer?.Name.TextValue ?? string.Empty,
            user?.World ?? gameData.WorldName(gameData.LocalHomeWorldId), user?.AvatarUrl, images, lodestone, 2f, 64,
            alpha, Frames.Of(user?.FrameId));
        DrawPhotoBadge(drawList, theme, avatarCenter, avatarRadius, alpha);
        if (live && !avatarBusy && UiInteract.HoverClickCircle(avatarCenter, avatarRadius))
        {
            picker.Open();
            pickingPhoto = true;
        }

        var titleCenter = new Vector2(centerX,
            top + avatarRadius * 2f + Metrics.Space.Xl * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, Loc.T(L.Setup.IdTitle), Fade(theme.TextStrong, alpha),
            TextStyles.Title1);
        var bodyBottom = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(theme.TextMuted, alpha),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + Metrics.Space.Md * scale), bodyWidth);
        var nameRect = CardRect(screen, offset, bodyBottom + Metrics.Space.Xl * scale + labelBlock, fieldHeight);
        DrawField(drawList, nameRect, theme, "setupDisplayName", Loc.T(L.Setup.DisplayNameLabel),
            ref displayNameDraft, DisplayNameMax, alpha, live);
        var handleRect = CardRect(screen, offset, nameRect.Max.Y + Metrics.Space.Lg * scale + labelBlock,
            fieldHeight);
        DrawField(drawList, handleRect, theme, "setupHandle", Loc.T(L.Setup.HandleLabel), ref handleDraft, HandleMax,
            alpha, live, "@");
        handleDraft = SanitizeHandle(handleDraft);
        var hintInk = handleRejected ? Fade(theme.Danger, alpha) : Fade(theme.TextMuted, alpha);
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, hintInk,
            new Vector2(centerX, handleRect.Max.Y + Metrics.Space.Md * scale), bodyWidth);
        var valid = !profileSaving && IsHandleValid(handleDraft) && displayNameDraft.Trim().Length > 0;
        var saveLabel = profileSaving ? Loc.T(L.Account.Saving) : Loc.T(L.Onboarding.Continue);
        if (Primary(drawList, ButtonRect(screen, offset, 1), saveLabel, theme, alpha, live, valid))
        {
            SaveProfile();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SkipForNow), theme, alpha, live))
        {
            AdvancePage();
        }
    }

    private static void DrawPhotoBadge(ImDrawListPtr drawList, PhoneTheme theme, Vector2 avatarCenter,
        float avatarRadius, float alpha)
    {
        var scale = UiScale.Current;
        var badgeRadius = 15f * scale;
        var reach = avatarRadius * 0.72f;
        var badgeCenter = new Vector2(avatarCenter.X + reach, avatarCenter.Y + reach);
        drawList.AddCircleFilled(badgeCenter, badgeRadius + 2.5f * scale,
            ImGui.GetColorU32(Fade(theme.AppBackground, alpha)), 32);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Fade(theme.Accent, alpha)), 32);
        AppSkin.Icon(drawList, badgeCenter, IconGlyph.Of(FontAwesomeIcon.Camera), new Vector4(1f, 1f, 1f, alpha),
            0.6f);
    }

    private void DrawPhotoPicker(Rect screen, PhoneTheme theme)
    {
        var scale = UiScale.Current;
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
            AvatarUploader.Upload(account, media, session, picker.SourcePath, picker.Crop, cancellation.Token,
                outcome =>
                {
                    avatarBusy = false;
                    avatarFailure = outcome;
                    avatarOutcome = outcome == AvatarUploadOutcome.Uploaded ? 1 : 2;
                });
        }
    }

    private void DrawFeatures(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Onboarding.AllInOneBody);
        var bodyWidth = BodyWidth(screen);
        var titleHeight = LineBlock(TextStyles.LargeTitle);
        var rowHeight = 62f * scale;
        var contentHeight = titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth) + Metrics.Space.Xl * scale +
                            Features.Length * rowHeight;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f),
            Loc.T(L.Onboarding.AllInOneTitle), Fade(theme.TextStrong, alpha), TextStyles.LargeTitle);
        var y = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, Fade(theme.TextMuted, alpha),
            new Vector2(centerX, top + titleHeight + Metrics.Space.Md * scale), bodyWidth);
        y += Metrics.Space.Xl * scale;
        var rowWidth = ContentWidth(screen);
        var rowLeft = centerX - rowWidth * 0.5f;
        for (var index = 0; index < Features.Length; index++)
        {
            y = DrawFeatureRow(drawList, Features[index], theme, rowLeft, y, rowWidth, alpha, scale);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), theme, alpha, live))
        {
            AdvancePage();
        }
    }

    private static float DrawFeatureRow(ImDrawListPtr drawList, in FeatureRow row, PhoneTheme theme, float left,
        float top, float width, float alpha, float scale)
    {
        var tile = 40f * scale;
        var tileRect = new Rect(new Vector2(left, top), new Vector2(left + tile, top + tile));
        Squircle.Fill(drawList, tileRect.Min, tileRect.Max, 12f * scale,
            ImGui.GetColorU32(Fade(row.Tint, 0.92f * alpha)));
        AppSkin.Icon(drawList, tileRect.Center, IconGlyph.Of(row.Icon), new Vector4(1f, 1f, 1f, alpha), 0.86f);
        var textLeft = tileRect.Max.X + Metrics.Space.Md * scale;
        var textWidth = width - tile - Metrics.Space.Md * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(row.Title), textWidth, TextStyles.Headline), Fade(theme.TextStrong, alpha),
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + Metrics.Space.Xl * scale),
            Typography.FitText(Loc.T(row.Body), textWidth, TextStyles.Footnote), Fade(theme.TextMuted, alpha),
            TextStyles.Footnote);
        return top + 62f * scale;
    }

    private void DrawReady(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        DrawHeroPage(drawList, screen, theme, offset, alpha, Loc.T(L.Onboarding.WelcomeTitle), TextStyles.Title1,
            Loc.T(L.Setup.ReadyBody));
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Setup.StartUsing), theme, alpha, live))
        {
            Complete();
        }
    }

    private static void DrawHeroPage(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, Vector2 offset,
        float alpha, string title, in TextStyle titleStyle, string body)
    {
        var scale = UiScale.Current;
        var bodyWidth = BodyWidth(screen);
        var heroHeight = 170f * scale;
        var titleHeight = LineBlock(titleStyle);
        var contentHeight = heroHeight + Metrics.Space.Xl * scale + titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth);
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        OnboardingHero.Draw(drawList, new Vector2(centerX, top + heroHeight * 0.5f), HeroMotif.Constellation,
            theme.Accent, scale, 1f, alpha);
        var titleCenter = new Vector2(centerX,
            top + heroHeight + Metrics.Space.Xl * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, title, Fade(theme.TextStrong, alpha), titleStyle);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, Fade(theme.TextMuted, alpha),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + Metrics.Space.Md * scale), bodyWidth);
    }

    private static float HeaderHeight(Rect screen, string body)
    {
        var scale = UiScale.Current;
        return 60f * scale + Metrics.Space.Xl * scale + LineBlock(TextStyles.Title1) + Metrics.Space.Md * scale +
               WrappedHeight(body, TextStyles.Subheadline, BodyWidth(screen));
    }

    private static float DrawHeader(ImDrawListPtr drawList, Rect screen, Vector2 offset, float alpha,
        FontAwesomeIcon icon, Vector4 tint, string title, string body, PhoneTheme theme, float top)
    {
        var scale = UiScale.Current;
        var centerX = screen.Center.X + offset.X;
        var tileHalf = 30f * scale;
        var tileCenter = new Vector2(centerX, top + tileHalf);
        Squircle.Fill(drawList, tileCenter - new Vector2(tileHalf, tileHalf),
            tileCenter + new Vector2(tileHalf, tileHalf), 18f * scale,
            ImGui.GetColorU32(Fade(tint, 0.92f * alpha)));
        AppSkin.Icon(drawList, tileCenter, IconGlyph.Of(icon), new Vector4(1f, 1f, 1f, alpha), 1.25f);
        var titleHeight = LineBlock(TextStyles.Title1);
        var titleCenter = new Vector2(centerX,
            top + tileHalf * 2f + Metrics.Space.Xl * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter, title, Fade(theme.TextStrong, alpha), TextStyles.Title1);
        var bodyTop = titleCenter.Y + titleHeight * 0.5f + Metrics.Space.Md * scale;
        return Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, Fade(theme.TextMuted, alpha),
            new Vector2(centerX, bodyTop), BodyWidth(screen));
    }

    private void DrawStatusLine(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, Vector2 offset, float alpha,
        int buttonSlots)
    {
        var message = flow.Status;
        if (message.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var center = new Vector2(screen.Center.X + offset.X,
            ButtonRect(screen, offset, buttonSlots - 1).Min.Y - Metrics.Space.Lg * scale);
        Typography.DrawCentered(drawList, center, message, Fade(theme.TextMuted, alpha), TextStyles.Footnote);
    }

    private void DrawBackButton(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float alpha, bool live)
    {
        if (page == SetupPage.Welcome || pickingPhoto || exiting)
        {
            return;
        }

        if (page == SetupPage.Account && (flow.XivAuthActive || flow.LodestoneActive))
        {
            return;
        }

        var scale = UiScale.Current;
        var label = Loc.T(L.Setup.Back);
        var labelSize = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var chevron = 13f * scale;
        var padding = Metrics.Space.Md * scale;
        var height = 34f * scale;
        var min = new Vector2(screen.Min.X + Metrics.Space.Lg * scale, screen.Min.Y + Metrics.Space.Lg * scale);
        var max = new Vector2(min.X + padding * 2f + chevron + Metrics.Space.Xs * scale + labelSize.X,
            min.Y + height);
        var hovered = live && UiInteract.Hover(min, max);
        var radius = height * 0.5f;
        Squircle.Fill(drawList, min, max, radius,
            ImGui.GetColorU32(Fade(theme.GroupedCard, (hovered ? 1f : 0.9f) * alpha)));
        Material.EdgeSquircle(drawList, min, max, radius, scale, alpha);
        var chevronCenter = new Vector2(min.X + padding + chevron * 0.5f, (min.Y + max.Y) * 0.5f);
        _ = BackButton.Draw("setup.back", chevronCenter, chevron, Fade(theme.Accent, alpha), hovered, scale);
        Typography.Draw(drawList,
            new Vector2(chevronCenter.X + chevron * 0.5f + Metrics.Space.Xs * scale,
                chevronCenter.Y - labelSize.Y * 0.5f), label, Fade(theme.Accent, alpha),
            TextStyles.SubheadlineEmphasized);
        if (live && UiInteract.Click(min, max, hovered))
        {
            BackPage();
        }
    }

    private static void DrawCard(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float alpha)
    {
        var scale = UiScale.Current;
        var radius = Metrics.Radius.Card * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(theme.GroupedCard, alpha)));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale, alpha);
    }

    private static bool DrawSelectRow(ImDrawListPtr drawList, Rect row, PhoneTheme theme, string label,
        bool selected, float alpha, bool live, bool first, bool last)
    {
        var scale = UiScale.Current;
        var radius = Metrics.Radius.Card * scale;
        var padding = Metrics.Space.Lg * scale;
        var hovered = live && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var tint = ImGui.GetColorU32(Fade(theme.SurfaceMuted, alpha));
            if (first)
            {
                Squircle.FillCap(drawList, row.Min, row.Max, radius, tint, true);
            }
            else if (last)
            {
                Squircle.FillCap(drawList, row.Min, row.Max, radius, tint, false);
            }
            else
            {
                drawList.AddRectFilled(row.Min, row.Max, tint);
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!first)
        {
            drawList.AddLine(new Vector2(row.Min.X + padding, row.Min.Y), new Vector2(row.Max.X, row.Min.Y),
                ImGui.GetColorU32(Fade(theme.Separator, alpha)), Metrics.Stroke.Hairline);
        }

        var checkColumn = 30f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(row.Min.X + padding, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, row.Width - padding * 2f - checkColumn, TextStyles.Body),
            Fade(theme.TextStrong, alpha), TextStyles.Body);
        if (selected)
        {
            AppSkin.Icon(drawList, new Vector2(row.Max.X - padding - 5f * scale, row.Center.Y),
                IconGlyph.Of(FontAwesomeIcon.Check), Fade(theme.Accent, alpha), 0.95f);
        }

        return live && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawIdentityCard(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, Vector2 offset,
        float top, string name, string world, float alpha)
    {
        var scale = UiScale.Current;
        var rect = CardRect(screen, offset, top, 62f * scale);
        DrawCard(drawList, rect, theme, alpha);
        var left = rect.Min.X + Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + Metrics.Space.Sm * scale),
            Loc.T(L.Account.SigningInAs), Fade(theme.TextMuted, alpha), TextStyles.Footnote);
        var identityMaxWidth = rect.Max.X - Metrics.Space.Lg * scale - left;
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + 30f * scale),
            Typography.FitText($"{name}@{world}", identityMaxWidth, TextStyles.Headline),
            Fade(theme.TextStrong, alpha), TextStyles.Headline);
    }

    private static bool DrawCodeCard(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, string value, float alpha,
        bool live)
    {
        var scale = UiScale.Current;
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Card * scale;
        var fill = hovered ? Palette.Mix(theme.GroupedCard, theme.Accent, 0.14f) : theme.GroupedCard;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(fill, alpha)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Fade(theme.Accent, 0.55f * alpha)), Metrics.Stroke.Thin * scale);
        Typography.DrawCentered(drawList, rect.Center, value, Fade(theme.Accent, alpha), TextStyles.Title3);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static float StepLineHeight(string text, float width, float scale)
    {
        var textWidth = width - 32f * scale;
        var lines = Typography.CountWrappedLines(text, TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.25f;
        return MathF.Max(20f * scale, lines * lineHeight) + Metrics.Space.Sm * scale;
    }

    private static float DrawStepLine(ImDrawListPtr drawList, string number, string text, float left, float top,
        float width, float alpha, PhoneTheme theme, float scale)
    {
        var badgeRadius = 10f * scale;
        var badgeCenter = new Vector2(left + badgeRadius, top + badgeRadius);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Fade(theme.Accent, alpha)), 24);
        Typography.DrawCentered(drawList, badgeCenter, number, new Vector4(1f, 1f, 1f, alpha), TextStyles.Caption2);
        var textLeft = left + badgeRadius * 2f + Metrics.Space.Md * scale;
        var textWidth = width - badgeRadius * 2f - Metrics.Space.Md * scale;
        var lines = Typography.WrapText(text, TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.25f;
        var textTop = top + badgeRadius - lineHeight * 0.5f + 2f * scale;
        var ink = Fade(theme.TextStrong, 0.88f * alpha);
        for (var index = 0; index < lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + index * lineHeight), lines[index], ink,
                TextStyles.Footnote);
        }

        var bottom = MathF.Max(badgeRadius * 2f, lines.Length * lineHeight);
        return top + bottom + Metrics.Space.Sm * scale;
    }

    private static void DrawField(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, string id, string label,
        ref string value, int maxLength, float alpha, bool live, string? prefix = null)
    {
        var scale = UiScale.Current;
        var labelMaxWidth = rect.Width - 2f * scale;
        var labelHeight = LineBlock(TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(rect.Min.X + Metrics.Space.Xxs * scale,
                rect.Min.Y - labelHeight - Metrics.Space.Xs * scale),
            Typography.FitText(label, labelMaxWidth, TextStyles.Footnote), Fade(theme.TextMuted, alpha),
            TextStyles.Footnote);
        DrawCard(drawList, rect, theme, alpha);
        var textLeft = rect.Min.X + Metrics.Space.Md * scale;
        if (prefix is not null)
        {
            var prefixSize = Typography.Measure(prefix, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - prefixSize.Y * 0.5f), prefix,
                Fade(theme.TextMuted, alpha), TextStyles.Body);
            textLeft += prefixSize.X + Metrics.Space.Xxs * scale;
        }

        if (!live)
        {
            if (value.Length > 0)
            {
                var valueSize = Typography.Measure(value, TextStyles.Body);
                var valueMaxWidth = rect.Max.X - Metrics.Space.Md * scale - textLeft;
                Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - valueSize.Y * 0.5f),
                    Typography.FitText(value, valueMaxWidth, TextStyles.Body), Fade(theme.TextStrong, alpha),
                    TextStyles.Body);
            }

            return;
        }

        ImGui.SetCursorScreenPos(new Vector2(textLeft, rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Max.X - Metrics.Space.Md * scale - textLeft);
        var transparent = new Vector4(0f, 0f, 0f, 0f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, transparent)
                   .Push(ImGuiCol.FrameBgHovered, transparent)
                   .Push(ImGuiCol.FrameBgActive, transparent)
                   .Push(ImGuiCol.Text, theme.TextStrong))
        {
            ImGui.InputText($"##{id}", ref value, maxLength);
        }
    }

    private static Rect CardRect(Rect screen, Vector2 offset, float top, float height)
    {
        var width = ContentWidth(screen);
        var left = screen.Center.X + offset.X - width * 0.5f;
        return new Rect(new Vector2(left, top), new Vector2(left + width, top + height));
    }

    private static Rect ButtonRect(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var scale = UiScale.Current;
        var width = ContentWidth(screen);
        var height = ButtonHeightUnits * scale;
        var bottom = screen.Max.Y - 62f * scale -
                     slotFromBottom * (height + Metrics.Space.Md * scale) + offset.Y;
        var left = screen.Center.X + offset.X - width * 0.5f;
        return new Rect(new Vector2(left, bottom - height), new Vector2(left + width, bottom));
    }

    private static Vector2 TextActionCenter(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var rect = ButtonRect(screen, offset, slotFromBottom);
        return new Vector2(rect.Center.X, rect.Max.Y - Metrics.Space.Lg * UiScale.Current);
    }

    private static (Rect Left, Rect Right) HalfButtonRects(Rect screen, Vector2 offset, int slotFromBottom)
    {
        var scale = UiScale.Current;
        var full = ButtonRect(screen, offset, slotFromBottom);
        var gap = Metrics.Space.Sm * scale;
        var half = (full.Width - gap) * 0.5f;
        return (new Rect(full.Min, new Vector2(full.Min.X + half, full.Max.Y)),
            new Rect(new Vector2(full.Max.X - half, full.Min.Y), full.Max));
    }

    private static bool Primary(ImDrawListPtr drawList, Rect rect, string label, PhoneTheme theme, float alpha,
        bool live, bool enabled = true)
    {
        UiAnchors.Report("setup.primary", rect);
        var hovered = live && enabled && UiInteract.Hover(rect.Min, rect.Max);
        var fill = hovered ? Palette.Mix(theme.Accent, Vector4.One, 0.14f) : theme.Accent;
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Fade(fill, (enabled ? 1f : 0.4f) * alpha)));
        Typography.DrawCentered(drawList, rect.Center,
            Typography.FitText(label, rect.Width - Metrics.Space.Xl * UiScale.Current, TextStyles.Headline),
            new Vector4(1f, 1f, 1f, (enabled ? 1f : 0.72f) * alpha), TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool Secondary(ImDrawListPtr drawList, Rect rect, string label, PhoneTheme theme, float alpha,
        bool live)
    {
        var scale = UiScale.Current;
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Palette.Mix(theme.GroupedCard, theme.Accent, 0.12f) : theme.GroupedCard;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(fill, alpha)));
        Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale, alpha);
        Typography.DrawCentered(drawList, rect.Center,
            Typography.FitText(label, rect.Width - Metrics.Space.Xl * scale, TextStyles.Headline),
            Fade(theme.TextStrong, alpha), TextStyles.Headline);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static bool TextAction(ImDrawListPtr drawList, Vector2 center, string label, PhoneTheme theme,
        float alpha, bool live)
    {
        var scale = UiScale.Current;
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var padding = new Vector2(Metrics.Space.Md * scale, Metrics.Space.Sm * scale);
        var min = center - size * 0.5f - padding;
        var max = center + size * 0.5f + padding;
        var hovered = live && UiInteract.Hover(min, max);
        var ink = hovered ? Palette.Mix(theme.Accent, Vector4.One, 0.25f) : theme.Accent;
        Typography.DrawCentered(drawList, center, label, Fade(ink, alpha), TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(min, max, hovered);
    }
}
