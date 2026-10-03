using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Strats;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Strats;

internal sealed partial class StratsApp
{
    private const int SearchMaxLength = 40;
    private const float SearchGap = 16f;
    private const float SectionGap = 24f;
    private const float SectionTitleGap = 10f;
    private const float FeatureHeight = 96f;
    private const float FeatureCover = 56f;
    private const float FeaturePad = 16f;
    private const float FeatureTextGap = 14f;
    private const float FeatureLineGap = 2f;
    private const float FeatureBarHeight = 4f;
    private const float FeatureBarGap = 10f;
    private const float FeatureGlowCoverage = 0.7f;
    private const float FeatureGlowRest = 0.12f;
    private const float FeatureGlowHover = 0.18f;
    private const float BarTrackAlpha = 0.16f;
    private const float FightRowHeight = 62f;
    private const float FightCover = 40f;
    private const float FightTextGap = 12f;
    private const float FightChevronScale = 0.55f;
    private const float FightChevronWidth = 14f;
    private const float FightProgressMinimum = 0.02f;
    private const float FightProgressRadius = 7f;
    private const float FightProgressThickness = 2f;
    private const float FightProgressTrackAlpha = 0.18f;
    private const float NoMatchTile = 56f;
    private const float NoMatchGlyph = 1.3f;
    private const float CreditsRowHeight = 32f;
    private const float CoverTextShare = 0.84f;

    private static readonly string ChevronGlyph = IconGlyph.Of(FontAwesomeIcon.ChevronRight);
    private static readonly string SearchGlyph = IconGlyph.Of(FontAwesomeIcon.Search);

    private string indexQuery = string.Empty;
    private uint dutyTerritory;
    private int dutyManifestVersion = -1;
    private ManifestFight? dutyFight;
    private CultureInfo? creditsCulture;
    private string creditsSource = string.Empty;
    private string creditsText = string.Empty;

