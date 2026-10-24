using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float WelcomeHeroGlyph = 64f;
    private const float WelcomeHeroGap = 16f;
    private const float FeatureGlyph = 36f;
    private const float FeatureGap = 14f;
    private const float FeatureRowGap = 18f;
    private const float StartPillHeight = Button.LargeHeight;

    private static readonly LocString[] FeatureTitles =
    {
        L.Health.FeatureWaterTitle,
        L.Health.FeatureWeightTitle,
        L.Health.FeatureStepsTitle,
    };

    private static readonly LocString[] FeatureBodies =
    {
        L.Health.FeatureWaterBody,
        L.Health.FeatureWeightBody,
        L.Health.FeatureStepsBody,
    };

    private static readonly FontAwesomeIcon[] FeatureIcons =
    {
        FontAwesomeIcon.Tint,
        FontAwesomeIcon.Weight,
        FontAwesomeIcon.ShoePrints,
    };

    private void DrawWelcome(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("health.welcome"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawWelcomeHero(drawList, origin, width, scale);
            cursorY = DrawFeatures(drawList, origin.X, cursorY + HealthArt.TileGap * scale, width, scale);
            cursorY = DrawUnitsCard(drawList, new Vector2(origin.X, cursorY + HealthArt.SectionGap * scale), width,
                scale);
            cursorY += HealthArt.TileGap * scale;
            var pill = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + StartPillHeight * scale));
            if (Button.Draw(pill, Loc.T(L.Health.GetStarted), ui.Ink))
            {
                FinishSetup();
            }

            cursorY = pill.Max.Y + Footnote(new Vector2(origin.X, pill.Max.Y), width, Loc.T(L.Health.Disclaimer),
                scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "health.nav.welcome", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawWelcomeHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = HealthArt.CardPad * scale;
        var glyph = WelcomeHeroGlyph * scale;
        var textWidth = MathF.Max(1f, width - pad * 2f);
        var title = Loc.T(L.Health.WelcomeTitle);
        var body = Loc.T(L.Health.WelcomeBody);
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Title2, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, textWidth).Y;
        var height = pad * 2f + glyph + WelcomeHeroGap * scale + titleHeight + HealthArt.LineGap * 2f * scale +
                     bodyHeight;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var centerX = origin.X + width * 0.5f;
        var top = origin.Y + pad;
        HealthArt.GlyphTile(drawList, new Vector2(centerX, top + glyph * 0.5f), glyph, ui.Accent,
            FontAwesomeIcon.Heartbeat);
        top += glyph + WelcomeHeroGap * scale;
        var titleBottom = Typography.DrawWrappedCentered(drawList, title, TextStyles.Title2, ui.TitleInk,
            new Vector2(centerX, top), textWidth);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(centerX, titleBottom + HealthArt.LineGap * 2f * scale), textWidth);
        return max.Y;
    }

    private float DrawFeatures(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var pad = HealthArt.CardPad * scale;
        var glyph = FeatureGlyph * scale;
        var textLeft = left + pad + glyph + FeatureGap * scale;
        var textWidth = MathF.Max(1f, left + width - pad - textLeft);
        var height = pad * 2f;
        for (var index = 0; index < FeatureTitles.Length; index++)
        {
            height += FeatureHeight(index, textWidth, glyph) + (index > 0 ? FeatureRowGap * scale : 0f);
        }

        var max = new Vector2(left + width, top + height);
        ui.Card(drawList, new Vector2(left, top), max, Metrics.Radius.Grouped * scale);
        var cursorY = top + pad;
        for (var index = 0; index < FeatureTitles.Length; index++)
        {
            if (index > 0)
            {
                cursorY += FeatureRowGap * scale;
            }

            var tint = index switch
            {
                0 => HealthArt.Tint(HealthMetric.Water),
                1 => HealthArt.WeightTint,
                _ => HealthArt.Tint(HealthMetric.Steps),
            };
            HealthArt.GlyphTile(drawList, new Vector2(left + pad + glyph * 0.5f, cursorY + glyph * 0.5f), glyph, tint,
                FeatureIcons[index]);
            var titleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY), Loc.T(FeatureTitles[index]),
                ui.TitleInk, TextStyles.Headline, textWidth);
            Typography.DrawWrappedLeft(new Vector2(textLeft, cursorY + titleHeight), Loc.T(FeatureBodies[index]),
                ui.MutedInk, TextStyles.Subheadline, textWidth);
            cursorY += FeatureHeight(index, textWidth, glyph);
        }

        return max.Y;
    }

    private static float FeatureHeight(int index, float textWidth, float glyph)
    {
        var titleHeight = Typography.MeasureWrappedBlock(Loc.T(FeatureTitles[index]), TextStyles.Headline, textWidth).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(Loc.T(FeatureBodies[index]), TextStyles.Subheadline, textWidth).Y;
        return MathF.Max(glyph, titleHeight + bodyHeight);
    }

    private void FinishSetup()
    {
        Profile.SetupCompleted = true;
        if (Profile.Goals.Count == 0)
        {
            Profile.Goals = HealthTracker.DefaultGoals(Profile.DailySwimGoalYalms);
        }

        tracker.SaveNow();
        digest.Invalidate();
        UiFeedback.Play(UiSound.Success);
    }
}
