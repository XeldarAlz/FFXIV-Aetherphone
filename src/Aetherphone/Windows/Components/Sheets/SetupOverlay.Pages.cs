using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
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
    private const float TopMarginUnits = 64f;
    private const float ButtonsGapUnits = 24f;
    private const float FieldHeightUnits = 46f;
    private const float SideMarginUnits = 24f;
    private const float ContentMaxUnits = 320f;
    private const float ButtonHeightUnits = 50f;
    private const float PreviewTileUnits = 224f;
    private const float WelcomeMarkUnits = 132f;
    private const float WelcomeMarkScreenShare = 0.36f;
    private const float ReadyHeroScale = 1.65f;
    private const float ReadyHeroUnits = 196f;
    private const float HeaderGlyphUnits = 64f;
    private const float FeatureIconUnits = 46f;
    private const float BackButtonUnits = 38f;
    private const float RevealDelaySeconds = 0.08f;
    private const float RevealStaggerSeconds = 0.075f;
    private const float RevealRiseUnits = 16f;
    private const float ShockwaveDelaySeconds = 0.22f;
    private const float ShockwaveSeconds = 1.25f;
    private const float BurstDelaySeconds = 0.18f;
    private const float BurstSeconds = 0.95f;
    private const int BurstSparks = 10;
    private const double FloatPeriodMs = 6200.0;
    private const double GlintPeriodMs = 4200.0;
    private const float FloatUnits = 3f;
    private const float ExitZoom = 0.55f;

    private static readonly SetupInk LightInk = new(new Vector4(0.07f, 0.05f, 0.13f, 0.95f),
        new Vector4(0.24f, 0.20f, 0.34f, 0.70f), new Vector4(0f, 0f, 0f, 0.08f), new Vector4(0f, 0f, 0f, 0.045f),
        new Vector4(0.44f, 0.27f, 0.92f, 1f), new Vector4(0.84f, 0.20f, 0.22f, 1f),
        new Vector4(0.95f, 0.94f, 0.99f, 1f), new Vector4(0f, 0f, 0f, 0.08f), new Vector4(0.07f, 0.05f, 0.13f, 0.38f));

    private static readonly SetupInk DarkInk = new(new Vector4(1f, 1f, 1f, 0.97f),
        new Vector4(0.90f, 0.88f, 1f, 0.66f), new Vector4(1f, 1f, 1f, 0.10f), new Vector4(1f, 1f, 1f, 0.06f),
        BrandMark.Lilac, new Vector4(1f, 0.55f, 0.55f, 1f), BrandMark.Night, new Vector4(1f, 1f, 1f, 0.14f),
        new Vector4(1f, 1f, 1f, 0.45f));

    private readonly record struct SetupInk(Vector4 Strong, Vector4 Muted, Vector4 Hairline, Vector4 Wash,
        Vector4 Accent, Vector4 Danger, Vector4 Base, Vector4 Disabled, Vector4 DisabledText);

    private static SetupInk ink = DarkInk;
    private static GlassTone glass = GlassTone.Dark;

    private static void ResolveInk(float darkness)
    {
        ink = new SetupInk(Vector4.Lerp(LightInk.Strong, DarkInk.Strong, darkness),
            Vector4.Lerp(LightInk.Muted, DarkInk.Muted, darkness),
            Vector4.Lerp(LightInk.Hairline, DarkInk.Hairline, darkness),
            Vector4.Lerp(LightInk.Wash, DarkInk.Wash, darkness),
            Vector4.Lerp(LightInk.Accent, DarkInk.Accent, darkness),
            Vector4.Lerp(LightInk.Danger, DarkInk.Danger, darkness),
            Vector4.Lerp(LightInk.Base, DarkInk.Base, darkness),
            Vector4.Lerp(LightInk.Disabled, DarkInk.Disabled, darkness),
            Vector4.Lerp(LightInk.DisabledText, DarkInk.DisabledText, darkness));
        glass = darkness >= 0.5f ? GlassTone.Dark : GlassTone.Light;
    }
    private static readonly Vector4 PreviewInk = new(1f, 1f, 1f, 0.96f);
    private static readonly Vector4 PreviewShadow = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 PreviewBadgeWarm = new(0.95f, 0.38f, 0.62f, 1f);
    private static readonly Vector4 PreviewBadgeCool = new(0.42f, 0.52f, 0.96f, 1f);
    private static readonly Vector4 LightPreviewFallback = new(0.86f, 0.88f, 0.93f, 1f);
    private static readonly Vector4 DarkPreviewFallback = new(0.07f, 0.07f, 0.11f, 1f);
    private static readonly Vector4 SignedInTint = new(0.36f, 0.86f, 0.48f, 1f);

    private string risingStonesUuidDraft = string.Empty;

    private readonly record struct FeatureRow(string AppId, LocString Title, LocString Body);

    private static readonly FeatureRow[] Features =
    {
        new("messages", L.Setup.FeatureMessageTitle, L.Setup.FeatureMessageBody),
        new("aethergram", L.Setup.FeatureSocialTitle, L.Setup.FeatureSocialBody),
        new("market", L.Setup.FeatureToolsTitle, L.Setup.FeatureToolsBody),
        new("music", L.Setup.FeaturePlayTitle, L.Setup.FeaturePlayBody),
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

    private float Reveal(int order) =>
        drawingCurrent
            ? Spring.Settle(pageAge - RevealDelaySeconds - order * RevealStaggerSeconds, Motion.Sheet)
            : 1f;

    private static float Rise(float reveal) => (1f - reveal) * RevealRiseUnits * UiScale.Current;

    private float Timeline(float delay, float duration) =>
        drawingCurrent ? (pageAge - delay) / duration : 2f;

    private static float Float(float scale) =>
        MathF.Sin(Pulse.Phase(FloatPeriodMs) * MathF.PI * 2f) * FloatUnits * scale;

    private static void DrawBackdrop(ImDrawListPtr drawList, Rect screen, float alpha, float rounding,
        float darkness) =>
        BrandMark.DrawStage(drawList, screen, rounding, alpha, true, darkness);

    private void DrawWelcome(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var title = Loc.T(L.Setup.WelcomeTitle);
        var body = Loc.T(L.Setup.WelcomeBody);
        var bodyWidth = BodyWidth(screen);
        var markSize = MathF.Min(WelcomeMarkUnits * scale, screen.Width * WelcomeMarkScreenShare);
        var titleHeight = LineBlock(TextStyles.Hero);
        var markGap = Metrics.Space.Xxl * scale * 1.4f;
        var contentHeight = markSize + markGap + titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth);
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var markReveal = Reveal(0);
        var markCenter = new Vector2(centerX,
            top + markSize * 0.5f + Float(scale) * markReveal + Rise(markReveal) * 1.6f);
        BrandMark.Shockwave(drawList, markCenter, markSize, Timeline(ShockwaveDelaySeconds, ShockwaveSeconds),
            alpha, scale);
        BrandMark.TryDrawEmblem(drawList, markCenter, markSize * (0.70f + 0.30f * markReveal), alpha * markReveal);
        var titleReveal = Reveal(3);
        var titleTop = top + markSize + markGap + Rise(titleReveal);
        DrawWordmark(drawList, new Vector2(centerX, titleTop + titleHeight * 0.5f), title, ContentWidth(screen),
            alpha * titleReveal);
        var bodyReveal = Reveal(4);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, Fade(ink.Muted, alpha * bodyReveal),
            new Vector2(centerX, titleTop + titleHeight + Metrics.Space.Md * scale + Rise(bodyReveal)), bodyWidth);
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.GetStarted), alpha * Reveal(6),
                live))
        {
            AdvancePage();
        }
    }

    private static void DrawWordmark(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth,
        float alpha)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        var fitted = Typography.FitText(text, maxWidth, TextStyles.Hero);
        var size = Typography.Measure(fitted, TextStyles.Hero);
        var effect = new TextEffect(NameEffectKind.Glint, Fade(BrandMark.Lilac, alpha),
            Pulse.Phase(GlintPeriodMs));
        Typography.Draw(drawList, new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f), fitted,
            Fade(ink.Strong, alpha), TextStyles.Hero, effect);
    }

    private void DrawLanguage(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var languages = Languages.All;
        var rowHeight = Metrics.Size.Row * scale;
        var glyphHeight = HeaderGlyphUnits * scale;
        var titleHeight = LineBlock(TextStyles.Title1);
        var listHeight = languages.Length * rowHeight;
        var contentHeight = glyphHeight + Metrics.Space.Xl * scale + titleHeight + Metrics.Space.Xl * scale +
                            listHeight;
        var top = CenteredTop(screen, contentHeight, 0) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        DrawGlyphBadge(drawList, new Vector2(centerX, top + glyphHeight * 0.5f), FontAwesomeIcon.Globe,
            ink.Accent, alpha, Reveal(0));
        var titleReveal = Reveal(1);
        var titleCenter = new Vector2(centerX,
            top + glyphHeight + Metrics.Space.Xl * scale + titleHeight * 0.5f + Rise(titleReveal));
        Typography.DrawCentered(drawList, titleCenter, Loc.T(L.Settings.Language),
            Fade(ink.Strong, alpha * titleReveal), TextStyles.Title1);
        var listAlpha = alpha * Reveal(2);
        var card = CardRect(screen, offset,
            top + glyphHeight + Metrics.Space.Xl * scale + titleHeight + Metrics.Space.Xl * scale, listHeight);
        DrawCard(drawList, card, listAlpha);
        var picked = -1;
        for (var index = 0; index < languages.Length; index++)
        {
            var rowTop = card.Min.Y + index * rowHeight;
            var row = new Rect(new Vector2(card.Min.X, rowTop), new Vector2(card.Max.X, rowTop + rowHeight));
            if (DrawSelectRow(drawList, row, languages[index].NativeName,
                    languages[index].Code == Loc.Current.Code, listAlpha, live, index == 0,
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
        var titleReveal = Reveal(0);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f + Rise(titleReveal)),
            Loc.T(L.Setup.AppearanceTitle), Fade(ink.Strong, alpha * titleReveal), TextStyles.LargeTitle);
        var bodyReveal = Reveal(1);
        var bodyBottom = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(ink.Muted, alpha * bodyReveal),
            new Vector2(centerX, top + titleHeight + Metrics.Space.Md * scale + Rise(bodyReveal)), bodyWidth) -
            Rise(bodyReveal);
        var tilesTop = bodyBottom + Metrics.Space.Xl * scale;
        var gap = Metrics.Space.Xl * scale;
        var lightReveal = Reveal(2);
        var darkReveal = Reveal(3);
        var lightLeft = centerX - gap * 0.5f - tileWidth;
        var lightTop = tilesTop + Rise(lightReveal) * 2f;
        var lightRect = new Rect(new Vector2(lightLeft, lightTop),
            new Vector2(lightLeft + tileWidth, lightTop + tileHeight));
        var darkLeft = centerX + gap * 0.5f;
        var darkTop = tilesTop + Rise(darkReveal) * 2f;
        var darkRect = new Rect(new Vector2(darkLeft, darkTop),
            new Vector2(darkLeft + tileWidth, darkTop + tileHeight));
        var pickedLight = DrawAppearanceTile(drawList, lightRect, theme, false,
            !dynamicAppearance && !prefersDark, alpha * lightReveal, live);
        var pickedDark = DrawAppearanceTile(drawList, darkRect, theme, true, !dynamicAppearance && prefersDark,
            alpha * darkReveal, live);
        var captionTop = tilesTop + tileHeight + Metrics.Space.Md * scale;
        DrawAppearanceCaption(drawList, lightRect.Center.X, captionTop, Loc.T(L.Settings.ThemeLight),
            !dynamicAppearance && !prefersDark, alpha * lightReveal);
        DrawAppearanceCaption(drawList, darkRect.Center.X, captionTop, Loc.T(L.Settings.ThemeDark),
            !dynamicAppearance && prefersDark, alpha * darkReveal);
        if (pickedLight || pickedDark)
        {
            prefersDark = pickedDark;
            dynamicAppearance = false;
            ApplyAppearance();
        }

        var cardAlpha = alpha * Reveal(4);
        var card = CardRect(screen, offset, captionTop + captionHeight + Metrics.Space.Xl * scale, rowHeight);
        DrawCard(drawList, card, cardAlpha);
        var labelSize = Typography.Measure(Loc.T(L.Setup.AppearanceDynamic), TextStyles.BodyEmphasized);
        Typography.Draw(drawList,
            new Vector2(card.Min.X + Metrics.Space.Lg * scale, card.Center.Y - labelSize.Y * 0.5f),
            Loc.T(L.Setup.AppearanceDynamic), Fade(ink.Strong, cardAlpha), TextStyles.BodyEmphasized);
        var toggleSize = new Vector2(Metrics.Size.ToggleWidth * scale, Metrics.Size.ToggleHeight * scale);
        var toggleMin = new Vector2(card.Max.X - Metrics.Space.Lg * scale - toggleSize.X,
            card.Center.Y - toggleSize.Y * 0.5f);
        var dynamicNext = Toggle.Draw("setup.dynamicAppearance", new Rect(toggleMin, toggleMin + toggleSize),
            dynamicAppearance, theme, cardAlpha, live);
        if (dynamicNext != dynamicAppearance)
        {
            dynamicAppearance = dynamicNext;
            ApplyAppearance();
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), alpha * Reveal(5), live))
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
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        if (selected)
        {
            BrandMark.Glow(drawList, rect.Center, MathF.Min(rect.Width, rect.Height) * 0.9f, alpha);
        }

        Elevation.Floating(drawList, rect.Min, rect.Max, radius, scale, alpha);
        var entry = Plugin.Wallpapers.Resolve(dark ? theme.DarkWallpaperId : theme.LightWallpaperId);
        var fallback = dark ? DarkPreviewFallback : LightPreviewFallback;
        WallpaperRenderer.DrawSingle(drawList, rect, radius, entry, aspect, alpha, Fade(fallback, alpha));
        var clockCenter = new Vector2(rect.Center.X, rect.Min.Y + rect.Height * 0.24f);
        var clock = TimeText.Clock(DateTime.Now);
        Typography.DrawCentered(drawList, clockCenter + new Vector2(0f, 1f * scale), clock,
            Fade(PreviewShadow, alpha), TextStyles.Title3);
        Typography.DrawCentered(drawList, clockCenter, clock, Fade(PreviewInk, alpha), TextStyles.Title3);
        DrawPreviewNotifications(drawList, rect, dark, alpha);
        var ring = selected ? ink.Accent : hovered ? ink.Muted : ink.Hairline;
        var ringPad = selected ? 3f * scale : 0f;
        Squircle.Stroke(drawList, rect.Min - new Vector2(ringPad, ringPad), rect.Max + new Vector2(ringPad, ringPad),
            radius + ringPad, ImGui.GetColorU32(Fade(ring, alpha)),
            (selected ? Metrics.Stroke.Ring : Metrics.Stroke.Hairline) * scale);
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

    private static void DrawAppearanceCaption(ImDrawListPtr drawList, float centerX, float top, string label,
        bool selected, float alpha)
    {
        var scale = UiScale.Current;
        var labelHeight = LineBlock(TextStyles.Subheadline);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + labelHeight * 0.5f), label,
            Fade(ink.Strong, alpha), TextStyles.Subheadline);
        var markRadius = 11f * scale;
        var markCenter = new Vector2(centerX, top + labelHeight + Metrics.Space.Sm * scale + markRadius);
        if (!selected)
        {
            drawList.AddCircle(markCenter, markRadius, ImGui.GetColorU32(Fade(ink.Muted, 0.6f * alpha)), 32,
                Metrics.Stroke.Thin * scale);
            return;
        }

        drawList.AddCircleFilled(markCenter, markRadius * 1.7f, ImGui.GetColorU32(Fade(BrandMark.Violet, 0.18f * alpha)),
            32);
        drawList.AddCircleFilled(markCenter, markRadius, ImGui.GetColorU32(Fade(BrandMark.Violet, alpha)), 32);
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
            DrawAccountRisingStones(screen, offset, alpha, live);
            return;
        }

        if (flow.XivAuthActive)
        {
            DrawAccountXivAuth(screen, offset, alpha, live);
            return;
        }

        if (flow.LodestoneActive)
        {
            DrawAccountLodestone(screen, offset, alpha, live);
            return;
        }

        DrawAccountLanding(screen, offset, alpha, live);
    }

    private void DrawAccountLanding(Rect screen, Vector2 offset, float alpha, bool live)
    {
        if (gameData.IsChineseGameClient())
        {
            DrawAccountRisingStonesLanding(screen, offset, alpha, live);
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
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.UserCircle, ink.Accent,
            Loc.T(L.Setup.AccountTitle), body, top);
        var contentAlpha = alpha * Reveal(3);
        if (hasPlayer)
        {
            DrawIdentityCard(drawList, screen, offset, y + Metrics.Space.Xl * scale, name, world, contentAlpha);
        }
        else
        {
            Typography.DrawWrappedCentered(drawList, logInFirst, TextStyles.Subheadline,
                Fade(ink.Muted, contentAlpha),
                new Vector2(screen.Center.X + offset.X, y + Metrics.Space.Xl * scale), BodyWidth(screen));
        }

        var buttonsAlpha = alpha * Reveal(4);
        var ready = live && !flow.Busy && name.Length > 0 && world.Length > 0;
        if (Primary(drawList, ButtonRect(screen, offset, 2), Loc.T(L.Account.XivSignIn), buttonsAlpha, live, ready))
        {
            flow.StartXivAuth(name, world);
        }

        if (Secondary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.SignIn), buttonsAlpha, live) && ready)
        {
            flow.StartLodestone(name, world);
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SetUpLater), buttonsAlpha, live))
        {
            flow.Reset();
            AdvancePage();
        }

        DrawStatusLine(drawList, screen, offset, alpha, 3);
    }

    private void DrawAccountRisingStonesLanding(Rect screen, Vector2 offset, float alpha, bool live)
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
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.UserCircle, ink.Accent,
            Loc.T(L.Setup.AccountTitle), body, top);
        var contentAlpha = alpha * Reveal(3);
        var ready = false;
        float warningTop;
        if (hasPlayer)
        {
            var fieldRect = CardRect(screen, offset, y + Metrics.Space.Xl * scale + labelBlock, fieldHeight);
            DrawField(drawList, fieldRect, "setupRisingStonesUuid", Loc.T(L.Account.RisingStonesUuidLabel),
                ref risingStonesUuidDraft, SignInFlow.RisingStonesUuidMaxLength, contentAlpha, live);
            risingStonesUuidDraft = SanitizeDigits(risingStonesUuidDraft);
            var hintTop = fieldRect.Max.Y + Metrics.Space.Md * scale;
            Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, Fade(ink.Muted, contentAlpha),
                new Vector2(screen.Center.X + offset.X, hintTop), bodyWidth);
            warningTop = hintTop + WrappedHeight(hint, TextStyles.Footnote, bodyWidth) + Metrics.Space.Md * scale;
            ready = live && !flow.Busy && risingStonesUuidDraft.Length > 0;
        }
        else
        {
            var noticeTop = y + Metrics.Space.Xl * scale;
            Typography.DrawWrappedCentered(drawList, logInFirst, TextStyles.Subheadline, Fade(ink.Muted, contentAlpha),
                new Vector2(screen.Center.X + offset.X, noticeTop), bodyWidth);
            warningTop = noticeTop + WrappedHeight(logInFirst, TextStyles.Subheadline, bodyWidth) +
                         Metrics.Space.Md * scale;
        }

        Typography.DrawWrappedCentered(drawList, warning, TextStyles.Footnote, Fade(ink.Danger, contentAlpha),
            new Vector2(screen.Center.X + offset.X, warningTop), bodyWidth);

        var buttonsAlpha = alpha * Reveal(4);
        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.RisingStonesSignIn), buttonsAlpha,
                live, ready))
        {
            flow.StartRisingStones(risingStonesUuidDraft);
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SetUpLater), buttonsAlpha, live))
        {
            flow.Reset();
            AdvancePage();
        }

        DrawStatusLine(drawList, screen, offset, alpha, 2);
    }

    private void DrawAccountRisingStones(Rect screen, Vector2 offset, float alpha, bool live)
    {
        DrawVerification(screen, offset, alpha, live, L.Account.RisingStonesVerifyTitle,
            L.Account.RisingStonesVerifyIntro, L.Account.RisingStonesStep2, L.Account.RisingStonesStep3,
            L.Account.RisingStonesOpen, RisingStonesProfileSettingsUrl);
    }

    private void DrawAccountLodestone(Rect screen, Vector2 offset, float alpha, bool live)
    {
        DrawVerification(screen, offset, alpha, live, L.Account.VerifyTitle, L.Account.VerifyIntro,
            L.Account.Step2, L.Account.Step3, L.Account.OpenProfile, LodestoneProfileUrl);
    }

    private void DrawVerification(Rect screen, Vector2 offset, float alpha, bool live, LocString titleKey,
        LocString bodyKey, LocString secondStepKey, LocString thirdStepKey, LocString openKey, string openUrl)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(bodyKey);
        var stepsWidth = ContentWidth(screen);
        var step1 = Loc.T(L.Account.Step1);
        var step2 = Loc.T(secondStepKey);
        var step3 = Loc.T(thirdStepKey);
        var step4 = Loc.T(L.Account.Step4);
        var stepsHeight = StepLineHeight(step1, stepsWidth, scale) + StepLineHeight(step2, stepsWidth, scale) +
                          StepLineHeight(step3, stepsWidth, scale) + StepLineHeight(step4, stepsWidth, scale);
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Lg * scale + 54f * scale +
                            Metrics.Space.Lg * scale + stepsHeight;
        var top = CenteredTop(screen, contentHeight, 3) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.Key, ink.Accent,
            Loc.T(titleKey), body, top);
        var contentAlpha = alpha * Reveal(3);
        var codeRect = CardRect(screen, offset, y + Metrics.Space.Lg * scale, 54f * scale);
        if (DrawCodeCard(drawList, codeRect, flow.ChallengeCode, contentAlpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        y = codeRect.Max.Y + Metrics.Space.Lg * scale;
        var stepsLeft = codeRect.Min.X;
        y = DrawStepLine(drawList, "1", step1, stepsLeft, y, stepsWidth, alpha * Reveal(4), scale);
        y = DrawStepLine(drawList, "2", step2, stepsLeft, y, stepsWidth, alpha * Reveal(5), scale);
        y = DrawStepLine(drawList, "3", step3, stepsLeft, y, stepsWidth, alpha * Reveal(6), scale);
        DrawStepLine(drawList, "4", step4, stepsLeft, y, stepsWidth, alpha * Reveal(7), scale);
        var buttonsAlpha = alpha * Reveal(8);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 2);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.CopyCode), buttonsAlpha, live))
        {
            ImGui.SetClipboardText(flow.ChallengeCode);
        }

        if (Secondary(drawList, rightRect, Loc.T(openKey), buttonsAlpha, live))
        {
            UrlActions.OpenInBrowser(openUrl);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 1), Loc.T(L.Account.VerifyAdded), buttonsAlpha, live,
                !flow.Busy))
        {
            flow.VerifyChallenge();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Common.Cancel), buttonsAlpha, live))
        {
            flow.Reset();
        }

        DrawStatusLine(drawList, screen, offset, alpha, 3);
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
        var avatarRadius = 40f * scale;
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Xl * scale + avatarRadius * 2f;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        DrawSuccessBurst(drawList, new Vector2(screen.Center.X + offset.X, top + HeaderGlyphUnits * scale * 0.5f),
            Timeline(BurstDelaySeconds, BurstSeconds), alpha, scale);
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.CheckCircle, SignedInTint,
            Loc.T(L.Setup.SignedInTitle), body, top);
        if (user is not null)
        {
            var avatarReveal = Reveal(3);
            var avatarCenter = new Vector2(screen.Center.X + offset.X,
                y + Metrics.Space.Xl * scale + avatarRadius);
            BrandMark.Glow(drawList, avatarCenter, avatarRadius * 1.6f, alpha * avatarReveal);
            AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius * (0.8f + 0.2f * avatarReveal), theme,
                user.Name, user.World, user.AvatarUrl, images, lodestone, 1.6f, 48, alpha * avatarReveal,
                Frames.Of(user.FrameId));
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue), alpha * Reveal(4), live))
        {
            AdvancePage();
        }
    }

    private static void DrawSuccessBurst(ImDrawListPtr drawList, Vector2 center, float progress, float alpha,
        float scale)
    {
        if (progress <= 0f || progress >= 1f || alpha <= 0.001f)
        {
            return;
        }

        var fade = (1f - progress) * (1f - progress) * alpha;
        var reach = Spring.Settle(progress, 0.3f);
        var ringRadius = (26f + 46f * reach) * scale;
        drawList.AddCircle(center, ringRadius, ImGui.GetColorU32(Fade(SignedInTint, 0.6f * fade)), 64,
            2f * scale);
        for (var sparkIndex = 0; sparkIndex < BurstSparks; sparkIndex++)
        {
            var angle = sparkIndex * (MathF.PI * 2f / BurstSparks) - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var distance = (30f + 52f * reach) * scale;
            var spark = center + direction * distance;
            var sparkInk = sparkIndex % 2 == 0 ? SignedInTint : ink.Accent;
            drawList.AddCircleFilled(spark, 4.5f * scale * fade, ImGui.GetColorU32(Fade(sparkInk, 0.18f * fade)), 12);
            drawList.AddCircleFilled(spark, 2f * scale, ImGui.GetColorU32(Fade(sparkInk, fade)), 10);
        }
    }

    private void DrawAccountXivAuth(Rect screen, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var body = Loc.T(L.Account.XivIntro);
        var contentHeight = HeaderHeight(screen, body) + Metrics.Space.Lg * scale + 54f * scale + 76f * scale;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var y = DrawHeader(drawList, screen, offset, alpha, FontAwesomeIcon.ShieldAlt, ink.Accent,
            Loc.T(L.Account.XivTitle), body, top);
        var contentAlpha = alpha * Reveal(3);
        if (flow.XivUserCode.Length > 0)
        {
            var codeRect = CardRect(screen, offset, y + Metrics.Space.Lg * scale, 54f * scale);
            if (DrawCodeCard(drawList, codeRect, flow.XivUserCode, contentAlpha, live))
            {
                ImGui.SetClipboardText(flow.XivUserCode);
            }

            y = codeRect.Max.Y;
        }

        var waitCenter = new Vector2(screen.Center.X + offset.X, y + Metrics.Space.Xxl * scale);
        LoadingPulse.Spinner(waitCenter, 9f * scale, ink.Accent, contentAlpha, drawList);
        Typography.DrawCentered(drawList, waitCenter + new Vector2(0f, Metrics.Space.Xl * scale),
            Loc.T(L.Account.XivWaiting), Fade(ink.Muted, contentAlpha), TextStyles.Footnote);
        var buttonsAlpha = alpha * Reveal(4);
        var (leftRect, rightRect) = HalfButtonRects(screen, offset, 0);
        if (Secondary(drawList, leftRect, Loc.T(L.Account.XivOpen), buttonsAlpha, live) &&
            flow.XivVerificationUri is { } verificationUri)
        {
            UrlActions.OpenInBrowser(verificationUri);
        }

        if (Secondary(drawList, rightRect, Loc.T(L.Common.Cancel), buttonsAlpha, live))
        {
            flow.CancelXivAuth();
        }
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
        var avatarReveal = Reveal(0);
        var avatarCenter = new Vector2(centerX, top + avatarRadius);
        var avatarHovered = live && !avatarBusy && UiInteract.Hover(avatarCenter - new Vector2(avatarRadius),
            avatarCenter + new Vector2(avatarRadius));
        BrandMark.Glow(drawList, avatarCenter, avatarRadius * (avatarHovered ? 1.9f : 1.6f), alpha * avatarReveal);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius * (0.8f + 0.2f * avatarReveal), theme,
            user?.Name ?? gameData.LocalPlayer?.Name.TextValue ?? string.Empty,
            user?.World ?? gameData.WorldName(gameData.LocalHomeWorldId), user?.AvatarUrl, images, lodestone, 2f, 64,
            alpha * avatarReveal, Frames.Of(user?.FrameId));
        DrawPhotoBadge(drawList, avatarCenter, avatarRadius, alpha * avatarReveal);
        if (live && !avatarBusy && UiInteract.HoverClickCircle(avatarCenter, avatarRadius))
        {
            picker.Open();
            pickingPhoto = true;
        }

        var titleReveal = Reveal(1);
        var titleCenter = new Vector2(centerX,
            top + avatarRadius * 2f + Metrics.Space.Xl * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter + new Vector2(0f, Rise(titleReveal)), Loc.T(L.Setup.IdTitle),
            Fade(ink.Strong, alpha * titleReveal), TextStyles.Title1);
        var bodyReveal = Reveal(2);
        var bodyBottom = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(ink.Muted, alpha * bodyReveal),
            new Vector2(centerX, titleCenter.Y + titleHeight * 0.5f + Metrics.Space.Md * scale + Rise(bodyReveal)),
            bodyWidth) - Rise(bodyReveal);
        var fieldsAlpha = alpha * Reveal(3);
        var nameRect = CardRect(screen, offset, bodyBottom + Metrics.Space.Xl * scale + labelBlock, fieldHeight);
        DrawField(drawList, nameRect, "setupDisplayName", Loc.T(L.Setup.DisplayNameLabel), ref displayNameDraft,
            DisplayNameMax, fieldsAlpha, live);
        var handleRect = CardRect(screen, offset, nameRect.Max.Y + Metrics.Space.Lg * scale + labelBlock,
            fieldHeight);
        DrawField(drawList, handleRect, "setupHandle", Loc.T(L.Setup.HandleLabel), ref handleDraft, HandleMax,
            fieldsAlpha, live, "@");
        handleDraft = SanitizeHandle(handleDraft);
        var hintInk = handleRejected ? Fade(ink.Danger, fieldsAlpha) : Fade(ink.Muted, fieldsAlpha);
        Typography.DrawWrappedCentered(drawList, hint, TextStyles.Footnote, hintInk,
            new Vector2(centerX, handleRect.Max.Y + Metrics.Space.Md * scale), bodyWidth);
        var valid = !profileSaving && IsHandleValid(handleDraft) && displayNameDraft.Trim().Length > 0;
        var saveLabel = profileSaving ? Loc.T(L.Account.Saving) : Loc.T(L.Onboarding.Continue);
        var buttonsAlpha = alpha * Reveal(4);
        if (Primary(drawList, ButtonRect(screen, offset, 1), saveLabel, buttonsAlpha, live, valid))
        {
            SaveProfile();
        }

        if (TextAction(drawList, TextActionCenter(screen, offset, 0), Loc.T(L.Setup.SkipForNow), buttonsAlpha, live))
        {
            AdvancePage();
        }
    }

    private static void DrawPhotoBadge(ImDrawListPtr drawList, Vector2 avatarCenter, float avatarRadius,
        float alpha)
    {
        var scale = UiScale.Current;
        var badgeRadius = 15f * scale;
        var reach = avatarRadius * 0.72f;
        var badgeCenter = new Vector2(avatarCenter.X + reach, avatarCenter.Y + reach);
        drawList.AddCircleFilled(badgeCenter, badgeRadius + 2.5f * scale,
            ImGui.GetColorU32(Fade(ink.Base, alpha)), 32);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Fade(BrandMark.Violet, alpha)), 32);
        AppSkin.Icon(drawList, badgeCenter, IconGlyph.Of(FontAwesomeIcon.Camera), new Vector4(1f, 1f, 1f, alpha),
            0.6f);
    }

    private void DrawPhotoPicker(Rect screen, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, theme.ScreenRounding * scale,
            ImGui.GetColorU32(theme.AppBackground));
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
        var rowWidth = ContentWidth(screen);
        var rowsHeight = 0f;
        for (var index = 0; index < Features.Length; index++)
        {
            rowsHeight += FeatureRowHeight(Features[index], rowWidth, scale);
        }

        var contentHeight = titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth) + Metrics.Space.Xxl * scale +
                            rowsHeight;
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var titleReveal = Reveal(0);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + titleHeight * 0.5f + Rise(titleReveal)),
            Loc.T(L.Onboarding.AllInOneTitle), Fade(ink.Strong, alpha * titleReveal), TextStyles.LargeTitle);
        var bodyReveal = Reveal(1);
        var y = Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(ink.Muted, alpha * bodyReveal),
            new Vector2(centerX, top + titleHeight + Metrics.Space.Md * scale + Rise(bodyReveal)), bodyWidth) -
            Rise(bodyReveal);
        y += Metrics.Space.Xxl * scale;
        var rowLeft = centerX - rowWidth * 0.5f;
        for (var index = 0; index < Features.Length; index++)
        {
            var rowReveal = Reveal(3 + index);
            DrawFeatureRow(drawList, Features[index], rowLeft, y + Rise(rowReveal), rowWidth, alpha * rowReveal,
                rowReveal, scale);
            y += FeatureRowHeight(Features[index], rowWidth, scale);
        }

        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Onboarding.Continue),
                alpha * Reveal(3 + Features.Length), live))
        {
            AdvancePage();
        }
    }

    private static float FeatureTextWidth(float rowWidth, float scale) =>
        rowWidth - FeatureIconUnits * scale - Metrics.Space.Lg * scale;

    private static float FeatureRowHeight(in FeatureRow row, float rowWidth, float scale)
    {
        var textWidth = FeatureTextWidth(rowWidth, scale);
        var lines = Typography.WrapText(Loc.T(row.Body), TextStyles.Footnote, textWidth).Length;
        var textHeight = LineBlock(TextStyles.Headline) + Metrics.Space.Xxs * scale +
                         lines * LineBlock(TextStyles.Footnote) * 1.2f;
        return MathF.Max(FeatureIconUnits * scale, textHeight) + Metrics.Space.Xl * scale;
    }

    private static void DrawFeatureRow(ImDrawListPtr drawList, in FeatureRow row, float left, float top, float width,
        float alpha, float reveal, float scale)
    {
        var tile = FeatureIconUnits * scale;
        var popped = tile * (0.7f + 0.3f * reveal);
        var tileCenter = new Vector2(left + tile * 0.5f, top + tile * 0.5f);
        var half = new Vector2(popped * 0.5f, popped * 0.5f);
        var tileMin = tileCenter - half;
        var tileMax = tileCenter + half;
        var radius = popped * BrandMark.CornerFraction;
        if (!AppIconTile.TryDraw(drawList, row.AppId, AppAccents.For(row.AppId), tileMin, tileMax, radius, alpha,
                true, scale))
        {
            Material.LiquidGlass(drawList, tileMin, tileMax, radius, scale, glass, 0f, alpha);
        }

        var textLeft = left + tile + Metrics.Space.Lg * scale;
        var textWidth = FeatureTextWidth(width, scale);
        var headlineHeight = LineBlock(TextStyles.Headline);
        var lines = Typography.WrapText(Loc.T(row.Body), TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.2f;
        var textHeight = headlineHeight + Metrics.Space.Xxs * scale + lines.Length * lineHeight;
        var textTop = top + MathF.Max(0f, (tile - textHeight) * 0.5f);
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(Loc.T(row.Title), textWidth, TextStyles.Headline), Fade(ink.Strong, alpha),
            TextStyles.Headline);
        var bodyTop = textTop + headlineHeight + Metrics.Space.Xxs * scale;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, bodyTop + lineIndex * lineHeight), lines[lineIndex],
                Fade(ink.Muted, alpha), TextStyles.Footnote);
        }
    }

    private void DrawReady(Rect screen, PhoneTheme theme, Vector2 offset, float alpha, bool live)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var title = Loc.T(L.Onboarding.WelcomeTitle);
        var body = Loc.T(L.Setup.ReadyBody);
        var bodyWidth = BodyWidth(screen);
        var heroHeight = ReadyHeroUnits * scale;
        var titleHeight = LineBlock(TextStyles.Title1);
        var contentHeight = heroHeight + Metrics.Space.Xl * scale + titleHeight + Metrics.Space.Md * scale +
                            WrappedHeight(body, TextStyles.Subheadline, bodyWidth);
        var top = CenteredTop(screen, contentHeight, 1) + offset.Y;
        var centerX = screen.Center.X + offset.X;
        var heroReveal = Reveal(0);
        var zoom = exiting ? Spring.Settle(exitClock, Motion.Sheet) : 0f;
        var heroCenter = new Vector2(centerX, top + heroHeight * 0.5f + Float(scale) * heroReveal);
        var heroScale = scale * ReadyHeroScale * (1f + ExitZoom * zoom);
        var heroAlpha = exiting ? 1f - exitProgress : alpha;
        BrandMark.Shockwave(drawList, heroCenter, 36f * heroScale,
            exiting ? exitProgress : Timeline(ShockwaveDelaySeconds, ShockwaveSeconds), heroAlpha, scale);
        OnboardingHero.Draw(drawList, heroCenter, HeroMotif.Constellation, BrandMark.Violet, heroScale, heroReveal,
            heroAlpha);
        var titleReveal = Reveal(3);
        var titleCenter = new Vector2(centerX,
            top + heroHeight + Metrics.Space.Xl * scale + titleHeight * 0.5f + Rise(titleReveal));
        Typography.DrawCentered(drawList, titleCenter,
            Typography.FitText(title, ContentWidth(screen), TextStyles.Title1), Fade(ink.Strong, alpha * titleReveal),
            TextStyles.Title1);
        var bodyReveal = Reveal(4);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, Fade(ink.Muted, alpha * bodyReveal),
            new Vector2(centerX, titleCenter.Y - Rise(titleReveal) + titleHeight * 0.5f + Metrics.Space.Md * scale +
                                 Rise(bodyReveal)), bodyWidth);
        if (Primary(drawList, ButtonRect(screen, offset, 0), Loc.T(L.Setup.StartUsing), alpha * Reveal(6), live))
        {
            Complete();
        }
    }

    private static float HeaderHeight(Rect screen, string body)
    {
        var scale = UiScale.Current;
        return HeaderGlyphUnits * scale + Metrics.Space.Xl * scale + LineBlock(TextStyles.Title1) +
               Metrics.Space.Md * scale + WrappedHeight(body, TextStyles.Subheadline, BodyWidth(screen));
    }

    private float DrawHeader(ImDrawListPtr drawList, Rect screen, Vector2 offset, float alpha, FontAwesomeIcon icon,
        Vector4 tint, string title, string body, float top)
    {
        var scale = UiScale.Current;
        var centerX = screen.Center.X + offset.X;
        var glyphHeight = HeaderGlyphUnits * scale;
        DrawGlyphBadge(drawList, new Vector2(centerX, top + glyphHeight * 0.5f), icon, tint, alpha, Reveal(0));
        var titleHeight = LineBlock(TextStyles.Title1);
        var titleReveal = Reveal(1);
        var titleCenter = new Vector2(centerX, top + glyphHeight + Metrics.Space.Xl * scale + titleHeight * 0.5f);
        Typography.DrawCentered(drawList, titleCenter + new Vector2(0f, Rise(titleReveal)),
            Typography.FitText(title, ContentWidth(screen), TextStyles.Title1), Fade(ink.Strong, alpha * titleReveal),
            TextStyles.Title1);
        var bodyReveal = Reveal(2);
        var bodyTop = titleCenter.Y + titleHeight * 0.5f + Metrics.Space.Md * scale;
        return Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline,
            Fade(ink.Muted, alpha * bodyReveal), new Vector2(centerX, bodyTop + Rise(bodyReveal)),
            BodyWidth(screen)) - Rise(bodyReveal);
    }

    private static void DrawGlyphBadge(ImDrawListPtr drawList, Vector2 center, FontAwesomeIcon icon, Vector4 tint,
        float alpha, float reveal)
    {
        var scale = UiScale.Current;
        var strength = alpha * reveal;
        if (strength <= 0.001f)
        {
            return;
        }

        var breath = 0.85f + 0.15f * Pulse.Wave(Pulse.Breath);
        drawList.AddCircleFilled(center, 54f * scale, ImGui.GetColorU32(Fade(tint, 0.035f * strength * breath)), 64);
        drawList.AddCircleFilled(center, 40f * scale, ImGui.GetColorU32(Fade(tint, 0.06f * strength * breath)), 64);
        drawList.AddCircleFilled(center, 28f * scale, ImGui.GetColorU32(Fade(tint, 0.10f * strength)), 48);
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), Fade(Palette.Mix(tint, Vector4.One, 0.25f), strength),
            2.4f * (0.75f + 0.25f * reveal));
    }

    private void DrawStatusLine(ImDrawListPtr drawList, Rect screen, Vector2 offset, float alpha, int buttonSlots)
    {
        var message = flow.Status;
        if (message.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var center = new Vector2(screen.Center.X + offset.X,
            ButtonRect(screen, offset, buttonSlots - 1).Min.Y - Metrics.Space.Lg * scale);
        Typography.DrawCentered(drawList, center, message, Fade(ink.Muted, alpha), TextStyles.Footnote);
    }

    private void DrawBackButton(ImDrawListPtr drawList, Rect screen, float alpha, bool live)
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
        var size = BackButtonUnits * scale;
        var inset = Metrics.Space.Lg * scale;
        var min = new Vector2(screen.Min.X + inset, screen.Min.Y + inset);
        var max = new Vector2(min.X + size, min.Y + size);
        var hovered = live && UiInteract.Hover(min, max);
        var pose = Animate(new Rect(min, max), "back", hovered, hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        var face = pose.Face;
        var radius = face.Width * 0.5f;
        DrawHalo(drawList, face, radius, new Vector4(0f, 0f, 0f, 1f), 0.18f * pose.Hover * alpha);
        Material.LiquidGlass(drawList, face.Min, face.Max, radius, scale, glass, 0f, alpha);
        DrawRimLight(drawList, face, radius, pose.Hover * alpha);
        var nudge = new Vector2((1.5f + 1.5f * pose.Hover) * scale, 0f);
        _ = BackButton.Draw("setup.back", face.Center - nudge, 13f * scale, Fade(ink.Strong, alpha), hovered, scale);
        if (live && UiInteract.Click(min, max, hovered))
        {
            BackPage();
        }
    }

    private static void DrawCard(ImDrawListPtr drawList, Rect rect, float alpha)
    {
        var scale = UiScale.Current;
        Material.LiquidGlass(drawList, rect.Min, rect.Max, Metrics.Radius.Card * scale, scale, glass, 0f,
            alpha);
    }

    private static bool DrawSelectRow(ImDrawListPtr drawList, Rect row, string label, bool selected, float alpha,
        bool live, bool first, bool last)
    {
        var scale = UiScale.Current;
        var radius = Metrics.Radius.Card * scale;
        var padding = Metrics.Space.Lg * scale;
        var hovered = live && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            var tint = ImGui.GetColorU32(Fade(ink.Wash, alpha));
            if (first && last)
            {
                Squircle.Fill(drawList, row.Min, row.Max, radius, tint);
            }
            else if (first)
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
                ImGui.GetColorU32(Fade(ink.Hairline, alpha)), Metrics.Stroke.Hairline);
        }

        var checkColumn = 30f * scale;
        var labelStyle = selected ? TextStyles.BodyEmphasized : TextStyles.Body;
        var labelSize = Typography.Measure(label, labelStyle);
        Typography.Draw(drawList, new Vector2(row.Min.X + padding, row.Center.Y - labelSize.Y * 0.5f),
            Typography.FitText(label, row.Width - padding * 2f - checkColumn, labelStyle),
            Fade(ink.Strong, alpha), labelStyle);
        if (selected)
        {
            AppSkin.Icon(drawList, new Vector2(row.Max.X - padding - 5f * scale, row.Center.Y),
                IconGlyph.Of(FontAwesomeIcon.Check), Fade(ink.Accent, alpha), 0.95f);
        }

        return live && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawIdentityCard(ImDrawListPtr drawList, Rect screen, Vector2 offset, float top,
        string name, string world, float alpha)
    {
        var scale = UiScale.Current;
        var rect = CardRect(screen, offset, top, 62f * scale);
        DrawCard(drawList, rect, alpha);
        var left = rect.Min.X + Metrics.Space.Lg * scale;
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + Metrics.Space.Sm * scale),
            Loc.T(L.Account.SigningInAs), Fade(ink.Muted, alpha), TextStyles.Footnote);
        var identityMaxWidth = rect.Max.X - Metrics.Space.Lg * scale - left;
        Typography.Draw(drawList, new Vector2(left, rect.Min.Y + 30f * scale),
            Typography.FitText($"{name}@{world}", identityMaxWidth, TextStyles.Headline),
            Fade(ink.Strong, alpha), TextStyles.Headline);
    }

    private static bool DrawCodeCard(ImDrawListPtr drawList, Rect rect, string value, float alpha, bool live)
    {
        var scale = UiScale.Current;
        var hovered = live && UiInteract.Hover(rect.Min, rect.Max);
        var radius = Metrics.Radius.Card * scale;
        DrawCard(drawList, rect, alpha);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Fade(ink.Wash, alpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Fade(ink.Accent, 0.55f * alpha)), Metrics.Stroke.Thin * scale);
        Typography.DrawCentered(drawList, rect.Center, value, Fade(ink.Accent, alpha), TextStyles.Title3);
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
        float width, float alpha, float scale)
    {
        var badgeRadius = 10f * scale;
        var badgeCenter = new Vector2(left + badgeRadius, top + badgeRadius);
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(Fade(BrandMark.Violet, alpha)), 24);
        Typography.DrawCentered(drawList, badgeCenter, number, new Vector4(1f, 1f, 1f, alpha), TextStyles.Caption2);
        var textLeft = left + badgeRadius * 2f + Metrics.Space.Md * scale;
        var textWidth = width - badgeRadius * 2f - Metrics.Space.Md * scale;
        var lines = Typography.WrapText(text, TextStyles.Footnote, textWidth);
        var lineHeight = LineBlock(TextStyles.Footnote) * 1.25f;
        var textTop = top + badgeRadius - lineHeight * 0.5f + 2f * scale;
        var stepInk = Fade(ink.Strong, 0.88f * alpha);
        for (var index = 0; index < lines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(textLeft, textTop + index * lineHeight), lines[index], stepInk,
                TextStyles.Footnote);
        }

        var bottom = MathF.Max(badgeRadius * 2f, lines.Length * lineHeight);
        return top + bottom + Metrics.Space.Sm * scale;
    }

    private static void DrawField(ImDrawListPtr drawList, Rect rect, string id, string label, ref string value,
        int maxLength, float alpha, bool live, string? prefix = null)
    {
        var scale = UiScale.Current;
        var labelMaxWidth = rect.Width - 2f * scale;
        var labelHeight = LineBlock(TextStyles.Footnote);
        Typography.Draw(drawList,
            new Vector2(rect.Min.X + Metrics.Space.Xs * scale,
                rect.Min.Y - labelHeight - Metrics.Space.Xs * scale),
            Typography.FitText(label, labelMaxWidth, TextStyles.Footnote), Fade(ink.Muted, alpha),
            TextStyles.Footnote);
        DrawCard(drawList, rect, alpha);
        var textLeft = rect.Min.X + Metrics.Space.Md * scale;
        if (prefix is not null)
        {
            var prefixSize = Typography.Measure(prefix, TextStyles.Body);
            Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - prefixSize.Y * 0.5f), prefix,
                Fade(ink.Muted, alpha), TextStyles.Body);
            textLeft += prefixSize.X + Metrics.Space.Xxs * scale;
        }

        if (!live)
        {
            if (value.Length > 0)
            {
                var valueSize = Typography.Measure(value, TextStyles.Body);
                var valueMaxWidth = rect.Max.X - Metrics.Space.Md * scale - textLeft;
                Typography.Draw(drawList, new Vector2(textLeft, rect.Center.Y - valueSize.Y * 0.5f),
                    Typography.FitText(value, valueMaxWidth, TextStyles.Body), Fade(ink.Strong, alpha),
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
                   .Push(ImGuiCol.TextSelectedBg, Fade(BrandMark.Violet, 0.45f))
                   .Push(ImGuiCol.Text, ink.Strong))
        {
            ImGui.InputText($"##{id}", ref value, maxLength);
        }

        if (ImGui.IsItemActive())
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(Fade(ink.Accent, 0.5f * alpha)), Metrics.Stroke.Thin * scale);
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

    private static bool TextAction(ImDrawListPtr drawList, Vector2 center, string label, float alpha, bool live)
    {
        if (alpha <= 0.001f)
        {
            return false;
        }

        var scale = UiScale.Current;
        var size = Typography.Measure(label, TextStyles.SubheadlineEmphasized);
        var padding = new Vector2(Metrics.Space.Md * scale, Metrics.Space.Sm * scale);
        var min = center - size * 0.5f - padding;
        var max = center + size * 0.5f + padding;
        var hovered = live && UiInteract.Hover(min, max);
        var actionInk = hovered ? ink.Strong : ink.Accent;
        Typography.DrawCentered(drawList, center, label, Fade(actionInk, alpha), TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return live && UiInteract.Click(min, max, hovered);
    }
}