    private void DrawIndex(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        var manifest = manifestStore.Manifest;
        if (manifest is null)
        {
            DrawIndexState(navBar.Body);
        }
        else
        {
            DrawLibrary(navBar.Body, manifest);
        }

        AppHeader.EndLargeTitle(in navBar, context, "strats.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawLibrary(Rect body, StratsManifest manifest)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("strats.index"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawIndexSearch(drawList, origin, width, scale) + SearchGap * scale;
            var searching = indexQuery.AsSpan().Trim().Length > 0;
            if (!searching)
            {
                cursorY = DrawFeatures(drawList, new Vector2(origin.X, cursorY), width, manifest, scale);
            }

            var shown = 0;
            var anchorTaken = false;
            for (var groupIndex = 0; groupIndex < manifest.Groups.Length; groupIndex++)
            {
                var group = manifest.Groups[groupIndex];
                var matches = CountMatches(group);
                if (matches == 0)
                {
                    continue;
                }

                if (shown > 0)
                {
                    cursorY += SectionGap * scale;
                }

                shown += matches;
                cursorY = DrawGroup(drawList, new Vector2(origin.X, cursorY), width, group, matches, ref anchorTaken,
                    scale);
            }

            if (searching && shown == 0)
            {
                cursorY = DrawNoMatches(drawList, new Vector2(origin.X, cursorY), width, scale);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, cursorY + SectionGap * scale));
            DrawCreditsRow(manifest, width, scale);
            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }
    }

    private float DrawIndexSearch(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Search(drawList, field, "##stratsSearch", Loc.T(L.Strats.Search), ref indexQuery, theme, scale,
            SearchMaxLength, false);
        return field.Max.Y;
    }

    private int CountMatches(ManifestGroup group)
    {
        var count = 0;
        for (var index = 0; index < group.Fights.Length; index++)
        {
            if (StratsLibrary.Matches(group.Fights[index], indexQuery))
            {
                count++;
            }
        }

        return count;
    }

    private float DrawFeatures(ImDrawListPtr drawList, Vector2 origin, float width, StratsManifest manifest,
        float scale)
    {
        var cursorY = origin.Y;
        var duty = DutyFight(manifest);
        if (duty is not null)
        {
            snapshot.Fights.TryGetValue(duty.Key, out var dutySaved);
            var line = dutySaved is { ReadingLabel.Length: > 0 } ? dutySaved.ReadingLabel : duty.Subtitle;
            var rect = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + FeatureHeight * scale));
            UiAnchors.Report("strats.duty", rect);
            if (DrawFeatureCard(drawList, rect, "strats.feature.duty", Loc.T(L.Strats.InThisDuty), duty, line,
                    dutySaved?.ReadingProgress ?? 0f, scale))
            {
                OpenFight(duty);
            }

            cursorY = rect.Max.Y + SectionGap * scale;
        }

        if (!StratsLibrary.TryMostRecent(manifest, snapshot, out var recent, out var saved) ||
            ReferenceEquals(recent, duty))
        {
            return cursorY;
        }

        var recentLine = saved.ReadingLabel.Length > 0 ? saved.ReadingLabel : recent.Subtitle;
        var recentRect = new Rect(new Vector2(origin.X, cursorY),
            new Vector2(origin.X + width, cursorY + FeatureHeight * scale));
        UiAnchors.Report("strats.continue", recentRect);
        if (DrawFeatureCard(drawList, recentRect, "strats.feature.continue", Loc.T(L.Strats.ContinueReading), recent,
                recentLine, saved.ReadingProgress, scale))
        {
            OpenFight(recent);
        }

        return recentRect.Max.Y + SectionGap * scale;
    }

    private ManifestFight? DutyFight(StratsManifest manifest)
    {
        var territory = (uint)Plugin.ClientState.TerritoryType;
        if (territory == dutyTerritory && dutyManifestVersion == manifestStore.Version)
        {
            return dutyFight;
        }

        dutyTerritory = territory;
        dutyManifestVersion = manifestStore.Version;
        dutyFight = StratsLibrary.TryFindByTerritory(manifest, territory, out var found) ? found : null;
        return dutyFight;
    }

    private bool DrawFeatureCard(ImDrawListPtr drawList, Rect rect, string id, string caption, ManifestFight fight,
        string line, float progress, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(id, pressed, PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, min, max, radius, true);
        Material.TopGlow(drawList, min, max, radius, ui.Accent, FeatureGlowCoverage,
            hovered ? FeatureGlowHover : FeatureGlowRest);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = FeaturePad * scale;
        var cover = FeatureCover * scale;
        var coverMin = new Vector2(min.X + pad, (min.Y + max.Y - cover) * 0.5f);
        DrawCover(drawList, coverMin, cover, fight.Abbrev, scale);
        var chevronCenter = new Vector2(max.X - pad - FightChevronWidth * 0.5f * scale, (min.Y + max.Y) * 0.5f);
        AppSkin.Icon(drawList, chevronCenter, ChevronGlyph, ui.MutedInk, FightChevronScale);
        var textLeft = coverMin.X + cover + FeatureTextGap * scale;
        var textWidth = MathF.Max(1f, chevronCenter.X - FightChevronWidth * scale - textLeft);
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = line.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var showBar = progress > FightProgressMinimum;
        var barBlock = showBar ? FeatureBarGap * scale + FeatureBarHeight * scale : 0f;
        var blockHeight = captionHeight + FeatureLineGap * scale + titleHeight +
                          (lineHeight > 0f ? FeatureLineGap * scale + lineHeight : 0f) + barBlock;
        var top = (min.Y + max.Y - blockHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(caption, textWidth,
            TextStyles.FootnoteEmphasized), ui.Accent, TextStyles.FootnoteEmphasized);
        top += captionHeight + FeatureLineGap * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(fight.Title, textWidth,
            TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        top += titleHeight;
        if (lineHeight > 0f)
        {
            top += FeatureLineGap * scale;
            Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(line, textWidth,
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            top += lineHeight;
        }

        if (showBar)
        {
            top += FeatureBarGap * scale;
            DrawProgressBar(drawList, new Vector2(textLeft, top), textWidth, progress, scale);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawProgressBar(ImDrawListPtr drawList, Vector2 origin, float width, float progress, float scale)
    {
        var height = FeatureBarHeight * scale;
        var rounding = height * 0.5f;
        var max = new Vector2(origin.X + width, origin.Y + height);
        drawList.AddRectFilled(origin, max, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, BarTrackAlpha)),
            rounding);
        var fill = MathF.Max(height, width * Math.Clamp(progress, 0f, 1f));
        drawList.AddRectFilled(origin, new Vector2(origin.X + fill, max.Y), ImGui.GetColorU32(ui.Accent), rounding);
    }

    private void DrawCover(ImDrawListPtr drawList, Vector2 min, float size, string abbrev, float scale)
    {
        var max = min + new Vector2(size, size);
        IconTile.FillShaded(drawList, min, max, size * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        if (abbrev.Length == 0)
        {
            AppSkin.Icon(drawList, (min + max) * 0.5f, SearchGlyph, AccentRing.Ink, FightChevronScale);
            return;
        }

        var budget = size * CoverTextShare;
        var style = CoverStyle(abbrev, budget, size);
        Typography.DrawCentered(drawList, (min + max) * 0.5f, Typography.FitText(abbrev, budget, style),
            AccentRing.Ink, style);
    }

    private static TextStyle CoverStyle(string abbrev, float budget, float size)
    {
        var large = size >= FeatureCover * UiScale.Current;
        var first = large ? TextStyles.Title3 : TextStyles.Headline;
        if (Typography.Measure(abbrev, first).X <= budget)
        {
            return first;
        }

        return Typography.Measure(abbrev, TextStyles.SubheadlineEmphasized).X <= budget
            ? TextStyles.SubheadlineEmphasized
            : TextStyles.FootnoteEmphasized;
    }

    private float DrawGroup(ImDrawListPtr drawList, Vector2 origin, float width, ManifestGroup group, int matches,
        ref bool anchorTaken, float scale)
    {
        var titleHeight = Typography.DrawWrappedLeft(origin, group.Title, ui.TitleInk, TextStyles.Title3, width);
        var cardTop = origin.Y + titleHeight + SectionTitleGap * scale;
        ImGui.SetCursorScreenPos(new Vector2(origin.X, cardTop));
        var card = GroupCard.Begin(ui, matches, FightRowHeight);
        card.SeparatorInset = FightCover + FightTextGap;
        for (var index = 0; index < group.Fights.Length; index++)
        {
            var fight = group.Fights[index];
            if (!StratsLibrary.Matches(fight, indexQuery))
            {
                continue;
            }

            var row = card.NextRow();
            if (!anchorTaken)
            {
                anchorTaken = true;
                UiAnchors.Report("strats.fight.first", row);
            }

            if (DrawFightRow(drawList, row, card.Bounds, fight, scale))
            {
                OpenFight(fight);
            }
        }

        card.End();
        return cardTop + matches * FightRowHeight * scale;
    }

    private bool DrawFightRow(ImDrawListPtr drawList, Rect row, Rect bounds, ManifestFight fight, float scale)
    {
        var hovered = UiInteract.Hover(new Vector2(bounds.Min.X, row.Min.Y), new Vector2(bounds.Max.X, row.Max.Y));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            drawList.AddRectFilled(new Vector2(bounds.Min.X, row.Min.Y), new Vector2(bounds.Max.X, row.Max.Y),
                ImGui.GetColorU32(ui.HoverWash));
        }

        var cover = FightCover * scale;
        var coverMin = new Vector2(row.Min.X, row.Center.Y - cover * 0.5f);
        DrawCover(drawList, coverMin, cover, fight.Abbrev, scale);
        var chevronCenter = new Vector2(row.Max.X - FightChevronWidth * 0.5f * scale, row.Center.Y);
        AppSkin.Icon(drawList, chevronCenter, ChevronGlyph, ui.MutedInk, FightChevronScale);
        var right = chevronCenter.X - FightChevronWidth * scale;
        if (snapshot.Fights.TryGetValue(fight.Key, out var saved) && saved.ReadingProgress > FightProgressMinimum)
        {
            var ringCenter = new Vector2(right - FightProgressRadius * scale, row.Center.Y);
            DrawProgressRing(drawList, ringCenter, saved.ReadingProgress, scale);
            right = ringCenter.X - FightProgressRadius * scale - Metrics.Space.Sm * scale;
        }

        if (fight.InProgress)
        {
            var badge = Loc.T(L.Strats.InProgress);
            var badgeWidth = InlineBadge.Width(badge, scale);
            InlineBadge.Draw(drawList, right - badgeWidth, row.Center.Y, badge, ui.Accent, scale);
            right -= badgeWidth + Metrics.Space.Sm * scale;
        }

        var textLeft = coverMin.X + cover + FightTextGap * scale;
        var textWidth = MathF.Max(1f, right - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Body);
        var subtitleHeight = fight.Subtitle.Length > 0 ? Typography.LineHeight(TextStyles.Footnote) : 0f;
        var top = row.Center.Y - (titleHeight + subtitleHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(fight.Title, textWidth,
            TextStyles.Body), ui.TitleInk, TextStyles.Body);
        if (subtitleHeight > 0f)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight), Typography.FitText(fight.Subtitle,
                textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        }

        return UiInteract.Click(new Vector2(bounds.Min.X, row.Min.Y), new Vector2(bounds.Max.X, row.Max.Y), hovered);
    }

    private void DrawProgressRing(ImDrawListPtr drawList, Vector2 center, float progress, float scale)
    {
        var radius = FightProgressRadius * scale;
        var thickness = FightProgressThickness * scale;
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, FightProgressTrackAlpha)),
            24, thickness);
        var start = -MathF.PI * 0.5f;
        drawList.PathArcTo(center, radius, start, start + MathF.Tau * Math.Clamp(progress, 0f, 1f), 24);
        drawList.PathStroke(ImGui.GetColorU32(ui.Accent), ImDrawFlags.None, thickness);
    }

    private float DrawNoMatches(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var tile = NoMatchTile * scale;
        var tileMin = new Vector2(origin.X + (width - tile) * 0.5f, origin.Y + SectionGap * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        AppSkin.Icon(drawList, tileMin + new Vector2(tile, tile) * 0.5f, SearchGlyph, AccentRing.Ink, NoMatchGlyph);
        var titleBottom = Typography.DrawWrappedCentered(drawList, Loc.T(L.Strats.NoMatches), TextStyles.Title3,
            ui.TitleInk, new Vector2(origin.X + width * 0.5f, tileMin.Y + tile + Metrics.Space.Lg * scale), width);
        return Typography.DrawWrappedCentered(drawList, Loc.T(L.Strats.NoMatchesHint), TextStyles.Subheadline,
            ui.MutedInk, new Vector2(origin.X + width * 0.5f, titleBottom + Metrics.Space.Xs * scale), width);
    }

    private void DrawIndexState(Rect body)
    {
        var scale = UiScale.Current;
        if (manifestStore.State == StratsState.Failed)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Strats.LoadFailed),
                    Loc.T(L.Strats.LoadFailedHint), Loc.T(L.Strats.Retry)))
            {
                manifestStore.EnsureFresh(true);
            }

            return;
        }

        Skeleton.Rows(ImGui.GetWindowDrawList(),
            new Rect(new Vector2(body.Min.X, body.Min.Y + Metrics.Space.Sm * scale),
                new Vector2(body.Max.X, body.Max.Y - Metrics.Space.Md * scale)), FightRowHeight, Metrics.Space.Md,
            scale);
    }

    private string CreditsText(string siteName)
    {
        if (ReferenceEquals(creditsCulture, Loc.Culture) &&
            string.Equals(creditsSource, siteName, StringComparison.Ordinal))
        {
            return creditsText;
        }

        creditsCulture = Loc.Culture;
        creditsSource = siteName;
        creditsText = Loc.T(L.Strats.PoweredBy, siteName);
        return creditsText;
    }

    private void DrawCreditsRow(StratsManifest manifest, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + CreditsRowHeight * scale));
        var linked = manifest.Credits.SiteUrl.Length > 0;
        var hovered = linked && UiInteract.Hover(row.Min, row.Max);
        var ink = hovered ? ui.BodyInk : ui.MutedInk;
        Typography.DrawCentered(ImGui.GetWindowDrawList(), row.Center,
            Typography.FitText(CreditsText(manifest.Credits.SiteName), width, TextStyles.Footnote), ink,
            TextStyles.Footnote);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            UrlActions.AskThenOpen(manifest.Credits.SiteUrl);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, CreditsRowHeight * scale));
    }
}
